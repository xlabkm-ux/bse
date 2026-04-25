using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#nullable enable
#pragma warning disable 8602,8604,8619,1717

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class MissionPipelineEditorService
    {
        private const string PipelineVersion = "2.3";
        private const string TemplateSchemaVersion = "tb.mission_template.v2.3";
        private const string PayloadSchemaVersion = "bse.mission_payload.v2.3";
        private const string LockOwner = "manage_mission";
        private static readonly HashSet<string> TacticalThemes = new(StringComparer.Ordinal) { "urban_cqb", "stealth_facility", "residential" };
        private static readonly HashSet<string> NavigationPolicies = new(StringComparer.Ordinal) { "FullAccess", "StaticGuard", "CanOpenDoors", "Immobilized" };
        private static readonly HashSet<string> PlacementPolicies = new(StringComparer.Ordinal) { "EntryPointOnly", "PostLayout_TaggedRoom", "PostLayout_AnyRoom", "SecureRoomOnly" };
        private static readonly HashSet<string> RetryableVerificationCodes = new(StringComparer.Ordinal)
        {
            "NAV_BREACHPOINT_UNREACHABLE",
            "NAV_OBJECTIVE_UNREACHABLE",
            "LAYOUT_GENERATION_FAILED",
            "TB-AUD-003",
            "TACTICAL_DENSITY_IMPOSSIBLE_BUDGET"
        };

        public static (bool Success, string Message) Execute(string action, string raw)
        {
            return action switch
            {
                "validate_template" => ValidateTemplate(raw),
                "compile_payload" => CompilePayload(raw),
                "generate_layout" => GenerateLayout(raw),
                "place_entities" => PlaceEntities(raw),
                "verify" => Verify(raw),
                "write_manifest" => WriteManifest(raw),
                "cleanup_generation_lock" => CleanupGenerationLock(raw),
                _ => (false, MissionResultJson("FAIL", "", Array.Empty<string>(), new[]
                {
                    Finding("error", "MISSION_ACTION_UNSUPPORTED", $"Unsupported manage_mission action: {action}")
                }))
            };
        }

        private static (bool Success, string Message) ValidateTemplate(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error ?? string.Empty);
            }

            var parse = MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings);
            var artifacts = new[] { ToRepoPath(context.TemplatePath!) };
            var status = parse ? "PASS" : "FAIL";
            return (parse, MissionResultJson(status, template?.MissionId ?? context.MissionId ?? "", artifacts, findings));
        }

        private static (bool Success, string Message) CompilePayload(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            if (!MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings))
            {
                return (false, MissionResultJson("FAIL", context.MissionId ?? template?.MissionId ?? "", new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var payloadPath = ResolveMissionArtifactPath(raw, "payloadPath", missionDir, "mission_payload.generated.json");
            if (!IsUnderProjectRoot(payloadPath))
            {
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, new[]
                {
                    Finding("error", "PAYLOAD_PATH_OUTSIDE_PROJECT", "payloadPath must stay inside the Unity project root")
                }));
            }

            var reportPath = Path.Combine(missionDir, "mission_compile_report.json");
            var statePath = MissionStatePath(missionDir);
            var artifacts = new[] { ToRepoPath(payloadPath), ToRepoPath(reportPath), ToRepoPath(statePath) };

            if (!GenerationLockSet.TryAcquire(template.MissionId, missionDir, "compile_payload", out var generationLock, out var conflictPath, out var conflictFinding))
            {
                findings.Add(conflictFinding);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            var lockHandle = generationLock ?? throw new InvalidOperationException("Generation lock acquisition failed.");
            using (lockHandle)
            {
                WriteMissionState(missionDir, template.MissionId, "VALIDATING", "compile_payload", "", LastFindingCode(findings), lockHandle.JobId);
            Directory.CreateDirectory(missionDir);
            Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);

            var payloadNode = BuildPayloadNode(template!);
            var payloadFindings = ValidatePayloadShape(payloadNode);
            findings.AddRange(payloadFindings);
            if (payloadFindings.Any(f => f["severity"]?.GetValue<string>() == "error"))
            {
                WriteMissionState(missionDir, template.MissionId, "BLOCKED", "compile_payload", "", LastFindingCode(findings), generationLock.JobId);
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            File.WriteAllText(payloadPath, payloadNode.ToJsonString() + Environment.NewLine);
            File.WriteAllText(reportPath, BuildCompileReportNode(template.MissionId, ToRepoPath(context.TemplatePath!), ToRepoPath(payloadPath), findings).ToJsonString() + Environment.NewLine);
            WriteMissionState(missionDir, template.MissionId, "COMPILED", "compile_payload", "", LastFindingCode(findings), generationLock.JobId);
            AssetDatabase.Refresh();

            return (true, MissionResultJson("PASS", template.MissionId, artifacts, findings));
            }
        }

        private static (bool Success, string Message) GenerateLayout(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            if (!MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings))
            {
                return (false, MissionResultJson("FAIL", context.MissionId ?? template?.MissionId ?? "", new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var layoutPath = ResolveMissionArtifactPath(raw, "layoutPath", missionDir, "mission_layout.generated.json");
            var statePath = MissionStatePath(missionDir);
            if (!IsUnderProjectRoot(layoutPath))
            {
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, new[]
                {
                    Finding("error", "LAYOUT_PATH_OUTSIDE_PROJECT", "layoutPath must stay inside the Unity project root")
                }));
            }

            var artifacts = new List<string> { ToRepoPath(layoutPath), ToRepoPath(statePath) };
            if (!GenerationLockSet.TryAcquire(template.MissionId, missionDir, "generate_layout", out var generationLock, out var conflictPath, out var conflictFinding))
            {
                findings.Add(conflictFinding);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            var lockHandle = generationLock ?? throw new InvalidOperationException("Generation lock acquisition failed.");
            using (lockHandle)
            {
            WriteMissionState(missionDir, template.MissionId, "VALIDATING", "generate_layout", "", LastFindingCode(findings), lockHandle.JobId);
            Directory.CreateDirectory(missionDir);
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);

            var payloadPath = ResolveMissionArtifactPath(raw, "payloadPath", missionDir, "mission_payload.generated.json");
            var payloadNode = File.Exists(payloadPath) && IsUnderProjectRoot(payloadPath) ? ReadJsonObject(payloadPath) : null;
            var generationSeed = PayloadGenerationSeed(payloadNode, template.InitialSeed);
            var layoutNode = BuildLayoutNode(template!, generationSeed, File.Exists(payloadPath) && IsUnderProjectRoot(payloadPath) ? ToRepoPath(payloadPath) : "");
            if (File.Exists(payloadPath) && IsUnderProjectRoot(payloadPath))
            {
                TryStampPayloadLayoutRevision(payloadPath, layoutNode["layoutRevisionId"]!.GetValue<string>());
            }

            File.WriteAllText(layoutPath, layoutNode.ToJsonString() + Environment.NewLine);
            WriteMissionState(missionDir, template.MissionId, "LAYOUT_GENERATED", "generate_layout", layoutNode["layoutRevisionId"]!.GetValue<string>(), LastFindingCode(findings), generationLock.JobId);
            AssetDatabase.Refresh();

            if (File.Exists(payloadPath) && IsUnderProjectRoot(payloadPath))
            {
                artifacts.Add(ToRepoPath(payloadPath));
            }

            return (true, MissionResultJson("PASS", template.MissionId, artifacts, findings));
            }
        }

        private static (bool Success, string Message) PlaceEntities(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            if (!MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings))
            {
                return (false, MissionResultJson("FAIL", context.MissionId ?? template?.MissionId ?? "", new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var layoutPath = ResolveMissionArtifactPath(raw, "layoutPath", missionDir, "mission_layout.generated.json");
            var statePath = MissionStatePath(missionDir);
            if (!IsUnderProjectRoot(layoutPath))
            {
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, new[]
                {
                    Finding("error", "LAYOUT_PATH_OUTSIDE_PROJECT", "layoutPath must stay inside the Unity project root")
                }));
            }

            if (!File.Exists(layoutPath))
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_NO_LAYOUT_GRAPH", "place_entities requires a current LayoutGraph from generate_layout"));
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(layoutPath) }, findings));
            }

            JsonObject? layoutNode;
            try
            {
                layoutNode = JsonNode.Parse(File.ReadAllText(layoutPath)) as JsonObject;
            }
            catch
            {
                layoutNode = null;
            }

            var layoutRevisionId = layoutNode?["layoutRevisionId"]?.GetValue<string>() ?? "";
            var layoutMissionId = layoutNode?["missionId"]?.GetValue<string>() ?? "";
            if (layoutNode == null || layoutNode["LayoutGraph"] is not JsonObject || layoutNode["RoomGraph"] is not JsonObject)
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_NO_LAYOUT_GRAPH", "place_entities requires a readable LayoutGraph and RoomGraph from generate_layout"));
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(layoutPath) }, findings));
            }

            var expectedRevisionId = ComputeLayoutRevisionId(template!, LayoutGenerationSeed(layoutNode, template.InitialSeed));
            if (!string.Equals(layoutMissionId, template.MissionId, StringComparison.Ordinal) ||
                !string.Equals(layoutRevisionId, expectedRevisionId, StringComparison.Ordinal))
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_STALE_LAYOUT_GRAPH", "place_entities requires the current layoutRevisionId from generate_layout"));
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(layoutPath) }, findings));
            }

            if (LayoutRooms(layoutNode).Count == 0)
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_NO_LAYOUT_GRAPH", "place_entities requires at least one room in RoomGraph"));
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(layoutPath) }, findings));
            }

            var entitiesPath = ResolveMissionArtifactPath(raw, "entitiesPath", missionDir, "mission_entities.generated.json");
            if (!IsUnderProjectRoot(entitiesPath))
            {
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(layoutPath) }, new[]
                {
                    Finding("error", "ENTITIES_PATH_OUTSIDE_PROJECT", "entitiesPath must stay inside the Unity project root")
                }));
            }

            var artifacts = new[] { ToRepoPath(layoutPath), ToRepoPath(entitiesPath), ToRepoPath(statePath) };
            if (!GenerationLockSet.TryAcquire(template.MissionId, missionDir, "place_entities", out var generationLock, out var conflictPath, out var conflictFinding))
            {
                findings.Add(conflictFinding);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            var lockHandle = generationLock ?? throw new InvalidOperationException("Generation lock acquisition failed.");
            using (lockHandle)
            {
            WriteMissionState(missionDir, template.MissionId, "LAYOUT_GENERATED", "place_entities", layoutRevisionId, LastFindingCode(findings), lockHandle.JobId);
            var placementNode = BuildEntityPlacementNode(template, layoutNode);
            Directory.CreateDirectory(Path.GetDirectoryName(entitiesPath)!);
            File.WriteAllText(entitiesPath, placementNode.ToJsonString() + Environment.NewLine);
            WriteMissionState(missionDir, template.MissionId, "ENTITIES_PLACED", "place_entities", layoutRevisionId, LastFindingCode(findings), generationLock.JobId);
            AssetDatabase.Refresh();

            return (true, MissionResultJson("PASS", template.MissionId, artifacts, findings));
            }
        }

        private static (bool Success, string Message) Verify(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            if (!MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings))
            {
                return (false, MissionResultJson("FAIL", context.MissionId ?? template?.MissionId ?? "", new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var payloadPath = ResolveMissionArtifactPath(raw, "payloadPath", missionDir, "mission_payload.generated.json");
            var layoutPath = ResolveMissionArtifactPath(raw, "layoutPath", missionDir, "mission_layout.generated.json");
            var entitiesPath = ResolveMissionArtifactPath(raw, "entitiesPath", missionDir, "mission_entities.generated.json");
            var summaryPath = ResolveMissionArtifactPath(raw, "verificationPath", missionDir, "verification_summary.json");
            var statePath = MissionStatePath(missionDir);
            if (!IsUnderProjectRoot(payloadPath) || !IsUnderProjectRoot(layoutPath) || !IsUnderProjectRoot(entitiesPath) || !IsUnderProjectRoot(summaryPath))
            {
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, new[]
                {
                    Finding("error", "VERIFICATION_PATH_OUTSIDE_PROJECT", "verification artifact paths must stay inside the Unity project root")
                }));
            }

            var artifacts = new List<string> { ToRepoPath(payloadPath), ToRepoPath(layoutPath), ToRepoPath(entitiesPath), ToRepoPath(summaryPath), ToRepoPath(statePath) };
            if (!GenerationLockSet.TryAcquire(template.MissionId, missionDir, "verify", out var generationLock, out var conflictPath, out var conflictFinding))
            {
                findings.Add(conflictFinding);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            var lockHandle = generationLock ?? throw new InvalidOperationException("Generation lock acquisition failed.");
            using (lockHandle)
            {
            WriteMissionState(missionDir, template.MissionId, "VERIFYING", "verify", "", LastFindingCode(findings), lockHandle.JobId);
            var layoutNode = ReadJsonObject(layoutPath);
            var entitiesNode = ReadJsonObject(entitiesPath);
            var payloadNode = ReadJsonObject(payloadPath);
            var metrics = EmptyVerificationMetrics();
            var expectedRevisionId = layoutNode == null
                ? ComputeLayoutRevisionId(template!)
                : ComputeLayoutRevisionId(template!, LayoutGenerationSeed(layoutNode, template!.InitialSeed));

            if (payloadNode == null)
            {
                findings.Add(Finding("error", "PAYLOAD_FILE_MISSING", "verify requires mission_payload.generated.json from compile_payload", ToRepoPath(payloadPath)));
            }

            if (layoutNode == null || layoutNode["LayoutGraph"] is not JsonObject || layoutNode["RoomGraph"] is not JsonObject)
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_NO_LAYOUT_GRAPH", "verify requires a readable LayoutGraph and RoomGraph from generate_layout", ToRepoPath(layoutPath)));
            }

            if (entitiesNode == null || entitiesNode["actors"] is not JsonArray || entitiesNode["objectives"] is not JsonArray)
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_NO_ENTITY_PLACEMENT", "verify requires mission_entities.generated.json from place_entities", ToRepoPath(entitiesPath)));
            }

            if (layoutNode != null && !string.Equals(layoutNode["layoutRevisionId"]?.GetValue<string>(), expectedRevisionId, StringComparison.Ordinal))
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_STALE_LAYOUT_GRAPH", "verify requires the current layoutRevisionId from generate_layout", ToRepoPath(layoutPath)));
            }

            if (entitiesNode != null && !string.Equals(entitiesNode["layoutRevisionId"]?.GetValue<string>(), expectedRevisionId, StringComparison.Ordinal))
            {
                findings.Add(Finding("error", "ORDER_VIOLATION_STALE_ENTITY_PLACEMENT", "verify requires entity placement generated from the current layoutRevisionId", ToRepoPath(entitiesPath)));
            }

            if (payloadNode != null)
            {
                ValidateProfileRefs(payloadNode, findings);
            }

            if (layoutNode != null && entitiesNode != null)
            {
                metrics = ComputeVerificationMetrics(layoutNode, entitiesNode, findings);
            }

            var status = findings.Any(f => string.Equals(f["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal)) ? "FAIL" : "PASS";
            var summary = BuildVerificationSummaryNode(template.MissionId, status, layoutNode?["layoutRevisionId"]?.GetValue<string>() ?? expectedRevisionId, artifacts, findings, metrics);
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
            File.WriteAllText(summaryPath, summary.ToJsonString() + Environment.NewLine);
            var missionStateStatus = status == "PASS"
                ? "PASS"
                : string.Equals(summary["retryClass"]?.GetValue<string>(), "RETRYABLE_FAIL", StringComparison.Ordinal)
                    ? "FAILED"
                    : "BLOCKED";
            WriteMissionState(missionDir, template.MissionId, missionStateStatus, "verify", summary["layoutRevisionId"]?.GetValue<string>() ?? expectedRevisionId, LastFindingCode(findings), generationLock.JobId);
            AssetDatabase.Refresh();

            return (status == "PASS", MissionResultJson(status, template.MissionId, artifacts, findings, metrics, new JsonObject
            {
                ["currentStep"] = "verify",
                ["jobId"] = generationLock.JobId
            }));
            }
        }

        private static (bool Success, string Message) WriteManifest(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            if (!MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out var findings))
            {
                return (false, MissionResultJson("FAIL", context.MissionId ?? template?.MissionId ?? "", new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var payloadPath = ResolveMissionArtifactPath(raw, "payloadPath", missionDir, "mission_payload.generated.json");
            var layoutPath = ResolveMissionArtifactPath(raw, "layoutPath", missionDir, "mission_layout.generated.json");
            var entitiesPath = ResolveMissionArtifactPath(raw, "entitiesPath", missionDir, "mission_entities.generated.json");
            var summaryPath = ResolveMissionArtifactPath(raw, "verificationPath", missionDir, "verification_summary.json");
            var manifestPath = ResolveMissionArtifactPath(raw, "manifestPath", missionDir, "generation_manifest.json");
            var statePath = MissionStatePath(missionDir);
            if (!IsUnderProjectRoot(payloadPath) || !IsUnderProjectRoot(layoutPath) || !IsUnderProjectRoot(entitiesPath) ||
                !IsUnderProjectRoot(summaryPath) || !IsUnderProjectRoot(manifestPath) || !IsUnderProjectRoot(statePath))
            {
                return (false, MissionResultJson("FAIL", template!.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, new[]
                {
                    Finding("error", "MANIFEST_PATH_OUTSIDE_PROJECT", "manifest artifact paths must stay inside the Unity project root")
                }));
            }

            var artifacts = new List<string>
            {
                ToRepoPath(payloadPath),
                ToRepoPath(Path.Combine(missionDir, "mission_compile_report.json")),
                ToRepoPath(layoutPath),
                ToRepoPath(entitiesPath),
                ToRepoPath(summaryPath),
                ToRepoPath(statePath),
                ToRepoPath(manifestPath)
            };

            if (!GenerationLockSet.TryAcquire(template.MissionId, missionDir, "write_manifest", out var generationLock, out var conflictPath, out var conflictFinding))
            {
                findings.Add(conflictFinding!);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            using (generationLock!)
            {
            var summaryNode = ReadJsonObject(summaryPath);
            if (summaryNode == null)
            {
                findings.Add(Finding("error", "VERIFICATION_SUMMARY_MISSING", "write_manifest requires verification_summary.json from verify", ToRepoPath(summaryPath)));
                WriteMissionState(missionDir, template.MissionId, "BLOCKED", "write_manifest", "", LastFindingCode(findings), generationLock.JobId);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

            var retrySeeds = ReadRetrySeeds(raw);
            var existingManifest = ReadJsonObject(manifestPath);
            if (retrySeeds.Count == 0 && existingManifest?["retrySeeds"] is JsonArray existingRetrySeeds)
            {
                retrySeeds = existingRetrySeeds.OfType<JsonValue>().Select(v => v.GetValue<int>()).ToList();
            }

            var verificationStatus = summaryNode["status"]?.GetValue<string>() ?? "";
            var manifestStatus = "PASS";
            if (!string.Equals(verificationStatus, "PASS", StringComparison.Ordinal))
            {
                WriteMissionState(missionDir, template.MissionId, "RETRYING", "write_manifest", summaryNode["layoutRevisionId"]?.GetValue<string>() ?? "", VerificationFailureCode(summaryNode), generationLock.JobId);
                var retryResult = TryRunRetryPipeline(template!, payloadPath, layoutPath, entitiesPath, summaryPath, summaryNode, artifacts, retrySeeds, findings);
                summaryNode = retryResult.SummaryNode;
                retrySeeds = retryResult.RetrySeeds;
                verificationStatus = summaryNode["status"]?.GetValue<string>() ?? "";
                if (!retryResult.Success)
                {
                    findings.Add(Finding("error", "MISSION_VERIFICATION_FAILED", "generation_manifest.json is blocked until verification status is PASS", ToRepoPath(summaryPath)));
                    manifestStatus = VerificationFailureIsRetryable(summaryNode) ? "FAILED" : "BLOCKED";
                    WriteMissionState(missionDir, template.MissionId, manifestStatus, "write_manifest", summaryNode["layoutRevisionId"]?.GetValue<string>() ?? "", LastFindingCode(findings), generationLock.JobId);
                    return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
                }

                WriteMissionState(missionDir, template.MissionId, "PASS", "verify", summaryNode["layoutRevisionId"]?.GetValue<string>() ?? "", LastFindingCode(findings), generationLock.JobId);
            }

            var verificationPassed = string.Equals(manifestStatus, "PASS", StringComparison.Ordinal);
            var acceptedSeed = verificationPassed ? AcceptedGenerationSeed(template!, retrySeeds, summaryNode) : 0;
            var layoutRevisionId = summaryNode["layoutRevisionId"]?.GetValue<string>() ?? ComputeLayoutRevisionId(template!, verificationPassed ? acceptedSeed : template.InitialSeed);
            if (verificationPassed)
            {
                var expectedRevisionId = ComputeLayoutRevisionId(template!, acceptedSeed);
                if (!string.Equals(layoutRevisionId, expectedRevisionId, StringComparison.Ordinal))
                {
                    findings.Add(Finding("error", "ORDER_VIOLATION_STALE_LAYOUT_GRAPH", "write_manifest requires the current layoutRevisionId from verification", ToRepoPath(summaryPath)));
                    WriteMissionState(missionDir, template.MissionId, "BLOCKED", "write_manifest", layoutRevisionId, LastFindingCode(findings), generationLock.JobId);
                    return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
                }
            }

            if (!MissionStateAllowsManifest(missionDir, summaryNode, out var stateFinding))
            {
                findings.Add(stateFinding!);
                WriteMissionState(missionDir, template.MissionId, "BLOCKED", "write_manifest", layoutRevisionId, LastFindingCode(findings), generationLock.JobId);
                return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
            }

                var payloadNode = ReadJsonObject(payloadPath);
                if (verificationPassed && payloadNode == null)
                {
                    findings.Add(Finding("error", "PAYLOAD_FILE_MISSING", "write_manifest requires mission_payload.generated.json from compile_payload", ToRepoPath(payloadPath)));
                    WriteMissionState(missionDir, template.MissionId, "BLOCKED", "write_manifest", layoutRevisionId, LastFindingCode(findings), generationLock.JobId);
                    return (false, MissionResultJson("FAIL", template.MissionId, artifacts, findings));
                }

                payloadNode ??= new JsonObject
                {
                    ["profileRefs"] = template.ProfileRefs()
                };
                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

                var effectiveSeed = 0;
                if (verificationPassed)
                {
                    effectiveSeed = ExistingAcceptedEffectiveSeed(existingManifest);
                    if (effectiveSeed <= 0)
                    {
                        effectiveSeed = acceptedSeed;
                    }

                    StampPayloadReplayFields(payloadPath, payloadNode, effectiveSeed, layoutRevisionId);
                }

                var manifestNode = BuildGenerationManifestNode(template, manifestStatus, effectiveSeed, retrySeeds, layoutRevisionId, payloadNode, summaryNode, artifacts);
                File.WriteAllText(manifestPath, manifestNode.ToJsonString() + Environment.NewLine);
                WriteMissionState(missionDir, template.MissionId, "PASS", "write_manifest", layoutRevisionId, LastFindingCode(findings), generationLock.JobId);
                AssetDatabase.Refresh();

                return (verificationPassed, MissionResultJson(verificationPassed ? "PASS" : "FAIL", template.MissionId, artifacts, findings, summaryNode["metrics"] as JsonObject, new JsonObject
                {
                    ["currentStep"] = "write_manifest",
                    ["jobId"] = generationLock.JobId
                }));
            }
        }

        private static (bool Success, string? MissionId, string? TemplatePath, string? Error) ResolveContext(string raw)
        {
            var missionId = JsonArgString(raw, "missionId");
            var templateArg = JsonArgString(raw, "templatePath");
            var projectRoot = ProjectRoot();

            string? templatePath = null;
            if (!string.IsNullOrWhiteSpace(templateArg))
            {
                templatePath = ToAbsoluteProjectPath(templateArg!);
            }
            else if (!string.IsNullOrWhiteSpace(missionId))
            {
                templatePath = Path.Combine(projectRoot, "UserMissionSources", "missions", missionId!, "mission_design.template.yaml");
            }

            if (string.IsNullOrWhiteSpace(templatePath))
            {
                return (false, missionId, null, MissionResultJson("FAIL", missionId ?? "", Array.Empty<string>(), new[]
                {
                    Finding("error", "TPL_FILE_MISSING", "missionId or templatePath is required")
                }));
            }

            if (!IsUnderProjectRoot(templatePath!))
            {
                return (false, missionId, templatePath, MissionResultJson("FAIL", missionId ?? "", Array.Empty<string>(), new[]
                {
                    Finding("error", "TPL_PATH_OUTSIDE_PROJECT", "templatePath must stay inside the Unity project root")
                }));
            }

            if (!File.Exists(templatePath))
            {
                return (false, missionId, templatePath, MissionResultJson("FAIL", missionId ?? "", new[] { ToRepoPath(templatePath) }, new[]
                {
                    Finding("error", "TPL_FILE_MISSING", $"Template file not found: {ToRepoPath(templatePath)}")
                }));
            }

            return (true, missionId, templatePath, null);
        }

        private static (bool Success, string Message) CleanupGenerationLock(string raw)
        {
            var context = ResolveContext(raw);
            if (!context.Success)
            {
                return (false, context.Error!);
            }

            var missionDir = Path.GetDirectoryName(context.TemplatePath!)!;
            var missionId = context.MissionId ?? "";
            if (MissionTemplateModel.TryLoad(context.TemplatePath!, context.MissionId, out var template, out _))
            {
                missionId = template!.MissionId;
            }

            var lockPath = GenerationLockPath(missionDir);
            var artifacts = new[] { ToRepoPath(lockPath), ToRepoPath(MissionStatePath(missionDir)) };
            if (!File.Exists(lockPath))
            {
                return (true, MissionResultJson("PASS", missionId, artifacts, Array.Empty<JsonObject>()));
            }

            var lockNode = ReadJsonObject(lockPath);
            if (!IsStaleLock(lockNode))
            {
                return (false, MissionResultJson("FAIL", missionId, artifacts, new[]
                {
                    Finding("error", "GENERATION_LOCK_CONFLICT", "Active generation lock was not removed; cleanup only removes stale locks", ToRepoPath(lockPath))
                }));
            }

            File.Delete(lockPath);
            WriteMissionState(missionDir, missionId, "IDLE", "cleanup_generation_lock", lockNode?["layoutRevisionId"]?.GetValue<string>() ?? "", "GENERATION_LOCK_CLEANED", lockNode?["jobId"]?.GetValue<string>() ?? "");
            AssetDatabase.Refresh();
            return (true, MissionResultJson("PASS", missionId, artifacts, new[]
            {
                Finding("warning", "GENERATION_LOCK_CLEANED", "Stale mission generation lock was removed", ToRepoPath(lockPath))
            }));
        }

        private static JsonObject BuildPayloadNode(MissionTemplateModel template)
        {
            return new JsonObject
            {
                ["header"] = new JsonObject
                {
                    ["schemaVersion"] = PayloadSchemaVersion,
                    ["pipelineVersion"] = PipelineVersion,
                    ["missionId"] = template.MissionId,
                    ["missionTitle"] = template.MissionTitle,
                    ["initialSeed"] = template.InitialSeed,
                    ["effectiveSeed"] = template.EffectiveSeed
                },
                ["spatial"] = new JsonObject
                {
                    ["bounds"] = IntArrayNode(template.WorldBounds),
                    ["theme"] = template.TacticalTheme,
                    ["ppu"] = template.PixelsPerUnit,
                    ["bsp"] = new JsonObject
                    {
                        ["minRoomSize"] = IntArrayNode(template.MinRoomSize),
                        ["maxRoomSize"] = IntArrayNode(template.MaxRoomSize),
                        ["corridorWidth"] = template.CorridorWidth,
                        ["forceAdjacency"] = template.ForceRoomAdjacency
                    }
                },
                ["logic"] = new JsonObject
                {
                    ["noise"] = new JsonObject
                    {
                        ["threshold"] = template.NoiseAlertThreshold,
                        ["wallMultiplier"] = template.WallMultiplier,
                        ["doorPenalty"] = template.DoorPenalty
                    },
                    ["navigation"] = new JsonObject
                    {
                        ["strict"] = template.StrictNavigationPolicy
                    }
                },
                ["roster"] = new JsonArray(template.Actors.Select(a => (JsonNode?)new JsonObject
                {
                    ["actorId"] = a.Id,
                    ["type"] = a.Type,
                    ["policy"] = a.NavigationPolicy,
                    ["placementPolicy"] = a.PlacementPolicy,
                    ["count"] = a.NormalizedCount
                }).ToArray()),
                ["objectives"] = new JsonObject
                {
                    ["primary"] = new JsonArray(template.PrimaryObjectives.Select(ObjectiveNode).ToArray()),
                    ["secondary"] = new JsonArray(template.SecondaryObjectives.Select(ObjectiveNode).ToArray())
                },
                ["profileRefs"] = template.ProfileRefs()
            };
        }

        private static JsonNode? ObjectiveNode(MissionObjective objective)
        {
            var node = new JsonObject
            {
                ["id"] = objective.Id,
                ["type"] = objective.Type
            };
            if (objective.RequiresLayoutGraph.HasValue) node["requiresLayoutGraph"] = objective.RequiresLayoutGraph.Value;
            if (!string.IsNullOrWhiteSpace(objective.TargetRoomTag)) node["targetRoomTag"] = objective.TargetRoomTag;
            if (objective.Optional.HasValue) node["optional"] = objective.Optional.Value;
            return node;
        }

        private static JsonArray IntArrayNode(IReadOnlyList<int> values)
        {
            var array = new JsonArray();
            foreach (var value in values)
            {
                array.Add(value);
            }

            return array;
        }

        private static JsonObject BuildLayoutNode(MissionTemplateModel template)
        {
            return BuildLayoutNode(template, template.InitialSeed, "");
        }

        private static JsonObject BuildLayoutNode(MissionTemplateModel template, int generationSeed)
        {
            return BuildLayoutNode(template, generationSeed, "");
        }

        private static JsonObject BuildLayoutNode(MissionTemplateModel template, int generationSeed, string sourcePayloadPath)
        {
            var revisionId = ComputeLayoutRevisionId(template, generationSeed);
            return BspLayoutGenerator.Generate(new BspLayoutGenerator.Options
            {
                PipelineVersion = PipelineVersion,
                MissionId = template.MissionId,
                LayoutRevisionId = revisionId,
                RequestedSeed = template.InitialSeed,
                GenerationSeed = generationSeed,
                Bounds = template.WorldBounds,
                Theme = template.TacticalTheme,
                PixelsPerUnit = template.PixelsPerUnit,
                MinRoomSize = template.MinRoomSize,
                MaxRoomSize = template.MaxRoomSize,
                CorridorWidth = template.CorridorWidth,
                ForceAdjacency = template.ForceRoomAdjacency,
                WallMultiplier = template.WallMultiplier,
                DoorPenalty = template.DoorPenalty,
                CoverBudget = template.Actors
                    .Where(actor => actor.Type.Contains("Sentry", StringComparison.OrdinalIgnoreCase) ||
                                    actor.Type.Contains("Roamer", StringComparison.OrdinalIgnoreCase) ||
                                    actor.Type.Contains("Enemy", StringComparison.OrdinalIgnoreCase))
                    .Sum(actor => actor.NormalizedCount),
                ObjectiveRoomTags = template.PrimaryObjectives
                    .Concat(template.SecondaryObjectives)
                    .Select(objective => objective.TargetRoomTag)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                SourcePayloadPath = sourcePayloadPath
            });
        }

        private static List<JsonObject> BuildRooms(MissionTemplateModel template, string revisionId)
        {
            var width = template.WorldBounds[0];
            var height = template.WorldBounds[1];
            var corridor = Math.Max(1, template.CorridorWidth);
            var halfW = Math.Max(template.MinRoomSize[0], width / 2);
            var halfH = Math.Max(template.MinRoomSize[1], height / 2);

            return new List<JsonObject>
            {
                Room("room_entry", revisionId, "entry", 0, 0, halfW - corridor, halfH - corridor),
                Room("room_living", revisionId, "living_area", halfW + corridor, 0, width - halfW - corridor, halfH - corridor),
                Room("room_hallway", revisionId, "hallway", 0, halfH + corridor, halfW - corridor, height - halfH - corridor),
                Room("room_vault", revisionId, "security_vault", halfW + corridor, halfH + corridor, width - halfW - corridor, height - halfH - corridor)
            };
        }

        private static JsonObject Room(string id, string revisionId, string tag, int x, int y, int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return new JsonObject
            {
                ["id"] = id,
                ["layoutRevisionId"] = revisionId,
                ["tag"] = tag,
                ["rect"] = new JsonObject
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["width"] = width,
                    ["height"] = height
                },
                ["navNodeId"] = $"nav_{id.Substring("room_".Length)}"
            };
        }

        private static List<JsonObject> BuildPortals(string revisionId, IReadOnlyList<JsonObject> rooms)
        {
            return new List<JsonObject>
            {
                Portal("portal_entry_living", revisionId, rooms[0], rooms[1]),
                Portal("portal_entry_hallway", revisionId, rooms[0], rooms[2]),
                Portal("portal_hallway_vault", revisionId, rooms[2], rooms[3]),
                Portal("portal_living_vault", revisionId, rooms[1], rooms[3])
            };
        }

        private static JsonObject Portal(string id, string revisionId, JsonObject a, JsonObject b)
        {
            return new JsonObject
            {
                ["id"] = id,
                ["layoutRevisionId"] = revisionId,
                ["fromRoomId"] = a["id"]!.GetValue<string>(),
                ["toRoomId"] = b["id"]!.GetValue<string>(),
                ["kind"] = "door",
                ["width"] = 1
            };
        }

        private static JsonObject BuildEntityPlacementNode(MissionTemplateModel template, JsonObject layoutNode)
        {
            var revisionId = layoutNode["layoutRevisionId"]!.GetValue<string>();
            var rooms = LayoutRooms(layoutNode);
            var actors = new JsonArray();
            var objectives = new JsonArray();

            foreach (var actor in template.Actors)
            {
                for (var i = 0; i < actor.NormalizedCount; i++)
                {
                    var room = RoomForActor(actor, rooms, layoutNode, i);
                    var entityId = $"{actor.Id}_{i + 1:00}";
                    actors.Add(new JsonObject
                    {
                        ["entityId"] = entityId,
                        ["kind"] = "actor",
                        ["sourceActorId"] = actor.Id,
                        ["type"] = actor.Type,
                        ["navigationPolicy"] = actor.NavigationPolicy,
                        ["placementPolicy"] = actor.PlacementPolicy,
                        ["roomId"] = room["id"]!.GetValue<string>(),
                        ["navNodeId"] = room["navNodeId"]!.GetValue<string>(),
                        ["layoutRevisionId"] = revisionId,
                        ["ownership"] = OwnershipNode(template.MissionId, revisionId, entityId, actor.Id)
                    });
                }
            }

            foreach (var objective in template.PrimaryObjectives)
            {
                objectives.Add(ObjectivePlacementNode(template, revisionId, rooms, layoutNode, objective, "primary"));
            }

            foreach (var objective in template.SecondaryObjectives)
            {
                objectives.Add(ObjectivePlacementNode(template, revisionId, rooms, layoutNode, objective, "secondary"));
            }

            return new JsonObject
            {
                ["schemaVersion"] = "bse.mission_entities.v2.3",
                ["pipelineVersion"] = PipelineVersion,
                ["missionId"] = template.MissionId,
                ["layoutRevisionId"] = revisionId,
                ["placementStep"] = 5,
                ["requiresLayoutStep"] = 6,
                ["actors"] = actors,
                ["objectives"] = objectives
            };
        }

        private static JsonObject ObjectivePlacementNode(
            MissionTemplateModel template,
            string revisionId,
            IReadOnlyList<JsonObject> rooms,
            JsonObject layoutNode,
            MissionObjective objective,
            string objectiveSet)
        {
            var room = RoomForObjective(objective, rooms, layoutNode);
            return new JsonObject
            {
                ["entityId"] = objective.Id,
                ["kind"] = "objective",
                ["objectiveSet"] = objectiveSet,
                ["type"] = objective.Type,
                ["requiresLayoutGraph"] = objective.RequiresLayoutGraph ?? false,
                ["targetRoomTag"] = objective.TargetRoomTag,
                ["optional"] = objective.Optional ?? false,
                ["roomId"] = room["id"]!.GetValue<string>(),
                ["navNodeId"] = room["navNodeId"]!.GetValue<string>(),
                ["layoutRevisionId"] = revisionId,
                ["ownership"] = OwnershipNode(template.MissionId, revisionId, objective.Id, objective.Id)
            };
        }

        private static JsonObject OwnershipNode(string missionId, string layoutRevisionId, string entityId, string sourceId)
        {
            return new JsonObject
            {
                ["owner"] = "bse-pipeline",
                ["generatedBy"] = "manage_mission.place_entities",
                ["missionId"] = missionId,
                ["sourceId"] = sourceId,
                ["entityId"] = entityId,
                ["layoutRevisionId"] = layoutRevisionId,
                ["stableKey"] = $"{missionId}:{layoutRevisionId}:{entityId}"
            };
        }

        private static JsonObject RoomForActor(MissionActor actor, IReadOnlyList<JsonObject> rooms, JsonObject layoutNode, int index)
        {
            var candidates = PlacementRoomsForActor(actor, rooms, layoutNode);
            if (candidates.Count == 0)
            {
                return EntryRoom(rooms, layoutNode);
            }

            var startIndex = StablePlacementOffset(layoutNode["layoutRevisionId"]?.GetValue<string>() ?? "", actor.Id, actor.PlacementPolicy, candidates.Count);
            return candidates[(startIndex + index) % candidates.Count];
        }

        private static JsonObject RoomForObjective(MissionObjective objective, IReadOnlyList<JsonObject> rooms, JsonObject layoutNode)
        {
            if (!string.IsNullOrWhiteSpace(objective.TargetRoomTag))
            {
                var tagged = RoomByTag(rooms, objective.TargetRoomTag);
                if (tagged != null)
                {
                    return tagged;
                }
            }

            return objective.RequiresLayoutGraph == true ? FirstObjectiveRoom(rooms, layoutNode) : EntryRoom(rooms, layoutNode);
        }

        private static List<JsonObject> LayoutRooms(JsonObject layoutNode)
        {
            var roomArray = layoutNode["RoomGraph"]?["rooms"] as JsonArray;
            if (roomArray == null)
            {
                return new List<JsonObject>();
            }

            return roomArray.OfType<JsonObject>().Select(room => (JsonObject)room.DeepClone()).ToList();
        }

        private static JsonObject EntryRoom(IReadOnlyList<JsonObject> rooms, JsonObject layoutNode)
        {
            var entryRoomId = layoutNode["LayoutGraph"]?["entryRoomId"]?.GetValue<string>() ?? "room_entry";
            return RoomById(rooms, entryRoomId) ?? rooms.First();
        }

        private static JsonObject FirstObjectiveRoom(IReadOnlyList<JsonObject> rooms, JsonObject layoutNode)
        {
            var objectiveIds = layoutNode["LayoutGraph"]?["objectiveRoomIds"] as JsonArray;
            var firstId = objectiveIds?.OfType<JsonValue>().Select(v => v.GetValue<string>()).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
            return !string.IsNullOrWhiteSpace(firstId) ? RoomById(rooms, firstId!) ?? EntryRoom(rooms, layoutNode) : EntryRoom(rooms, layoutNode);
        }

        private static JsonObject? RoomById(IReadOnlyList<JsonObject> rooms, string id)
        {
            return rooms.FirstOrDefault(room => string.Equals(room["id"]?.GetValue<string>(), id, StringComparison.Ordinal));
        }

        private static JsonObject? RoomByTag(IReadOnlyList<JsonObject> rooms, string tag)
        {
            return rooms.FirstOrDefault(room => string.Equals(room["tag"]?.GetValue<string>(), tag, StringComparison.Ordinal));
        }

        private static List<JsonObject> PlacementRoomsForActor(MissionActor actor, IReadOnlyList<JsonObject> rooms, JsonObject layoutNode)
        {
            var entryRoom = EntryRoom(rooms, layoutNode);
            var objectiveRoom = FirstObjectiveRoom(rooms, layoutNode);
            var secureRoom = RoomByTag(rooms, "security_vault");
            var orderedRooms = OrderedPlacementRooms(rooms, layoutNode);

            return actor.PlacementPolicy switch
            {
                "EntryPointOnly" => UniqueRooms(new[] { entryRoom }),
                "PostLayout_TaggedRoom" => UniqueRooms(new[] { objectiveRoom }.Concat(orderedRooms.Where(room => !SameRoom(room, entryRoom) && !SameRoom(room, objectiveRoom)))),
                "PostLayout_AnyRoom" => UniqueRooms(orderedRooms.Where(room => !SameRoom(room, entryRoom))),
                "SecureRoomOnly" => UniqueRooms(new[] { secureRoom, objectiveRoom, entryRoom }),
                _ => UniqueRooms(new[] { entryRoom })
            };
        }

        private static List<JsonObject> OrderedPlacementRooms(IReadOnlyList<JsonObject> rooms, JsonObject layoutNode)
        {
            var ordered = new List<JsonObject>();
            AddRoomIfMissing(ordered, EntryRoom(rooms, layoutNode));
            AddRoomIfMissing(ordered, RoomByTag(rooms, "security_vault"));
            AddRoomIfMissing(ordered, FirstObjectiveRoom(rooms, layoutNode));

            foreach (var room in rooms
                         .OrderBy(room => room["tag"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(room => room["id"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal))
            {
                AddRoomIfMissing(ordered, room);
            }

            return ordered;
        }

        private static List<JsonObject> UniqueRooms(IEnumerable<JsonObject?> rooms)
        {
            var ordered = new List<JsonObject>();
            foreach (var room in rooms)
            {
                AddRoomIfMissing(ordered, room);
            }

            return ordered;
        }

        private static void AddRoomIfMissing(List<JsonObject> rooms, JsonObject? room)
        {
            if (room == null)
            {
                return;
            }

            var roomId = room["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(roomId) || rooms.Any(existing => SameRoom(existing, room)))
            {
                return;
            }

            rooms.Add(room);
        }

        private static bool SameRoom(JsonObject left, JsonObject right)
        {
            return string.Equals(left["id"]?.GetValue<string>() ?? "", right["id"]?.GetValue<string>() ?? "", StringComparison.Ordinal);
        }

        private static int StablePlacementOffset(string layoutRevisionId, string actorId, string placementPolicy, int candidateCount)
        {
            if (candidateCount <= 1)
            {
                return 0;
            }

            using var sha = SHA256.Create();
            var source = $"{layoutRevisionId}:{actorId}:{placementPolicy}:{PipelineVersion}";
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
            var value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            return value % candidateCount;
        }

        private static string ComputeLayoutRevisionId(MissionTemplateModel template)
        {
            return ComputeLayoutRevisionId(template, template.InitialSeed);
        }

        private static string ComputeLayoutRevisionId(MissionTemplateModel template, int generationSeed)
        {
            var source = new JsonObject
            {
                ["pipelineVersion"] = PipelineVersion,
                ["layoutGenerator"] = BspLayoutGenerator.GeneratorId,
                ["missionId"] = template.MissionId,
                ["requestedSeed"] = generationSeed,
                ["bounds"] = IntArrayNode(template.WorldBounds),
                ["theme"] = template.TacticalTheme,
                ["ppu"] = template.PixelsPerUnit,
                ["bsp"] = new JsonObject
                {
                    ["minRoomSize"] = IntArrayNode(template.MinRoomSize),
                    ["maxRoomSize"] = IntArrayNode(template.MaxRoomSize),
                    ["corridorWidth"] = template.CorridorWidth,
                    ["forceAdjacency"] = template.ForceRoomAdjacency
                },
                ["primaryObjectives"] = new JsonArray(template.PrimaryObjectives.Select(ObjectiveNode).ToArray())
            };

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source.ToJsonString()));
            return "layout_" + BitConverter.ToString(bytes, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static int LayoutGenerationSeed(JsonObject layoutNode, int fallbackSeed)
        {
            try
            {
                return layoutNode["generationSeed"]?.GetValue<int>() ?? fallbackSeed;
            }
            catch
            {
                return fallbackSeed;
            }
        }

        private static int PayloadGenerationSeed(JsonObject? payloadNode, int fallbackSeed)
        {
            try
            {
                return payloadNode?["header"]?["initialSeed"]?.GetValue<int>() ?? fallbackSeed;
            }
            catch
            {
                return fallbackSeed;
            }
        }

        private static void TryStampPayloadLayoutRevision(string payloadPath, string layoutRevisionId)
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(payloadPath)) as JsonObject;
                if (node?["header"] is not JsonObject header)
                {
                    return;
                }

                header["layoutRevisionId"] = layoutRevisionId;
                File.WriteAllText(payloadPath, node.ToJsonString() + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static JsonObject? ReadJsonObject(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            }
            catch
            {
                return null;
            }
        }

        private static JsonObject EmptyVerificationMetrics()
        {
            return new JsonObject
            {
                ["enemyCount"] = 0,
                ["roomCount"] = 0,
                ["emptyRoomCount"] = 0,
                ["light2DCount"] = 0,
                ["activeHearingChecks"] = 0,
                ["visibilityRayCount"] = 0,
                ["unreachableCriticalNodes"] = 0,
                ["actorCount"] = 0,
                ["objectiveCount"] = 0,
                ["coverPointCount"] = 0,
                ["reachableObjectives"] = 0,
                ["unreachableObjectives"] = 0,
                ["averageCoverPerRoom"] = 0,
                ["alternateRoutes"] = 0,
                ["hearingOverlapPercentage"] = 0,
                ["chokepointPressure"] = 0,
                ["objectiveRoomPressure"] = 0
            };
        }

        private static JsonObject ComputeVerificationMetrics(JsonObject layoutNode, JsonObject entitiesNode, List<JsonObject> findings)
        {
            var rooms = LayoutRooms(layoutNode);
            var roomIds = rooms.Select(r => r["id"]?.GetValue<string>() ?? "").Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
            var reachableRooms = ReachableRooms(layoutNode, roomIds);
            var actors = (entitiesNode["actors"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            var objectives = (entitiesNode["objectives"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            var occupiedRooms = new HashSet<string>(StringComparer.Ordinal);
            var objectiveRoomOccupancy = new Dictionary<string, int>(StringComparer.Ordinal);
            var unreachableCriticalNodes = 0;
            var reachableObjectives = 0;

            foreach (var entity in actors.Concat(objectives))
            {
                var roomId = entity["roomId"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(roomId))
                {
                    occupiedRooms.Add(roomId);
                    objectiveRoomOccupancy.TryGetValue(roomId, out var currentOccupancy);
                    objectiveRoomOccupancy[roomId] = currentOccupancy + 1;
                }

                if (string.IsNullOrWhiteSpace(roomId) || !roomIds.Contains(roomId) || !reachableRooms.Contains(roomId))
                {
                    unreachableCriticalNodes++;
                    var code = string.Equals(entity["kind"]?.GetValue<string>(), "objective", StringComparison.Ordinal)
                        ? "NAV_OBJECTIVE_UNREACHABLE"
                        : "NAV_ENTITY_UNREACHABLE";
                    findings.Add(Finding("error", code, $"Generated entity is not reachable from the breach entry: {entity["entityId"]?.GetValue<string>() ?? "(unknown)"}"));
                }
            }

            foreach (var objective in objectives)
            {
                var roomId = objective["roomId"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(roomId) && roomIds.Contains(roomId) && reachableRooms.Contains(roomId))
                {
                    reachableObjectives++;
                }
            }

            var entryRoomId = layoutNode["LayoutGraph"]?["entryRoomId"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(entryRoomId) || !roomIds.Contains(entryRoomId))
            {
                unreachableCriticalNodes++;
                findings.Add(Finding("error", "NAV_BREACHPOINT_UNREACHABLE", "LayoutGraph entryRoomId is missing from RoomGraph"));
            }

            var tacticalMetrics = BuildVerificationTacticalMetrics(layoutNode, entitiesNode, findings);
            var metrics = EmptyVerificationMetrics();
            metrics["enemyCount"] = tacticalMetrics["enemyCount"]?.GetValue<int>() ?? 0;
            metrics["roomCount"] = rooms.Count;
            metrics["emptyRoomCount"] = rooms.Count(room => !occupiedRooms.Contains(room["id"]?.GetValue<string>() ?? ""));
            metrics["light2DCount"] = CountLight2DObjects();
            metrics["activeHearingChecks"] = tacticalMetrics["activeHearingChecks"]?.GetValue<int>() ?? 0;
            metrics["visibilityRayCount"] = tacticalMetrics["visibilityRayCount"]?.GetValue<int>() ?? 0;
            metrics["unreachableCriticalNodes"] = unreachableCriticalNodes;
            metrics["actorCount"] = actors.Count;
            metrics["objectiveCount"] = objectives.Count;
            metrics["coverPointCount"] = tacticalMetrics["coverPointCount"]?.GetValue<int>() ?? 0;
            metrics["reachableObjectives"] = reachableObjectives;
            metrics["unreachableObjectives"] = Math.Max(0, objectives.Count - reachableObjectives);
            metrics["averageCoverPerRoom"] = tacticalMetrics["averageCoverPerRoom"]?.GetValue<double>() ?? 0;
            metrics["alternateRoutes"] = tacticalMetrics["alternateRoutes"]?.GetValue<int>() ?? 0;
            metrics["hearingOverlapPercentage"] = tacticalMetrics["hearingOverlapPercentage"]?.GetValue<double>() ?? 0;
            metrics["chokepointPressure"] = tacticalMetrics["chokepointPressure"]?.GetValue<double>() ?? 0;
            metrics["objectiveRoomPressure"] = tacticalMetrics["objectiveRoomPressure"]?.GetValue<double>() ?? 0;
            return metrics;
        }

        private static HashSet<string> ReachableRooms(JsonObject layoutNode, HashSet<string> roomIds)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var entryRoomId = layoutNode["LayoutGraph"]?["entryRoomId"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(entryRoomId) || !roomIds.Contains(entryRoomId))
            {
                return reachable;
            }

            var adjacency = roomIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            var portals = layoutNode["PortalGraph"]?["portals"] as JsonArray;
            foreach (var portal in portals?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                var from = portal["fromRoomId"]?.GetValue<string>() ?? "";
                var to = portal["toRoomId"]?.GetValue<string>() ?? "";
                if (adjacency.ContainsKey(from) && adjacency.ContainsKey(to))
                {
                    adjacency[from].Add(to);
                    adjacency[to].Add(from);
                }
            }

            var queue = new Queue<string>();
            queue.Enqueue(entryRoomId);
            reachable.Add(entryRoomId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in adjacency[current])
                {
                    if (reachable.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return reachable;
        }

        private static void ValidateProfileRefs(JsonObject payloadNode, List<JsonObject> findings)
        {
            if (payloadNode["profileRefs"] is not JsonObject profileRefs)
            {
                findings.Add(Finding("error", "PROFILE_REFS_MISSING", "Payload is missing profileRefs"));
                return;
            }

            var required = new[]
            {
                "tacticalThemeProfile",
                "performanceProfile",
                "renderProfile",
                "navigationPolicy",
                "tacticalDensityProfile",
                "addressablesCatalogProfile"
            };

            foreach (var key in required)
            {
                var assetPath = profileRefs[key]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    findings.Add(Finding("error", "PROFILE_REF_MISSING", $"Payload profileRefs.{key} is missing"));
                    continue;
                }

                var absolutePath = ToAbsoluteProjectPath(assetPath);
                if (!IsUnderProjectRoot(absolutePath) || (!File.Exists(absolutePath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null))
                {
                    findings.Add(Finding("error", "PROFILE_REF_MISSING", $"Profile reference does not resolve: {assetPath}", assetPath));
                }
            }
        }

        private static int CountLight2DObjects()
        {
            var light2DType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (light2DType == null)
            {
                return 0;
            }

            return Resources.FindObjectsOfTypeAll(light2DType)
                .Count(obj => !EditorUtility.IsPersistent(obj));
        }

        private static JsonObject BuildVerificationSummaryNode(
            string missionId,
            string status,
            string layoutRevisionId,
            IEnumerable<string> artifacts,
            IEnumerable<JsonObject> findings,
            JsonObject metrics)
        {
            var classification = ClassifyVerificationFindings(findings);
            return new JsonObject
            {
                ["schemaVersion"] = "bse.verification_summary.v2.3",
                ["pipelineVersion"] = PipelineVersion,
                ["missionId"] = missionId,
                ["status"] = status,
                ["layoutRevisionId"] = layoutRevisionId,
                ["retryClass"] = status == "PASS" ? "PASS" : classification.RetryClass,
                ["failureCode"] = status == "PASS" ? "" : classification.FailureCode,
                ["retryableFailureCount"] = classification.RetryableFailureCount,
                ["blockingFailureCount"] = classification.BlockingFailureCount,
                ["artifacts"] = new JsonArray(artifacts.Select(a => (JsonNode?)a).ToArray()),
                ["findings"] = FindingsArray(findings),
                ["metrics"] = (JsonObject)metrics.DeepClone()
            };
        }

        private static (bool Success, JsonObject SummaryNode, List<int> RetrySeeds) TryRunRetryPipeline(
            MissionTemplateModel template,
            string payloadPath,
            string layoutPath,
            string entitiesPath,
            string summaryPath,
            JsonObject failedSummary,
            IEnumerable<string> artifacts,
            List<int> retrySeeds,
            List<JsonObject> findings)
        {
            if (!VerificationFailureIsRetryable(failedSummary))
            {
                return (false, failedSummary, retrySeeds);
            }

            if (retrySeeds.Count >= template.MaxRetries)
            {
                findings.Add(Finding("error", "RETRY_BUDGET_EXHAUSTED", "Verification failed with retryable findings, but generationMeta.maxRetries has been exhausted", ToRepoPath(summaryPath)));
                return (false, failedSummary, retrySeeds);
            }

            var summaryNode = failedSummary;
            while (retrySeeds.Count < template.MaxRetries)
            {
                var failureCode = VerificationFailureCode(summaryNode);
                var retrySeed = DeriveRetrySeed(template.InitialSeed, template.MissionId, retrySeeds.Count + 1, failureCode);
                retrySeeds.Add(retrySeed);

                var layoutNode = BuildLayoutNode(template, retrySeed);
                Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
                File.WriteAllText(layoutPath, layoutNode.ToJsonString() + Environment.NewLine);
                TryStampPayloadLayoutRevision(payloadPath, layoutNode["layoutRevisionId"]!.GetValue<string>());

                var placementNode = BuildEntityPlacementNode(template, layoutNode);
                Directory.CreateDirectory(Path.GetDirectoryName(entitiesPath)!);
                File.WriteAllText(entitiesPath, placementNode.ToJsonString() + Environment.NewLine);

                var attemptFindings = new List<JsonObject>();
                var payloadNode = ReadJsonObject(payloadPath);
                if (payloadNode == null)
                {
                    attemptFindings.Add(Finding("error", "PAYLOAD_FILE_MISSING", "retry verification requires mission_payload.generated.json from compile_payload", ToRepoPath(payloadPath)));
                }
                else
                {
                    ValidateProfileRefs(payloadNode, attemptFindings);
                }

                var metrics = ComputeVerificationMetrics(layoutNode, placementNode, attemptFindings);
                var status = attemptFindings.Any(f => string.Equals(f["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal)) ? "FAIL" : "PASS";
                summaryNode = BuildVerificationSummaryNode(template.MissionId, status, layoutNode["layoutRevisionId"]!.GetValue<string>(), artifacts, attemptFindings, metrics);
                Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
                File.WriteAllText(summaryPath, summaryNode.ToJsonString() + Environment.NewLine);

                if (string.Equals(status, "PASS", StringComparison.Ordinal))
                {
                    findings.Add(Finding("warning", "MISSION_RETRIED", $"Verification passed after deterministic retry seed {retrySeed}", ToRepoPath(summaryPath)));
                    AssetDatabase.Refresh();
                    return (true, summaryNode, retrySeeds);
                }

                if (!VerificationFailureIsRetryable(summaryNode))
                {
                    AssetDatabase.Refresh();
                    return (false, summaryNode, retrySeeds);
                }
            }

            findings.Add(Finding("error", "RETRY_BUDGET_EXHAUSTED", "Verification retry attempts did not produce a passing mission", ToRepoPath(summaryPath)));
            AssetDatabase.Refresh();
            return (false, summaryNode, retrySeeds);
        }

        private static bool VerificationFailureIsRetryable(JsonObject summaryNode)
        {
            if (string.Equals(summaryNode["status"]?.GetValue<string>(), "PASS", StringComparison.Ordinal))
            {
                return false;
            }

            var retryClass = summaryNode["retryClass"]?.GetValue<string>() ?? "";
            if (string.Equals(retryClass, "RETRYABLE_FAIL", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(retryClass, "BLOCKED", StringComparison.Ordinal))
            {
                return false;
            }

            var errorCodes = (summaryNode["findings"] as JsonArray)?
                .OfType<JsonObject>()
                .Where(f => string.Equals(f["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal))
                .Select(f => f["code"]?.GetValue<string>() ?? "")
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList() ?? new List<string>();

            return errorCodes.Count > 0 && errorCodes.All(code => RetryableVerificationCodes.Contains(code));
        }

        private static string VerificationFailureCode(JsonObject summaryNode)
        {
            var code = summaryNode["failureCode"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }

            return FirstErrorCode(summaryNode);
        }

        private static (string RetryClass, string FailureCode, int RetryableFailureCount, int BlockingFailureCount) ClassifyVerificationFindings(IEnumerable<JsonObject> findings)
        {
            var errorFindings = findings
                .Where(f => string.Equals(f["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal))
                .ToList();
            if (errorFindings.Count == 0)
            {
                return ("PASS", "", 0, 0);
            }

            var retryableFailureCount = errorFindings.Count(f =>
            {
                var code = f["code"]?.GetValue<string>() ?? "";
                return !string.IsNullOrWhiteSpace(code) && RetryableVerificationCodes.Contains(code);
            });
            var blockingFailureCount = errorFindings.Count - retryableFailureCount;
            var retryClass = blockingFailureCount == 0 ? "RETRYABLE_FAIL" : "BLOCKED";
            var failureCode = errorFindings.FirstOrDefault()?["code"]?.GetValue<string>() ?? "";
            return (retryClass, failureCode, retryableFailureCount, blockingFailureCount);
        }

        private static int AcceptedGenerationSeed(MissionTemplateModel template, IReadOnlyList<int> retrySeeds, JsonObject summaryNode)
        {
            var layoutRevisionId = summaryNode["layoutRevisionId"]?.GetValue<string>() ?? "";
            if (retrySeeds.Count > 0 && string.Equals(layoutRevisionId, ComputeLayoutRevisionId(template, retrySeeds[^1]), StringComparison.Ordinal))
            {
                return retrySeeds[^1];
            }

            return template.InitialSeed;
        }

        private static int DeriveRetrySeed(int requestedSeed, string missionId, int retryIndex, string failureCode)
        {
            using var sha = SHA256.Create();
            var source = $"{requestedSeed}:{missionId}:{retryIndex}:{failureCode}:{PipelineVersion}";
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
            var value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            return value == 0 ? retryIndex : value;
        }

        private static List<JsonObject> ValidatePayloadShape(JsonObject payload)
        {
            var findings = new List<JsonObject>();
            RequireAllowedKeys(payload, "payload", new[] { "header", "spatial", "logic", "roster", "objectives", "profileRefs" }, findings);
            var header = RequiredPayloadObject(payload, "header", findings);
            var spatial = RequiredPayloadObject(payload, "spatial", findings);
            var logic = RequiredPayloadObject(payload, "logic", findings);
            RequiredPayloadArray(payload, "roster", findings);
            var objectives = RequiredPayloadObject(payload, "objectives", findings);
            var profileRefs = RequiredPayloadObject(payload, "profileRefs", findings);

            if (header != null)
            {
                RequireAllowedKeys(header, "header", new[] { "schemaVersion", "pipelineVersion", "missionId", "missionTitle", "initialSeed", "effectiveSeed", "layoutRevisionId" }, findings);
                RequiredPayloadString(header, "schemaVersion", PayloadSchemaVersion, findings);
                RequiredPayloadString(header, "pipelineVersion", PipelineVersion, findings);
                RequiredPayloadString(header, "missionId", null, findings);
                RequiredPayloadInteger(header, "initialSeed", 0, findings);
                RequiredPayloadInteger(header, "effectiveSeed", 0, findings);
            }

            if (spatial != null)
            {
                RequireAllowedKeys(spatial, "spatial", new[] { "bounds", "theme", "ppu", "bsp" }, findings);
                RequiredPayloadArray(spatial, "bounds", findings);
                RequiredPayloadString(spatial, "theme", null, findings);
                RequiredPayloadInteger(spatial, "ppu", 1, findings);
                var bsp = RequiredPayloadObject(spatial, "bsp", findings);
                if (bsp != null)
                {
                    RequireAllowedKeys(bsp, "spatial.bsp", new[] { "minRoomSize", "maxRoomSize", "corridorWidth", "forceAdjacency" }, findings);
                    RequiredPayloadArray(bsp, "minRoomSize", findings);
                    RequiredPayloadArray(bsp, "maxRoomSize", findings);
                    RequiredPayloadInteger(bsp, "corridorWidth", 1, findings);
                    RequiredPayloadBoolean(bsp, "forceAdjacency", findings);
                }
            }

            if (logic != null)
            {
                RequireAllowedKeys(logic, "logic", new[] { "noise", "navigation" }, findings);
                var noise = RequiredPayloadObject(logic, "noise", findings);
                var navigation = RequiredPayloadObject(logic, "navigation", findings);
                if (noise != null)
                {
                    RequireAllowedKeys(noise, "logic.noise", new[] { "threshold", "wallMultiplier", "doorPenalty" }, findings);
                    RequiredPayloadNumber(noise, "threshold", findings);
                    RequiredPayloadNumber(noise, "wallMultiplier", findings);
                    RequiredPayloadNumber(noise, "doorPenalty", findings);
                }

                if (navigation != null)
                {
                    RequireAllowedKeys(navigation, "logic.navigation", new[] { "strict" }, findings);
                    RequiredPayloadBoolean(navigation, "strict", findings);
                }
            }

            if (objectives != null)
            {
                RequireAllowedKeys(objectives, "objectives", new[] { "primary", "secondary" }, findings);
                RequiredPayloadArray(objectives, "primary", findings);
            }

            if (profileRefs != null)
            {
                RequireAllowedKeys(profileRefs, "profileRefs", new[]
                {
                    "tacticalThemeProfile",
                    "performanceProfile",
                    "renderProfile",
                    "navigationPolicy",
                    "tacticalDensityProfile",
                    "addressablesCatalogProfile"
                }, findings);
            }

            return findings;
        }

        private static JsonObject? RequiredPayloadObject(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonObject)
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload missing object: {key}"));
                return null;
            }

            return (JsonObject)obj[key]!;
        }

        private static void RequiredPayloadArray(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonArray)
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload missing array: {key}"));
            }
        }

        private static void RequiredPayloadString(JsonObject obj, string key, string? expected, List<JsonObject> findings)
        {
            if (obj[key] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload missing string: {key}"));
                return;
            }

            if (expected != null && !string.Equals(text, expected, StringComparison.Ordinal))
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload {key} must be {expected}"));
            }
        }

        private static void RequiredPayloadInteger(JsonObject obj, string key, int minimum, List<JsonObject> findings)
        {
            if (obj[key] is not JsonValue value || !value.TryGetValue<int>(out var number) || number < minimum)
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload {key} must be an integer >= {minimum}"));
            }
        }

        private static void RequiredPayloadNumber(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonValue value || !value.TryGetValue<double>(out _))
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload {key} must be a number"));
            }
        }

        private static void RequiredPayloadBoolean(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonValue value || !value.TryGetValue<bool>(out _))
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload {key} must be a boolean"));
            }
        }

        private static void RequireAllowedKeys(JsonObject obj, string path, IEnumerable<string> allowed, List<JsonObject> findings)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (var key in obj.Select(pair => pair.Key))
            {
                if (!allowedSet.Contains(key))
                {
                    findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload has unknown field: {path}.{key}"));
                }
            }
        }

        private static string MissionResultJson(string status, string missionId, IEnumerable<string> artifacts, IEnumerable<JsonObject> findings, JsonObject? metrics = null, JsonObject? state = null)
        {
            return new JsonObject
            {
                ["status"] = status,
                ["missionId"] = missionId,
                ["pipelineVersion"] = PipelineVersion,
                ["artifacts"] = new JsonArray(artifacts.Select(a => (JsonNode?)a).ToArray()),
                ["findings"] = FindingsArray(findings),
                ["metrics"] = metrics != null ? (JsonObject)metrics.DeepClone() : new JsonObject(),
                ["state"] = state != null ? (JsonObject)state.DeepClone() : new JsonObject
                {
                    ["currentStep"] = "",
                    ["jobId"] = ""
                }
            }.ToJsonString();
        }

        private static JsonObject BuildCompileReportNode(string missionId, string templatePath, string payloadPath, IEnumerable<JsonObject> findings)
        {
            return new JsonObject
            {
                ["status"] = "PASS",
                ["missionId"] = missionId,
                ["pipelineVersion"] = PipelineVersion,
                ["templatePath"] = templatePath,
                ["payloadPath"] = payloadPath,
                ["findings"] = FindingsArray(findings)
            };
        }

        private static JsonObject BuildGenerationManifestNode(
            MissionTemplateModel template,
            string status,
            int effectiveSeed,
            IReadOnlyList<int> retrySeeds,
            string layoutRevisionId,
            JsonObject payloadNode,
            JsonObject summaryNode,
            IEnumerable<string> artifacts)
        {
            return new JsonObject
            {
                ["schemaVersion"] = "bse.generation_manifest.v2.3",
                ["pipelineVersion"] = PipelineVersion,
                ["missionId"] = template.MissionId,
                ["status"] = status,
                ["requestedSeed"] = template.InitialSeed,
                ["effectiveSeed"] = effectiveSeed,
                ["retrySeeds"] = new JsonArray(retrySeeds.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
                ["acceptedAttempt"] = effectiveSeed > 0 && retrySeeds.Count > 0 && retrySeeds[^1] == effectiveSeed ? retrySeeds.Count : 0,
                ["layoutRevisionId"] = layoutRevisionId,
                ["lockOwner"] = LockOwner,
                ["profileRefs"] = (payloadNode["profileRefs"] as JsonObject)?.DeepClone() ?? template.ProfileRefs(),
                ["artifacts"] = ManifestArtifactsObject(artifacts),
                ["verification"] = new JsonObject
                {
                    ["status"] = summaryNode["status"]?.GetValue<string>() ?? "PASS",
                    ["retryClass"] = summaryNode["retryClass"]?.GetValue<string>() ?? "PASS",
                    ["failureCode"] = summaryNode["failureCode"]?.GetValue<string>() ?? "",
                    ["retryableFailureCount"] = summaryNode["retryableFailureCount"]?.GetValue<int>() ?? 0,
                    ["blockingFailureCount"] = summaryNode["blockingFailureCount"]?.GetValue<int>() ?? 0,
                    ["findings"] = (summaryNode["findings"] as JsonArray)?.DeepClone() ?? new JsonArray(),
                    ["metrics"] = (summaryNode["metrics"] as JsonObject)?.DeepClone() ?? EmptyVerificationMetrics()
                }
            };
        }

        private static JsonObject ManifestArtifactsObject(IEnumerable<string> artifacts)
        {
            var unique = artifacts.Distinct(StringComparer.Ordinal).ToList();
            var result = new JsonObject
            {
                ["payload"] = unique.FirstOrDefault(p => p.EndsWith("mission_payload.generated.json", StringComparison.Ordinal)) ?? "",
                ["compileReport"] = unique.FirstOrDefault(p => p.EndsWith("mission_compile_report.json", StringComparison.Ordinal)) ?? "",
                ["layout"] = unique.FirstOrDefault(p => p.EndsWith("mission_layout.generated.json", StringComparison.Ordinal)) ?? "",
                ["entities"] = unique.FirstOrDefault(p => p.EndsWith("mission_entities.generated.json", StringComparison.Ordinal)) ?? "",
                ["verificationSummary"] = unique.FirstOrDefault(p => p.EndsWith("verification_summary.json", StringComparison.Ordinal)) ?? "",
                ["missionState"] = unique.FirstOrDefault(p => p.EndsWith("mission_state.json", StringComparison.Ordinal)) ?? ""
            };
            return result;
        }

        private static int ExistingAcceptedEffectiveSeed(JsonObject? existingManifest)
        {
            if (existingManifest == null)
            {
                return 0;
            }

            var status = existingManifest["status"]?.GetValue<string>() ?? "";
            var seed = existingManifest["effectiveSeed"]?.GetValue<int>() ?? 0;
            return string.Equals(status, "PASS", StringComparison.Ordinal) && seed > 0 ? seed : 0;
        }

        private static void StampPayloadReplayFields(string payloadPath, JsonObject payloadNode, int effectiveSeed, string layoutRevisionId)
        {
            if (payloadNode["header"] is JsonObject header)
            {
                header["effectiveSeed"] = effectiveSeed;
                header["layoutRevisionId"] = layoutRevisionId;
                File.WriteAllText(payloadPath, payloadNode.ToJsonString() + Environment.NewLine);
            }
        }

        private static List<int> ReadRetrySeeds(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var source = doc.RootElement;
                if (source.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object)
                {
                    source = args;
                }

                if (!source.TryGetProperty("retrySeeds", out var seeds) || seeds.ValueKind != JsonValueKind.Array)
                {
                    return new List<int>();
                }

                return seeds.EnumerateArray()
                    .Where(seed => seed.ValueKind == JsonValueKind.Number && seed.TryGetInt32(out _))
                    .Select(seed => seed.GetInt32())
                    .ToList();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private sealed class GenerationLockSet : IDisposable
        {
            private FileStream? _stream;
            private readonly string _path;
            public string JobId { get; }

            private GenerationLockSet(FileStream stream, string path, string jobId)
            {
                _stream = stream;
                _path = path;
                JobId = jobId;
            }

            public static bool TryAcquire(
                string missionId,
                string missionDir,
                string currentStep,
                out GenerationLockSet? lockSet,
                out string? conflictPath,
                out JsonObject conflictFinding)
            {
                var path = GenerationLockPath(missionDir);
                var jobId = Guid.NewGuid().ToString("N");
                lockSet = null;
                conflictPath = null;
                conflictFinding = new JsonObject();
                try
                {
                    Directory.CreateDirectory(missionDir);
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    var now = DateTime.UtcNow;
                    var bodyNode = BuildLockNode(missionId, jobId, currentStep, now);
                    var body = Encoding.UTF8.GetBytes(bodyNode.ToJsonString() + Environment.NewLine);
                    stream.Write(body, 0, body.Length);
                    stream.Flush();
                    lockSet = new GenerationLockSet(stream, path, jobId);
                    return true;
                }
                catch (IOException)
                {
                    conflictPath = path;
                    var existingLock = ReadJsonObject(path);
                    var staleSuffix = IsStaleLock(existingLock) ? " The lock appears stale; run cleanup_generation_lock for this mission to remove it diagnostically." : "";
                    conflictFinding = Finding("error", "GENERATION_LOCK_CONFLICT", "Another mission generation writer holds the mission generation lock." + staleSuffix, ToRepoPath(path));
                    return false;
                }
            }

            public void Dispose()
            {
                if (_stream == null)
                {
                    return;
                }

                _stream.Dispose();
                _stream = null;
                TryDeleteFile(_path);
            }
        }

        private static string GenerationLockPath(string missionDir)
        {
            return Path.Combine(missionDir, ".generation.lock");
        }

        private static string MissionStatePath(string missionDir)
        {
            return Path.Combine(missionDir, "mission_state.json");
        }

        private static JsonObject BuildLockNode(string missionId, string jobId, string currentStep, DateTime now)
        {
            return new JsonObject
            {
                ["missionId"] = missionId,
                ["jobId"] = jobId,
                ["lockOwner"] = LockOwner,
                ["startedAtUtc"] = now.ToString("O", CultureInfo.InvariantCulture),
                ["updatedAtUtc"] = now.ToString("O", CultureInfo.InvariantCulture),
                ["currentStep"] = currentStep,
                ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id
            };
        }

        private static bool IsStaleLock(JsonObject? lockNode)
        {
            if (lockNode == null)
            {
                return false;
            }

            var updated = lockNode["updatedAtUtc"]?.GetValue<string>() ?? lockNode["startedAtUtc"]?.GetValue<string>() ?? "";
            return DateTime.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedAt) &&
                DateTime.UtcNow - updatedAt > TimeSpan.FromMinutes(30);
        }

        private static void WriteMissionState(string missionDir, string missionId, string status, string currentStep, string layoutRevisionId, string lastFindingCode, string jobId)
        {
            Directory.CreateDirectory(missionDir);
            var path = MissionStatePath(missionDir);
            var existing = ReadJsonObject(path);
            var startedAt = existing?["startedAtUtc"]?.GetValue<string>() ?? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var node = new JsonObject
            {
                ["missionId"] = missionId,
                ["pipelineVersion"] = PipelineVersion,
                ["status"] = status,
                ["currentStep"] = currentStep,
                ["startedAtUtc"] = startedAt,
                ["updatedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["jobId"] = jobId,
                ["lockOwner"] = LockOwner,
                ["layoutRevisionId"] = layoutRevisionId,
                ["lastFindingCode"] = lastFindingCode
            };
            File.WriteAllText(path, node.ToJsonString() + Environment.NewLine);
        }

        private static bool MissionStateAllowsManifest(string missionDir, JsonObject summaryNode, out JsonObject? finding)
        {
            finding = null;
            var statePath = MissionStatePath(missionDir);
            var state = ReadJsonObject(statePath);
            if (state == null)
            {
                finding = Finding("error", "MISSION_STATE_INCOMPATIBLE", "write_manifest requires mission_state.json from a passing verify step", ToRepoPath(statePath));
                return false;
            }

            var stateStatus = state["status"]?.GetValue<string>() ?? "";
            var summaryStatus = summaryNode["status"]?.GetValue<string>() ?? "";
            var stateLayout = state["layoutRevisionId"]?.GetValue<string>() ?? "";
            var summaryLayout = summaryNode["layoutRevisionId"]?.GetValue<string>() ?? "";
            if (!string.Equals(stateStatus, "PASS", StringComparison.Ordinal) ||
                !string.Equals(summaryStatus, "PASS", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(summaryLayout) ||
                !string.Equals(stateLayout, summaryLayout, StringComparison.Ordinal))
            {
                finding = Finding("error", "MISSION_STATE_INCOMPATIBLE", "write_manifest requires PASS mission_state.json matching verification_summary.layoutRevisionId", ToRepoPath(statePath));
                return false;
            }

            return true;
        }

        private static string LastFindingCode(IEnumerable<JsonObject> findings)
        {
            return findings.LastOrDefault(f => f["code"] != null)?["code"]?.GetValue<string>() ?? "";
        }

        private static string FirstErrorCode(JsonObject summaryNode)
        {
            return (summaryNode["findings"] as JsonArray)?
                .OfType<JsonObject>()
                .FirstOrDefault(f => string.Equals(f["severity"]?.GetValue<string>(), "error", StringComparison.Ordinal))?["code"]?.GetValue<string>() ?? "";
        }

        private static JsonArray FindingsArray(IEnumerable<JsonObject> findings)
        {
            return new JsonArray(findings.Select(f => (JsonNode?)f.DeepClone()).ToArray());
        }

        private static JsonObject Finding(string severity, string code, string message, string? path = null)
        {
            var finding = new JsonObject
            {
                ["severity"] = severity,
                ["code"] = code,
                ["message"] = message
            };
            if (!string.IsNullOrWhiteSpace(path))
            {
                finding["path"] = path;
            }

            return finding;
        }

        private static (bool, string) NotImplemented(string action)
        {
            return (false, MissionResultJson("FAIL", "", Array.Empty<string>(), new[]
            {
                Finding("error", "MISSION_ACTION_NOT_IMPLEMENTED", $"{action} is not implemented in this vertical slice")
            }));
        }

        private static string ResolveMissionArtifactPath(string raw, string key, string missionDir, string fileName)
        {
            var arg = JsonArgString(raw, key);
            if (string.IsNullOrWhiteSpace(arg))
            {
                return Path.Combine(missionDir, fileName);
            }

            return ToAbsoluteProjectPath(arg);
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(ProjectRoot(), normalized));
        }

        private static string ToRepoPath(string path)
        {
            return Path.GetRelativePath(ProjectRoot(), path).Replace('\\', '/');
        }

        private static bool IsUnderProjectRoot(string path)
        {
            var root = ProjectRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string? JsonArgString(string json, string key)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var source = doc.RootElement;
                if (source.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object)
                {
                    source = args;
                }

                if (source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                }
            }
            catch
            {
            }

            return null;
        }

        private sealed class MissionTemplateModel
        {
            public string SchemaVersion { get; private set; } = "";
            public string MissionId { get; private set; } = "";
            public string MissionTitle { get; private set; } = "";
            public int InitialSeed { get; private set; }
            public int EffectiveSeed { get; private set; }
            public int GenerationTimeout { get; private set; }
            public int MaxRetries { get; private set; }
            public int[] WorldBounds { get; private set; } = Array.Empty<int>();
            public int PixelsPerUnit { get; private set; }
            public string TacticalTheme { get; private set; } = "";
            public int[] MinRoomSize { get; private set; } = Array.Empty<int>();
            public int[] MaxRoomSize { get; private set; } = Array.Empty<int>();
            public int CorridorWidth { get; private set; }
            public bool ForceRoomAdjacency { get; private set; }
            public double NoiseAlertThreshold { get; private set; }
            public bool StrictNavigationPolicy { get; private set; }
            public bool EnforcePostLayoutPlacement { get; private set; }
            public double WallMultiplier { get; private set; }
            public double DoorPenalty { get; private set; }
            public List<MissionActor> Actors { get; } = new();
            public List<MissionObjective> PrimaryObjectives { get; } = new();
            public List<MissionObjective> SecondaryObjectives { get; } = new();

            private readonly HashSet<string> _seenFields = new(StringComparer.Ordinal);

            public JsonObject ProfileRefs()
            {
                const string root = "Assets/Data/Mission/Profiles";
                return new JsonObject
                {
                    ["tacticalThemeProfile"] = $"{root}/TacticalThemeProfile.asset",
                    ["performanceProfile"] = $"{root}/PerformanceProfile.asset",
                    ["renderProfile"] = $"{root}/RenderProfile.asset",
                    ["navigationPolicy"] = $"{root}/NavigationPolicy.asset",
                    ["tacticalDensityProfile"] = $"{root}/TacticalDensityProfile.asset",
                    ["addressablesCatalogProfile"] = $"{root}/AddressablesCatalogProfile.asset"
                };
            }

            public static bool TryLoad(string path, string? requestedMissionId, out MissionTemplateModel template, out List<JsonObject> findings)
            {
                template = new MissionTemplateModel();
                findings = new List<JsonObject>();

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(path);
                }
                catch (Exception ex)
                {
                    findings.Add(Finding("error", "TPL_FILE_READ_FAILED", ex.Message));
                    return false;
                }

                string section = "";
                string subsection = "";
                string objectiveSection = "";
                MissionActor? currentActor = null;
                MissionObjective? currentObjective = null;

                for (var i = 0; i < lines.Length; i++)
                {
                    var rawLine = lines[i];
                    if (rawLine.Contains('\t'))
                    {
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "Tabs are not supported for indentation", $"line:{i + 1}"));
                        continue;
                    }

                    var noComment = StripComment(rawLine).TrimEnd();
                    if (string.IsNullOrWhiteSpace(noComment))
                    {
                        continue;
                    }

                    var indent = rawLine.TakeWhile(char.IsWhiteSpace).Count();
                    var line = noComment.Trim();
                    var pathPrefix = $"line:{i + 1}";

                    if (indent == 0 && line.EndsWith(":", StringComparison.Ordinal))
                    {
                        section = line.TrimEnd(':');
                        subsection = "";
                        objectiveSection = "";
                        currentActor = null;
                        currentObjective = null;
                        if (!new[] { "generationMeta", "spatialConstraints", "tacticalRules", "actorRoster", "objectives" }.Contains(section, StringComparer.Ordinal))
                        {
                            findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown top-level section: {section}", pathPrefix));
                        }
                        continue;
                    }

                    if (!line.Contains(':'))
                    {
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Expected key/value pair: {line}", pathPrefix));
                        continue;
                    }

                    if (indent == 0)
                    {
                        SetScalar(template, line, findings, pathPrefix);
                        continue;
                    }

                    if (indent == 2 && line.EndsWith(":", StringComparison.Ordinal))
                    {
                        subsection = line.TrimEnd(':');
                        currentActor = null;
                        currentObjective = null;
                        if (section == "objectives")
                        {
                            objectiveSection = subsection;
                            if (objectiveSection != "primary" && objectiveSection != "secondary")
                            {
                                findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown objectives section: {objectiveSection}", pathPrefix));
                            }
                        }
                        continue;
                    }

                    if (section == "actorRoster")
                    {
                        if (line.StartsWith("- ", StringComparison.Ordinal))
                        {
                            currentActor = new MissionActor();
                            template.Actors.Add(currentActor);
                            SetActor(currentActor, line.Substring(2), findings, pathPrefix);
                        }
                        else if (currentActor != null)
                        {
                            SetActor(currentActor, line, findings, pathPrefix);
                        }
                        else
                        {
                            findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "actorRoster entries must start with '-'", pathPrefix));
                        }
                        continue;
                    }

                    if (section == "objectives")
                    {
                        if (line.StartsWith("- ", StringComparison.Ordinal))
                        {
                            currentObjective = new MissionObjective();
                            if (objectiveSection == "secondary")
                            {
                                template.SecondaryObjectives.Add(currentObjective);
                            }
                            else
                            {
                                template.PrimaryObjectives.Add(currentObjective);
                            }
                            SetObjective(currentObjective, line.Substring(2), findings, pathPrefix);
                        }
                        else if (currentObjective != null)
                        {
                            SetObjective(currentObjective, line, findings, pathPrefix);
                        }
                        else
                        {
                            findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "objective entries must start with '-'", pathPrefix));
                        }
                        continue;
                    }

                    SetSectionScalar(template, section, subsection, line, findings, pathPrefix);
                }

                Validate(template, requestedMissionId, findings);
                return findings.All(f => f["severity"]?.GetValue<string>() != "error");
            }

            private static void SetScalar(MissionTemplateModel template, string line, List<JsonObject> findings, string path)
            {
                var (key, value) = KeyValue(line);
                switch (key)
                {
                    case "schemaVersion": template.SchemaVersion = template.MarkedString(value, "schemaVersion", findings, path); break;
                    case "missionId": template.MissionId = template.MarkedString(value, "missionId", findings, path); break;
                    case "missionTitle": template.MissionTitle = template.MarkedString(value, "missionTitle", findings, path); break;
                    default: findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown top-level field: {key}", path)); break;
                }
            }

            private static void SetSectionScalar(MissionTemplateModel template, string section, string subsection, string line, List<JsonObject> findings, string path)
            {
                var (key, value) = KeyValue(line);
                switch (section)
                {
                    case "generationMeta":
                        if (key == "initialSeed") template.InitialSeed = template.MarkedInt(value, "generationMeta.initialSeed", findings, path);
                        else if (key == "effectiveSeed") template.EffectiveSeed = template.MarkedInt(value, "generationMeta.effectiveSeed", findings, path);
                        else if (key == "generationTimeout") template.GenerationTimeout = template.MarkedInt(value, "generationMeta.generationTimeout", findings, path);
                        else if (key == "maxRetries") template.MaxRetries = template.MarkedInt(value, "generationMeta.maxRetries", findings, path);
                        else findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown generationMeta field: {key}", path));
                        break;
                    case "spatialConstraints":
                        if (key == "worldBounds") template.WorldBounds = template.MarkedIntList(value, "spatialConstraints.worldBounds", findings, path);
                        else if (key == "pixelsPerUnit") template.PixelsPerUnit = template.MarkedInt(value, "spatialConstraints.pixelsPerUnit", findings, path);
                        else if (key == "tacticalTheme") template.TacticalTheme = template.MarkedString(value, "spatialConstraints.tacticalTheme", findings, path);
                        else if (subsection == "bspConstraints" && key == "minRoomSize") template.MinRoomSize = template.MarkedIntList(value, "spatialConstraints.bspConstraints.minRoomSize", findings, path);
                        else if (subsection == "bspConstraints" && key == "maxRoomSize") template.MaxRoomSize = template.MarkedIntList(value, "spatialConstraints.bspConstraints.maxRoomSize", findings, path);
                        else if (subsection == "bspConstraints" && key == "corridorWidth") template.CorridorWidth = template.MarkedInt(value, "spatialConstraints.bspConstraints.corridorWidth", findings, path);
                        else if (subsection == "bspConstraints" && key == "forceRoomAdjacency") template.ForceRoomAdjacency = template.MarkedBool(value, "spatialConstraints.bspConstraints.forceRoomAdjacency", findings, path);
                        else findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown spatialConstraints field: {key}", path));
                        break;
                    case "tacticalRules":
                        if (key == "noiseAlertThreshold") template.NoiseAlertThreshold = template.MarkedDouble(value, "tacticalRules.noiseAlertThreshold", findings, path);
                        else if (key == "strictNavigationPolicy") template.StrictNavigationPolicy = template.MarkedBool(value, "tacticalRules.strictNavigationPolicy", findings, path);
                        else if (key == "enforcePostLayoutPlacement") template.EnforcePostLayoutPlacement = template.MarkedBool(value, "tacticalRules.enforcePostLayoutPlacement", findings, path);
                        else if (subsection == "acousticOcclusion" && key == "wallMultiplier") template.WallMultiplier = template.MarkedDouble(value, "tacticalRules.acousticOcclusion.wallMultiplier", findings, path);
                        else if (subsection == "acousticOcclusion" && key == "doorPenalty") template.DoorPenalty = template.MarkedDouble(value, "tacticalRules.acousticOcclusion.doorPenalty", findings, path);
                        else findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown tacticalRules field: {key}", path));
                        break;
                    default:
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Field is not in a known section: {key}", path));
                        break;
                }
            }

            private static void SetActor(MissionActor actor, string line, List<JsonObject> findings, string path)
            {
                var (key, value) = KeyValue(line);
                switch (key)
                {
                    case "id": actor.Id = ParseString(value, "actorRoster[].id", findings, path); break;
                    case "type": actor.Type = ParseString(value, "actorRoster[].type", findings, path); break;
                    case "countRange": actor.CountRange = ParseIntList(value, "actorRoster[].countRange", findings, path); break;
                    case "navigationPolicy": actor.NavigationPolicy = ParseString(value, "actorRoster[].navigationPolicy", findings, path); break;
                    case "placementPolicy": actor.PlacementPolicy = ParseString(value, "actorRoster[].placementPolicy", findings, path); break;
                    default: findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown actor field: {key}", path)); break;
                }
            }

            private static void SetObjective(MissionObjective objective, string line, List<JsonObject> findings, string path)
            {
                var (key, value) = KeyValue(line);
                switch (key)
                {
                    case "id": objective.Id = ParseString(value, "objectives[].id", findings, path); break;
                    case "type": objective.Type = ParseString(value, "objectives[].type", findings, path); break;
                    case "requiresLayoutGraph": objective.RequiresLayoutGraph = ParseBool(value, "objectives[].requiresLayoutGraph", findings, path); break;
                    case "targetRoomTag": objective.TargetRoomTag = ParseString(value, "objectives[].targetRoomTag", findings, path); break;
                    case "optional": objective.Optional = ParseBool(value, "objectives[].optional", findings, path); break;
                    default: findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unknown objective field: {key}", path)); break;
                }
            }

            private static void Validate(MissionTemplateModel template, string? requestedMissionId, List<JsonObject> findings)
            {
                Required(template.SchemaVersion, "schemaVersion", findings);
                Required(template.MissionId, "missionId", findings);
                Required(template.MissionTitle, "missionTitle", findings);
                Required(template._seenFields.Contains("generationMeta.initialSeed"), "generationMeta.initialSeed", findings);
                Required(template._seenFields.Contains("generationMeta.generationTimeout"), "generationMeta.generationTimeout", findings);
                Required(template._seenFields.Contains("generationMeta.maxRetries"), "generationMeta.maxRetries", findings);
                Required(template.WorldBounds.Length == 2, "spatialConstraints.worldBounds", findings);
                Required(template._seenFields.Contains("spatialConstraints.pixelsPerUnit"), "spatialConstraints.pixelsPerUnit", findings);
                Required(template.TacticalTheme, "spatialConstraints.tacticalTheme", findings);
                Required(template.MinRoomSize.Length == 2, "spatialConstraints.bspConstraints.minRoomSize", findings);
                Required(template.MaxRoomSize.Length == 2, "spatialConstraints.bspConstraints.maxRoomSize", findings);
                Required(template._seenFields.Contains("spatialConstraints.bspConstraints.corridorWidth"), "spatialConstraints.bspConstraints.corridorWidth", findings);
                Required(template._seenFields.Contains("spatialConstraints.bspConstraints.forceRoomAdjacency"), "spatialConstraints.bspConstraints.forceRoomAdjacency", findings);
                Required(template._seenFields.Contains("tacticalRules.noiseAlertThreshold"), "tacticalRules.noiseAlertThreshold", findings);
                Required(template._seenFields.Contains("tacticalRules.strictNavigationPolicy"), "tacticalRules.strictNavigationPolicy", findings);
                Required(template._seenFields.Contains("tacticalRules.enforcePostLayoutPlacement"), "tacticalRules.enforcePostLayoutPlacement", findings);
                Required(template._seenFields.Contains("tacticalRules.acousticOcclusion.wallMultiplier"), "tacticalRules.acousticOcclusion.wallMultiplier", findings);
                Required(template._seenFields.Contains("tacticalRules.acousticOcclusion.doorPenalty"), "tacticalRules.acousticOcclusion.doorPenalty", findings);
                Required(template.Actors.Count > 0, "actorRoster", findings);
                Required(template.PrimaryObjectives.Count > 0, "objectives.primary", findings);

                if (template.SchemaVersion != TemplateSchemaVersion)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"schemaVersion must be {TemplateSchemaVersion}"));
                }

                if (!Regex.IsMatch(template.MissionId, "^VS[0-9]{2}_[A-Za-z0-9]+(?:[A-Za-z0-9]+)*$"))
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "missionId must match VS##_ShortMissionName"));
                }

                if (!string.IsNullOrWhiteSpace(requestedMissionId) && !string.Equals(requestedMissionId, template.MissionId, StringComparison.Ordinal))
                {
                    findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", "missionId argument must match template missionId"));
                }

                if (!TacticalThemes.Contains(template.TacticalTheme))
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "spatialConstraints.tacticalTheme has an unsupported value"));
                }

                if (template.PixelsPerUnit != 128 && template.PixelsPerUnit != 256)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", "spatialConstraints.pixelsPerUnit must be 128 or 256"));
                }

                RangeAtLeast(template.InitialSeed, 0, "generationMeta.initialSeed", findings);
                RangeAtLeast(template.EffectiveSeed, 0, "generationMeta.effectiveSeed", findings);
                RangeAtLeast(template.GenerationTimeout, 1, "generationMeta.generationTimeout", findings);
                RangeAtLeast(template.MaxRetries, 0, "generationMeta.maxRetries", findings);
                ValidatePositivePair(template.WorldBounds, "spatialConstraints.worldBounds", findings);
                ValidatePositivePair(template.MinRoomSize, "spatialConstraints.bspConstraints.minRoomSize", findings);
                ValidatePositivePair(template.MaxRoomSize, "spatialConstraints.bspConstraints.maxRoomSize", findings);
                RangeAtLeast(template.CorridorWidth, 1, "spatialConstraints.bspConstraints.corridorWidth", findings);
                RangeBetween(template.NoiseAlertThreshold, 0, 1, "tacticalRules.noiseAlertThreshold", findings);
                RangeAtLeast(template.WallMultiplier, 0, "tacticalRules.acousticOcclusion.wallMultiplier", findings);
                RangeAtLeast(template.DoorPenalty, 0, "tacticalRules.acousticOcclusion.doorPenalty", findings);

                if (template.MinRoomSize.Length == 2 && template.MaxRoomSize.Length == 2)
                {
                    if (template.MinRoomSize[0] > template.MaxRoomSize[0] || template.MinRoomSize[1] > template.MaxRoomSize[1])
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", "minRoomSize must be less than or equal to maxRoomSize"));
                    }
                }

                ValidateActors(template.Actors, findings);
                ValidateObjectives(template.PrimaryObjectives, "objectives.primary", findings);
                ValidateObjectives(template.SecondaryObjectives, "objectives.secondary", findings);
                ValidateObjectiveReferences(template, findings);
            }

            private static void ValidateActors(IEnumerable<MissionActor> actors, List<JsonObject> findings)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var actor in actors)
                {
                    Required(actor.Id, "actorRoster[].id", findings);
                    Required(actor.Type, "actorRoster[].type", findings);
                    Required(actor.NavigationPolicy, "actorRoster[].navigationPolicy", findings);
                    Required(actor.PlacementPolicy, "actorRoster[].placementPolicy", findings);
                    Required(actor.CountRange.Length == 2, "actorRoster[].countRange", findings);
                    if (!string.IsNullOrWhiteSpace(actor.Id) && !ids.Add(actor.Id))
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", $"Duplicate actor id: {actor.Id}"));
                    }

                    if (!NavigationPolicies.Contains(actor.NavigationPolicy))
                    {
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unsupported actor navigationPolicy: {actor.NavigationPolicy}"));
                    }

                    if (!PlacementPolicies.Contains(actor.PlacementPolicy))
                    {
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Unsupported actor placementPolicy: {actor.PlacementPolicy}"));
                    }

                    if (actor.CountRange.Length == 2)
                    {
                        if (actor.CountRange[0] < 0 || actor.CountRange[1] < 0 || actor.CountRange[0] > actor.CountRange[1])
                        {
                            findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Invalid actor countRange for {actor.Id}"));
                        }
                    }
                }
            }

            private static void ValidateObjectives(IEnumerable<MissionObjective> objectives, string section, List<JsonObject> findings)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var objective in objectives)
                {
                    Required(objective.Id, $"{section}[].id", findings);
                    Required(objective.Type, $"{section}[].type", findings);
                    if (!string.IsNullOrWhiteSpace(objective.Id) && !ids.Add(objective.Id))
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", $"Duplicate objective id: {objective.Id}"));
                    }

                    if (objective.RequiresLayoutGraph == true && string.IsNullOrWhiteSpace(objective.TargetRoomTag))
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", $"{objective.Id} requires a targetRoomTag when requiresLayoutGraph is true"));
                    }
                }
            }

            private static void ValidateObjectiveReferences(MissionTemplateModel template, List<JsonObject> findings)
            {
                var knownRoomTags = new HashSet<string>(StringComparer.Ordinal)
                {
                    "entry",
                    "living_area",
                    "hallway",
                    "security_vault"
                };
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var objective in template.PrimaryObjectives.Concat(template.SecondaryObjectives))
                {
                    if (!string.IsNullOrWhiteSpace(objective.Id) && !ids.Add(objective.Id))
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", $"Duplicate objective id across objective sections: {objective.Id}"));
                    }

                    if (!string.IsNullOrWhiteSpace(objective.TargetRoomTag) && !knownRoomTags.Contains(objective.TargetRoomTag))
                    {
                        findings.Add(Finding("error", "TPL_SEMANTIC_INVALID", $"Unknown objective targetRoomTag: {objective.TargetRoomTag}"));
                    }
                }
            }

            private static void ValidatePositivePair(IReadOnlyList<int> values, string field, List<JsonObject> findings)
            {
                if (values.Count == 2 && values.Any(v => v < 1))
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} values must be positive"));
                }
            }

            private static void RangeAtLeast(int value, int minimum, string field, List<JsonObject> findings)
            {
                if (value < minimum)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be >= {minimum}"));
                }
            }

            private static void RangeAtLeast(double value, double minimum, string field, List<JsonObject> findings)
            {
                if (value < minimum)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be >= {minimum.ToString(CultureInfo.InvariantCulture)}"));
                }
            }

            private static void RangeBetween(double value, double minimum, double maximum, string field, List<JsonObject> findings)
            {
                if (value < minimum || value > maximum)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}"));
                }
            }

            private string MarkedString(string value, string field, List<JsonObject> findings, string path)
            {
                _seenFields.Add(field);
                return ParseString(value, field, findings, path);
            }

            private int MarkedInt(string value, string field, List<JsonObject> findings, string path)
            {
                _seenFields.Add(field);
                if (int.TryParse(Unquote(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be an integer", path));
                return 0;
            }

            private double MarkedDouble(string value, string field, List<JsonObject> findings, string path)
            {
                _seenFields.Add(field);
                if (double.TryParse(Unquote(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be a number", path));
                return 0;
            }

            private bool MarkedBool(string value, string field, List<JsonObject> findings, string path)
            {
                _seenFields.Add(field);
                return ParseBool(value, field, findings, path);
            }

            private int[] MarkedIntList(string value, string field, List<JsonObject> findings, string path)
            {
                _seenFields.Add(field);
                return ParseIntList(value, field, findings, path);
            }

            private static bool ParseBool(string value, string field, List<JsonObject> findings, string path)
            {
                if (bool.TryParse(Unquote(value), out var result))
                {
                    return result;
                }

                findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be a boolean", path));
                return false;
            }

            private static int[] ParseIntList(string value, string field, List<JsonObject> findings, string path)
            {
                var trimmed = value.Trim();
                if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be an inline integer array", path));
                    return Array.Empty<int>();
                }

                var pieces = trimmed.Trim('[', ']').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                var values = new List<int>();
                foreach (var piece in pieces)
                {
                    if (!int.TryParse(piece, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} contains a non-integer value", path));
                        return Array.Empty<int>();
                    }

                    values.Add(parsed);
                }

                return values.ToArray();
            }

            private static string ParseString(string value, string field, List<JsonObject> findings, string path)
            {
                var result = Unquote(value);
                if (string.IsNullOrWhiteSpace(result))
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"{field} must be a non-empty string", path));
                }

                return result;
            }

            private static void Required(string value, string field, List<JsonObject> findings)
            {
                Required(!string.IsNullOrWhiteSpace(value), field, findings);
            }

            private static void Required(bool ok, string field, List<JsonObject> findings)
            {
                if (!ok)
                {
                    findings.Add(Finding("error", "TPL_SCHEMA_INVALID", $"Missing or invalid required field: {field}"));
                }
            }

            private static (string Key, string Value) KeyValue(string line)
            {
                var idx = line.IndexOf(':');
                return idx < 0 ? (line.Trim(), "") : (line.Substring(0, idx).Trim(), line.Substring(idx + 1).Trim());
            }

            private static string StripComment(string value)
            {
                var inSingle = false;
                var inDouble = false;
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    if (c == '"' && !inSingle)
                    {
                        inDouble = !inDouble;
                    }
                    else if (c == '\'' && !inDouble)
                    {
                        inSingle = !inSingle;
                    }
                    else if (c == '#' && !inSingle && !inDouble)
                    {
                        return value.Substring(0, i);
                    }
                }

                return value;
            }

            private static string Unquote(string value)
            {
                value = value.Trim();
                if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                {
                    return value.Substring(1, value.Length - 2);
                }

                return value;
            }
        }

        private sealed class MissionActor
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "";
            public int[] CountRange { get; set; } = Array.Empty<int>();
            public string NavigationPolicy { get; set; } = "";
            public string PlacementPolicy { get; set; } = "";
            public int NormalizedCount => CountRange.Length == 2 ? CountRange[0] : 0;
        }

        private sealed class MissionObjective
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "";
            public bool? RequiresLayoutGraph { get; set; }
            public string TargetRoomTag { get; set; } = "";
            public bool? Optional { get; set; }
        }

        public static JsonObject BuildCoverGraph(
            string revisionId,
            IReadOnlyList<JsonObject> rooms,
            IReadOnlyList<JsonObject> portals,
            IReadOnlyList<JsonObject> breachPoints,
            int coverBudget)
        {
            var roomSnapshots = rooms.Select(RoomSnapshot.FromJson).ToList();
            if (roomSnapshots.Count == 0)
            {
                return new JsonObject
                {
                    ["layoutRevisionId"] = revisionId,
                    ["coverPoints"] = new JsonArray()
                };
            }

            var portalSnapshots = portals.Select(PortalSnapshot.FromJson).ToList();
            var breachSnapshots = breachPoints.Select(BreachSnapshot.FromJson).ToList();
            var roomsById = roomSnapshots.ToDictionary(room => room.Id, StringComparer.Ordinal);
            var coverPoints = new JsonArray();
            var totalBudget = Math.Max(0, coverBudget);

            for (var index = 0; index < totalBudget; index++)
            {
                var room = roomSnapshots[index % roomSnapshots.Count];
                var offset = 1 + (index / roomSnapshots.Count) % 3;
                var x = Clamp(room.X + offset, room.X + 1, room.Right - 1);
                var y = Clamp(room.Top - offset, room.Y + 1, room.Top - 1);

                if (TooCloseToPortal(room.Id, x, y, portalSnapshots) || TooCloseToBreach(room.Id, x, y, breachSnapshots))
                {
                    x = Clamp(room.Right - offset, room.X + 1, room.Right - 1);
                    y = Clamp(room.Y + offset, room.Y + 1, room.Top - 1);
                }

                coverPoints.Add(new JsonObject
                {
                    ["id"] = $"cover_{index + 1:00}",
                    ["layoutRevisionId"] = revisionId,
                    ["roomId"] = room.Id,
                    ["navNodeId"] = roomsById[room.Id].NavNodeId,
                    ["x"] = x,
                    ["y"] = y,
                    ["quality"] = index % 4 == 0 ? "low" : "medium"
                });
            }

            return new JsonObject
            {
                ["layoutRevisionId"] = revisionId,
                ["coverPoints"] = coverPoints
            };
        }

        private static bool TooCloseToPortal(string roomId, int x, int y, IEnumerable<PortalSnapshot> portals)
        {
            return portals.Any(p =>
                (string.Equals(p.FromRoomId, roomId, StringComparison.Ordinal) || string.Equals(p.ToRoomId, roomId, StringComparison.Ordinal)) &&
                Math.Abs(p.X - x) + Math.Abs(p.Y - y) < 3);
        }

        private static bool TooCloseToBreach(string roomId, int x, int y, IEnumerable<BreachSnapshot> breachPoints)
        {
            return breachPoints.Any(b =>
                string.Equals(b.RoomId, roomId, StringComparison.Ordinal) &&
                Math.Abs(b.X - x) + Math.Abs(b.Y - y) < 3);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Min(Math.Max(value, min), max);
        }

        public static JsonObject BuildVisibilityGraph(string revisionId, IReadOnlyList<JsonObject> portals)
        {
            var portalSnapshots = portals.Select(PortalSnapshot.FromJson).ToList();
            var edges = new JsonArray();
            for (var index = 0; index < portalSnapshots.Count; index++)
            {
                var portal = portalSnapshots[index];
                edges.Add(new JsonObject
                {
                    ["layoutRevisionId"] = revisionId,
                    ["fromRoomId"] = portal.FromRoomId,
                    ["toRoomId"] = portal.ToRoomId,
                    ["portalId"] = portal.Id,
                    ["openness"] = Math.Max(0.25, 0.75 - index * 0.03)
                });
            }

            return new JsonObject
            {
                ["layoutRevisionId"] = revisionId,
                ["edges"] = edges
            };
        }

        public static JsonObject BuildHearingGraph(string revisionId, IReadOnlyList<JsonObject> portals, double wallMultiplier, double doorPenalty)
        {
            var portalSnapshots = portals.Select(PortalSnapshot.FromJson).ToList();
            var edges = new JsonArray();
            for (var index = 0; index < Math.Min(64, portalSnapshots.Count); index++)
            {
                var portal = portalSnapshots[index];
                edges.Add(new JsonObject
                {
                    ["layoutRevisionId"] = revisionId,
                    ["fromRoomId"] = portal.FromRoomId,
                    ["toRoomId"] = portal.ToRoomId,
                    ["portalId"] = portal.Id,
                    ["attenuation"] = doorPenalty
                });
            }

            return new JsonObject
            {
                ["layoutRevisionId"] = revisionId,
                ["wallMultiplier"] = wallMultiplier,
                ["doorPenalty"] = doorPenalty,
                ["edges"] = edges
            };
        }

        public static JsonObject BuildVerificationTacticalMetrics(JsonObject layoutNode, JsonObject entitiesNode, List<JsonObject> findings)
        {
            var rooms = LayoutRooms(layoutNode);
            var actors = (entitiesNode["actors"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            var objectives = (entitiesNode["objectives"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            var objectiveRoomOccupancy = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var entity in actors.Concat(objectives))
            {
                var roomId = entity["roomId"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(roomId))
                {
                    objectiveRoomOccupancy.TryGetValue(roomId, out var currentOccupancy);
                    objectiveRoomOccupancy[roomId] = currentOccupancy + 1;
                }
            }

            var enemyCount = actors.Count(a =>
            {
                var type = a["type"]?.GetValue<string>() ?? "";
                return type.Contains("Enemy", StringComparison.OrdinalIgnoreCase) ||
                       type.Contains("Sentry", StringComparison.OrdinalIgnoreCase) ||
                       type.Contains("Roamer", StringComparison.OrdinalIgnoreCase);
            });

            var objectiveRoomIds = objectives
                .Select(o => o["roomId"]?.GetValue<string>() ?? "")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var portalPairs = UndirectedEdgePairs(layoutNode["PortalGraph"]?["portals"] as JsonArray);
            var visibilityPairs = UndirectedEdgePairs(layoutNode["VisibilityGraph"]?["edges"] as JsonArray);
            var hearingPairs = UndirectedEdgePairs(layoutNode["HearingGraph"]?["edges"] as JsonArray);
            var sharedHearingVisibilityPairs = hearingPairs.Intersect(visibilityPairs, StringComparer.Ordinal).Count();
            var portalDegrees = RoomDegrees(rooms, portalPairs);
            var coverPointCount = (layoutNode["CoverGraph"]?["coverPoints"] as JsonArray)?.Count ?? 0;
            var activeHearingChecks = (layoutNode["HearingGraph"]?["edges"] as JsonArray)?.Count ?? 0;
            var visibilityRayCount = (layoutNode["VisibilityGraph"]?["edges"] as JsonArray)?.Count ?? 0;
            var alternateRoutes = Math.Max(0, portalPairs.Count - Math.Max(0, rooms.Count - 1));
            var hearingOverlapPercentage = activeHearingChecks > 0
                ? Math.Round(sharedHearingVisibilityPairs * 100.0 / activeHearingChecks, 2)
                : 0;
            var chokepointRooms = portalDegrees.Values.Count(degree => degree <= 1);
            var chokepointPressure = rooms.Count > 0 ? Math.Round(chokepointRooms * 100.0 / rooms.Count, 2) : 0;
            var objectiveRoomPressure = objectiveRoomIds.Count > 0
                ? Math.Round(objectiveRoomIds.Sum(roomId => objectiveRoomOccupancy.TryGetValue(roomId, out var occupancy) ? occupancy : 0) * 1.0 / objectiveRoomIds.Count, 2)
                : 0;

            if (rooms.Count > 0 && actors.Count > rooms.Count * 6)
            {
                findings.Add(Finding("error", "TACTICAL_DENSITY_IMPOSSIBLE_BUDGET", "Actor density exceeds the verification budget of six actors per room"));
            }

            if (enemyCount > 0 && coverPointCount < enemyCount)
            {
                findings.Add(Finding("error", "TACTICAL_DENSITY_IMPOSSIBLE_BUDGET", "Enemy count exceeds available cover points"));
            }

            if (activeHearingChecks > 64)
            {
                findings.Add(Finding("error", "TB-AUD-003", "Active hearing edge count exceeds the verification budget"));
            }

            if (visibilityRayCount > 128)
            {
                findings.Add(Finding("error", "PERFORMANCE_BUDGET_EXCEEDED", "Visibility ray count exceeds the verification budget"));
            }

            return new JsonObject
            {
                ["enemyCount"] = enemyCount,
                ["activeHearingChecks"] = activeHearingChecks,
                ["visibilityRayCount"] = visibilityRayCount,
                ["coverPointCount"] = coverPointCount,
                ["averageCoverPerRoom"] = rooms.Count > 0 ? Math.Round(coverPointCount * 1.0 / rooms.Count, 2) : 0,
                ["alternateRoutes"] = alternateRoutes,
                ["hearingOverlapPercentage"] = hearingOverlapPercentage,
                ["chokepointPressure"] = chokepointPressure,
                ["objectiveRoomPressure"] = objectiveRoomPressure
            };
        }

        private static HashSet<string> UndirectedEdgePairs(JsonArray? edges)
        {
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in edges?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                var from = edge["fromRoomId"]?.GetValue<string>() ?? "";
                var to = edge["toRoomId"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    continue;
                }

                pairs.Add(RoomPairKey(from, to));
            }

            return pairs;
        }

        private static Dictionary<string, int> RoomDegrees(IReadOnlyList<JsonObject> rooms, HashSet<string> edgePairs)
        {
            var degrees = rooms
                .Select(room => room["id"]?.GetValue<string>() ?? "")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id, _ => 0, StringComparer.Ordinal);

            foreach (var pair in edgePairs)
            {
                var separatorIndex = pair.IndexOf('|');
                if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
                {
                    continue;
                }

                var left = pair.Substring(0, separatorIndex);
                var right = pair.Substring(separatorIndex + 1);
                if (degrees.ContainsKey(left))
                {
                    degrees[left]++;
                }

                if (degrees.ContainsKey(right))
                {
                    degrees[right]++;
                }
            }

            return degrees;
        }

        private static string RoomPairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        }

        private sealed record RoomSnapshot(string Id, string NavNodeId, int X, int Y, int Width, int Height)
        {
            public int Right => X + Width;
            public int Top => Y + Height;

            public static RoomSnapshot FromJson(JsonObject room)
            {
                var rect = room["rect"] as JsonObject;
                return new RoomSnapshot(
                    room["id"]?.GetValue<string>() ?? "",
                    room["navNodeId"]?.GetValue<string>() ?? "",
                    rect?["x"]?.GetValue<int>() ?? 0,
                    rect?["y"]?.GetValue<int>() ?? 0,
                    Math.Max(1, rect?["width"]?.GetValue<int>() ?? 1),
                    Math.Max(1, rect?["height"]?.GetValue<int>() ?? 1));
            }
        }

        private sealed record PortalSnapshot(string Id, string FromRoomId, string ToRoomId, int X, int Y)
        {
            public static PortalSnapshot FromJson(JsonObject portal)
            {
                return new PortalSnapshot(
                    portal["id"]?.GetValue<string>() ?? "",
                    portal["fromRoomId"]?.GetValue<string>() ?? "",
                    portal["toRoomId"]?.GetValue<string>() ?? "",
                    portal["x"]?.GetValue<int>() ?? 0,
                    portal["y"]?.GetValue<int>() ?? 0);
            }
        }

        private sealed record BreachSnapshot(string RoomId, int X, int Y)
        {
            public static BreachSnapshot FromJson(JsonObject breachPoint)
            {
                return new BreachSnapshot(
                    breachPoint["roomId"]?.GetValue<string>() ?? "",
                    breachPoint["x"]?.GetValue<int>() ?? 0,
                    breachPoint["y"]?.GetValue<int>() ?? 0);
            }
        }
    }
}
