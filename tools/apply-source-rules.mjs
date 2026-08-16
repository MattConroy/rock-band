#!/usr/bin/env node
// Recomputes every song's `source` in catalogue.json from a fixed rule set and
// reports what changed.
//
//   node tools/apply-source-rules.mjs          # report only
//   node tools/apply-source-rules.mjs --write  # rewrite catalogue.json
//
// Idempotent: a second run reports zero changes. Needs
// tools/data/disc-tracklists.json (see fetch-disc-tracklists.mjs).
//
// WHAT `source` MEANS
// -------------------
// `source` answers "where did this song FIRST appear". It is not "which discs
// hold it" and it is not "what does this pack grant" — those are different
// questions with different answers, and conflating them is what put 40 songs in
// the wrong bucket. Pack contents come from disc-tracklists.json instead.
//
// THE RULES, in precedence order
// ------------------------------
// 1. If the song shipped on a MAINLINE or SPINOFF game disc, it belongs to the
//    earliest such game.
//
//    Verified against the tracklists: no song on an RB1/RB2/RB3/RB4/LEGO/TBRB/
//    GDRB disc has a release date earlier than that disc, so "first appearance"
//    and "on the disc" never disagree for these games. 23 songs sit on more than
//    one disc, but every collision is mainline/spinoff vs a side game, so the
//    earliest-wins tiebreak always picks the mainline/spinoff one.
//
// 2. Retail packs and expansions whose songs were EXCLUSIVE AT LAUNCH keep their
//    own source: ACDC_TP, CTP2, RIVALS. "At launch" is the workable reading —
//    Country Track Pack 2 and the Rivals songs were both sold individually
//    later, so an "exclusive forever" test would dissolve them, while the retail
//    compilations (Track Pack Vol. 1 and friends) were assembled from existing
//    DLC and never qualify under either reading. Checked: neither exclusive pack
//    contains a song that shipped on a mainline or spinoff disc.
//
// 2b. Spinoff-exclusive DLC stays with its spinoff. The Beatles: Rock Band sold
//    29 songs of album DLC that Wikipedia is explicit were "not playable in the
//    other games", so filing them as RB2-era DLC would be wrong.
//
// 2c. Side games keep their own sources for songs exclusive to them. Unplugged,
//    Blitz and Reloaded were standalone releases, not DLC of a mainline game.
//    (Their songs that DO appear on a mainline disc are caught by rule 1 — that
//    is where all 40 corrections come from.)
//
// 2d. Rock Band Network keeps its own sources. RBN was a separate,
//    community-authored channel. Verified: no RBN song is on any disc, and no
//    song+artist pair exists under both an RBN and a non-RBN source.
//
// 3. Everything else is DLC, belonging to the mainline game in force on its
//    release date.
//
//    Note this rule currently changes nothing: all 2,371 songs already in a
//    *_DLC bucket are in the right era, to the day. It is encoded so the
//    boundary is checked rather than assumed.
//
// NOT USED: the entitlement code's layout. It is tempting to read the format as
// an era marker, but the layouts coexist for years (dec4 spans 2007-2019, and
// 2010 alone has ccf, dec4 and 7hex), so it cannot date a song. The counter
// inside the code does track release order — that is what `calibration` uses.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CATALOGUE_PATH = join(
  __dirname, "..", "src", "RockBandSpotify", "wwwroot", "data", "catalogue.json",
);
const TRACKLISTS_PATH = join(__dirname, "data", "disc-tracklists.json");

const MAINLINE = ["RB1", "RB2", "RB3", "RB4"];
const SPINOFF = ["TBRB", "GDRB", "LEGO"];
const EXCLUSIVE_PACKS = ["ACDC_TP", "CTP2", "RIVALS"];
const SIDE_GAMES = ["UNPLUGGED", "BLITZ", "RELOADED"];
const NETWORK = ["RBN1", "RBN2"];

