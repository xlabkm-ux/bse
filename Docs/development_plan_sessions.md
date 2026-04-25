# Development Plan by Sessions

Date: 2026-04-24
Project: `Breach Scenario Engine`

This document splits the mission pipeline work into handoff-friendly sessions.
It is intended for switching between chats or resuming later without rebuilding
the whole roadmap.

## Active Continuation: v2.3 Stabilization

The v2.2 mission pipeline sessions below are functionally complete. New chats
should continue the v2.3 stabilization track unless the user asks for a
different task.

### v2.3 Session 1: Current-State Audit

Status:

- completed

Goal:

- record the factual repository state before changing v2.3 contracts or code

Completed:

- created `Docs/audit_current_state_v2.3.md`
- linked the audit from the active docs index and project documentation
- identified Must Fix / Should Fix / Nice To Have work for v2.3

Handoff note:

- "The v2.3 audit baseline is recorded. Please start by stabilizing the repo
  owned v2.3 docs before changing runtime behavior."

### v2.3 Session 2: Documentation Stabilization

Status:

- completed

Goal:

- create one repo-owned v2.3 documentation set and stop mixing the external
  continuation plan with active v2.2 contracts

Work:

- create `Docs/breach_mcp_architecture_v2.3.md`
- create `Docs/mission_pipeline_contract_v2.3.md`
- create `Docs/mission_authoring_contract_v2.3.md`
- create `Docs/mission_template_v2.3.md`
- create `Docs/mission_data_contract_v2.3.md`
- create `Docs/generation_manifest_contract_v2.3.md`
- update `Docs/index.md`, `Docs/project_documentation.md`,
  `Docs/runtime_tools.md`, `Docs/canonical_tools.md`, and this plan
- decide the authoritative profile/catalog root before v2.3 validation work

Completed:

- created the repo-owned v2.3 documentation set
- updated `README.md`, `Docs/README.md`, `Docs/index.md`,
  `Docs/project_documentation.md`, `Docs/workspace_index.md`,
  `Docs/runtime_tools.md`, `Docs/canonical_tools.md`, and this plan
- made the active reading path point at v2.3 contracts
- documented v2.3 invariants: Step 6 before Step 5, retry back to Step 6,
  manifest only after PASS, JSON-only machine decisions, and `effectiveSeed`
  only after PASS
- chose `Assets/Data/Mission/Profiles/` as the authoritative profile root and
  `Assets/Data/Mission/Catalogs/` as the authoritative catalog root

Exit criteria:

- active docs have a single v2.3 reading path
- v2.3 invariants are explicitly documented:
  Step 6 before Step 5, retry to Step 6, manifest after PASS, JSON-only
  machine decisions, and `effectiveSeed` only after PASS

Handoff note:

- "The v2.3 docs are now repo-owned and active. Please implement the
  mission-scoped `.generation.lock` and `mission_state.json` lifecycle next,
  keeping `write_manifest` blocked unless verification is PASS."

### v2.3 Session 3: Generation Locks and Lifecycle

Status:

- completed

Goal:

- align implementation with the v2.3 lock and mission lifecycle contract

Work:

- add mission-scoped `.generation.lock`
- add `mission_state.json`
- define lifecycle states and current-step updates
- add safe unlock behavior, stale lock handling, and diagnostic cleanup
- keep `write_manifest` blocked when lifecycle state is incompatible

Completed:

- added mission-scoped `.generation.lock` under each mission directory
- added `mission_state.json` lifecycle writes for compile, layout, placement,
  verification, retry, manifest, and cleanup transitions
- added explicit stale-lock diagnostic cleanup through
  `manage_mission(action="cleanup_generation_lock")`
- changed failed/blocked manifest attempts to update `mission_state.json`
  without writing a failed `generation_manifest.json`
- kept `write_manifest` gated on PASS verification and compatible PASS mission
  state
- updated retry seed derivation to include the verification failure code

Exit criteria:

- generated mission writes are mission-lock guarded
- lock conflicts return `GENERATION_LOCK_CONFLICT`
- stale lock cleanup is explicit and diagnostic
- `write_manifest` writes accepted manifests only after compatible PASS state

Handoff note:

