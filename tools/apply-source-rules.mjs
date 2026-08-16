#!/usr/bin/env node
// Recomputes every song's `sources` in catalogue.json from a fixed rule set and
// reports what changed.
//
//   node tools/apply-source-rules.mjs          # report only
//   node tools/apply-source-rules.mjs --write  # rewrite catalogue.json
//
// Idempotent: a second run reports zero changes. Needs
// tools/data/game-tracklists.json (see fetch-game-tracklists.mjs).
//
// WHAT `sources` MEANS
// --------------------
// `sources` lists the full games a song shipped in, MAINLINE FIRST. Most songs
// have a single entry; the 32 that shipped in more than one game carry them all,
// so Everlong is ["RB2", "UNPLUGGED"] — it is on both those games' tracklists.
//
// Index 0 is load-bearing: it is the mainline game the song belongs to, or its
// origin when no mainline shipped it. That makes the array a superset of the old
// scalar, so sorting and grouping by "the" source still work and nothing has to
// guess which entry is the primary one.
//
// What `sources` deliberately does NOT answer:
//   - which games can PLAY the song. Exports move songs between games — 49 of
//     RB1's 58 export to RB2/RB3/RB4 and 9 do not — so playability is a
//     different and much larger relation.
//   - what a PACK grants. That is keyed by pack, and comes from the tracklists.
// Folding either of those in would make the array ambiguous, so they are kept
// out: two songs with identical `sources` can still differ on both.
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
const TRACKLISTS_PATH = join(__dirname, "data", "game-tracklists.json");

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

// song id -> every full game whose tracklist contains it, earliest game first
const gamesOf = new Map();
for (const [game, disc] of Object.entries(tracklists.games)) {
  for (const s of disc.songs) {
    if (!gamesOf.has(s.id)) gamesOf.set(s.id, []);
    gamesOf.get(s.id).push(game);
  }
}
for (const games of gamesOf.values()) {
  games.sort((a, b) => (LAUNCHED[a] < LAUNCHED[b] ? -1 : 1));
}

/** The song's ORIGIN — where it first appeared. Always exactly one value. */
function primarySource(song, current) {
  // Rule 1 — earliest mainline or spinoff game.
  const claimed = (gamesOf.get(song.id) ?? []).find(
    (g) => MAINLINE.includes(g) || SPINOFF.includes(g),
  );
  if (claimed) return claimed;

  // Rules 2, 2b, 2c, 2d — categories that keep their own source.
  if (
    EXCLUSIVE_PACKS.includes(current) ||
    SPINOFF.includes(current) ||
    SIDE_GAMES.includes(current) ||
    NETWORK.includes(current)
  ) {
    return current;
  }

  // Rule 3 — DLC of the mainline game in force at release.
  if (!song.releaseDate) return null;
  for (const [bucket, start] of DLC_ERAS) {
    if (song.releaseDate >= start) return bucket;
  }
  return DLC_ERAS[DLC_ERAS.length - 1][0];
}

/**
 * The full `sources` array. Ordering, in priority order:
 *
 *   1. the mainline game (RB1-RB4), if the song shipped in one
 *   2. otherwise the origin
 *   3. then every remaining game, oldest first
 *
 * Mainline-first is the rule rather than a happy accident. Today it coincides
 * with origin-first — no song on a mainline disc predates that disc, so the
 * mainline is already the oldest entry — but that is a property of the current
 * tracklists, not something the data guarantees. Stating it explicitly means a
 * future game whose tracklist re-uses a mainline song still sorts and groups
 * under the mainline rather than under the newcomer.
 */
function expectedSources(song, current) {
  const primary = primarySource(song, current);
  if (primary === null) return null;
  const games = gamesOf.get(song.id) ?? [];
  const lead = games.find((g) => MAINLINE.includes(g)) ?? primary;
  const rest = [primary, ...games].filter((g, i, a) => g !== lead && a.indexOf(g) === i);
  return [lead, ...rest];
}

const changes = [];
let undecidable = 0;
for (const song of catalogue) {
  // Accepts either shape so the tool can be run against a pre-array catalogue.
  const current = Array.isArray(song.sources) ? song.sources[0] : song.source;
  const have = Array.isArray(song.sources) ? song.sources : current ? [current] : [];
  const want = expectedSources(song, current);
  if (want === null) {
    undecidable++;
    continue;
  }
  if (want.join(",") !== have.join(",")) changes.push({ song, from: have, to: want });
}

if (undecidable) console.log(`${undecidable} song(s) have no release date and were left alone\n`);

if (changes.length === 0) {
  console.log(`No changes — all ${catalogue.length} songs already match the rules.`);
} else {
  const tally = {};
  for (const c of changes) {
    const k = `${c.from.join("+") || "(none)"} -> ${c.to.join("+")}`;
    tally[k] = (tally[k] || 0) + 1;
  }
  console.log(`${changes.length} of ${catalogue.length} songs disagree with the rules:\n`);
  for (const [move, n] of Object.entries(tally).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(n).padStart(4)}  ${move}`);
  }
  const multi = changes.filter((c) => c.to.length > 1).length;
  if (multi) console.log(`\n  (${multi} of these gain a second or third game)`);
}

if (process.argv.includes("--write") && changes.length) {
  for (const c of changes) c.song.sources = c.to;
  // Emit `sources` in the slot `source` used to occupy so the field order stays
  // stable, and drop the old scalar.
  const rewritten = catalogue.map((s) => ({
    id: s.id,
    song: s.song,
    artist: s.artist,
    year: s.year,
    genre: s.genre,
    sources: s.sources ?? (s.source ? [s.source] : []),
    releaseDate: s.releaseDate,
  }));
  // catalogue.json is compact single-line JSON; keep it that way so the diff
  // stays about the data rather than the formatting.
  writeFileSync(CATALOGUE_PATH, JSON.stringify(rewritten));
  console.log(`\nWrote ${CATALOGUE_PATH}`);
} else if (changes.length) {
  console.log(`\n(report only — pass --write to apply)`);
}
