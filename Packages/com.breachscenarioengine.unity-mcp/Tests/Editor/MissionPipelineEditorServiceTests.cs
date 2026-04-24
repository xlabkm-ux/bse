using System.IO;
using System.Linq;
using System.Text.Json;
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

        private static string RawArgs(string templatePath, string payloadPath = null)
        {
            var template = ToRepoPath(templatePath);
            if (payloadPath == null)
            {
                return "{ \"templatePath\": \"" + template + "\" }";
            }

            return "{ \"templatePath\": \"" + template + "\", \"payloadPath\": \"" + ToRepoPath(payloadPath) + "\" }";
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
