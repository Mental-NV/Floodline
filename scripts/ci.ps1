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
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
  $cliProject = Join-Path $repoRoot "src\Floodline.Cli\Floodline.Cli.csproj"
  $campaignPath = Join-Path $repoRoot "levels\campaign.v0.2.0.json"

  if (-not (Test-Path $cliProject)) { throw "CLI project not found: $cliProject" }
  if (-not (Test-Path $campaignPath)) { throw "Campaign manifest not found: $campaignPath" }

  $campaignJson = Get-Content -LiteralPath $campaignPath -Raw | ConvertFrom-Json
  if (-not $campaignJson.levels) {
    throw "Campaign manifest missing levels array: $campaignPath"
  }

  foreach ($levelEntry in $campaignJson.levels) {
    if (-not $levelEntry.path) {
      $levelId = $levelEntry.id
      throw "Campaign level entry missing path for id '$levelId'."
    }

    $levelPath = $levelEntry.path
    if (-not [System.IO.Path]::IsPathRooted($levelPath)) {
      $levelPath = Join-Path $repoRoot $levelPath
    }

    if (-not (Test-Path $levelPath)) {
      throw "Campaign level not found: $levelPath"
    }

    $levelId = $levelEntry.id
    if (-not $levelId) {
      $levelId = "(unknown)"
    }

    Write-Host ">> Campaign solution: $levelId ($levelPath)"

    $solutionArgs = @(
      "run",
      "--project", $cliProject,
      "--configuration", $Configuration,
      "--no-build",
      "--",
      "--level", $levelPath,
      "--solution"
    )

    try {
      $solutionOutput = RunDotnet $solutionArgs
      $solutionJoined = $solutionOutput -join [Environment]::NewLine
      Write-Host $solutionJoined
    }
    catch {
      throw "Campaign solution failed for level '$levelId' ($levelPath): $($_.Exception.Message)"
    }
  }
}

if ($Unity) {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
  $cliProject = Join-Path $repoRoot "src\Floodline.Cli\Floodline.Cli.csproj"
  $unityProjectRoot = Join-Path $repoRoot "unity"
  
  # Use L01_First_Stack solution replay as parity fixture
  $levelPath = Join-Path $repoRoot "levels\campaign\L01_First_Stack.json"
  $replayPath = Join-Path $repoRoot "levels\solutions\L01_First_Stack.replay.json"

  if (-not (Test-Path $cliProject)) { throw "CLI project not found: $cliProject" }
  if (-not (Test-Path $levelPath)) { throw "Parity fixture level not found: $levelPath" }
  if (-not (Test-Path $replayPath)) { throw "Parity fixture replay not found: $replayPath" }
  if (-not (Test-Path $unityProjectRoot)) { throw "Unity project not found: $unityProjectRoot" }

  # Execute replay via CLI and capture determinism hash
  Write-Host ">> CLI replay parity (L01_First_Stack)"
  $cliArgs = @(
    "run",
    "--project", $cliProject,
    "--configuration", $Configuration,
    "--no-build",
    "--",
    "--level", $levelPath,
    "--replay", $replayPath
  )

  $cliOutput = RunDotnet $cliArgs
  $cliHash = GetDeterminismHash $cliOutput
  Write-Host "CLI DeterminismHash: $cliHash"

  # Execute replay via Unity in batchmode
  Write-Host ">> Unity replay parity (L01_First_Stack)"
  
  $unityOutputFile = Join-Path ([System.IO.Path]::GetTempPath()) ("unity-parity-" + [Guid]::NewGuid().ToString("N") + ".txt")
  
  try {
    # Detect Unity path (common installation paths)
    $unityExe = $null
    $possiblePaths = @(
      "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe",
      "C:\Program Files (x86)\Unity\Editor\Unity.exe",
      "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe"
    )
    
    foreach ($pattern in $possiblePaths) {
      $found = @(Get-Item -LiteralPath $pattern -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1)
      if ($found.Count -gt 0) {
        $unityExe = $found[0].FullName
        Write-Host "Found Unity at: $unityExe"
        break
      }
    }
    
    if (-not $unityExe) {
      Write-Host "⚠ WARNING: Unity not found on this system. Skipping Unity parity check."
      Write-Host "   (This is OK for local development; GitHub Actions will verify parity)"
    } else {
      $unityArgs = @(
        "-projectPath", $unityProjectRoot,
        "-batchmode",
        "-nographics",
        "-quit",
        "-executeMethod", "Floodline.Client.ReplayTester.ExecuteReplay",
        "--replay-file", $replayPath,
        "--level-file", $levelPath,
        "--output-file", $unityOutputFile
      )

      $unityLogFile = Join-Path ([System.IO.Path]::GetTempPath()) ("unity-parity-log-" + [Guid]::NewGuid().ToString("N") + ".log")
      
      $process = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -LogFilePath $unityLogFile -PassThru -Wait -NoNewWindow
      
      if ($process.ExitCode -ne 0) {
        Write-Host "Unity execution log:"
        if (Test-Path $unityLogFile) {
          Get-Content -LiteralPath $unityLogFile
        }
        throw "Unity batch mode failed with exit code $($process.ExitCode)"
      }

      if (-not (Test-Path $unityOutputFile)) {
        throw "Unity did not produce output file: $unityOutputFile"
      }

      $unityOutput = Get-Content -LiteralPath $unityOutputFile -Raw
      $unityHashMatch = [regex]::Match($unityOutput, "DeterminismHash:\s*(\S+)")
      if (-not $unityHashMatch.Success) {
        throw "Unity did not output DeterminismHash. Output: $unityOutput"
      }

      $unityHash = $unityHashMatch.Groups[1].Value.Trim()
      Write-Host "Unity DeterminismHash: $unityHash"

      if ($cliHash -ne $unityHash) {
        throw "Unity parity mismatch: CLI=$cliHash Unity=$unityHash"
      }

      Write-Host "Unity parity OK: $cliHash"
    }
  }
  finally {
    Remove-Item -Path $unityOutputFile -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $unityLogFile -Force -ErrorAction SilentlyContinue
  }
}

# Formatting (only when gate is introduced)
if ($IncludeFormat) {
  Run "dotnet format `"$($sln.FullName)`" --verify-no-changes"
}

# Future: keep switch names stable so backlog items don't churn; add enforcement when tooling exists.

Write-Host "CI OK: Scope=$Scope Config=$Configuration LockedRestore=$LockedRestore IncludeFormat=$IncludeFormat Golden=$Golden"
exit 0
