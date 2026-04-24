# Breach Scenario Engine Project Documentation

Date: 2026-04-07
Project: `Breach Scenario Engine`

This document is the map for the active documentation set. It intentionally
tracks only the current Breach Scenario Engine mission pipeline. Imported Unity
MCP backlog and generic bridge workflow notes are not part of the active docs.

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

### Current MCP tools

- [Docs/canonical_tools.md](canonical_tools.md)
  - Source of truth for MCP functionality required by this project

- [Docs/runtime_tools.md](runtime_tools.md)
  - Public tool names exposed by the current runtime

### Mission pipeline contracts

- [Docs/breach_mcp_architecture_v2.2.md](breach_mcp_architecture_v2.2.md)
  - Layout-first mission generation architecture

- [Docs/mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md)
  - User-authored YAML ownership contract

- [Docs/mission_template_v2.2.md](mission_template_v2.2.md)
  - Canonical mission template specification

- [Docs/mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md)
  - `manage_mission(...)` action order, result envelope, retries, and artifacts

- [Docs/mission_data_contract_v2.2.md](mission_data_contract_v2.2.md)
  - Generated payload shape consumed by Unity

- [Docs/generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md)
  - Replay, seed, artifact, and verification manifest contract

### Planning and history

- [Docs/development_plan_sessions.md](development_plan_sessions.md)
  - Current session plan and handoff checklist

- [Docs/Archive/README.md](Archive/README.md)
  - Historical notes only

## 2. Recommended Reading Order

1. [README.md](../README.md)
2. [Docs/index.md](index.md)
3. [Docs/canonical_tools.md](canonical_tools.md)
4. [Docs/runtime_tools.md](runtime_tools.md)
5. [Docs/mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md)
6. [Docs/mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md)
7. [Docs/mission_data_contract_v2.2.md](mission_data_contract_v2.2.md)
8. [Docs/generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md)
9. [Docs/development_plan_sessions.md](development_plan_sessions.md)

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
