// Shared PSN logic used by both the Worker (worker.js) and the local test
// script (test-local.mjs), so the auth/fetch/parse behaviour is identical.
//
// Stateless: callers pass the npsso per request; nothing is stored here.

// Public client credentials of the official PlayStation app (same values the
// community psn-api library uses). Not secret — they identify the app to Sony.
export const CLIENT_ID = "09515159-7237-4370-9b40-3806e67c0891";
export const CLIENT_SECRET = "ucPjka5tntB2KqsP";
export const REDIRECT_URI = "com.scee.psxandroid.scecompcall://redirect";
export const AUTH_BASE = "https://ca.account.sony.com/api/authz/v3/oauth";
export const ENTITLEMENT_URL =
  "https://m.np.playstation.com/api/entitlement/v2/users/me/internal/entitlements";

export const DEFAULT_INCLUDE = "rock ?band";
export const DEFAULT_EXCLUDE =
  "rock band 4|rock band rivals|season pass|full game|bundle|track pack|rb4";

// npsso -> authorization code -> access token. The authorize call must NOT follow
// its redirect; the code is in the Location header of the 302.
export async function getAccessToken(npsso) {
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

export async function fetchEntitlements(accessToken) {
  // The endpoint reports a large totalResults but historically ignored a `start`
  // offset, returning the same page repeatedly. Dedupe by id and stop as soon as
  // a page adds nothing new, so we never blow up into 41x duplicates.
  const byId = new Map();
  const pageSize = 500;
  let start = 0;
  for (let page = 0; page < 80; page++) {
    const res = await fetch(`${ENTITLEMENT_URL}?start=${start}&size=${pageSize}`, {
      headers: { Authorization: `Bearer ${accessToken}`, Accept: "application/json" },
    });
    if (!res.ok) throw new Error(`Entitlement request failed (${res.status}).`);
    const data = await res.json();
    const items = data.entitlements || data.entitlementList || [];
    const before = byId.size;
    for (const e of items) byId.set(e.id ?? e.productId ?? JSON.stringify(e), e);
    // Advance by however many we actually got, not a fixed stride.
    start += items.length || pageSize;
    if (items.length === 0 || byId.size === before) break;
  }
  return [...byId.values()];
}

// Probe the several field names PSN has used for a human-readable entitlement name.
export function entitlementName(e) {
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
export function parseSongName(raw) {
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

export function toSongs(entitlements, include, exclude) {
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
