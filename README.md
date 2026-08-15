# Rock Band → Spotify

Build a **Spotify playlist** from your owned **Rock Band DLC** — a
[Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) app served
from **GitHub Pages**, with a tiny stateless gateway for the PlayStation side.

Each visitor logs in with **their own** Spotify and PlayStation accounts.

## How it works

The Spotify half runs entirely in your browser (its API is CORS-enabled, and it
uses PKCE so no secret is needed). The PlayStation half **can't** run in the
browser — PSN doesn't send CORS headers, so browsers refuse to read its responses.
So PSN calls go through a **stateless gateway** (a Cloudflare Worker) that relays
your request server-side and stores nothing.

```
┌──────────────────────────── your browser ───────────────────────────┐
│  Blazor WASM app (GitHub Pages, static)                              │
│                                                                      │
│   Spotify  ──── PKCE login, search, playlist ────►  api.spotify.com  │
│   (direct; Spotify allows browser calls)                            │
│                                                                      │
│   PlayStation ── paste npsso once ──► your Worker ──► PSN API         │
│                 (cached in localStorage)   │ stateless, stores nothing│
│                 ◄──────── song list ───────┘                         │
└──────────────────────────────────────────────────────────────────────┘
```

## Repository layout

| Path | What it is |
|------|------------|
| `src/RockBandSpotify/` | The Blazor WASM app (Spotify + matching + the PSN connect flow) |
| `gateway/` | The stateless Cloudflare Worker that relays PSN calls (`worker.js`) |
| `.github/workflows/deploy.yml` | Builds the app and deploys it to GitHub Pages |

## Setup

### 1. Deploy the gateway (free, no credit card)

```bash
cd gateway
npx wrangler login
npx wrangler deploy --var ALLOWED_ORIGIN:https://<your-username>.github.io
```

Copy the printed Worker URL (e.g. `https://rockband-psn-gateway.<you>.workers.dev`).
See [`gateway/README.md`](gateway/README.md) for details and filter tuning.

### 2. Create a Spotify app

1. [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) → **Create app**.
2. Add Redirect URI: `https://<you>.github.io/<repo>/spotify-connect`
   (and `https://localhost:5001/spotify-connect` for local dev).
3. Copy the **Client ID** (no secret needed — PKCE).

### 3. Configure the app

Edit [`src/RockBandSpotify/wwwroot/appsettings.json`](src/RockBandSpotify/wwwroot/appsettings.json):

```json
{
  "Spotify": { "ClientId": "your-spotify-client-id" },
  "Psn":     { "GatewayUrl": "https://rockband-psn-gateway.you.workers.dev" }
}
```

Both values are safe to commit — neither is sensitive (the Client ID is public by
design; PKCE uses no client secret).

### 4. Enable GitHub Pages

**Settings → Pages → Build and deployment → Source = GitHub Actions**, then push to
the default branch (or run the **Deploy to GitHub Pages** workflow).

### 5. Use it

1. **Connect Spotify** — logs into your Spotify (PKCE).
2. **Connect PlayStation** — click *Open PlayStation login*, sign in, copy the
   `npsso` value it shows, paste it in. It's cached in your browser and lasts
   ~2 months, so you rarely repeat this.
3. **Match songs** — review the matches (low-confidence ones are unchecked).
4. **Sync** — creates/updates one playlist. Re-runs only add what's missing.

## Running locally

```bash
# App
cd src/RockBandSpotify && dotnet run          # https://localhost:5001

# Gateway
cd gateway && npx wrangler dev                 # http://localhost:8787
```

Point `Psn.GatewayUrl` at `http://localhost:8787` for local testing.

## Caveats & honesty

- **The token paste is unavoidable.** We're not the official PlayStation app, so
  we can't capture Sony's login redirect or read its cookie. Best we can do is
  one click to log in + one paste, cached for ~2 months.
- **The PSN API is unofficial** and against Sony's ToS to automate — fine for a
  personal tool; don't put it in front of strangers. Keep the Worker's
  `ALLOWED_ORIGIN` locked to your site.
- **Nothing is stored server-side.** Your npsso lives only in your own browser's
  localStorage and passes through the Worker transiently, never logged or saved.
- **Matching is fuzzy** (covers, live cuts, remasters), which is why you review
  matches before syncing.
