# BREACH Technology Transfer Traceability v2.3

Status: Source traceability audit
Version: v2.3
Project: `Breach Scenario Engine`
Reference project: `E:/Games/Breach/BREACH`
Audit date: 2026-04-25

This audit freezes `BREACH` as a read-only reference for the transfer track.
No source code from `BREACH` has been copied into BSE. The useful behaviors are
recorded here as implementation guidance for new BSE modules that must obey
the v2.3 pipeline: JSON artifacts are authoritative, Step 6 layout runs before
Step 5 placement, and scene materialization is output rather than source of
truth.

## 1. Reference Files Inspected

| Reference file | Source role | Transfer posture |
|---|---|---|
| `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1LevelGenerator.cs` | Monolithic editor generator for BSP layout, tilemaps, portals, windows, covers, enemies, NavMesh, and camera setup | Extract algorithms only; discard editor-monolith flow |
| `E:/Games/Breach/BREACH/Assets/Scripts/Mission/ApartmentLayoutBuilder.cs` | Fixed VS01 apartment tilemap builder | Preserve proportions and room intent as preset/fallback data |
| `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1SceneSetup.cs` | Minimal Grid/Tilemap/collider scene skeleton | Convert to typed materialization context |
| `E:/Games/Breach/BREACH/Assets/Scripts/Editor/Day1Verification.cs` | Console verification for hierarchy, import settings, tiles, and prefabs | Convert to JSON findings and catalog/profile validation |
| `E:/Games/Breach/BREACH/Assets/Scripts/Editor/MapTileAssetBootstrap.cs` | Tile asset bootstrap from sprite specifications | Convert to catalog bootstrap/content validation |
| `E:/Games/Breach/BREACH/Assets/Scripts/Editor/FullProjectBake.cs` | One-command editor bake for assets, scene rebuild, NavMesh, lighting, and save | Replace with CI wrapper around public mission actions |
| `E:/Games/Breach/BREACH/MISSION_STANDARDS.md` | Wall, door, window, cover, PPU, pivot, sorting, and NavMesh standards | Re-express as materialization/content verification rules |

## 2. Current BSE Implementation Points

| BSE path | Current role | Transfer impact |
|---|---|---|
| `dotnet-prototype/unity/com.breachscenarioengine.unity-mcp/Editor/MissionPipelineEditorService.cs` | Public `manage_mission` service; currently owns template validation, payload compile, simple fixed layout, placement, verification, retry, lock, and manifest writes | Keep as orchestration/service boundary; extract Step 6 layout logic into pure generation modules |
| `Assets/Scripts/Generation/` | Generation namespace with placeholder folders and hybrid authoring scaffold | Add pure `Layout/`, `TacticalGraphs/`, `Placement/`, `Verification/`, `UnityMaterialization/`, and `Presets/` modules here as transfer proceeds |
| `Assets/Scripts/Runtime/MissionRuntimeJsonModels.cs` | Runtime DTOs for manifest, layout graphs, tactical graphs, entities, and ownership | Expand DTOs only when generated artifacts need new deterministic fields such as breach/window positions |
| `Assets/Scripts/Runtime/MissionSceneBuilder.cs` | Current debug scene builder from manifest/layout/entities using generated ownership markers | Treat as preview baseline; later replace or wrap with materializer using typed `MissionSceneContext` and catalogs |

## 3. Feature Traceability Matrix

