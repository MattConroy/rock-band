// Stateless PSN gateway for the Rock Band -> Spotify app.
//
// Why this exists: browsers refuse to read PlayStation's API responses (no CORS),
// so the WASM app can't call PSN directly. This Worker relays the call from the
// server side, where CORS doesn't apply, and adds the one header the browser needs.
//
// It is deliberately STATELESS: the caller's npsso arrives in the request body,
// is used for this request only, and is never stored or logged. The PSN logic
// lives in psn.mjs (shared with the local test script).
//
// Request:  POST /              { "npsso": "<64-char token>" }
// Response: { "generatedAt", "source", "songs": [...] }   (or ?debug=1 for raw)

import {
  getAccessToken,
  fetchEntitlements,
  entitlementName,
  toSongs,
  ENTITLEMENT_URL,
  DEFAULT_INCLUDE,
  DEFAULT_EXCLUDE,
} from "./psn.mjs";

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

      const url = new URL(request.url);
      if (url.searchParams.get("debug") === "1") {
        // Rock Band DLC: Harmonix publishers, content code starting RB or XRB.
        const HARMONIX = new Set(["EP0006", "EP8802"]);
        const codes = new Set();
        const byTitle = {};
        let n = 0;
        for (const e of entitlements) {
          const pid = e.productId || e.id || "";
          const parts = pid.split("-");
          if (!HARMONIX.has(parts[0])) continue;
          const content = parts.slice(2).join("-");
          if (!/^X?RB/.test(content)) continue;
          n++;
          codes.add(content);
          const t = `${parts[0]}-${(parts[1] || "").split("_")[0]}`;
          byTitle[t] = (byTitle[t] || 0) + 1;
        }
        return json(
          {
            rbEntitlements: n,
            uniqueCodes: codes.size,
            byTitle,
            codes: [...codes].sort(),
          },
          200,
          cors,
        );
      }

      const include = new RegExp(env.RB_INCLUDE_REGEX || DEFAULT_INCLUDE, "i");
      const exclude = new RegExp(env.RB_EXCLUDE_REGEX || DEFAULT_EXCLUDE, "i");
      const songs = toSongs(entitlements, include, exclude);

      return json(
        { generatedAt: new Date().toISOString(), source: "psn-entitlements", songs },
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
