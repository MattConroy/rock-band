# PSN entitlement matching — research notes

Everything below was verified against a **real 4,664-entitlement PSN dump**
(875 Rock Band items). Numbers are measured, not estimated. This file exists so
the work does not have to be re-derived from scratch, and so the dead ends stay
dead.

---

## The goal

Identify which Rock Band songs a person owns, by matching the content codes in
their PSN entitlements against the 4,953-song catalogue in
`src/RockBandSpotify/wwwroot/data/catalogue.json`.

**Architecture decision (settled):** the database is *static* and
maintainer-generated. The app does not query the PSN API at runtime and does not
collect anything from users. Regenerating is a deliberate offline act — see
`tools/generate-entitlement-db.mjs`.

---

## Ruled out — do not re-attempt

| Approach | Why it fails |
|---|---|
| **Resolving names from the PSN Store** (`store.playstation.com/{region}/product/{id}`, scraping `og:title`) | The store does not consistently server-render titles, and its GraphQL API is a locked persisted-query manifest with no public schema. Tried twice: first in commit `ead4dda`, again later in this research. `tools/psn-names.ps1` is that attempt, kept only for reference. |
| **`rbdb.io` / "Rock Band Database" API** | The site no longer exists. RBDB was real but shut down — the community spreadsheet's own Credits sheet says "This app has since shutdown and even before then had stopped receiving updates." Do not trust search summaries claiming otherwise. |
| **`psn-api` (JS) / `psnawp` (Python) for DLC ownership** | Verified by reading the actual package source, not the docs. Their closest call, `getPurchasedGames()`, is PS4/PS5 **whole games only** — a Sony API limitation, not a library one. Neither reaches individual DLC. |
| **A pre-existing public dataset of song → product IDs** | None found. Harmonix never published one. The most thorough community spreadsheet (`Official_Songs_In_Rock_Band.xlsx`, by PikedPike) chased down obscure per-song data from a dozen sources and still has no product IDs — if such a dataset existed it would be in there. |
| **The "Rock Band / Guitar Hero Songs" download spreadsheet** | Community modding/backup links for jailbroken consoles. Its only IDs are Xbox 360 *title* folder paths, one per **pack**, not per song. Wrong granularity and wrong platform. |

---

## Code format — the core finding

A Rock Band PSN content code is **always 16 characters**. It splits into a
**name field** (the song title, stripped to alphanumerics, uppercased, then
truncated or right-padded with `X`) and a **counter**. The split point varies by
era, and there are at least four layouts:

| Layout | Example | Name field | Counter | Share of real codes |
|---|---|---|---|---|
| `ccf` | `RBALLIWANCCF01FE` | 7 chars | `CCF` + 4 hex | 265 (34%) |
| `dec4` | `RBGIRLSANDBO1926` | 10 chars | 4 decimal | 419 (53%) |
| other/hex | `RBLOBOTOMDC27C00` | 7 chars? | 7 hex | 92 (12%) — **not yet decoded** |
| `RB4`-prefixed | `RB4BLOODDOLLXXXX` | rest of string | none | 8 (1%) |

Padding is meaningful: `CALLMEXX` means the title *ended* there, so it should be
matched **exactly** ("Call Me"), not as a prefix (which wrongly also admits
"Call Me Maybe"). A name field that fills its width was truncated, so it needs a
prefix match.

### The counter is an internal Harmonix song ID

Not a release-order index — that was an early wrong guess. Songs sit in
**contiguous per-game blocks**, and **each block is alphabetically sorted**, but
the sort key differs between games:

| Counter range | Game | Sorted by | Monotonic pairs |
|---|---|---|---|
| 2416–2459 | Rock Band 1 | **song title** | **37/37** |
| 2014–2111 | Rock Band 2 | **artist** | 67/70 |
| 1911–1955 | LEGO Rock Band | **artist** | **30/31** |
| 2307–2396 | Rock Band 3 | **song title** | **74/74** |

Rock Band 2 has a second alphabetical run from 2103 for its bonus/indie tier,
restarting from A.

**Rock Band 3 was previously recorded as "unclear" at 36/74 — that was the wrong
key, not a disordered block.** It sorts by title, and on title it is perfect.
The sort key is the raw field **lowercased**, with punctuation kept and leading
articles *not* stripped:

