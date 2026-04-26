# Development Plan by Sessions

Date: 2026-04-24
Project: `Breach Scenario Engine`

This document splits the mission pipeline work into handoff-friendly sessions.
It is intended for switching between chats or resuming later without rebuilding
the whole roadmap.

## Active Continuation: BREACH Technology Transfer

The v2.3 stabilization track is complete enough to serve as the target
architecture. New chats should continue the BREACH technology transfer track
unless the user asks for a different task.

### Transfer Session 0: Documentation Baseline

Status:

- completed

Goal:

- define how proven technologies from `E:/Games/Breach/BREACH` move into BSE
  without copying the legacy editor-monolith architecture

Completed:

- created `Docs/migration_from_breach_scene_builders.md`
- created `Docs/breach_technology_transfer_plan_v2.3.md`
- created `Docs/scene_materialization_contract_v2.3.md`
- created `Docs/breach_technology_transfer_acceptance_v2.3.md`
- linked the transfer docs from `README.md`, `Docs/README.md`,
  `Docs/index.md`, and `Docs/project_documentation.md`
- documented target platforms:
  - Primary: Windows 10 / 11 desktop, 1920x1080+ resolution, 16:9 or wider
  - Secondary: Android mobile, 1080p portrait and landscape, touch controls,
    and optional controller support
  - scalable UI across supported desktop and mobile resolutions

Handoff note:

- "The BREACH transfer docs are in place. Start with a source traceability
  audit, then implement BSP as a pure Step 6 layout generator without Tilemap,
  prefab, Resources, GameObject.Find, or NavMesh dependencies."

### Transfer Session 1: Source Traceability Audit

Status:

- completed

Goal:

- freeze `BREACH` as a read-only reference and map each useful source behavior
  to a BSE target module before implementation

Work:

- inspect and summarize:
  - `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1LevelGenerator.cs`
  - `E:/Games/Breach/BREACH/Assets/Scripts/Mission/ApartmentLayoutBuilder.cs`
  - `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1SceneSetup.cs`
  - `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1Verification.cs`
  - `E:/Games/Breach/BREACH/Assets/Scripts/Editor/MapTileAssetBootstrap.cs`
  - `E:/Games/Breach/BREACH/Assets/Scripts/Editor/FullProjectBake.cs`
  - `E:/Games/Breach/BREACH/MISSION_STANDARDS.md`
- create or update a traceability section that records:
  - source behavior
  - BSE target file/module
  - copied idea
  - intentionally discarded legacy behavior
  - verification method
- identify existing BSE implementation points in:
  - `dotnet-prototype/unity/com.breachscenarioengine.unity-mcp/Editor/MissionPipelineEditorService.cs`
  - `Assets/Scripts/Generation/`
  - `Assets/Scripts/Runtime/MissionRuntimeJsonModels.cs`
  - `Assets/Scripts/Runtime/MissionSceneBuilder.cs`

Exit criteria:

- every planned transfer feature has source and target traceability
- no BREACH code has been copied into BSE yet
- first implementation slice is clearly scoped to pure Step 6 layout data

Completed:

- inspected the required BREACH reference files as read-only sources
- inspected the current BSE implementation points in the Unity MCP service,
  generation folders, runtime JSON DTOs, and runtime scene builder
- created `Docs/breach_technology_transfer_traceability_v2.3.md`
- linked the traceability audit from the transfer plan, acceptance checklist,
  README, docs index, and project documentation
- scoped the next implementation slice to a pure Step 6 BSP layout generator
  that keeps Tilemap, prefab, `Resources`, `GameObject.Find`, and NavMesh work
  out of generation

Handoff note:

- "Source traceability is complete. Implement Transfer Session 2 by replacing
  the temporary fixed four-room `BuildLayoutNode` path with a pure BSP layout
  generator module that emits deterministic room, portal, window, breach, and
  cover-candidate data while preserving existing placement and verification
  gates."

### Transfer Session 2: Pure BSP Layout Generator

Status:

- completed

Goal:

- extract BSP room/portal/window/breach-point heuristics into BSE as a pure
  Step 6 data generator

Work:

- add BSE layout generator types under `Assets/Scripts/Generation/Layout/`
  or the currently active Unity MCP editor service layer, following existing
  project patterns
- consume `mission_payload.generated.json`, seed, BSP constraints, profiles,
  and catalogs
- write deterministic room, portal, window, breach point, and initial cover
  candidate data into `mission_layout.generated.json`
