# Migration From BREACH Scene Builders

Status: Planning document
Version: v2.3
Project: `Breach Scenario Engine`
Reference project: `E:/Games/Breach/BREACH`
Target project: `E:/Games/Breach/BreachScenarioEngine`

This document records which practical Unity solutions from the legacy
`BREACH` project should be transferred into `BreachScenarioEngine` and how they
must be adapted to the active v2.3 mission pipeline.

## 1. Migration Rule

Do not copy the legacy editor flow as-is. `BREACH` is a reference
implementation for tested Unity scene-building techniques. `BSE` remains the
source of truth for mission authoring, JSON contracts, lifecycle state, retry
rules, verification, and accepted replay data.

Transfer algorithms and conventions, not monolithic menu commands.

The target pipeline remains:

1. `validate_template`
2. `compile_payload`
3. `generate_layout`
4. `place_entities`
5. `verify`
6. `write_manifest`

Step 6 layout generation still runs before Step 5 placement, and
`generation_manifest.json` is written only after PASS.

## 2. Reference Sources

| BREACH source | Useful technology | Target role in BSE |
|---|---|---|
| `Assets/Scripts/Editor/Day1LevelGenerator.cs` | BSP room splitting, perimeter walls, room connections, windows, entrance, cover heuristics | Split into pure layout, tactical graph, placement, materialization, and preview layers |
| `Assets/Scripts/Mission/ApartmentLayoutBuilder.cs` | Fixed VS01 apartment layout and room/decor anchors | Reference preset or fallback layout for VS01 regression |
| `Assets/Scripts/Editor/Day1SceneSetup.cs` | Minimal Grid/Tilemap/collider scene skeleton | Formal scene skeleton contract plus typed `MissionSceneContext` |
| `Assets/Scripts/Editor/Day1Verification.cs` | Hierarchy, asset, tile, prefab, and import checks | JSON verification checks and content catalog validation |
| `Assets/Scripts/Editor/MapTileAssetBootstrap.cs` | Tile asset generation from sprite inputs | Catalog/bootstrap migration, not `Resources` runtime lookup |
| `Assets/Scripts/Editor/FullProjectBake.cs` | One-command bake workflow | CI wrapper around `manage_mission` plus optional scene preview |
| `MISSION_STANDARDS.md` | PPU, wall, door, window, sorting, NavMesh, pivot standards | Consolidated into BSE scene materialization and content validation docs |

## 3. What To Extract

### BSP and Room Topology

Extract these ideas from `Day1LevelGenerator`:

- `BSPRoom` as the seed for a serializable room node model
- `SplitRoom(...)` as the starting split heuristic
- `CollectLeaves(...)` as leaf-room enumeration
- `DrawLeafRoomWalls(...)` as wall-boundary intent, not direct Tilemap writes
- `ConnectRooms(...)` as a portal/doorway heuristic

Target location:

- `Assets/Scripts/Generation/Layout/BspLayoutGenerator.cs`
- `Assets/Scripts/Generation/Layout/RoomGraphBuilder.cs`
- `Assets/Scripts/Generation/Layout/PortalGraphBuilder.cs`

Target output:

- `mission_layout.generated.json`
- `LayoutGraph`
- `RoomGraph`
- `PortalGraph`

The pure layout generator must not call:

- `Tilemap.SetTile`
- `PrefabUtility.InstantiatePrefab`
- `GameObject.Find`
- `AssetDatabase.LoadAssetAtPath`
- `NavMeshBuilder.BuildNavMesh`

### Windows, Doors, and Breach Points

Extract:

- perimeter-window placement from `AddWindows(...)`
- external entry placement from `AddEntrance(...)`
- door clearing rule from `MISSION_STANDARDS.md`

Target output:

- portal edges with `kind: "door"` or `kind: "window"`
- breach points in layout graph metadata
- collision-clearance requirements for materialization

### Cover Placement

Extract:

- 1-3 cover items per room from `SpawnFurniture(...)`
- no cover near doors from `MISSION_STANDARDS.md`

Target location:

- `Assets/Scripts/Generation/TacticalGraphs/CoverGraphBuilder.cs`
- later: `Assets/Scripts/Generation/Placement/EntityPlacementPlanner.cs`

Cover must become deterministic data in `mission_layout.generated.json` or
`mission_entities.generated.json`, not immediate prefab instantiation.

### Fixed Apartment Preset

Extract from `ApartmentLayoutBuilder`:

- VS01 room proportions
- corridor shape
- hostage/guard room intent
- extraction zone
- minimal decor anchors

Target location:

- `Assets/Scripts/Generation/Presets/FixedApartmentLayoutPreset.cs`

