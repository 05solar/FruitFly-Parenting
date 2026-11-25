param(
    [ValidateSet('observer','overprotective','rebellious','relationship')][string]$Style = 'observer',
    [string]$BuildDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Builds/FlatHUD')
)
$ErrorActionPreference = 'Stop'
$playtestRoot = Split-Path $PSScriptRoot -Parent
$playtestExecutable = Join-Path $BuildDirectory 'FruitFlyParenting.exe'
if (-not (Test-Path -LiteralPath $playtestExecutable)) { throw "Build missing: $playtestExecutable" }
$playtestOutput = Join-Path $playtestRoot 'PlaytestReports'
New-Item -ItemType Directory -Path $playtestOutput -Force | Out-Null
$playtestLog = Join-Path $playtestOutput ('player-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
$playtestArguments = @('-playtest','-playtest-style',$Style,'-playtest-source','player',
    '-playtest-output',('"'+$playtestOutput+'"'),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
    '-logFile',('"'+$playtestLog+'"'))
# A visible window is intentional: this is the human playtest launcher.
$playtestPlayer = Start-Process -FilePath $playtestExecutable -ArgumentList $playtestArguments -PassThru
Write-Output "Playtest started (PID $($playtestPlayer.Id), style $Style). F6: mark moment; F7: screenshot."
Write-Output "Reports and isolated saves: $playtestOutput"
Write-Output "Complete the human review in docs/playtest-log-template.md after playing."
