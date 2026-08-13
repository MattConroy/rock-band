# psn-fetch

Queries the (unofficial) PlayStation Network API for the signed-in account's
owned entitlements, keeps the ones that look like Rock Band DLC, parses each into
`{ title, artist }`, and writes `songs.json` for the Blazor app.

Authentication uses [`psn-api`](https://github.com/achievements-app/psn-api)
(npsso → access token). The entitlement endpoint itself is undocumented, so the
script is defensive and always writes a raw dump for debugging.

## Usage

```bash
npm install
PSN_NPSSO=your-64-char-npsso node index.mjs
```

Get your `npsso`: sign in at <https://ca.account.sony.com>, then open
<https://ca.account.sony.com/api/v1/ssocookie> and copy the `npsso` value.

## Environment variables

| Var | Default | Purpose |
|-----|---------|---------|
| `PSN_NPSSO` | *(required)* | Your npsso token |
| `OUTPUT` | `../../src/RockBandSpotify/wwwroot/data/songs.json` | Where to write `songs.json` |
| `RAW_DUMP` | *(unset)* | Path to dump the raw entitlement list for debugging |
| `RB_INCLUDE_REGEX` | `rock ?band` | Entitlement name must match to be kept |
| `RB_EXCLUDE_REGEX` | `rock band 4\|rivals\|season pass\|full game\|bundle\|track pack\|rb4` | Names matching this are dropped |
| `MANUAL_SONGS` | *(unset)* | Path to a JSON array of `{ "title", "artist" }` merged into the output |

## Tuning

PSN entitlement names aren't perfectly consistent, so the include/exclude regexes
may need one pass against your real data:

1. Run with `RAW_DUMP=./psn-raw.json` (the workflow does this and uploads it as an
   artifact) and open the dump.
2. Find how your Rock Band songs are named (`drm_def.contentName`,
   `game_meta.name`, etc. — the script probes several fields).
3. Widen/narrow `RB_INCLUDE_REGEX` and `RB_EXCLUDE_REGEX` accordingly.
4. For stragglers PSN names oddly, list them in a `MANUAL_SONGS` file.

## Output shape

```json
{
  "generatedAt": "2026-08-13T06:00:00.000Z",
  "source": "psn-entitlements",
  "songs": [
    { "title": "Everlong", "artist": "Foo Fighters", "source": "…", "productId": "…" }
  ]
}
```
