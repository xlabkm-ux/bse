# Mission Authoring Contract v2.2

Status: Full Release Specification  
Version: 2.2  
Project: `Breach Scenario Engine`

This document separates fields authored by designers from fields produced by
the compiler, Unity profiles, and verification pipeline.

## 1. Authoring Principle

A normal mission is authored as one YAML file:

`UserMissionSources/missions/<missionId>/mission_design.template.yaml`

Designers should not edit split compiler artifacts during normal mission
authoring.

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

## 3. Compiler-Derived Fields

The compiler derives:

- `MissionDraft`
- `mission_payload.generated.json`
- normalized actor counts
- normalized objective references
- runtime payload section names
- profile references
- compile report findings

## 4. Pipeline-Owned Fields

The pipeline owns:

- `generationMeta.effectiveSeed`
- `retrySeeds`
- `layoutRevisionId`
- generated graph revisions
- generated scene roots
- verification metrics
- `generation_manifest.json`

Designers must not hand-edit these values after generation.

## 5. Profile-Owned Fields

Reusable tuning and platform limits live in profile assets.

Global defaults:

`Assets/Data/Mission/Profiles/`

Per-mission overrides:

`Assets/Data/Mission/MissionConfig/<missionId>/Profiles/`

Required profile types:

- `TacticalThemeProfile.asset`
- `PerformanceProfile.asset`
- `RenderProfile.asset`
- `NavigationPolicy.asset`
- `TacticalDensityProfile.asset`
- `AddressablesCatalogProfile.asset`

The compiler loads global defaults first, then applies per-mission overrides
when present.

## 6. Mission ID Rules

New mission ids must use:

`VS##_ShortMissionName`

Example:

`VS01_HostageApartment`

New authoring templates must use this convention.

## 7. Minimal Valid Template

```yaml
schemaVersion: "tb.mission_template.v2.2"
missionId: "VS01_HostageApartment"
missionTitle: "Hostage Apartment"

generationMeta:
  initialSeed: 428193
  effectiveSeed: 0
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
