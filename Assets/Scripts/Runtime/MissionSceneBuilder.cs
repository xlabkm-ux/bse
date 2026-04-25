using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    public sealed class MissionSceneBuilder : MonoBehaviour
    {
        private const float WallThickness = 0.18f;
        private static Sprite sharedSprite;

        [SerializeField] private float roomScale = 0.18f;
        [SerializeField] private Color roomColor = new(0.18f, 0.22f, 0.24f, 0.72f);
        [SerializeField] private Color wallColor = new(0.78f, 0.82f, 0.84f, 1f);
        [SerializeField] private Color doorColor = new(0.9f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color coverColor = new(0.28f, 0.55f, 0.42f, 1f);
        [SerializeField] private Color operativeColor = new(0.24f, 0.62f, 0.9f, 1f);
        [SerializeField] private Color enemyColor = new(0.84f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color hostageColor = new(0.92f, 0.82f, 0.34f, 1f);
        [SerializeField] private Color objectiveColor = new(0.56f, 0.36f, 0.86f, 1f);

        public GameObject Build(MissionConfig config, MissionManifest manifest, MissionLayout layout, MissionEntities entities)
        {
            var missionId = config.MissionId;
            var layoutRevisionId = layout.layoutRevisionId;
            var root = new GameObject("GeneratedMissionRoot_" + missionId);
            var layoutRoot = Child(root.transform, "Layout");
            var roomsRoot = Child(layoutRoot, "Rooms");
            var portalsRoot = Child(layoutRoot, "Portals");
            var coverRoot = Child(root.transform, "Cover");
            var actorsRoot = Child(root.transform, "Actors");
            var objectivesRoot = Child(root.transform, "Objectives");
            Child(root.transform, "Debug");

            var rooms = ReadRooms(layout);
            foreach (var room in rooms.Values)
            {
                CreateRoom(roomsRoot, room, missionId, layoutRevisionId);
            }

            foreach (var portal in layout.PortalGraph.portals)
            {
                CreatePortal(portalsRoot, portal, rooms, missionId, layoutRevisionId);
            }

            foreach (var cover in layout.CoverGraph.coverPoints)
            {
                var roomId = cover.roomId;
                var position = RoomPosition(rooms, roomId, 0.22f, -0.22f);
                CreateMarker(coverRoot, cover.id, position, new Vector2(0.45f, 0.45f), coverColor, "cover", missionId, cover.id, cover.id, layoutRevisionId, false);
            }

            var actorIndex = 0;
            foreach (var actor in entities.actors)
            {
                actorIndex++;
                var type = actor.type;
                var color = type == "Operative" ? operativeColor : type == "Hostage" ? hostageColor : enemyColor;
                var offset = Offset(actorIndex, 0.34f);
                var position = RoomPosition(rooms, actor.roomId, offset.x, offset.y);
                CreateMarker(actorsRoot, actor.entityId, position, new Vector2(0.5f, 0.5f), color, type, missionId, actor.entityId, actor.sourceActorId, layoutRevisionId, false, actor.ownership);
            }

            foreach (var objective in entities.objectives)
            {
                var objectiveId = objective.entityId;
                var position = RoomPosition(rooms, objective.roomId, -0.28f, 0.28f);
                var marker = CreateMarker(objectivesRoot, objectiveId, position, new Vector2(0.62f, 0.62f), objectiveColor, objective.type, missionId, objectiveId, objectiveId, layoutRevisionId, true, objective.ownership);
                var trigger = marker.AddComponent<MissionCompleteTrigger>();
                trigger.Initialize(missionId, objectiveId);
            }

            root.AddComponent<PilotMissionRuntimeState>().Initialize(
                missionId,
                manifest.effectiveSeed,
                layoutRevisionId,
                manifest.status,
                rooms.Count);

            return root;
        }

        private void CreateRoom(Transform parent, RoomInfo room, string missionId, string layoutRevisionId)
        {
            var roomObject = CreateRect(parent, room.Id, room.Center, room.Size, roomColor, 0);
            roomObject.AddComponent<BoxCollider2D>();
            AddMarker(roomObject, missionId, room.Id, room.Id, layoutRevisionId, missionId + ":" + layoutRevisionId + ":" + room.Id);

            CreateRect(roomObject.transform, "Wall_N", new Vector2(0, room.Size.y / 2), new Vector2(room.Size.x, WallThickness), wallColor, 1);
            CreateRect(roomObject.transform, "Wall_S", new Vector2(0, -room.Size.y / 2), new Vector2(room.Size.x, WallThickness), wallColor, 1);
            CreateRect(roomObject.transform, "Wall_E", new Vector2(room.Size.x / 2, 0), new Vector2(WallThickness, room.Size.y), wallColor, 1);
            CreateRect(roomObject.transform, "Wall_W", new Vector2(-room.Size.x / 2, 0), new Vector2(WallThickness, room.Size.y), wallColor, 1);
        }

        private void CreatePortal(Transform parent, MissionPortal portal, IReadOnlyDictionary<string, RoomInfo> rooms, string missionId, string layoutRevisionId)
        {
            var from = portal.fromRoomId;
            var to = portal.toRoomId;
            if (!rooms.TryGetValue(from, out var fromRoom) || !rooms.TryGetValue(to, out var toRoom))
            {
                return;
            }

            var id = portal.id;
            var position = (fromRoom.Center + toRoom.Center) * 0.5f;
            var marker = CreateRect(parent, id, position, new Vector2(0.7f, 0.7f), doorColor, 2);
            AddMarker(marker, missionId, id, id, layoutRevisionId, missionId + ":" + layoutRevisionId + ":" + id);
        }

        private GameObject CreateMarker(Transform parent, string name, Vector2 position, Vector2 size, Color color, string label, string missionId, string entityId, string sourceId, string layoutRevisionId, bool trigger, MissionOwnership ownership = null)
        {
            var marker = CreateRect(parent, name, position, size, color, 3);
            var collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;

            if (string.IsNullOrEmpty(sourceId))
            {
                sourceId = entityId;
            }

            var stableKey = missionId + ":" + layoutRevisionId + ":" + entityId;
            if (ownership != null)
            {
                stableKey = string.IsNullOrEmpty(ownership.stableKey) ? stableKey : ownership.stableKey;
                sourceId = string.IsNullOrEmpty(ownership.sourceId) ? sourceId : ownership.sourceId;
            }

            AddMarker(marker, missionId, entityId, sourceId, layoutRevisionId, stableKey);
            marker.name = string.IsNullOrEmpty(label) ? name : name + "_" + label;
            return marker;
        }

        private GameObject CreateRect(Transform parent, string name, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = SharedSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return item;
        }

        private static void AddMarker(GameObject target, string missionId, string entityId, string sourceId, string layoutRevisionId, string stableKey)
        {
            target.AddComponent<GeneratedOwnershipMarker>().Initialize("bse-pipeline", missionId, entityId, sourceId, layoutRevisionId, stableKey);
        }

        private Dictionary<string, RoomInfo> ReadRooms(MissionLayout layout)
        {
            var rooms = new Dictionary<string, RoomInfo>(StringComparer.Ordinal);
            foreach (var room in layout.RoomGraph.rooms)
            {
                if (room.rect == null)
                {
                    continue;
                }

                var id = room.id;
                var x = room.rect.x * roomScale;
                var y = room.rect.y * roomScale;
                var width = Math.Max(1f, room.rect.width * roomScale);
                var height = Math.Max(1f, room.rect.height * roomScale);
                rooms[id] = new RoomInfo(id, new Vector2(x + width / 2f, y + height / 2f), new Vector2(width, height));
            }

            return rooms;
        }

        private static Vector2 RoomPosition(IReadOnlyDictionary<string, RoomInfo> rooms, string roomId, float offsetX, float offsetY)
        {
            return rooms.TryGetValue(roomId, out var room) ? room.Center + new Vector2(offsetX, offsetY) : new Vector2(offsetX, offsetY);
        }

        private static Vector2 Offset(int index, float radius)
        {
            var angle = index * 1.618f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Sprite SharedSprite()
        {
            if (sharedSprite != null)
            {
                return sharedSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "BSE_Debug_Rect";
            return sharedSprite;
        }

        private readonly struct RoomInfo
        {
            public RoomInfo(string id, Vector2 center, Vector2 size)
            {
                Id = id;
                Center = center;
                Size = size;
            }

            public string Id { get; }
            public Vector2 Center { get; }
            public Vector2 Size { get; }
        }
    }
}