| Feature | BREACH source behavior | BSE target module | Copied idea | Intentionally discarded legacy behavior | Verification method |
|---|---|---|---|---|---|
| BSP room splitting | `Day1LevelGenerator` splits a rectangular building recursively using minimum room size and aspect-ratio pressure | `Assets/Scripts/Generation/Layout/BspLayoutGenerator.cs` plus `RoomGraphBuilder.cs` | Deterministic rectangular split tree, leaf-room enumeration, room bounds within payload `spatial.bounds` | Unity `Random`, editor menu command, Tilemap writes during split, scene cleanup, direct asset lookup | `manage_mission(action="generate_layout")` writes stable `mission_layout.generated.json` and stable `layoutRevisionId` for identical payload/seed |
| Room perimeter intent | `Day1LevelGenerator` fills floor, draws perimeter walls, then draws leaf-room wall boundaries | `RoomGraphBuilder.cs` and later `TilemapMaterializer.cs` | Represent outer shell and room boundaries as data | `Tilemap.SetTile` in Step 6, wall drawing as source of truth | Layout JSON contains room rects; materialization smoke test later proves tile output |
| Door portals | `ConnectRooms` chooses a doorway on each split boundary, clears one wall tile, and instantiates a door prefab | `PortalGraphBuilder.cs` | Portal edges between sibling rooms with width and orientation metadata | `PrefabUtility.InstantiatePrefab`, `GameObject.Find("Doors")`, Tilemap mutation inside layout | Portal graph count and connectivity verified in JSON; placement/verify reachability remains PASS |
| Perimeter windows | `AddWindows` places windows on rooms touching west/east/north perimeter and avoids south entrance side | `PortalGraphBuilder.cs` or `LayoutFeatureBuilder.cs` | External window markers tied to perimeter rooms | `Resources`/`AssetDatabase` tile lookup and direct collision tile replacement | `mission_layout.generated.json` includes deterministic window features; content validation reports missing window catalog refs |
| Breach and entry points | `AddEntrance` selects a south-side room, clears a perimeter wall tile, and creates `Entrance_Door` | `BspLayoutGenerator.cs` layout metadata and `PortalGraphBuilder.cs` external portal | Explicit entry/breach point with entry room id | Hard-coded south street spawn, direct door prefab creation, operative scene repositioning | `LayoutGraph.entryRoomId` and breach metadata are reachable; `NAV_BREACHPOINT_UNREACHABLE` catches invalid output |
| Initial cover candidates | `SpawnFurniture` places 1-3 cover prefabs per room away from room edges | `Assets/Scripts/Generation/TacticalGraphs/CoverGraphBuilder.cs` | Cover density by room size and tactical profile | Prefab spawning during layout, random non-seeded positions, no door clearance model | Cover graph count and door/breach/objective clearance metrics in `verification_summary.json` |
| Enemy placement | `SpawnEnemies` puts enemies in generated rooms and is accidentally called twice | `Assets/Scripts/Generation/Placement/EntityPlacementPlanner.cs` | Enemy count should be room-aware and avoid entry/street rooms | All enemy spawning in Step 6, duplicate call, direct prefab lookup | `place_entities` writes enemies exactly once with `roomId`, `navNodeId`, and current `layoutRevisionId` |
| Operative entry placement | `PositionOperativesOnStreet` moves scene operatives below the generated building | `EntityPlacementPlanner.cs` | Separate player entry placement from enemy/objective placement | Finding existing scene operatives, mutating scene transforms before JSON placement | Entities contain entry placement records; materializer consumes records later |
| Fixed VS01 apartment | `ApartmentLayoutBuilder` builds kitchen, bathroom, hostage room, guard room, corridor, two entries, extraction, and decor anchors | `Assets/Scripts/Generation/Presets/FixedApartmentLayoutPreset.cs` | Preserve VS01 proportions, room tags, corridor/extraction intent for regression or fallback | `[ExecuteAlways]`, destructive Tilemap clearing, `Resources.Load`, generated sprites in runtime path | VS01 regression pipeline can choose preset/fallback and still pass layout, placement, verification |
| Scene skeleton | `Day1SceneSetup` creates `Grid`, `World_Base`, `World_Collision`, collider, composite collider, and sorting order | `Assets/Scripts/Generation/UnityMaterialization/MissionSceneContext.cs` and `TilemapMaterializer.cs` | Canonical roots and Tilemap component requirements | `GameObject.Find` as dependency contract, manual sorting-layer warning, editor-only menu flow | Materializer smoke test builds typed context and does not depend on hierarchy name lookup |
| Content/import verification | `Day1Verification` checks Grid, tilemaps, NavMeshManager, lights, texture PPU/pivot, tile sprites, and prefab existence | `Assets/Scripts/Generation/Verification/MissionSceneVerifier.cs` and catalog validators | Required hierarchy/content/import checks | Console-only PASS/FAIL, `AssetDatabase` checks as runtime truth | JSON findings in `verification_summary.json` or materialization result; missing content never relies on Console parsing |
| Tile bootstrap | `MapTileAssetBootstrap` maps tile asset names to sprite paths under `Assets/Resources` and creates/updates Tile assets | Optional editor bootstrap plus `EnvironmentCatalog.asset` | Tile specs and sprite-to-tile bootstrap workflow | Runtime `Resources` lookup as primary content path | Catalog resolution tests and JSON findings for missing tile/sprite/import requirements |
| Full bake | `FullProjectBake` fixes textures, generates tiles, opens VS01 scene, rebuilds layout, bakes NavMesh, sets lighting, and saves scene | Editor CI runner around `manage_mission` plus optional preview materialization | One command for end-to-end preview | Bypassing mission lifecycle, writing scenes as acceptance source, deprecated direct NavMesh bake, direct scene save as required output | Batchmode CI runs canonical mission actions and writes accepted manifest only after Step 7 PASS |
| Mission standards | `MISSION_STANDARDS.md` defines 1-tile walls, door clearing/rotation, perimeter windows, 1-3 covers per room, PPU 128, center pivots, sorting, NavMesh agent sizing | `scene_materialization_contract_v2.3.md`, catalog validation, tactical verification | Transfer standards as measurable JSON/content rules | Legacy requirement to run `Day1Verification` and bake NavMesh after every generator change | Verification metrics and catalog/import findings cover standards without requiring BREACH console scripts |

