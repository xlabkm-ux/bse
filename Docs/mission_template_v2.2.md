# Tactical Breach Mission Design Template v2.2

Status: Full Release Specification  
Version: 2.2  
Project: `TACTICAL BREACH: Mission Architect`  
Environment: `Unity 6 + Breach Scenario Engine MCP + Codex AI`

This document captures the mission template contract used by the procedural
mission pipeline. It is the reference for authoring and validating mission
design templates in `UserMissionSources/`.

The generated payload emitted from this template must satisfy
[mission_data_contract_v2.2.md](mission_data_contract_v2.2.md).

Authoring ownership is defined in
[mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md).
Pipeline outputs are defined in
[mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md) and
[generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md).

User-facing authoring remains a single template file. Codex and the compiler
may split it into internal sections, but the designer should not be asked to
edit multiple small files for a normal mission.

Canonical mission IDs use the `VS##_ShortMissionName` convention.

## 1. Generation Meta

Controls the procedural generation lifecycle.

`initialSeed` is authored. `effectiveSeed` is written by the pipeline after
verification and must remain `0` in ungenerated templates.

```yaml
schemaVersion: "tb.mission_template.v2.2"
missionId: "VS01_HostageApartment"
generationMeta:
  initialSeed: 0
  effectiveSeed: 0
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

Defines the interaction model between the player and the generated layout.

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

Defines actor access and placement rules.

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

## 6. Blocking Errors

Any of the following conditions must stop generation or trigger a seed retry.

- `TB-TPL-001`: template version mismatch
- `TB-NAV-001`: navigation deadlock
- `TB-AUD-003`: noise leak
- `TB-PLC-002`: ordering violation
- `TB-DTR-001`: determinism error

## 7. Profile and Manifest Ownership

Reusable tuning lives in profile assets, not in the authoring template.

Global profile defaults:

`Assets/Data/Mission/Profiles/`

Per-mission overrides:

`Assets/Data/Mission/MissionConfig/<missionId>/Profiles/`

The pipeline owns `generation_manifest.json`, `retrySeeds`, and
`layoutRevisionId`.
