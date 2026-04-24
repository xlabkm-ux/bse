# Mission Data Contract v2.2

Status: Full Release Specification  
Version: 2.2  
Project: `Breach Scenario Engine`  
Scope: Final technical contract between the mission compiler and Unity runtime

This document defines the JSON Schema for `mission_payload.generated.json`.
It is the authoritative payload contract consumed by the Unity-side DTO layer.

## 1. JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Breach Scenario Engine Mission Payload",
  "type": "object",
  "additionalProperties": false,
  "required": ["header", "spatial", "logic", "roster", "objectives", "profileRefs"],
  "properties": {
    "header": {
      "type": "object",
      "additionalProperties": false,
      "required": ["schemaVersion", "pipelineVersion", "missionId", "initialSeed", "effectiveSeed"],
      "properties": {
        "schemaVersion": { "const": "bse.mission_payload.v2.2" },
        "pipelineVersion": { "const": "2.2" },
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
        "tacticalThemeProfile": { "type": "string" },
        "performanceProfile": { "type": "string" },
        "renderProfile": { "type": "string" },
        "navigationPolicy": { "type": "string" },
        "tacticalDensityProfile": { "type": "string" },
        "addressablesCatalogProfile": { "type": "string" }
      }
    }
  },
  "$defs": {
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

When Unity 6 parses this payload:

1. Use `Newtonsoft.Json`.
2. Set `MissingMemberHandling.Error`.
3. Map `policy` to the `NavigationPolicy` enum.
4. Treat `effectiveSeed > 0` as authoritative and ignore `initialSeed` for replay.
5. Validate `theme` and all `profileRefs` through Addressables or AssetDatabase before Step 6 begins.

## 3. Compiler Mapping

The user template does not need to match payload section names directly.

| Template Field | Payload Field |
|---|---|
| `schemaVersion` | `header.schemaVersion` |
| `missionId` | `header.missionId` |
| `missionTitle` | `header.missionTitle` |
| `generationMeta.initialSeed` | `header.initialSeed` |
| `generationMeta.effectiveSeed` | `header.effectiveSeed` |
| `spatialConstraints.worldBounds` | `spatial.bounds` |
| `spatialConstraints.tacticalTheme` | `spatial.theme` |
| `spatialConstraints.pixelsPerUnit` | `spatial.ppu` |
| `spatialConstraints.bspConstraints.forceRoomAdjacency` | `spatial.bsp.forceAdjacency` |
| `tacticalRules.noiseAlertThreshold` | `logic.noise.threshold` |
| `tacticalRules.strictNavigationPolicy` | `logic.navigation.strict` |
| `actorRoster[].navigationPolicy` | `roster[].policy` |

## 4. Contract Notes

### Seed handling

- `initialSeed` is the generator input seed from the user template.
- `effectiveSeed` is `0` until Step 7 returns `PASS`.
- Replay mode must use `effectiveSeed` if it is greater than zero.
- Final seed ownership lives in `generation_manifest.json`.

### Naming alignment

- New mission payloads must use the `VS##_ShortMissionName` convention.
- Payload identifiers must match the mission folder name.

### Validation posture

- Missing members are treated as errors.
- Unknown payload fields are treated as errors.
- Theme/profile lookup failure must stop layout generation before geometry work starts.
