[CmdletBinding()]
param(
  [string]$BacklogPath = ".agent/backlog.json",
  [switch]$FailOnDirtyTree = $true
)

$ErrorActionPreference = "Stop"

function Fail([string]$msg) {
  Write-Error $msg
  exit 1
}

# 1) must be on main
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") { Fail "Preflight requires being on 'main'. Current branch: $branch" }

# 2) main must be in sync with origin/main
git fetch origin main | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "git fetch origin main failed; cannot validate sync with origin/main." }

$revList = (git rev-list --left-right --count origin/main...HEAD).Trim()
if ($LASTEXITCODE -ne 0) { Fail "Unable to compare HEAD with origin/main." }

$parts = $revList -split '\s+'
if ($parts.Count -lt 2) { Fail "Unexpected rev-list output: $revList" }

$behind = [int]$parts[0]
$ahead = [int]$parts[1]
if ($behind -gt 0 -or $ahead -gt 0) {
  Fail "main is out of sync with origin/main (ahead $ahead, behind $behind). Pull/rebase before proceeding."
}

# 3) git clean check
if ($FailOnDirtyTree) {
  $status = git status --porcelain
  if ($status) { Fail "Working tree is not clean. Commit or stash changes before proceeding." }
}

# 4) backlog exists + parse
if (-not (Test-Path $BacklogPath)) { Fail "Backlog not found at: $BacklogPath" }
$raw = Get-Content $BacklogPath -Raw
$backlog = $raw | ConvertFrom-Json

# Expect either { items: [...] } or [...] (tolerate both)
$items = $null
if ($backlog.PSObject.Properties.Name -contains "items") { 
    $items = $backlog.items 
} else { 
    $items = $backlog 
}

if (-not $items) { Fail "Backlog contains no items." }

# 5) invariants: WIP limit
$active = @($items | Where-Object { $_.status -in @("InProgress","InReview") })
if ($active.Count -gt 1) { Fail "WIP violation: more than one item is active (InProgress/InReview)." }

$done = @($items | Where-Object { $_.status -eq "Done" })
$new  = @($items | Where-Object { $_.status -eq "New" })

# 6) compute NEXT = lowest ID New with all dependsOn Done
$doneIds = @($done | ForEach-Object { [string]$_.id })

function IsEligible($item) {
  # In PS 5.1, sometimes property access is safer with .id directly
  $deps = $item.dependsOn
  if (-not $deps) { return $true }
  foreach ($dep in $deps) {
    if ([string]$dep -notin $doneIds) { return $false }
  }
  return $true
}

# Explicitly filter and force array
[array]$eligibleNew = $new | Where-Object { IsEligible $_ }

if ($eligibleNew) {
    # Sort and pick first (numeric-aware)
    $sorted = $eligibleNew | Sort-Object { [int]($_.id -replace '\D','') }, id
    $next = $sorted[0]
} else {
    $next = $null
}

# 7) print summary (agent-friendly)
Write-Host "DONE:    $($done.Count)"
if ($active.Count -eq 1) {
  $cur = $active[0]
  Write-Host "CURRENT: $($cur.id) [$($cur.status)] - $($cur.title)"
} else {
  Write-Host "CURRENT: (none)"
}
if ($next) {
  Write-Host "NEXT:    $($next.id) - $($next.title)"
} else {
  Write-Host "NEXT:    (none eligible)"
}

exit 0