- preserve stable `layoutRevisionId` for identical inputs
- keep Step 6 free of Tilemap, PrefabUtility, GameObject.Find, Resources, and
  NavMesh calls

Exit criteria:

- `manage_mission(action="generate_layout")` produces deterministic BSP-based
  layout data
- repeated generation with the same payload and seed is stable
- Step 5 remains blocked without a current layout

Completed:

- added a pure `BspLayoutGenerator` in the active Unity MCP editor package
  under `Packages/com.breachscenarioengine.unity-mcp/Editor/`
- kept the source-copy package under
  `dotnet-prototype/unity/com.breachscenarioengine.unity-mcp/Editor/` in sync
- replaced the temporary fixed four-room `BuildLayoutNode` path with the BSP
  generator call while leaving `manage_mission(action="generate_layout")` as
  the public entry point
- generation now emits BSP rooms, deterministic internal portals, perimeter
  windows, breach points, cover candidates, visibility edges, and hearing edges
  as JSON-only Step 6 data
- `layoutRevisionId` now includes the generator id, payload seed, BSP
  constraints, and primary objective shape so identical inputs remain stable
  and generator changes invalidate stale layouts
- `generate_layout` reads `mission_payload.generated.json` when present and
  uses `header.initialSeed` as the Step 6 generation seed
- expanded editor tests to assert BSP generator identity, more than four rooms,
  windows, breach points, non-empty portals, and stable repeated generation
- confirmed the new BSP generator source contains no `Tilemap`,
  `PrefabUtility`, `GameObject.Find`, `Resources`, `AssetDatabase`, or NavMesh
  calls

Verification:

- Unity batchmode EditMode test execution was attempted with
  `Unity.exe -batchmode -runTests`, but Unity returned:
  `It looks like another Unity instance is running with this project open.`
- after `manage_asset(action="refresh")`, live MCP
  `manage_mission(action="generate_layout")` produced
  `pure_bsp_layout_v1` output for `VS01_HostageApartment`
- repeated live generation for VS01 kept `layoutRevisionId: layout_9f491a30`
  and SHA-256
  `B790E6A6AAFB1D307080755B200ACBD9900B1179ECA626112AEEEC6DA1BA3FAC`
- live `place_entities` and `verify` passed for VS01 after capping initial
  hearing edges to the current Step 7 budget

Handoff note:

- "Pure BSP layout code is in the active Unity MCP package and live VS01
  `generate_layout` / `place_entities` / `verify` pass after AssetDatabase
  refresh. Batchmode EditMode tests are still blocked while another Unity
  instance has the project open. Continue Transfer Session 3 by moving cover,
  visibility, hearing, and pressure heuristics out of the layout generator into
  dedicated tactical graph builders."

### Transfer Session 3: Tactical Graphs and Cover Heuristics

Status:

- completed

Goal:

- turn BREACH cover/window/door heuristics into BSE tactical graph data

Work:

- add or expand cover, visibility, and hearing graph builders
- keep cover away from doors, windows, breach points, and objective access
- emit verification-friendly metrics for cover density, alternate routes,
  hearing overlap, chokepoint pressure, and objective room pressure

Completed:

- kept tactical graph generation isolated in `TacticalGraphBuilder`
- removed obsolete cover, visibility, and hearing helper implementations from
  `MissionPipelineEditorService` in both the embedded package and the source
  copy
- preserved deterministic tactical metrics in verification summaries for
  cover density, alternate routes, hearing overlap, chokepoint pressure, and
  objective room pressure

Exit criteria:

- tactical graph output remains deterministic
- verification summary includes the expanded metric families

Handoff note:

- "Tactical graphs and verification metrics are now isolated in the dedicated
  builder layer. Continue Transfer Session 4 by binding actor, hostage,
  objective, and enemy placement to the active `layoutRevisionId`."

### Transfer Session 4: Layout-Bound Placement

Status:

- completed

Goal:

- keep actors, hostages, objectives, and enemies in Step 5 and bind all
  placements to the active `layoutRevisionId`

Work:

- adapt placement to the new BSP room and portal data
- prevent duplicate enemy placement from the legacy BREACH pattern
- ensure entities include stable ownership metadata, `roomId`, `navNodeId`,
  and `layoutRevisionId`

Completed:

- replaced the simple round-robin room choice with layout-aware placement
  candidates keyed off `layoutRevisionId`
- kept `EntryPointOnly` and `SecureRoomOnly` anchored to their layout-derived
  rooms
