# PlayStation gateway (Cloudflare Worker)

A **stateless** relay that lets the browser app read your PlayStation entitlements.
The browser can't call PlayStation directly (no CORS); this Worker makes the call
server-side and returns just your Rock Band song list.

**It stores nothing.** Your `npsso` arrives in the POST body, is used for that one
request, and is never persisted or logged. The Worker only ever contacts Sony's
two hosts, and CORS is locked to your app's origin.

## Endpoint

```
POST /              body: { "npsso": "<64-char token>" }
```

Response: `{ "generatedAt", "source", "counts", "items": [ { code, id, type } ] }`

`code` is the PlayStation content code (e.g. `RBBELIEVERXX2775`), `type` is
`"song"`, `"disc"`, or `"bundle"`. The app matches these codes against its
own static song catalogue — this Worker never resolves or returns names.

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

## Local dev

```bash
cd gateway
wrangler dev      # serves http://localhost:8787
```

## Checking your entitlements without deploying

`test-local.mjs` runs the same PlayStation calls the Worker makes, on your own machine,
so your `npsso` never leaves it:

```bash
PlayStation_NPSSO=<your-64-char-token> node test-local.mjs
```

It prints how many entitlements the account holds and which Rock Band items the
Worker would return, and writes the full raw list to `psn-raw.json` (gitignored)
for digging through.
