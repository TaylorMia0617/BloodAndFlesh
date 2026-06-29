param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path
)

$unityLockPath = Join-Path $ProjectPath "Temp\UnityLockfile"
$editModeResultsPath = Join-Path $ProjectPath "Logs\editmode-test-results.xml"
$sceneAuditPath = Join-Path $ProjectPath "Logs\semantic-world-scene-audit.json"
$seedReportPath = Join-Path $ProjectPath "Logs\semantic-world-validation-report.json"

Write-Host "Semantic world acceptance status"
Write-Host "Project path: $ProjectPath"
Write-Host "Unity lock present: $(Test-Path $unityLockPath)"

if (Test-Path $unityLockPath) {
    $lock = Get-Item $unityLockPath
    Write-Host "Unity lock file: $($lock.FullName)"
    Write-Host "Unity lock last write: $($lock.LastWriteTime)"

    $unityProcesses = Get-Process -Name Unity -ErrorAction SilentlyContinue
    if ($unityProcesses) {
        Write-Host "Unity processes:"
        foreach ($process in $unityProcesses) {
            $title = $process.MainWindowTitle
            if ([string]::IsNullOrWhiteSpace($title)) {
                $title = "(no window title)"
            }

            Write-Host "  Id=$($process.Id) Title=$title"
        }
    }
    else {
        Write-Host "No Unity process was found, but the lock file still exists."
        Write-Host "If Unity is fully closed, inspect the Temp directory before removing any generated lock file."
    }
}

Write-Host "EditMode test results present: $(Test-Path $editModeResultsPath)"
Write-Host "Scene audit report present: $(Test-Path $sceneAuditPath)"
Write-Host "Seed validation report present: $(Test-Path $seedReportPath)"

if (-not (Test-Path $unityLockPath) -and
    -not (Test-Path $editModeResultsPath) -and
    -not (Test-Path $sceneAuditPath) -and
    -not (Test-Path $seedReportPath)) {
    Write-Host "Unity appears ready for validation. Run Tools\RunSemanticWorldAcceptance.ps1."
}
