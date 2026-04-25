using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BreachScenarioEngine.Runtime
{
    [Serializable]
    public sealed class MissionSceneMaterializationFinding
    {
        public string code = "";
        public string message = "";
        public string artifactPath = "";
    }

    [Serializable]
    public sealed class MissionSceneMaterializationMetrics
    {
        public int roomCount;
        public int portalCount;
        public int catalogCount;
        public int doorCount;
        public int windowCount;
        public int coverCount;
        public int actorCount;
        public int objectiveCount;
        public int extractionZoneCount;
        public int clearedGeneratedObjectCount;
        public int tileCount;
    }

    [Serializable]
    public sealed class MissionSceneMaterializationReport
    {
        public string status = "";
        public string missionId = "";
        public string pipelineVersion = "";
        public string action = "materialize_scene_preview";
        public string message = "";
        public string[] artifacts = Array.Empty<string>();
        public MissionSceneMaterializationFinding[] findings = Array.Empty<MissionSceneMaterializationFinding>();
        public MissionSceneMaterializationMetrics metrics = new();
    }

    public static class MissionSceneMaterializer
    {
        private const string GeneratedOwner = "bse-pipeline";
        private static readonly Dictionary<string, TileBase> TileCache = new(StringComparer.Ordinal);
        private static Sprite sharedSprite;

        public static MissionSceneMaterializationReport Materialize(
            MissionSceneContext context,
            MissionConfig config,
            MissionManifest manifest,
            MissionLayout layout,
            MissionEntities entities)
        {
            var report = new MissionSceneMaterializationReport
            {
                missionId = config != null ? config.MissionId : "",
                pipelineVersion = manifest != null ? manifest.pipelineVersion : "2.3"
            };

            var findings = new List<MissionSceneMaterializationFinding>();
            var artifacts = new List<string>();

            if (config != null)
            {
                AddArtifact(artifacts, config.PayloadPath);
                AddArtifact(artifacts, config.LayoutPath);
                AddArtifact(artifacts, config.EntitiesPath);
                AddArtifact(artifacts, config.VerificationSummaryPath);
                AddArtifact(artifacts, config.GenerationManifestPath);
            }

            if (context == null)
            {
                findings.Add(Finding("SCENE_CONTEXT_MISSING", "MissionSceneContext is missing.", ""));
                return Complete(report, findings, artifacts, "MissionSceneContext is missing.");
            }

            if (config == null)
            {
                findings.Add(Finding("SCENE_CONFIG_MISSING", "MissionConfig is missing.", ""));
                return Complete(report, findings, artifacts, "MissionConfig is missing.");
            }

            if (manifest == null)
            {
                findings.Add(Finding("SCENE_MANIFEST_MISSING", "Mission manifest is missing.", config.GenerationManifestPath));
                return Complete(report, findings, artifacts, "Mission manifest is missing.");
            }

            if (!string.Equals(manifest.status, "PASS", StringComparison.Ordinal))
            {
                findings.Add(Finding("SCENE_MANIFEST_NOT_PASS", "Scene preview requires a PASS manifest.", config.GenerationManifestPath));
                return Complete(report, findings, artifacts, "Scene preview requires a PASS manifest.");
            }

            if (layout == null)
            {
                findings.Add(Finding("SCENE_LAYOUT_MISSING", "Layout artifact is missing or unreadable.", config.LayoutPath));
                return Complete(report, findings, artifacts, "Layout artifact is missing or unreadable.");
            }

            if (entities == null)
            {
                findings.Add(Finding("SCENE_ENTITIES_MISSING", "Entities artifact is missing or unreadable.", config.EntitiesPath));
                return Complete(report, findings, artifacts, "Entities artifact is missing or unreadable.");
            }

            ValidateArtifactConsistency(config, manifest, layout, entities, findings);
            ValidateLayoutGeometry(layout, findings, config.LayoutPath);
            ValidateCatalogRefs(config, findings, artifacts);
            if (findings.Count > 0)
            {
                report.status = "FAIL";
                report.message = findings[0].message;
                report.findings = findings.ToArray();
                report.artifacts = artifacts.ToArray();
                return report;
            }

            EnsureStructure(context, config, layout, manifest);
            var clearedGeneratedObjectCount = ClearGeneratedContent(context);
            var rooms = BuildRoomLookup(layout.RoomGraph.rooms);

            report.metrics.roomCount = layout.RoomGraph.rooms != null ? layout.RoomGraph.rooms.Length : 0;
            report.metrics.portalCount = layout.PortalGraph.portals != null ? layout.PortalGraph.portals.Length : 0;
            report.metrics.coverCount = layout.CoverGraph.coverPoints != null ? layout.CoverGraph.coverPoints.Length : 0;
            report.metrics.actorCount = entities.actors != null ? entities.actors.Length : 0;
            report.metrics.objectiveCount = entities.objectives != null ? entities.objectives.Length : 0;
            report.metrics.catalogCount = CountCatalogRefs(config);
            report.metrics.clearedGeneratedObjectCount = clearedGeneratedObjectCount;

            MaterializeRooms(layout, context, findings, ref report.metrics.tileCount);
            MaterializePortals(layout, context, rooms, findings, ref report.metrics.doorCount, ref report.metrics.windowCount, ref report.metrics.extractionZoneCount);
            MaterializeCovers(layout, context, rooms, findings);
            MaterializeActors(entities, context, rooms, findings);
            MaterializeObjectives(entities, context, rooms, findings);

            report.status = findings.Count == 0 ? "PASS" : "FAIL";
            report.message = findings.Count == 0 ? "Scene materialized successfully." : findings[0].message;
            report.findings = findings.ToArray();
            report.artifacts = artifacts.ToArray();
            return report;
        }

        private static void EnsureStructure(MissionSceneContext context, MissionConfig config, MissionLayout layout, MissionManifest manifest)
        {
            var root = context.gameObject;
            AddMarker(root, config.MissionId, config.MissionId + ":scene_root", config.MissionId, layout.layoutRevisionId, "scene-root");

            context.Grid = EnsureChildComponent<Grid>(root.transform, "Grid", context.Grid);
            context.Grid.cellSize = Vector3.one;
            context.Grid.cellGap = Vector3.zero;

            context.BaseMap = EnsureTilemap(context.Grid.transform, "World_Base", context.BaseMap, 0, false, false);
            context.CollisionMap = EnsureTilemap(context.Grid.transform, "World_Collision", context.CollisionMap, 1, true, true);
            context.DecorMap = EnsureTilemap(context.Grid.transform, "World_Decor", context.DecorMap, 2, false, false);
            context.InteractablesMap = EnsureTilemap(context.Grid.transform, "World_Interactables", context.InteractablesMap, 2, false, false);

            var generated = EnsureContainer(root.transform, "Generated", config.MissionId, layout.layoutRevisionId);
            context.DoorsRoot = EnsureContainer(generated, "Doors", config.MissionId, layout.layoutRevisionId);
            context.WindowsRoot = EnsureContainer(generated, "Windows", config.MissionId, layout.layoutRevisionId);
            context.CoversRoot = EnsureContainer(generated, "Covers", config.MissionId, layout.layoutRevisionId);
            context.EnemiesRoot = EnsureContainer(generated, "Enemies", config.MissionId, layout.layoutRevisionId);
            context.OperativesRoot = EnsureContainer(generated, "Operatives", config.MissionId, layout.layoutRevisionId);
            context.ObjectivesRoot = EnsureContainer(generated, "Objectives", config.MissionId, layout.layoutRevisionId);
            context.HostagesRoot = EnsureContainer(generated, "Hostages", config.MissionId, layout.layoutRevisionId);
            context.ExtractionRoot = EnsureContainer(generated, "Extraction", config.MissionId, layout.layoutRevisionId);
            context.DebugRoot = EnsureContainer(generated, "Debug", config.MissionId, layout.layoutRevisionId);

            EnsureMarker(context.Grid.gameObject, config.MissionId, "Grid", "grid", layout.layoutRevisionId);
            EnsureMarker(context.BaseMap.gameObject, config.MissionId, "World_Base", "base-map", layout.layoutRevisionId);
            EnsureMarker(context.CollisionMap.gameObject, config.MissionId, "World_Collision", "collision-map", layout.layoutRevisionId);
            EnsureMarker(context.DecorMap.gameObject, config.MissionId, "World_Decor", "decor-map", layout.layoutRevisionId);
            EnsureMarker(context.InteractablesMap.gameObject, config.MissionId, "World_Interactables", "interactables-map", layout.layoutRevisionId);
        }

        private static int ClearGeneratedContent(MissionSceneContext context)
        {
            var cleared = 0;
            cleared += ClearTransformChildren(context.DoorsRoot);
            cleared += ClearTransformChildren(context.WindowsRoot);
            cleared += ClearTransformChildren(context.CoversRoot);
            cleared += ClearTransformChildren(context.EnemiesRoot);
            cleared += ClearTransformChildren(context.OperativesRoot);
            cleared += ClearTransformChildren(context.ObjectivesRoot);
            cleared += ClearTransformChildren(context.HostagesRoot);
            cleared += ClearTransformChildren(context.ExtractionRoot);
            cleared += ClearTransformChildren(context.DebugRoot);
            ClearTilemap(context.BaseMap);
            ClearTilemap(context.CollisionMap);
            ClearTilemap(context.DecorMap);
            ClearTilemap(context.InteractablesMap);
            return cleared;
        }

        private static void MaterializeRooms(MissionLayout layout, MissionSceneContext context, List<MissionSceneMaterializationFinding> findings, ref int tileCount)
        {
            if (layout.RoomGraph.rooms == null)
            {
                findings.Add(Finding("SCENE_ROOM_GRAPH_MISSING", "Room graph is missing.", ""));
                return;
            }

            foreach (var room in layout.RoomGraph.rooms)
            {
                if (room == null || room.rect == null)
                {
                    continue;
                }

                var x = Mathf.RoundToInt(room.rect.x);
                var y = Mathf.RoundToInt(room.rect.y);
                var width = Mathf.Max(1, Mathf.RoundToInt(room.rect.width));
                var height = Mathf.Max(1, Mathf.RoundToInt(room.rect.height));
                var floorTile = TileFor("floor", new Color(0.18f, 0.22f, 0.24f, 0.72f));
                var wallTile = TileFor("wall", new Color(0.78f, 0.82f, 0.84f, 1f));

                for (var tx = x; tx < x + width; tx++)
                {
                    for (var ty = y; ty < y + height; ty++)
                    {
                        SetTile(context.BaseMap, tx, ty, floorTile);
                        tileCount++;
                    }
                }

                for (var tx = x; tx < x + width; tx++)
                {
                    SetTile(context.CollisionMap, tx, y, wallTile);
                    SetTile(context.CollisionMap, tx, y + height - 1, wallTile);
                    tileCount += 2;
                }

                for (var ty = y; ty < y + height; ty++)
                {
                    SetTile(context.CollisionMap, x, ty, wallTile);
                    SetTile(context.CollisionMap, x + width - 1, ty, wallTile);
                    tileCount += 2;
                }
            }
        }

        private static void MaterializePortals(
            MissionLayout layout,
            MissionSceneContext context,
            IReadOnlyDictionary<string, MissionRoom> rooms,
            List<MissionSceneMaterializationFinding> findings,
            ref int doorCount,
            ref int windowCount,
            ref int extractionZoneCount)
        {
            if (layout.PortalGraph.portals != null)
            {
                foreach (var portal in layout.PortalGraph.portals)
                {
                    if (portal == null)
                    {
                        continue;
                    }

                    var parent = IsWindowPortal(portal) ? context.WindowsRoot : context.DoorsRoot;
                    var color = IsWindowPortal(portal)
                        ? new Color(0.7f, 0.85f, 0.98f, 1f)
                        : new Color(0.9f, 0.72f, 0.22f, 1f);
                    var position = new Vector3(portal.x, portal.y, 0f);
                    var width = Mathf.Max(0.5f, portal.width);
                    var size = IsHorizontal(portal.orientation) ? new Vector3(width, 0.35f, 1f) : new Vector3(0.35f, width, 1f);

                    if (!IsWindowPortal(portal))
                    {
                        ClearCollisionSegment(context.CollisionMap, portal.x, portal.y, portal.width, portal.orientation);
                    }

                    CreateMarker(parent, layout.missionId, portal.id, position, size, color, portal.kind, portal.id, portal.id, portal.layoutRevisionId, false);

                    if (IsWindowPortal(portal))
                    {
                        windowCount++;
                    }
                    else
                    {
                        doorCount++;
                    }
                }
            }

            if (layout.LayoutGraph.breachPoints == null)
            {
                return;
            }

            foreach (var breachPoint in layout.LayoutGraph.breachPoints)
            {
                if (breachPoint == null)
                {
                    continue;
                }

                var position = new Vector3(breachPoint.x, breachPoint.y, 0f);
                var size = new Vector3(Mathf.Max(0.5f, breachPoint.width), 0.35f, 1f);
                ClearCollisionSegment(context.CollisionMap, breachPoint.x, breachPoint.y, breachPoint.width, breachPoint.side);
                CreateMarker(context.DoorsRoot, layout.missionId, breachPoint.id, position, size, new Color(0.95f, 0.42f, 0.24f, 1f), breachPoint.kind, breachPoint.id, breachPoint.id, breachPoint.layoutRevisionId, false);
                doorCount++;
            }

            if (!string.IsNullOrEmpty(layout.LayoutGraph.entryRoomId) && rooms.TryGetValue(layout.LayoutGraph.entryRoomId, out var entryRoom))
            {
                var extractionPosition = RoomCenter(entryRoom);
                CreateMarker(context.ExtractionRoot, layout.missionId, "ExtractionZone", extractionPosition, new Vector3(1.25f, 1.25f, 1f), new Color(0.42f, 0.9f, 0.45f, 0.4f), "extraction", "ExtractionZone", "ExtractionZone", layout.layoutRevisionId, true);
                extractionZoneCount++;
            }
        }

        private static void MaterializeCovers(MissionLayout layout, MissionSceneContext context, IReadOnlyDictionary<string, MissionRoom> rooms, List<MissionSceneMaterializationFinding> findings)
        {
            if (layout.CoverGraph.coverPoints == null)
            {
                return;
            }

            foreach (var cover in layout.CoverGraph.coverPoints)
            {
                if (cover == null)
                {
                    continue;
                }

                var roomPosition = new Vector3(cover.x, cover.y, 0f);
                CreateMarker(context.CoversRoot, layout.missionId, cover.id, roomPosition, new Vector3(0.45f, 0.45f, 1f), new Color(0.28f, 0.55f, 0.42f, 1f), cover.quality, cover.id, cover.id, cover.layoutRevisionId, false);
            }
        }

        private static void MaterializeActors(MissionEntities entities, MissionSceneContext context, IReadOnlyDictionary<string, MissionRoom> rooms, List<MissionSceneMaterializationFinding> findings)
        {
            if (entities.actors == null)
            {
                return;
            }

            var slotIndex = 0;
            foreach (var actor in entities.actors)
            {
                if (actor == null)
                {
                    continue;
                }

                slotIndex++;
                var parent = ResolveActorParent(context, actor.type);
                var position = ResolveRoomAnchor(rooms, actor.roomId, actor.entityId, slotIndex);
                var color = ResolveActorColor(actor.type);
                CreateMarker(parent, entities.missionId, actor.entityId, position, new Vector3(0.5f, 0.5f, 1f), color, actor.type, actor.entityId, actor.sourceActorId, actor.layoutRevisionId, false, actor.ownership);
            }
        }

        private static void MaterializeObjectives(MissionEntities entities, MissionSceneContext context, IReadOnlyDictionary<string, MissionRoom> rooms, List<MissionSceneMaterializationFinding> findings)
        {
            if (entities.objectives == null)
            {
                return;
            }

            foreach (var objective in entities.objectives)
            {
                if (objective == null)
                {
                    continue;
                }

                var position = ResolveRoomAnchor(rooms, objective.roomId, objective.entityId, objective.optional ? 2 : 1);
                var marker = CreateMarker(context.ObjectivesRoot, entities.missionId, objective.entityId, position, new Vector3(0.62f, 0.62f, 1f), new Color(0.56f, 0.36f, 0.86f, 1f), objective.type, objective.entityId, objective.entityId, objective.layoutRevisionId, true, objective.ownership);
                if (marker != null)
                {
                    var trigger = marker.AddComponent<MissionCompleteTrigger>();
                    trigger.Initialize(entities.missionId, objective.entityId);
                }
            }
        }

        private static void ValidateCatalogRefs(MissionConfig config, List<MissionSceneMaterializationFinding> findings, List<string> artifacts)
        {
            if (config == null)
            {
                findings.Add(Finding("SCENE_CONFIG_MISSING", "MissionConfig is missing.", ""));
                return;
            }

            ValidateCatalogAsset(config.EnemyCatalog, "enemyCatalog", findings, artifacts);
            ValidateCatalogAsset(config.EnvironmentCatalog, "environmentCatalog", findings, artifacts);
            ValidateCatalogAsset(config.ObjectiveCatalog, "objectiveCatalog", findings, artifacts);
        }

        private static void ValidateArtifactConsistency(
            MissionConfig config,
            MissionManifest manifest,
            MissionLayout layout,
            MissionEntities entities,
            List<MissionSceneMaterializationFinding> findings)
        {
            var expectedMissionId = config != null ? config.MissionId : "";
            if (string.IsNullOrWhiteSpace(expectedMissionId))
            {
                expectedMissionId = manifest != null ? manifest.missionId : "";
            }

            ValidateMissionId(expectedMissionId, manifest != null ? manifest.missionId : "", "manifest", config != null ? config.GenerationManifestPath : "", findings);
            ValidateMissionId(expectedMissionId, layout != null ? layout.missionId : "", "layout", config != null ? config.LayoutPath : "", findings);
            ValidateMissionId(expectedMissionId, entities != null ? entities.missionId : "", "entities", config != null ? config.EntitiesPath : "", findings);

            var expectedLayoutRevisionId = manifest != null ? manifest.layoutRevisionId : "";
            if (string.IsNullOrWhiteSpace(expectedLayoutRevisionId))
            {
                expectedLayoutRevisionId = layout != null ? layout.layoutRevisionId : "";
            }

            ValidateLayoutRevision(expectedLayoutRevisionId, layout != null ? layout.layoutRevisionId : "", "layout", config != null ? config.LayoutPath : "", findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, entities != null ? entities.layoutRevisionId : "", "entities", config != null ? config.EntitiesPath : "", findings);
            ValidateLayoutGraphRevision(expectedLayoutRevisionId, layout, config != null ? config.LayoutPath : "", findings);
            ValidateEntityRevision(expectedLayoutRevisionId, entities, config != null ? config.EntitiesPath : "", findings);
        }

        private static void ValidateMissionId(string expectedMissionId, string artifactMissionId, string artifactName, string artifactPath, List<MissionSceneMaterializationFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(expectedMissionId) || string.IsNullOrWhiteSpace(artifactMissionId))
            {
                findings.Add(Finding("SCENE_MISSION_ID_MISSING", "Mission id is missing for " + artifactName + ".", artifactPath));
                return;
            }

            if (!string.Equals(expectedMissionId, artifactMissionId, StringComparison.Ordinal))
            {
                findings.Add(Finding("SCENE_MISSION_ID_MISMATCH", "Mission id mismatch for " + artifactName + ".", artifactPath));
            }
        }

        private static void ValidateLayoutRevision(string expectedLayoutRevisionId, string artifactLayoutRevisionId, string artifactName, string artifactPath, List<MissionSceneMaterializationFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(expectedLayoutRevisionId) || string.IsNullOrWhiteSpace(artifactLayoutRevisionId))
            {
                findings.Add(Finding("SCENE_LAYOUT_REVISION_MISSING", "Layout revision id is missing for " + artifactName + ".", artifactPath));
                return;
            }

            if (!string.Equals(expectedLayoutRevisionId, artifactLayoutRevisionId, StringComparison.Ordinal))
            {
                findings.Add(Finding("SCENE_LAYOUT_REVISION_MISMATCH", "Layout revision id mismatch for " + artifactName + ".", artifactPath));
            }
        }

        private static void ValidateLayoutGraphRevision(string expectedLayoutRevisionId, MissionLayout layout, string artifactPath, List<MissionSceneMaterializationFinding> findings)
        {
            if (layout == null)
            {
                return;
            }

            ValidateLayoutRevision(expectedLayoutRevisionId, layout.LayoutGraph != null ? layout.LayoutGraph.layoutRevisionId : "", "layout graph", artifactPath, findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, layout.RoomGraph != null ? layout.RoomGraph.layoutRevisionId : "", "room graph", artifactPath, findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, layout.PortalGraph != null ? layout.PortalGraph.layoutRevisionId : "", "portal graph", artifactPath, findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, layout.CoverGraph != null ? layout.CoverGraph.layoutRevisionId : "", "cover graph", artifactPath, findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, layout.VisibilityGraph != null ? layout.VisibilityGraph.layoutRevisionId : "", "visibility graph", artifactPath, findings);
            ValidateLayoutRevision(expectedLayoutRevisionId, layout.HearingGraph != null ? layout.HearingGraph.layoutRevisionId : "", "hearing graph", artifactPath, findings);
        }

        private static void ValidateEntityRevision(string expectedLayoutRevisionId, MissionEntities entities, string artifactPath, List<MissionSceneMaterializationFinding> findings)
        {
            if (entities == null)
            {
                return;
            }

            if (entities.actors != null)
            {
                foreach (var actor in entities.actors)
                {
                    if (actor != null)
                    {
                        ValidateLayoutRevision(expectedLayoutRevisionId, actor.layoutRevisionId, "actor " + actor.entityId, artifactPath, findings);
                    }
                }
            }

            if (entities.objectives != null)
            {
                foreach (var objective in entities.objectives)
                {
                    if (objective != null)
                    {
                        ValidateLayoutRevision(expectedLayoutRevisionId, objective.layoutRevisionId, "objective " + objective.entityId, artifactPath, findings);
                    }
                }
            }
        }

        private static void ValidateLayoutGeometry(MissionLayout layout, List<MissionSceneMaterializationFinding> findings, string artifactPath)
        {
            if (layout == null)
            {
                findings.Add(Finding("SCENE_LAYOUT_MISSING", "Layout artifact is missing or unreadable.", artifactPath));
                return;
            }

            if (layout.PortalGraph.portals != null)
            {
                foreach (var portal in layout.PortalGraph.portals)
                {
                    ValidatePortal(portal, findings, artifactPath);
                }
            }

            if (layout.LayoutGraph.breachPoints != null)
            {
                foreach (var breachPoint in layout.LayoutGraph.breachPoints)
                {
                    ValidateBreachPoint(breachPoint, findings, artifactPath);
                }
            }
        }

        private static void ValidatePortal(MissionPortal portal, List<MissionSceneMaterializationFinding> findings, string artifactPath)
        {
            if (portal == null)
            {
                return;
            }

            if (!IsFinite(portal.x) || !IsFinite(portal.y))
            {
                findings.Add(Finding("SCENE_PORTAL_POSITION_INVALID", "Portal position is invalid.", artifactPath));
            }

            if (!IsFinite(portal.width) || portal.width <= 0f)
            {
                findings.Add(Finding("SCENE_PORTAL_WIDTH_INVALID", "Portal width is invalid.", artifactPath));
            }

            if (!TryGetPortalAxis(portal.orientation, out _))
            {
                findings.Add(Finding("SCENE_PORTAL_ORIENTATION_INVALID", "Portal orientation is invalid.", artifactPath));
            }
        }

        private static void ValidateBreachPoint(MissionBreachPoint breachPoint, List<MissionSceneMaterializationFinding> findings, string artifactPath)
        {
            if (breachPoint == null)
            {
                return;
            }

            if (!IsFinite(breachPoint.x) || !IsFinite(breachPoint.y))
            {
                findings.Add(Finding("SCENE_BREACH_POSITION_INVALID", "Breach point position is invalid.", artifactPath));
            }

            if (!IsFinite(breachPoint.width) || breachPoint.width <= 0f)
            {
                findings.Add(Finding("SCENE_BREACH_WIDTH_INVALID", "Breach point width is invalid.", artifactPath));
            }

            if (!TryGetBreachAxis(breachPoint.side, out _))
            {
                findings.Add(Finding("SCENE_BREACH_SIDE_INVALID", "Breach point side is invalid.", artifactPath));
            }
        }

        private static void ValidateCatalogAsset(MissionCatalogAsset catalog, string role, List<MissionSceneMaterializationFinding> findings, List<string> artifacts)
        {
            if (catalog == null)
            {
                findings.Add(Finding("SCENE_CATALOG_MISSING", "Mission catalog is missing: " + role, ""));
                return;
            }

            if (!string.Equals(catalog.SchemaVersion, "bse.catalog.v2.3", StringComparison.Ordinal))
            {
                findings.Add(Finding("SCENE_CATALOG_VERSION_INVALID", "Mission catalog has an unsupported schema version: " + role, ""));
            }
            if (string.IsNullOrWhiteSpace(catalog.CatalogId))
            {
                findings.Add(Finding("SCENE_CATALOG_ID_MISSING", "Mission catalog is missing catalogId: " + role, ""));
            }
            if (string.IsNullOrWhiteSpace(catalog.CatalogType))
            {
                findings.Add(Finding("SCENE_CATALOG_TYPE_MISSING", "Mission catalog is missing catalogType: " + role, ""));
            }
        }

        private static int CountCatalogRefs(MissionConfig config)
        {
            var count = 0;
            if (config == null)
            {
                return 0;
            }

            if (config.EnemyCatalog != null)
            {
                count++;
            }
            if (config.EnvironmentCatalog != null)
            {
                count++;
            }
            if (config.ObjectiveCatalog != null)
            {
                count++;
            }

            return count;
        }

        private static Vector3 ResolveRoomAnchor(IReadOnlyDictionary<string, MissionRoom> rooms, string roomId, string entityId, int slotIndex)
        {
            if (rooms.TryGetValue(roomId, out var room))
            {
                var hash = StableHash(entityId);
                var offsetX = (((hash >> 0) & 3) - 1.5f) * 0.24f + slotIndex * 0.03f;
                var offsetY = (((hash >> 2) & 3) - 1.5f) * 0.24f;
                return RoomCenter(room) + new Vector3(offsetX, offsetY, 0f);
            }

            return new Vector3(slotIndex * 0.5f, 0f, 0f);
        }

        private static Transform ResolveActorParent(MissionSceneContext context, string actorType)
        {
            if (string.Equals(actorType, "Operative", StringComparison.OrdinalIgnoreCase))
            {
                return context.OperativesRoot;
            }

            if (string.Equals(actorType, "Hostage", StringComparison.OrdinalIgnoreCase))
            {
                return context.HostagesRoot;
            }

            return context.EnemiesRoot;
        }

        private static Color ResolveActorColor(string actorType)
        {
            if (string.Equals(actorType, "Operative", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.24f, 0.62f, 0.9f, 1f);
            }

            if (string.Equals(actorType, "Hostage", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.92f, 0.82f, 0.34f, 1f);
            }

            return new Color(0.84f, 0.25f, 0.22f, 1f);
        }

        private static Dictionary<string, MissionRoom> BuildRoomLookup(MissionRoom[] rooms)
        {
            var result = new Dictionary<string, MissionRoom>(StringComparer.Ordinal);
            if (rooms == null)
            {
                return result;
            }

            foreach (var room in rooms)
            {
                if (room == null || string.IsNullOrEmpty(room.id))
                {
                    continue;
                }

                result[room.id] = room;
            }

            return result;
        }

        private static Vector3 RoomCenter(MissionRoom room)
        {
            var x = room.rect != null ? room.rect.x : 0f;
            var y = room.rect != null ? room.rect.y : 0f;
            var width = room.rect != null ? Mathf.Max(1f, room.rect.width) : 1f;
            var height = room.rect != null ? Mathf.Max(1f, room.rect.height) : 1f;
            return new Vector3(x + width / 2f, y + height / 2f, 0f);
        }

        private static bool IsHorizontal(string orientation)
        {
            return string.Equals(orientation, "horizontal", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetPortalAxis(string orientation, out string axis)
        {
            if (IsHorizontal(orientation))
            {
                axis = "horizontal";
                return true;
            }

            if (string.Equals(orientation, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                axis = "vertical";
                return true;
            }

            axis = "";
            return false;
        }

        private static bool TryGetBreachAxis(string side, out string axis)
        {
            if (string.Equals(side, "north", StringComparison.OrdinalIgnoreCase) || string.Equals(side, "south", StringComparison.OrdinalIgnoreCase))
            {
                axis = "horizontal";
                return true;
            }

            if (string.Equals(side, "east", StringComparison.OrdinalIgnoreCase) || string.Equals(side, "west", StringComparison.OrdinalIgnoreCase))
            {
                axis = "vertical";
                return true;
            }

            axis = "";
            return false;
        }

        private static bool IsWindowPortal(MissionPortal portal)
        {
            return portal != null && portal.kind != null && portal.kind.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (string.IsNullOrEmpty(value))
                {
                    return unchecked((int)hash);
                }

                foreach (var ch in value)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                return unchecked((int)hash);
            }
        }

        private static void AddArtifact(List<string> artifacts, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            if (!artifacts.Contains(relativePath))
            {
                artifacts.Add(relativePath);
            }
        }

        private static MissionSceneMaterializationFinding Finding(string code, string message, string artifactPath)
        {
            return new MissionSceneMaterializationFinding
            {
                code = code,
                message = message,
                artifactPath = artifactPath
            };
        }

        private static MissionSceneMaterializationReport Complete(
            MissionSceneMaterializationReport report,
            List<MissionSceneMaterializationFinding> findings,
            List<string> artifacts,
            string message)
        {
            report.status = findings.Count == 0 ? "PASS" : "FAIL";
            report.message = message;
            report.findings = findings.ToArray();
            report.artifacts = artifacts.ToArray();
            return report;
        }

        private static void SetTile(Tilemap tilemap, int x, int y, TileBase tile)
        {
            if (tilemap == null)
            {
                return;
            }

            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        private static void ClearCollisionSegment(Tilemap tilemap, float x, float y, float span, string axis)
        {
            if (tilemap == null)
            {
                return;
            }

            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(span) || span <= 0f)
            {
                return;
            }

            var centerX = Mathf.RoundToInt(x);
            var centerY = Mathf.RoundToInt(y);
            var tileSpan = Mathf.Max(1, Mathf.RoundToInt(span));
            var halfSpan = Mathf.FloorToInt(tileSpan / 2f);

            if (string.Equals(axis, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                var startY = centerY - halfSpan;
                for (var offset = 0; offset < tileSpan; offset++)
                {
                    SetTile(tilemap, centerX, startY + offset, null);
                }

                return;
            }

            var startX = centerX - halfSpan;
            for (var offset = 0; offset < tileSpan; offset++)
            {
                SetTile(tilemap, startX + offset, centerY, null);
            }
        }

        private static void ClearTilemap(Tilemap tilemap)
        {
            if (tilemap != null)
            {
                tilemap.ClearAllTiles();
            }
        }

        private static int ClearTransformChildren(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            var cleared = 0;
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                Destroy(child.gameObject);
                cleared++;
            }

            return cleared;
        }

        private static void Destroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Transform EnsureContainer(Transform parent, string name, string missionId, string layoutRevisionId)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                EnsureMarker(existing.gameObject, missionId, name, name, layoutRevisionId);
                return existing;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            EnsureMarker(go, missionId, name, name, layoutRevisionId);
            return go.transform;
        }

        private static T EnsureChildComponent<T>(Transform parent, string name, T existing) where T : Component
        {
            if (existing != null)
            {
                return existing;
            }

            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            existing = child.GetComponent<T>();
            if (existing == null)
            {
                existing = child.gameObject.AddComponent<T>();
            }

            return existing;
        }

        private static Tilemap EnsureTilemap(Transform parent, string name, Tilemap existing, int sortingOrder, bool useCollider, bool useComposite)
        {
            if (existing == null)
            {
                var child = parent.Find(name);
                if (child == null)
                {
                    child = new GameObject(name).transform;
                    child.SetParent(parent, false);
                }

                existing = child.GetComponent<Tilemap>();
                if (existing == null)
                {
                    existing = child.gameObject.AddComponent<Tilemap>();
                }
            }

            var renderer = existing.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = existing.gameObject.AddComponent<TilemapRenderer>();
            }

            renderer.sortingOrder = sortingOrder;

            if (useCollider)
            {
                var collider = existing.GetComponent<TilemapCollider2D>();
                if (collider == null)
                {
                    collider = existing.gameObject.AddComponent<TilemapCollider2D>();
                }

                collider.compositeOperation = useComposite
                    ? Collider2D.CompositeOperation.Merge
                    : Collider2D.CompositeOperation.None;

                var composite = existing.GetComponent<CompositeCollider2D>();
                if (useComposite && composite == null)
                {
                    composite = existing.gameObject.AddComponent<CompositeCollider2D>();
                }

                var body = existing.GetComponent<Rigidbody2D>();
                if (body == null)
                {
                    body = existing.gameObject.AddComponent<Rigidbody2D>();
                }

                body.bodyType = RigidbodyType2D.Static;
                body.simulated = true;
            }

            return existing;
        }

        private static TileBase TileFor(string key, Color color)
        {
            if (TileCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SharedSprite();
            tile.color = color;
            tile.name = key + "_tile";
            tile.hideFlags = HideFlags.HideAndDontSave;
            TileCache[key] = tile;
            return tile;
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
            texture.hideFlags = HideFlags.HideAndDontSave;
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "BSE_Materializer_Sprite";
            sharedSprite.hideFlags = HideFlags.HideAndDontSave;
            return sharedSprite;
        }

        private static GameObject CreateMarker(Transform parent, string missionId, string name, Vector3 position, Vector3 size, Color color, string label, string entityId, string sourceId, string layoutRevisionId, bool trigger, MissionOwnership ownership = null)
        {
            if (parent == null)
            {
                return null;
            }

            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = size;

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = SharedSprite();
            renderer.color = color;
            renderer.sortingOrder = 3;

            var collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;

            if (ownership != null && !string.IsNullOrEmpty(ownership.generatedBy))
            {
                marker.AddComponent<GeneratedOwnershipMarker>().Initialize(
                    ownership.owner,
                    ownership.missionId,
                    ownership.entityId,
                    ownership.sourceId,
                    ownership.layoutRevisionId,
                    ownership.stableKey);
            }
            else
            {
                AddMarker(marker, missionId, entityId, sourceId, layoutRevisionId, ownership != null ? ownership.stableKey : StableKey(entityId, layoutRevisionId));
            }

            marker.name = string.IsNullOrEmpty(label) ? name : name + "_" + label;
            return marker;
        }

        private static void AddMarker(GameObject target, string missionId, string entityId, string sourceId, string layoutRevisionId, string stableKey)
        {
            if (target == null)
            {
                return;
            }

            target.AddComponent<GeneratedOwnershipMarker>().Initialize(GeneratedOwner, missionId, entityId, sourceId, layoutRevisionId, stableKey);
        }

        private static void EnsureMarker(GameObject target, string missionId, string entityId, string sourceId, string layoutRevisionId)
        {
            if (target == null)
            {
                return;
            }

            var marker = target.GetComponent<GeneratedOwnershipMarker>();
            if (marker == null)
            {
                marker = target.AddComponent<GeneratedOwnershipMarker>();
            }

            marker.Initialize(GeneratedOwner, missionId, entityId, sourceId, layoutRevisionId, StableKey(entityId, layoutRevisionId));
        }

        private static string StableKey(string entityId, string layoutRevisionId)
        {
            return string.IsNullOrEmpty(layoutRevisionId) ? entityId : layoutRevisionId + ":" + entityId;
        }

    }
}
