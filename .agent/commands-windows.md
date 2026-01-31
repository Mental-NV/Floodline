# Canonical Commands (Windows)

Use PowerShell unless the repo standardizes on something else.

## .NET (general)
- Restore:
  - `dotnet restore`
- Build:
  - `dotnet build -c Release`
- Test:
  - `dotnet test -c Release`
- Format (if enabled):
  - `dotnet format`

## Unity (batchmode tests)
Adjust UNITY_EXE path per environment.

### EditMode tests
`& "<UNITY_EXE>" -batchmode -nographics -quit -projectPath "<PROJECT_PATH>" -runTests -testPlatform editmode -testResults "<PROJECT_PATH>\TestResults\editmode.xml" -logFile "<PROJECT_PATH>\TestResults\editmode.log"`

### PlayMode tests
`& "<UNITY_EXE>" -batchmode -nographics -quit -projectPath "<PROJECT_PATH>" -runTests -testPlatform playmode -testResults "<PROJECT_PATH>\TestResults\playmode.xml" -logFile "<PROJECT_PATH>\TestResults\playmode.log"`

## Recommended local shortcuts (optional)
If you add scripts later, keep a single entrypoint:
- `./scripts/test.ps1`
- `./scripts/build.ps1`
- `./scripts/ci.ps1`
