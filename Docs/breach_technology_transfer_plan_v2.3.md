# BREACH Technology Transfer Plan v2.3

Status: Planning document
Version: v2.3
Project: `Breach Scenario Engine`
Reference project: `E:/Games/Breach/BREACH`

This plan breaks the transfer of proven BREACH technologies into BSE into
implementation-ready phases. It assumes the v2.3 contracts remain authoritative:
layout before placement, JSON-only mission decisions, mission-scoped lifecycle
state, and accepted manifests only after PASS.

Target platforms:

- Primary: Windows 10 / 11 desktop, 1920x1080+ resolution, 16:9 or wider
- Secondary: Android mobile, 1080p portrait and landscape, touch controls, and
  optional controller support
- UI: scalable across supported desktop and mobile resolutions

## 1. Goal

Bring the practical Unity work from `BREACH` into the current project without
importing the old editor-monolith architecture.

The transfer should improve:

- BSP apartment-like layout generation
- doors, windows, breach points, and extraction geometry
- cover-point and tactical-density heuristics
- scene materialization from generated artifacts
- content and prefab validation
- optional one-command CI preview generation
- platform-aware preview/runtime assumptions for desktop controls first and
  mobile touch support second

The transfer must not replace:

- `MissionConfig`
- v2.3 mission templates
- `manage_mission`
- `mission_payload.generated.json`
- `mission_layout.generated.json`
- `mission_entities.generated.json`
- `verification_summary.json`
- `generation_manifest.json`

## 2. Phase 0: Documentation Baseline

Deliverables:

- `Docs/migration_from_breach_scene_builders.md`
- `Docs/breach_technology_transfer_plan_v2.3.md`
- `Docs/scene_materialization_contract_v2.3.md`
- `Docs/breach_technology_transfer_acceptance_v2.3.md`

Exit criteria:

- migration sources are named
- non-transferable legacy patterns are recorded
- target module boundaries are documented
- acceptance gates are explicit

## 3. Phase 1: Reference Source Freeze

Purpose:

Treat BREACH as a read-only reference while BSE receives new implementation.

Work:

- record exact reference files and paths
- capture source behavior notes for BSP, fixed layout, scene skeleton,
  verification, tile bootstrap, and full bake
- avoid editing `E:/Games/Breach/BREACH` during BSE migration

Exit criteria:

- each migrated feature points back to a BREACH reference file
- each feature has a BSE target module
- known hazards are listed before implementation starts

## 4. Phase 2: Pure Layout Extraction

Purpose:

Move useful BSP behavior from `Day1LevelGenerator` into a pure Step 6 layout
generator.

Target files:

- `Assets/Scripts/Generation/Layout/BspLayoutGenerator.cs`
- `Assets/Scripts/Generation/Layout/RoomGraphBuilder.cs`
- `Assets/Scripts/Generation/Layout/PortalGraphBuilder.cs`

Inputs:

- `mission_payload.generated.json`
- `spatial.bounds`
- `spatial.bsp`
- `header.initialSeed` or retry seed
- profile and catalog refs validated before Step 6

Outputs:

- deterministic room graph
- portal graph
- breach points
- initial cover candidate graph
- `layoutRevisionId`
- `mission_layout.generated.json`

Rules:

- no Tilemap writes
- no prefab instantiation
- no editor object lookup
- no NavMesh bake
- no actor or objective placement

Exit criteria:

- same seed and payload produce the same layout revision
- generated layout satisfies v2.3 artifact shape
- placement remains blocked when layout is absent or stale

## 5. Phase 3: Tactical Graph Builders

Purpose:

Turn BREACH scene heuristics into reusable tactical graph data.

Target files:

- `Assets/Scripts/Generation/TacticalGraphs/CoverGraphBuilder.cs`
- `Assets/Scripts/Generation/TacticalGraphs/VisibilityGraphBuilder.cs`
- `Assets/Scripts/Generation/TacticalGraphs/HearingGraphBuilder.cs`

Reference inputs:

- cover count and door-clearance rules from `MISSION_STANDARDS.md`
- window and portal placement from `Day1LevelGenerator`
- acoustic thresholds from v2.3 mission template and profiles

Outputs:

- `CoverGraph`
- `VisibilityGraph`
- `HearingGraph`

Exit criteria:

- cover points do not block doors or breach points
- visibility and hearing metrics are included in verification
- tactical graph ids are deterministic for the same layout revision

## 6. Phase 4: Placement Planner

Purpose:

Replace direct BREACH spawning with Step 5 data placement.

Target files:

- `Assets/Scripts/Generation/Placement/EntityPlacementPlanner.cs`
- `Assets/Scripts/Generation/Placement/ObjectivePlacementPlanner.cs`

