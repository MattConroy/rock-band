#!/usr/bin/env node
// Local test for the PlayStation side — runs on YOUR machine so your npsso never leaves it.
//
//   PSN_NPSSO=your-64-char-token node test-local.mjs
//
// Prints your entitlement count and the owned Rock Band items the Worker
// would return, then writes the full raw list to psn-raw.json for digging.
//
// Get your npsso: sign in at https://ca.account.sony.com, then open
// https://ca.account.sony.com/api/v1/ssocookie and copy the npsso value.

import { writeFile } from "node:fs/promises";
import { getAccessToken, fetchEntitlements, ownedRockBandSongs } from "./playstation.mjs";

const npsso = process.env.PSN_NPSSO || process.argv[2];
if (!npsso) {
  console.error("Set PSN_NPSSO (env var) or pass the token as the first argument.");
  process.exit(1);
}

function line() {
  console.log("─".repeat(60));
}

try {
  console.log("→ Authenticating with PlayStation…");
  const accessToken = await getAccessToken(npsso);

  console.log("→ Fetching entitlements…");
  const entitlements = await fetchEntitlements(accessToken);
  console.log(`  ${entitlements.length} total entitlements\n`);

  const items = ownedRockBandSongs(entitlements);
  const byType = {
    song: items.filter((i) => i.type === "song").length,
    disc: items.filter((i) => i.type === "disc").length,
    bundle: items.filter((i) => i.type === "bundle").length,
  };

  line();
  console.log(`Owned Rock Band items: ${items.length} (${byType.song} song, ${byType.disc} disc, ${byType.bundle} bundle)`);
  line();
  for (const i of items) console.log(`  [${i.type}] ${i.code}`);

  await writeFile("psn-raw.json", JSON.stringify(entitlements, null, 2));
  line();
  console.log("Full raw entitlement list written to gateway/psn-raw.json (gitignored).");
} catch (err) {
  console.error("\n✖ " + (err.message || err));
  process.exit(1);
}
