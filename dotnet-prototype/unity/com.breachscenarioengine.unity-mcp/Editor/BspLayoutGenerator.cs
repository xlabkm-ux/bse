using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable

namespace BreachScenarioEngine.Mcp.Editor
{
    internal static class BspLayoutGenerator
    {
        public const string GeneratorId = "pure_bsp_layout_v1";

        public sealed class Options
        {
            public string PipelineVersion { get; set; } = "";
            public string MissionId { get; set; } = "";
            public string LayoutRevisionId { get; set; } = "";
            public int RequestedSeed { get; set; }
            public int GenerationSeed { get; set; }
            public int[] Bounds { get; set; } = Array.Empty<int>();
            public string Theme { get; set; } = "";
            public int PixelsPerUnit { get; set; }
            public int[] MinRoomSize { get; set; } = Array.Empty<int>();
            public int[] MaxRoomSize { get; set; } = Array.Empty<int>();
            public int CorridorWidth { get; set; }
            public bool ForceAdjacency { get; set; }
            public double WallMultiplier { get; set; }
            public double DoorPenalty { get; set; }
            public int CoverBudget { get; set; }
            public IReadOnlyList<string> ObjectiveRoomTags { get; set; } = Array.Empty<string>();
            public string SourcePayloadPath { get; set; } = "";
        }

        public static JsonObject Generate(Options options)
        {
            var boundsWidth = Math.Max(1, options.Bounds.ElementAtOrDefault(0));
            var boundsHeight = Math.Max(1, options.Bounds.ElementAtOrDefault(1));
            var minWidth = Math.Max(1, options.MinRoomSize.ElementAtOrDefault(0));
            var minHeight = Math.Max(1, options.MinRoomSize.ElementAtOrDefault(1));
            var maxWidth = Math.Max(minWidth, options.MaxRoomSize.ElementAtOrDefault(0));
            var maxHeight = Math.Max(minHeight, options.MaxRoomSize.ElementAtOrDefault(1));
            var random = new DeterministicRandom(options.GenerationSeed);

            var root = BuildTree(new RectInt(0, 0, boundsWidth, boundsHeight), minWidth, minHeight, maxWidth, maxHeight, random, 0);
            var leafRects = new List<RectInt>();
            CollectLeaves(root, leafRects);
            leafRects = leafRects
                .OrderBy(r => r.Y)
                .ThenBy(r => r.X)
                .ThenBy(r => r.Width)
                .ThenBy(r => r.Height)
                .ToList();

            var roomModels = BuildRooms(leafRects, options.LayoutRevisionId, options.ObjectiveRoomTags);
            var portals = BuildPortals(roomModels, options.LayoutRevisionId, Math.Max(1, options.CorridorWidth), options.ForceAdjacency);
            var windows = BuildWindows(roomModels, options.LayoutRevisionId, boundsWidth, boundsHeight);
            var breachPoints = BuildBreachPoints(roomModels, options.LayoutRevisionId);
            var coverPoints = BuildCoverPoints(roomModels, portals, breachPoints, options.LayoutRevisionId, Math.Max(roomModels.Count, options.CoverBudget));
            var visibilityEdges = BuildVisibilityEdges(portals, options.LayoutRevisionId);
            var hearingEdges = BuildHearingEdges(portals, options.LayoutRevisionId, options.DoorPenalty);

            var entryRoomId = roomModels.First(r => string.Equals(r.Tag, "entry", StringComparison.Ordinal)).Id;
            var objectiveRoomIds = roomModels
                .Where(r => options.ObjectiveRoomTags.Contains(r.Tag, StringComparer.Ordinal))
                .Select(r => (JsonNode?)r.Id)
                .ToArray();

            return new JsonObject
            {
                ["schemaVersion"] = "bse.mission_layout.v2.3",
                ["pipelineVersion"] = options.PipelineVersion,
                ["missionId"] = options.MissionId,
                ["layoutRevisionId"] = options.LayoutRevisionId,
                ["requestedSeed"] = options.RequestedSeed,
                ["generationSeed"] = options.GenerationSeed,
                ["generator"] = new JsonObject
                {
                    ["id"] = GeneratorId,
                    ["sourcePayloadPath"] = options.SourcePayloadPath,
                    ["step"] = 6,
                    ["pureData"] = true
                },
                ["retryPolicy"] = new JsonObject
                {
                    ["retryFromStep"] = 6,
                    ["retryAction"] = "generate_layout",
                    ["placementStep"] = 5
                },
                ["LayoutGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["bounds"] = IntArrayNode(new[] { boundsWidth, boundsHeight }),
                    ["theme"] = options.Theme,
                    ["ppu"] = options.PixelsPerUnit,
                    ["entryRoomId"] = entryRoomId,
                    ["objectiveRoomIds"] = new JsonArray(objectiveRoomIds),
                    ["breachPoints"] = new JsonArray(breachPoints.Select(b => (JsonNode?)b.DeepClone()).ToArray())
                },
                ["RoomGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["rooms"] = new JsonArray(roomModels.Select(r => (JsonNode?)r.ToJson()).ToArray())
                },
                ["PortalGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["portals"] = new JsonArray(portals.Select(p => (JsonNode?)p.ToJson()).ToArray()),
                    ["windows"] = new JsonArray(windows.Select(w => (JsonNode?)w).ToArray())
                },
                ["CoverGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["coverPoints"] = new JsonArray(coverPoints.Select(c => (JsonNode?)c).ToArray())
                },
                ["VisibilityGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["edges"] = new JsonArray(visibilityEdges.Select(e => (JsonNode?)e).ToArray())
                },
                ["HearingGraph"] = new JsonObject
                {
                    ["layoutRevisionId"] = options.LayoutRevisionId,
                    ["wallMultiplier"] = options.WallMultiplier,
                    ["doorPenalty"] = options.DoorPenalty,
                    ["edges"] = new JsonArray(hearingEdges.Select(e => (JsonNode?)e).ToArray())
                }
            };
        }

