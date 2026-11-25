param(
    [string]$UnityPath,
    [switch]$Test,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$generatedRoot = Join-Path $sourceRoot 'Builds/ConnectomeProject'
$logRoot = Join-Path $sourceRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
if (-not $UnityPath) {
    $versionLine = Get-Content (Join-Path $sourceRoot 'ProjectSettings/ProjectVersion.txt') | Select-Object -First 1
    $editorVersion = ($versionLine -split ': ', 2)[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity not found: $UnityPath" }

# Only a marked, generated project may be refreshed; never touch the open source project.
$marker = Join-Path $generatedRoot 'ProjectSettings/ConnectomeStandalone.txt'
if ((Test-Path -LiteralPath $generatedRoot) -and -not (Test-Path -LiteralPath $marker)) {
    throw "Destination exists without generated-project marker: $generatedRoot"
}
New-Item -ItemType Directory -Path (Join-Path $generatedRoot 'ProjectSettings'), (Join-Path $generatedRoot 'Packages') -Force | Out-Null
Set-Content -LiteralPath $marker -Value 'Generated isolated desktop connectome project. Safe to regenerate with Build-Connectome.ps1.' -Encoding UTF8

$directories = @('Assets/_Project/Experimental', 'Assets/_Project/Art/Fonts', 'Assets/_Project/Settings')
foreach ($relative in $directories) {
    $destination = Join-Path $generatedRoot $relative
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path (Join-Path $sourceRoot $relative) '*') -Destination $destination -Recurse -Force
    $meta = Join-Path $sourceRoot ($relative + '.meta')
    if (Test-Path -LiteralPath $meta) { Copy-Item -LiteralPath $meta -Destination ($destination + '.meta') -Force }
}
Copy-Item -LiteralPath (Join-Path $sourceRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $generatedRoot 'ProjectSettings/ProjectVersion.txt') -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'tools/connectome/standalone-manifest.json') -Destination (Join-Path $generatedRoot 'Packages/manifest.json') -Force
# Input System only, no XR loaders or vendor packages. Unity completes remaining defaults.
$settings = Join-Path $generatedRoot 'ProjectSettings/ProjectSettings.asset'
if (-not (Test-Path -LiteralPath $settings)) {
    @'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 28
  activeInputHandler: 1
'@ | Set-Content -LiteralPath $settings -Encoding UTF8
}

function Invoke-ConnectomeUnity([string]$Name, [string]$Arguments) {
    $log = Join-Path $logRoot ("connectome-" + $Name + '.log')
    $fullArguments = '-batchmode -projectPath "{0}" -logFile "{1}" {2}' -f $generatedRoot, $log, $Arguments
    $process = Start-Process -FilePath $UnityPath -ArgumentList $fullArguments -WorkingDirectory $generatedRoot -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$Name failed ($($process.ExitCode)); inspect $log" }
}

Start-Transcript -Path (Join-Path $logRoot 'connectome-build-run.log') -Force | Out-Null
try {
    Invoke-ConnectomeUnity 'prepare' '-quit -executeMethod FruitFlyParenting.Connectome.Editor.ConnectomeSetup.PrepareStandalone'
    if ($PrepareOnly) { Write-Output "Prepared $generatedRoot"; return }
    if ($Test) {
        foreach ($platform in @('EditMode', 'PlayMode')) {
            $result = Join-Path $logRoot ("connectome-" + $platform + '.xml')
            Invoke-ConnectomeUnity $platform ('-runTests -testPlatform {0} -testFilter FruitFlyParenting.Connectome.Tests -screen-width 1440 -screen-height 900 -testResults "{1}"' -f $platform, $result)
            [xml]$report = Get-Content -LiteralPath $result -Raw
            $run = $report.'test-run'
            if ($run.result -ne 'Passed' -or [int]$run.total -eq 0 -or [int]$run.skipped -ne 0) { throw "$platform did not pass: $result" }
            Write-Output "$platform`: $($run.passed)/$($run.total) passed"
        }
    }
    Invoke-ConnectomeUnity 'build' '-quit -executeMethod FruitFlyParenting.Connectome.Editor.ConnectomeSetup.BuildViewer'
    Write-Output "Viewer: $generatedRoot/Builds/Step13/ConnectomeExperiment/ConnectomeExperiment.exe"
} finally { Stop-Transcript | Out-Null }
