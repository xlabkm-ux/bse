# Pilot Testing Preparation Plan

Status: Implementation handoff  
Project: `Breach Scenario Engine`  
Branch target: `codex/pilot-readiness`

This repository-local plan adapts the pilot-preparation handoff into a compact
execution roadmap that can be resumed across Codex sessions without relying on
chat history.

## 1. Mission

Prepare the repository for controlled local pilot validation of:

- `VS01_HostageApartment`
- `VS02_DataRaidOffice`
- `VS03_StealthSafehouse`

The pilot is a local technical validation. Unity batchmode on the developer
machine is the acceptance source. GitHub Actions stays a preview shell until a
licensed Unity runner exists.

Keep the v2.3 mission-pipeline invariants intact:

1. `generate_layout` runs before `place_entities`.
2. `place_entities` fails without a current layout graph.
3. `place_entities` fails on stale `layoutRevisionId`.
4. `verify` is the machine-readable decision source.
5. `write_manifest` writes `generation_manifest.json` only after verification
   `PASS`.
6. `effectiveSeed` remains pipeline-owned.
7. Generated artifacts stay local outputs and are not hand-edited.
8. Machine decisions stay JSON-readable.
9. Runtime preview consumes generated artifacts, not the other way around.

## 2. Non-Goals

Do not spend the first pilot-prep pass on:

- production build packaging
- Android build work
- external playtest distribution
- art polish
- combat balance tuning
- full Addressables migration
- GitHub-hosted Unity runner setup
- large mission-pipeline contract rewrites
- replacing the YAML parser unless a real failing test forces it

## 3. Working Assumptions

- Unity editor: `6000.4.3f1`
- Canonical missions:
  - `VS01_HostageApartment`
  - `VS02_DataRaidOffice`
  - `VS03_StealthSafehouse`
- Canonical pipeline entry point:
  - `BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll`
- Canonical mission actions:
  - `validate_template`
  - `compile_payload`
  - `generate_layout`
  - `place_entities`
  - `verify`
  - `write_manifest`
  - `cleanup_generation_lock`
- Generated artifacts:
  - `mission_payload.generated.json`
  - `mission_compile_report.json`
  - `mission_layout.generated.json`
  - `mission_entities.generated.json`
  - `mission_state.json`
  - `.generation.lock`
  - `generation_manifest.json`
  - `verification_summary.json`

## 4. Summary Execution Plan by Codex Sessions

### Session 0 - Repository Intake and Safety Baseline

Goal:

- confirm the repository state before changing files

Inspect:

- `README.md`
- `Docs/index.md`
- `Docs/pilot_operation_checklist.md`
- `Docs/ci_honesty_validation_session6.md`
- `.github/workflows/pilot-mission-ci.yml`
- `Assets/Editor/CI/PilotMissionPipelineCi.cs`
- `.gitignore`

Do:

1. Confirm the active branch and create `codex/pilot-readiness` if needed.
2. Confirm generated artifacts are ignored by git.
3. Confirm GitHub Actions is preview-only, not a Unity acceptance gate.
4. Confirm the three pilot mission folders exist.
5. Avoid runtime code changes unless a blocking inconsistency is found.

Exit criteria:

- branch strategy confirmed
- generated artifacts still unstaged
- current pilot risks noted in the handoff

### Session 1 - Pilot Documentation and Runbook

Goal:

- make the pilot executable without chat history

Create:

- `Docs/pilot_runbook.md`
- `Docs/pilot_result_report_template.md`

Update:

- `README.md`
- `Docs/README.md`
- `Docs/index.md`
- `Docs/project_documentation.md`

Do:

1. Document scope, non-goals, commands, stale-lock policy, and Go/No-Go rules.
2. Add the operator report template.
3. Link the new docs from all navigation entry points.
4. State clearly that local Unity batchmode is the acceptance source.
5. State clearly that GitHub Actions is preview-only until a real Unity runner
   exists.

Exit criteria:

- a new reader can find and execute the pilot process from the docs
- docs do not imply that GitHub Actions runs Unity

### Session 2 - Runner Diagnostics

Goal:

- make `PilotMissionPipelineCi.RunAll` easier to diagnose from its report output

Inspect:

- `Assets/Editor/CI/PilotMissionPipelineCi.cs`
- `Docs/pilot_runbook.md`