- `"(Don't Fear) The Reaper"` really does sort ahead of `"29 Fingers"` — the
  opening parenthesis orders before a digit. Strip punctuation and RB1 drops from
  37/37 to 35/37.
- LEGO's artists sort as displayed: Bryan Adams under B, Katrina & the Waves
  under K. Lowercasing matters (30/31 vs 29/31 raw); stripping "The" hurts.

Getting this key exactly right is what makes interpolation safe, so it is worth
re-measuring rather than assuming when a new block turns up.

Because DLC counters *do* climb steadily with release date, a counter can be
interpolated to a release date and used to break ties between songs sharing a
truncated title. Held-out validation (calibrate on half the anchors, test on the
other half): **median 3 days error, 77% within a fortnight**, p90 161 days —
accuracy degrades where anchors are sparse. Each layout needs its **own**
calibration curve; their counter spaces are unrelated.

---

## Three kinds of code, three mechanisms

| Kind | Example | Resolved by |
|---|---|---|
| Title-bearing | `RBALLIWANCCF01FE` | Name index + calibration tie-break |
| Pack | `RB1EXPORTCCF0099` | Expanded via `game-tracklists.json` (Rivals via `sources`) |
| **Opaque** | `PROCKBANDX000012`, `RBRLPBONUSSONG01` | **Nothing derives these.** A bare product number with no title in it. Needs a hand-confirmed mapping. 74 outstanding. |

Packs punch above their weight: 8 pack codes unlock **358 songs**.

---

## Measured results

Against the real dump, 786 title-bearing song codes:

```
unique              524  (66.7%)   name field alone identifies one song
by-date              85  (10.8%)   tie broken by counter -> release date
ambiguous            21  ( 2.7%)
no-match             50  ( 6.4%)   ~18 are artist packs mis-typed as songs
unsupported-layout  106  (13.5%)   the 7-hex layout, mostly

IDENTIFIED          609/786 = 77.5%
```

Catalogue-wide coverage of the derivable name index (independent of any dump):

- width 7: 3,766 of 4,953 songs (76.0%) uniquely keyed
- width 10: 4,386 of 4,953 songs (88.6%) uniquely keyed
- only **34 songs** share both a truncated title and a release date, so nearly
  everything else is separable given an accurate date

---

## Known next steps, in value order

1. **Decode the 7-hex layout** (92 codes, 12%) — give it a calibration curve the
   way `dec4` got one. That change alone took ambiguity from 127 codes to 21.
2. **Fix pack misclassification** in `gateway/psn.mjs` — `BUNDLE_RE` misses
   artist packs (`RBFOOPACK`, `RBQUEENPA`, `RBMAROONP`, `RBAVENGED`, …), so ~18
   packs are counted as failed songs. Regex fix.
3. ~~**Block interpolation**~~ — **done**, see below. 226 exact
   `counter -> song` mappings for the four known blocks, 21 of them for songs no
   dump contains.
4. **Wire it into the app.** `PsnService.FetchSongsAsync()` still POSTs an npsso
   to the gateway Worker and deserializes into `SongLibrary` (`{songs:[...]}`),
   but the gateway returns `{items:[{code,id,type}]}` — mismatched shapes, so it
   silently yields zero songs. Given the no-runtime-PSN decision, this path
   should become "load a local dump → match against the static database", which
   also retires the gateway.

---

## On-disc tracklists — the block-interpolation input

Fetched and committed: `tools/data/game-tracklists.json`, produced by
`tools/fetch-game-tracklists.mjs` from Wikipedia's per-game song-list articles.
**504 tracks across 10 games, every one resolved to exactly one catalogue song.**

| Game | Disc tracks | Tagged with that `source` | Tracks whose `source` differs |
|---|---|---|---|
| RB1 | 58 | 50 | 8 |
| RB2 | 84 | 53 | **31** |
| RB3 | 83 | 83 | 0 |
| LEGO | 45 | 44 | 1 |
| TBRB | 44 | 73 | 0 |
| GDRB | 44 | 44 | 0 |
| RB4 | 65 | 65 | 0 |
| BLITZ | 25 | 25 | 0 |
| UNPLUGGED | 41 | 98 | 1 |

