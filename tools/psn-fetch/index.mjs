#!/usr/bin/env node
// Queries the (unofficial) PlayStation Network API for the signed-in account's
// owned entitlements, keeps the ones that look like Rock Band DLC, parses each
// into { artist, title }, and writes songs.json for the Blazor app to read.
//
// Auth uses psn-api (npsso -> access token). The entitlement endpoint is not
// officially documented, so field names can drift; this script is deliberately
// defensive and always writes a raw diagnostics dump so the filter can be tuned
// against your real data (see README).
//
// Required env:
//   PSN_NPSSO            your npsso token (64 chars) from ca.account.sony.com
// Optional env:
//   OUTPUT              output path for songs.json
//                       (default: ../../src/RockBandSpotify/wwwroot/data/songs.json)
//   RAW_DUMP            path to write the raw entitlement list for debugging
//   RB_INCLUDE_REGEX    JS regex; entitlement name must match to be kept
//                       (default: rock ?band — case-insensitive)
//   RB_EXCLUDE_REGEX    JS regex; entitlement name matching this is dropped
//                       (default: rock band 4|rivals|season pass|game|bundle|track pack)
//   MANUAL_SONGS        path to a JSON array of {title,artist} merged into output

import { writeFile, mkdir, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  exchangeNpssoForCode,
  exchangeCodeForAccessToken,
} from "psn-api";

const __dirname = dirname(fileURLToPath(import.meta.url));

const NPSSO = process.env.PSN_NPSSO;
const OUTPUT = process.env.OUTPUT
  ? resolve(process.env.OUTPUT)
  : resolve(__dirname, "../../src/RockBandSpotify/wwwroot/data/songs.json");
const RAW_DUMP = process.env.RAW_DUMP ? resolve(process.env.RAW_DUMP) : null;

const INCLUDE = new RegExp(process.env.RB_INCLUDE_REGEX ?? "rock ?band", "i");
const EXCLUDE = new RegExp(
  process.env.RB_EXCLUDE_REGEX ??
    "rock band 4|rock band rivals|season pass|full game|bundle|track pack|rb4",
  "i",
);

function fail(msg) {
  console.error(`\n✖ ${msg}\n`);
  process.exit(1);
}

if (!NPSSO) {
  fail(
    "PSN_NPSSO is not set. Get it by signing in at https://ca.account.sony.com " +
      "and visiting https://ca.account.sony.com/api/v1/ssocookie — copy the npsso value.",
  );
}

async function authenticate() {
  const accessCode = await exchangeNpssoForCode(NPSSO);
  const authorization = await exchangeCodeForAccessToken(accessCode);
  return authorization.accessToken;
}

// Pull all entitlements, paging until exhausted.
async function fetchEntitlements(accessToken) {
  const all = [];
  const pageSize = 500;
  let start = 0;

  for (;;) {
    const url =
      "https://m.np.playstation.com/api/entitlement/v2/users/me/internal/entitlements" +
      `?start=${start}&size=${pageSize}`;
    const res = await fetch(url, {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: "application/json",
      },
    });

    if (!res.ok) {
      const body = await res.text().catch(() => "");
      throw new Error(
        `Entitlement request failed: ${res.status} ${res.statusText}. ${body.slice(0, 300)}`,
      );
    }

    const data = await res.json();
    const items = data.entitlements ?? data.entitlementList ?? [];
    all.push(...items);

    const total = data.totalResults ?? data.total ?? all.length;
    start += pageSize;
    if (items.length === 0 || all.length >= total) break;
    if (start > 20000) break; // safety valve
  }

  return all;
}

// Probe the several field names PSN has used for a human-readable entitlement name.
function entitlementName(e) {
  return (
    e?.drm_def?.contentName ??
    e?.game_meta?.name ??
    e?.product_name ??
    e?.entitlement_name ??
    e?.name ??
    ""
  );
}

// Rock Band store names look like  "Song Name - Artist"  or  '"Song Name" by Artist'.
// Return { title, artist } doing our best; artist may be empty if unparseable.
function parseSongName(raw) {
  let s = String(raw).trim();
  // Strip a leading "Rock Band -" style prefix if present.
  s = s.replace(/^rock ?band[\s:–-]*\s*/i, "").trim();
  // Strip surrounding quotes on the whole thing.
  s = s.replace(/^["“](.+)["”]$/u, "$1").trim();

  let title = s;
  let artist = "";

  const byMatch = s.match(/^["“]?(.+?)["”]?\s+by\s+(.+)$/i);
  const dashMatch = s.match(/^(.+?)\s+[-–—]\s+(.+)$/);

  if (byMatch) {
    title = byMatch[1].trim();
    artist = byMatch[2].trim();
  } else if (dashMatch) {
    // Ambiguous which side is artist; PSN Rock Band listings are "Title - Artist".
    title = dashMatch[1].trim();
    artist = dashMatch[2].trim();
  }

  title = title.replace(/^["“](.+)["”]$/u, "$1").trim();
  return { title, artist };
}

async function loadManual() {
  if (!process.env.MANUAL_SONGS) return [];
  try {
    const raw = await readFile(resolve(process.env.MANUAL_SONGS), "utf8");
    const arr = JSON.parse(raw);
    return Array.isArray(arr) ? arr : [];
  } catch (err) {
    console.warn(`! Could not read MANUAL_SONGS: ${err.message}`);
    return [];
  }
}

async function main() {
  console.log("→ Authenticating with PSN…");
  const accessToken = await authenticate();

  console.log("→ Fetching entitlements…");
  const entitlements = await fetchEntitlements(accessToken);
  console.log(`  ${entitlements.length} total entitlements`);

  if (RAW_DUMP) {
    await mkdir(dirname(RAW_DUMP), { recursive: true });
    await writeFile(RAW_DUMP, JSON.stringify(entitlements, null, 2));
    console.log(`  raw dump → ${RAW_DUMP}`);
  }

  const songs = [];
  const seen = new Set();
  for (const e of entitlements) {
    const name = entitlementName(e);
    if (!name) continue;
    if (!INCLUDE.test(name)) continue;
    if (EXCLUDE.test(name)) continue;

    const { title, artist } = parseSongName(name);
    if (!title) continue;

    const key = `${artist}|${title}`.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);

    songs.push({
      title,
      artist,
      source: name,
      productId: e?.id ?? e?.sku_id ?? e?.product_id ?? null,
    });
  }

  // Merge any manually-maintained songs (useful for entries PSN names oddly).
  const manual = await loadManual();
  for (const m of manual) {
    if (!m?.title) continue;
    const key = `${m.artist ?? ""}|${m.title}`.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    songs.push({ title: m.title, artist: m.artist ?? "", source: "manual" });
  }

  songs.sort((a, b) =>
    (a.artist || "").localeCompare(b.artist || "") ||
    a.title.localeCompare(b.title),
  );

  const output = {
    generatedAt: new Date().toISOString(),
    source: "psn-entitlements",
    songs,
  };

  await mkdir(dirname(OUTPUT), { recursive: true });
  await writeFile(OUTPUT, JSON.stringify(output, null, 2) + "\n");

  console.log(`\n✔ Wrote ${songs.length} songs → ${OUTPUT}`);
  if (songs.length === 0) {
    console.log(
      "\n! No Rock Band songs matched. Inspect the raw dump and tune " +
        "RB_INCLUDE_REGEX / RB_EXCLUDE_REGEX (see tools/psn-fetch/README.md).",
    );
  }
}

main().catch((err) => fail(err.stack || err.message));