- spread `PostLayout_TaggedRoom` actors across multiple layout rooms instead of
  stacking every enemy into one room
- added a regression test that verifies tagged enemy placements span more than
  one room on the same generated layout

Verification:

- Unity batchmode recompiled the updated `BreachScenarioEngine.Mcp.Editor` and
  `BreachScenarioEngine.Mcp.Editor.Tests` assemblies successfully
- the batchmode session did not emit a test XML report before it quit, so the
  verification here is compile-level rather than a finished EditMode result

Exit criteria:

- placement fails on missing or stale layout
- placement never runs inside Step 6
- generated entity data is stable and verification-ready

### Transfer Session 5: Scene Materializer

Status:

- completed

Goal:

- build Unity preview/playable scenes from generated artifacts as output, not
  source of truth

Work:

- implement `MissionSceneContext`
- implement materialization from `mission_layout.generated.json` and
  `mission_entities.generated.json`
- materialize Tilemaps, doors, windows, covers, enemies, objectives,
  extraction zones, lighting, and debug roots
- cleanup only generated-owned objects
- preserve Windows 10/11 desktop and Android 1080p portrait/landscape
  assumptions with scalable UI

Completed:

- added `MissionSceneContext` with typed references for the grid, tilemaps,
  generated roots, and debug roots
- added `MissionSceneMaterializer` and refactored `MissionSceneBuilder` to
  materialize generated layout and entity artifacts into a scene preview root
- materialized Tilemap-backed world layers, generated doors/windows/covers,
  actors, objectives, extraction preview, and ownership markers without using
  `GameObject.Find`
- updated the runtime smoke test to validate the context hierarchy and
  generated tilemap roots
- enabled the Unity Tilemap module in `Packages/manifest.json` so the runtime
  preview layer compiles under the active editor target

Verification:

- Unity batchmode script compilation completed successfully against
  `MissionSceneContext` and `MissionSceneMaterializer`
- the direct `-runTests` invocation did not emit a new test XML report, so the
  verification here is compile-level rather than a finished EditMode result

Exit criteria:

- VS01 can be materialized from generated artifacts
- scene cleanup does not affect user-owned objects
- materialized scene follows `Docs/scene_materialization_contract_v2.3.md`

### Transfer Session 6: Catalogs, Verification, and CI Preview

Status:

- completed

Goal:

- finish the transfer by moving content lookup to catalogs, expanding JSON
  verification, and replacing full bake with manage_mission-driven CI preview

Work:

- move tile/prefab lookup to Mission Catalogs rather than `Resources`
- port useful BREACH verification checks into JSON findings and metrics
- add CI runner that executes the canonical mission pipeline and optional
  scene preview materialization

Completed:

- extended `mission_payload.generated.json` and `generation_manifest.json`
  with repo-relative `catalogRefs`
- validated catalog references alongside profile references before Step 6 and
  again during verification/retry flows
- bound `MissionConfig` assets to the repo-owned catalog assets and taught the
  runtime scene materializer to require the catalog assets during preview
- added optional scene preview materialization to
  `Assets/Editor/CI/PilotMissionPipelineCi.cs` behind the
  `BSE_CI_MATERIALIZE_SCENE_PREVIEW` environment flag
- updated the editmode smoke tests to assert catalog references and mission
  catalog bindings

Verification:

- Unity editmode test run completed successfully with `21/21` tests passing
- results were saved to
  `C:\Users\MY\AppData\LocalLow\DefaultCompany\BreachScenarioEngine\TestResults.xml`

Exit criteria:

- missing content is reported as JSON findings
- `verification_summary.json` is the decision source
- CI never writes an accepted manifest unless Step 7 PASS is proven

Handoff note:

- "Catalog references now flow through payload, verification, manifest, and
  runtime preview. The transfer track is complete for the current v2.3
  content-layer session."

### Transfer Session 7: Tactical Graph Runtime Transfer

Status:

- completed

Goal:

- move tactical graph builders out of the editor package and into repo-owned
  runtime generation files while keeping verification metrics stable

Work:

- add dedicated tactical graph builders under
  `Assets/Scripts/Generation/TacticalGraphs/`
- keep the editor package as a consumer of the runtime tactical graph layer
- preserve deterministic cover, visibility, hearing, and verification metric
  output

Completed:

- added repo-owned runtime builders for cover, visibility, and hearing graphs
- moved the runtime tactical graph namespace to
  `BreachScenarioEngine.Generation.TacticalGraphs`