This confirms and quantifies why `source` could never have supplied disc
membership. **The table above is the state before the source rules were applied**
— see the next section; 40 of those 41 were genuine errors and are now fixed, and
the RB1/RB2/LEGO rows now match their disc size exactly. The one remaining
legitimate disagreement is a song first sold as DLC that later appeared on a
spin-off disc.

Two counting traps worth knowing before trusting any figure here:

- **Tracks ≠ songs.** A medley is one playable track with one catalogue entry,
  but Wikipedia's prose counts its halves separately. Green Day's disc is 44
  tracks / "47 songs"; The Beatles' is 44 / "45".
- **`source` totals ≠ disc size** in *both* directions. TBRB shows 73 because
  that count includes its DLC, not because the disc holds 73.

## What `sources` means, and the 40 songs that had it wrong

`sources` is an **array of the full games a song shipped in, origin first**. Most
songs have one entry; 32 have two or three — Everlong is `["RB2","UNPLUGGED"]`.
Index 0 is the origin, so the array stays a superset of the old scalar and
sorting or grouping by "the" source still works. The rules live in
`tools/apply-source-rules.mjs`, which is idempotent and so doubles as a guard.

It answers **membership only**. Two relations were deliberately left out because
folding them in would make the array ambiguous:

- **Playability.** Exports move songs between games — 49 of RB1's 58 export to
  RB2/RB3/RB4 and 9 do not. Enter Sandman and Learn to Fly have identical
  `sources` and differ 1-vs-4 on playability.
- **Pack contents.** Keyed by pack, and derived from the tracklists.

Validating them against the tracklists turned up three things worth keeping:

- **"No song is on two discs" is false — 23 are.** Harmless, though: every
  collision is a mainline/spin-off disc against a *side game* (RB2 ↔ Unplugged
  22, Blitz ↔ RB2 2, Blitz ↔ Unplugged 1), never mainline↔mainline. Earliest-wins
  therefore always picks the game the rule intends anyway.
- **"Shipped there first" is exactly true for mainline and spin-off discs.** Of
  the 419 songs across RB1–RB4, LEGO, TBRB and GDRB, *zero* have a release date
  earlier than their disc. Only the side games re-use (Unplugged 32 of 41, Blitz
  2 of 25) — precisely why they must lose the tiebreak.
- **The code layout is not an era marker.** Tempting, but false: layouts overlap
  for years (`dec4` spans 2007–2019; 2010 alone has ccf 27, dec4 78, 7hex 14).
  Only the counter inside the code tracks release order.

The corrections, all from the first rule:

| Move | Songs |
|---|---|
| `UNPLUGGED -> RB2` | 22 |
| `RELOADED -> RB1` | 8 |
| `RELOADED -> RB2` | 7 |
| `BLITZ -> RB2` | 2 |
| `RELOADED -> LEGO` | 1 |

Two near-misses the rules had to be sharpened to avoid:

- **Beatles DLC.** 29 TBRB songs aren't on the TBRB disc, and a naive era rule
  files them as `RB2_DLC`. Wikipedia is explicit that Beatles DLC "was not
  playable in the other games", so spin-off-exclusive DLC stays with its spin-off.
- **"Exclusive" has to mean *at launch*.** Country Track Pack 2 and the Rivals
  songs were both sold individually later, so an exclusive-*forever* test would
  dissolve them into DLC. At-launch keeps them, and still correctly excludes the
  retail compilations (Track Pack Vol. 1 was assembled from existing DLC).

Rock Band Network was left alone and checks out as a clean partition: none of its
1,923 songs is on any disc, and no song+artist pair exists under both an RBN and
a non-RBN source.

### Knock-on: packs no longer expand through `source`

Disc-export packs used to be expanded by matching `source`, which only worked
while `source` doubled as disc membership. They now read the tracklists directly:

| Pack | Was | Now | True disc |
|---|---|---|---|
| `RB1EXPORT` | 50 | **58** | 58 |
| `RBRB2EXPO` | 53 | **84** | 84 |
| `RBLRBEXPO` / `RBLRBXKEY` | 44 | **45** | 45 |
| `RBUNPLUGG` | 98 | **41** | 41 |
| `RBBLITZ00` | 25 | **25** | 25 |

