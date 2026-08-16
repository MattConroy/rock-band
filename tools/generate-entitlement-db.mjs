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
//
// On top of those, block interpolation (see DISC_BLOCKS) emits a COUNTERS table:
// exact counter -> song mappings, including for songs no dump has ever contained.
// It needs tools/data/disc-tracklists.json; without it that step is skipped.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { ownedRockBandSongs } from "../gateway/psn.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const DATA = join(__dirname, "..", "src", "RockBandSpotify", "wwwroot", "data");
const CATALOGUE_PATH = join(DATA, "catalogue.json");
const TRACKLISTS_PATH = join(__dirname, "data", "disc-tracklists.json");
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

// Contiguous counter blocks, one per game disc, with the sort key Harmonix used
// inside each. Measured against a real dump: ordering anchors by counter and
// checking adjacent pairs gives RB1 37/37 and RB3 74/74 on lowercased title,
// RB2 67/70 and LEGO 30/31 on lowercased artist. Case and punctuation matter and
// articles do not get stripped — "(Don't Fear) The Reaper" really does sort ahead
// of "29 Fingers", which is how the raw title ordering was confirmed.
//
// `lo`/`hi` bound the block so that a song re-issued later (Dani California sits
// at 2174, far below the RB1 disc block) cannot be mistaken for a block member.
const DISC_BLOCKS = {
  RB1: { lo: 2416, hi: 2473, layout: "dec4", key: (s) => s.song.toLowerCase() },
  RB2: { lo: 2014, hi: 2111, layout: "dec4", key: (s) => s.artist.toLowerCase() },
  RB3: { lo: 2307, hi: 2396, layout: "dec4", key: (s) => s.song.toLowerCase() },
  LEGO: { lo: 1911, hi: 1955, layout: "dec4", key: (s) => s.artist.toLowerCase() },
};

const norm = (t) => t.replace(/[^A-Za-z0-9]/g, "").toUpperCase();

/** Longest strictly-increasing (by `.c`) subsequence, preserving list order. */
function longestIncreasing(items) {
  if (!items.length) return [];
  const tails = [];
  const prev = new Array(items.length).fill(-1);
  for (let i = 0; i < items.length; i++) {
    let lo = 0, hi = tails.length;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (items[tails[mid]].c < items[i].c) lo = mid + 1;
      else hi = mid;
    }
    if (lo > 0) prev[i] = tails[lo - 1];
    if (lo === tails.length) tails.push(i);
    else tails[lo] = i;
  }
  const out = [];
  for (let k = tails[tails.length - 1]; k !== -1; k = prev[k]) out.push(items[k]);
  return out.reverse();
}

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

// --- Table 4: counters (exact counter -> song id), from disc blocks ----------
// The counter is an internal Harmonix song ID, and a game's disc songs occupy one
// contiguous run of them, ordered by that game's sort key. So the songs a dump
// *does* contain pin down positions for the ones it does not: between two anchors
// whose counter gap exactly equals their position gap in the sorted disc list,
// every song in between is determined.
//
// This is the only route to an entry for a song no dump has ever contained. It is
// deliberately conservative — a span whose arithmetic does not close exactly is
// skipped rather than guessed, because it means the block holds entries the disc
// list does not account for (RB3's block spans 90 counters for 83 disc songs).
// Leave-one-out over the anchors predicts 149 of 216 with zero errors; hiding
// random halves and three-quarters of them keeps precision at 100%.
//
// Unlike `calibration`, which yields an approximate date, these are exact.
const byId = new Map(catalogue.map((s) => [s.id, s]));
const counters = {};
const interpolated = {};
let observedCount = 0;
let derivedCount = 0;
let tracklists = null;
try {
  tracklists = JSON.parse(readFileSync(TRACKLISTS_PATH, "utf8"));
} catch {
  console.warn(
    `\n! ${TRACKLISTS_PATH} missing — skipping block interpolation.\n` +
      `  Run: node tools/fetch-disc-tracklists.mjs`,
  );
}

if (tracklists) {
  // counter observed in a dump, per song, for the layouts blocks are defined in
  const observed = new Map();
  for (const code of items.keys()) {
    const d = decodeTitleCode(code);
    if (!d) continue;
    const stripped = d.field.replace(/X+$/, "");
    if (stripped.length < 3) continue;
    const matches = catalogue.filter((s) =>
      stripped.length < d.field.length ? norm(s.song) === stripped : norm(s.song).startsWith(stripped),
    );
    if (matches.length === 1) observed.set(matches[0].id, { counter: d.counter, layout: d.layout });
  }

  for (const [game, block] of Object.entries(DISC_BLOCKS)) {
    const disc = tracklists.games[game];
    if (!disc) continue;
    const table = (counters[block.layout] ||= {});
    const derivedHere = (interpolated[block.layout] ||= []);

    const list = disc.songs
      .map((s) => byId.get(s.id))
      .filter(Boolean)
      .sort((a, b) => (block.key(a) < block.key(b) ? -1 : block.key(a) > block.key(b) ? 1 : 0));

    // Anchors: songs in this block whose counter came from a dump.
    const anchors = [];
    list.forEach((s, i) => {
      const o = observed.get(s.id);
      if (o && o.layout === block.layout && o.counter >= block.lo && o.counter <= block.hi) {
        anchors.push({ i, c: o.counter, id: s.id });
      }
    });
    // A stray anchor out of alphabetical order would drag a whole span with it.
    const keep = longestIncreasing(anchors);
    for (const a of keep) {
      table[a.c] = a.id;
      observedCount++;
    }

    for (let k = 1; k < keep.length; k++) {
      const a = keep[k - 1], b = keep[k];
      if (b.c - a.c !== b.i - a.i) continue; // block holds entries the disc list lacks
      for (let i = a.i + 1; i < b.i; i++) {
        const s = list[i];
        if (observed.has(s.id)) continue;
        const counter = a.c + (i - a.i);
        if (counter in table) continue;
        table[counter] = s.id;
        derivedHere.push(counter);
        derivedCount++;
      }
    }
    derivedHere.sort((x, y) => x - y);
  }
}

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
      counters,
      // Which of `counters` were interpolated rather than seen in a dump, so a
      // suspect mapping can be traced back to the method that produced it.
      interpolated,
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
for (const [layout, table] of Object.entries(counters)) {
  const n = Object.keys(table).length;
  const d = (interpolated[layout] || []).length;
  console.log(`counters ${layout.padEnd(9)} : ${n} exact  (${n - d} observed, ${d} interpolated)`);
}
console.log(`packs               : ${Object.keys(packs).length} -> ${packSongs} songs`);
console.log(`opaque (confirmed)  : ${Object.keys(opaque).length}`);
console.log(`unidentified codes  : ${unidentified.length}  (need a real mapping)`);
console.log(`\nWrote ${OUTPUT_PATH}`);
