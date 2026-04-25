# Tactical Breach Mission Design Template v2.3

Status: Active documentation contract
Version: 2.3
Project: `TACTICAL BREACH: Mission Architect`
Environment: `Unity 6000.4.3f1 + Breach Scenario Engine MCP + Codex AI`

This document captures the mission template contract used by the procedural
mission pipeline. It is the reference for authoring and validating mission
design templates in `UserMissionSources/`.

The generated payload emitted from this template must satisfy
[mission_data_contract_v2.3.md](mission_data_contract_v2.3.md).

Authoring ownership is defined in
[mission_authoring_contract_v2.3.md](mission_authoring_contract_v2.3.md).
Pipeline outputs are defined in
[mission_pipeline_contract_v2.3.md](mission_pipeline_contract_v2.3.md) and
[generation_manifest_contract_v2.3.md](generation_manifest_contract_v2.3.md).

User-facing authoring remains a single template file. Codex and the compiler
may split it into internal sections, but the designer should not be asked to
edit multiple small files for a normal mission.

Canonical mission IDs use the `VS##_ShortMissionName` convention.

## 1. Generation Meta

Controls the procedural generation lifecycle.

`initialSeed` is authored. `effectiveSeed` is never authored in new v2.3
templates; it is written by the pipeline only after verification passes.

```yaml
schemaVersion: "tb.mission_template.v2.3"
missionId: "VS01_HostageApartment"
missionTitle: "Hostage Apartment"

generationMeta:
  initialSeed: 428193
  generationTimeout: 45
  maxRetries: 5
```

## 2. Spatial Constraints

Defines the world bounds and BSP room-splitting rules.

```yaml
spatialConstraints:
  worldBounds: [64, 64]
  pixelsPerUnit: 128
  tacticalTheme: "urban_cqb"
  bspConstraints:
    minRoomSize: [4, 4]
    maxRoomSize: [12, 12]
    corridorWidth: 2
    forceRoomAdjacency: true
```

## 3. Tactical and Acoustic Rules

Defines mission intent for navigation and acoustic behavior. Profile assets
remain authoritative for validation thresholds.

```yaml
tacticalRules:
  noiseAlertThreshold: 0.15
  strictNavigationPolicy: true
  enforcePostLayoutPlacement: true
  acousticOcclusion:
    wallMultiplier: 2.5
    doorPenalty: 1.2
```

## 4. Actor Roster

Defines actor access and placement rules. Post-layout placement policies require
Step 6 output before Step 5 can run.

```yaml
actorRoster:
  - id: "OP_01"
    type: "Operative"
    countRange: [2, 4]
    navigationPolicy: "FullAccess"
    placementPolicy: "EntryPointOnly"
  - id: "EN_01"
    type: "Sentry"
    countRange: [3, 6]
    navigationPolicy: "StaticGuard"
    placementPolicy: "PostLayout_TaggedRoom"
  - id: "EN_02"
    type: "Roamer"
    countRange: [1, 2]
    navigationPolicy: "CanOpenDoors"
    placementPolicy: "PostLayout_AnyRoom"
  - id: "CIV_01"
    type: "Hostage"
    countRange: [1, 2]
    navigationPolicy: "Immobilized"
    placementPolicy: "SecureRoomOnly"
```

## 5. Objectives

Primary and secondary mission goals.

```yaml
objectives:
  primary:
    - id: "OBJ_MAIN"
      type: "RescueHostage"
      requiresLayoutGraph: true
      targetRoomTag: "security_vault"
  secondary:
    - id: "OBJ_DATA"
      type: "CollectItem"
      optional: true
```

Objectives that require a layout graph must not be resolved before Step 6.

## 6. Advanced References

Normal templates may rely on default profiles and catalogs. Advanced missions
may override them with repository-relative paths under the v2.3 roots.

