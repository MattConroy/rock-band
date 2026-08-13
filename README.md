# Rock Band → Spotify

Turn your owned **Rock Band DLC** into a **Spotify playlist** — as a static
[Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) app served
from **GitHub Pages**, with no server to run.

## How it works

A page in your browser can't call PSN directly (PSN has no CORS support and needs
your login token, which must never ship to a browser). So the PSN query runs in a
**GitHub Action** instead — that's your "backend", but it's just CI you don't host.

```
┌─────────────────────────────────────────────────────────────┐
│  GitHub Action: "Refresh PSN songs"  (scheduled + manual)    │
│  • uses your npsso token (encrypted GitHub secret)           │
│  • queries the unofficial PSN API for owned entitlements     │
│  • keeps Rock Band DLC, writes & commits songs.json          │
└───────────────────────────────┬─────────────────────────────┘
                                 │ commit triggers deploy
┌────────────────────────────────▼────────────────────────────┐
│  GitHub Pages (static)                                       │
│  • Blazor WASM app + songs.json                              │
└───────────────────────────────┬─────────────────────────────┘
                                 │ runs in your browser
┌────────────────────────────────▼────────────────────────────┐
│  Your browser                                                │
│  • Spotify login via Authorization Code + PKCE (no secret)   │
│  • matches songs → Spotify tracks (review before syncing)    │
│  • create / update one playlist                              │
└─────────────────────────────────────────────────────────────┘
```

## Repository layout

| Path | What it is |
|------|------------|
| `src/RockBandSpotify/` | The Blazor WASM app (all Spotify work happens here, in-browser) |
| `tools/psn-fetch/` | Node script the Action runs to query PSN and write `songs.json` |
| `.github/workflows/deploy.yml` | Builds the app and deploys it to GitHub Pages |
| `.github/workflows/refresh-songs.yml` | Runs `psn-fetch` on a schedule / on demand and commits `songs.json` |
| `src/RockBandSpotify/wwwroot/data/songs.json` | Generated list of your owned songs (starts as sample data) |

## Setup

### 1. Create a Spotify app

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) → **Create app**.
2. Under **Redirect URIs**, add your Pages URL **with a trailing slash**:
   `https://<your-github-username>.github.io/<repo-name>/`
   (for local dev also add `https://localhost:5001/`).
3. Copy the **Client ID**.
4. Put it in [`src/RockBandSpotify/wwwroot/appsettings.json`](src/RockBandSpotify/wwwroot/appsettings.json):

   ```json
   { "Spotify": { "ClientId": "your-client-id-here" } }
   ```

   The Client ID is safe to commit — PKCE needs **no** client secret.

### 2. Add your PSN token as a secret

1. Sign in at <https://ca.account.sony.com>, then open
   <https://ca.account.sony.com/api/v1/ssocookie> and copy the 64-character
   `npsso` value.
2. In the repo: **Settings → Secrets and variables → Actions → New repository
   secret**, name it **`PSN_NPSSO`**, paste the value.

   > npsso tokens expire after roughly two months — refresh this secret when the
   > "Refresh PSN songs" job starts failing at authentication.

### 3. Enable GitHub Pages

**Settings → Pages → Build and deployment → Source = GitHub Actions.**

### 4. Deploy and populate

- Push to the default branch (or run the **Deploy to GitHub Pages** workflow) to
  publish the app.
- Run the **Refresh PSN songs** workflow (Actions tab → *Run workflow*) to query
  PSN and commit your real `songs.json`. That commit re-deploys automatically.

### 5. Use it

Open your Pages URL → **Connect Spotify** → **Match songs** → review the matches
→ **Sync**. Re-running is additive: it only adds tracks the playlist is missing,
so you never get duplicates.

## Tuning the PSN filter

The Rock Band DLC detection in `tools/psn-fetch` is heuristic, because PSN's
entitlement endpoint is unofficial and its naming isn't perfectly consistent. The
refresh workflow uploads a **`psn-entitlements-raw`** artifact (the full raw
entitlement list) on every run. If songs are missing or misparsed:

- Inspect that artifact to see the exact entitlement names.
- Adjust `RB_INCLUDE_REGEX` / `RB_EXCLUDE_REGEX` (env vars in
  `refresh-songs.yml`) to match your data.
- Or maintain a `MANUAL_SONGS` JSON file of `{ "title", "artist" }` entries that
  gets merged in — a robust fallback for anything PSN names oddly.

See [`tools/psn-fetch/README.md`](tools/psn-fetch/README.md) for details.

## Running locally

```bash
# The app
cd src/RockBandSpotify
dotnet run
# → https://localhost:5001  (add this exact URL to your Spotify redirect URIs)

# The PSN fetch (writes songs.json locally)
cd tools/psn-fetch
npm install
PSN_NPSSO=your-npsso-token node index.mjs
```

## Caveats & honesty

- **The PSN API is unofficial.** Sony can change it; the fetch tool may need
  occasional tweaks. That's why raw dumps and a manual-merge escape hatch exist.
- **Matching is fuzzy.** Covers, live cuts, and remasters can mismatch — that's
  why the app makes you review matches (and lets you pick an alternative) before
  syncing. Low-confidence matches are unchecked by default.
- **Nothing sensitive touches the browser.** Your npsso lives only in GitHub
  Actions secrets; Spotify auth uses PKCE with no secret.
