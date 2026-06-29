param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$UnityPath = "",
    [string]$LogPath = "",
    [string]$ResultsPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath "Logs\editmode-tests.log"
}

if ([string]::IsNullOrWhiteSpace($ResultsPath)) {
    $ResultsPath = Join-Path $ProjectPath "Logs\editmode-test-results.xml"
}

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
    Write-Host "EditMode tests cannot run while this project is open in the Unity Editor."
    Write-Host "Project path: $ProjectPath"
    Write-Host "Unity lock file: $unityLockPath"
    Write-Host "Close the Unity Editor for this project, then rerun this script."
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path $LogPath -Parent) | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $LogPath, $ResultsPath

& $UnityPath `
    -batchmode `
    -quit `
    -projectPath $ProjectPath `
    -runTests `
    -testPlatform EditMode `
    -testResults $ResultsPath `
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
    "Scripts have compiler errors",
    "Compilation failed",
    "Test run failed"
)

$fatalLog = $false
foreach ($pattern in $fatalLogPatterns) {
    if ($logText -match $pattern) {
        $fatalLog = $true
        break
    }
}

if ($unityExitCode -ne 0 -or $fatalLog -or -not (Test-Path $ResultsPath)) {
    Write-Host "EditMode tests did not complete."
    Write-Host "Unity exit code: $unityExitCode"
    Write-Host "Log path: $LogPath"
    Write-Host "Results exist: $(Test-Path $ResultsPath)"

    if ($fatalLog) {
        Write-Host "The Unity log contains a fatal test-run pattern."
    }

    exit 1
}

[xml]$results = Get-Content -LiteralPath $ResultsPath -Raw
$testRun = $results.SelectSingleNode("//test-run")

if ($null -eq $testRun) {
    Write-Host "EditMode test results were produced but do not contain a test-run root."
    exit 1
}

$failed = [int]$testRun.failed
$inconclusive = [int]$testRun.inconclusive
$total = [int]$testRun.total

if ($total -le 0 -or $failed -gt 0 -or $inconclusive -gt 0) {
    Write-Host "EditMode tests completed with unexpected results."
    Write-Host "Total: $total"
    Write-Host "Failed: $failed"
    Write-Host "Inconclusive: $inconclusive"
    exit 1
}

Write-Host "EditMode tests completed."
Write-Host "Total: $total"
Write-Host "Results: $ResultsPath"
exit 0