- added a runtime `BreachScenarioEngine.Generation.TacticalGraphs` asmdef so
  the new layer compiles independently
- kept `TacticalGraphBuilder` in the editor package as the compatibility facade
  for verification metrics and legacy call sites
- updated the editor-side layout generator and verification service to import
  the runtime tactical graph namespace explicitly
- restored the package-side layout generator to the stable compile path while
  the runtime boundary is being wired

Verification:

- Unity batchmode compiles the new runtime tactical graph assembly and the
  package assemblies successfully after the `IsExternalInit` compatibility stub
  was added
- the active tactical graph tests still exercise deterministic graph and
  metric output through the compatibility facade
- Unity batchmode compile also succeeded after the runtime namespace was
  separated from the editor namespace and the editor-side generators imported
  `BreachScenarioEngine.Generation.TacticalGraphs`

Exit criteria:

- tactical graph generation code lives in repo-owned runtime files and is
  consumed by the package assembly
- deterministic graph and metric output remains stable for the same inputs
- the editor package no longer owns the tactical graph implementation copy

Handoff note:

- "Tactical graph generation now lives in repo-owned runtime files, the editor
  package consumes the runtime namespace directly, and the compile path is
  clean. Continue by expanding or hardening the verification and
  content-facing runtime modules."

## Active Continuation: v2.3 Stabilization

The v2.2 mission pipeline sessions below are functionally complete. New chats
should continue the v2.3 stabilization track unless the user asks for a
different task.

### Pilot Operation Preparation: 2-3 Mission Slice

Status:

- completed

Goal:

- prepare the repository for pilot operation across VS01, VS02, and VS03

Completed:

- added pilot mission templates for `VS02_DataRaidOffice` and
  `VS03_StealthSafehouse`
- kept `VS01_HostageApartment` as the regression baseline with seed `428193`
- added `MissionConfig` assets for VS01, VS02, and VS03
- added runtime loading and debug scene construction from accepted manifests
- added pilot debug placeholders under `Assets/Prefabs/Pilot/`
- added `Assets/Scenes/MissionBootstrap.unity`
- added `PilotMissionPipelineCi.RunAll` and smoke tests
- added `.github/workflows/pilot-mission-ci.yml`
- added `Docs/pilot_operation_checklist.md`
- expanded layout cover generation so pilot tactical density gates can satisfy
  higher enemy counts when the template requests them
- generated accepted v2.3 artifacts for all three pilot missions through the
  pipeline

Verification:

- Unity batchmode `BreachScenarioEngine.Editor.CI.PilotMissionPipelineCi.RunAll`
  passed for VS01, VS02, and VS03
- Unity EditMode smoke tests passed for `Assembly-CSharp-Editor`

Handoff note:

- "Pilot operation preparation is in place. Next work can focus on richer
  playable interactions, real art-prefab mapping, and CI runner licensing."

### v2.3 Session 1: Current-State Audit

Status:

- completed

Goal:

- record the factual repository state before changing v2.3 contracts or code

Completed:

- created `Docs/audit_current_state_v2.3.md`
- linked the audit from the active docs index and project documentation
- identified Must Fix / Should Fix / Nice To Have work for v2.3

Handoff note:

- "The v2.3 audit baseline is recorded. Please start by stabilizing the repo
  owned v2.3 docs before changing runtime behavior."

### v2.3 Session 2: Documentation Stabilization

Status:

- completed

Goal:

- create one repo-owned v2.3 documentation set and stop mixing the external
  continuation plan with active v2.2 contracts

Work:

- create `Docs/breach_mcp_architecture_v2.3.md`
- create `Docs/mission_pipeline_contract_v2.3.md`
- create `Docs/mission_authoring_contract_v2.3.md`
- create `Docs/mission_template_v2.3.md`
- create `Docs/mission_data_contract_v2.3.md`
- create `Docs/generation_manifest_contract_v2.3.md`
- update `Docs/index.md`, `Docs/project_documentation.md`,
  `Docs/runtime_tools.md`, `Docs/canonical_tools.md`, and this plan
- decide the authoritative profile/catalog root before v2.3 validation work

Completed:

- created the repo-owned v2.3 documentation set
- updated `README.md`, `Docs/README.md`, `Docs/index.md`,
  `Docs/project_documentation.md`, `Docs/workspace_index.md`,
  `Docs/runtime_tools.md`, `Docs/canonical_tools.md`, and this plan
