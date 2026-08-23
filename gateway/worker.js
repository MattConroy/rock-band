// Stateless PlayStation gateway for the Rock Band -> Spotify app.
//
// Why this exists: browsers refuse to read PlayStation's API responses (no CORS),
// so the WASM app can't call PlayStation directly. This Worker relays the call from the
// server side, where CORS doesn't apply, and adds the one header the browser needs.
//
// It is deliberately STATELESS: the caller's npsso arrives in the request body,
// is used for this request only, and is never stored or logged. The PSN logic
// lives in playstation.mjs (shared with the local test script).
//
// The app matches the returned entitlement codes against its own static song
// catalogue client-side — this Worker's only job is handing back what PlayStation says
// you own.
//
// Request:  POST /              { "npsso": "<64-char token>" }
// Response: { "generatedAt", "source", "counts", "items": [{ code, id, type }] }

import { getAccessToken, fetchEntitlements, ownedRockBandSongs } from "./playstation.mjs";

export default {
  async fetch(request, env) {
    const cors = {
      "Access-Control-Allow-Origin": env.ALLOWED_ORIGIN || "*",
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
      "Access-Control-Max-Age": "86400",
    };

    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: cors });
    }

    if (request.method !== "POST") {
      return json({ error: "Use POST with { npsso }." }, 405, cors);
    }

    let npsso;
    try {
      ({ npsso } = await request.json());
    } catch {
      return json({ error: "Invalid JSON body." }, 400, cors);
    }
    if (!npsso || typeof npsso !== "string" || npsso.length < 40) {
      return json({ error: "Missing or malformed npsso." }, 400, cors);
    }

    try {
      const accessToken = await getAccessToken(npsso);
      const entitlements = await fetchEntitlements(accessToken);
      const items = ownedRockBandSongs(entitlements);

      return json(
        {
          generatedAt: new Date().toISOString(),
          source: "psn-entitlements",
          counts: {
            song: items.filter((i) => i.type === "song").length,
            disc: items.filter((i) => i.type === "disc").length,
            bundle: items.filter((i) => i.type === "bundle").length,
          },
          items,
        },
        200,
        cors,
      );
    } catch (err) {
      // Never echo the token; surface only a short reason.
      return json({ error: String(err.message || err).slice(0, 200) }, 502, cors);
    }
  },
};

function json(body, status, cors) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...cors },
  });
}