// Retail launch dates, used to order multi-disc songs and to bound DLC eras.
const LAUNCHED = {
  RB1: "2007-11-20",
  RB2: "2008-09-14",
  UNPLUGGED: "2009-06-11",
  TBRB: "2009-09-09",
  LEGO: "2009-11-03",
  GDRB: "2010-06-08",
  RB3: "2010-10-26",
  BLITZ: "2012-08-28",
  RB4: "2015-10-06",
};

// A DLC song belongs to the mainline game in force when it released. Newest
// first so the first match wins.
const DLC_ERAS = [
  ["RB4_DLC", LAUNCHED.RB4],
  ["RB3_DLC", LAUNCHED.RB3],
  ["RB2_DLC", LAUNCHED.RB2],
  ["RB1_DLC", LAUNCHED.RB1],
];

const catalogue = JSON.parse(readFileSync(CATALOGUE_PATH, "utf8"));
const tracklists = JSON.parse(readFileSync(TRACKLISTS_PATH, "utf8"));

// song id -> the discs it shipped on, earliest game first
const discsOf = new Map();
for (const [game, disc] of Object.entries(tracklists.games)) {
  for (const s of disc.songs) {
    if (!discsOf.has(s.id)) discsOf.set(s.id, []);
    discsOf.get(s.id).push(game);
  }
}
for (const games of discsOf.values()) {
  games.sort((a, b) => (LAUNCHED[a] < LAUNCHED[b] ? -1 : 1));
}

function expectedSource(song) {
  // Rule 1 — earliest mainline or spinoff disc.
  const claimed = (discsOf.get(song.id) ?? []).find(
    (g) => MAINLINE.includes(g) || SPINOFF.includes(g),
  );
  if (claimed) return claimed;

  // Rules 2, 2b, 2c, 2d — categories that keep their own source.
  if (
    EXCLUSIVE_PACKS.includes(song.source) ||
    SPINOFF.includes(song.source) ||
    SIDE_GAMES.includes(song.source) ||
    NETWORK.includes(song.source)
  ) {
    return song.source;
  }

  // Rule 3 — DLC of the mainline game in force at release.
  if (!song.releaseDate) return null;
  for (const [bucket, start] of DLC_ERAS) {
    if (song.releaseDate >= start) return bucket;
  }
  return DLC_ERAS[DLC_ERAS.length - 1][0];
}

const changes = [];
let undecidable = 0;
for (const song of catalogue) {
  const want = expectedSource(song);
  if (want === null) {
    undecidable++;
    continue;
  }
  if (want !== song.source) changes.push({ song, from: song.source, to: want });
}

if (undecidable) console.log(`${undecidable} song(s) have no release date and were left alone\n`);

if (changes.length === 0) {
  console.log(`No changes — all ${catalogue.length} sources already match the rules.`);
} else {
  const tally = {};
  for (const c of changes) {
    const k = `${c.from} -> ${c.to}`;
    tally[k] = (tally[k] || 0) + 1;
  }
  console.log(`${changes.length} of ${catalogue.length} songs have a source the rules disagree with:\n`);
  for (const [move, n] of Object.entries(tally).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(n).padStart(3)}  ${move}`);
  }
  console.log();
  for (const c of changes) {
    console.log(
      `  ${c.from.padEnd(10)} -> ${c.to.padEnd(6)} ${c.song.song.slice(0, 42).padEnd(43)} ${c.song.artist.slice(0, 26)}`,
    );
  }
}

if (process.argv.includes("--write") && changes.length) {
  for (const c of changes) c.song.source = c.to;
  // catalogue.json is compact single-line JSON; keep it that way so the diff
  // stays about the data rather than the formatting.
  writeFileSync(CATALOGUE_PATH, JSON.stringify(catalogue));
  console.log(`\nWrote ${CATALOGUE_PATH}`);
} else if (changes.length) {
  console.log(`\n(report only — pass --write to apply)`);
}
