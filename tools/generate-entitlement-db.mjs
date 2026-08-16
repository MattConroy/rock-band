#!/usr/bin/env node
// Generates the static entitlement database shipped with the app
// (src/RockBandSpotify/wwwroot/data/entitlements.json).
//
// Maintainer-run and offline. The live app never writes to this — see
// tools/README.md. Regenerate by re-running with more dumps.
//
//   node generate-entitlement-db.mjs <entitlements-raw.json> [more dumps...]
//
// PSN Rock Band content codes come in three kinds, and each needs a different
// table because only one of them can be derived from the catalogue alone:
//
//   1. Title-bearing  RBALLIWANCCF01FE
//      Carries a truncated title plus a release-order counter. Resolvable at
//      runtime against catalogue.json, so this file ships only the CALIBRATION
//      curve (counter -> release date) that the decoder needs to break ties
//      between songs sharing a truncated title.
//
//   2. Opaque          PROCKBANDX000012, RBRLPBONUSSONG01
//      A bare product number with no title in it whatsoever. Nothing can derive
//      these; they exist in OPAQUE only when a real dump has shown us one, and
//      even then we can only record it if some other signal identified the song.
//
//   3. Pack            RB1EXPORTCCF0099
//      A single entitlement granting many songs. Expanded via the catalogue's
//      own `source` field, so one purchase unlocks its whole tracklist.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { ownedRockBandSongs } from "../gateway/psn.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const DATA = join(__dirname, "..", "src", "RockBandSpotify", "wwwroot", "data");
const CATALOGUE_PATH = join(DATA, "catalogue.json");
const OUTPUT_PATH = join(DATA, "entitlements.json");

// Pack code prefix -> the catalogue `source` value(s) it grants. Hand-maintained:
// a pack's contents are a fact about what Harmonix sold, not something derivable
// from an entitlement code.
const PACK_SOURCES = {
  RB1EXPORT: ["RB1"],
  RBRB2EXPO: ["RB2"],
  RBLRBEXPO: ["LEGO"],
  RBLRBXKEY: ["LEGO"],
  RBUNPLUGG: ["UNPLUGGED"],
  RBGDBONUS: ["GDRB"],
  RBBLITZ00: ["BLITZ"],
  RBEXPANSI: ["RIVALS"],
};

const norm = (t) => t.replace(/[^A-Za-z0-9]/g, "").toUpperCase();

/**
 * Splits a title-bearing code into its layout, name field, and release-order
 * counter. Two layouts are understood, each with its own counter space (so each
 * needs its own calibration curve — the numbers are not comparable):
 *
 *   ccf   RBALLIWANCCF01FE   name(7)  + "CCF" + 4 hex
 *   dec4  RBGIRLSANDBO1926   name(10) +         4 decimal
 */
function decodeTitleCode(code) {
  if (code.length !== 16 || !code.startsWith("RB")) return null;
  const body = code.slice(2);
  if (body.slice(7, 10) === "CCF") {
    const counter = parseInt(body.slice(10, 14), 16);
    return Number.isNaN(counter) ? null : { layout: "ccf", width: 7, field: body.slice(0, 7), counter };
  }
  if (/^\d{4}$/.test(body.slice(10, 14))) {
    const counter = parseInt(body.slice(10, 14), 10);
    // Counter 0 is a placeholder rather than a real release position.
    return counter === 0 ? null : { layout: "dec4", width: 10, field: body.slice(0, 10), counter };
  }
  return null;
}

const dumps = process.argv.slice(2);
if (dumps.length === 0) {
  console.error("Usage: node generate-entitlement-db.mjs <entitlements-raw.json> [more...]");
  process.exit(1);
}

const catalogue = JSON.parse(readFileSync(CATALOGUE_PATH, "utf8"));

// Songs whose normalized title is unique catalogue-wide are safe calibration
// anchors: their code maps to exactly one song without needing a date at all.
const titleCounts = new Map();
for (const s of catalogue) {
  const n = norm(s.song);
  titleCounts.set(n, (titleCounts.get(n) || 0) + 1);
}

const items = new Map();
for (const path of dumps) {
  const raw = JSON.parse(readFileSync(path, "utf8"));
  const ents = Array.isArray(raw) ? raw : raw.entitlements || [];
  const owned = ownedRockBandSongs(ents);
  for (const it of owned) items.set(it.code, it);
  console.log(`${path}: ${ents.length} entitlements -> ${owned.length} Rock Band items`);
}

