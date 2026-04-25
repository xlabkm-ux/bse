using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable

namespace BreachScenarioEngine.Generation.TacticalGraphs
{
    public static class CoverGraphBuilder
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
