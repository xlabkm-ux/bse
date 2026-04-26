# Pilot Operation Checklist

Status: Active pilot checklist
Version: 1.0
Project: `Breach Scenario Engine`

This checklist accepts the project for pilot operation across three missions:
`VS01_HostageApartment`, `VS02_DataRaidOffice`, and
`VS03_StealthSafehouse`.

## Mission Authoring

- [ ] Each pilot mission has `mission_design.template.yaml`.
- [ ] `schemaVersion` is `tb.mission_template.v2.3`.
- [ ] `missionId` matches the mission folder name.
- [ ] `generationMeta.initialSeed` is authored.
- [ ] `generationMeta.effectiveSeed` remains `0` or absent in YAML.
- [ ] `pixelsPerUnit` is `128`.
- [ ] `actorRoster` is not empty.
- [ ] `objectives.primary` is not empty.
- [ ] Post-layout placement policies are used only through the pipeline.

## Pipeline Gate

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' -batchmode -projectPath 'E:\Games\Breach\BreachScenarioEngine' -executeMethod BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll -quit
```

Accept only if every mission returns PASS for:

- [ ] `validate_template`
- [ ] `compile_payload`
- [ ] `generate_layout`
- [ ] `place_entities`
- [ ] `verify`
- [ ] `write_manifest`

## Generated Artifacts

For each pilot mission:

- [ ] `mission_payload.generated.json` exists.
- [ ] `mission_compile_report.json` exists.
- [ ] `mission_layout.generated.json` exists.
- [ ] `mission_entities.generated.json` exists.
- [ ] `verification_summary.json` has `status: "PASS"`.
- [ ] `generation_manifest.json` has `status: "PASS"`.
- [ ] `generation_manifest.json` has `effectiveSeed > 0`.
- [ ] `.generation.lock` is absent after completion.

## Runtime Gate

- [ ] `MissionConfig_VS01.asset` exists.
- [ ] `MissionConfig_VS02.asset` exists.
- [ ] `MissionConfig_VS03.asset` exists.
- [ ] `Assets/Scenes/MissionBootstrap.unity` opens without compile errors.
- [ ] `MissionRuntimeLoader` loads each selected manifest.
- [ ] Each mission creates `GeneratedMissionRoot_<missionId>`.
- [ ] Actors and objectives have `GeneratedOwnershipMarker`.
- [ ] At least one `MissionCompleteTrigger` exists.
- [ ] The debug overlay shows mission id, effective seed, layout revision, and verification status.

## Report Artifacts

After CI/batchmode run:

- [ ] `Artifacts/PilotReports/VS01_verification_summary.json`
- [ ] `Artifacts/PilotReports/VS01_generation_manifest.json`
- [ ] `Artifacts/PilotReports/VS02_verification_summary.json`
- [ ] `Artifacts/PilotReports/VS02_generation_manifest.json`
- [ ] `Artifacts/PilotReports/VS03_verification_summary.json`
- [ ] `Artifacts/PilotReports/VS03_generation_manifest.json`
- [ ] `Artifacts/PilotReports/pilot_summary.md`

## Operating Rules

- Do not manually edit generated artifacts.
- Do not hand-author `effectiveSeed`.
- Do not run placement before layout generation.
- Do not write a manifest unless verification is PASS.
- Treat JSON tool results and generated JSON files as the machine-readable source of truth.

## Operator References

- [Docs/pilot_testing_preparation_plan.md](pilot_testing_preparation_plan.md)
- [Docs/pilot_runbook.md](pilot_runbook.md)
- [Docs/pilot_result_report_template.md](pilot_result_report_template.md)
- `Scripts/run_pilot_validation.ps1`
