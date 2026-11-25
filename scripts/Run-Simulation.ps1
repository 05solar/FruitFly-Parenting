param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent),
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe',
    [ValidateRange(1,100000)][int]$Count = 1000,
    [switch]$Probe
)
$ErrorActionPreference = 'Stop'
$simulationProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$simulationLog = Join-Path $simulationProject 'Logs/step16-simulation.log'
New-Item -ItemType Directory -Path (Split-Path $simulationLog) -Force | Out-Null
$simulationArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$simulationProject+'"'),
    '-executeMethod','FruitFlyParenting.Simulation.SimulationRunnerMenu.Run','-simulationCount',$Count,
    '-logFile',('"'+$simulationLog+'"'))
if ($Probe) { $simulationArguments += '-simulationProbe' }
$simulationProcess = Start-Process -FilePath $UnityPath -ArgumentList $simulationArguments -WindowStyle Hidden -PassThru
$simulationProcess.WaitForExit()
if ($simulationProcess.ExitCode -ne 0) { throw "Simulation failed (exit $($simulationProcess.ExitCode)). See $simulationLog" }
Get-Content -LiteralPath (Join-Path $simulationProject 'SimulationReports/summary.json') -Encoding UTF8
