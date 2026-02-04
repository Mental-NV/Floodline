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
  [switch]$CampaignSolutions = $false,
  [switch]$Unity = $false
)

$ErrorActionPreference = "Stop"

switch ($Scope) {
  "M2" { $Golden = $true }
  "M3" { $Golden = $true; $Replay = $true }
  "M4" { $Golden = $true; $Replay = $true }
  "M5" { $Golden = $true; $Replay = $true }
  default { }
}

function Run([string]$cmd) {
  Write-Host ">> $cmd"
  iex $cmd
  if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $cmd" }
}

function RunDotnet([string[]]$dotnetArgs) {
  Write-Host (">> dotnet " + ($dotnetArgs -join " "))
  $output = & dotnet @dotnetArgs 2>&1
  if ($LASTEXITCODE -ne 0) {
    $joinedOutput = $output -join [Environment]::NewLine
    throw "Command failed with exit code ${LASTEXITCODE}: dotnet $($dotnetArgs -join ' ')`n$joinedOutput"
  }
  return $output
}

function GetDeterminismHash([string[]]$outputLines) {
  $joinedOutput = $outputLines -join [Environment]::NewLine
  $match = [regex]::Match($joinedOutput, "DeterminismHash:\s*(\S+)")
  if ($match.Success) {
    return $match.Groups[1].Value.Trim()
  }

  throw "DeterminismHash not found in CLI output.`n$joinedOutput"
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

# Replay parity gate (record -> replay determinism hash must match)
if ($Replay) {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
  $cliProject = Join-Path $repoRoot "src\Floodline.Cli\Floodline.Cli.csproj"
  $levelPath = Join-Path $repoRoot "levels\minimal_level.json"
  $inputsPath = Join-Path $repoRoot "levels\minimal_inputs.txt"

  if (-not (Test-Path $cliProject)) { throw "CLI project not found: $cliProject" }
  if (-not (Test-Path $levelPath)) { throw "Replay fixture level not found: $levelPath" }
  if (-not (Test-Path $inputsPath)) { throw "Replay fixture inputs not found: $inputsPath" }

  $replayPath = Join-Path ([System.IO.Path]::GetTempPath()) ("floodline-ci-replay-" + [Guid]::NewGuid().ToString("N") + ".json")

  try {
    $recordArgs = @(
      "run",
      "--project", $cliProject,
      "--configuration", $Configuration,
      "--no-build",
      "--",
      "--level", $levelPath,
      "--inputs", $inputsPath,
      "--record", $replayPath
    )

    $recordOutput = RunDotnet $recordArgs

    if (-not (Test-Path $replayPath)) {
      throw "Replay file was not created: $replayPath"
    }

    $recordHash = GetDeterminismHash $recordOutput

    $replayArgs = @(
      "run",
      "--project", $cliProject,
      "--configuration", $Configuration,
      "--no-build",
      "--",
      "--level", $levelPath,
      "--replay", $replayPath
    )

    $replayOutput = RunDotnet $replayArgs
    $replayHash = GetDeterminismHash $replayOutput

    if ($recordHash -ne $replayHash) {
      throw "Replay determinism mismatch: record=$recordHash replay=$replayHash"
    }

    Write-Host "Replay parity OK: $recordHash"
  }
  finally {
    Remove-Item -Path $replayPath -Force -ErrorAction SilentlyContinue
  }
}

# Placeholder gates (fail fast until implemented)
if ($ValidateLevels) {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
  $cliProject = Join-Path $repoRoot "src\Floodline.Cli\Floodline.Cli.csproj"
  $campaignPath = Join-Path $repoRoot "levels\campaign.v0.2.0.json"

  if (-not (Test-Path $cliProject)) { throw "CLI project not found: $cliProject" }

  $validateArgs = @(
    "run",
    "--project", $cliProject,
    "--configuration", $Configuration,
    "--no-build",
    "--",
    "--validate-campaign",
    "--campaign", $campaignPath
  )

  $validateOutput = RunDotnet $validateArgs
  $validateJoined = $validateOutput -join [Environment]::NewLine
  Write-Host $validateJoined
}

if ($CampaignSolutions) {
  throw "-CampaignSolutions is not implemented yet. Implement solution runner gate first (see backlog FL-0419)."
}

if ($Unity) {
  throw "-Unity is not implemented yet. Implement Unity parity gate first (see backlog FL-0503)."
}

# Formatting (only when gate is introduced)
if ($IncludeFormat) {
  Run "dotnet format `"$($sln.FullName)`" --verify-no-changes"
}

# Future: keep switch names stable so backlog items don't churn; add enforcement when tooling exists.

Write-Host "CI OK: Scope=$Scope Config=$Configuration LockedRestore=$LockedRestore IncludeFormat=$IncludeFormat Golden=$Golden"
exit 0