`RBUNPLUGG` was the worst, granting Unplugged's DLC as well as its disc.
`RBEXPANSI` (Rivals) has no disc and stays keyed on `source`.

## Block interpolation — done

`generate-entitlement-db.mjs` now emits a **`counters`** table: exact
`counter -> song id` mappings, as opposed to `calibration`'s approximate
`counter -> date`. Against the reference dump it holds **226 entries for the four
known blocks — 205 observed in the dump, 21 interpolated for songs the dump does
not contain**, which is the first time songs nobody owns have entries at all.

### How it works, and why it is conservative

Sort a game's disc list by its sort key. Songs the dump *does* contain pin down
positions for the ones it does not: where two anchors have a counter gap exactly
equal to their position gap, every song between them is determined.

A span whose arithmetic does not close is **skipped, not guessed**. That happens
because blocks contain entries the disc list cannot account for — RB3's block
spans 90 counters for 83 disc songs, a drift of exactly 7. Anchors that break
alphabetical order are dropped first, by keeping the longest strictly increasing
subsequence, so one stray code cannot drag a whole span with it.

### Validation

Leave-one-out over all 216 anchors: **149 predicted, 149 correct, zero errors.**
Because real gaps cluster (leave-one-out always leaves tight neighbours), that
overstates recall, so precision was re-checked while hiding anchors in bulk:

| Anchors hidden | Recall | Precision |
|---|---|---|
| 25% | 62.4% | 99.7% (2 wrong of 649) |
| 50% | 53.5% | 100% |
| 75% | 42.2% | 100% |

Recall degrades with sparsity, as expected; precision does not. That asymmetry is
the point of the design — it emits nothing it cannot pin down.

Interpolated entries slot cleanly into their neighbourhoods, which is the easiest
sanity check to repeat by eye:

```
2416   (Don't Fear) The Reaper      2027   Dinosaur Jr.
2417   29 Fingers                   2028   Disturbed
2418 * Are You Gonna Be My Girl     2029 * Dream Theater
2419   Ballroom Blitz               2030   Duran Duran
2420   Black Hole Sun               2031   Elvis Costello
```

`entitlements.json` records which counters were interpolated (the `interpolated`
key) so a suspect mapping can be traced back to the method that produced it.

### What would extend it

- Only four blocks are mapped. TBRB, GDRB, RB4 and BLITZ produced **no `dec4`
  anchors at all** in this dump, so their blocks are unlocated — those games'
  codes presumably use another layout.
- The 7-hex layout is still undecoded, which is the largest remaining gap.
- `counters` currently covers disc blocks only. Every resolved DLC anchor is also
  an exact fact and could be added, at the cost of a larger table.

## Environment notes

Network egress is **no longer** as restricted as it was during the original
research. Re-verified 2026-08-16:

- **Now works:** `en.wikipedia.org` article fetches, both `/wiki/<page>` and
  `/w/index.php?title=<page>&action=raw`. The raw route is what the tracklist
  tool uses — parsing wikitext is far steadier than scraping rendered HTML.
- **Still blocked:** `en.wikipedia.org/w/api.php` and `/api/rest_v1/…` both
  return **HTTP 429** regardless of `User-Agent`. Only the two routes above are
  usable, so don't reach for the MediaWiki API.
- Previously recorded as blocked and **not** re-tested: `store.playstation.com`,
  `codeload.github.com`, `github.com`, `api.github.com`.
- `WebSearch` works (server-side) while `WebFetch`/`curl` are proxied. Search
  results therefore **cannot be verified** — this is exactly how the phantom
  `rbdb.io` lead got through. Treat unverifiable search summaries with suspicion.

---

## Reproducing

```bash
# Refresh the on-disc tracklists from Wikipedia (needs network; output committed)
node tools/fetch-game-tracklists.mjs

# Regenerate the static database from one or more real dumps
node tools/generate-entitlement-db.mjs entitlements-raw.json [more...]
```

`tools/psn-rockband.ps1` produces `entitlements-raw.json` locally (PowerShell 7+),
so the npsso never leaves your machine. Dumps are gitignored and are the one
input that cannot be reconstructed.
