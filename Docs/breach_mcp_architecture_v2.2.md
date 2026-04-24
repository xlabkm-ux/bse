# Tactical Breach Mission Architecture v2.2

Document: Technical architecture specification  
System: `Breach Scenario Engine (BSE)`  
Technologies: `Unity 6`, `Burst Compiler`, `Unity Awaitable`, `Addressables`  
Version: 2.2

This document defines the runtime orchestration model for BSE v2.2. The
contract fixes a `Layout First` pipeline: geometry, graphs, and pathing are
materialized before live entities are placed.

## 1. Fundamental Order

Version 2.2 enforces inverted spawn order.

### Phase A: Preparation

Steps 1-4 prepare project data and runtime context.

1. Addressables catalog synchronization.
   - Verify tilesets for the selected `tacticalTheme`.
2. Scene initialization.
3. Scene bootstrap completion.
4. `McpPipelineContext` creation.

### Phase B: Geometry and Graphs

Step 6 runs before live entity placement and has `HIGH` priority.

1. BSP slicing.
   - Split physical space into playable rooms and corridors.
2. LayoutGraph materialization.
   - Build logical room and portal links.
3. Graph pre-calculation.
   - `VisibilityGraph`: static raycasting for line-of-sight.
   - `HearingGraph`: sound propagation through `acousticOcclusion`.
   - `CoverGraph`: angle and cover analysis.

### Phase C: Filling

Step 5 runs after the layout graph exists and has `MEDIUM` priority.

1. Character placement.
   - Place actors only on `LayoutGraph` room nodes.
2. Object placement.
   - Spawn objective items and mission props.
3. Navigation bake.
   - Update NavMesh after all placements are locked.

### Phase D: Verification

Step 7 is `CRITICAL`.

1. Navigation check.
   - Verify `OP_01` can physically reach `CIV_01`.
2. Tactical density check.
   - Reject stacked enemy placement or point overcrowding.

## 2. Acoustic Occlusion Mathematics

In BSE v2.2, sound attenuation is computed over the portal graph:

```text
Cost = sum(Distance_segment * multiplier_env) + sum(penalty_door)
```

If the resulting occlusion cost is lower than the enemy distance adjusted by
`noiseAlertThreshold`, an alert event is generated.

If the alert occurs on the first second of the mission, the verification gate
must emit `TB-AUD-003`.

## 3. Determinism Modes

The system supports two seed modes.

### Exploration Mode

- `initialSeed = 0`
- The generator may try alternative seeds until Step 7 returns `PASS`
- The final successful seed is written to `effectiveSeed`

### Replay Mode

- `initialSeed` is fixed by configuration
- The generator must reproduce the same `LayoutGraph`

## 4. Unity 6 Integration

### Unity Awaitable

The full pipeline runs through `async` / `await` and must not block the Unity
Editor main thread.

### Burst Compiler

`HearingGraph` and `VisibilityGraph` math runs in Jobs for throughput.

Target claim for the current design:

- approximately `0.2 ms` for `1000` rays

### Addressables

Themes load dynamically.

- If `urban_cqb` is missing from the catalog, generation fails with `TB-AST-001`.

## 5. Retry Policy

Seed incrementation is allowed only for these blocking errors:

- `TB-NAV-001` navigation deadlock
- `TB-PLC-001` room placement failure
- `TB-AUD-003` geometric noise leak

Retry is forbidden for syntax errors in the template.

