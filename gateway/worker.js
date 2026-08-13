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
        // Entitlements carry no names — only product codes like
        // EP0001-BLES00669_00-AC2UPLAYTHEME001. Group by the title code (middle
        // segment) so the Rock Band block (hundreds of song DLCs) stands out,
        // and show sample content segments to judge if names are recoverable.
        const info = {};
        for (const e of entitlements) {
          const pid = e.productId || e.id || "";
          const parts = pid.split("-");
          const code = (parts[1] || "?").split("_")[0];
          const content = parts.slice(2).join("-");
          (info[code] ??= { n: 0, sample: [] }).n++;
          if (info[code].sample.length < 4) info[code].sample.push(content);
        }
        const byTitle = Object.entries(info)
          .map(([code, v]) => ({ code, n: v.n, sample: v.sample }))
          .sort((a, b) => b.n - a.n)
          .slice(0, 40);

        // Probe how the endpoint really paginates: if page@500 returns a
        // different firstId than page@0, `start` works and we can walk pages.
        const probe = async (qs) => {
          const r = await fetch(`${ENTITLEMENT_URL}?${qs}`, {
            headers: { Authorization: `Bearer ${accessToken}`, Accept: "application/json" },
          });
          const d = await r.json().catch(() => ({}));
          const items = d.entitlements || d.entitlementList || [];
          return {
            qs,
            status: r.status,
            returned: items.length,
            total: d.totalResults ?? d.total ?? null,
            firstId: items[0]?.id ?? null,
            lastId: items[items.length - 1]?.id ?? null,
          };
        };
        const probes = [
          await probe("start=0&size=500"),
          await probe("start=500&size=500"),
          await probe("start=0&size=5000"),
          await probe("offset=500&limit=500"),
        ];

        return json(
          { uniqueCount: entitlements.length, byTitle, probes },
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