- made the active reading path point at v2.3 contracts
- documented v2.3 invariants: Step 6 before Step 5, retry back to Step 6,
  manifest only after PASS, JSON-only machine decisions, and `effectiveSeed`
  only after PASS
- chose `Assets/Data/Mission/Profiles/` as the authoritative profile root and
  `Assets/Data/Mission/Catalogs/` as the authoritative catalog root

Exit criteria:

- active docs have a single v2.3 reading path
- v2.3 invariants are explicitly documented:
  Step 6 before Step 5, retry to Step 6, manifest after PASS, JSON-only
  machine decisions, and `effectiveSeed` only after PASS

Handoff note:

- "The v2.3 docs are now repo-owned and active. Please implement the
  mission-scoped `.generation.lock` and `mission_state.json` lifecycle next,
  keeping `write_manifest` blocked unless verification is PASS."

### v2.3 Session 3: Generation Locks and Lifecycle

Status:

- completed

Goal:

- align implementation with the v2.3 lock and mission lifecycle contract

Work:

- add mission-scoped `.generation.lock`
- add `mission_state.json`
- define lifecycle states and current-step updates
- add safe unlock behavior, stale lock handling, and diagnostic cleanup
- keep `write_manifest` blocked when lifecycle state is incompatible

Completed:

- added mission-scoped `.generation.lock` under each mission directory
- added `mission_state.json` lifecycle writes for compile, layout, placement,
  verification, retry, manifest, and cleanup transitions
- added explicit stale-lock diagnostic cleanup through
  `manage_mission(action="cleanup_generation_lock")`
- changed failed/blocked manifest attempts to update `mission_state.json`
  without writing a failed `generation_manifest.json`
- kept `write_manifest` gated on PASS verification and compatible PASS mission
  state
- updated retry seed derivation to include the verification failure code

Exit criteria:

- generated mission writes are mission-lock guarded
- lock conflicts return `GENERATION_LOCK_CONFLICT`
- stale lock cleanup is explicit and diagnostic
- `write_manifest` writes accepted manifests only after compatible PASS state

Handoff note:

- "Generation locks and mission lifecycle are now v2.3-aligned. Continue with
  validation and payload fidelity: split template findings into the v2.3
  `TPL_*` family, add invalid fixtures, document or replace the YAML subset
  parser, and add repo-owned JSON Schema validation."

### v2.3 Session 4: Validation and Payload Fidelity

Status:

- completed

Goal:

- harden template validation and payload schema fidelity

Work:

- split validation findings into the v2.3 `TPL_*` code family
- add invalid template fixtures
- document or replace the current custom YAML subset parser
- add repo-owned JSON Schema validation before payload write

Completed:

- split template validation findings into `TPL_UNKNOWN_FIELD`,
  `TPL_RANGE_INVALID`, `TPL_PROFILE_REF_MISSING`, `TPL_OBJECTIVE_INVALID`,
  and `TPL_ACTOR_ROSTER_INVALID`
- added invalid template fixtures under `UserMissionSources/missions/_test_invalid_*`
- documented the current line-oriented YAML subset parser in the template contract
- added repo-owned JSON Schema validation for `mission_payload.generated.json`

### v2.3 Session 5: Verification and Retry Hardening

Status:

- completed

Goal:

- expand verification metrics and make retry derivation match the v2.3 policy

Work:

- add v2.3 verification metrics and retryable/blocking classification
- include failure code in retry seed derivation if retained by contract
- preserve Step 6 -> Step 5 -> Step 7 on every retry

Completed:

- added expanded verification metrics for reachability, alternate routes,
  hearing overlap, chokepoint pressure, and objective room pressure
- added `retryClass`, `failureCode`, and retryability counts to
  `verification_summary.json`
- kept retry derivation tied to the current failure code while preserving
  Step 6 -> Step 5 -> Step 7 on every retry

### v2.3 Session 6: Unity Package and Content Layer

Status:

- completed

Goal:

- align Unity dependencies, Addressables, profiles, and catalogs with the
  v2.3 target architecture

Work:

- resolve direct package dependency decisions for Addressables, Burst,
  Collections, Jobs, and AI Assistant
- align embedded package metadata with Unity `6000.4.3f1`
- add typed profiles and catalogs after the docs choose an authoritative path
- validate Addressables labels and profile versions

Completed:

- aligned the embedded Unity package metadata with Unity `6000.4.3f1`
- aligned `com.unity.test-framework` with the resolved `1.6.0` lockfile
- normalized the canonical mission profile schema versions to v2.3
- added repo-owned catalog assets under `Assets/Data/Mission/Catalogs/`
- captured Addressables labels on the mission profile asset
- made `compile_payload` and `verify` emit and validate `catalogRefs` against the
  repo-owned catalog assets
