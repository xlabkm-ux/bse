using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class TacticalGraphBuilder
    {
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

        private static IReadOnlyList<JsonObject> LayoutRooms(JsonObject layoutNode)
        {
            return (layoutNode["RoomGraph"]?["rooms"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
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

        private static string RoomPairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
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
