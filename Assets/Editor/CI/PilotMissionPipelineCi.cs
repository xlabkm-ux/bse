using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BreachScenarioEngine.Mcp.Editor;
using UnityEditor;
using UnityEngine;

namespace BreachScenarioEngine.Editor.CI
{
    public static class PilotMissionPipelineCi
    {
        private static readonly string[] MissionIds =
        {
            "VS01_HostageApartment",
            "VS02_DataRaidOffice",
            "VS03_StealthSafehouse"
        };

        private static readonly string[] Actions =
        {
            "validate_template",
            "compile_payload",
            "generate_layout",
            "place_entities",
            "verify",
            "write_manifest"
        };

        public static void RunAll()
        {
            RunAll(ShouldMaterializeScenePreview());
        }

        public static void RunAll(bool includeScenePreview)
        {
            var allResults = new List<MissionRunResult>();
            var passed = true;

            foreach (var missionId in MissionIds)
            {
                var result = RunMission(missionId, includeScenePreview);
                allResults.Add(result);
                passed &= result.Passed;
            }

            WritePilotReports(allResults);
            AssetDatabase.Refresh();

            if (!passed)
            {
                Debug.LogError("Pilot mission pipeline failed. See Artifacts/PilotReports/pilot_summary.md");
            }
            else
            {
                Debug.Log("Pilot mission pipeline passed for all missions.");
            }

            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static MissionRunResult RunMission(string missionId, bool includeScenePreview)
        {
            var actionResults = new List<ActionResult>();
            var raw = "{ \"missionId\": \"" + missionId + "\" }";

            foreach (var action in Actions)
            {
                var (success, message) = MissionPipelineEditorService.Execute(action, raw);
                var status = JsonStatus(message);
                actionResults.Add(new ActionResult(action, success, status, message));
                if (!success || status != "PASS")
                {
                    break;
                }
            }

            if (includeScenePreview && actionResults.Count == Actions.Length)
            {
                var previewResult = MaterializeScenePreview(missionId);
                actionResults.Add(previewResult);
            }

            return new MissionRunResult(missionId, actionResults);
        }

        private static ActionResult MaterializeScenePreview(string missionId)
        {
            var configPath = "Assets/Data/Mission/MissionConfig/MissionConfig_" + ShortCode(missionId) + ".asset";
            var config = AssetDatabase.LoadAssetAtPath<BreachScenarioEngine.Runtime.MissionConfig>(configPath);
            if (config == null)
            {
                return new ActionResult("materialize_scene_preview", false, "FAIL", "{\"status\":\"FAIL\",\"message\":\"MissionConfig missing\",\"missionId\":\"" + missionId + "\"}");
            }

            var host = new GameObject("PilotScenePreviewHost_" + missionId);
            try
            {
                var loader = host.AddComponent<BreachScenarioEngine.Runtime.MissionRuntimeLoader>();
                var success = loader.LoadMission(config);
                if (!success)
                {
                    return new ActionResult("materialize_scene_preview", false, "FAIL", "{\"status\":\"FAIL\",\"message\":\"" + EscapeJson(loader.LastError) + "\",\"missionId\":\"" + missionId + "\"}");
                }

                loader.ClearCurrentRoot();
                return new ActionResult("materialize_scene_preview", true, "PASS", "{\"status\":\"PASS\",\"message\":\"Scene preview materialized successfully.\",\"missionId\":\"" + missionId + "\"}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void WritePilotReports(IReadOnlyList<MissionRunResult> results)
        {
            var reportRoot = Path.Combine(ProjectRoot(), "Artifacts", "PilotReports");
            Directory.CreateDirectory(reportRoot);
            foreach (var staleReport in Directory.GetFiles(reportRoot))
            {
                File.Delete(staleReport);
            }

            var lines = new List<string>
            {
                "# Pilot Mission Summary",
                "",
                "Generated: `" + DateTime.UtcNow.ToString("O") + "`",
                "",
                "| Mission | Result | Last action | Last status |",
                "|---|---:|---|---|"
            };

            foreach (var result in results)
            {
                var last = result.Actions.Count > 0 ? result.Actions[^1] : new ActionResult("", false, "", "");
                lines.Add("| `" + result.MissionId + "` | " + (result.Passed ? "PASS" : "FAIL") + " | `" + last.Action + "` | `" + last.Status + "` |");
                CopyIfExists(result.MissionId, "verification_summary.json", reportRoot, ShortCode(result.MissionId) + "_verification_summary.json");
                CopyIfExists(result.MissionId, "generation_manifest.json", reportRoot, ShortCode(result.MissionId) + "_generation_manifest.json");

                if (!result.Passed && !string.IsNullOrWhiteSpace(last.Message))
                {
                    File.WriteAllText(Path.Combine(reportRoot, ShortCode(result.MissionId) + "_last_failure.json"), last.Message + Environment.NewLine);
                }
            }

            lines.Add("");
            lines.Add("Acceptance gate: validate_template, compile_payload, generate_layout, place_entities, verify, and write_manifest must all return PASS for VS01, VS02, and VS03.");
            File.WriteAllLines(Path.Combine(reportRoot, "pilot_summary.md"), lines);
        }

        private static void CopyIfExists(string missionId, string fileName, string reportRoot, string targetName)
        {
            var source = Path.Combine(ProjectRoot(), "UserMissionSources", "missions", missionId, fileName);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(reportRoot, targetName), true);
            }
        }

        private static string JsonStatus(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                return doc.RootElement.TryGetProperty("status", out var status)
                    ? status.GetString() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ShortCode(string missionId)
        {
            return missionId.Length >= 4 ? missionId.Substring(0, 4) : missionId;
        }

        private static bool ShouldMaterializeScenePreview()
        {
            var value = Environment.GetEnvironmentVariable("BSE_CI_MATERIALIZE_SCENE_PREVIEW") ?? "";
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct ActionResult
        {
            public ActionResult(string action, bool success, string status, string message)
            {
                Action = action;
                Success = success;
                Status = status;
                Message = message;
            }

            public string Action { get; }
            public bool Success { get; }
            public string Status { get; }
            public string Message { get; }
        }

        private sealed class MissionRunResult
        {
            public MissionRunResult(string missionId, IReadOnlyList<ActionResult> actions)
            {
                MissionId = missionId;
                Actions = actions;
                IncludeScenePreview = actions.Count > PilotMissionPipelineCi.Actions.Length;
            }

            public string MissionId { get; }
            public IReadOnlyList<ActionResult> Actions { get; }
            public bool IncludeScenePreview { get; }
            public bool Passed => Actions.Count >= PilotMissionPipelineCi.Actions.Length && Actions[^1].Success && Actions[^1].Status == "PASS";
        }
    }
}
