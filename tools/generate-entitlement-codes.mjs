#!/usr/bin/env node
// Generates the static entitlement-code database shipped with the app
// (src/RockBandSpotify/wwwroot/data/entitlement-codes.json).
//
// This is a maintainer-run, offline tool — NOT something the live app calls.
// It only ever records a (fragment -> catalogue song) entry when a REAL
// entitlement dump actually contains a code that reduces to that fragment,
// and only when that fragment matches exactly one catalogue song. It never
// guesses codes for songs no dump has shown us — see tools/README.md.
//
// Usage:
//   node generate-entitlement-codes.mjs entitlements-raw-1.json entitlements-raw-2.json ...
//
// Reads catalogue songs from src/RockBandSpotify/wwwroot/data/catalogue.json
// and writes src/RockBandSpotify/wwwroot/data/entitlement-codes.json.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { ownedRockBandSongs } from "../gateway/psn.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CATALOGUE_PATH = join(__dirname, "..", "src", "RockBandSpotify", "wwwroot", "data", "catalogue.json");
const OUTPUT_PATH = join(__dirname, "..", "src", "RockBandSpotify", "wwwroot", "data", "entitlement-codes.json");

function fragment(title) {
  const cleaned = title.replace(/[^A-Za-z0-9]/g, "").toUpperCase();
  return (cleaned + "XXXXXXX").slice(0, 7);
}

function fragmentFromCode(code) {
  const rest = code.startsWith("XRB") ? code.slice(3) : code.startsWith("RB") ? code.slice(2) : null;
  return rest && rest.length >= 7 ? rest.slice(0, 7) : null;
}

const dumpPaths = process.argv.slice(2);
if (dumpPaths.length === 0) {
  console.error("Usage: node generate-entitlement-codes.mjs <entitlements-raw.json> [more dumps...]");
  process.exit(1);
}

const catalogue = JSON.parse(readFileSync(CATALOGUE_PATH, "utf8"));
const byFragment = new Map();
for (const song of catalogue) {
  const f = fragment(song.song);
  if (!byFragment.has(f)) byFragment.set(f, []);
  byFragment.get(f).push(song);
}

// Real observed codes, deduped across all input dumps.
const observedCodes = new Map(); // code -> true
for (const path of dumpPaths) {
  const raw = JSON.parse(readFileSync(path, "utf8"));
  const entitlements = Array.isArray(raw) ? raw : raw.entitlements || [];
  const owned = ownedRockBandSongs(entitlements);
  for (const item of owned) {
    if (item.type === "song") observedCodes.set(item.code, true);
  }
  console.log(`${path}: ${entitlements.length} entitlements, ${owned.filter(i => i.type === "song").length} owned song codes`);
}

const database = {};
let confirmed = 0, ambiguous = 0, unmatched = 0;
for (const code of observedCodes.keys()) {
  const f = fragmentFromCode(code);
  if (!f) { unmatched++; continue; }
  const candidates = byFragment.get(f) || [];
  if (candidates.length === 1) {
    database[f] = candidates[0].id;
    confirmed++;
  } else if (candidates.length === 0) {
    unmatched++;
  } else {
    ambiguous++;
  }
}

writeFileSync(
  OUTPUT_PATH,
  JSON.stringify(
    {
      generatedAt: new Date().toISOString(),
      generatedFrom: dumpPaths.length,
      note: "Only fragments actually observed in a real entitlement dump. Never a guess for an unowned song.",
      codes: database,
    },
    null,
    2,
  ),
);

console.log(`\nReal observed codes across ${dumpPaths.length} dump(s): ${observedCodes.size}`);
console.log(`  confirmed (unique catalogue match): ${confirmed}`);
console.log(`  ambiguous (multiple catalogue candidates, left out): ${ambiguous}`);
console.log(`  unmatched (no catalogue candidate or malformed code): ${unmatched}`);
console.log(`\nWrote ${confirmed} entries to ${OUTPUT_PATH}`);