- added a dedicated Unity editmode bootstrap runner and captured an XML test
  report from the editor test suite

Exit criteria:

- package metadata matches the Unity 6 target
- the chosen profile/catalog root exists in repo-owned assets
- profile versions and Addressables labels are represented in content assets
- the session 6 continuation path is no longer blocked on root selection

## Current State

Completed:

- `manage_mission` is exposed by the server
- `manage_mission` is routed through the Unity bridge
- `validate_template` works for `VS01_HostageApartment`
- `compile_payload` works for `VS01_HostageApartment`
- `mission_payload.generated.json` and `mission_compile_report.json` are generated
- `generate_layout` writes deterministic layout/tactical graph output
- `layoutRevisionId` is stable for identical layout inputs
- `place_entities` remains blocked by `ORDER_VIOLATION_NO_LAYOUT_GRAPH` until layout exists
- `place_entities` writes deterministic actor/objective placement after current layout validation
- generated placement entities carry stable ownership metadata, `roomId`, `navNodeId`, and `layoutRevisionId`
- `verify` writes `verification_summary.json` with structured findings and metrics
- Unity editor compilation was verified against the embedded package
- retry execution returns retryable verification failures to Step 6 before
  writing the manifest
- mission-scoped `.generation.lock` guards generated mission writes
- `mission_state.json` records lifecycle state and blocks incompatible manifest
  writes

Pending:

- richer template validation and schema fidelity

## Session 1: Contract Stabilization

Goal:

- keep the mission pipeline contract consistent between docs, server, bridge,
  and tests

Work:

- review `Docs/mission_pipeline_contract_v2.2.md`
- keep `Docs/runtime_tools.md` and `Docs/canonical_tools.md` in sync
- tighten the `manage_mission` result shape
- align template validation errors to contract codes

Exit criteria:

- no doc drift around `manage_mission`
- `validate_template` and `compile_payload` stay stable

Recommended handoff note:

- "The first slice is working. Please keep contract and doc updates in sync
  while expanding validation fidelity."

## Session 2: Template and Payload Fidelity

Goal:

- make `validate_template` and `compile_payload` closer to the v2.2 contracts

Work:

- improve YAML parsing robustness
- validate all required authoring fields
- normalize actor counts and objective references
- make payload generation match the JSON schema more strictly
- add focused tests for invalid templates and boundary cases

Exit criteria:

- invalid templates fail with structured findings
- payload shape matches `Docs/mission_data_contract_v2.2.md`

Recommended handoff note:

- "The compiler works for VS01. Please harden parsing and schema checks before
  touching layout."

## Session 3: Layout Generation

Goal:

- implement `manage_mission(action="generate_layout")`

Work:

- create layout generation service
- produce `LayoutGraph`, `RoomGraph`, `PortalGraph`, `CoverGraph`,
  `VisibilityGraph`, and `HearingGraph`
- compute `layoutRevisionId`
- ensure retries return to Step 6, not Step 5
- keep entity placement blocked until layout exists

Exit criteria:

- a deterministic layout exists for `VS01_HostageApartment`
- `layoutRevisionId` is stable for identical inputs

Recommended handoff note:

- "Please build the layout layer next. Placement must remain layout-gated."

## Session 4: Entity Placement

Goal:

- implement `manage_mission(action="place_entities")`

Status:

- completed

Work:

- place actors and objectives only after layout
- enforce `Step 6 -> Step 5` ordering
- attach generated ownership markers
- ensure entities carry `roomId`, `navNodeId`, and `layoutRevisionId`

Exit criteria:

- placement fails cleanly without a current layout
- generated objects have stable ownership metadata

Recommended handoff note:

- "Layout now exists. Please wire post-layout placement and keep the ordering
  invariant intact."

## Session 5: Verification

Goal:

- implement `manage_mission(action="verify")`

Status:

- completed

Work:

- check navigation reachability
- check tactical density and performance budgets
- validate profile references
- validate scene and prefab references where relevant
- produce `verification_summary.json`

Exit criteria:

- verification emits structured findings and metrics
- pass/fail is machine readable

Recommended handoff note:

- "Placement is ready. Please add verification and keep the output JSON-only."

## Session 6: Manifest and Replay

Goal:

- implement `manage_mission(action="write_manifest")`

Status:

- completed

Work:

