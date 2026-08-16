#!/usr/bin/env node
// Fetches each Rock Band game's true on-disc tracklist from Wikipedia and
// writes tools/data/disc-tracklists.json.
//
//   node tools/fetch-disc-tracklists.mjs
//
// Maintainer-run and offline, like generate-entitlement-db.mjs. The output is
// committed so the app and the generator never need network access.
//
// WHY THIS EXISTS
// ---------------
// Block interpolation (see docs/psn-entitlement-research.md) needs to know which
// songs shipped on which game disc. The catalogue's `source` field cannot answer
// that: it records where a song *originated*, so the Rock Band 2 disc block comes
// back tagged a mix of RB2, RELOADED and UNPLUGGED, and songs that a later game
// re-used are attributed to the earlier one. Disc membership is a separate fact
// and has to come from a separate source.
//
// Wikipedia's per-game song-list articles are that source. They are cited to
// Harmonix's own published setlists, and the article prose states the disc count
// explicitly — which is what EXPECTED below checks against, so a silent edit or a
// parser regression fails the run instead of quietly producing a short list.

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CATALOGUE_PATH = join(
  __dirname, "..", "src", "RockBandSpotify", "wwwroot", "data", "catalogue.json",
);
const OUTPUT_PATH = join(__dirname, "data", "disc-tracklists.json");

// Wikipedia asks automated clients to identify themselves.
const USER_AGENT =
  "RockBandSpotify-tracklists/1.0 (https://github.com/MattConroy/rock-band) node-fetch";

// One entry per game disc.
//
//   key        the catalogue `source` value for songs that ORIGINATED on this disc
//   page       Wikipedia article holding the tracklist
//   sections   heading -> group label; only on-disc sections, never DLC
//   tables     how many tables to read per section (default: all of them)
//   song       0-based column index of the song title
//   artist     0-based column index of the artist, or `fixedArtist` when the
//              game is single-artist and the column holds the album instead
//   expected   number of TRACKS the table should yield — a tripwire against a
//              parser regression or a reshaped article
//   note       why `expected` differs from the song count in the article prose
//
// Track count and song count are not always the same number. Where a disc ships
// a medley as a single playable track, Wikipedia's prose counts the halves
// separately while the table gives it one row — and the catalogue, like the
// table, holds one entry ("Brain Stew / Jaded"). Tracks are what a content code
// can refer to, so tracks are what this file records.
const GAMES = [
  {
    key: "RB1",
    title: "Rock Band",
    page: "List_of_songs_in_Rock_Band",
    sections: { "Main setlist": "main", "Bonus songs": "bonus" },
    song: 0,
    artist: 1,
    expected: 58,
  },
  {
    key: "RB2",
    title: "Rock Band 2",
    page: "List_of_songs_in_Rock_Band_2",
    sections: { "Track listing": "main" },
    song: 0,
    artist: 1,
    expected: 84,
  },
  {
    key: "RB3",
    title: "Rock Band 3",
    page: "List_of_songs_in_Rock_Band_3",
    sections: { "Track listing": "main" },
    song: 0,
    artist: 1,
    expected: 83,
  },
  {
    key: "LEGO",
    title: "Lego Rock Band",
    page: "List_of_songs_in_Lego_Rock_Band",
    sections: { "Main setlist": "main" },
    song: 1, // column 0 is the career-mode tier
    artist: 2,
    expected: 45,
  },
  {
    key: "TBRB",
    title: "The Beatles: Rock Band",
    page: "List_of_songs_in_The_Beatles:_Rock_Band",
    sections: { "On-disc track listing": "main" },
    song: 0,
    fixedArtist: "The Beatles", // column 1 is the album
    expected: 44,
    note: "Prose says 45 songs; 'Sgt. Pepper's ... / With a Little Help from My Friends' is one track.",
  },
  {
    key: "GDRB",
    title: "Green Day: Rock Band",
    page: "List_of_songs_in_Green_Day:_Rock_Band",
    sections: { "Main setlist": "main" },
    song: 0,
    fixedArtist: "Green Day", // column 1 is the album
    expected: 44,
    note: "Prose says 47 songs; three medley tracks (Brain Stew/Jaded, Are We the Waiting/St. Jimmy, Give Me Novacaine/She's a Rebel).",
  },
  {
    key: "RB4",
    title: "Rock Band 4",
    page: "List_of_songs_in_Rock_Band_4",
    sections: { "Main soundtrack": "main" },
    song: 0,
    artist: 1,
    expected: 65,
  },
  {
    key: "BLITZ",
    title: "Rock Band Blitz",
    page: "Rock_Band_Blitz",
    sections: { Soundtrack: "main" },
    song: 0,
    artist: 1,
    expected: 25,
  },
  {
    key: "UNPLUGGED",
    title: "Rock Band Unplugged",
    page: "Rock_Band_Unplugged",
    sections: { Soundtrack: "main" },
    tables: 1, // a second table in the same section lists the Starter Pack, not the disc
    song: 0,
    artist: 1,
    expected: 41,
  },
];

