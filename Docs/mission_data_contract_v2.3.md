# Mission Data Contract v2.3

Status: Active documentation contract
Version: 2.3
Project: `Breach Scenario Engine`
Scope: Technical contract between the mission compiler and Unity runtime

This document defines the JSON Schema for `mission_payload.generated.json`.
The compiler writes this file after successful template validation and before
layout generation.

## 1. JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Breach Scenario Engine Mission Payload v2.3",
  "type": "object",
  "additionalProperties": false,
  "required": ["header", "spatial", "logic", "roster", "objectives", "profileRefs"],
  "properties": {
    "header": {
      "type": "object",
      "additionalProperties": false,
      "required": ["schemaVersion", "pipelineVersion", "missionId", "initialSeed", "effectiveSeed"],
      "properties": {
        "schemaVersion": { "const": "bse.mission_payload.v2.3" },
        "pipelineVersion": { "const": "2.3" },
        "missionId": {
          "type": "string",
          "pattern": "^VS[0-9]{2}_[A-Za-z0-9]+(?:[A-Za-z0-9]+)*$"
        },
        "missionTitle": { "type": "string" },
        "initialSeed": { "type": "integer", "minimum": 0 },
        "effectiveSeed": { "type": "integer", "minimum": 0 },
        "layoutRevisionId": { "type": "string" }
      }
    },
    "spatial": {
      "type": "object",
      "additionalProperties": false,
      "required": ["bounds", "theme", "ppu", "bsp"],
      "properties": {
        "bounds": {
          "type": "array",
          "items": { "type": "integer", "minimum": 1 },
          "minItems": 2,
          "maxItems": 2
        },
        "theme": {
          "type": "string",
          "enum": ["urban_cqb", "stealth_facility", "residential"]
        },
        "ppu": { "type": "integer", "enum": [128, 256] },
        "bsp": {
          "type": "object",
          "additionalProperties": false,
          "required": ["minRoomSize", "maxRoomSize", "corridorWidth", "forceAdjacency"],
          "properties": {
            "minRoomSize": {
              "type": "array",
              "items": { "type": "integer", "minimum": 1 },
              "minItems": 2,
              "maxItems": 2
            },
            "maxRoomSize": {
              "type": "array",
              "items": { "type": "integer", "minimum": 1 },
              "minItems": 2,
              "maxItems": 2
            },
            "corridorWidth": { "type": "integer", "minimum": 1 },
            "forceAdjacency": { "type": "boolean" }
          }
        }
      }
    },
    "logic": {
      "type": "object",
      "additionalProperties": false,
      "required": ["noise", "navigation"],
      "properties": {
        "noise": {
          "type": "object",
          "additionalProperties": false,
          "required": ["threshold", "wallMultiplier", "doorPenalty"],
          "properties": {
            "threshold": { "type": "number", "minimum": 0, "maximum": 1 },
            "wallMultiplier": { "type": "number", "minimum": 0 },
            "doorPenalty": { "type": "number", "minimum": 0 }
          }
        },
        "navigation": {
          "type": "object",
          "additionalProperties": false,
          "required": ["strict"],
          "properties": {
            "strict": { "type": "boolean" }
          }
        },
        "tacticalDensity": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "minEnemiesPerOccupiedRoom": { "type": "integer", "minimum": 0 },
            "targetEnemiesPerOccupiedRoom": { "type": "integer", "minimum": 0 },
            "maxEnemiesPerRoom": { "type": "integer", "minimum": 0 },
            "maxEmptyRooms": { "type": "integer", "minimum": 0 },
            "minCoverPiecesPerRoom": { "type": "integer", "minimum": 0 },
            "minAlternateRoutes": { "type": "integer", "minimum": 0 },
            "minHearingOverlapPercentage": { "type": "number", "minimum": 0, "maximum": 100 }
          }
        }
      }
    },
    "roster": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["actorId", "type", "policy", "placementPolicy", "count"],
        "properties": {
          "actorId": { "type": "string" },
          "type": { "type": "string" },
          "policy": {
            "type": "string",
            "enum": ["FullAccess", "StaticGuard", "CanOpenDoors", "Immobilized"]
          },
          "placementPolicy": {
            "type": "string",
            "enum": ["EntryPointOnly", "PostLayout_TaggedRoom", "PostLayout_AnyRoom", "SecureRoomOnly"]
          },
          "count": { "type": "integer", "minimum": 0 }
        }
      }
    },
    "objectives": {
      "type": "object",
      "additionalProperties": false,
      "required": ["primary"],
      "properties": {
        "primary": {
          "type": "array",
          "minItems": 1,
          "items": { "$ref": "#/$defs/objective" }
        },
        "secondary": {
          "type": "array",
          "items": { "$ref": "#/$defs/objective" }
        }
      }
    },
    "profileRefs": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "tacticalThemeProfile",
        "performanceProfile",
        "renderProfile",
        "navigationPolicy",
        "tacticalDensityProfile",
        "addressablesCatalogProfile"
      ],
      "properties": {
        "tacticalThemeProfile": { "$ref": "#/$defs/repoPath" },
        "performanceProfile": { "$ref": "#/$defs/repoPath" },
        "renderProfile": { "$ref": "#/$defs/repoPath" },
        "navigationPolicy": { "$ref": "#/$defs/repoPath" },
        "tacticalDensityProfile": { "$ref": "#/$defs/repoPath" },
        "addressablesCatalogProfile": { "$ref": "#/$defs/repoPath" }
      }
    },
    "catalogRefs": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "enemyCatalog": { "$ref": "#/$defs/repoPath" },
        "environmentCatalog": { "$ref": "#/$defs/repoPath" },
        "objectiveCatalog": { "$ref": "#/$defs/repoPath" }
      }
    }
  },
  "$defs": {
    "repoPath": {
      "type": "string",
      "pattern": "^(Assets|UserMissionSources)/"
    },
    "objective": {
      "type": "object",
      "additionalProperties": false,
      "required": ["id", "type"],
      "properties": {
        "id": { "type": "string" },
        "type": { "type": "string" },
        "requiresLayoutGraph": { "type": "boolean" },
        "targetRoomTag": { "type": "string" },
        "optional": { "type": "boolean" }
      }
    }
  }
}
```

## 2. Unity DTO Requirements

When Unity parses this payload:

1. Use strict member handling.
2. Treat missing members as errors.
3. Treat unknown payload fields as errors.
4. Map `policy` to the runtime navigation policy enum.
5. Treat `effectiveSeed > 0` as authoritative only when the accepted manifest
   also reports `status == "PASS"`.
6. Validate theme, profile refs, and catalog refs before Step 6 begins.

## 3. Compiler Mapping

| Template Field | Payload Field |
|---|---|
| `schemaVersion` | derives `header.schemaVersion` |
| `missionId` | `header.missionId` |
| `missionTitle` | `header.missionTitle` |
| `generationMeta.initialSeed` | `header.initialSeed` |
| none | `header.effectiveSeed` |
| `spatialConstraints.worldBounds` | `spatial.bounds` |
| `spatialConstraints.tacticalTheme` | `spatial.theme` |
| `spatialConstraints.pixelsPerUnit` | `spatial.ppu` |
| `spatialConstraints.bspConstraints.forceRoomAdjacency` | `spatial.bsp.forceAdjacency` |
| `tacticalRules.noiseAlertThreshold` | `logic.noise.threshold` |
| `tacticalRules.strictNavigationPolicy` | `logic.navigation.strict` |
| `actorRoster[].navigationPolicy` | `roster[].policy` |

## 4. Seed Handling

- `initialSeed` is copied from the user template.
- `effectiveSeed` is `0` until Step 7 returns PASS.
- Replay must use `effectiveSeed` only when the manifest status is PASS.
- Final seed ownership lives in `generation_manifest.json`.

## 5. Profile and Catalog Paths

Profile and catalog paths must be repository-relative. v2.3 roots are:

- `Assets/Data/Mission/Profiles/`
- `Assets/Data/Mission/Catalogs/`
- `Assets/Data/Mission/MissionConfig/<missionId>/Profiles/`
- `Assets/Data/Mission/MissionConfig/<missionId>/Catalogs/`

The payload must not contain absolute local machine paths.

## 6. Validation Posture

Before writing payload:

- template validation must PASS
- payload schema validation must PASS
- generated payload must be deterministic for the same source template
- compile report must include warnings and normalized decisions as JSON

Before Step 6:

- profile references must resolve
- catalog references must resolve when present
- unsupported profile or catalog versions are blocking
