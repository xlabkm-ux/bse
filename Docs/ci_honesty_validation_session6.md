# CI Honesty and Validation Session 6

Date: 2026-04-26
Track: Post-Transfer Hardening
Session: 6

## Summary

The GitHub Actions pilot workflow is currently a CI preview placeholder, not a
licensed GitHub-hosted Unity runner. The workflow text now states that
explicitly and prints the local Unity batchmode command instead of claiming to
run EditMode pipeline validation on GitHub Actions.

Local Unity validation was run against Unity `6000.4.3f1`.

## Workflow Decision

`.github/workflows/pilot-mission-ci.yml` remains useful as a visible CI preview
and artifact upload shell, but it does not execute Unity on GitHub Actions yet.
It must not be treated as a remote acceptance gate until a licensed Unity runner
is configured.

## Local Validation

Command used for pilot mission execution:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -projectPath 'E:\Games\Breach\BreachScenarioEngine' -executeMethod BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll -logFile 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\PilotMissionPipelineCi.log' -quit
```

Result:

- Unity batchmode returned exit code `0`.
- Script compilation completed with `Tundra build success`.
- `PilotMissionPipelineCi` logged `Pilot mission pipeline passed for all missions.`
- `VS01_HostageApartment`, `VS02_DataRaidOffice`, and
  `VS03_StealthSafehouse` each produced `PASS` verification summaries and
  `PASS` generation manifests.

EditMode test command attempted:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -projectPath 'E:\Games\Breach\BreachScenarioEngine' -runTests -testPlatform EditMode -testResults 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\EditModeTestResults.xml' -logFile 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\EditModeTests.log'
```

Observed result:

- Unity batchmode returned exit code `0`.
- `EditModeTestResults.xml` was emitted.
- Unity EditMode tests passed: `33/33`.

.NET server test command:

```powershell
dotnet test 'dotnet-prototype\tests\BreachScenarioEngine.Mcp.Server.Tests\BreachScenarioEngine.Mcp.Server.Tests.csproj' --logger "trx;LogFileName=DotnetServerTests.trx" --results-directory 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation' --verbosity minimal
```

Result:

- .NET server tests passed: `93/93`.

## Remaining Risks

- GitHub Actions is not a real Unity acceptance gate yet.
- Current acceptance evidence for this session is local validation: Unity
  EditMode `33/33`, .NET server tests `93/93`, and
  `PilotMissionPipelineCi.RunAll` PASS for all three pilot missions.