## 4. Target Implementation Boundaries

The first implementation slice is pure Step 6 layout data:

- read `mission_payload.generated.json` or the same normalized draft data
  currently used by `MissionPipelineEditorService`
- consume `spatial.bounds`, `spatial.bsp`, seed, profile/catalog refs already
  validated before Step 6
- emit rooms, portals, windows, breach points, initial cover candidates, and
  `layoutRevisionId`
- keep actor/objective placement in Step 5
- keep Unity scene objects in materialization, not generation

Step 6 transfer code must not call:

- `Tilemap.SetTile`
- `PrefabUtility.InstantiatePrefab`
- `GameObject.Find`
- `Resources.Load`
- `AssetDatabase.LoadAssetAtPath`
- `NavMeshBuilder.BuildNavMesh`
- actor or objective placement helpers

## 5. First Implementation Slice

Start Transfer Session 2 by replacing the temporary fixed four-room layout in
`MissionPipelineEditorService.BuildLayoutNode(...)` with a call into a pure
BSP layout generator module. The service should remain the public
`manage_mission(action="generate_layout")` entry point while the generator owns
only deterministic layout data.

Required outputs for the first slice:

- `RoomGraph.rooms` generated from BSP leaves
- `PortalGraph.portals` generated from BSP split adjacency
- external window and breach/entry features represented in layout JSON
- `CoverGraph.coverPoints` generated as deterministic candidates
- stable `layoutRevisionId` for identical mission payload, seed, and generator
  parameters

Existing regression expectations to preserve:

- `place_entities` fails with `ORDER_VIOLATION_NO_LAYOUT_GRAPH` without layout
- `place_entities` fails on stale `layoutRevisionId`
- generated entities include ownership, `roomId`, `navNodeId`, and
  `layoutRevisionId`
- `verify` remains JSON-only and manifest writing remains blocked unless PASS

## 6. Audit Result

Transfer Session 1 exit criteria are satisfied:

- every planned transfer feature has a reference source and BSE target
- no BREACH code was copied into BSE
- the first implementation slice is scoped to pure Step 6 layout data
