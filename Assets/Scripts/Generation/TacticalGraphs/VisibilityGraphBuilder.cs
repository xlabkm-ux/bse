using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class VisibilityGraphBuilder
    {
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
    }
}
