# Breach Scenario Engine MCP Server Backlog

Date: 2026-04-06
Project: `Breach Scenario Engine`
Server: `BreachScenarioEngine.Mcp.Server`
Bridge target: Unity Editor + `Library/BreachMcpBridge`

## Goal

Convert the current Breach Scenario Engine MCP bridge/server into a stable Unity-native automation layer that lets the Codex agent run gameplay development and verification without manual Editor clicking.

Legacy source notes are archived in `Archive/2026-04-06/`.

It remains aligned with the active Unity verification contract and canonical tools list:

- [breach_mcp_verification_contract.md](breach_mcp_verification_contract.md)
- [canonical_tools.md](canonical_tools.md)

The current runtime tool inventory is documented in [runtime_tools.md](runtime_tools.md).

## Priority Model

- `P0` critical blocker for autonomous verification or reliable day-to-day use
- `P1` high value, needed for the full verification workflow
- `P2` important productivity and robustness work
- `P3` useful polish or future-facing work

## Complexity Model

- `S` small
- `M` medium
- `L` large
- `XL` very large / cross-cutting

## Current State Summary

Already partially or fully present:

- basic editor connection
- live bridge capability probe
- deterministic test job finalization
- structured scene/prefab reference validation
- play mode lifecycle control
- contract-safe autonomous input injection
- structured component serialization read/write
- scene-object method invocation
- localization tables discovery
- localization key listing and resolution
- build profile inspection and switching
- quality level switching
- profiler counters verification
- scene/object/script mutations
- console reading
- camera screenshots
- test execution primitives

Confirmed weak areas:

- no controlled save corruption workflow for resilience testing
- tool naming/history drift between older project docs and current Unity-style `manage_*` contract
- tool naming/history drift between older project docs and the canonical tools list

## Backlog

## P0 - Verification Blockers

### BSE-001 - Runtime tool capability probe

- Priority: `P0`
- Complexity: `M`
- Status: completed
- Goal: add live `tools.list`/capability probe from the bridge, not just server-side declarations
- Why: Codex must know what is actually callable in the connected Unity instance before every step
- Deliverables:
  - capability endpoint or resource with `tool`, `action`, `supported`, `notes`
  - version/build hash of active bridge package
  - readiness flags for optional groups like profiler/localization/tests
- Dependencies: none
- Unlocks:
  - reliable preflight for all steps

### BSE-002 - Deterministic test job finalization

- Priority: `P0`
- Complexity: `M`
- Status: completed
- Goal: make `get_test_job` return terminal structured results reliably
- Why: current behavior can stall or return stale/running snapshots
- Deliverables:
  - terminal statuses: `completed`, `failed`, `canceled`, `timeout`
  - totals: passed/failed/skipped/duration
  - failed tests with `fixture`, `message`, `stacktrace`
  - polling semantics documented and stable
- Dependencies:
  - `run_tests`
- Unlocks:
  - the test finalization workflow

### BSE-003 - Scene reference validation action

- Priority: `P0`
- Complexity: `M`
- Status: completed
- Goal: implement `manage_scene(action="validate_references")`
- Why: the scene validation workflow cannot be closed safely by screenshot/manual inspection alone
- Deliverables:
  - detection of missing scripts
  - detection of missing object references
  - detection of broken prefab instance links
  - structured findings payload
- Dependencies:
  - scene traversal and serialized component inspection
- Unlocks:
  - scene and prefab validation workflows

### BSE-004 - Prefab reference validation action

- Priority: `P0`
- Complexity: `M`
- Status: completed
- Goal: implement `manage_prefabs(action="validate_references")`
- Why: the prefab validation workflow needs prefab integrity, not only scene integrity
- Deliverables:
  - headless prefab load
  - broken reference scan
  - missing script scan
  - structured report
- Dependencies:
  - prefab inspection support
- Unlocks:
  - scene and prefab validation workflows

### BSE-005 - Play mode lifecycle control

- Priority: `P0`
- Complexity: `M`
- Status: completed
- Goal: add `manage_editor(action="play_mode")` with `enter`, `exit`, `status`
- Why: play scenario sweeps must be controllable from Codex without manual clicks
- Deliverables:
  - async job-based enter/exit
  - play state polling
  - timeout and domain reload recovery