// --- Table 1: calibration (counter -> release date), one curve per layout ---
// Anchored only on codes whose name alone already identifies exactly one song,
// so the curve is never fitted to a guess.
const calibration = {};
for (const code of items.keys()) {
  const d = decodeTitleCode(code);
  if (!d) continue;
  const stripped = d.field.replace(/X+$/, "");
  if (stripped.length < 3) continue;
  const matches = catalogue.filter((s) =>
    stripped.length < d.field.length ? norm(s.song) === stripped : norm(s.song).startsWith(stripped),
  );
  if (matches.length === 1 && matches[0].releaseDate && titleCounts.get(norm(matches[0].song)) === 1) {
    (calibration[d.layout] ||= []).push([d.counter, matches[0].releaseDate]);
  }
}
for (const curve of Object.values(calibration)) curve.sort((a, b) => a[0] - b[0]);

// --- Table 0: name index (name field -> song ids), covering EVERY song ------
// The name field is a pure function of the title, so this is derivable for the
// whole catalogue — not just songs some dump happened to contain. Both known
// field widths are emitted because the layouts differ by era. A field mapping
// to one id is an outright answer; several ids means the runtime must break the
// tie with the release-order counter and `calibration`.
const index = {};
for (const width of [7, 10]) {
  const table = {};
  for (const s of catalogue) {
    const n = norm(s.song);
    const field = n.length >= width ? n.slice(0, width) : n.padEnd(width, "X");
    (table[field] ||= []).push(s.id);
  }
  index[width] = table;
}

// --- Table 2: packs (pack code -> song ids) ---------------------------------
const packs = {};
for (const [prefix, sources] of Object.entries(PACK_SOURCES)) {
  const ids = catalogue.filter((s) => sources.includes(s.source)).map((s) => s.id);
  if (ids.length) packs[prefix] = ids;
}

// --- Table 3: opaque (literal code -> song id) ------------------------------
// Codes carrying NO title text at all — just a product number or a slot label.
// Nothing can derive these from the catalogue, so they are the one thing that
// genuinely requires a hand-confirmed mapping. Codes that do carry a title (in
// any layout) are the runtime decoder's job and deliberately absent here.
const OPAQUE_PATTERNS = [
  /^PROCKBAND/,        // PROCKBANDX000012 — bare RB4-era product number
  /RLPBONUSSONG/,      // Rivals bonus slots
  /ANNSONG/,           // anniversary song slots
  /WEEK\d+SONG/,       // weekly release slots
  /PASS.*SONG/,        // season-pass slots
  /^S\d+PASS/,
  /BONUS(GUITAR|SHIRT)/,
];
const isOpaque = (code) => OPAQUE_PATTERNS.some((re) => re.test(code));

const opaque = {};
const unidentified = [];
for (const code of items.keys()) {
  if (!isOpaque(code)) continue;
  if (!(code in opaque)) unidentified.push(code);
}
unidentified.sort();

writeFileSync(
  OUTPUT_PATH,
  JSON.stringify(
    {
      generatedAt: new Date().toISOString(),
      generatedFromDumps: dumps.length,
      index,
      calibration,
      packs,
      opaque,
      unidentified,
    },
    null,
    2,
  ),
);

const packSongs = new Set(Object.values(packs).flat()).size;
for (const width of [7, 10]) {
  const t = index[width];
  const solo = Object.values(t).filter((v) => v.length === 1).length;
  console.log(`index width ${width}      : ${Object.keys(t).length} fields, ${solo} songs keyed outright`);
}
for (const [layout, curve] of Object.entries(calibration)) {
  console.log(`calibration ${layout.padEnd(6)} : ${curve.length} anchors  (${curve[0][1]} -> ${curve[curve.length - 1][1]})`);
}
console.log(`packs               : ${Object.keys(packs).length} -> ${packSongs} songs`);
console.log(`opaque (confirmed)  : ${Object.keys(opaque).length}`);
console.log(`unidentified codes  : ${unidentified.length}  (need a real mapping)`);
console.log(`\nWrote ${OUTPUT_PATH}`);
