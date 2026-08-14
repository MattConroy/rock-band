#!/usr/bin/env pwsh
<#
  Rock Band DLC extractor — runs locally (PowerShell 7+). Your npsso never leaves
  your machine.

  Usage:
    pwsh ./psn-rockband.ps1 -Npsso "<64-char npsso>"
    pwsh ./psn-rockband.ps1 -Npsso "<...>" -SkipNames      # faster, no Store lookup

  Get npsso: sign in at https://my.playstation.com, then open
    https://ca.account.sony.com/api/v1/ssocookie  and copy the "npsso" value.

  Outputs (in the current folder):
    entitlements-raw.json   every entitlement (for digging)
    rockband.csv            code, productId, resolved name
#>
param(
  [Parameter(Mandatory = $true)][string]$Npsso,
  [switch]$SkipNames,
  [string]$Region = "en-gb"
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) { throw "Please run in PowerShell 7+ (pwsh)." }

$CLIENT_ID       = "09515159-7237-4370-9b40-3806e67c0891"
$CLIENT_SECRET   = "ucPjka5tntB2KqsP"
$REDIRECT_URI    = "com.scee.psxandroid.scecompcall://redirect"
$AUTH_BASE       = "https://ca.account.sony.com/api/authz/v3/oauth"
$ENTITLEMENT_URL = "https://m.np.playstation.com/api/entitlement/v2/users/me/internal/entitlements"
$HARMONIX        = @("EP0006", "EP8802")   # Rock Band + Rock Band Blitz publishers

# 1) npsso -> authorization code (do NOT follow the redirect; read Location).
Write-Host "-> Authenticating..."
$scope   = [uri]::EscapeDataString("psn:mobile.v2.core psn:clientapp")
$redir   = [uri]::EscapeDataString($REDIRECT_URI)
$authUrl = "$AUTH_BASE/authorize?access_type=offline&client_id=$CLIENT_ID&response_type=code&scope=$scope&redirect_uri=$redir"
# Use HttpClient so the 302 is returned (not followed) and we can read Location.
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$authReq = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $authUrl)
[void]$authReq.Headers.TryAddWithoutValidation("Cookie", "npsso=$Npsso")
$authResp = $client.SendAsync($authReq).GetAwaiter().GetResult()
$location = if ($authResp.Headers.Location) { $authResp.Headers.Location.OriginalString } else { "" }
if ($location -notmatch "code=([^&]+)") { throw "Login failed - npsso is invalid or expired. Grab a fresh one." }
$code = $Matches[1]

# 2) code -> access token.
$basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${CLIENT_ID}:${CLIENT_SECRET}"))
$tok = Invoke-RestMethod -Method Post -Uri "$AUTH_BASE/token" `
  -Headers @{ Authorization = "Basic $basic" } `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ grant_type = "authorization_code"; code = $code; redirect_uri = $REDIRECT_URI; token_format = "jwt" }
$access = $tok.access_token
if (-not $access) { throw "Token exchange failed." }

# 3) Fetch all entitlements. Pagination is offset/limit (start/size is ignored).
Write-Host "-> Fetching entitlements..."
$byId = [ordered]@{}
$offset = 0; $limit = 500
while ($true) {
  $page = Invoke-RestMethod -Uri "$ENTITLEMENT_URL`?offset=$offset&limit=$limit" -Headers @{ Authorization = "Bearer $access" }
  $items = @($page.entitlements)
  if ($items.Count -eq 0) { break }
  foreach ($e in $items) { $byId[$e.id] = $e }
  $offset += $items.Count
  if ($offset -ge $page.totalResults) { break }
}
$entitlements = @($byId.Values)
$entitlements | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 "entitlements-raw.json"
Write-Host "   $($entitlements.Count) entitlements  ->  entitlements-raw.json"

# 4) Rock Band filter: Harmonix publisher + content code starting RB or XRB; dedupe by code.
$songs = [ordered]@{}
foreach ($e in $entitlements) {
  $prodId = if ($e.productId) { $e.productId } else { $e.id }
  $parts = $prodId -split "-"
  if ($HARMONIX -notcontains $parts[0]) { continue }
  $content = ($parts[2..($parts.Count - 1)] -join "-")
  if ($content -notmatch "^X?RB") { continue }
  if (-not $songs.Contains($content)) { $songs[$content] = $pid }
}
Write-Host "   $($songs.Count) unique Rock Band codes"

# 5) Optional: resolve real names from the PSN Store product page.
$rows = foreach ($kv in $songs.GetEnumerator()) {
  $name = ""
  if (-not $SkipNames) {
    try {
      $html = (Invoke-WebRequest -Uri "https://store.playstation.com/$Region/product/$($kv.Value)" `
          -Headers @{ "Accept-Language" = $Region } -SkipHttpErrorCheck).Content
      if ($html -match '<meta property="og:title" content="([^"]*)"') { $name = $Matches[1] }
    } catch { }
  }
  [pscustomobject]@{ code = $kv.Key; productId = $kv.Value; name = $name }
}
$rows | Export-Csv -NoTypeInformation -Encoding utf8 "rockband.csv"
Write-Host "   wrote rockband.csv"
if (-not $SkipNames) {
  $hit = ($rows | Where-Object { $_.name }).Count
  Write-Host "   Store names resolved: $hit / $($rows.Count)"
}
