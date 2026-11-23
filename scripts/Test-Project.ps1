param(
    [string]$UnityPath,
    [ValidateSet('All', 'EditMode', 'PlayMode')]
    [string]$Platform = 'All'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $UnityPath) {
    $versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Select-Object -First 1
    $editorVersion = ($versionLine -split ': ', 2)[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity not found: $UnityPath" }
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$platforms = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }
foreach ($testPlatform in $platforms) {
    $resultPath = Join-Path $logDirectory ($testPlatform.ToLowerInvariant() + '-results.xml')
    $logPath = Join-Path $logDirectory ($testPlatform.ToLowerInvariant() + '-tests.log')
    if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }
    # Graphics stay enabled: PlayMode includes a rendered-frame regression test.
    $arguments = '-batchmode -projectPath "{0}" -runTests -testPlatform {1} -testResults "{2}" -logFile "{3}"' -f `
        $projectRoot, $testPlatform, $resultPath, $logPath
    $testProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $projectRoot `
        -WindowStyle Hidden -PassThru
    $testProcess.WaitForExit()
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "No $testPlatform report. Inspect $logPath" }
    [xml]$report = Get-Content -LiteralPath $resultPath -Raw
    $run = $report.'test-run'
    Write-Output "$testPlatform`: $($run.result), $($run.passed)/$($run.total) passed, $($run.failed) failed, $($run.skipped) skipped."
    if ($testProcess.ExitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.skipped -ne 0) {
        throw "Tests did not all pass. Inspect $resultPath and $logPath"
    }
}
