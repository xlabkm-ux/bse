# Current State Audit v2.3

Date: 2026-04-25
Project: `Breach Scenario Engine`
Scope: repository, active docs, mission pipeline runtime, Unity package setup,
and VS01 mission artifacts.

This audit captures the factual repo state while v2.3 stabilization continues.
It does not change runtime behavior.

## Summary

The project has a working mission-pipeline vertical slice for `manage_mission`
and the `VS01_HostageApartment` mission. The pipeline can validate, compile,
generate layout graphs, place entities, verify, retry from layout on retryable
failures, and write a manifest after verification passes.

The active docs now point at the v2.3 contract set. Validation findings use the
v2.3 `TPL_*` family, the payload compile step validates against a repo-owned
JSON Schema file, and the remaining v2.3 gaps are concentrated in locks,
retry-policy refinement, verification metrics, and content-layer alignment.

## Repository Structure

Implemented project areas:

- `Assets/` exists with `Art`, `Data`, `Editor`, `Prefabs`, `Scenes`,
  `Scripts`, and `Settings`.
- `Assets/Scripts/Runtime/` contains `MissionConfig`,
  `MissionProfileAsset`, and basic runtime components.
- `Assets/Scripts/Generation/` contains generation namespace scaffolding and a
  hybrid authoring component.
- `Assets/Editor/CI/Vs01MissionPipelineCi.cs` runs the VS01 mission pipeline
  and writes a human-readable report.
- `UserMissionSources/missions/VS01_HostageApartment/` contains the authored
  template and ignored generated artifacts.
- `dotnet-prototype/` contains the MCP server, protocol project, contracts,
  tests, and the source copy of the Unity package.
- `Packages/com.breachscenarioengine.unity-mcp/` contains the embedded Unity
  bridge package used by the project.

Not present yet:

- `Assets/TacticalBreach/Profiles/`
- `Assets/TacticalBreach/Catalogs/`

## Active Documentation

Current active docs are v2.3:

- `Docs/breach_mcp_architecture_v2.3.md`
- `Docs/mission_authoring_contract_v2.3.md`
- `Docs/mission_pipeline_contract_v2.3.md`
- `Docs/mission_template_v2.3.md`
- `Docs/mission_data_contract_v2.3.md`
- `Docs/generation_manifest_contract_v2.3.md`

`Docs/project_documentation.md`, `Docs/index.md`, `Docs/README.md`, and
`Docs/workspace_index.md` point at the v2.3 documentation set.

## Runtime Tools

Implemented and documented MCP surface:

- status and preflight tools:
  `project_root.set`, `project.info`, `project.health_check`,
  `project.capabilities`, `editor.state`, `read_console`, `run_tests`,
  `get_test_job`
- content and bridge tools:
  `manage_asset`, `manage_hierarchy`, `manage_scene`, `manage_gameobject`,
  `manage_components`, `manage_script`, `manage_scriptableobject`,
  `manage_prefabs`, `manage_graph`, `manage_ui`, `manage_localization`,
  `manage_editor`, `manage_input`, `manage_camera`, `manage_graphics`,
  `manage_profiler`, `manage_build`
- mission pipeline:
  `manage_mission(action="validate_template")`,
  `compile_payload`, `generate_layout`, `place_entities`, `verify`,
  `write_manifest`

The server schema is in
`dotnet-prototype/contracts/breachscenarioengine-mcp-tools.schema.json`.
The server dispatcher and bridge route `manage_mission` through the embedded
Unity package.

## Mission Pipeline Implementation

Implemented:

- `validate_template` loads `mission_design.template.yaml` and returns the
  shared JSON envelope.
- Template validation checks required fields, unknown fields, simple type
  errors, ranges, tactical themes, navigation policies, placement policies,
  duplicate actor/objective ids, and known objective room tags.
- `compile_payload` writes `mission_payload.generated.json` and
  `mission_compile_report.json`, then validates the payload shape in code
  before writing.
- `generate_layout` writes deterministic `LayoutGraph`, `RoomGraph`,
  `PortalGraph`, `CoverGraph`, `VisibilityGraph`, and `HearingGraph` output
  and stamps `layoutRevisionId` into the payload when present.
- `place_entities` is blocked without a current layout and writes actors and
  objectives with ownership, `roomId`, `navNodeId`, and `layoutRevisionId`.
- `verify` writes `verification_summary.json` with machine-readable status,
  findings, and metrics.
- `write_manifest` reads verification status, retries retryable failures from
  layout generation, records retry seeds, stamps replay fields, and writes
  `generation_manifest.json`.
- `effectiveSeed` remains `0` unless verification passes.
- Generated mission JSON files are ignored by `.gitignore`.

Gaps against the v2.3 plan:

- There is no `mission_state.json` lifecycle file.
- There is no mission-scoped `.generation.lock` under the mission directory.
  Current locks live under `Temp/BseGenerationLocks/<missionId>/` plus
  `generation_manifest.json.lock`.
- There is no explicit stale lock detection or diagnostic cleanup command.
- Retry seed derivation currently uses
  `requestedSeed:missionId:retryIndex:pipelineVersion`; the v2.3 plan calls
  for a failure-code-aware policy.
- Verification metrics do not yet cover the full v2.3 target set such as
  alternate routes, hearing overlap percentage, chokepoint pressure, and
  objective room pressure.

Implemented since the audit snapshot:

