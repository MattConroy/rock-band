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
| `src/RockBandSpotify/` | The Blazor WASM app — song catalogue, Spotify matching, PSN connect flow |
| `src/RockBandSpotify/wwwroot/data/catalogue.json` | Every Rock Band song, with the ids that identify it on each store |
| `gateway/` | The stateless Cloudflare Worker that relays PSN calls (`worker.js`) |
| `tests/RockBandSpotify.UnitTests/` | xUnit tests for the filtering, sorting, matching and resolving logic |
| `tests/RockBandSpotify.EndToEndTests/` | Playwright tests that drive the app in a real browser |
| `.github/workflows/Build.yml` | Builds, then runs both test suites and a gateway syntax check |
| `.github/workflows/Publish.yml` | Deploys the app to GitHub Pages and the Worker to Cloudflare |

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
2. Add Redirect URI: `https://<you>.github.io/<repo>/spotify-connect`. The match
   is exact — a trailing slash makes it a different URI. For local development
   add `https://127.0.0.1:5001/spotify-connect` as well; Spotify no longer
   accepts `localhost` as a redirect host.
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
the default branch (or run the **Publish** workflow by hand).

### 5. Use it

The catalogue is the landing page and needs no login at all: every Rock Band song,
searchable and filterable by genre and by the game it came from. Connecting
PlayStation adds an ownership column so you can filter it down to what you have.

To build a playlist:

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

## The catalogue

`src/RockBandSpotify/wwwroot/data/catalogue.json` is the one committed dataset:
4,953 songs, each with the game or games it shipped in and the ids that identify
it elsewhere.

| Field | Coverage | What it is |
|-------|----------|------------|
| `psnIds` | 2,961 | PlayStation Store content codes that grant the song |
| `spotifyId` | 3,401 | The Spotify track the song is |

Much of it is compiled from the public song list published by
[rb4.app](https://rb4.app), whose entries link to each song's Store page. Thanks
to them for maintaining it.

**`psnIds` stores only the content code** — the last segment of a PSN product id
— because that is the region-independent part: a US listing and a European
entitlement for the same song differ in their prefix but agree on that segment.
A song can have several, because a code can be a single purchase, a pack, or a
whole game: every track on the Rock Band 3 disc carries that disc's export code,
since those songs never had store pages of their own.

The songs without ids are mostly not a gap in the data but a property of the
stores. Nearly two thirds of the catalogue is Rock Band Network — user-authored
tracks by unsigned bands, long delisted, many never released anywhere else.
Excluding those and the Beatles content, 2,784 of 2,957 songs have a PlayStation
id. The rest are the handful of disc tracks whose licences kept them out of every
export, and the AC/DC track pack, which had no store release at all.

## Caveats & honesty

- **The token paste is unavoidable.** We're not the official PlayStation app, so
  we can't capture Sony's login redirect or read its cookie. Best we can do is
  one click to log in + one paste, cached for ~2 months.
- **The PSN API is unofficial** and against Sony's ToS to automate — fine for a
  personal tool; don't put it in front of strangers. Keep the Worker's
  `ALLOWED_ORIGIN` locked to your site.
- **Nothing is stored server-side.** Your npsso lives only in your own browser's
  localStorage and passes through the Worker transiently, never logged or saved.
- **Matching is fuzzy** where the catalogue has no Spotify id for a song. The app
  searches by title and artist and scores the candidates, which covers, live cuts
  and remasters can all confuse — so you review the matches before syncing, and
  low-confidence ones start unchecked.