- write `generation_manifest.json`
- own `effectiveSeed`, `retrySeeds`, `layoutRevisionId`, and artifact paths
- set `effectiveSeed` only after verification passes
- lock mission writes to avoid concurrent corruption

Exit criteria:

- manifest is written only after PASS
- replay data is deterministic and complete

Recommended handoff note:

- "Verification is done. Please finish manifest ownership and replay handling."

## Session 7: Tests and Cleanup

Goal:

- protect the pipeline with tests and remove rough edges

Status:

- completed

Work:

- add coverage for success and failure paths
- test command routing through the server and bridge
- test artifact creation paths
- update backlog and runtime docs
- clean up warnings only when they affect the mission pipeline work

Exit criteria:

- pipeline regressions are covered by targeted tests
- docs reflect current implementation status

Recommended handoff note:

- "The pipeline is functionally complete enough for end-to-end checks. Please
  shore up tests and docs."

## Session 8: Retry Execution

Goal:

- execute deterministic retries after Step 7 returns a retryable failure

Status:

- completed

Work:

- keep retry orchestration inside the existing public `manage_mission`
  surface
- derive retry seeds with the v2.2 hash policy
- rerun Step 6 -> Step 5 -> Step 7 for retryable verification failures
- write `generation_manifest.json` only after retry verification returns PASS
- record attempted retry seeds and accepted `effectiveSeed`

Exit criteria:

- retryable verification failures return to layout generation, not placement
- non-retryable failures still block manifest writing
- retry seeds are recorded in `generation_manifest.json`

Recommended handoff note:

- "Retry execution is wired through write_manifest. Please keep future
  generator changes honoring Step 6 -> Step 5 -> Step 7 on every retry."

## Suggested Chat Split

Use separate chats for these clusters:

1. Contract and validation work
2. Layout generation
3. Placement and ownership markers
4. Verification and metrics
5. Manifest, locks, and replay
6. Tests and documentation cleanup

## Handoff Checklist

Before switching chats:

- note what was completed
- note the exact file paths touched
- note the next blocking step
- note any failing test or compile message verbatim
- note whether Unity was last validated in batch mode or live mode

## Anchor Files

- [Docs/audit_current_state_v2.3.md](audit_current_state_v2.3.md)
- [Docs/breach_mcp_architecture_v2.3.md](breach_mcp_architecture_v2.3.md)
- [Docs/mission_pipeline_contract_v2.3.md](mission_pipeline_contract_v2.3.md)
- [Docs/mission_authoring_contract_v2.3.md](mission_authoring_contract_v2.3.md)
- [Docs/mission_template_v2.3.md](mission_template_v2.3.md)
- [Docs/mission_data_contract_v2.3.md](mission_data_contract_v2.3.md)
- [Docs/generation_manifest_contract_v2.3.md](generation_manifest_contract_v2.3.md)
- [Docs/runtime_tools.md](runtime_tools.md)

## Proposed Next Track: Post-Transfer Hardening

This track is the next execution plan after the BREACH technology transfer
finishes or when the user explicitly asks to switch focus. It keeps the
v2.3 invariants intact while making the generated missions more playable,
more honest in preview, and easier to validate.

### Session 1: Materializer Passability

Status:

- planned

Goal:

- make generated scenes traversable by clearing collision for doors and breach
  points while keeping windows on an explicit policy

Work:

- inspect `Assets/Scripts/Runtime/MissionSceneMaterializer.cs`
- clear `World_Collision` tiles for door portals and breach points using
  deterministic tile rounding rules
- keep normal windows visual-only unless the contract explicitly marks them as
  breachable
- add clear findings for invalid portal position or orientation data

Exit criteria:

- doors and breach points open the matching collision tiles
- window behavior is documented and stable
- materialization still returns JSON-compatible PASS/FAIL

Recommended handoff note:

- "Please fix collision clearing in the materializer first. Preview scenes
  need to be traversable before we harden the rest of the pipeline."

### Session 2: Materializer Consistency Guards

Status:

- planned

Goal:

- stop stale layout and entity artifacts from producing mixed preview scenes

Work:

- compare `layoutRevisionId` across layout, entities, and manifest artifacts
- compare mission ids across config, layout, entities, and manifest where
  applicable
- fail fast with machine-readable findings when artifacts do not match
- add EditMode tests for stale layout and mission-id mismatches

Exit criteria:

- stale or mismatched artifacts cannot materialize a preview scene
- failures are returned as structured JSON findings

