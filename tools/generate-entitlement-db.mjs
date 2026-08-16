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

/** Splits a title-bearing code into its name field and release-order counter. */
function decodeTitleCode(code) {
  if (code.length !== 16 || !code.startsWith("RB")) return null;
  const body = code.slice(2);
  if (body.slice(7, 10) !== "CCF") return null; // only this layout is calibrated
  const counter = parseInt(body.slice(10, 14), 16);
  return Number.isNaN(counter) ? null : { field: body.slice(0, 7), counter };
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

// --- Table 1: calibration (counter -> release date) -------------------------
const calibration = [];
for (const code of items.keys()) {
  const d = decodeTitleCode(code);
  if (!d) continue;
  const stripped = d.field.replace(/X+$/, "");
  if (stripped.length < 3) continue;
  const matches = catalogue.filter((s) =>
    stripped.length < d.field.length ? norm(s.song) === stripped : norm(s.song).startsWith(stripped),
  );
  if (matches.length === 1 && matches[0].releaseDate && titleCounts.get(norm(matches[0].song)) === 1) {
    calibration.push([d.counter, matches[0].releaseDate]);
  }
}
calibration.sort((a, b) => a[0] - b[0]);

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
console.log(`\ncalibration anchors : ${calibration.length}`);
console.log(`packs               : ${Object.keys(packs).length} -> ${packSongs} songs`);
console.log(`opaque (confirmed)  : ${Object.keys(opaque).length}`);
console.log(`unidentified codes  : ${unidentified.length}  (need a real mapping)`);
console.log(`\nWrote ${OUTPUT_PATH}`);
