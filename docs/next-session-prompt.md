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

The outstanding work, in value order, is listed at the end of the research doc:

1. Decode the 7-hex code layout (92 codes, ~12%) — give it its own calibration
   curve, the way `dec4` already has one.
2. Fix pack misclassification in `gateway/psn.mjs` (`BUNDLE_RE` misses artist
   packs like `RBFOOPACK`, `RBQUEENPA`) — ~18 codes wrongly counted as failures.
3. Block interpolation, to cover songs no dump contains. Needs per-game on-disc
   tracklists — Wikipedia has them, but check network access first.
4. Wire the database into the app; `PsnService` currently yields zero songs
   because it expects a different response shape than the gateway returns.

**To regenerate the database you need a real PSN entitlement dump**, which is
gitignored and does not survive between sessions — I'll need to re-upload it.
Ask me for it before doing anything that depends on regenerating.