- "Generation locks and mission lifecycle are now v2.3-aligned. Continue with
  validation and payload fidelity: split template findings into the v2.3
  `TPL_*` family, add invalid fixtures, document or replace the YAML subset
  parser, and add repo-owned JSON Schema validation."

### v2.3 Session 4: Validation and Payload Fidelity

Status:

- completed

Goal:

- harden template validation and payload schema fidelity

Work:

- split validation findings into the v2.3 `TPL_*` code family
- add invalid template fixtures
- document or replace the current custom YAML subset parser
- add repo-owned JSON Schema validation before payload write

Completed:

- split template validation findings into `TPL_UNKNOWN_FIELD`,
  `TPL_RANGE_INVALID`, `TPL_PROFILE_REF_MISSING`, `TPL_OBJECTIVE_INVALID`,
  and `TPL_ACTOR_ROSTER_INVALID`
- added invalid template fixtures under `UserMissionSources/missions/_test_invalid_*`
- documented the current line-oriented YAML subset parser in the template contract
- added repo-owned JSON Schema validation for `mission_payload.generated.json`

### v2.3 Session 5: Verification and Retry Hardening

Status:

- completed

Goal:

- expand verification metrics and make retry derivation match the v2.3 policy

Work:

- add v2.3 verification metrics and retryable/blocking classification
- include failure code in retry seed derivation if retained by contract
- preserve Step 6 -> Step 5 -> Step 7 on every retry

Completed:

- added expanded verification metrics for reachability, alternate routes,
  hearing overlap, chokepoint pressure, and objective room pressure
- added `retryClass`, `failureCode`, and retryability counts to
  `verification_summary.json`
- kept retry derivation tied to the current failure code while preserving
  Step 6 -> Step 5 -> Step 7 on every retry

### v2.3 Session 6: Unity Package and Content Layer

Status:

- pending

Goal:

- align Unity dependencies, Addressables, profiles, and catalogs with the
  v2.3 target architecture

Work:

- resolve direct package dependency decisions for Addressables, Burst,
  Collections, Jobs, and AI Assistant
- align embedded package metadata with Unity `6000.4.3f1`
- add typed profiles and catalogs after the docs choose an authoritative path
- validate Addressables labels and profile versions

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
- retry execution returns retryable verification failures to Step 6 before
  writing the manifest
- mission-scoped `.generation.lock` guards generated mission writes
- `mission_state.json` records lifecycle state and blocks incompatible manifest
  writes

Pending:

- richer template validation and schema fidelity

## Session 1: Contract Stabilization

Goal:

- keep the mission pipeline contract consistent between docs, server, bridge,
  and tests

Work:

- review `Docs/mission_pipeline_contract_v2.2.md`
- keep `Docs/runtime_tools.md` and `Docs/canonical_tools.md` in sync
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

Status:

- completed

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

Status:

- completed

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

## Session 8: Retry Execution

Goal:

- execute deterministic retries after Step 7 returns a retryable failure

Status:

- completed

Work:

- keep retry orchestration inside the existing public `manage_mission`
  surface
- derive retry seeds with the v2.2 hash policy
- rerun Step 6 -> Step 5 -> Step 7 for retryable verification failures
- write `generation_manifest.json` only after retry verification returns PASS
- record attempted retry seeds and accepted `effectiveSeed`

Exit criteria:

- retryable verification failures return to layout generation, not placement
- non-retryable failures still block manifest writing
- retry seeds are recorded in `generation_manifest.json`

Recommended handoff note:

- "Retry execution is wired through write_manifest. Please keep future
  generator changes honoring Step 6 -> Step 5 -> Step 7 on every retry."

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

- [Docs/audit_current_state_v2.3.md](audit_current_state_v2.3.md)
- [Docs/breach_mcp_architecture_v2.3.md](breach_mcp_architecture_v2.3.md)
- [Docs/mission_pipeline_contract_v2.3.md](mission_pipeline_contract_v2.3.md)
- [Docs/mission_authoring_contract_v2.3.md](mission_authoring_contract_v2.3.md)
- [Docs/mission_template_v2.3.md](mission_template_v2.3.md)
- [Docs/mission_data_contract_v2.3.md](mission_data_contract_v2.3.md)
- [Docs/generation_manifest_contract_v2.3.md](generation_manifest_contract_v2.3.md)
- [Docs/runtime_tools.md](runtime_tools.md)
