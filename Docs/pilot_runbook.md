# Pilot Runbook

Status: Active operator guide  
Scope: local pilot validation for `VS01_HostageApartment`,
`VS02_DataRaidOffice`, and `VS03_StealthSafehouse`

This runbook is the operator-facing companion to
[pilot_testing_preparation_plan.md](pilot_testing_preparation_plan.md) and the
existing [pilot_operation_checklist.md](pilot_operation_checklist.md).

## 1. Operating Rules

- Local Unity batchmode is the current acceptance source.
- GitHub Actions is preview-only until a licensed Unity runner is configured.
- Generated mission artifacts are local outputs and must not be hand-edited.
- `verify` is the machine-readable decision source.
- `write_manifest` only happens after verification `PASS`.
- `effectiveSeed` stays pipeline-owned.

## 2. Prerequisites

- Unity editor available at
  `C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe`
- Repository open at `E:\Games\Breach\BreachScenarioEngine`
- Canonical pilot missions present under `UserMissionSources/missions/`
- `Artifacts/PilotReports/` and `Artifacts/Validation/` writable

## 3. One-Command Validation

Run the local pilot validation script from the repository root:

```powershell
& .\Scripts\run_pilot_validation.ps1
```

Optional scene-preview materialization can be enabled with the script switch
documented in the script header.

The script runs, in order:

1. `BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll`
2. Unity EditMode tests
3. .NET server tests

## 4. Manual Command Sequence

If the script is not available, run the commands directly.

### 4.1 Pilot mission pipeline

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -projectPath 'E:\Games\Breach\BreachScenarioEngine' -executeMethod BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll -logFile 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\PilotMissionPipelineCi.log' -quit
```

Expected outputs:

- `Artifacts/PilotReports/pilot_summary.md`
- `Artifacts/PilotReports/VS01_verification_summary.json`
- `Artifacts/PilotReports/VS01_generation_manifest.json`
- `Artifacts/PilotReports/VS02_verification_summary.json`
- `Artifacts/PilotReports/VS02_generation_manifest.json`
- `Artifacts/PilotReports/VS03_verification_summary.json`
- `Artifacts/PilotReports/VS03_generation_manifest.json`

### 4.2 Unity EditMode tests

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -projectPath 'E:\Games\Breach\BreachScenarioEngine' -runTests -testPlatform EditMode -testResults 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\EditModeTestResults.xml' -logFile 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation\EditModeTests.log'
```

Expected outputs:

- `Artifacts/Validation/EditModeTestResults.xml`
- `Artifacts/Validation/EditModeTests.log`

### 4.3 .NET server tests

```powershell
dotnet test 'dotnet-prototype\tests\BreachScenarioEngine.Mcp.Server.Tests\BreachScenarioEngine.Mcp.Server.Tests.csproj' --logger "trx;LogFileName=DotnetServerTests.trx" --results-directory 'E:\Games\Breach\BreachScenarioEngine\Artifacts\Validation' --verbosity minimal
```

Expected outputs:

- `Artifacts/Validation/DotnetServerTests.trx`

## 5. Artifact Checks

Accept the pilot run only if all of the following are true:

- every pilot mission reports `PASS`
- `verification_summary.json` is `PASS` for each mission
- `generation_manifest.json` is `PASS` for each mission
- `effectiveSeed` is non-zero after `PASS`
- `.generation.lock` is absent after completion
- the scene preview, if enabled, materializes without replacing user-owned
  objects

## 6. Stale Lock Policy

- If `.generation.lock` exists while no Unity process is actively running, stop
  and inspect the last failed command before deleting anything.
- Do not delete generated mission artifacts by default.
- Only remove stale generated outputs when the operator explicitly chooses a
  cleanup path and can explain the reason.

## 7. Failure Triage

Use this order:

1. Read `Artifacts/PilotReports/pilot_summary.md`.
2. Inspect the mission-specific verification summary and manifest copy.
3. Inspect the log from the failed command in `Artifacts/Validation/`.
4. Compare the failed action against the required order:
   `validate_template`, `compile_payload`, `generate_layout`,
   `place_entities`, `verify`, `write_manifest`.
5. Confirm the failure is not caused by a stale layout or stale manifest.

## 8. Go / No-Go

Go only when:

- local Unity batchmode passes for all three pilot missions
- EditMode tests pass
- .NET server tests pass
- no stale lock remains
- the copied pilot reports are complete

No-Go when:

- any mission fails
- any action order violation appears
- any manifest is written before PASS
- any artifact mismatch is detected

