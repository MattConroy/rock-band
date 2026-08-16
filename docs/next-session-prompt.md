# Prompt for starting a fresh session

Paste this as the first message of a new session.

---

Working on `MattConroy/rock-band`, branch `claude/spotify-rockband-playlist-jevclx`.

Read `CLAUDE.md` and `docs/psn-entitlement-research.md` first — the research doc
records what's already been tried and measured for PSN entitlement matching,
including several dead ends I don't want re-attempted.

Context in one line: we're building a **static, maintainer-generated** database
that maps PSN content codes to catalogue songs, so the app can show which Rock
Band DLC someone owns. No runtime PSN queries, nothing collected from users.
Currently identifying 77.5% of real codes.

**Since last session:** the on-disc tracklist blocker on item 3 is cleared.
Wikipedia is reachable now (article and `action=raw` routes — the `api.php` and
`rest_v1` routes still 429). `tools/fetch-disc-tracklists.mjs` fetches all nine
discs into `tools/data/disc-tracklists.json` — 489 tracks, every one resolved to
exactly one catalogue song. That confirmed the `source` field really can't stand
in for disc membership: 41 of the 489 are tagged with a different game, and the
RB2 disc alone splits `RB2` 53 / `UNPLUGGED` 22 / `RELOADED` 7 / `BLITZ` 2.

The outstanding work, in value order:

1. Decode the 7-hex code layout (92 codes, ~12%) — give it its own calibration
   curve, the way `dec4` already has one.
2. Fix pack misclassification in `gateway/psn.mjs` (`BUNDLE_RE` misses artist
   packs like `RBFOOPACK`, `RBQUEENPA`) — ~18 codes wrongly counted as failures.
3. Block interpolation — **the static half is done**; what's left needs a dump.
   Locate each game's counter block, sort its disc list by that game's sort key
   (title for RB1, artist for RB2 and LEGO), and assign positions across the
   block so that songs absent from any dump still get an entry.
4. Wire the database into the app; `PsnService` currently yields zero songs
   because it expects a different response shape than the gateway returns.

**To regenerate the database you need a real PSN entitlement dump**, which is
gitignored and does not survive between sessions — I'll need to re-upload it.
Ask me for it before doing anything that depends on regenerating. Items 1 and 3
both need it; item 2 is a regex fix and item 4 is app wiring, so those two can
be done without it.
