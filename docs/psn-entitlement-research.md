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
| 2416–2459 | Rock Band 1 | **song title** | 38/39 |
| 2014–2111 | Rock Band 2 | **artist** | 67/70 |
| 1918–1939 | LEGO Rock Band | **artist** | 30/31 |
| 2307–2386 | Rock Band 3 | unclear | 36/74 |

Rock Band 2 has a second alphabetical run from 2103 for its bonus/indie tier,
restarting from A.

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
| Pack | `RB1EXPORTCCF0099` | Expanded via the catalogue's `source` field |
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
3. **Block interpolation** — within a contiguous alphabetical block, positions
   for *unowned* songs can be filled in. This is the only known route to
   entries for songs no dump contains. **The tracklist blocker is cleared** (see
   below); what remains is the interpolation itself, which needs a dump to
   establish each block's counter range.
4. **Wire it into the app.** `PsnService.FetchSongsAsync()` still POSTs an npsso
   to the gateway Worker and deserializes into `SongLibrary` (`{songs:[...]}`),
   but the gateway returns `{items:[{code,id,type}]}` — mismatched shapes, so it
   silently yields zero songs. Given the no-runtime-PSN decision, this path
   should become "load a local dump → match against the static database", which
   also retires the gateway.

---

## On-disc tracklists — the block-interpolation input

Fetched and committed: `tools/data/disc-tracklists.json`, produced by
`tools/fetch-disc-tracklists.mjs` from Wikipedia's per-game song-list articles.
**489 tracks across 9 discs, every one resolved to exactly one catalogue song.**

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
membership: **41 of 489** on-disc tracks are tagged with some other game. The RB2
disc breaks down as `RB2` 53, `UNPLUGGED` 22, `RELOADED` 7, `BLITZ` 2 — the exact
mixing predicted above. A neat single illustration: "Seven" by Vagiant is on the
RB1 disc but carries `source: RELOADED`, and the band is in the catalogue under
its later name, Tijuana Sweetheart.

Two counting traps worth knowing before trusting any figure here:

- **Tracks ≠ songs.** A medley is one playable track with one catalogue entry,
  but Wikipedia's prose counts its halves separately. Green Day's disc is 44
  tracks / "47 songs"; The Beatles' is 44 / "45".
- **`source` totals ≠ disc size** in *both* directions. TBRB shows 73 because
  that count includes its DLC, not because the disc holds 73.

### Still to do for interpolation

The tracklists are the static half. The other half needs a dump: each game's
counter block has to be located (RB1 2416–2459, RB2 2014–2111, LEGO 1918–1939,
RB3 2307–2386 from the last dump), the disc list sorted by that game's sort key
(title for RB1, artist for RB2 and LEGO), and positions assigned across the
block — including the slots for songs the dump does not contain, which is the
whole point.

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
node tools/fetch-disc-tracklists.mjs

# Regenerate the static database from one or more real dumps
node tools/generate-entitlement-db.mjs entitlements-raw.json [more...]
```

`tools/psn-rockband.ps1` produces `entitlements-raw.json` locally (PowerShell 7+),
so the npsso never leaves your machine. Dumps are gitignored and are the one
input that cannot be reconstructed.