// Parenthetical annotations Wikipedia may append to a title. The catalogue keeps
// some of them ("Good Vibrations (Live)" is the shipped track name) and drops
// others ("(Cover)" only records that the audio is a re-recording), so these are
// stripped as a fallback rather than up front — the literal title is tried first.
const TITLE_ANNOTATIONS = /\s*\((?:Cover|Live|Album Version|\d{4} Re-record)\)\s*$/i;

// Genuine disagreements between Wikipedia and the catalogue, each checked by
// hand. Keyed by the Wikipedia spelling.
const SONG_ALIASES = {
  "Walkin' on the Sun": "Walking on the Sun", // catalogue uses the -ing spelling
};
const ARTIST_ALIASES = {
  Vagiant: "Tijuana Sweetheart", // the band renamed; the catalogue uses the later name
};

async function fetchWikitext(page) {
  const url = `https://en.wikipedia.org/w/index.php?title=${encodeURIComponent(page)}&action=raw`;
  const res = await fetch(url, { headers: { "User-Agent": USER_AGENT } });
  if (!res.ok) throw new Error(`${page}: HTTP ${res.status}`);
  return res.text();
}

/** Resolves [[link|label]] before templates so pipes inside links can't confuse them. */
function resolveLinks(s) {
  return s
    .replace(/\[\[[^\]|]*\|([^\]]*)\]\]/g, "$1")
    .replace(/\[\[([^\]]*)\]\]/g, "$1");
}

/**
 * Expands templates innermost-first. Most are decoration ({{Yes}}, {{ref}}) and
 * drop out; the ones that wrap real content keep their last argument, which is
 * where the display text sits — {{sort|Main Drag|The Main Drag}} is a sort key
 * plus the artist, and dropping it wholesale would blank the column.
 */
function resolveTemplates(s) {
  const KEEP_LAST = new Set(["sort", "nowrap", "nobr", "small", "sortname"]);
  let prev;
  do {
    prev = s;
    s = s.replace(/\{\{([^{}]*)\}\}/g, (_, inner) => {
      const parts = inner.split("|");
      const name = parts[0].trim().toLowerCase();
      if (name === "'") return "'";
      if (name === "'s") return "'s";
      if (KEEP_LAST.has(name) && parts.length > 1) return parts[parts.length - 1];
      return "";
    });
  } while (s !== prev);
  return s;
}

