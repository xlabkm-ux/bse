# Mission Authoring Contract v2.3

Status: Active documentation contract
Version: 2.3
Project: `Breach Scenario Engine`

This document separates fields authored by mission designers from fields
produced by the compiler, Unity profiles, catalogs, verification, and manifest
pipeline.

## 1. Authoring Principle

A normal mission is authored as one YAML file:

`UserMissionSources/missions/<missionId>/mission_design.template.yaml`

Designers should not edit generated artifacts during normal mission authoring.

## 2. User-Authored Fields

The template may contain:

- `schemaVersion`
- `missionId`
- `missionTitle`
- `generationMeta.initialSeed`
- `generationMeta.generationTimeout`
- `generationMeta.maxRetries`
- `spatialConstraints`
- `tacticalRules`
- `actorRoster`
- `objectives`
- optional advanced `profileRefs`
- optional advanced `catalogRefs`

New v2.3 templates must not author `generationMeta.effectiveSeed`.
Legacy templates that contain `effectiveSeed: 0` may be accepted during
migration, but the compiler must ignore it as a replay authority.

## 3. Compiler-Derived Fields

The compiler derives:

- `MissionDraft`
- `mission_payload.generated.json`
- normalized actor counts
- normalized objective references
- runtime payload section names
- default profile references
- default catalog references
- compile report findings and warnings

## 4. Pipeline-Owned Fields

The pipeline owns:

- `effectiveSeed`
- `retrySeeds`
- `layoutRevisionId`
- `mission_state.json`
- `.generation.lock`
- generated graph revisions
- generated scene roots
- verification metrics
- `generation_manifest.json`

Designers must not hand-edit these values after generation.

## 5. Profile and Catalog Ownership

v2.3 uses the existing project data root as the authoritative root.

Global defaults:

`Assets/Data/Mission/Profiles/`

`Assets/Data/Mission/Catalogs/`

Per-mission overrides:

`Assets/Data/Mission/MissionConfig/<missionId>/Profiles/`

`Assets/Data/Mission/MissionConfig/<missionId>/Catalogs/`

The compiler loads global defaults first, then applies per-mission overrides
when present. `Assets/TacticalBreach/Profiles/` and
`Assets/TacticalBreach/Catalogs/` are not active roots in v2.3.

Required profile types:

- `TacticalThemeProfile.asset`
- `PerformanceProfile.asset`
- `RenderProfile.asset`
- `NavigationPolicy.asset`
- `TacticalDensityProfile.asset`
- `AddressablesCatalogProfile.asset`

Target catalog types:

- `EnemyCatalog.asset`
- `EnvironmentCatalog.asset`
- `ObjectiveCatalog.asset`

Template references to profiles or catalogs must resolve before Step 4.

## 6. Mission ID Rules

New mission ids must use:

`VS##_ShortMissionName`

Example:

`VS01_HostageApartment`

The mission id must match the containing mission folder name.

## 7. Validation Findings

v2.3 validation findings use the `TPL_*` family for authoring and template
gates:

- `TPL_FILE_MISSING`
- `TPL_SCHEMA_INVALID`
- `TPL_UNKNOWN_FIELD`
- `TPL_RANGE_INVALID`
- `TPL_PROFILE_REF_MISSING`
- `TPL_OBJECTIVE_INVALID`
- `TPL_ACTOR_ROSTER_INVALID`
- `TPL_CLARIFICATION_REQUIRED`

These findings block compile and are not retryable layout failures.

## 8. Minimal Valid Template

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

objectives:
  primary:
    - id: "OBJ_MAIN"
      type: "RescueHostage"
      requiresLayoutGraph: true
      targetRoomTag: "security_vault"
```

## 9. Advanced Overrides

Normal missions do not need to author explicit `profileRefs` or `catalogRefs`.
When present, they must be repository-relative paths under the v2.3 roots:

```yaml
profileRefs:
  tacticalThemeProfile: "Assets/Data/Mission/Profiles/TacticalThemeProfile.asset"

catalogRefs:
  enemyCatalog: "Assets/Data/Mission/Catalogs/EnemyCatalog.asset"
```

Missing profile or catalog references fail validation before payload compile.