```yaml
profileRefs:
  tacticalThemeProfile: "Assets/Data/Mission/Profiles/TacticalThemeProfile.asset"
  performanceProfile: "Assets/Data/Mission/Profiles/PerformanceProfile.asset"
  renderProfile: "Assets/Data/Mission/Profiles/RenderProfile.asset"
  navigationPolicy: "Assets/Data/Mission/Profiles/NavigationPolicy.asset"
  tacticalDensityProfile: "Assets/Data/Mission/Profiles/TacticalDensityProfile.asset"
  addressablesCatalogProfile: "Assets/Data/Mission/Profiles/AddressablesCatalogProfile.asset"

catalogRefs:
  enemyCatalog: "Assets/Data/Mission/Catalogs/EnemyCatalog.asset"
  environmentCatalog: "Assets/Data/Mission/Catalogs/EnvironmentCatalog.asset"
  objectiveCatalog: "Assets/Data/Mission/Catalogs/ObjectiveCatalog.asset"
```

Profile and catalog references are validation inputs. Missing references are
blocking template errors, not retryable layout failures.

## 7. Blocking Errors

Template validation must return structured JSON findings for:

- `TPL_FILE_MISSING`
- `TPL_SCHEMA_INVALID`
- `TPL_UNKNOWN_FIELD`
- `TPL_RANGE_INVALID`
- `TPL_PROFILE_REF_MISSING`
- `TPL_OBJECTIVE_INVALID`
- `TPL_ACTOR_ROSTER_INVALID`
- `TPL_CLARIFICATION_REQUIRED`

Pipeline order and replay errors use their own runtime code families, such as:

- `ORDER_VIOLATION_NO_LAYOUT_GRAPH`
- `ORDER_VIOLATION_STALE_LAYOUT`
- `SEED_EFFECTIVE_WRITTEN_BEFORE_PASS`
- `GENERATION_LOCK_CONFLICT`

## 8. Parser Constraints

The current compiler intentionally uses a narrow, line-oriented YAML subset
parser instead of a full YAML engine. Supported templates keep to:

- two-space indentation
- scalar key/value pairs
- inline integer arrays such as `[64, 64]`
- nested mappings for the known mission sections

Unsupported YAML features include tabs, anchors, aliases, multiline scalars,
and arbitrary sequence syntax outside the mission roster/objective patterns.
Templates that need those features are out of scope for the current v2.3
pipeline.

## 9. Complete Example

```yaml
schemaVersion: "tb.mission_template.v2.3"
missionId: "VS01_HostageApartment"
missionTitle: "Hostage Apartment"

generationMeta:
  initialSeed: 428193
  generationTimeout: 45
  maxRetries: 5

spatialConstraints:
  worldBounds: [64, 64]
  pixelsPerUnit: 128
  tacticalTheme: "urban_cqb"
  bspConstraints:
    minRoomSize: [4, 4]
    maxRoomSize: [12, 12]
    corridorWidth: 2
    forceRoomAdjacency: true

tacticalRules:
  noiseAlertThreshold: 0.15
  strictNavigationPolicy: true
  enforcePostLayoutPlacement: true
  acousticOcclusion:
    wallMultiplier: 2.5
    doorPenalty: 1.2

actorRoster:
  - id: "OP_01"
    type: "Operative"
    countRange: [2, 4]
    navigationPolicy: "FullAccess"
    placementPolicy: "EntryPointOnly"
  - id: "EN_01"
    type: "Sentry"
    countRange: [3, 6]
    navigationPolicy: "StaticGuard"
    placementPolicy: "PostLayout_TaggedRoom"
  - id: "CIV_01"
    type: "Hostage"
    countRange: [1, 2]
    navigationPolicy: "Immobilized"
    placementPolicy: "SecureRoomOnly"

objectives:
  primary:
  - id: "OBJ_MAIN"
      type: "RescueHostage"
      requiresLayoutGraph: true
      targetRoomTag: "security_vault"
```

## 10. Artifact Boundary

The template never owns:

- `effectiveSeed`
- `retrySeeds`
- `layoutRevisionId`
- generated room, portal, cover, visibility, or hearing graph ids
- generated actor `roomId` or `navNodeId`
- `generation_manifest.json`
- `mission_state.json`
- `.generation.lock`