function clean(s) {
  let t = s
    .replace(/<ref[^>]*\/>/g, "")
    .replace(/<ref[^>]*>[\s\S]*?<\/ref>/g, "")
    .replace(/<!--[\s\S]*?-->/g, "")
    // Hidden sort keys: <span style="display:none">Reaper, Don't Fear The</span>.
    // The text is invisible on the page but would otherwise be concatenated onto
    // the real value.
    .replace(/<span[^>]*display:\s*none[^>]*>[\s\S]*?<\/span>/gi, "");
  t = resolveTemplates(resolveLinks(t));
  return t
    .replace(/'''/g, "") // bold marks master recordings; not data we keep
    .replace(/''/g, "")
    .replace(/<br\s*\/?>/gi, " ")
    .replace(/<[^>]+>/g, "")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&[mn]dash;/g, "-")
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    // Titles are quoted in the source tables, and a medley row quotes each half
    // separately ("A"/"B"), so drop the quote character throughout rather than
    // just at the ends. No Rock Band title contains one.
    .replace(/"/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

/** Splits an article into { heading: body }. */
function sectionsOf(text) {
  const out = {};
  const marks = [];
  for (const m of text.matchAll(/^=+[ \t]*(.+?)[ \t]*=+[ \t]*$/gm)) {
    marks.push({ title: m[1].trim(), start: m.index + m[0].length });
  }
  for (let i = 0; i < marks.length; i++) {
    const end = i + 1 < marks.length ? text.lastIndexOf("\n=", marks[i + 1].start) : text.length;
    out[marks[i].title] = text.slice(marks[i].start, end);
  }
  return out;
}

/**
 * Reads the data rows out of every wikitable in `body`, as arrays of cell text.
 *
 * Rows are delimited by `|-`. A cell starts with `|` or `!` (some tables mark the
 * song title as a row header with `! scope="row"`, so `!` rows are data too) and
 * several cells may share a line separated by `||`. Header rows are dropped by
 * looking for the column captions rather than by position, since tables vary in
 * whether they open with a `|-`.
 */
function parseRows(body, limit = Infinity) {
  const rows = [];
  let seen = 0;
  for (const [table] of body.matchAll(/^\{\|[\s\S]*?^\|\}/gm)) {
    if (++seen > limit) break;
    let cur = null;
    let isHeader = false;
    for (const rawLine of table.split("\n")) {
      const line = rawLine.trim();
      if (line.startsWith("{|") || line.startsWith("|+")) continue;
      if (line.startsWith("|-") || line.startsWith("|}")) {
        if (cur && cur.length && !isHeader) rows.push(cur);
        cur = [];
        isHeader = false;
        continue;
      }
      if (!line.startsWith("|") && !line.startsWith("!")) continue;
      if (cur === null) cur = [];
      // `! scope="col"` is a real column caption; `! scope="row"` is data.
      if (line.startsWith("!") && !/scope\s*=\s*"?row/i.test(line)) isHeader = true;
      for (const cell of line.slice(1).split(line.startsWith("!") ? /\|\||!!/ : "||")) {
        // Drop a leading cell-attribute clause ( style="..." | value ).
        const stripped = cell.replace(/^[^|[{]*?(?:style|scope|align|class|width|colspan|rowspan)\s*=\s*[^|]*\|(?!\|)/i, "");
        cur.push(clean(stripped));
      }
    }
    if (cur && cur.length && !isHeader) rows.push(cur);
  }
  return rows;
}

// --- Reconcile each track against the catalogue -----------------------------
// This is the real correctness check. A row count only proves the parser found
// the right number of rows; resolving all 489 of them to distinct catalogue
// songs proves it read the right text out of them.

const catalogue = JSON.parse(readFileSync(CATALOGUE_PATH, "utf8"));
const norm = (t) => t.replace(/[^A-Za-z0-9]/g, "").toUpperCase();

const byTitle = new Map();
for (const s of catalogue) {
  const k = norm(s.song);
  if (!byTitle.has(k)) byTitle.set(k, []);
  byTitle.get(k).push(s);
}

/** Resolves a (song, artist) pair to exactly one catalogue song, or null. */
function resolve(song, artist) {
  const band = norm(ARTIST_ALIASES[artist] ?? artist);
  const title = SONG_ALIASES[song] ?? song;
  // Literal title first, then again without a trailing annotation.
  for (const candidate of [title, title.replace(TITLE_ANNOTATIONS, "")]) {
    const matches = byTitle.get(norm(candidate)) ?? [];
    if (matches.length === 1) return matches[0];
    const exact = matches.filter((c) => norm(c.artist) === band);
    if (exact.length === 1) return exact[0];
  }
  return null;
}

const games = {};
let failures = 0;
let unresolved = 0;

for (const game of GAMES) {
  const text = await fetchWikitext(game.page);
  const sections = sectionsOf(text);
  const songs = [];

  for (const [heading, group] of Object.entries(game.sections)) {
    if (!(heading in sections)) {
      console.error(`  !! ${game.key}: section "${heading}" not found in ${game.page}`);
      failures++;
      continue;
    }
    for (const cells of parseRows(sections[heading], game.tables)) {
      const song = cells[game.song];
      const artist = game.fixedArtist ?? cells[game.artist];
      if (!song || !artist) continue;
      const match = resolve(song, artist);
      if (!match) {
        console.error(`  !! ${game.key}: no catalogue match for "${song}" by ${artist}`);
        unresolved++;
      }
      songs.push({ id: match?.id ?? null, song, artist, group });
    }
  }

  const counted = songs.length === game.expected;
  if (!counted) failures++;
  const matched = songs.filter((s) => s.id !== null).length;
  console.log(
    `${counted && matched === songs.length ? "ok  " : "FAIL"} ${game.key.padEnd(10)}` +
      ` ${String(songs.length).padStart(3)} tracks, ${String(matched).padStart(3)} matched` +
      `${counted ? "" : `  (expected ${game.expected})`}`,
  );
  games[game.key] = {
    title: game.title,
    source: `https://en.wikipedia.org/wiki/${game.page}`,
    songs,
  };
}

if (failures || unresolved) {
  console.error(
    `\n${failures} game(s) off the expected track count, ${unresolved} track(s) unmatched.\n` +
      `Fix the parser or add an alias rather than lowering an expectation — and only change an\n` +
      `expected count if the article itself now lists a different number of rows.`,
  );
  process.exit(1);
}

mkdirSync(dirname(OUTPUT_PATH), { recursive: true });
writeFileSync(
  OUTPUT_PATH,
  JSON.stringify({ fetchedAt: new Date().toISOString(), games }, null, 2) + "\n",
);

const total = Object.values(games).reduce((n, g) => n + g.songs.length, 0);
console.log(`\n${total} on-disc tracks across ${Object.keys(games).length} games, all resolved`);
console.log(`Wrote ${OUTPUT_PATH}`);
