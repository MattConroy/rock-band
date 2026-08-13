// Stateless PSN gateway for the Rock Band -> Spotify app.
//
// Why this exists: browsers refuse to read PlayStation's API responses (no CORS),
// so the WASM app can't call PSN directly. This Worker relays the call from the
// server side, where CORS doesn't apply, and adds the one header the browser needs.
//
// It is deliberately STATELESS: the caller's npsso arrives in the request body,
// is used for this request only, and is never stored or logged. The Worker only
// ever talks to Sony's two hosts, and CORS is locked to your app's origin.
//
// Request:  POST /  { "npsso": "<64-char token>" }   (optional ?debug=1)
// Response: { "generatedAt": "...", "source": "psn-entitlements", "songs": [...] }

// Public client credentials of the official PlayStation app (the same values the
// community psn-api library uses). Not secret — they identify the app to Sony.
const CLIENT_ID = "09515159-7237-4370-9b40-3806e67c0891";
const CLIENT_SECRET = "ucPjka5tntB2KqsP";
const REDIRECT_URI = "com.scee.psxandroid.scecompcall://redirect";
const AUTH_BASE = "https://ca.account.sony.com/api/authz/v3/oauth";
const ENTITLEMENT_URL =
  "https://m.np.playstation.com/api/entitlement/v2/users/me/internal/entitlements";

const DEFAULT_INCLUDE = "rock ?band";
const DEFAULT_EXCLUDE =
  "rock band 4|rock band rivals|season pass|full game|bundle|track pack|rb4";

export default {
  async fetch(request, env) {
    const allowedOrigin = env.ALLOWED_ORIGIN || "*";
    const cors = {
      "Access-Control-Allow-Origin": allowedOrigin,
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
        // Returns raw entitlements so you can tune the include/exclude filters.
        return json({ count: entitlements.length, entitlements }, 200, cors);
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

// npsso -> authorization code -> access token. The authorize call must NOT follow
// its redirect; the code is in the Location header of the 302.
async function getAccessToken(npsso) {
  const authorizeUrl =
    `${AUTH_BASE}/authorize?access_type=offline` +
    `&client_id=${CLIENT_ID}` +
    `&response_type=code` +
    `&scope=${encodeURIComponent("psn:mobile.v2.core psn:clientapp")}` +
    `&redirect_uri=${encodeURIComponent(REDIRECT_URI)}`;

  const authRes = await fetch(authorizeUrl, {
    method: "GET",
    redirect: "manual",
    headers: { Cookie: `npsso=${npsso}` },
  });

  const location = authRes.headers.get("location") || "";
  const match = location.match(/[?&]code=([^&]+)/);
  if (!match) {
    throw new Error("Login failed — the npsso is likely expired or invalid.");
  }
  const code = decodeURIComponent(match[1]);

  const body = new URLSearchParams({
    code,
    redirect_uri: REDIRECT_URI,
    grant_type: "authorization_code",
    token_format: "jwt",
  });
  const basic = btoa(`${CLIENT_ID}:${CLIENT_SECRET}`);
  const tokenRes = await fetch(`${AUTH_BASE}/token`, {
    method: "POST",
    headers: {
      Authorization: `Basic ${basic}`,
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body,
  });
  if (!tokenRes.ok) {
    throw new Error(`Token exchange failed (${tokenRes.status}).`);
  }
  const token = await tokenRes.json();
  if (!token.access_token) throw new Error("No access token returned.");
  return token.access_token;
}

async function fetchEntitlements(accessToken) {
  const all = [];
  const pageSize = 500;
  let start = 0;
  for (;;) {
    const res = await fetch(`${ENTITLEMENT_URL}?start=${start}&size=${pageSize}`, {
      headers: { Authorization: `Bearer ${accessToken}`, Accept: "application/json" },
    });
    if (!res.ok) throw new Error(`Entitlement request failed (${res.status}).`);
    const data = await res.json();
    const items = data.entitlements || data.entitlementList || [];
    all.push(...items);
    const total = data.totalResults || data.total || all.length;
    start += pageSize;
    if (items.length === 0 || all.length >= total || start > 20000) break;
  }
  return all;
}

function entitlementName(e) {
  return (
    e?.drm_def?.contentName ||
    e?.game_meta?.name ||
    e?.product_name ||
    e?.entitlement_name ||
    e?.name ||
    ""
  );
}

// Rock Band store names look like "Song Name - Artist" or '"Song" by Artist'.
function parseSongName(raw) {
  let s = String(raw).trim();
  s = s.replace(/^rock ?band[\s:–-]*\s*/i, "").trim();
  s = s.replace(/^["“](.+)["”]$/u, "$1").trim();

  let title = s;
  let artist = "";
  const byMatch = s.match(/^["“]?(.+?)["”]?\s+by\s+(.+)$/i);
  const dashMatch = s.match(/^(.+?)\s+[-–—]\s+(.+)$/);
  if (byMatch) {
    title = byMatch[1].trim();
    artist = byMatch[2].trim();
  } else if (dashMatch) {
    title = dashMatch[1].trim();
    artist = dashMatch[2].trim();
  }
  title = title.replace(/^["“](.+)["”]$/u, "$1").trim();
  return { title, artist };
}

function toSongs(entitlements, include, exclude) {
  const songs = [];
  const seen = new Set();
  for (const e of entitlements) {
    const name = entitlementName(e);
    if (!name || !include.test(name) || exclude.test(name)) continue;
    const { title, artist } = parseSongName(name);
    if (!title) continue;
    const key = `${artist}|${title}`.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    songs.push({ title, artist, source: name });
  }
  songs.sort(
    (a, b) =>
      (a.artist || "").localeCompare(b.artist || "") ||
      a.title.localeCompare(b.title),
  );
  return songs;
}