        private static SplitNode BuildTree(RectInt rect, int minWidth, int minHeight, int maxWidth, int maxHeight, DeterministicRandom random, int depth)
        {
            if (depth >= 8 || !ShouldSplit(rect, maxWidth, maxHeight))
            {
                return new SplitNode(rect);
            }

            var canSplitVertical = rect.Width >= (minWidth * 2);
            var canSplitHorizontal = rect.Height >= (minHeight * 2);
            if (!canSplitVertical && !canSplitHorizontal)
            {
                return new SplitNode(rect);
            }

            var splitVertical = ChooseVerticalSplit(rect, maxWidth, maxHeight, canSplitVertical, canSplitHorizontal, random);
            if (splitVertical)
            {
                var split = random.Range(minWidth, rect.Width - minWidth + 1);
                var left = new RectInt(rect.X, rect.Y, split, rect.Height);
                var right = new RectInt(rect.X + split, rect.Y, rect.Width - split, rect.Height);
                return new SplitNode(rect, BuildTree(left, minWidth, minHeight, maxWidth, maxHeight, random, depth + 1), BuildTree(right, minWidth, minHeight, maxWidth, maxHeight, random, depth + 1));
            }
            else
            {
                var split = random.Range(minHeight, rect.Height - minHeight + 1);
                var bottom = new RectInt(rect.X, rect.Y, rect.Width, split);
                var top = new RectInt(rect.X, rect.Y + split, rect.Width, rect.Height - split);
                return new SplitNode(rect, BuildTree(bottom, minWidth, minHeight, maxWidth, maxHeight, random, depth + 1), BuildTree(top, minWidth, minHeight, maxWidth, maxHeight, random, depth + 1));
            }
        }

        private static bool ShouldSplit(RectInt rect, int maxWidth, int maxHeight)
        {
            return rect.Width > maxWidth || rect.Height > maxHeight || rect.Width * rect.Height > maxWidth * maxHeight * 2;
        }

        private static bool ChooseVerticalSplit(RectInt rect, int maxWidth, int maxHeight, bool canSplitVertical, bool canSplitHorizontal, DeterministicRandom random)
        {
            if (!canSplitHorizontal)
            {
                return true;
            }

            if (!canSplitVertical)
            {
                return false;
            }

            var widthPressure = rect.Width / (double)Math.Max(1, maxWidth);
            var heightPressure = rect.Height / (double)Math.Max(1, maxHeight);
            if (Math.Abs(widthPressure - heightPressure) > 0.25)
            {
                return widthPressure > heightPressure;
            }

            return random.NextBool();
        }

        private static void CollectLeaves(SplitNode node, List<RectInt> leaves)
        {
            if (node.Left == null || node.Right == null)
            {
                leaves.Add(node.Rect);
                return;
            }

            CollectLeaves(node.Left, leaves);
            CollectLeaves(node.Right, leaves);
        }

