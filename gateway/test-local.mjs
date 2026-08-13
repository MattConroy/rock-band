#!/usr/bin/env node
// Local test for the PSN side — runs on YOUR machine so your npsso never leaves it.
//
//   PSN_NPSSO=your-64-char-token node test-local.mjs
//
// Prints your entitlement count, the entries that look Rock Band-related, and
// what the current filter would keep — then writes the full raw list to
// psn-raw.json so we can tune the include/exclude filters together.
//
// Get your npsso: sign in at https://ca.account.sony.com, then open
// https://ca.account.sony.com/api/v1/ssocookie and copy the npsso value.

import { writeFile } from "node:fs/promises";
import {
  getAccessToken,
  fetchEntitlements,
  entitlementName,
  toSongs,
  DEFAULT_INCLUDE,
  DEFAULT_EXCLUDE,
} from "./psn.mjs";

const npsso = process.env.PSN_NPSSO || process.argv[2];
if (!npsso) {
  console.error("Set PSN_NPSSO (env var) or pass the token as the first argument.");
  process.exit(1);
}

const include = new RegExp(process.env.RB_INCLUDE_REGEX || DEFAULT_INCLUDE, "i");
const exclude = new RegExp(process.env.RB_EXCLUDE_REGEX || DEFAULT_EXCLUDE, "i");

function line() {
  console.log("─".repeat(60));
}

try {
  console.log("→ Authenticating with PSN…");
  const accessToken = await getAccessToken(npsso);

  console.log("→ Fetching entitlements…");
  const entitlements = await fetchEntitlements(accessToken);
  console.log(`  ${entitlements.length} total entitlements\n`);

  const named = entitlements
    .map((e) => entitlementName(e))
    .filter((n) => n && n.trim().length > 0);

  // Broad candidate view: anything mentioning "rock" or "band", ignoring the
  // real filter — so you can see Rock Band items even if the filter misses them.
  const candidates = [...new Set(named.filter((n) => /rock|band/i.test(n)))].sort();
  line();
  console.log(`Entries containing "rock" or "band" (${candidates.length}):`);
  line();
  for (const n of candidates) console.log("  " + n);

  // What the current filter actually keeps, parsed into title/artist.
  const songs = toSongs(entitlements, include, exclude);
  line();
  console.log(`Current filter keeps ${songs.length} songs (title — artist):`);
  line();
  for (const s of songs) console.log(`  ${s.title} — ${s.artist || "(artist unparsed)"}`);

  await writeFile("psn-raw.json", JSON.stringify(entitlements, null, 2));
  line();
  console.log("Full raw entitlement list written to gateway/psn-raw.json (gitignored).");
  console.log(
    "\nThe entry names above are product titles, not secrets — safe to share so we can tune the filter.",
  );
} catch (err) {
  console.error("\n✖ " + (err.message || err));
  process.exit(1);
}
