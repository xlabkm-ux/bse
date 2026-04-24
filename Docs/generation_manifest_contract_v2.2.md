# Generation Manifest Contract v2.2

Status: Full Release Specification  
Version: 2.2  
Project: `Breach Scenario Engine`

This document defines `generation_manifest.json`, the authoritative record for
deterministic replay and verification status.

## 1. Ownership

`generation_manifest.json` is written by the generation pipeline after Step 7.
It is not hand-authored.

The manifest owns:

- `effectiveSeed`
- `retrySeeds`
- `layoutRevisionId`
- `status`
- verification metrics
- generated artifact paths

The user template owns only the requested seed through
`generationMeta.initialSeed`.

## 2. Required Shape

```json
{
  "schemaVersion": "bse.generation_manifest.v2.2",
  "pipelineVersion": "2.2",
  "missionId": "VS01_HostageApartment",
  "status": "PASS",
  "requestedSeed": 428193,
  "effectiveSeed": 428193,
  "retrySeeds": [],
  "layoutRevisionId": "layout_00000000",
  "lockOwner": "bse-pipeline",
  "profileRefs": {
    "tacticalThemeProfile": "Assets/Data/Mission/Profiles/TacticalThemeProfile.asset",
    "performanceProfile": "Assets/Data/Mission/Profiles/PerformanceProfile.asset",
    "renderProfile": "Assets/Data/Mission/Profiles/RenderProfile.asset",
    "navigationPolicy": "Assets/Data/Mission/Profiles/NavigationPolicy.asset",
    "tacticalDensityProfile": "Assets/Data/Mission/Profiles/TacticalDensityProfile.asset",
    "addressablesCatalogProfile": "Assets/Data/Mission/Profiles/AddressablesCatalogProfile.asset"
  },
  "artifacts": {
    "payload": "UserMissionSources/missions/VS01_HostageApartment/mission_payload.generated.json",
    "compileReport": "UserMissionSources/missions/VS01_HostageApartment/mission_compile_report.json",
    "verificationSummary": "UserMissionSources/missions/VS01_HostageApartment/verification_summary.json"
  },
  "verification": {
    "status": "PASS",
    "findings": [],
    "metrics": {
      "enemyCount": 0,
      "roomCount": 0,
      "emptyRoomCount": 0,
      "light2DCount": 0,
      "activeHearingChecks": 0,
      "visibilityRayCount": 0,
      "unreachableCriticalNodes": 0
    }
  }
}
```

## 3. Status Values

- `PENDING`
- `PASS`
- `FAILED`
- `BLOCKED`

## 4. Seed Rules

1. `requestedSeed` is copied from `generationMeta.initialSeed`.
2. `effectiveSeed` is `0` until Step 7 returns `PASS`.
3. `effectiveSeed` is written exactly once for an accepted generation.
4. `retrySeeds` records every attempted deterministic retry seed.
5. Replay uses `effectiveSeed` when `status == "PASS"`.

## 5. Lock Rules

The writer must hold generation locks for:

- mission id
- MissionConfig asset
- generated scene root
- manifest file

Concurrent writes to the same mission must fail with
`GENERATION_LOCK_CONFLICT`.

## 6. Artifact Rules

Paths are repository-relative. Generated artifacts may be ignored by git, but
their paths must remain stable across replay.

The manifest must not point to absolute local machine paths.
