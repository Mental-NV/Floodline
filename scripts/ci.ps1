[CmdletBinding()]
param(
  [ValidateSet("Always","M0","M1","M2","M3","M4","M5")]
  [string]$Scope = "Always",

  [ValidateSet("Debug","Release")]
  [string]$Configuration = "Release",

  [switch]$LockedRestore = $true,
  [switch]$UseLockFile = $false,
  [switch]$IncludeFormat = $true,

  # future switches (safe to add later without breaking callers)
  [switch]$Golden = $false,
  [switch]$Replay = $false,
  [switch]$ValidateLevels = $false,
  [switch]$Unity = $false
)

$ErrorActionPreference = "Stop"

function Run([string]$cmd) {
  Write-Host ">> $cmd"
  iex $cmd
  if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $cmd" }
}

# Find solution (first *.sln under repo root)
$sln = Get-ChildItem -Path "." -Filter "*.sln" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $sln) { throw "No .sln found. Create solution/projects before running CI script." }

# Restore
# - UseLockFile: generates/updates packages.lock.json (for initial adoption)
# - LockedRestore: verifies restore uses committed lock files
if ($UseLockFile) {
  Run "dotnet restore `"$($sln.FullName)`" --use-lock-file"
}

if ($LockedRestore) {
  Run "dotnet restore `"$($sln.FullName)`" --locked-mode"
} elseif (-not $UseLockFile) {
  Run "dotnet restore `"$($sln.FullName)`""
}

# Build + test
Run "dotnet build `"$($sln.FullName)`" -c $Configuration"

$testFilter = $null
if ($Golden) {
  $testFilter = "FullyQualifiedName!~.Golden."
}

$testCommand = "dotnet test `"$($sln.FullName)`" -c $Configuration --no-build"
if ($testFilter) {
  $testCommand += " --filter `"$testFilter`""
}
Run $testCommand

if ($Golden) {
  $goldenFilter = "FullyQualifiedName~.Golden."
  $goldenResultsDir = Join-Path ([System.IO.Path]::GetTempPath()) ("Floodline-GoldenTests-" + [Guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path $goldenResultsDir -Force | Out-Null

  Run "dotnet test `"$($sln.FullName)`" -c $Configuration --no-build --filter `"$goldenFilter`" --results-directory `"$goldenResultsDir`" --logger `"trx`""

  $trxFiles = Get-ChildItem -Path $goldenResultsDir -Filter *.trx -ErrorAction SilentlyContinue
  if (-not $trxFiles) {
    throw "Golden test results not found in $goldenResultsDir."
  }

  $goldenTotal = 0
  foreach ($trxFile in $trxFiles) {
    [xml]$trx = Get-Content -LiteralPath $trxFile.FullName
    $counterNode = $trx.GetElementsByTagName("Counters") | Select-Object -First 1
    if (-not $counterNode) {
      continue
    }
    $goldenTotal += [int]$counterNode.total
  }

  if ($goldenTotal -le 0) {
    throw "Golden test run found no tests. Filter '$goldenFilter' may be wrong."
  }

  Remove-Item -Path $goldenResultsDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Formatting (only when gate is introduced)
if ($IncludeFormat) {
  Run "dotnet format `"$($sln.FullName)`" --verify-no-changes"
}

# Future: golden / replay / level validation / unity
# Implement these later when the projects/tools exist; keep switches stable so backlog items don't churn.

Write-Host "CI OK: Scope=$Scope Config=$Configuration LockedRestore=$LockedRestore IncludeFormat=$IncludeFormat Golden=$Golden"
exit 0