Use this only for:

- VS01 regression
- smoke testing the materializer
- fallback when the BSP generator is temporarily blocked

Do not keep the `[ExecuteAlways]` destructive scene-rebuild pattern in BSE.

### Scene Skeleton

Extract from `Day1SceneSetup`:

- `Grid`
- `World_Base`
- `World_Collision`
- `TilemapCollider2D`
- `CompositeCollider2D`
- static `Rigidbody2D`

Target form:

- `MissionSceneRoot`
- explicit child roots
- serialized `MissionSceneContext`

The materializer should depend on typed references, not `GameObject.Find`.

### Verification

Extract checks from `Day1Verification`, but change the output model.

Target location:

- `Assets/Scripts/Generation/Verification/MissionSceneVerifier.cs`
- `Assets/Scripts/Generation/Verification/LayoutGraphVerifier.cs`
- `Assets/Scripts/Generation/Verification/TacticalDensityVerifier.cs`
- `Assets/Scripts/Generation/Verification/NavigationVerifier.cs`

Target output is JSON:

```json
{
  "status": "PASS",
  "missionId": "VS01_HostageApartment",
  "pipelineVersion": "2.3",
  "action": "verify",
  "findings": [],
  "metrics": {
    "roomCount": 6,
    "portalCount": 7,
    "breachPointCount": 2,
    "enemyCount": 4,
    "coverPointCount": 12,
    "alternateRoutes": 2,
    "hearingOverlapPercentage": 42,
    "chokepointPressure": 0.35,
    "objectiveRoomPressure": 0.5
  }
}
```

Console output may duplicate JSON for humans, but it must not become the
decision source.

### Asset Bootstrap

Extract from `MapTileAssetBootstrap`:

- tile specifications
- sprite-to-tile bootstrap workflow
- import validation requirements

Target form:

- `Assets/Data/Mission/Catalogs/EnvironmentCatalog.asset`
- `Assets/Data/Mission/Profiles/AddressablesCatalogProfile.asset`
- optional editor-only catalog bootstrap command

Avoid transferring runtime `Resources.Load<T>()` as the normal lookup path.

### Full Bake

Extract from `FullProjectBake` only the idea of a single command.

Target flow:

1. `manage_mission(action="validate_template")`
2. `manage_mission(action="compile_payload")`
3. `manage_mission(action="generate_layout")`
4. `manage_mission(action="place_entities")`
5. `manage_mission(action="verify")`
6. `manage_mission(action="write_manifest")`
7. optional editor-only scene preview materialization

## 4. What Not To Transfer Directly

Do not transfer these patterns into BSE:

- monolithic `Day1LevelGenerator` editor command
- direct enemy spawning inside layout generation
- the duplicate `SpawnEnemies(leaves)` behavior observed in
  `Day1LevelGenerator`
- destructive scene cleanup without generated ownership markers
- `GameObject.Find` as a materializer contract
- `Resources.Load<T>()` as the primary runtime content lookup
- direct `AssetDatabase` access from pure generation code
- NavMesh bake inside BSP generation
- console-only verification
- old BREACH `MissionConfig` as the BSE mission config model

These patterns conflict with v2.3 JSON-only decisions, layout-before-placement
ordering, generated ownership, catalog/profile validation, and accepted-manifest
rules.

## 5. Target Layering

```text
Assets/Scripts/Generation/
  Contracts/
    MissionPayloadDto.cs
    LayoutGraphDto.cs
    EntityPlacementDto.cs
    VerificationDto.cs

  Layout/
    BspLayoutGenerator.cs
    RoomGraphBuilder.cs
    PortalGraphBuilder.cs

  TacticalGraphs/
    CoverGraphBuilder.cs
    VisibilityGraphBuilder.cs
    HearingGraphBuilder.cs

  Placement/
    EntityPlacementPlanner.cs
    ObjectivePlacementPlanner.cs

  Verification/
    MissionVerifier.cs
    LayoutGraphVerifier.cs
    TacticalDensityVerifier.cs
    NavigationVerifier.cs

  UnityMaterialization/
    MissionSceneContext.cs
    MissionSceneMaterializer.cs
    TilemapMaterializer.cs
    PrefabMaterializer.cs

  Presets/
    FixedApartmentLayoutPreset.cs
```

## 6. Acceptance Rule

A migrated BREACH technology is accepted in BSE only when:

- it is represented as deterministic data or typed content references
- it obeys Step 6 before Step 5
- it writes or consumes v2.3 artifacts
- it does not require Markdown or Console parsing for decisions
- it preserves generated ownership markers when creating scene objects
- it can be verified through `manage_mission` and JSON artifacts