Inputs:

- current layout revision
- room tags and portal graph
- actor roster
- objective rules
- enemy/objective catalogs

Outputs:

- `mission_entities.generated.json`
- actors with `roomId`, `navNodeId`, and `layoutRevisionId`
- objectives with generated ownership metadata

Rules:

- do not place against missing layout
- do not place against stale `layoutRevisionId`
- do not duplicate enemy spawning
- keep player entry placement and enemy placement separated

Exit criteria:

- Step 5 returns `ORDER_VIOLATION_NO_LAYOUT_GRAPH` without current layout
- generated entities reference the active layout revision
- no entity placement occurs inside Step 6

## 7. Phase 5: Scene Materialization

Purpose:

Build an optional Unity scene preview from accepted or current generated
artifacts.

Target files:

- `Assets/Scripts/Generation/UnityMaterialization/MissionSceneContext.cs`
- `Assets/Scripts/Generation/UnityMaterialization/MissionSceneMaterializer.cs`
- `Assets/Scripts/Generation/UnityMaterialization/TilemapMaterializer.cs`
- `Assets/Scripts/Generation/UnityMaterialization/PrefabMaterializer.cs`

Inputs:

- `mission_layout.generated.json`
- `mission_entities.generated.json`
- content catalogs
- profile settings

Rules:

- materialization is output, not mission authority
- all generated GameObjects receive `GeneratedOwnershipMarker`
- destructive cleanup targets only generated-owned roots
- no `GameObject.Find` dependency in the core contract

Exit criteria:

- VS01 can be materialized from generated artifacts
- materializer can rebuild preview without deleting user-owned objects
- generated hierarchy follows `scene_materialization_contract_v2.3.md`

## 8. Phase 6: JSON Verification Expansion

Purpose:

Transfer BREACH verification intent into BSE JSON verification.

Target files:

- `Assets/Scripts/Generation/Verification/MissionSceneVerifier.cs`
- `Assets/Scripts/Generation/Verification/LayoutGraphVerifier.cs`
- `Assets/Scripts/Generation/Verification/TacticalDensityVerifier.cs`
- `Assets/Scripts/Generation/Verification/NavigationVerifier.cs`

Metrics to cover:

- `roomCount`
- `portalCount`
- `breachPointCount`
- `enemyCount`
- `coverPointCount`
- `reachableObjectives`
- `unreachableObjectives`
- `alternateRoutes`
- `hearingOverlapPercentage`
- `chokepointPressure`
- `objectiveRoomPressure`

Exit criteria:

- `verification_summary.json` is the decision source
- `write_manifest` remains blocked unless verification PASS is compatible with
  current lifecycle state
- source BREACH console checks are either represented in JSON or explicitly
  deferred

## 9. Phase 7: Content Catalog Bootstrap

Purpose:

Replace BREACH `Resources` lookup with BSE profile/catalog-owned content.

Target files and assets:

- `Assets/Data/Mission/Catalogs/EnvironmentCatalog.asset`
- `Assets/Data/Mission/Catalogs/EnemyCatalog.asset`
- `Assets/Data/Mission/Catalogs/ObjectiveCatalog.asset`
- optional editor bootstrap under `Assets/Editor/`

Reference source:

- `MapTileAssetBootstrap.cs`
- `Day1Verification.cs`
- `MISSION_STANDARDS.md`

Exit criteria:

- tile and prefab refs resolve through catalogs
- import requirements are validated as JSON findings
- `Resources` remains only as temporary compatibility if absolutely needed

## 10. Phase 8: CI Preview Wrapper

Purpose:

Provide the BSE equivalent of BREACH `FullProjectBake`.

Target:

- editor CI runner that calls mission actions in the canonical order
- optional `materialize_scene_preview` or editor-only preview step after PASS

Exit criteria:

- CI runs through the public `manage_mission` surface or the same service
  layer
- failed generation writes state and verification JSON, not a failed manifest
- preview scene generation is not required for accepted replay

## 11. Recommended Order

1. Finish documentation baseline.
2. Implement pure BSP layout.
3. Add tactical graph builders.
4. Harden placement planner.
5. Add materializer and scene context.
6. Expand verification metrics.
7. Move content lookup into catalogs.
8. Add CI preview wrapper.

## 12. Stop Conditions

Pause implementation and update docs if any phase requires:

- changing v2.3 artifact ownership
- making scene hierarchy the source of mission truth
- writing manifests before PASS
- skipping Step 6 on retry
- hand-authoring `effectiveSeed` or `layoutRevisionId`
- relying on Console or Markdown for machine decisions
