# Generation Manifest Contract v2.3

Status: Active documentation contract
Version: 2.3
Project: `Breach Scenario Engine`

This document defines `generation_manifest.json`, the accepted-generation
record for deterministic replay. In v2.3 the manifest is written only after
Step 7 returns PASS.

Incomplete, failed, blocked, and retrying runs are recorded in
`mission_state.json` and `verification_summary.json`, not in a failed manifest.

## 1. Ownership

`generation_manifest.json` is written by the generation pipeline after Step 7
PASS. It is not hand-authored.

The manifest owns:

- `effectiveSeed`
- `requestedSeed`
- `retrySeeds`
- `acceptedAttempt`
- `layoutRevisionId`
- accepted verification metrics
- generated artifact paths
- accepted profile and catalog references

The user template owns only the requested seed through
`generationMeta.initialSeed`.

## 2. Required Shape

```json
{
  "schemaVersion": "bse.generation_manifest.v2.3",
  "pipelineVersion": "2.3",
  "missionId": "VS01_HostageApartment",
  "status": "PASS",
  "requestedSeed": 428193,
  "effectiveSeed": 428193,
  "retrySeeds": [],
  "acceptedAttempt": 0,
  "layoutRevisionId": "layout_00000000",
  "lockOwner": "manage_mission",
  "profileRefs": {
    "tacticalThemeProfile": "Assets/Data/Mission/Profiles/TacticalThemeProfile.asset",
    "performanceProfile": "Assets/Data/Mission/Profiles/PerformanceProfile.asset",
    "renderProfile": "Assets/Data/Mission/Profiles/RenderProfile.asset",
    "navigationPolicy": "Assets/Data/Mission/Profiles/NavigationPolicy.asset",
    "tacticalDensityProfile": "Assets/Data/Mission/Profiles/TacticalDensityProfile.asset",
    "addressablesCatalogProfile": "Assets/Data/Mission/Profiles/AddressablesCatalogProfile.asset"
  },
  "catalogRefs": {
    "enemyCatalog": "Assets/Data/Mission/Catalogs/EnemyCatalog.asset",
    "environmentCatalog": "Assets/Data/Mission/Catalogs/EnvironmentCatalog.asset",
    "objectiveCatalog": "Assets/Data/Mission/Catalogs/ObjectiveCatalog.asset"
  },
  "artifacts": {
    "payload": "UserMissionSources/missions/VS01_HostageApartment/mission_payload.generated.json",
    "compileReport": "UserMissionSources/missions/VS01_HostageApartment/mission_compile_report.json",
    "layout": "UserMissionSources/missions/VS01_HostageApartment/mission_layout.generated.json",
    "entities": "UserMissionSources/missions/VS01_HostageApartment/mission_entities.generated.json",
    "verificationSummary": "UserMissionSources/missions/VS01_HostageApartment/verification_summary.json",
    "missionState": "UserMissionSources/missions/VS01_HostageApartment/mission_state.json"
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
      "unreachableCriticalNodes": 0,
      "reachableObjectives": 1,
      "unreachableObjectives": 0,
      "averageCoverPerRoom": 0,
      "hearingOverlapPercentage": 0,
      "alternateRoutes": 0,
      "chokepointPressure": 0,
      "objectiveRoomPressure": 0
    }
  }
}
```

## 3. Status Rules

`generation_manifest.json` represents an accepted generation. Therefore:

- manifest `status` must be `PASS`
- manifest `verification.status` must be `PASS`
- failed or blocked runs must not overwrite the accepted manifest
- failed or blocked runs must update `mission_state.json` and
  `verification_summary.json`

## 4. Seed Rules

1. `requestedSeed` is copied from `generationMeta.initialSeed`.
2. `effectiveSeed` is `0` or absent until Step 7 returns PASS.
3. `effectiveSeed` is non-zero in the manifest.
4. `effectiveSeed` is written exactly once for an accepted generation.
5. `retrySeeds` records every attempted deterministic retry seed.
6. `acceptedAttempt` is `0` for the initial seed, or the 1-based retry attempt
   that passed.
7. Replay uses `effectiveSeed` when manifest `status == "PASS"`.

Retry seed derivation:

`retrySeed = Hash32(requestedSeed, missionId, layoutAttemptIndex, failureCode, pipelineVersion)`

## 5. Lock Rules

The mission-scoped lock path is:

`UserMissionSources/missions/<missionId>/.generation.lock`

The writer must hold the mission lock before mutating:

- layout artifacts
- entity artifacts
- verification summary
- mission state
- generation manifest
- generated scene roots or MissionConfig assets when applicable

Concurrent writes to the same mission must fail with
`GENERATION_LOCK_CONFLICT`.

The lock must be released on normal completion and failed completion. Stale lock
cleanup must be explicit and diagnostic.

## 6. Mission State Shape

`mission_state.json` records non-accepted lifecycle state:

```json
{
  "missionId": "VS01_HostageApartment",
  "pipelineVersion": "2.3",
  "status": "VERIFYING",
  "currentStep": "verify",
  "startedAtUtc": "2026-04-25T00:00:00Z",
  "updatedAtUtc": "2026-04-25T00:00:00Z",
  "jobId": "",
  "lockOwner": "manage_mission",
  "layoutRevisionId": "layout_00000000",
  "lastFindingCode": ""
}
```

Allowed states:

- `IDLE`
- `VALIDATING`
- `COMPILED`
- `LAYOUT_GENERATED`
- `ENTITIES_PLACED`
- `VERIFYING`
- `RETRYING`
- `PASS`
- `FAILED`
- `BLOCKED`

`write_manifest` must check state compatibility before writing.

## 7. Artifact Rules

Paths are repository-relative. Generated artifacts may be ignored by git, but
their paths must remain stable across replay.

The manifest must not point to absolute local machine paths.

## 8. Verification Rules

The manifest copies the accepted verification summary. It must include enough
machine-readable data to prove:

- Step 6 ran before Step 5
- Step 5 used the current `layoutRevisionId`
- navigation checks passed
- tactical density checks passed
- render and performance budgets passed
- retryable findings are empty on the accepted attempt

