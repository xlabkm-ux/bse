# Breach Scenario Engine Project Documentation

Date: 2026-04-25
Project: `Breach Scenario Engine`

This document is the map for the active documentation set. It intentionally
tracks only the current Breach Scenario Engine mission pipeline. Imported Unity
MCP backlog and generic bridge workflow notes are not part of the active docs.

## Target Platforms

- Primary: Windows 10 / 11 desktop, 1920x1080+ resolution, 16:9 or wider
- Secondary: Android mobile, 1080p portrait and landscape, touch controls, and
  optional controller support
- UI: scalable across supported desktop and mobile resolutions

## 1. Active Documentation Set

### Entry points

- [README.md](../README.md)
  - Project landing page
  - Repository layout and mission workflow

- [Docs/README.md](README.md)
  - Short active-docs list

- [Docs/index.md](index.md)
  - Navigation table and reading order

- [Docs/workspace_index.md](workspace_index.md)
  - Code and data location map

- [Docs/audit_current_state_v2.3.md](audit_current_state_v2.3.md)
  - Factual baseline before v2.3 stabilization work

### Current MCP tools

- [Docs/canonical_tools.md](canonical_tools.md)
  - Source of truth for MCP functionality required by this project

- [Docs/runtime_tools.md](runtime_tools.md)
  - Public tool names exposed by the current runtime

### Mission pipeline contracts

- [Docs/breach_mcp_architecture_v2.3.md](breach_mcp_architecture_v2.3.md)
  - Active layout-first mission generation architecture

- [Docs/mission_authoring_contract_v2.3.md](mission_authoring_contract_v2.3.md)
  - User-authored YAML ownership, profile root, and catalog root

- [Docs/mission_template_v2.3.md](mission_template_v2.3.md)
  - Canonical mission template specification

- [Docs/mission_pipeline_contract_v2.3.md](mission_pipeline_contract_v2.3.md)
  - `manage_mission(...)` action order, result envelope, retries, and artifacts

- [Docs/mission_data_contract_v2.3.md](mission_data_contract_v2.3.md)
  - Generated payload shape consumed by Unity

- [Docs/generation_manifest_contract_v2.3.md](generation_manifest_contract_v2.3.md)
  - Accepted replay, seed, lifecycle, and manifest contract

### BREACH technology migration

- [Docs/migration_from_breach_scene_builders.md](migration_from_breach_scene_builders.md)
  - Reference mapping from legacy BREACH scene builders to BSE v2.3 modules

- [Docs/breach_technology_transfer_plan_v2.3.md](breach_technology_transfer_plan_v2.3.md)
  - Phased plan for transferring BSP, tactical graphs, materialization,
    verification, content catalogs, and CI preview workflow

- [Docs/breach_technology_transfer_traceability_v2.3.md](breach_technology_transfer_traceability_v2.3.md)
  - Source-to-target audit for transferring BREACH technology without copying
    legacy editor architecture

- [Docs/scene_materialization_contract_v2.3.md](scene_materialization_contract_v2.3.md)
  - Contract for turning generated JSON artifacts into optional Unity scenes

- [Docs/breach_technology_transfer_acceptance_v2.3.md](breach_technology_transfer_acceptance_v2.3.md)
  - Acceptance gates, traceability requirements, and migration risks

### Planning and history

- [Docs/development_plan_sessions.md](development_plan_sessions.md)
  - Current session plan and handoff checklist

- [Docs/Archive/README.md](Archive/README.md)
  - Historical notes only

## 2. Recommended Reading Order

1. [README.md](../README.md)
2. [Docs/index.md](index.md)
3. [Docs/audit_current_state_v2.3.md](audit_current_state_v2.3.md)
4. [Docs/canonical_tools.md](canonical_tools.md)
5. [Docs/runtime_tools.md](runtime_tools.md)
6. [Docs/breach_mcp_architecture_v2.3.md](breach_mcp_architecture_v2.3.md)
7. [Docs/mission_authoring_contract_v2.3.md](mission_authoring_contract_v2.3.md)
8. [Docs/mission_template_v2.3.md](mission_template_v2.3.md)
9. [Docs/mission_pipeline_contract_v2.3.md](mission_pipeline_contract_v2.3.md)
10. [Docs/mission_data_contract_v2.3.md](mission_data_contract_v2.3.md)
11. [Docs/generation_manifest_contract_v2.3.md](generation_manifest_contract_v2.3.md)
12. [Docs/migration_from_breach_scene_builders.md](migration_from_breach_scene_builders.md)
13. [Docs/breach_technology_transfer_plan_v2.3.md](breach_technology_transfer_plan_v2.3.md)
14. [Docs/breach_technology_transfer_traceability_v2.3.md](breach_technology_transfer_traceability_v2.3.md)
15. [Docs/scene_materialization_contract_v2.3.md](scene_materialization_contract_v2.3.md)
16. [Docs/breach_technology_transfer_acceptance_v2.3.md](breach_technology_transfer_acceptance_v2.3.md)
17. [Docs/development_plan_sessions.md](development_plan_sessions.md)

## 3. Runtime Snapshot

The current project-required MCP surface is:

- status and preflight: `project_root.set`, `project.info`,
  `project.health_check`, `project.capabilities`, `editor.state`,
  `read_console`, `run_tests`, `get_test_job`
- mission pipeline: `manage_mission(action="validate_template")`,
  `compile_payload`, `generate_layout`, `place_entities`, `verify`,
  `write_manifest`

The broader runtime may expose additional Unity bridge tools. Those tools are
implementation support and should not be treated as Breach Scenario Engine
requirements unless a task explicitly depends on them.

## 4. Maintenance Rules

1. Keep active project docs in `Docs/`.
2. Keep `canonical_tools.md` limited to MCP functionality required by the
   current project.
3. Keep `runtime_tools.md` aligned with the actual runtime inventory.
4. Use `development_plan_sessions.md` as the active backlog and handoff plan.
5. Move historical notes to `Docs/Archive/` only when this repo needs to retain
   them; otherwise prefer removing copied notes that can be found in the source
   MCP project.
6. Keep generated artifacts, `bin/`, and `obj/` out of version control.
