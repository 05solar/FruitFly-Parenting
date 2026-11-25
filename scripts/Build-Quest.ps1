param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe',
    [switch]$Development,
    [switch]$PrepareOnly,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskStage = Join-Path $taskRoot 'Builds/QuestProject'
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable missing: $UnityPath" }
# An independent copy includes the user's installed SDK and XR settings. Source scenes are never rebuilt.
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskStage $folder))
    $taskAllowed = [IO.Path]::GetFullPath($taskStage) + [IO.Path]::DirectorySeparatorChar
    if (-not $taskDestination.StartsWith($taskAllowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging destination.' }
    foreach ($taskAncestor in @($taskStage, $taskDestination)) {
        if ((Test-Path -LiteralPath $taskAncestor) -and ((Get-Item -LiteralPath $taskAncestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Staging paths must not be links.' }
    }
    & robocopy (Join-Path $taskRoot $folder) $taskDestination /MIR /XJ /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
}
New-Item -ItemType Directory -Force (Join-Path $taskStage 'Logs') | Out-Null
if ($PrepareOnly) { Write-Output "Prepared $taskStage"; exit 0 }
function Invoke-QuestUnity([string]$Arguments, [string]$LogName) {
    $taskArgs = '-batchmode -projectPath "{0}" -buildTarget Android {1} -logFile "{0}/Logs/{2}.log"' -f $taskUnityProject, $Arguments, $LogName
    $taskProcess = Start-Process -FilePath $UnityPath -ArgumentList $taskArgs -WorkingDirectory $taskUnityProject -WindowStyle Hidden -PassThru -Wait
    if ($taskProcess.ExitCode -ne 0) { throw "Unity failed ($($taskProcess.ExitCode)): $taskStage/Logs/$LogName.log" }
}
$taskUnityProject = $taskStage
$taskDrive = $null
$taskOldGradle = $env:GRADLE_USER_HOME
$taskOldTemp = $env:TEMP
$taskOldTmp = $env:TMP
try {
    if ($taskStage -match '[^\x00-\x7F]') {
        # Unity Android rejects non-ASCII project paths. Map this staging copy only; no source files move.
        $taskDrive = @('Z:', 'Y:', 'X:', 'W:', 'V:') | Where-Object { -not (Test-Path ($_ + '/')) } | Select-Object -First 1
        if (-not $taskDrive) { throw 'No free drive letter for the Android build path.' }
        & subst $taskDrive $taskStage
        if ($LASTEXITCODE -ne 0) { $taskDrive = $null; throw 'Could not map an ASCII Android project path.' }
        $taskUnityProject = $taskDrive + '/'
    }
    $env:GRADLE_USER_HOME = Join-Path $taskUnityProject 'Logs/GradleCache'
    $env:TEMP = Join-Path $taskUnityProject 'Logs/BuildTemp'
    $env:TMP = $env:TEMP
    New-Item -ItemType Directory -Force $env:TEMP | Out-Null
    Invoke-QuestUnity '-quit -executeMethod FruitFlyParenting.Editor.QuestBuildSetup.Configure' 'quest-setup'
    if ($Test) {
        Invoke-QuestUnity '-runTests -testPlatform EditMode -testResults Logs/quest-editmode.xml' 'quest-tests'
        [xml]$taskResults = Get-Content -LiteralPath (Join-Path $taskStage 'Logs/quest-editmode.xml') -Raw
        if ($taskResults.'test-run'.result -ne 'Passed') { throw 'Quest EditMode tests did not pass.' }
    }
    $taskMethod = if ($Development) { 'BuildDevelopment' } else { 'BuildRelease' }
    Invoke-QuestUnity ('-quit -executeMethod FruitFlyParenting.Editor.QuestBuildSetup.' + $taskMethod) 'quest-build'
    Get-ChildItem -LiteralPath (Join-Path $taskStage 'Builds/Step15') -Filter '*.apk' | Select-Object FullName, Length
}
finally {
    $env:GRADLE_USER_HOME = $taskOldGradle
    $env:TEMP = $taskOldTemp
    $env:TMP = $taskOldTmp
    if ($taskDrive) { & subst $taskDrive /D }
}
