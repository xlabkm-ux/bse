# Breach Scenario Engine

Initial project scaffold for **TACTICAL BREACH: Mission Architect / Breach Scenario Engine**.

## What is here

- Unity project skeleton
- Mission source folders under `UserMissionSources/`
- Starter mission template aligned with the v1.4 mission architecture
- Git ignore rules for generated Unity and pipeline artifacts

## Reference workflow

1. Fill a mission template in `UserMissionSources/missions/<missionId>/mission_design.template.yaml`
2. Validate and compile it into `mission_payload.generated.json`
3. Run the MCP / Unity generation pipeline
4. Store deterministic outputs in the mission folder

## Next steps

- Open the project in Unity 6
- Add the actual runtime assemblies and Editor tooling
- Connect the mission compiler and verification pipeline