Recommended handoff note:

- "The next slice should guard the materializer against stale artifact mixes.
  Please keep the failure path fully machine-readable."

### Session 3: Materializer Tests

Status:

- planned

Goal:

- cover the materializer with focused EditMode tests before broader refactors

Work:

- add tests for context hierarchy creation
- add tests for door collision clearing
- add tests for breach-point collision clearing
- add tests for failed manifest handling
- add tests for missing catalog handling

Exit criteria:

- the materializer behavior is protected by targeted tests
- collision and validation regressions are caught in EditMode

Recommended handoff note:

- "Please add targeted EditMode coverage for the materializer. The goal is to
  lock down passability, failure handling, and catalog validation."

### Session 4: Runtime BSP Boundary

Status:

- completed

Goal:

- move the pure BSP layout generator out of the editor package and into
  repo-owned runtime generation code

Work:

- create or move `BspLayoutGenerator` into
  `Assets/Scripts/Generation/Layout/`
- add a runtime asmdef for layout generation if needed
- keep the editor package as a thin consumer or facade
- preserve deterministic layout output and `layoutRevisionId` behavior unless
  a deliberate bump is required

Completed:

- moved `BspLayoutGenerator` into the repo-owned runtime namespace
  `BreachScenarioEngine.Generation.Layout`
- added `Assets/Scripts/Generation/Layout/BreachScenarioEngine.Generation.Layout.asmdef`
  with a runtime dependency on `BreachScenarioEngine.Generation.TacticalGraphs`
- updated the embedded editor package and source-copy package to consume the
  runtime layout generator through asmdef references
- removed the editor-package-owned BSP implementation copy
- kept the generator id as `pure_bsp_layout_v1` so existing
  `layoutRevisionId` behavior remains intentionally stable

Verification:

- confirmed the runtime BSP source has no `UnityEditor`, `UnityEngine`,
  `Tilemap`, `PrefabUtility`, `GameObject.Find`, `Resources`,
  `AssetDatabase`, `NavMesh`, or `MissionPipelineEditorService` references
- Unity batchmode script compilation completed with `ExitCode: 0` and
  `Tundra build success`
- targeted EditMode test execution was attempted, but Unity did not emit a
  test XML report before quitting; verification is compile-level for this
  session

Exit criteria:

- the layout algorithm no longer lives only inside the editor package
- the editor package consumes the runtime generator boundary cleanly

Recommended handoff note:

- "Please move the pure layout generator across the runtime boundary. Keep the
  existing deterministic layout contract intact."

Handoff note:

- "Pure BSP layout generation now lives under
  `Assets/Scripts/Generation/Layout/`, with the editor package consuming the
  runtime assembly. Compile is clean; rerun EditMode tests in a live Unity test
  pass if XML reporting is required before the next slice."

### Session 5: Catalog-Driven Visuals

Status:

- planned

Goal:

- make preview content lookup honest while keeping debug fallback available

Work:

- add a simple content resolver seam for tiles and prefabs
- validate catalog presence, but keep preview fallback explicit
- document that preview visuals are output-only and not the source of truth
- avoid introducing `Resources.Load` as the normal content path

Exit criteria:

- preview uses a clear content-resolution seam
- debug fallback is explicit and documented
- catalog handling is honest without overbuilding the content layer

Recommended handoff note:

- "Please add a content resolution seam so the preview layer can move toward
  real assets without hiding debug fallback behavior."

### Session 6: CI Honesty And Validation

Status:

- planned

Goal:

- make the GitHub Actions pilot workflow truthful and close the loop with a
  full validation sweep

Work:

- decide whether the workflow is a placeholder or a real Unity runner
- update workflow naming and step text accordingly
- run the local validation sequence for compile, EditMode tests, and pilot
  mission execution
- inspect generated reports and note any remaining risks

Exit criteria:

- workflow text matches its actual behavior
- local validation is run and recorded
- remaining risks are called out explicitly

Recommended handoff note:

- "Please finish by making the CI story honest and running the full validation
  sequence. If anything is deferred, record the exact blocker."

### Suggested Chat Split For This Track

Use separate chats for these clusters:

1. Materializer passability and consistency guards
2. Materializer tests
3. Runtime BSP boundary
4. Catalog-driven visuals
5. CI honesty and validation

### Handoff Checklist For This Track

Before switching chats:

- note what was completed
- note the exact file paths touched
- note the next blocking step
- note any failing test or compile message verbatim
- note whether Unity was last validated in batch mode or live mode
