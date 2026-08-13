#!/usr/bin/env node
// Fetches entitlements directly (bypassing the Worker) and prints them as JSON in
// the same shape the Worker's ?debug=1 returns: { count, entitlements }.
// Used by the Test PSN fetch workflow's "direct" mode. Reads PSN_NPSSO from env.

import { getAccessToken, fetchEntitlements } from "./psn.mjs";

const npsso = process.env.NPSSO || process.env.PSN_NPSSO;
if (!npsso) {
  console.error("NPSSO env var is required.");
  process.exit(1);
}

const accessToken = await getAccessToken(npsso);
const entitlements = await fetchEntitlements(accessToken);
process.stdout.write(JSON.stringify({ count: entitlements.length, entitlements }));