- Dependencies:
  - editor readiness checks
- Unlocks:
  - play scenario workflows

### BSE-006 - Input injection for gameplay verification

- Priority: `P0`
- Complexity: `L`
- Status: completed
- Goal: add `manage_input(action="send")`
- Why: without input synthesis, Codex cannot fully drive combat/hostage/readability scenarios
- Deliverables:
  - key press / key hold
  - mouse position + mouse button events
  - frame-safe dispatch in Play Mode
  - result payload with sent events and frame window
- Dependencies:
  - play mode lifecycle control
- Unlocks:
  - play and input workflows

## P1 - Full Verification Coverage

### BSE-007 - Serialized component read/write actions

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: add `manage_components(action="get_serialized")` and `manage_components(action="set_serialized")`
- Why: scenario verification needs safe, structured state introspection and targeted state injection
- Deliverables:
  - primitive field reads
  - object reference reads
  - missing reference reporting
  - controlled writes to serialized fields
- Dependencies:
  - component inspection layer
- Unlocks:
  - serialized state workflows

### BSE-008 - Method invocation on scene objects

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: implement `manage_gameobject(action="invoke_method")`
- Why: test scenarios need to call methods like `SaveNow` or project-specific verification hooks
- Deliverables:
  - public method invocation
  - simple argument support
  - structured return/error payload
- Dependencies:
  - object lookup and reflection helper
- Unlocks:
  - gameplay invocation workflows

### BSE-009 - Localization tables resource

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: add `breachmcp://localization/tables`
- Why: localization coverage must be checked via Unity data, not by parsing arbitrary assets blindly
- Deliverables:
  - table list
  - locale list
  - per-table entry counts
  - missing locale counts
- Dependencies:
  - Localization package presence detection
- Unlocks:
  - the localization workflow

### BSE-010 - Localization key listing and resolving

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: add `manage_asset(action="list_localization_keys")` and `manage_asset(action="resolve_localization_keys")`
- Why: Codex needs deterministic key-coverage checks for `ru` and `en`
- Deliverables:
  - list keys by table
  - resolve values by locale
  - explicit `missing` and `empty` lists
- Dependencies:
  - `BSE-009`
- Unlocks:
  - the localization workflow

### BSE-011 - Build profile inspection and switching

- Priority: `P1`
- Complexity: `L`
- Status: completed
- Goal: implement `manage_build(action="profiles")`
- Why: Unity 6 verification must switch between Windows/Android oriented profiles cleanly
- Deliverables:
  - list profiles
  - get active profile
  - set active profile
  - report build target/platform info
- Dependencies:
  - build settings/build profile editor API integration
- Unlocks:
  - platform workflows

### BSE-012 - Quality level switching for visual verification

- Priority: `P1`
- Complexity: `S`
- Status: completed
- Goal: implement `manage_graphics(action="set_quality_level")`
- Why: runtime readability/perf checks depend on `PC_Default`, `Android_Default`, `Android_Low`
- Deliverables:
  - switch quality level by name
  - report active level after switch
- Dependencies:
  - none
- Unlocks:
  - platform workflows

### BSE-013 - Profiler counters verification pack

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: harden `manage_profiler(action="get_counters")` and `get_frame_timing`
- Why: Android/Windows verification needs repeatable performance snapshots
- Deliverables:
  - render counters
  - memory counters
  - frame timing summary
  - stable counter naming in payload
- Dependencies:
  - profiler integration
- Unlocks:
  - profiler and release workflows

### BSE-014 - Controlled save diagnostics file workflow

- Priority: `P1`
- Complexity: `M`
- Status: completed
- Goal: add controlled project-local text read/write actions for save fault injection
- Why: save resilience must be tested without giving MCP unrestricted OS filesystem access
- Deliverables:
  - `read_text_file`
  - `write_text_file`
  - path allowlist such as `Library/McpDiagnostics`
  - bridge-side validation and clear errors on denied paths
- Dependencies:
  - agreed runtime save-path override in game code
- Unlocks:
  - the save/load resilience workflow

## P2 - Robustness and Developer Experience

