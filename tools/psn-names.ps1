#!/usr/bin/env pwsh
<#
  Resolve Rock Band song names from the PSN Store, from your entitlements dump.
  Usage:  pwsh ./psn-names.ps1 -Json .\entitlements-raw.json
  Output: rb-names.csv  (id, name)  + a resolved/total count.
#>
param(
  [string]$Json = "entitlements-raw.json",
  [string]$Region = "en-gb"
)
$ErrorActionPreference = "Stop"

$data = Get-Content $Json -Raw | ConvertFrom-Json
$ents = if ($data.entitlements) { $data.entitlements } else { $data }

# The 7 Rock Band game title codes (the definitive filter).
$RB = 'BLES00228','BLES00986','CUSA03384','CUSA02901','BLES01611','NPEB00988','NPEH90013'
$skip = 'DISCEXP|EXPO|TRACKP|BONUS|BLITZ0|EXPANSION|LRBX|ANNPACK|ANNSONG|RLPBONUS|WEEK|PASS|ROCKBAND1|HMXBAND|UNPLUGG|FAILURE|GUITARGS|SHIRTBGR|000000000'

# Unique individual songs (keep full id for the Store URL).
$seen = @{}; $ids = New-Object System.Collections.Generic.List[string]
foreach ($e in $ents) {
  $pid = if ($e.id) { $e.id } else { $e.productId }
  $parts = $pid -split '-'
  $title = ($parts[1] -split '_')[0]
  $content = ($parts[2..($parts.Count - 1)] -join '-')
  if ($RB -notcontains $title) { continue }
  if ($content -notmatch '^X?RB') { continue }
  if ($content -match $skip) { continue }
  if ($seen[$content]) { continue }
  $seen[$content] = $true
  $ids.Add($pid)
}

Write-Host "Resolving $($ids.Count) songs from the Store..."
$i = 0
$out = foreach ($pid in $ids) {
  $i++; if ($i % 50 -eq 0) { Write-Host "  $i / $($ids.Count)" }
  $name = ''
  try {
    $r = Invoke-WebRequest "https://store.playstation.com/$Region/product/$pid" -SkipHttpErrorCheck -TimeoutSec 20
    if ($r.Content -match '<meta property="og:title" content="([^"]*)"') { $name = $Matches[1] }
    elseif ($r.Content -match '"name"\s*:\s*"([^"]{2,140})"') { $name = $Matches[1] }
  } catch { }
  [pscustomobject]@{ id = $pid; name = $name }
}
$out | Export-Csv rb-names.csv -NoTypeInformation -Encoding utf8
$hit = ($out | Where-Object { $_.name }).Count
Write-Host "`nResolved $hit / $($out.Count)  ->  rb-names.csv"
