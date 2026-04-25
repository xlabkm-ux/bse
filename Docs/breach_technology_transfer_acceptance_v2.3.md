# BREACH Technology Transfer Acceptance v2.3

Status: Planning checklist
Version: v2.3
Project: `Breach Scenario Engine`

This checklist defines when a technology migrated from `E:/Games/Breach/BREACH`
is considered accepted in BSE.

Target platforms:

- Primary: Windows 10 / 11 desktop, 1920x1080+ resolution, 16:9 or wider
- Secondary: Android mobile, 1080p portrait and landscape, touch controls, and
  optional controller support
- UI: scalable across supported desktop and mobile resolutions

## 1. Global Acceptance Gates

Every transferred feature must satisfy:

- v2.3 docs and implementation remain consistent
- `manage_mission` remains the public mission-pipeline surface
- machine decisions are available as JSON
- Step 6 layout runs before Step 5 placement
- retries return to Step 6
- `generation_manifest.json` is written only after verification PASS
- `effectiveSeed` remains pipeline-owned
- generated scene objects have ownership markers
- generated files use repository-relative paths
- runtime-facing migrated code does not assume desktop-only input or
  editor-only asset lookup
- migrated UI and preview/runtime surfaces remain scalable for 1920x1080+
  desktop and 1080p Android portrait/landscape targets

## 2. Source Traceability

The active source traceability audit is recorded in
[breach_technology_transfer_traceability_v2.3.md](breach_technology_transfer_traceability_v2.3.md).

Each transferred feature must record:

- BREACH reference file
- BSE target file or asset
- transferred behavior
- intentionally discarded behavior
- verification method

Suggested traceability table:

| Feature | BREACH source | BSE target | Verification |
|---|---|---|---|
| BSP layout | `Day1LevelGenerator.cs` | `BspLayoutGenerator.cs` | deterministic layout artifact |
| Fixed VS01 preset | `ApartmentLayoutBuilder.cs` | `FixedApartmentLayoutPreset.cs` | VS01 regression run |
| Scene skeleton | `Day1SceneSetup.cs` | `MissionSceneContext.cs` | materialization smoke test |
| Content checks | `Day1Verification.cs` | JSON verifier/catalog validation | `verification_summary.json` |
| Tile bootstrap | `MapTileAssetBootstrap.cs` | environment catalog bootstrap | catalog resolution test |
| Full bake | `FullProjectBake.cs` | mission CI runner | batchmode CI |

## 3. Layout Acceptance

The migrated layout layer is accepted when:

- layout generation is deterministic for the same payload and seed
- `layoutRevisionId` is stable for identical layout inputs
- generated rooms fit within `spatial.bounds`
- portal graph represents doors, windows, and breach points
- layout output does not instantiate Unity objects
- layout output does not bake NavMesh
- layout output does not place actors or objectives

Failure examples:

- direct `Tilemap.SetTile` inside pure layout generation
- `PrefabUtility.InstantiatePrefab` inside Step 6
- actor placement before `mission_layout.generated.json` exists

## 4. Placement Acceptance

The migrated placement layer is accepted when:

- placement is blocked without a current layout
- placement fails on stale `layoutRevisionId`
- actors and objectives include `roomId` and `navNodeId` where applicable
- enemies are not spawned twice
- entry placement, enemy placement, hostage placement, and objective placement
  are separate decisions

Required failure codes:

- `ORDER_VIOLATION_NO_LAYOUT_GRAPH`
- `ORDER_VIOLATION_STALE_LAYOUT`

## 5. Materialization Acceptance

The materializer is accepted when:

- it consumes generated JSON artifacts
- it uses a typed `MissionSceneContext`
- it follows `scene_materialization_contract_v2.3.md`
- it writes only generated-owned scene objects
- it can rebuild a preview without deleting user-owned content
- it resolves prefabs and tiles through catalogs or documented compatibility
  adapters

Failure examples:

- scene hierarchy becomes the mission source of truth
- cleanup deletes all objects with common names regardless of ownership
- materializer requires `GameObject.Find` for required roots

## 6. Verification Acceptance

The migrated verification layer is accepted when:

- `verification_summary.json` is the decision source
- Console output is optional and human-only
- findings include stable machine-readable codes
- verification metrics cover navigation, tactical density, and content
  resolution

Required metric families:

- layout: room count, portal count, breach point count
- tactical density: enemy count, cover count, empty room count
- navigation: reachable objectives, unreachable critical nodes, alternate
  routes
- acoustic/visibility: hearing overlap percentage, visibility ray count
- pressure: chokepoint pressure, objective room pressure

## 7. Content Acceptance

The migrated content layer is accepted when:

- mission payloads contain repository-relative profile and catalog refs
- tile, prefab, enemy, objective, and environment refs resolve through BSE
  catalog assets
- import standards such as PPU and pivot are validated
- missing content is reported as JSON findings

Temporary compatibility exceptions must include:

- reason
- allowed scope
- removal condition

## 8. CI Acceptance

The migrated full-bake workflow is accepted when it:

- runs the canonical mission actions in order
- does not bypass `manage_mission` semantics
- records lifecycle state
- fails without writing an accepted manifest
- can optionally materialize a scene preview after PASS

## 9. Known Risks

| Risk | Source | Required mitigation |
|---|---|---|
| Duplicate enemy creation | `Day1LevelGenerator` calls `SpawnEnemies(leaves)` twice | Step 5 placement owns entity creation exactly once |
| Destructive scene cleanup | legacy cleanup deletes common object names | generated ownership marker and mission-scoped cleanup only |
| Runtime `Resources` dependency | tile bootstrap and fixed apartment builder use Resources paths | catalog/profile lookup with optional temporary adapter |
| Console-only pass/fail | `Day1Verification` logs to Console | JSON verifier and MCP envelope |
| Layout/editor coupling | `Day1LevelGenerator` mixes BSP, tilemaps, prefabs, NavMesh | pure layout plus separate materializer |
| Scene as source of truth | fixed scene builders mutate opened scenes | generated JSON artifacts remain authoritative |

## 10. Handoff Checklist

Before handing off migration work:

- update this checklist with completed feature rows
- record touched files
- record whether Unity was validated in batch mode or live mode
- record any failing compile or test output verbatim
- record the next blocking phase from
  `breach_technology_transfer_plan_v2.3.md`
