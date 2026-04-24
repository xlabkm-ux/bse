# Development Plan by Sessions

Date: 2026-04-24
Project: `Breach Scenario Engine`

This document splits the mission pipeline work into handoff-friendly sessions.
It is intended for switching between chats or resuming later without rebuilding
the whole roadmap.

## Current State

Completed:

- `manage_mission` is exposed by the server
- `manage_mission` is routed through the Unity bridge
- `validate_template` works for `VS01_HostageApartment`
- `compile_payload` works for `VS01_HostageApartment`
- `mission_payload.generated.json` and `mission_compile_report.json` are generated
- `generate_layout` writes deterministic layout/tactical graph output
- `layoutRevisionId` is stable for identical layout inputs
- `place_entities` remains blocked by `ORDER_VIOLATION_NO_LAYOUT_GRAPH` until layout exists
- `place_entities` writes deterministic actor/objective placement after current layout validation
- generated placement entities carry stable ownership metadata, `roomId`, `navNodeId`, and `layoutRevisionId`
- `verify` writes `verification_summary.json` with structured findings and metrics
- Unity editor compilation was verified against the embedded package

Pending:

- `write_manifest`
- retry policy
- generation locks
- richer template validation and schema fidelity
- manifest/status lifecycle updates

## Session 1: Contract Stabilization

Goal:

- keep the mission pipeline contract consistent between docs, server, bridge,
  and tests

Work:

- review `Docs/mission_pipeline_contract_v2.2.md`
- keep `Docs/runtime_tools.md` and `Docs/breach_mcp_server_backlog.md` in sync
- tighten the `manage_mission` result shape
- align template validation errors to contract codes

Exit criteria:

- no doc drift around `manage_mission`
- `validate_template` and `compile_payload` stay stable

Recommended handoff note:

- "The first slice is working. Please keep contract and doc updates in sync
  while expanding validation fidelity."

## Session 2: Template and Payload Fidelity

Goal:

- make `validate_template` and `compile_payload` closer to the v2.2 contracts

Work:

- improve YAML parsing robustness
- validate all required authoring fields
- normalize actor counts and objective references
- make payload generation match the JSON schema more strictly
- add focused tests for invalid templates and boundary cases

Exit criteria:

- invalid templates fail with structured findings
- payload shape matches `Docs/mission_data_contract_v2.2.md`

Recommended handoff note:

- "The compiler works for VS01. Please harden parsing and schema checks before
  touching layout."

## Session 3: Layout Generation

Goal:

- implement `manage_mission(action="generate_layout")`

Work:

- create layout generation service
- produce `LayoutGraph`, `RoomGraph`, `PortalGraph`, `CoverGraph`,
  `VisibilityGraph`, and `HearingGraph`
- compute `layoutRevisionId`
- ensure retries return to Step 6, not Step 5
- keep entity placement blocked until layout exists

Exit criteria:

- a deterministic layout exists for `VS01_HostageApartment`
- `layoutRevisionId` is stable for identical inputs

Recommended handoff note:

- "Please build the layout layer next. Placement must remain layout-gated."

## Session 4: Entity Placement

Goal:

- implement `manage_mission(action="place_entities")`

Status:

- completed

Work:

- place actors and objectives only after layout
- enforce `Step 6 -> Step 5` ordering
- attach generated ownership markers
- ensure entities carry `roomId`, `navNodeId`, and `layoutRevisionId`

Exit criteria:

- placement fails cleanly without a current layout
- generated objects have stable ownership metadata

Recommended handoff note:

- "Layout now exists. Please wire post-layout placement and keep the ordering
  invariant intact."

## Session 5: Verification

Goal:

- implement `manage_mission(action="verify")`

Status:

- completed

Work:

- check navigation reachability
- check tactical density and performance budgets
- validate profile references
- validate scene and prefab references where relevant
- produce `verification_summary.json`

Exit criteria:

- verification emits structured findings and metrics
- pass/fail is machine readable

Recommended handoff note:

- "Placement is ready. Please add verification and keep the output JSON-only."

## Session 6: Manifest and Replay

Goal:

- implement `manage_mission(action="write_manifest")`

Work:

- write `generation_manifest.json`
- own `effectiveSeed`, `retrySeeds`, `layoutRevisionId`, and artifact paths
- set `effectiveSeed` only after verification passes
- lock mission writes to avoid concurrent corruption

Exit criteria:

- manifest is written only after PASS
- replay data is deterministic and complete

Recommended handoff note:

- "Verification is done. Please finish manifest ownership and replay handling."

## Session 7: Tests and Cleanup

Goal:

- protect the pipeline with tests and remove rough edges

Work:

- add coverage for success and failure paths
- test command routing through the server and bridge
- test artifact creation paths
- update backlog and runtime docs
- clean up warnings only when they affect the mission pipeline work

Exit criteria:

- pipeline regressions are covered by targeted tests
- docs reflect current implementation status

Recommended handoff note:

- "The pipeline is functionally complete enough for end-to-end checks. Please
  shore up tests and docs."

## Suggested Chat Split

Use separate chats for these clusters:

1. Contract and validation work
2. Layout generation
3. Placement and ownership markers
4. Verification and metrics
5. Manifest, locks, and replay
6. Tests and documentation cleanup

## Handoff Checklist

Before switching chats:

- note what was completed
- note the exact file paths touched
- note the next blocking step
- note any failing test or compile message verbatim
- note whether Unity was last validated in batch mode or live mode

## Anchor Files

- [Docs/mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md)
- [Docs/mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md)
- [Docs/mission_data_contract_v2.2.md](mission_data_contract_v2.2.md)
- [Docs/generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md)
- [Docs/breach_mcp_server_backlog.md](breach_mcp_server_backlog.md)
- [Docs/runtime_tools.md](runtime_tools.md)
