// Shared PlayStation Network logic used by both the Worker (worker.js) and the local test
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

// npsso -> authorization code -> access token. The authorize call must NOT follow
// its redirect; the code is in the Location header of the 302.
export async function getAccessToken(npsso) {
  const authorizeUrl =
    `${AUTH_BASE}/authorize?access_type=offline` +
    `&client_id=${CLIENT_ID}` +
    `&response_type=code` +
    `&scope=${encodeURIComponent("psn:mobile.v2.core psn:clientapp")}` +
    `&redirect_uri=${encodeURIComponent(REDIRECT_URI)}`;

  const authorizeResponse = await fetch(authorizeUrl, {
    method: "GET",
    redirect: "manual",
    headers: { Cookie: `npsso=${npsso}` },
  });

  const location = authorizeResponse.headers.get("location") || "";
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
  const tokenResponse = await fetch(`${AUTH_BASE}/token`, {
    method: "POST",
    headers: {
      Authorization: `Basic ${basic}`,
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body,
  });
  if (!tokenResponse.ok) {
    throw new Error(`Token exchange failed (${tokenResponse.status}).`);
  }
  const token = await tokenResponse.json();
  if (!token.access_token) throw new Error("No access token returned.");
  return token.access_token;
}

export async function fetchEntitlements(accessToken) {
  // The endpoint reports a large totalResults but historically ignored a `start`
  // offset, returning the same page repeatedly. Dedupe by id and stop as soon as
  // a page adds nothing new, so we never blow up into 41x duplicates.
  // Pagination is offset/limit (start/size is ignored by the endpoint).
  const byId = new Map();
  const limit = 500;
  let offset = 0;
  for (let page = 0; page < 40; page++) {
    const response = await fetch(`${ENTITLEMENT_URL}?offset=${offset}&limit=${limit}`, {
      headers: { Authorization: `Bearer ${accessToken}`, Accept: "application/json" },
    });
    if (!response.ok) throw new Error(`Entitlement request failed (${response.status}).`);
    const data = await response.json();
    const items = data.entitlements || data.entitlementList || [];
    const total = data.totalResults ?? data.total ?? Infinity;
    for (const entitlement of items) byId.set(entitlement.id ?? entitlement.productId ?? JSON.stringify(entitlement), e);
    offset += items.length;
    if (items.length === 0 || offset >= total) break;
  }
  return [...byId.values()];
}

// The 7 confirmed Rock Band game title codes (publisher-title). A song is owned
// if either the entitlement id OR its productId sits under one of these.
export const ROCK_BAND_TITLES = new Set([
  "EP0006-BLES00228", // Rock Band 1
  "EP0006-BLES00986", // Rock Band 2
  "EP0006-CUSA03384", // Rock Band 4
  "EP8802-CUSA02901", // Rock Band 4 / Rivals DLC
  "EP8802-BLES01611", // PS3 Rock Band disc
  "EP8802-NPEB00988", // Rock Band Blitz
  "EP0006-NPEH90013", // Rock Band Unplugged
]);

const titleOf = (s) => {
  const p = (s || "").split("-");
  return (p[0] || "") + "-" + ((p[1] || "").split("_")[0]);
};
const contentOf = (s) => (s || "").split("-").slice(2).join("-");

// Returns unique owned Rock Band items: { code, id, type } where type is
// "song" (an individual track), "disc" (an on-disc PROCKBAND entitlement) or
// "bundle" (a pack or disc export).
//
// PlayStation grants a pack's contents as one entitlement per song, each carrying that
// song's own content code, so a pack needs no special handling to be matched.
// A disc export is the exception: its songs never had store listings of their
// own, so the export product's code is the only identifier they have. That
// product is often absent from the entitlement list entirely, appearing only as
// the productId of the rows it granted — hence it is emitted as an item too.
export function ownedRockBandSongs(entitlements) {
  const owned = entitlements.filter(
    (entitlement) => ROCK_BAND_TITLES.has(titleOf(entitlement.id)) || ROCK_BAND_TITLES.has(titleOf(entitlement.productId || entitlement.id)),
  );

  // A product is a bundle because it granted an entitlement other than itself,
  // not because of any pattern in its name.
  const bundles = new Map();
  for (const entitlement of owned) {
    const product = contentOf(entitlement.productId);
    if (product && product !== contentOf(entitlement.id)) bundles.set(product, entitlement.productId);
  }

  const byCode = new Map();
  for (const [code, id] of bundles) byCode.set(code, { code, id, type: "bundle" });
  for (const entitlement of owned) {
    const code = contentOf(entitlement.id);
    if (!code || byCode.has(code)) continue;
    byCode.set(code, { code, id: entitlement.id, type: /^PROC/.test(code) ? "disc" : "song" });
  }
  return [...byCode.values()];
}
