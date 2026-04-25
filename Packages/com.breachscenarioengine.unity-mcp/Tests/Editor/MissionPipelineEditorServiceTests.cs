using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
                "schemaVersion: \"tb.mission_template.v2.3\"",
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
        public void ValidateTemplate_WithUnknownAuthoringFields_FailsSchemaValidation()
        {
            var templatePath = Path.Combine(_testRoot, "unknown-fields.template.yaml");
            File.WriteAllText(templatePath, ValidTemplate("VS84_UnknownFields") + string.Join("\n", new[]
            {
                "unexpectedTopLevel: true",
                "generationMeta:",
                "  unsupportedSeedMode: \"random\"",
                ""
            }));

            var (success, message) = MissionPipelineEditorService.Execute("validate_template", RawArgs(templatePath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f =>
                f.GetProperty("code").GetString() == "TPL_SCHEMA_INVALID" &&
                f.GetProperty("message").GetString().Contains("Unknown top-level field")));
            Assert.IsTrue(findings.Any(f =>
                f.GetProperty("code").GetString() == "TPL_SCHEMA_INVALID" &&
                f.GetProperty("message").GetString().Contains("Unknown generationMeta field")));
        }

        [Test]
        public void CompilePayload_ValidTemplate_WritesSchemaAlignedPayload()
        {
            var templatePath = Path.Combine(_testRoot, "valid.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            File.WriteAllText(templatePath, string.Join("\n", new[]
            {
                "schemaVersion: \"tb.mission_template.v2.3\"",
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
            Assert.AreEqual("bse.mission_payload.v2.3", root.GetProperty("header").GetProperty("schemaVersion").GetString());
            Assert.AreEqual("2.3", root.GetProperty("header").GetProperty("pipelineVersion").GetString());
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
            Assert.AreEqual("bse.mission_entities.v2.3", root.GetProperty("schemaVersion").GetString());
            Assert.AreEqual(layoutRevisionId, root.GetProperty("layoutRevisionId").GetString());
            Assert.AreEqual(2, root.GetProperty("actors").GetArrayLength());
            Assert.AreEqual(1, root.GetProperty("objectives").GetArrayLength());

            foreach (var entity in root.GetProperty("actors").EnumerateArray().Concat(root.GetProperty("objectives").EnumerateArray()))
            {
                Assert.False(string.IsNullOrWhiteSpace(entity.GetProperty("roomId").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(entity.GetProperty("navNodeId").GetString()));
                Assert.AreEqual(layoutRevisionId, entity.GetProperty("layoutRevisionId").GetString());
                var ownership = entity.GetProperty("ownership");
                Assert.AreEqual("manage_mission", ownership.GetProperty("owner").GetString());
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

        [Test]
        public void Verify_WithCurrentArtifacts_WritesMachineReadableSummary()
        {
            var templatePath = Path.Combine(_testRoot, "verify.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            File.WriteAllText(templatePath, ValidTemplate("VS93_VerifyMission"));

            var (compileSuccess, compileMessage) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));
            Assert.True(compileSuccess, compileMessage);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            var (layoutSuccess, layoutMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(layoutSuccess, layoutMessage);
            var (placementSuccess, placementMessage) = MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath));
            Assert.True(placementSuccess, placementMessage);

            var (success, message) = MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath));

            Assert.True(success, message);
            Assert.True(File.Exists(summaryPath));
            using var resultDoc = JsonDocument.Parse(message);
            Assert.AreEqual("PASS", resultDoc.RootElement.GetProperty("status").GetString());
            using var summaryDoc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            var summary = summaryDoc.RootElement;
            Assert.AreEqual("bse.verification_summary.v2.3", summary.GetProperty("schemaVersion").GetString());
            Assert.AreEqual("PASS", summary.GetProperty("status").GetString());
            Assert.AreEqual(2, summary.GetProperty("metrics").GetProperty("actorCount").GetInt32());
            Assert.AreEqual(1, summary.GetProperty("metrics").GetProperty("objectiveCount").GetInt32());
            Assert.AreEqual(0, summary.GetProperty("metrics").GetProperty("unreachableCriticalNodes").GetInt32());
        }

        [Test]
        public void Verify_WithMissingProfileRef_FailsWithStructuredFinding()
        {
            var templatePath = Path.Combine(_testRoot, "verify-missing-profile.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            File.WriteAllText(templatePath, ValidTemplate("VS92_ProfileMission"));

            var (compileSuccess, compileMessage) = MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath));
            Assert.True(compileSuccess, compileMessage);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            using (var payloadDoc = JsonDocument.Parse(File.ReadAllText(payloadPath)))
            {
                var payload = JsonNode.Parse(payloadDoc.RootElement.GetRawText()).AsObject();
                payload["profileRefs"]["performanceProfile"] = "Assets/Data/Mission/Profiles/DefinitelyMissingProfile.asset";
                File.WriteAllText(payloadPath, payload.ToJsonString());
            }

            var (layoutSuccess, layoutMessage) = MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath));
            Assert.True(layoutSuccess, layoutMessage);
            var (placementSuccess, placementMessage) = MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath));
            Assert.True(placementSuccess, placementMessage);

            var (success, message) = MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            Assert.AreEqual("FAIL", doc.RootElement.GetProperty("status").GetString());
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "PROFILE_REF_MISSING"));
        }

        [Test]
        public void Verify_WithDisconnectedObjective_FailsReachability()
        {
            var templatePath = Path.Combine(_testRoot, "verify-nav.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            File.WriteAllText(templatePath, ValidTemplate("VS91_NavMission"));

            Assert.True(MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath)).Success);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            Assert.True(MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath)).Success);
            using (var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath)))
            {
                var layout = JsonNode.Parse(layoutDoc.RootElement.GetRawText()).AsObject();
                layout["PortalGraph"]["portals"] = new JsonArray();
                File.WriteAllText(layoutPath, layout.ToJsonString());
            }

            Assert.True(MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath)).Success);

            var (success, message) = MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath));

            Assert.False(success);
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "NAV_OBJECTIVE_UNREACHABLE"));
        }

        [Test]
        public void WriteManifest_AfterPassVerification_WritesReplayManifestAndStampsPayload()
        {
            var templatePath = Path.Combine(_testRoot, "manifest.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            var manifestPath = Path.Combine(_testRoot, "generation_manifest.json");
            File.WriteAllText(templatePath, ValidTemplate("VS90_ManifestMission"));

            Assert.True(MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath)).Success);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            Assert.True(MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath)).Success);
            Assert.True(MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath)).Success);
            Assert.True(MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath)).Success);

            var (success, message) = MissionPipelineEditorService.Execute("write_manifest", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath, manifestPath));

            Assert.True(success, message);
            Assert.True(File.Exists(manifestPath));
            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var manifest = manifestDoc.RootElement;
            Assert.AreEqual("bse.generation_manifest.v2.3", manifest.GetProperty("schemaVersion").GetString());
            Assert.AreEqual("PASS", manifest.GetProperty("status").GetString());
            Assert.AreEqual(42, manifest.GetProperty("requestedSeed").GetInt32());
            Assert.AreEqual(42, manifest.GetProperty("effectiveSeed").GetInt32());
            Assert.AreEqual("manage_mission", manifest.GetProperty("lockOwner").GetString());
            Assert.AreEqual("PASS", manifest.GetProperty("verification").GetProperty("status").GetString());
            Assert.AreEqual(ToRepoPath(payloadPath), manifest.GetProperty("artifacts").GetProperty("payload").GetString());
            Assert.AreEqual(ToRepoPath(summaryPath), manifest.GetProperty("artifacts").GetProperty("verificationSummary").GetString());

            using var payloadDoc = JsonDocument.Parse(File.ReadAllText(payloadPath));
            var header = payloadDoc.RootElement.GetProperty("header");
            Assert.AreEqual(42, header.GetProperty("effectiveSeed").GetInt32());
            Assert.AreEqual(manifest.GetProperty("layoutRevisionId").GetString(), header.GetProperty("layoutRevisionId").GetString());
        }

        [Test]
        public void WriteManifest_WhenVerificationBlocked_DoesNotWriteManifestAndUpdatesState()
        {
            var templatePath = Path.Combine(_testRoot, "manifest-fail.template.yaml");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            var manifestPath = Path.Combine(_testRoot, "generation_manifest.json");
            File.WriteAllText(templatePath, ValidTemplate("VS89_ManifestFail"));
            File.WriteAllText(summaryPath, new JsonObject
            {
                ["schemaVersion"] = "bse.verification_summary.v2.3",
                ["pipelineVersion"] = "2.3",
                ["missionId"] = "VS89_ManifestFail",
                ["status"] = "FAIL",
                ["layoutRevisionId"] = "layout_failed",
                ["findings"] = new JsonArray(),
                ["metrics"] = new JsonObject()
            }.ToJsonString());

            var (success, message) = MissionPipelineEditorService.Execute("write_manifest", RawArgs(templatePath, verificationPath: summaryPath, manifestPath: manifestPath));

            Assert.False(success);
            Assert.False(File.Exists(manifestPath));
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "MISSION_VERIFICATION_FAILED"));
            using var stateDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_testRoot, "mission_state.json")));
            Assert.AreEqual("BLOCKED", stateDoc.RootElement.GetProperty("status").GetString());
            Assert.AreEqual("MISSION_VERIFICATION_FAILED", stateDoc.RootElement.GetProperty("lastFindingCode").GetString());
        }

        [Test]
        public void WriteManifest_WhenRetryBudgetExhausted_DoesNotWriteManifestAndUpdatesState()
        {
            var templatePath = Path.Combine(_testRoot, "manifest-retry-exhausted.template.yaml");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            var manifestPath = Path.Combine(_testRoot, "generation_manifest.json");
            File.WriteAllText(templatePath, ValidTemplate("VS83_RetryExhausted").Replace("  maxRetries: 5", "  maxRetries: 0"));
            File.WriteAllText(summaryPath, new JsonObject
            {
                ["schemaVersion"] = "bse.verification_summary.v2.3",
                ["pipelineVersion"] = "2.3",
                ["missionId"] = "VS83_RetryExhausted",
                ["status"] = "FAIL",
                ["layoutRevisionId"] = "layout_failed",
                ["findings"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["severity"] = "error",
                        ["code"] = "NAV_OBJECTIVE_UNREACHABLE",
                        ["message"] = "Objective is unreachable"
                    }
                },
                ["metrics"] = new JsonObject()
            }.ToJsonString());

            var (success, message) = MissionPipelineEditorService.Execute("write_manifest", RawArgs(templatePath, verificationPath: summaryPath, manifestPath: manifestPath));

            Assert.False(success);
            Assert.False(File.Exists(manifestPath));
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "RETRY_BUDGET_EXHAUSTED"));
            using var stateDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_testRoot, "mission_state.json")));
            Assert.AreEqual("FAILED", stateDoc.RootElement.GetProperty("status").GetString());
        }

        [Test]
        public void WriteManifest_WithRetryableVerificationFailure_RetriesFromLayoutAndWritesManifest()
        {
            var templatePath = Path.Combine(_testRoot, "manifest-retry.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            var manifestPath = Path.Combine(_testRoot, "generation_manifest.json");
            File.WriteAllText(templatePath, ValidTemplate("VS85_ManifestRetry"));

            Assert.True(MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath)).Success);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            Assert.True(MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath)).Success);
            using (var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath)))
            {
                var layout = JsonNode.Parse(layoutDoc.RootElement.GetRawText()).AsObject();
                layout["PortalGraph"]["portals"] = new JsonArray();
                File.WriteAllText(layoutPath, layout.ToJsonString());
            }

            Assert.True(MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath)).Success);
            Assert.False(MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath)).Success);

            var (success, message) = MissionPipelineEditorService.Execute("write_manifest", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath, manifestPath));

            Assert.True(success, message);
            using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var manifest = manifestDoc.RootElement;
            var retrySeeds = manifest.GetProperty("retrySeeds").EnumerateArray().Select(s => s.GetInt32()).ToArray();
            Assert.AreEqual(1, retrySeeds.Length);
            Assert.AreEqual(retrySeeds[0], manifest.GetProperty("effectiveSeed").GetInt32());
            Assert.AreEqual("PASS", manifest.GetProperty("verification").GetProperty("status").GetString());

            using var summaryDoc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            Assert.AreEqual("PASS", summaryDoc.RootElement.GetProperty("status").GetString());
            using var layoutAfterRetryDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
            Assert.AreEqual(retrySeeds[0], layoutAfterRetryDoc.RootElement.GetProperty("generationSeed").GetInt32());
            Assert.Greater(layoutAfterRetryDoc.RootElement.GetProperty("PortalGraph").GetProperty("portals").GetArrayLength(), 0);
        }

        [Test]
        public void WriteManifest_WhenLockExists_ReturnsLockConflict()
        {
            var templatePath = Path.Combine(_testRoot, "manifest-lock.template.yaml");
            var payloadPath = Path.Combine(_testRoot, "mission_payload.generated.json");
            var layoutPath = Path.Combine(_testRoot, "mission_layout.generated.json");
            var entitiesPath = Path.Combine(_testRoot, "mission_entities.generated.json");
            var summaryPath = Path.Combine(_testRoot, "verification_summary.json");
            var manifestPath = Path.Combine(_testRoot, "generation_manifest.json");
            File.WriteAllText(templatePath, ValidTemplate("VS88_ManifestLock"));
            Assert.True(MissionPipelineEditorService.Execute("compile_payload", RawArgs(templatePath, payloadPath)).Success);
            RewritePayloadProfileRefsToTempAssets(payloadPath);
            Assert.True(MissionPipelineEditorService.Execute("generate_layout", RawArgs(templatePath, payloadPath, layoutPath)).Success);
            Assert.True(MissionPipelineEditorService.Execute("place_entities", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath)).Success);
            Assert.True(MissionPipelineEditorService.Execute("verify", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath)).Success);
            var lockPath = Path.Combine(_testRoot, ".generation.lock");
            File.WriteAllText(lockPath, new JsonObject
            {
                ["missionId"] = "VS88_ManifestLock",
                ["jobId"] = "held",
                ["lockOwner"] = "manage_mission",
                ["startedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["updatedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["currentStep"] = "write_manifest",
                ["processId"] = 1
            }.ToJsonString());

            var (success, message) = MissionPipelineEditorService.Execute("write_manifest", RawArgs(templatePath, payloadPath, layoutPath, entitiesPath, summaryPath, manifestPath));

            Assert.False(success);
            Assert.True(File.Exists(lockPath));
            using var doc = JsonDocument.Parse(message);
            var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToArray();
            Assert.IsTrue(findings.Any(f => f.GetProperty("code").GetString() == "GENERATION_LOCK_CONFLICT"));
        }

        [Test]
        public void CleanupGenerationLock_WhenLockIsStale_RemovesMissionLock()
        {
            var templatePath = Path.Combine(_testRoot, "cleanup-lock.template.yaml");
            File.WriteAllText(templatePath, ValidTemplate("VS82_CleanupLock"));
            var lockPath = Path.Combine(_testRoot, ".generation.lock");
            File.WriteAllText(lockPath, new JsonObject
            {
                ["missionId"] = "VS82_CleanupLock",
                ["jobId"] = "stale",
                ["lockOwner"] = "manage_mission",
                ["startedAtUtc"] = DateTime.UtcNow.AddHours(-2).ToString("O"),
                ["updatedAtUtc"] = DateTime.UtcNow.AddHours(-2).ToString("O"),
                ["currentStep"] = "verify",
                ["processId"] = 1
            }.ToJsonString());

            var (success, message) = MissionPipelineEditorService.Execute("cleanup_generation_lock", RawArgs(templatePath));

            Assert.True(success, message);
            Assert.False(File.Exists(lockPath));
            using var stateDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_testRoot, "mission_state.json")));
            Assert.AreEqual("IDLE", stateDoc.RootElement.GetProperty("status").GetString());
        }

        [Test]
        public void ManageMission_ThroughBridgeExecute_RoutesToMissionService()
        {
            var templatePath = Path.Combine(_testRoot, "bridge-route.template.yaml");
            File.WriteAllText(templatePath, ValidTemplate("VS87_BridgeRoute"));
            var execute = typeof(McpBridgeProcessor).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(execute);

            var result = (ValueTuple<bool, string>)execute.Invoke(null, new object[]
            {
                "manage_mission",
                RawArgs(templatePath)
            });

            Assert.True(result.Item1, result.Item2);
            using var doc = JsonDocument.Parse(result.Item2);
            Assert.AreEqual("PASS", doc.RootElement.GetProperty("status").GetString());
            Assert.AreEqual("VS87_BridgeRoute", doc.RootElement.GetProperty("missionId").GetString());
        }

        [Test]
        public void ProjectCapabilities_ReportsAllMissionActionsSupported()
        {
            var execute = typeof(McpBridgeProcessor).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(execute);

            var result = (ValueTuple<bool, string>)execute.Invoke(null, new object[]
            {
                "project.capabilities",
                "{}"
            });

            Assert.True(result.Item1, result.Item2);
            using var doc = JsonDocument.Parse(result.Item2);
            var missionActions = doc.RootElement.GetProperty("capabilities")
                .EnumerateArray()
                .Where(c => c.GetProperty("tool").GetString() == "manage_mission")
                .ToArray();
            var expected = new[]
            {
                "validate_template",
                "compile_payload",
                "generate_layout",
                "place_entities",
                "verify",
                "write_manifest"
            };

            foreach (var action in expected)
            {
                var capability = missionActions.Single(c => c.GetProperty("action").GetString() == action);
                Assert.True(capability.GetProperty("supported").GetBoolean(), action);
            }
        }

        [Test]
        public void Pipeline_WithDefaultMissionPaths_CreatesExpectedArtifacts()
        {
            var missionId = "VS86_DefaultPaths";
            var missionDir = Path.Combine(ProjectRoot(), "UserMissionSources", "missions", missionId);
            if (Directory.Exists(missionDir))
            {
                Directory.Delete(missionDir, recursive: true);
            }

            try
            {
                Directory.CreateDirectory(missionDir);
                var templatePath = Path.Combine(missionDir, "mission_design.template.yaml");
                File.WriteAllText(templatePath, ValidTemplate(missionId));
                var raw = "{ \"missionId\": \"" + missionId + "\" }";

                Assert.True(MissionPipelineEditorService.Execute("compile_payload", raw).Success);
                RewritePayloadProfileRefsToTempAssets(Path.Combine(missionDir, "mission_payload.generated.json"));
                Assert.True(MissionPipelineEditorService.Execute("generate_layout", raw).Success);
                Assert.True(MissionPipelineEditorService.Execute("place_entities", raw).Success);
                Assert.True(MissionPipelineEditorService.Execute("verify", raw).Success);
                var (success, message) = MissionPipelineEditorService.Execute("write_manifest", raw);

                Assert.True(success, message);
                var expectedArtifacts = new[]
                {
                    "mission_payload.generated.json",
                    "mission_compile_report.json",
                    "mission_layout.generated.json",
                    "mission_entities.generated.json",
                    "mission_state.json",
                    "verification_summary.json",
                    "generation_manifest.json"
                };
                foreach (var artifact in expectedArtifacts)
                {
                    Assert.True(File.Exists(Path.Combine(missionDir, artifact)), artifact);
                }

                using var doc = JsonDocument.Parse(message);
                var artifacts = doc.RootElement.GetProperty("artifacts").EnumerateArray().Select(a => a.GetString()).ToArray();
                Assert.Contains("UserMissionSources/missions/VS86_DefaultPaths/generation_manifest.json", artifacts);
            }
            finally
            {
                if (Directory.Exists(missionDir))
                {
                    Directory.Delete(missionDir, recursive: true);
                }
            }
        }

        private static string RawArgs(string templatePath, string payloadPath = null, string layoutPath = null, string entitiesPath = null, string verificationPath = null, string manifestPath = null)
        {
            var template = ToRepoPath(templatePath);
            if (payloadPath == null && layoutPath == null && entitiesPath == null && verificationPath == null && manifestPath == null)
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

            if (verificationPath != null)
            {
                json += ", \"verificationPath\": \"" + ToRepoPath(verificationPath) + "\"";
            }

            if (manifestPath != null)
            {
                json += ", \"manifestPath\": \"" + ToRepoPath(manifestPath) + "\"";
            }

            return json + " }";
        }

        private void RewritePayloadProfileRefsToTempAssets(string payloadPath)
        {
            var profileRoot = Path.Combine(_testRoot, "Profiles");
            Directory.CreateDirectory(profileRoot);
            var refs = new[]
            {
                ("tacticalThemeProfile", "TacticalThemeProfile.asset"),
                ("performanceProfile", "PerformanceProfile.asset"),
                ("renderProfile", "RenderProfile.asset"),
                ("navigationPolicy", "NavigationPolicy.asset"),
                ("tacticalDensityProfile", "TacticalDensityProfile.asset"),
                ("addressablesCatalogProfile", "AddressablesCatalogProfile.asset")
            };

            using var payloadDoc = JsonDocument.Parse(File.ReadAllText(payloadPath));
            var payload = JsonNode.Parse(payloadDoc.RootElement.GetRawText()).AsObject();
            var profileRefs = payload["profileRefs"].AsObject();
            foreach (var (key, name) in refs)
            {
                var path = Path.Combine(profileRoot, name);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "%YAML 1.1\n");
                }

                profileRefs[key] = ToRepoPath(path);
            }

            File.WriteAllText(payloadPath, payload.ToJsonString());
        }

        private static string ValidTemplate(string missionId)
        {
            return string.Join("\n", new[]
            {
                "schemaVersion: \"tb.mission_template.v2.3\"",
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


