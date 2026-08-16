# RockBandSpotify

Blazor WebAssembly (.NET 10) app for browsing the Rock Band song catalogue and
building Spotify playlists from the DLC you own. Deployed to GitHub Pages.

```
src/RockBandSpotify/            the app
  Pages/, Layout/, Components/  UI (scoped .razor.css beside each component)
  Services/                     logic; pure/testable pieces split out of pages
  wwwroot/data/                 static assets: catalogue.json, entitlements.json
gateway/                        Cloudflare Worker (PSN relay — being retired, see below)
tools/                          maintainer-only offline scripts; see tools/README.md
tests/RockBandSpotify.UnitTests/      xUnit
tests/RockBandSpotify.EndToEndTests/  Playwright + NUnit
docs/                           research notes
```

## Working agreements

- **One complete change per PR.** Small, self-contained, reviewable. This is an
  explicit preference — don't batch unrelated changes together.
- **Don't write flaky tests, and don't re-run a suite to "check" for flakiness.**
  If something is timing-sensitive, fix the wait condition rather than retrying.
- Put pure logic in `Services/` as a static/testable class (see
  `CatalogueFilter`, `CatalogueSort`) rather than in a page's code-behind.
- Prefer no JavaScript. Reach for a CSS-only solution first; `wwwroot/js/interop.js`
  exists for the few things that genuinely need it (localStorage, viewport height).

## Build and test

```bash
dotnet build RockBandSpotify.slnx -c Release -warnaserror
dotnet test tests/RockBandSpotify.UnitTests/RockBandSpotify.UnitTests.csproj -c Release --no-build

# E2E needs the app running
dotnet run --project src/RockBandSpotify/RockBandSpotify.csproj -c Release --no-build --urls http://localhost:5010 &
APP_BASE_URL=http://localhost:5010 PLAYWRIGHT_CHROMIUM_PATH=/opt/pw-browsers/chromium \
  dotnet test tests/RockBandSpotify.EndToEndTests/RockBandSpotify.EndToEndTests.csproj -c Release --no-build
```

## Gotchas

- **Bootstrap is loaded before `app.css`.** Don't name a class `.table` — it
  collides with Bootstrap's component and forces `--bs-table-bg`. The catalogue
  grid uses `.catalogue-table` for exactly this reason.
- **The catalogue grid renders all ~4,953 rows** with no `<Virtualize>`. This was
  deliberate: `Virtualize` keeps a single running-average row height, so mixed
  wrapped/unwrapped rows made it jump during scroll. Don't reintroduce it without
  solving that.
- **The grid header is a separate table** outside the scrolling element, not a
  `position: sticky` row inside it — sticky let row content bleed above the
  header during iOS rubber-band scroll.
- Theme tokens live in `:root` in `wwwroot/css/app.css`. Dark navy/blue palette,
  original work — no Rock Band art or assets.

## PSN entitlement matching — read before touching

**`docs/psn-entitlement-research.md`** holds the findings, the measured numbers,
and — importantly — the approaches already ruled out. Several plausible-looking
routes (PSN Store name scraping, `rbdb.io`, `psn-api` for DLC ownership) are
proven dead ends; the doc says why so they don't get re-attempted.

Two settled decisions:

- The entitlement database is **static and maintainer-generated**
  (`tools/generate-entitlement-db.mjs` → `wwwroot/data/entitlements.json`).
  Nothing is collected from users.
- The app will **not query the PSN API at runtime**. The gateway Worker predates
  that decision and is on its way out; `PsnService` still points at it and
  currently yields zero songs because the response shapes don't match.

The single input that cannot be reconstructed is a real PSN entitlement dump.
`tools/psn-rockband.ps1` produces one locally; dumps are gitignored.
