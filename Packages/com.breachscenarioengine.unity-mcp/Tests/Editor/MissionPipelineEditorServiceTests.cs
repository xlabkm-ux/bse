using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using UnityEngine;

namespace BreachScenarioEngine.Mcp.Editor.Tests
{
    public sealed class MissionPipelineEditorServiceTests
    {
        private string _testRoot = "";

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine(ProjectRoot(), "Temp", "MissionPipelineEditorServiceTests");
            Directory.CreateDirectory(_testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }

        [Test]
        public void ValidateTemplate_InvalidTemplate_ReturnsStructuredFindings()
        {
            var templatePath = Path.Combine(_testRoot, "invalid.template.yaml");
            File.WriteAllText(templatePath, string.Join("\n", new[]
            {
                "schemaVersion: \"tb.mission_template.v2.2\"",
                "missionId: \"VS99_Invalid\"",
                "missionTitle: \"Invalid\"",
                "",
                "generationMeta:",
                "  initialSeed: -1",
                "  effectiveSeed: 0",
                "  generationTimeout: 45",
                "  maxRetries: 5",
                "",
                "spatialConstraints:",
                "  worldBounds: [64, 64]",
                "  pixelsPerUnit: 64",
                "  tacticalTheme: \"unknown\"",
                "  bspConstraints:",
                "    minRoomSize: [12, 12]",
                "    maxRoomSize: [4, 4]",
                "    corridorWidth: 0",
                "    forceRoomAdjacency: true",
                "",
                "tacticalRules:",
                "  noiseAlertThreshold: 2",
                "  strictNavigationPolicy: true",
                "  enforcePostLayoutPlacement: true",
                "  acousticOcclusion:",
                "    wallMultiplier: 2.5",
                "    doorPenalty: 1.2",
                "",
                "actorRoster:",
                "  - id: \"EN_01\"",
                "    type: \"Sentry\"",
                "    countRange: [3, 1]",
                "    navigationPolicy: \"Unknown\"",
                "    placementPolicy: \"PostLayout_TaggedRoom\"",
                "",
                "objectives:",
                "  primary:",
                "    - id: \"OBJ_MAIN\"",
                "      type: \"RescueHostage\"",
                "      requiresLayoutGraph: true",
                ""
            }));

            var (success, message) = MissionPipelineEditorService.Execute("validate_template", RawArgs(templatePath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            Assert.AreEqual("FAIL", doc.RootElement.GetProperty("status").GetString());
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsNotEmpty(findings);
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "TPL_SCHEMA_INVALID"));
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "TPL_SEMANTIC_INVALID"));
        }

        [Test]
        public void CompilePayload_ValidTemplate_WritesSchemaAlignedPayload()
        {
            var templatePath = Path.Combine(_testRoot, "valid.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            File.WriteAllText(templatePath, string.Join("\n", new[]
            {
                "schemaVersion: \"tb.mission_template.v2.2\"",
                "missionId: \"VS98_TestMission\"",
                "missionTitle: \"Test Mission #1\"",
                "",
                "generationMeta:",
                "  initialSeed: 42",
                "  effectiveSeed: 0",
                "  generationTimeout: 45",
                "  maxRetries: 5",
                "",
                "spatialConstraints:",
                "  worldBounds: [64, 64]",
                "  pixelsPerUnit: 128",
                "  tacticalTheme: \"urban_cqb\"",
                "  bspConstraints:",
                "    minRoomSize: [4, 4]",
                "    maxRoomSize: [12, 12]",
                "    corridorWidth: 2",
                "    forceRoomAdjacency: true",
                "",
                "tacticalRules:",
                "  noiseAlertThreshold: 0.15",
                "  strictNavigationPolicy: true",
                "  enforcePostLayoutPlacement: true",
                "  acousticOcclusion:",
                "    wallMultiplier: 2.5",
                "    doorPenalty: 1.2",
                "",
                "actorRoster:",
                "  - id: \"OP_01\"",
                "    type: \"Operative\"",
                "    countRange: [2, 4]",
                "    navigationPolicy: \"FullAccess\"",
                "    placementPolicy: \"EntryPointOnly\"",
                "",
                "objectives:",
                "  primary:",
                "    - id: \"OBJ_MAIN\"",
                "      type: \"SecureRoom\"",
                "      requiresLayoutGraph: true",
                "      targetRoomTag: \"entry\"",
                ""
            }));

            var (success, message) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));

            Assert.True(success, message);
            Assert.True(File.Exists(payloadPath));
            using var doc = JsonDocument.Parse(File.ReadAllText(payloadPath));
            var root = doc.RootElement;
            Assert.AreEqual("bse.mission_payload.v2.2", root.GetProperty("header").GetProperty("schemaVersion").GetString());
            Assert.AreEqual("2.2", root.GetProperty("header").GetProperty("pipelineVersion").GetString());
            Assert.AreEqual("VS98_TestMission", root.GetProperty("header").GetProperty("missionId").GetString());
            Assert.AreEqual(42, root.GetProperty("header").GetProperty("initialSeed").GetInt32());
            Assert.AreEqual(0, root.GetProperty("header").GetProperty("effectiveSeed").GetInt32());
            Assert.AreEqual("urban_cqb", root.GetProperty("spatial").GetProperty("theme").GetString());
            Assert.AreEqual(2, root.GetProperty("roster")[0].GetProperty("count").GetInt32());
            Assert.True(root.GetProperty("objectives").GetProperty("primary")[0].GetProperty("requiresLayoutGraph").GetBoolean());
            Assert.True(root.TryGetProperty("profileRefs", out _));
        }

        [Test]
        public void GenerateLayout_ValidTemplate_WritesDeterministicGraphs()
        {
            var templatePath = Path.Combine(_testRoot, "layout.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            File.WriteAllText(templatePath, ValidTemplate("VS97_LayoutMission"));

            var (compileSuccess, compileMessage) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));
            Assert.True(compileSuccess, compileMessage);

            var (firstSuccess, firstMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(firstSuccess, firstMessage);
            Assert.True(File.Exists(layoutPath));
            using var firstDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
            var firstRevision = firstDoc.RootElement.GetProperty("layoutRevisionId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(firstRevision));
            Assert.True(firstDoc.RootElement.TryGetProperty("LayoutGraph", out _));
            Assert.True(firstDoc.RootElement.TryGetProperty("RoomGraph", out _));
            Assert.True(firstDoc.RootElement.TryGetProperty("PortalGraph", out _));
            Assert.True(firstDoc.RootElement.TryGetProperty("CoverGraph", out _));
            Assert.True(firstDoc.RootElement.TryGetProperty("VisibilityGraph", out _));
            Assert.True(firstDoc.RootElement.TryGetProperty("HearingGraph", out _));

            var (secondSuccess, secondMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(secondSuccess, secondMessage);
            using var secondDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
            Assert.AreEqual(firstRevision, secondDoc.RootElement.GetProperty("layoutRevisionId").GetString());

            using var payloadDoc = JsonDocument.Parse(File.ReadAllText(payloadPath));
            Assert.AreEqual(firstRevision, payloadDoc.RootElement.GetProperty("header").GetProperty("layoutRevisionId").GetString());
        }

        [Test]
        public void PlaceEntities_WithoutLayout_FailsWithOrderingFinding()
        {
            var templatePath = Path.Combine(_testRoot, "placement.template.yaml");
            File.WriteAllText(templatePath, ValidTemplate("VS96_PlacementMission"));

            var (success, message) = MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            Assert.AreEqual("FAIL", doc.RootElement.GetProperty("status").GetString());
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "ORDER_VIOLATION_NO_LAYOUT_GRAPH"));
        }

        [Test]
        public void PlaceEntities_WithCurrentLayout_WritesOwnedEntities()
        {
            var templatePath = Path.Combine(_testRoot, "entities.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            File.WriteAllText(templatePath, ValidTemplate("VS95_EntityMission"));

            var (compileSuccess, compileMessage) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));
            Assert.True(compileSuccess, compileMessage);
            var (layoutSuccess, layoutMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(layoutSuccess, layoutMessage);

            var (success, message) = MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath));

            Assert.True(success, message);
            Assert.True(File.Exists(entitiesPath));
            using var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
            using var entitiesDoc = JsonDocument.Parse(File.ReadAllText(entitiesPath));
            var layoutRevisionId = layoutDoc.RootElement.GetProperty("layoutRevisionId").GetString();
            var root = entitiesDoc.RootElement;
            Assert.AreEqual("bse.mission_entities.v2.2", root.GetProperty("schemaVersion").GetString());
            Assert.AreEqual(layoutRevisionId, root.GetProperty("layoutRevisionId").GetString());
            Assert.AreEqual(2, root.GetProperty("actors").GetArrayLength());
            Assert.AreEqual(1, root.GetProperty("objectives").GetArrayLength());

            foreach (var entity in root.GetProperty("actors").EnumerateArray().Concat(root.GetProperty("objectives").EnumerateArray()))
            {
                Assert.False(string.IsNullOrWhiteSpace(entity.GetProperty("roomId").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(entity.GetProperty("navNodeId").GetString()));
                Assert.AreEqual(layoutRevisionId, entity.GetProperty("layoutRevisionId").GetString());
                var ownership = entity.GetProperty("ownership");
                Assert.AreEqual("bse-pipeline", ownership.GetProperty("owner").GetString());
                Assert.AreEqual("manage_mission.place_entities", ownership.GetProperty("generatedBy").GetString());
                Assert.AreEqual(layoutRevisionId, ownership.GetProperty("layoutRevisionId").GetString());
                Assert.False(string.IsNullOrWhiteSpace(ownership.GetProperty("stableKey").GetString()));
            }
        }

        [Test]
        public void PlaceEntities_WithStaleLayout_FailsWithOrderingFinding()
        {
            var templatePath = Path.Combine(_testRoot, "stale.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            File.WriteAllText(templatePath, ValidTemplate("VS94_StaleMission"));

            var (compileSuccess, compileMessage) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));
            Assert.True(compileSuccess, compileMessage);
            var (layoutSuccess, layoutMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(layoutSuccess, layoutMessage);

            using (var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath)))
            {
                var layout = JsonNode.Parse(layoutDoc.RootElement.GetRawText()).AsObject();
                layout["layoutRevisionId"] = "layout_stale";
                File.WriteAllText(layoutPath, layout.ToJsonString());
            }

            var (success, message) = MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "ORDER_VIOLATION_STALE_LAYOUT_GRAPH"));
        }

        private static string RawArgs(string templatePath, string payloadPath = null, string layoutPath = null, string entitiesPath = null)
        {
            var template = ToRepoPath(templatePath);
            if (payloadPath == null && layoutPath == null && entitiesPath == null)
            {
                return "{ \"templatePath\": \"" + template + "\" }";
            }

            var json = "{ \"templatePath\": \"" + template + "\"";
            if (payloadPath != null)
            {
                json += ", \"payloadPath\": \"" + ToRepoPath(payloadPath) + "\"";
            }

            if (layoutPath != null)
            {
                json += ", \"layoutPath\": \"" + ToRepoPath(layoutPath) + "\"";
            }

            if (entitiesPath != null)
            {
                json += ", \"entitiesPath\": \"" + ToRepoPath(entitiesPath) + "\"";
            }

            return json + " }";
        }

        private static string ValidTemplate(string missionId)
        {
            return string.Join("\n", new[]
            {
                "schemaVersion: \"tb.mission_template.v2.2\"",
                $"missionId: \"{missionId}\"",
                "missionTitle: \"Layout Mission\"",
                "",
                "generationMeta:",
                "  initialSeed: 42",
                "  effectiveSeed: 0",
                "  generationTimeout: 45",
                "  maxRetries: 5",
                "",
                "spatialConstraints:",
                "  worldBounds: [64, 64]",
                "  pixelsPerUnit: 128",
                "  tacticalTheme: \"urban_cqb\"",
                "  bspConstraints:",
                "    minRoomSize: [4, 4]",
                "    maxRoomSize: [12, 12]",
                "    corridorWidth: 2",
                "    forceRoomAdjacency: true",
                "",
                "tacticalRules:",
                "  noiseAlertThreshold: 0.15",
                "  strictNavigationPolicy: true",
                "  enforcePostLayoutPlacement: true",
                "  acousticOcclusion:",
                "    wallMultiplier: 2.5",
                "    doorPenalty: 1.2",
                "",
                "actorRoster:",
                "  - id: \"OP_01\"",
                "    type: \"Operative\"",
                "    countRange: [2, 4]",
                "    navigationPolicy: \"FullAccess\"",
                "    placementPolicy: \"EntryPointOnly\"",
                "",
                "objectives:",
                "  primary:",
                "    - id: \"OBJ_MAIN\"",
                "      type: \"SecureRoom\"",
                "      requiresLayoutGraph: true",
                "      targetRoomTag: \"security_vault\"",
                ""
            });
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ToRepoPath(string path)
        {
            return Path.GetRelativePath(ProjectRoot(), path).Replace('\\', '/');
        }
    }
}
