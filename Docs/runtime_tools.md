# Current Runtime Tools

This document lists the public tool names exposed by the current `dotnet-prototype` runtime.
The inventory is target-only.

## Core project and status

- `project_root.set`
- `project.info`
- `project.health_check`
- `project.capabilities`
- `editor.state`
- `read_console`

## Asset and content operations

- `manage_asset`
- `manage_hierarchy`
- `manage_scene`
- `manage_gameobject`
- `manage_components`
- `manage_script`
- `manage_scriptableobject`
- `manage_prefabs`
- `manage_graph`
- `manage_ui`
- `manage_localization`

## Runtime control and verification

- `manage_editor`
- `manage_input`
- `manage_camera`
- `manage_graphics`
- `manage_profiler`
- `manage_build`
- `manage_mission`
- `run_tests`
- `get_test_job`

## Notes

- The runtime no longer advertises legacy bridge command names in `tools/list`.
- The target names above are the ones to use in new workflows and docs.
- `manage_editor` now covers editor status/play mode plus Unity MCP package lifecycle actions: `install`, `update`, and `delete`.
- `manage_mission` is now part of the current runtime inventory for the mission pipeline. The current vertical slice supports `validate_template`, `compile_payload`, `generate_layout`, layout-gated `place_entities`, and `verify`; `write_manifest` remains a planned follow-up action.
- `manage_mission` returns the shared mission JSON envelope in the tool result text, so callers should parse `status`, `missionId`, `pipelineVersion`, `artifacts`, and `findings` from the returned string.
