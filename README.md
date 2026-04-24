# Breach Scenario Engine

Initial project scaffold for **TACTICAL BREACH: Mission Architect / Breach Scenario Engine**.

## Project Layout

- `Assets/` - Unity runtime and editor content
- `Assets/Scripts/` - gameplay and generation code
- `Assets/Editor/` - editor tooling and MCP integration
- `UserMissionSources/` - mission authoring inputs and generated mission artifacts
- `Docs/` - supporting notes and technical references
- `Packages/manifest.json` - Unity package manifest
- `ProjectSettings/` - Unity project configuration

## Mission Workflow

1. Fill a mission template in `UserMissionSources/missions/<missionId>/mission_design.template.yaml`
2. Validate and compile it into `mission_payload.generated.json`
3. Run the MCP / Unity generation pipeline
4. Store deterministic outputs in the mission folder

## Standards

- C# uses 4-space indentation.
- YAML, JSON, Markdown, UXML, and USS use 2-space indentation.
- Source files are normalized through `.editorconfig`.
- Generated mission artifacts are ignored through `.gitignore`.

## Next Steps

- Add runtime assemblies under `Assets/Scripts/`
- Add MCP/editor tooling under `Assets/Editor/`
- Connect the mission compiler and verification pipeline
