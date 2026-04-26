<#
.SYNOPSIS
Runs the local pilot validation sequence for the three canonical missions.

.DESCRIPTION
Executes the pilot mission pipeline, Unity EditMode tests, and .NET server
tests in order.

.PARAMETER MaterializeScenePreview
Sets BSE_CI_MATERIALIZE_SCENE_PREVIEW=1 before the Unity pipeline run.

.PARAMETER UnityExe
Overrides the Unity editor executable path.

.PARAMETER ProjectPath
Overrides the Unity project path.
#>

param(
    [switch]$MaterializeScenePreview,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,

        [Parameter(Mandatory = $true)]
        [string]$LogFile,

        [string]$CaptureOutputFile
    )

    Write-Host "==> $Name"
    if ($CaptureOutputFile) {
        & $Command 2>&1 | Tee-Object -FilePath $CaptureOutputFile
    }
    else {
        & $Command
    }
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode. See $LogFile"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validationRoot = Join-Path $repoRoot 'Artifacts\Validation'
$pilotReportsRoot = Join-Path $repoRoot 'Artifacts\PilotReports'

New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
New-Item -ItemType Directory -Force -Path $pilotReportsRoot | Out-Null

if (-not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity executable not found at $UnityExe"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found on PATH."
}

if ($MaterializeScenePreview) {
    $env:BSE_CI_MATERIALIZE_SCENE_PREVIEW = '1'
    Write-Host 'Scene preview materialization is enabled for the pilot mission pipeline.'
}
else {
    Remove-Item Env:\BSE_CI_MATERIALIZE_SCENE_PREVIEW -ErrorAction SilentlyContinue
}

$pilotLog = Join-Path $validationRoot 'PilotMissionPipelineCi.log'
$editModeLog = Join-Path $validationRoot 'EditModeTests.log'
$editModeResults = Join-Path $validationRoot 'EditModeTestResults.xml'
$dotnetResults = $validationRoot
$dotnetProject = Join-Path $repoRoot 'dotnet-prototype\tests\BreachScenarioEngine.Mcp.Server.Tests\BreachScenarioEngine.Mcp.Server.Tests.csproj'

Invoke-LoggedCommand -Name 'Pilot mission pipeline' -LogFile $pilotLog -Command {
    & $UnityExe -batchmode -projectPath $ProjectPath -executeMethod BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll -logFile $pilotLog -quit -nographics
}

Invoke-LoggedCommand -Name 'Unity EditMode tests' -LogFile $editModeLog -Command {
    & $UnityExe -batchmode -projectPath $ProjectPath -runTests -testPlatform EditMode -testResults $editModeResults -logFile $editModeLog -quit -nographics
}

Invoke-LoggedCommand -Name '.NET server tests' -LogFile (Join-Path $validationRoot 'DotnetServerTests.log') -CaptureOutputFile (Join-Path $validationRoot 'DotnetServerTests.log') -Command {
    & dotnet test $dotnetProject --logger 'trx;LogFileName=DotnetServerTests.trx' --results-directory $dotnetResults --verbosity minimal
}

Write-Host ''
Write-Host 'Local pilot validation finished successfully.'
Write-Host "Pilot reports: $pilotReportsRoot"
Write-Host "Validation logs: $validationRoot"
