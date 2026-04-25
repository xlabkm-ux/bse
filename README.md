# Breach Scenario Engine

Initial project scaffold for **TACTICAL BREACH: Mission Architect / Breach Scenario Engine**.

## Project Layout

- `Assets/` - Unity runtime and editor content
- `Assets/Scripts/` - gameplay and generation code
- `Assets/Editor/` - editor tooling and MCP integration
- `UserMissionSources/` - mission authoring inputs and generated mission artifacts
- `dotnet-prototype/` - imported .NET MCP server prototype and Unity bridge package
- `Docs/` - supporting notes and technical references
- `Packages/manifest.json` - Unity package manifest
- `ProjectSettings/` - Unity project configuration

## Mission Workflow

1. Fill a mission template in `UserMissionSources/missions/<missionId>/mission_design.template.yaml`
2. Validate it against `Docs/mission_authoring_contract_v2.3.md`
3. Compile it through `Docs/mission_pipeline_contract_v2.3.md` into `mission_payload.generated.json`
4. Generate layout before placement, verify the generated mission, and write `generation_manifest.json` only after PASS

## Docs

- [Docs/README.md](Docs/README.md)
- [Docs/index.md](Docs/index.md)
- [Docs/breach_mcp_architecture_v2.3.md](Docs/breach_mcp_architecture_v2.3.md)
- [Docs/mission_authoring_contract_v2.3.md](Docs/mission_authoring_contract_v2.3.md)
- [Docs/mission_template_v2.3.md](Docs/mission_template_v2.3.md)
- [Docs/mission_pipeline_contract_v2.3.md](Docs/mission_pipeline_contract_v2.3.md)
- [Docs/mission_data_contract_v2.3.md](Docs/mission_data_contract_v2.3.md)
- [Docs/generation_manifest_contract_v2.3.md](Docs/generation_manifest_contract_v2.3.md)

## Standards

- C# uses 4-space indentation.
- YAML, JSON, Markdown, UXML, and USS use 2-space indentation.
- Source files are normalized through `.editorconfig`.
- Generated mission artifacts are ignored through `.gitignore`.

## Next Steps

- Keep mission pipeline contracts and runtime behavior in sync.
- Implement v2.3 generation locks and lifecycle state from the session plan.
- Use `Docs/canonical_tools.md` as the current project MCP tool contract.