Do:

1. Confirm the runner executes the canonical actions in order for all three
   pilot missions.
2. Confirm it stops a mission on the first failed action.
3. Ensure the summary report includes mission id, pass/fail status, last failed
   action, and copied artifact paths.
4. Preserve successful behavior.

Exit criteria:

- `Artifacts/PilotReports/pilot_summary.md` is actionable
- failed runs point to the failing action

### Session 3 - Artifact Consistency Guards

Goal:

- prevent stale or mixed artifacts from being accepted by runtime preview

Inspect:

- `Assets/Scripts/Runtime/MissionRuntimeLoader.cs`
- `Assets/Scripts/Runtime/MissionSceneMaterializer.cs`
- current EditMode tests

Do:

1. Add or extend EditMode tests for mission id mismatch.
2. Add or extend EditMode tests for `layoutRevisionId` mismatch.
3. Add tests for stale actor/objective `layoutRevisionId`.
4. Add tests for nested graph `layoutRevisionId` mismatch.
5. Confirm failures return structured materializer findings.

Exit criteria:

- stale artifact mixes cannot materialize
- mismatch failures are machine-readable

### Session 4 - Materializer Passability Hardening

Goal:

- ensure generated preview scenes are traversable through generated doors and
  breach points

Inspect:

- `Assets/Scripts/Runtime/MissionSceneMaterializer.cs`
- runtime JSON model files under `Assets/Scripts/Runtime/`
- materializer tests

Do:

1. Confirm door portals clear the correct collision tiles.
2. Confirm breach points clear the correct collision tiles.
3. Keep windows visual-only unless the contract says otherwise.
4. Validate invalid portal and breach geometry.
5. Add tests for both horizontal and vertical collision clearing.

Exit criteria:

- doors are passable in preview
- breach points are passable in preview
- invalid geometry fails with structured findings

### Session 5 - Pilot Mission Runtime Smoke Tests

Goal:

- protect the real pilot path for VS01, VS02, and VS03

Inspect:

- `Assets/Data/Mission/MissionConfig/`
- `Assets/Scripts/Runtime/MissionRuntimeLoader.cs`
- `Assets/Scripts/Runtime/MissionSceneBuilder.cs`
- `Assets/Scripts/Runtime/MissionSceneMaterializer.cs`
- existing EditMode tests

Do:

1. Add tests that `MissionConfig_VS01.asset`, `MissionConfig_VS02.asset`, and
   `MissionConfig_VS03.asset` exist.
2. Verify each config carries payload, layout, entities, verification, and
   manifest paths.
3. Verify each config has required catalogs assigned.
4. Test loader failures for missing or non-PASS manifests.
5. Add a smoke path using generated artifacts or dedicated fixtures.

Exit criteria:

- MissionConfig references are protected
- runtime-loader failures are tested

### Session 6 - Local Pilot Validation Script

Goal:

- make full local validation reproducible with one command

Create:

- `Scripts/run_pilot_validation.ps1`

Update:

- `Docs/pilot_runbook.md`
- `README.md`
- `Docs/index.md`

Do:

1. Create `Artifacts/Validation` and `Artifacts/PilotReports` if needed.
2. Run `PilotMissionPipelineCi.RunAll`.
3. Run Unity EditMode tests.
4. Run .NET server tests.
5. Return a non-zero exit code on any failure.
6. Keep cleanup safe and avoid deleting generated mission artifacts by default.

Exit criteria:

- one command launches the local pilot validation
- failures are visible and non-silent

### Session 7 - Full Validation and Evidence Capture

Goal:

- execute the complete pilot-readiness validation and record the result honestly

Do:

1. Run the full pilot pipeline for all three missions.
2. Run Unity EditMode tests.
3. Run .NET server tests.
4. Confirm `Artifacts/PilotReports/pilot_summary.md` exists.
5. Confirm copied verification summaries and manifests exist for all missions.
6. Confirm no `.generation.lock` remains after success.

Exit criteria:

- validation result is explicit: PASS, FAIL, or NOT RUN
- no unverified claim is made

## 5. Handoff Checklist

Before switching sessions, record:

- what was completed
- the exact files touched
- the next blocking step
- any failing test or compile message verbatim
- whether Unity was validated in batch mode or live mode

