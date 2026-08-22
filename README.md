# Rock Band → Spotify

Browse the Rock Band song catalogue, mark which songs you own on PlayStation, and
build a Spotify playlist from them.

Live at **<https://mattconroy.github.io/rock-band/>**.

## What it does

- **Browse 4,953 songs** — every Rock Band song across the discs, the DLC, the
  spin-offs and the Rock Band Network. Search by title or artist, filter by genre
  or by the game a song came from, sort any column. No login needed.
- **See what you own** — connect PlayStation once and the catalogue gains an
  ownership column, so you can narrow it to the songs in your library.
- **Build a playlist** — connect Spotify and sync your owned songs into a
  playlist. Matches are shown for review first, and re-running only adds what is
  missing.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Node 22 is only
needed if you want to work on the PlayStation gateway.

```bash
git clone https://github.com/MattConroy/rock-band.git
cd rock-band/src/RockBandSpotify
dotnet run
```

That serves the app at <http://localhost:5010>. The catalogue works immediately.
Connecting Spotify or PlayStation needs the setup below.

### Connecting Spotify

1. Create an app in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Add a Redirect URI. The match is exact, so a trailing slash makes it a
   different URI, and Spotify no longer accepts `localhost` as a host:
   - local: `http://127.0.0.1:5010/spotify-connect`
   - deployed: `https://<you>.github.io/<repo>/spotify-connect`
3. Put the **Client ID** in `src/RockBandSpotify/wwwroot/appsettings.json`. There
   is no client secret — the app uses PKCE, and the Client ID is safe to commit.

The same section has `SearchForMissingTracks`, off by default. Most songs already
carry the Spotify track they are, and those are always used. The setting only
governs the rest: leave it off and they're listed as *not searched*; turn it on
and the app searches Spotify by name for each one, which costs a request per song
and produces a guess worth reviewing.

### Connecting PlayStation

PlayStation's API refuses browser requests, so those calls go through a small
Cloudflare Worker you deploy yourself. It is free and stores nothing.

```bash
cd gateway
npx wrangler login
npx wrangler deploy --var ALLOWED_ORIGIN:https://<you>.github.io
```

Put the Worker URL it prints into `appsettings.json` under `Psn.GatewayUrl`. See
[`gateway/README.md`](gateway/README.md) for more.

In the app, **Connect** → *Open PlayStation login*, sign in, then copy the `npsso`
value the page shows and paste it in. It is cached in your browser and lasts about
two months.

## Deploying your own

Push to the default branch. The **Publish** workflow builds the app to GitHub
Pages and deploys the Worker to Cloudflare.

It needs two things set up once:

- **Settings → Pages → Build and deployment → Source = GitHub Actions**
- A `CLOUDFLARE_API_TOKEN` repository secret with the *Edit Cloudflare Workers*
  permission

## Tests

```bash
dotnet test tests/RockBandSpotify.UnitTests
```

The browser tests drive a real Chromium, so it has to be installed once, and the
app has to be running:

```bash
dotnet build tests/RockBandSpotify.EndToEndTests
pwsh tests/RockBandSpotify.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium

dotnet run --project src/RockBandSpotify &
dotnet test tests/RockBandSpotify.EndToEndTests
```

## Worth knowing

- **The PlayStation token paste can't be avoided.** This isn't the official
  PlayStation app, so it can't capture Sony's login redirect. One sign-in and one
  paste, every couple of months.
- **PlayStation's API is unofficial** and automating it is against Sony's terms.
  Fine as a personal tool; keep your Worker's `ALLOWED_ORIGIN` locked to your own
  site rather than opening it to strangers.
- **Nothing is stored on a server.** Your tokens live in your own browser and pass
  through the Worker without being logged or saved.

## Credits

Catalogue data is compiled in part from the public song list published by
[rb4.app](https://rb4.app). Thanks to them for maintaining it.
