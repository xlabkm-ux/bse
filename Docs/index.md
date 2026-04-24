# Docs Index

Short entry point for the active Breach Scenario Engine MCP documentation.

| Document | Purpose |
|---|---|
| [project_documentation.md](project_documentation.md) | Full map of the docs set and reading order |
| [breach_mcp_architecture_v2.2.md](breach_mcp_architecture_v2.2.md) | Technical architecture specification for the layout-first pipeline |
| [mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md) | Authoring ownership and profile split |
| [mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md) | Step 0-7 mission generation contract |
| [mission_template_v2.2.md](mission_template_v2.2.md) | Canonical mission template specification |
| [mission_data_contract_v2.2.md](mission_data_contract_v2.2.md) | JSON Schema contract for generated mission payloads |
| [generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md) | Replay, seed, artifact, and verification manifest contract |
| [canonical_tools.md](canonical_tools.md) | Target tool and resource contract |
| [runtime_tools.md](runtime_tools.md) | Actual tools exposed by the current runtime |
| [breach_mcp_verification_contract.md](breach_mcp_verification_contract.md) | Required verification payloads and resources |
| [breach_mcp_server_backlog.md](breach_mcp_server_backlog.md) | Remaining server tasks and status |
| [Archive/README.md](Archive/README.md) | Archive index |

## Quick use

1. Read [project_documentation.md](project_documentation.md) for the full map.
2. Check [runtime_tools.md](runtime_tools.md) against [canonical_tools.md](canonical_tools.md).
3. Read [breach_mcp_architecture_v2.2.md](breach_mcp_architecture_v2.2.md) for pipeline orchestration and retry rules.
4. Read [mission_authoring_contract_v2.2.md](mission_authoring_contract_v2.2.md) before editing mission YAML.
5. Read [mission_pipeline_contract_v2.2.md](mission_pipeline_contract_v2.2.md) before implementing generation code.
6. Read [mission_data_contract_v2.2.md](mission_data_contract_v2.2.md) and [generation_manifest_contract_v2.2.md](generation_manifest_contract_v2.2.md) for runtime artifacts.
7. Use [breach_mcp_verification_contract.md](breach_mcp_verification_contract.md) when validating bridge behavior.
8. Track remaining work in [breach_mcp_server_backlog.md](breach_mcp_server_backlog.md).