- v2.3-specific validation codes such as `TPL_UNKNOWN_FIELD`,
  `TPL_RANGE_INVALID`, `TPL_PROFILE_REF_MISSING`, `TPL_OBJECTIVE_INVALID`, and
  `TPL_ACTOR_ROSTER_INVALID` are emitted by the template validator.
- Payload validation now uses a repo-owned JSON Schema file before payload
  write.
- Invalid template fixtures now exist under
  `UserMissionSources/missions/_test_invalid_*`.

## VS01 Mission State

`UserMissionSources/missions/VS01_HostageApartment/mission_design.template.yaml`
is the only mission under `UserMissionSources/missions/`.

Observed generated artifacts:

- `mission_compile_report.json`
- `mission_payload.generated.json`
- `mission_layout.generated.json`
- `mission_entities.generated.json`
- `verification_summary.json`
- `generation_manifest.json`

The current generated `verification_summary.json` reports `status: "PASS"` and
`layoutRevisionId: "layout_961a296a"`.

The current generated `generation_manifest.json` reports:

- `status: "PASS"`
- `requestedSeed: 428193`
- `effectiveSeed: 428193`
- `retrySeeds: []`
- `layoutRevisionId: "layout_961a296a"`

These generated files are ignored and are not tracked by git.

## Unity Package Alignment

Project version:

- Unity `6000.4.3f1`

Direct dependencies in `Packages/manifest.json`:

- `com.unity.entities` `6.4.0`
- `com.unity.inputsystem` `1.19.0`
- `com.unity.multiplayer.center` `1.0.1`
- `com.unity.render-pipelines.universal` `17.4.0`
- `com.unity.test-framework` `1.6.0`
- selected Unity modules, including Physics 2D, UIElements, and Vector Graphics

Resolved dependencies in `Packages/packages-lock.json` include Burst and
Collections transitively through Entities and URP. Addressables, Jobs, and
Unity AI Assistant remain out of the direct package promise for now.

URP 2D appears configured:

- `Assets/Settings/Rendering/Breach_2D_Renderer.asset`
- `Assets/Settings/Rendering/Breach_URP_RenderGraph.asset`
- `ProjectSettings/GraphicsSettings.asset` references a custom render pipeline
- `ProjectSettings/QualitySettings.asset` assigns the pipeline on the active
  quality level

Resolved alignment:

- The embedded package `package.json` now targets Unity `6000.4.3f1`, matching
  the project editor version.
- `Packages/manifest.json` and `Packages/packages-lock.json` now align on
  `com.unity.test-framework` `1.6.0`.
- `com.unity.addressables` was evaluated for direct inclusion, but the direct
  dependency was deferred because the imported package does not currently
  compile cleanly under this editor target.

## Profiles And Catalogs

Implemented:

- `Assets/Data/Mission/Profiles/` contains global profile assets for
  Tactical Theme, Performance, Render, Navigation Policy, Tactical Density, and
  Addressables Catalog.
- `Assets/Scripts/Runtime/MissionProfileAsset.cs` provides a generic
  ScriptableObject profile shell.
- `Assets/Data/Mission/Catalogs/` contains repo-owned catalog assets for
  enemies, environments, and objectives.
- `Assets/Scripts/Runtime/MissionCatalogAsset.cs` provides a catalog asset
  shell for the new content layer.
- `dotnet-prototype/unity/com.breachscenarioengine.unity-mcp/Editor/MissionPipelineEditorService.cs`
  now validates `profileRefs` and `catalogRefs` against the repo-owned content
  assets, including schema versions, content type fields, and Addressables
  labels for the Addressables catalog profile.
- `Assets/Data/Mission/MissionConfig/MissionConfig_VS01.asset` points to the
  VS01 template and generated artifact paths.

Not implemented:

- typed profile classes such as `TacticalThemeProfile`,
  `EnemyProfile`, `ObjectiveProfile`, and `EnvironmentBiomeProfile`
- typed catalog subclasses such as `EnemyCatalog`,
  `EnvironmentCatalog`, and `ObjectiveCatalog`
- type-safe editor accessors for the catalog and profile assets

Path decision needed:

- v2.2 docs and current code use `Assets/Data/Mission/Profiles/`
- the v2.3 continuation plan proposes `Assets/TacticalBreach/Profiles/` and
  `Assets/TacticalBreach/Catalogs/`

Pick one authoritative profile/catalog root before implementing v2.3 content
validation.

## Must Fix

- Create repo-owned v2.3 docs and update the active docs index before relying
  on v2.3 behavior.
- Implement or revise the v2.3 generation lock and lifecycle contract:
  `.generation.lock`, `mission_state.json`, stale lock detection, and cleanup.
- Align package declarations with the target architecture: Addressables, Burst,
  Collections, Jobs, and AI Assistant should be either added, justified as
  transitive/unneeded, or removed from the v2.3 promise.
- Choose the authoritative profile/catalog root and update docs, payload refs,
  and validation around that choice.
- Split template validation findings into the v2.3 code family and add invalid
  template fixtures.

## Should Fix

- Replace or formally document the current custom YAML subset parser.
- Add repo-owned JSON Schema validation for generated payloads.
- Expand `verification_summary.json` metrics to the v2.3 target shape.
- Include failure code in retry seed derivation if v2.3 keeps that policy.
- Update embedded package metadata to match the current Unity target.
- Resolve the manifest/package-lock test framework version drift.

## Nice To Have

- Add Addressables labels and deterministic asset catalogs.
- Add a short migration note from v2.2 to v2.3 after the new contract set lands.
- Keep a single generated artifact glossary shared by pipeline, data, and
  manifest docs.