### BSE-015 - Bridge watchdog and recovery diagnostics

- Priority: `P2`
- Complexity: `L`
- Status: completed
- Goal: add heartbeat, queue timeout diagnostics, and stale bridge recovery guidance
- Why: current failures can look like random hangs or silent no-op behavior
- Deliverables:
  - heartbeat timestamp
  - queue depth
  - stuck command diagnostics
  - reconnect / restart recommendation payload
- Dependencies:
  - none
- Improves:
  - all steps

### BSE-016 - Canonical contract cleanup across docs and server help

- Priority: `P2`
- Complexity: `M`
- Status: completed
- Goal: align old project docs/tool names with the canonical tools list
- Why: active documentation must not leak legacy aliases
- Deliverables:
  - server help output
  - docs update guidance
  - alias/deprecation map if needed
- Dependencies:
  - confirmation of canonical naming
- Improves:
  - onboarding, prompt reliability, lower confusion

### BSE-017 - Structured operation audit trail

- Priority: `P2`
- Complexity: `M`
- Status: completed
- Goal: log each MCP operation with timestamp, active scene, tool, action, outcome
- Why: helpful for debugging bridge failures and reproducing verification runs
- Deliverables:
  - rolling editor-side log
  - last command summary in diagnostic resource
- Dependencies:
  - none

### BSE-018 - Screenshot artifact indexing

- Priority: `P2`
- Complexity: `S`
- Status: completed
- Goal: make screenshots easy to retrieve by step/scenario
- Why: verification should leave inspectable artifacts
- Deliverables:
  - artifact naming convention
  - last screenshot index or resource
- Dependencies:
  - `manage_camera(action="screenshot")`

## P3 - Future-facing Extensions

### BSE-019 - Graph inspection parity

- Priority: `P3`
- Complexity: `XL`
- Status: completed
- Goal: restore safe graph-level inspection/editing for Visual Scripting
- Why: not required to close the current verification workflow, but needed for full hybrid workflow parity
- Dependencies:
  - graph serialization/editor API work

### BSE-020 - Scenario macros / reusable verification recipes

- Priority: `P3`
- Complexity: `L`
- Status: completed
- Goal: allow named scenario recipes composed from existing tools
- Why: reduce prompt size and repeatable regression effort
- Dependencies:
  - play mode, input, screenshot, profiler, test result stability

### BSE-021 - Build smoke automation

- Priority: `P3`
- Complexity: `L`
- Status: completed
- Goal: add compile-only or lightweight build smoke checks for Windows/Android
- Why: useful before release candidate gating
- Dependencies:
  - build profile switching

## Recommended Delivery Waves

### Wave 1 - Unblock autonomous verification

- `BSE-001`
- `BSE-002`
- `BSE-003`
- `BSE-004`
- `BSE-005`
- `BSE-006`

Outcome:

- Codex can reliably preflight, enter Play Mode, validate scene/prefab refs, run tests, and drive gameplay scenarios.

### Wave 2 - Close verification end-to-end

- `BSE-007`
- `BSE-008`
- `BSE-009`
- `BSE-010`
- `BSE-011`
- `BSE-012`
- `BSE-013`
- `BSE-014`

Outcome:

- Codex can perform the full verification matrix for `Breach Scenario Engine MCP` without manual Unity interaction.

### Wave 3 - Harden the platform

- `BSE-015`
- `BSE-016`
- `BSE-017`
- `BSE-018`

Outcome:

- higher reliability, better diagnostics, cleaner developer workflow

### Wave 4 - Expand product surface

- `BSE-019`
- `BSE-020`
- `BSE-021`

Outcome:

- broader Unity automation coverage beyond the current vertical slice gate

## Success Criteria

The `Breach Scenario Engine MCP` server backlog can be considered successfully delivered for current project goals when:

- Wave 1 and Wave 2 are complete
- Codex can execute the full verification workflow from prompt alone
- final test results are deterministic
- ref-validation findings are structured and actionable
- localization coverage can be checked by locale and key
- build profile switching works through the shared bridge
- perf snapshots can be collected under `PC_Default`, `Android_Default`, and `Android_Low`
- save corruption scenarios can be exercised through a controlled diagnostics path



