# Mission Data Contract v2.2

Status: Full Release Specification  
Version: 2.2  
Project: `Breach Scenario Engine`  
Scope: Final technical contract between the Codex generator and the Unity runtime

This document defines the JSON Schema for `mission_payload.generated.json`.
It is the authoritative payload contract consumed by the Unity-side DTO layer.

## 1. JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Tactical Breach Mission Payload",
  "version": "2.2",
  "type": "object",
  "required": ["header", "spatial", "logic", "roster"],
  "properties": {
    "header": {
      "type": "object",
      "required": ["missionId", "initialSeed"],
      "properties": {
        "missionId": { "type": "string" },
        "initialSeed": { "type": "integer" },
        "effectiveSeed": { "type": "integer", "default": 0 }
      }
    },
    "spatial": {
      "type": "object",
      "required": ["bounds", "theme", "ppu"],
      "properties": {
        "bounds": {
          "type": "array",
          "items": { "type": "integer" },
          "minItems": 2
        },
        "theme": {
          "type": "string",
          "enum": ["urban_cqb", "stealth_facility", "residential"]
        },
        "ppu": { "type": "integer", "enum": [128, 256] },
        "bsp": {
          "type": "object",
          "properties": {
            "minRoomSize": { "type": "array", "items": { "type": "integer" } },
            "forceAdjacency": { "type": "boolean" }
          }
        }
      }
    },
    "logic": {
      "type": "object",
      "properties": {
        "noise": {
          "type": "object",
          "properties": {
            "threshold": { "type": "number", "minimum": 0, "maximum": 1 },
            "wallMultiplier": { "type": "number" }
          }
        },
        "navigation": {
          "type": "object",
          "properties": {
            "strict": { "type": "boolean", "default": true }
          }
        }
      }
    },
    "roster": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["actorId", "policy"],
        "properties": {
          "actorId": { "type": "string" },
          "type": { "type": "string" },
          "policy": {
            "type": "string",
            "enum": ["FullAccess", "StaticGuard", "CanOpenDoors", "Immobilized"]
          },
          "count": { "type": "integer" }
        }
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
5. Validate `theme` through `Addressables.LoadResourceLocationsAsync` before Step 6 begins.

## 3. Contract Notes

### Seed handling

- `initialSeed` is the generator input seed.
- `effectiveSeed` is the resolved seed after a successful pass.
- Replay mode must use `effectiveSeed` if it is greater than zero.
- Final seed ownership lives in `GenerationManifest`; the payload reflects the
  verified result after PASS, not an unvalidated template claim.

### Naming alignment

- New mission payloads should use the `VS##_ShortMissionName` convention.
- Legacy `TBM_####_*` identifiers are allowed only for migration of archived
  content.

### Version alignment

The payload contract is versioned at `2.2` to match the current mission
template and architecture documents in this repository.

### Validation posture

- Missing members are treated as errors.
- Unknown payload fields are treated as errors.
- Theme lookup failure must stop layout generation before geometry work starts.
