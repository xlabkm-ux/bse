using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BreachScenarioEngine.Mcp.Editor;
using UnityEditor;
using UnityEngine;

namespace BreachScenarioEngine.Editor.CI
{
    public static class Vs01MissionPipelineCi
    {
        private const string MissionId = "VS01_HostageApartment";
        private const string ReportPath = "Assets/Data/Mission/MissionConfig/VS01/Reports/VS01_verification_log.md";

        public static void RunVs01()
        {
            var results = new List<(string Action, bool Success, string Status, string Message)>();
            var raw = "{ \"missionId\": \"" + MissionId + "\" }";
            var actions = new[]
            {
                "validate_template",
                "compile_payload",
                "generate_layout",
                "place_entities",
                "verify",
                "write_manifest"
            };

            foreach (var action in actions)
            {
                var (success, message) = MissionPipelineEditorService.Execute(action, raw);
                var status = JsonStatus(message);
                results.Add((action, success, status, message));
                if (!success)
                {
                    break;
                }
            }

            WriteReport(results);
            AssetDatabase.Refresh();

            var passed = results.Count == actions.Length && results[^1].Success && results[^1].Status == "PASS";
            if (!passed)
            {
                Debug.LogError("VS01 mission pipeline failed. See " + ReportPath);
            }
            else
            {
                Debug.Log("VS01 mission pipeline passed.");
            }

            EditorApplication.Exit(passed ? 0 : 1);
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

        private static void WriteReport(IReadOnlyList<(string Action, bool Success, string Status, string Message)> results)
        {
            var fullPath = Path.Combine(ProjectRoot(), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var lines = new List<string>
            {
                "# VS01 Verification Log",
                "",
                "Mission: `VS01_HostageApartment`",
                "Pipeline: `v2.3`",
                "Generated: `" + DateTime.UtcNow.ToString("O") + "`",
                ""
            };

            foreach (var result in results)
            {
                var check = result.Success && result.Status == "PASS" ? "x" : " ";
                lines.Add("- [" + check + "] " + result.Action + " `" + result.Status + "`");
            }

            lines.Add("");
            lines.Add("Latest machine-readable status is owned by:");
            lines.Add("");
            lines.Add("- `UserMissionSources/missions/VS01_HostageApartment/verification_summary.json`");
            lines.Add("- `UserMissionSources/missions/VS01_HostageApartment/generation_manifest.json`");

            if (results.Count > 0 && (!results[^1].Success || results[^1].Status != "PASS"))
            {
                lines.Add("");
                lines.Add("Last failure:");
                lines.Add("");
                lines.Add("```json");
                lines.Add(results[^1].Message);
                lines.Add("```");
            }

            File.WriteAllLines(fullPath, lines);
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
