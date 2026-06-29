param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$UnityPath = "",
    [string]$LogPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath "Logs\semantic-world-batch.log"
}

$sceneAuditPath = Join-Path $ProjectPath "Logs\semantic-world-scene-audit.json"
$seedReportPath = Join-Path $ProjectPath "Logs\semantic-world-validation-report.json"

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $candidates = @(
        "D:\Unity\Unity Hub\2022.3.23f1c1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\2022.3.23f1c1\Editor\Unity.exe"
    )

    $UnityPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path $UnityPath)) {
    Write-Error "Unity 2022.3.23f1c1 executable was not found. Pass -UnityPath explicitly."
    exit 2
}

$unityLockPath = Join-Path $ProjectPath "Temp\UnityLockfile"
if (Test-Path $unityLockPath) {
    Write-Host "Semantic world validation cannot run while this project is open in the Unity Editor."
    Write-Host "Project path: $ProjectPath"
    Write-Host "Unity lock file: $unityLockPath"
    Write-Host "Close the Unity Editor for this project, then rerun this script."
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path $LogPath -Parent) | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $LogPath, $sceneAuditPath, $seedReportPath

& $UnityPath `
    -batchmode `
    -quit `
    -projectPath $ProjectPath `
    -executeMethod SemanticWorldValidationMenu.BatchCreateCatalogAndValidate `
    -logFile $LogPath

$unityExitCode = $LASTEXITCODE
$logText = ""

if (Test-Path $LogPath) {
    $logText = Get-Content -LiteralPath $LogPath -Raw
}

$fatalLogPatterns = @(
    "Fatal Error",
    "another Unity instance is running with this project open",
    "HandleProjectAlreadyOpenInAnotherInstance",
    "Aborting batchmode",
    "executeMethod class .* could not be found",
    "Scripts have compiler errors",
    "Semantic world batch validation failed"
)

$fatalLog = $false
foreach ($pattern in $fatalLogPatterns) {
    if ($logText -match $pattern) {
        $fatalLog = $true
        break
    }
}

$missingReports = -not (Test-Path $sceneAuditPath) -or -not (Test-Path $seedReportPath)

if ($unityExitCode -ne 0 -or $fatalLog -or $missingReports) {
    Write-Host "Semantic world validation did not complete."
    Write-Host "Unity exit code: $unityExitCode"
    Write-Host "Log path: $LogPath"
    Write-Host "Scene audit exists: $(Test-Path $sceneAuditPath)"
    Write-Host "Seed report exists: $(Test-Path $seedReportPath)"

    if ($fatalLog) {
        Write-Host "The Unity log contains a fatal validation pattern."
    }

    if ($missingReports) {
        Write-Host "Expected JSON reports were not produced."
    }

    exit 1
}

Write-Host "Semantic world validation completed."
Write-Host "Scene audit: $sceneAuditPath"
Write-Host "Seed report: $seedReportPath"
exit 0
