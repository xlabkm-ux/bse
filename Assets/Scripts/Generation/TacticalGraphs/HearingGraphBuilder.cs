using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class HearingGraphBuilder
    {
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
