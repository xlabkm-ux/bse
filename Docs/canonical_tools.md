# Current MCP Tools

This document is the source of truth for MCP functionality required by the
current Breach Scenario Engine project.

The broader Unity MCP bridge may expose more generic tools. Those generic tools
are implementation support, not project requirements. Do not add them to this
document unless Breach Scenario Engine actively depends on them.

The actual runtime inventory is documented separately in `runtime_tools.md`.

## Required Project Tools

### Status and preflight

- `project_root.set`
- `project.info`
- `project.health_check`
- `project.capabilities`
- `editor.state`
- `read_console`
- `run_tests`
- `get_test_job`

### Mission pipeline

- `manage_mission(action="validate_template")`
- `manage_mission(action="compile_payload")`
- `manage_mission(action="generate_layout")`
- `manage_mission(action="place_entities")`
- `manage_mission(action="verify")`
- `manage_mission(action="write_manifest")`
- `manage_mission(action="cleanup_generation_lock")`

## Required Mission Artifacts

The mission pipeline writes project artifacts under
`UserMissionSources/missions/<missionId>/`:

- `mission_compile_report.json`
- `mission_payload.generated.json`
- `mission_layout.generated.json`
- `mission_entities.generated.json`
- `verification_summary.json`
- `mission_state.json`
- `generation_manifest.json`

The v2.3 lifecycle contract also reserves the transient mission lock:

- `.generation.lock`

## Supporting Documentation

- `mission_pipeline_contract_v2.3.md` defines action order, result envelope,
  retry behavior, and artifact ownership.
- `mission_authoring_contract_v2.3.md` defines what authors may put in mission
  templates.
- `mission_data_contract_v2.3.md` defines generated payload shape.
- `generation_manifest_contract_v2.3.md` defines accepted replay, lifecycle,
  and manifest fields.
