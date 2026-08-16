# Prompt for starting a fresh session

Paste this as the first message of a new session.

---

Working on `MattConroy/rock-band`, branch `claude/rock-band-wikipedia-query-9e9186`.

Read `CLAUDE.md` and `docs/psn-entitlement-research.md` first — the research doc
records what's already been tried and measured for PSN entitlement matching,
including several dead ends I don't want re-attempted.

Context in one line: we're building a **static, maintainer-generated** database
that maps PSN content codes to catalogue songs, so the app can show which Rock
Band DLC someone owns. No runtime PSN queries, nothing collected from users.

**Since last session — item 3 (block interpolation) is done.**

- Wikipedia is reachable now (`/wiki/` and `action=raw`; `api.php` and
  `rest_v1` still 429). `tools/fetch-disc-tracklists.mjs` pulls all nine discs
  into `tools/data/disc-tracklists.json` — 489 tracks, each resolved to exactly
  one catalogue song. This confirmed `source` can't stand in for disc membership:
  41 of 489 are tagged with a different game.
- `generate-entitlement-db.mjs` now emits a **`counters`** table — exact
  `counter -> song id`, unlike `calibration`'s approximate `counter -> date`.
  226 entries for the four known blocks: 205 observed, **21 interpolated for
  songs no dump contains**. Leave-one-out: 149 predicted, 149 correct, zero
  errors; precision holds at 100% with three-quarters of anchors hidden.
- Rock Band 3's block was recorded as "unclear (36/74)" — that was the wrong sort
  key. It sorts by **title** and is 74/74. The keys are lowercased but keep
  punctuation and articles.

The outstanding work, in value order:

1. **Decode the 7-hex layout** (92 codes, ~12%) — the largest remaining gap.
   Give it its own calibration curve the way `dec4` has one. Needs a dump.
2. **Fix pack misclassification** in `gateway/psn.mjs` — `BUNDLE_RE` misses
   artist packs (`RBFOOPACK`, `RBQUEENPA`, …), so ~18 packs are counted as failed
   songs. Regex fix, no dump needed.
3. **Wire the database into the app.** `PsnService.FetchSongsAsync()` still POSTs
   an npsso to the gateway and deserializes `{songs:[...]}` while the gateway
   returns `{items:[...]}`, so it silently yields zero songs. Given the
   no-runtime-PSN decision this should become "load a local dump → match against
   the static database", which also retires the gateway. Nothing yet reads
   `counters` or `calibration` — that consumer doesn't exist. No dump needed.
4. Optional extensions to `counters`: TBRB/GDRB/RB4/BLITZ produced no `dec4`
   anchors at all, so their blocks are unlocated; and every resolved DLC anchor
   is an exact fact that could be added beyond the disc blocks.

**To regenerate the database you need a real PSN entitlement dump**, which is
gitignored and does not survive between sessions — I'll need to re-upload it.
Ask me for it before doing anything that depends on regenerating. Items 2 and 3
don't need it.
