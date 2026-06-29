param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"

$editModeScript = Join-Path $ProjectPath "Tools\RunEditModeTests.ps1"
$semanticScript = Join-Path $ProjectPath "Tools\RunSemanticWorldValidation.ps1"

if (-not (Test-Path $editModeScript)) {
    Write-Error "Missing EditMode validation script: $editModeScript"
    exit 2
}

if (-not (Test-Path $semanticScript)) {
    Write-Error "Missing semantic world validation script: $semanticScript"
    exit 2
}

Write-Host "Step 1/3: Building C# projects..."
dotnet build (Join-Path $ProjectPath "TopDownActRogue.sln") -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "C# build failed."
    exit $LASTEXITCODE
}

$unityArgs = @()
if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
    $unityArgs += "-UnityPath"
    $unityArgs += $UnityPath
}

Write-Host "Step 2/3: Running Unity EditMode tests..."
& powershell -NoProfile -ExecutionPolicy Bypass -File $editModeScript -ProjectPath $ProjectPath @unityArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "EditMode tests did not pass."
    exit $LASTEXITCODE
}

Write-Host "Step 3/3: Running semantic world validation..."
& powershell -NoProfile -ExecutionPolicy Bypass -File $semanticScript -ProjectPath $ProjectPath @unityArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Semantic world validation did not pass."
    exit $LASTEXITCODE
}

Write-Host "Semantic world acceptance passed."
exit 0
