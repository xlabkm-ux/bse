# Scene Materialization Contract v2.3

Status: Planning contract
Version: v2.3
Project: `Breach Scenario Engine`

This contract defines how generated mission artifacts are turned into an
optional Unity scene preview or playable scene. The scene is never the source of
truth for mission acceptance. The source of truth remains the v2.3 JSON
pipeline artifacts.

Target platforms:

- Primary: Windows 10 / 11 desktop, 1920x1080+ resolution, 16:9 or wider
- Secondary: Android mobile, 1080p portrait and landscape, touch controls, and
  optional controller support
- UI: scalable across supported desktop and mobile resolutions

## 1. Authority

Materialization consumes:

- `mission_payload.generated.json`
- `mission_layout.generated.json`
- `mission_entities.generated.json`
- profile assets under `Assets/Data/Mission/Profiles/`
- catalog assets under `Assets/Data/Mission/Catalogs/`

Materialization may read `generation_manifest.json` when building an accepted
replay preview. It must not write accepted replay data.

## 2. Canonical Hierarchy

```text
MissionSceneRoot
  Grid
    World_Base
    World_Collision
    World_Decor
    World_Interactables

  Generated
    Doors
    Windows
    Covers
    Enemies
    Operatives
    Objectives
    Hostages
    Extraction
    Debug
```

`MissionSceneContext` owns serialized references to these roots and components.
The materializer should receive a context object and should not discover its
core dependencies through `GameObject.Find`.

## 3. Required Context Shape

```csharp
public sealed class MissionSceneContext : MonoBehaviour
{
    public Grid Grid;
    public Tilemap BaseMap;
    public Tilemap CollisionMap;
    public Tilemap DecorMap;
    public Tilemap InteractablesMap;
    public Transform DoorsRoot;
    public Transform WindowsRoot;
    public Transform CoversRoot;
    public Transform EnemiesRoot;
    public Transform OperativesRoot;
    public Transform ObjectivesRoot;
    public Transform HostagesRoot;
    public Transform ExtractionRoot;
    public Transform DebugRoot;
}
```

The exact implementation may evolve, but it must preserve typed references for
the materializer.

## 4. Tilemap Layers

| Layer | Purpose | Required components | Sorting order |
|---|---|---|---:|
| `World_Base` | floors, street, non-blocking base tiles | `Tilemap`, `TilemapRenderer` | 0 |
| `World_Collision` | walls and blocking collision tiles | `Tilemap`, `TilemapRenderer`, `TilemapCollider2D`, `CompositeCollider2D`, static `Rigidbody2D` | 1 |
| `World_Decor` | non-blocking decor | `Tilemap`, `TilemapRenderer` | 2 |
| `World_Interactables` | tile-based interactables where needed | `Tilemap`, `TilemapRenderer` | 2 |

Collision tilemaps must use composite colliders when the active platform and
package set support them.

## 5. Spatial Standards

Transferred standards from `BREACH/MISSION_STANDARDS.md`:

- wall thickness: 1 tile
- default PPU: 128
- allowed PPU from v2.3 payload: 128 or 256
- tile sprite pivot: center `(0.5, 0.5)`
- door and window placement is tile-centered
- characters render above world and props
- generated objects use stable ownership metadata

Sorting:

- floors: order 0
- walls: order 1
- doors, windows, covers, furniture: order 2
- characters: order 3
- debug overlays: order 10 or above

## 6. Door Rules

Doors are materialized from portal edges.

Required behavior:

- every door clears the corresponding collision tile
- door prefab is centered in the tile
- horizontal-wall doors rotate 90 degrees
- vertical-wall doors rotate 0 degrees
- external entry doors are marked as breach or entry points when the layout
  graph says so
- breach points clear collision using deterministic tile-rounding rules from
  their `side` and `width` data

The materializer must not infer authoritative portal topology from the scene.
The portal graph owns that topology.

## 7. Window Rules

Windows are materialized from portal edges or window markers in the layout
graph.

Required behavior:

- windows are allowed only on external walls unless a future contract adds
  interior windows
- windows do not create actor placement by themselves
- windows stay visual-only in materialization unless a future contract adds an
  explicit breachable flag or equivalent field
- window visual assets come from the environment catalog
- missing window assets are validation/materialization findings

## 8. Cover Rules

Covers are materialized from cover points or generated entity records.

Required behavior:

- cover does not block doors, breach points, or objective access
- cover references a catalog entry
- cover receives generated ownership metadata
- cover density is verified through JSON metrics

Transferred BREACH heuristic:

- 1-3 cover objects per suitable room, adjusted by room size and tactical
  density profile.

## 9. Actors and Objectives

Actors and objectives come from `mission_entities.generated.json`.

Required metadata:

- `missionId`
- `layoutRevisionId`
- generated ownership marker
- source artifact path or stable generated id
- room id when applicable
- nav node id when applicable

The materializer must reject or warn on entities whose `layoutRevisionId` does
not match the current layout.

## 10. Generated Ownership

All materialized objects under `Generated` must have a stable generated
ownership marker.

Cleanup is allowed only for:

- generated roots
- generated-owned objects
- artifacts owned by the current mission id

Cleanup must not delete:

- user-authored scene objects
- non-generated prefabs
- profile/catalog assets
- mission templates

This replaces the destructive BREACH cleanup pattern.

## 11. Navigation

Navigation baking or preview navigation setup is a materialization/verification
concern, not a layout-generator concern.

Navigation rules:

- layout generation may emit navigation graph data
- materialization may prepare Unity colliders and preview navigation surfaces
- verification reports reachability and navigation metrics through JSON
- NavMesh bake must not be required to produce `mission_layout.generated.json`

Transferred BREACH default:

- agent radius target: 0.4-0.5 world units for 2D top-down actors
- update rotation/up-axis disabled where Unity NavMeshAgent is used for 2D

## 12. Content Lookup

Materialization resolves content through:

- `EnvironmentCatalog.asset`
- `EnemyCatalog.asset`
- `ObjectiveCatalog.asset`
- relevant profile assets

`Resources.Load<T>()` is not the normal contract. It may exist only as a
temporary compatibility path with a documented removal plan.

## 13. Platform Constraints

Materialized scenes must preserve the product platform targets:

- Windows 10 / 11 is the primary validation target for editor preview,
  desktop play mode, keyboard/mouse, optional controller workflows, and
  1920x1080+ 16:9-or-wider displays.
- Android is the secondary runtime target and must remain compatible with
  1080p portrait and landscape orientations, touch controls, mobile performance
  budgets, mobile texture/import settings, and optional controller support.
- UI generated or configured by materialization/runtime systems must be
  scalable across the supported desktop and mobile resolutions.

Transferred BREACH scene code must not assume desktop-only input, editor-only
asset lookup, or desktop-only performance budgets in runtime paths.

## 14. Materialization Result

Materialization should return a JSON-compatible result:

```json
{
  "status": "PASS",
  "missionId": "VS01_HostageApartment",
  "pipelineVersion": "2.3",
  "action": "materialize_scene_preview",
  "artifacts": [
    "UserMissionSources/missions/VS01_HostageApartment/mission_layout.generated.json",
    "UserMissionSources/missions/VS01_HostageApartment/mission_entities.generated.json"
  ],
  "findings": [],
  "metrics": {
    "materializedDoorCount": 4,
    "materializedCoverCount": 12,
    "materializedActorCount": 6
  }
}
```

The action name may change during implementation, but the result must remain
machine-readable.