        private static List<RoomModel> BuildRooms(IReadOnlyList<RectInt> rects, string revisionId, IReadOnlyList<string> objectiveRoomTags)
        {
            var rooms = rects.Select((rect, index) => new RoomModel
            {
                Id = $"room_{index + 1:00}",
                LayoutRevisionId = revisionId,
                Tag = index % 3 == 0 ? "living_area" : index % 3 == 1 ? "hallway" : "support",
                Rect = rect,
                NavNodeId = $"nav_{index + 1:00}"
            }).ToList();

            var entry = rooms
                .OrderBy(r => r.Rect.Y)
                .ThenBy(r => Math.Abs(r.Rect.CenterX - rects.Max(x => x.Right) / 2.0))
                .First();
            entry.Id = "room_entry";
            entry.Tag = "entry";
            entry.NavNodeId = "nav_entry";

            var requiredObjectiveTags = objectiveRoomTags
                .Where(t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, "entry", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var candidates = rooms
                .Where(r => !ReferenceEquals(r, entry))
                .OrderByDescending(r => DistanceSquared(entry.Rect.CenterX, entry.Rect.CenterY, r.Rect.CenterX, r.Rect.CenterY))
                .ToList();

            for (var i = 0; i < requiredObjectiveTags.Count && i < candidates.Count; i++)
            {
                candidates[i].Tag = requiredObjectiveTags[i];
                if (string.Equals(requiredObjectiveTags[i], "security_vault", StringComparison.Ordinal))
                {
                    candidates[i].Id = "room_vault";
                    candidates[i].NavNodeId = "nav_vault";
                }
            }

            return rooms
                .OrderBy(r => string.Equals(r.Id, "room_entry", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(r => r.Rect.Y)
                .ThenBy(r => r.Rect.X)
                .ToList();
        }

        private static List<PortalModel> BuildPortals(IReadOnlyList<RoomModel> rooms, string revisionId, int corridorWidth, bool forceAdjacency)
        {
            var candidates = new List<PortalModel>();
            for (var i = 0; i < rooms.Count; i++)
            {
                for (var j = i + 1; j < rooms.Count; j++)
                {
                    if (TryBuildPortal(rooms[i], rooms[j], revisionId, corridorWidth, out var portal))
                    {
                        candidates.Add(portal);
                    }
                }
            }

            candidates = candidates
                .OrderByDescending(p => p.SharedSpan)
                .ThenBy(p => p.FromRoomId)
                .ThenBy(p => p.ToRoomId)
                .ToList();

            var selected = new List<PortalModel>();
            var connected = new HashSet<string>(StringComparer.Ordinal);
            var remaining = new HashSet<string>(rooms.Select(r => r.Id), StringComparer.Ordinal);
            var entry = rooms.First(r => string.Equals(r.Tag, "entry", StringComparison.Ordinal)).Id;
            connected.Add(entry);
            remaining.Remove(entry);

            while (remaining.Count > 0)
            {
                var next = candidates.FirstOrDefault(p =>
                    (connected.Contains(p.FromRoomId) && remaining.Contains(p.ToRoomId)) ||
                    (connected.Contains(p.ToRoomId) && remaining.Contains(p.FromRoomId)));
                if (next == null)
                {
                    break;
                }

                selected.Add(next);
                connected.Add(next.FromRoomId);
                connected.Add(next.ToRoomId);
                remaining.Remove(next.FromRoomId);
                remaining.Remove(next.ToRoomId);
            }

            if (forceAdjacency)
            {
                foreach (var portal in candidates)
                {
                    if (!selected.Any(p => string.Equals(p.Id, portal.Id, StringComparison.Ordinal)))
                    {
                        selected.Add(portal);
                    }
                }
            }

            for (var i = 0; i < selected.Count; i++)
            {
                selected[i].Id = $"portal_{i + 1:00}";
            }

            return selected;
        }

        private static bool TryBuildPortal(RoomModel a, RoomModel b, string revisionId, int corridorWidth, out PortalModel portal)
        {
            portal = new PortalModel();
            var verticalTouch = a.Rect.Right == b.Rect.X || b.Rect.Right == a.Rect.X;
            var horizontalTouch = a.Rect.Top == b.Rect.Y || b.Rect.Top == a.Rect.Y;
            if (verticalTouch)
            {
                var min = Math.Max(a.Rect.Y, b.Rect.Y);
                var max = Math.Min(a.Rect.Top, b.Rect.Top);
                if (max <= min)
                {
                    return false;
                }

                var x = a.Rect.Right == b.Rect.X ? a.Rect.Right : b.Rect.Right;
                portal = new PortalModel(a.Id, b.Id, revisionId, "vertical", x, (min + max) / 2, Math.Min(corridorWidth, max - min), max - min);
                return true;
            }

            if (horizontalTouch)
            {
                var min = Math.Max(a.Rect.X, b.Rect.X);
                var max = Math.Min(a.Rect.Right, b.Rect.Right);
                if (max <= min)
                {
                    return false;
                }

                var y = a.Rect.Top == b.Rect.Y ? a.Rect.Top : b.Rect.Top;
                portal = new PortalModel(a.Id, b.Id, revisionId, "horizontal", (min + max) / 2, y, Math.Min(corridorWidth, max - min), max - min);
                return true;
            }

            return false;
        }

        private static List<JsonObject> BuildWindows(IReadOnlyList<RoomModel> rooms, string revisionId, int boundsWidth, int boundsHeight)
        {
            var windows = new List<JsonObject>();
            foreach (var room in rooms)
            {
                var side = room.Rect.Top == boundsHeight ? "north" :
                    room.Rect.X == 0 ? "west" :
                    room.Rect.Right == boundsWidth ? "east" :
                    "";
                if (string.IsNullOrWhiteSpace(side))
                {
                    continue;
                }

                windows.Add(new JsonObject
                {
                    ["id"] = $"window_{windows.Count + 1:00}",
                    ["layoutRevisionId"] = revisionId,
                    ["roomId"] = room.Id,
                    ["kind"] = "window",
                    ["side"] = side,
                    ["x"] = side == "west" ? room.Rect.X : side == "east" ? room.Rect.Right : room.Rect.CenterX,
                    ["y"] = side == "north" ? room.Rect.Top : room.Rect.CenterY,
                    ["width"] = Math.Min(2, Math.Max(1, room.Rect.Width - 2))
                });
            }

            return windows;
        }

        private static List<JsonObject> BuildBreachPoints(IReadOnlyList<RoomModel> rooms, string revisionId)
        {
            var entry = rooms.First(r => string.Equals(r.Tag, "entry", StringComparison.Ordinal));
            return new List<JsonObject>
            {
                new JsonObject
                {
                    ["id"] = "breach_01",
                    ["layoutRevisionId"] = revisionId,
                    ["roomId"] = entry.Id,
                    ["navNodeId"] = entry.NavNodeId,
                    ["kind"] = "entry_door",
                    ["side"] = "south",
                    ["x"] = entry.Rect.CenterX,
                    ["y"] = entry.Rect.Y,
                    ["width"] = 1
                }
            };
        }

        private static List<JsonObject> BuildCoverPoints(IReadOnlyList<RoomModel> rooms, IReadOnlyList<PortalModel> portals, IReadOnlyList<JsonObject> breachPoints, string revisionId, int coverBudget)
        {
            var covers = new List<JsonObject>();
            var roomsById = rooms.ToDictionary(r => r.Id, StringComparer.Ordinal);
            for (var index = 0; index < coverBudget; index++)
            {
                var room = rooms[index % rooms.Count];
                var offset = 1 + (index / rooms.Count) % 3;
                var x = Clamp(room.Rect.X + offset, room.Rect.X + 1, room.Rect.Right - 1);
                var y = Clamp(room.Rect.Top - offset, room.Rect.Y + 1, room.Rect.Top - 1);

                if (TooCloseToPortal(room.Id, x, y, portals) || TooCloseToBreach(room.Id, x, y, breachPoints))
                {
                    x = Clamp(room.Rect.Right - offset, room.Rect.X + 1, room.Rect.Right - 1);
                    y = Clamp(room.Rect.Y + offset, room.Rect.Y + 1, room.Rect.Top - 1);
                }

                covers.Add(new JsonObject
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

            return covers;
        }

        private static bool TooCloseToPortal(string roomId, int x, int y, IEnumerable<PortalModel> portals)
        {
            return portals.Any(p =>
                (string.Equals(p.FromRoomId, roomId, StringComparison.Ordinal) || string.Equals(p.ToRoomId, roomId, StringComparison.Ordinal)) &&
                Math.Abs(p.X - x) + Math.Abs(p.Y - y) < 3);
        }

        private static bool TooCloseToBreach(string roomId, int x, int y, IEnumerable<JsonObject> breachPoints)
        {
            return breachPoints.Any(b =>
                string.Equals(b["roomId"]?.GetValue<string>(), roomId, StringComparison.Ordinal) &&
                Math.Abs((b["x"]?.GetValue<int>() ?? x) - x) + Math.Abs((b["y"]?.GetValue<int>() ?? y) - y) < 3);
        }

        private static List<JsonObject> BuildVisibilityEdges(IReadOnlyList<PortalModel> portals, string revisionId)
        {
            return portals.Select((portal, index) => new JsonObject
            {
                ["layoutRevisionId"] = revisionId,
                ["fromRoomId"] = portal.FromRoomId,
                ["toRoomId"] = portal.ToRoomId,
                ["portalId"] = portal.Id,
                ["openness"] = Math.Max(0.25, 0.75 - index * 0.03)
            }).ToList();
        }

        private static List<JsonObject> BuildHearingEdges(IReadOnlyList<PortalModel> portals, string revisionId, double doorPenalty)
        {
            return portals.Select(portal => new JsonObject
            {
                ["layoutRevisionId"] = revisionId,
                ["fromRoomId"] = portal.FromRoomId,
                ["toRoomId"] = portal.ToRoomId,
                ["portalId"] = portal.Id,
                ["attenuation"] = doorPenalty
            }).ToList();
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

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Min(Math.Max(value, min), max);
        }

        private static double DistanceSquared(double ax, double ay, double bx, double by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        private sealed class SplitNode
        {
            public SplitNode(RectInt rect)
            {
                Rect = rect;
            }

            public SplitNode(RectInt rect, SplitNode left, SplitNode right)
            {
                Rect = rect;
                Left = left;
                Right = right;
            }

            public RectInt Rect { get; }
            public SplitNode? Left { get; }
            public SplitNode? Right { get; }
        }

        private readonly struct RectInt
        {
            public RectInt(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = Math.Max(1, width);
                Height = Math.Max(1, height);
            }

            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public int Right => X + Width;
            public int Top => Y + Height;
            public int CenterX => X + Width / 2;
            public int CenterY => Y + Height / 2;
        }

        private sealed class RoomModel
        {
            public string Id { get; set; } = "";
            public string LayoutRevisionId { get; set; } = "";
            public string Tag { get; set; } = "";
            public RectInt Rect { get; set; }
            public string NavNodeId { get; set; } = "";

            public JsonObject ToJson()
            {
                return new JsonObject
                {
                    ["id"] = Id,
                    ["layoutRevisionId"] = LayoutRevisionId,
                    ["tag"] = Tag,
                    ["rect"] = new JsonObject
                    {
                        ["x"] = Rect.X,
                        ["y"] = Rect.Y,
                        ["width"] = Rect.Width,
                        ["height"] = Rect.Height
                    },
                    ["navNodeId"] = NavNodeId
                };
            }
        }

        private sealed class PortalModel
        {
            public PortalModel()
            {
            }

            public PortalModel(string fromRoomId, string toRoomId, string revisionId, string orientation, int x, int y, int width, int sharedSpan)
            {
                FromRoomId = fromRoomId;
                ToRoomId = toRoomId;
                LayoutRevisionId = revisionId;
                Orientation = orientation;
                X = x;
                Y = y;
                Width = width;
                SharedSpan = sharedSpan;
            }

            public string Id { get; set; } = "";
            public string LayoutRevisionId { get; }
            public string FromRoomId { get; } = "";
            public string ToRoomId { get; } = "";
            public string Orientation { get; } = "";
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int SharedSpan { get; }

            public JsonObject ToJson()
            {
                return new JsonObject
                {
                    ["id"] = Id,
                    ["layoutRevisionId"] = LayoutRevisionId,
                    ["fromRoomId"] = FromRoomId,
                    ["toRoomId"] = ToRoomId,
                    ["kind"] = "door",
                    ["orientation"] = Orientation,
                    ["x"] = X,
                    ["y"] = Y,
                    ["width"] = Width
                };
            }
        }

        private sealed class DeterministicRandom
        {
            private uint _state;

            public DeterministicRandom(int seed)
            {
                _state = unchecked((uint)seed);
                if (_state == 0)
                {
                    _state = 0x6D2B79F5;
                }
            }

            public bool NextBool()
            {
                return (NextUInt() & 1) == 0;
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    return minInclusive;
                }

                return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
            }

            private uint NextUInt()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }
    }
}
