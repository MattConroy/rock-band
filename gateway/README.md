# PSN gateway (Cloudflare Worker)

A **stateless** relay that lets the browser app read your PlayStation entitlements.
The browser can't call PSN directly (no CORS); this Worker makes the call
server-side and returns just your Rock Band song list.

**It stores nothing.** Your `npsso` arrives in the POST body, is used for that one
request, and is never persisted or logged. The Worker only ever contacts Sony's
two hosts, and CORS is locked to your app's origin.

## Endpoint

```
POST /              body: { "npsso": "<64-char token>" }
POST /?debug=1      returns the raw entitlement list (for tuning the filters)
```

Response: `{ "generatedAt", "source", "songs": [ { title, artist, source } ] }`

## Deploy (free, no credit card)

```bash
npm install -g wrangler        # or: npx wrangler ...
cd gateway
wrangler login                 # opens Cloudflare in your browser
wrangler deploy
```

Set your Pages origin so only your app can use it:

```bash
wrangler deploy --var ALLOWED_ORIGIN:https://<your-username>.github.io
```

`wrangler deploy` prints the Worker URL (e.g.
`https://rockband-psn-gateway.<you>.workers.dev`). Put that URL in the app's
`wwwroot/appsettings.json` under `Psn.GatewayUrl`.

## Tuning the song filter

If songs are missing or misparsed, POST with `?debug=1` to see the raw entitlement
names, then set `RB_INCLUDE_REGEX` / `RB_EXCLUDE_REGEX` (in `wrangler.toml` or via
`--var`) to match your data. No redeploy of the app is needed — only the Worker.

## Local dev

```bash
cd gateway
wrangler dev      # serves http://localhost:8787
```
