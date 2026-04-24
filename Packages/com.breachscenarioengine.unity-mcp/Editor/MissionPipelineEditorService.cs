using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class MissionPipelineEditorService
    {
        private const string PipelineVersion = "2.2";
        private const string TemplateSchemaVersion = "tb.mission_template.v2.2";
        private const string PayloadSchemaVersion = "bse.mission_payload.v2.2";
        private static readonly HashSet<string> TacticalThemes = new(StringComparer.Ordinal) { "urban_cqb", "stealth_facility", "residential" };
        private static readonly HashSet<string> NavigationPolicies = new(StringComparer.Ordinal) { "FullAccess", "StaticGuard", "CanOpenDoors", "Immobilized" };
        private static readonly HashSet<string> PlacementPolicies = new(StringComparer.Ordinal) { "EntryPointOnly", "PostLayout_TaggedRoom", "PostLayout_AnyRoom", "SecureRoomOnly" };

        public static (bool Success, string Message) Execute(string action, string raw)
        {
            return action switch
            {
                "validate_template" => ValidateTemplate(raw),
                "compile_payload" => CompilePayload(raw),
                "generate_layout" => NotImplemented(action),
                "place_entities" => NotImplemented(action),
                "verify" => NotImplemented(action),
                "write_manifest" => NotImplemented(action),
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
            Directory.CreateDirectory(missionDir);
            Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);

            var payloadNode = BuildPayloadNode(template!);
            var payloadFindings = ValidatePayloadShape(payloadNode);
            findings.AddRange(payloadFindings);
            if (payloadFindings.Any(f => f["severity"]?.GetValue<string>() == "error"))
            {
                return (false, MissionResultJson("FAIL", template.MissionId, new[] { ToRepoPath(context.TemplatePath!) }, findings));
            }

            File.WriteAllText(payloadPath, payloadNode.ToJsonString() + Environment.NewLine);
            File.WriteAllText(reportPath, BuildCompileReportNode(template.MissionId, ToRepoPath(context.TemplatePath!), ToRepoPath(payloadPath), findings).ToJsonString() + Environment.NewLine);
            AssetDatabase.Refresh();

            var artifacts = new[] { ToRepoPath(payloadPath), ToRepoPath(reportPath) };
            return (true, MissionResultJson("PASS", template.MissionId, artifacts, findings));
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

        private static List<JsonObject> ValidatePayloadShape(JsonObject payload)
        {
            var findings = new List<JsonObject>();
            RequiredPayloadObject(payload, "header", findings);
            RequiredPayloadObject(payload, "spatial", findings);
            RequiredPayloadObject(payload, "logic", findings);
            RequiredPayloadArray(payload, "roster", findings);
            RequiredPayloadObject(payload, "objectives", findings);
            RequiredPayloadObject(payload, "profileRefs", findings);
            return findings;
        }

        private static void RequiredPayloadObject(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonObject)
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload missing object: {key}"));
            }
        }

        private static void RequiredPayloadArray(JsonObject obj, string key, List<JsonObject> findings)
        {
            if (obj[key] is not JsonArray)
            {
                findings.Add(Finding("error", "PAYLOAD_SCHEMA_INVALID", $"Generated payload missing array: {key}"));
            }
        }

        private static string MissionResultJson(string status, string missionId, IEnumerable<string> artifacts, IEnumerable<JsonObject> findings)
        {
            return new JsonObject
            {
                ["status"] = status,
                ["missionId"] = missionId,
                ["pipelineVersion"] = PipelineVersion,
                ["artifacts"] = new JsonArray(artifacts.Select(a => (JsonNode?)a).ToArray()),
                ["findings"] = FindingsArray(findings)
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
            return string.IsNullOrWhiteSpace(arg) ? Path.Combine(missionDir, fileName) : ToAbsoluteProjectPath(arg!);
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

            public static bool TryLoad(string path, string? requestedMissionId, out MissionTemplateModel? template, out List<JsonObject> findings)
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
                            findings.Add(Finding("warning", "TPL_UNKNOWN_TOP_LEVEL_FIELD", $"Unknown top-level section: {section}", pathPrefix));
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
                                findings.Add(Finding("warning", "TPL_UNKNOWN_OBJECTIVE_SECTION", $"Unknown objectives section: {objectiveSection}", pathPrefix));
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
                    default: findings.Add(Finding("warning", "TPL_UNKNOWN_TOP_LEVEL_FIELD", $"Unknown top-level field: {key}", path)); break;
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
                        else findings.Add(Finding("warning", "TPL_UNKNOWN_FIELD", $"Unknown generationMeta field: {key}", path));
                        break;
                    case "spatialConstraints":
                        if (key == "worldBounds") template.WorldBounds = template.MarkedIntList(value, "spatialConstraints.worldBounds", findings, path);
                        else if (key == "pixelsPerUnit") template.PixelsPerUnit = template.MarkedInt(value, "spatialConstraints.pixelsPerUnit", findings, path);
                        else if (key == "tacticalTheme") template.TacticalTheme = template.MarkedString(value, "spatialConstraints.tacticalTheme", findings, path);
                        else if (subsection == "bspConstraints" && key == "minRoomSize") template.MinRoomSize = template.MarkedIntList(value, "spatialConstraints.bspConstraints.minRoomSize", findings, path);
                        else if (subsection == "bspConstraints" && key == "maxRoomSize") template.MaxRoomSize = template.MarkedIntList(value, "spatialConstraints.bspConstraints.maxRoomSize", findings, path);
                        else if (subsection == "bspConstraints" && key == "corridorWidth") template.CorridorWidth = template.MarkedInt(value, "spatialConstraints.bspConstraints.corridorWidth", findings, path);
                        else if (subsection == "bspConstraints" && key == "forceRoomAdjacency") template.ForceRoomAdjacency = template.MarkedBool(value, "spatialConstraints.bspConstraints.forceRoomAdjacency", findings, path);
                        else findings.Add(Finding("warning", "TPL_UNKNOWN_FIELD", $"Unknown spatialConstraints field: {key}", path));
                        break;
                    case "tacticalRules":
                        if (key == "noiseAlertThreshold") template.NoiseAlertThreshold = template.MarkedDouble(value, "tacticalRules.noiseAlertThreshold", findings, path);
                        else if (key == "strictNavigationPolicy") template.StrictNavigationPolicy = template.MarkedBool(value, "tacticalRules.strictNavigationPolicy", findings, path);
                        else if (key == "enforcePostLayoutPlacement") template.EnforcePostLayoutPlacement = template.MarkedBool(value, "tacticalRules.enforcePostLayoutPlacement", findings, path);
                        else if (subsection == "acousticOcclusion" && key == "wallMultiplier") template.WallMultiplier = template.MarkedDouble(value, "tacticalRules.acousticOcclusion.wallMultiplier", findings, path);
                        else if (subsection == "acousticOcclusion" && key == "doorPenalty") template.DoorPenalty = template.MarkedDouble(value, "tacticalRules.acousticOcclusion.doorPenalty", findings, path);
                        else findings.Add(Finding("warning", "TPL_UNKNOWN_FIELD", $"Unknown tacticalRules field: {key}", path));
                        break;
                    default:
                        findings.Add(Finding("warning", "TPL_UNKNOWN_FIELD", $"Field is not in a known section: {key}", path));
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
                    default: findings.Add(Finding("warning", "TPL_UNKNOWN_ACTOR_FIELD", $"Unknown actor field: {key}", path)); break;
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
                    default: findings.Add(Finding("warning", "TPL_UNKNOWN_OBJECTIVE_FIELD", $"Unknown objective field: {key}", path)); break;
                }
            }

            private static void Validate(MissionTemplateModel template, string? requestedMissionId, List<JsonObject> findings)
            {
                Required(template.SchemaVersion, "schemaVersion", findings);
                Required(template.MissionId, "missionId", findings);
                Required(template.MissionTitle, "missionTitle", findings);
                Required(template._seenFields.Contains("generationMeta.initialSeed"), "generationMeta.initialSeed", findings);
                Required(template._seenFields.Contains("generationMeta.effectiveSeed"), "generationMeta.effectiveSeed", findings);
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
    }
}
