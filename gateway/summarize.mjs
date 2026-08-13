#!/usr/bin/env node
// Summarizes an entitlements JSON file ({ count, entitlements } or { error })
// for the Test PSN fetch workflow: total, Rock Band candidates, and what the
// current filter keeps. Reuses the shared parsing logic so it matches the Worker.

import { readFile } from "node:fs/promises";
import {
  entitlementName,
  toSongs,
  DEFAULT_INCLUDE,
  DEFAULT_EXCLUDE,
} from "./psn.mjs";

const file = process.argv[2] || "entitlements.json";
let data;
try {
  data = JSON.parse(await readFile(file, "utf8"));
} catch (err) {
  console.error(`Could not read ${file}: ${err.message}`);
  process.exit(1);
}

if (data.error) {
  console.error(`Gateway/PSN error: ${data.error}`);
  console.error("If it mentions the npsso, it has likely expired — refresh the secret.");
  process.exit(1);
}

const ents = data.entitlements || [];
const rule = (v, d) => new RegExp(process.env[v] || d, "i");
const include = rule("RB_INCLUDE_REGEX", DEFAULT_INCLUDE);
const exclude = rule("RB_EXCLUDE_REGEX", DEFAULT_EXCLUDE);
const bar = "─".repeat(60);

console.log(`Total entitlements: ${data.count ?? ents.length}\n`);

const candidates = [
  ...new Set(ents.map(entitlementName).filter((n) => n && /rock|band/i.test(n))),
].sort();
console.log(bar);
console.log(`Entries containing "rock" or "band" (${candidates.length}):`);
console.log(bar);
for (const n of candidates) console.log("  " + n);

const songs = toSongs(ents, include, exclude);
console.log("\n" + bar);
console.log(`Current filter keeps ${songs.length} songs (title — artist):`);
console.log(bar);
for (const s of songs) console.log(`  ${s.title} — ${s.artist || "(artist unparsed)"}`);
