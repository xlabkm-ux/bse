using System;
using System.Reflection;
using BreachScenarioEngine.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BreachScenarioEngine.Editor.Tests
{
    public sealed class MissionSceneMaterializerTests
    {
        [Test]
        public void MaterializeCreatesCanonicalContextHierarchy()
        {
            var bundle = CreatePreviewBundle();
            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("PASS", report.status, report.message);
                Assert.AreEqual("debug_fallback", report.metrics.previewContentMode);
                Assert.Greater(report.metrics.debugFallbackCount, 0);
                Assert.NotNull(bundle.Context.Grid, "Grid");
                Assert.NotNull(bundle.Context.BaseMap, "BaseMap");
                Assert.NotNull(bundle.Context.CollisionMap, "CollisionMap");
                Assert.NotNull(bundle.Context.DecorMap, "DecorMap");
                Assert.NotNull(bundle.Context.InteractablesMap, "InteractablesMap");
                Assert.NotNull(bundle.Context.DoorsRoot, "DoorsRoot");
                Assert.NotNull(bundle.Context.WindowsRoot, "WindowsRoot");
                Assert.NotNull(bundle.Context.CoversRoot, "CoversRoot");
                Assert.NotNull(bundle.Context.EnemiesRoot, "EnemiesRoot");
                Assert.NotNull(bundle.Context.OperativesRoot, "OperativesRoot");
                Assert.NotNull(bundle.Context.ObjectivesRoot, "ObjectivesRoot");
                Assert.NotNull(bundle.Context.HostagesRoot, "HostagesRoot");
                Assert.NotNull(bundle.Context.ExtractionRoot, "ExtractionRoot");
                Assert.NotNull(bundle.Context.DebugRoot, "DebugRoot");

                Assert.AreSame(bundle.Root.transform, bundle.Context.Grid.transform.parent);
                Assert.AreSame(bundle.Context.Grid.transform, bundle.Context.BaseMap.transform.parent);
                Assert.AreSame(bundle.Context.Grid.transform, bundle.Context.CollisionMap.transform.parent);
                Assert.AreSame(bundle.Context.Grid.transform, bundle.Context.DecorMap.transform.parent);
                Assert.AreSame(bundle.Context.Grid.transform, bundle.Context.InteractablesMap.transform.parent);

                var generated = bundle.Root.transform.Find("Generated");
                Assert.NotNull(generated, "Generated");
                Assert.AreSame(generated, bundle.Context.DoorsRoot.parent);
                Assert.AreSame(generated, bundle.Context.WindowsRoot.parent);
                Assert.AreSame(generated, bundle.Context.CoversRoot.parent);
                Assert.AreSame(generated, bundle.Context.EnemiesRoot.parent);
                Assert.AreSame(generated, bundle.Context.OperativesRoot.parent);
                Assert.AreSame(generated, bundle.Context.ObjectivesRoot.parent);
                Assert.AreSame(generated, bundle.Context.HostagesRoot.parent);
                Assert.AreSame(generated, bundle.Context.ExtractionRoot.parent);
                Assert.AreSame(generated, bundle.Context.DebugRoot.parent);
                Assert.NotNull(bundle.Context.CollisionMap.GetComponent<TilemapCollider2D>(), "Collision TilemapCollider2D");
                Assert.NotNull(bundle.Context.CollisionMap.GetComponent<CompositeCollider2D>(), "Collision CompositeCollider2D");
                Assert.NotNull(bundle.Context.CollisionMap.GetComponent<Rigidbody2D>(), "Collision Rigidbody2D");
                Assert.Greater(bundle.Root.GetComponentsInChildren<GeneratedOwnershipMarker>().Length, 0);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeClearsDoorAndBreachCollisionButLeavesWindowsVisualOnly()
        {
            var bundle = CreatePreviewBundle();
            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("PASS", report.status, report.message);
                Assert.AreEqual(2, report.metrics.doorCount);
                Assert.AreEqual(1, report.metrics.windowCount);

                var collision = bundle.Context.CollisionMap;
                Assert.NotNull(collision);
                Assert.False(collision.HasTile(new Vector3Int(0, 1, 0)), "Door collision tile was not cleared.");
                Assert.False(collision.HasTile(new Vector3Int(0, 2, 0)), "Door collision tile was not cleared.");
                Assert.False(collision.HasTile(new Vector3Int(2, 0, 0)), "Breach collision tile was not cleared.");
                Assert.True(collision.HasTile(new Vector3Int(2, 3, 0)), "Window should stay visual-only.");
                Assert.True(collision.HasTile(new Vector3Int(3, 3, 0)), "Window should stay visual-only.");
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnInvalidPortalPosition()
        {
            var bundle = CreatePreviewBundle();
            bundle.Layout.PortalGraph.portals = new[]
            {
                new MissionPortal
                {
                    id = "portal_invalid",
                    layoutRevisionId = bundle.Layout.layoutRevisionId,
                    fromRoomId = "room_a",
                    toRoomId = "room_a",
                    kind = "door",
                    orientation = "vertical",
                    x = float.NaN,
                    y = 2f,
                    width = 1f
                }
            };

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_PORTAL_POSITION_INVALID", report.findings[0].code);
                Assert.AreEqual("Portal position is invalid.", report.findings[0].message);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnInvalidPortalOrientation()
        {
            var bundle = CreatePreviewBundle();
            bundle.Layout.PortalGraph.portals = new[]
            {
                new MissionPortal
                {
                    id = "portal_invalid",
                    layoutRevisionId = bundle.Layout.layoutRevisionId,
                    fromRoomId = "room_a",
                    toRoomId = "room_a",
                    kind = "door",
                    orientation = "diagonal",
                    x = 0f,
                    y = 2f,
                    width = 1f
                }
            };

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_PORTAL_ORIENTATION_INVALID", report.findings[0].code);
                Assert.AreEqual("Portal orientation is invalid.", report.findings[0].message);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnNonPassManifest()
        {
            var bundle = CreatePreviewBundle();
            bundle.Manifest.status = "FAIL";

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_MANIFEST_NOT_PASS", report.findings[0].code);
                Assert.AreEqual("Scene preview requires a PASS manifest.", report.findings[0].message);
                Assert.IsNull(bundle.Root.transform.Find("Generated"));
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnMissingCatalog()
        {
            var bundle = CreatePreviewBundle();
            SetPrivateField(bundle.Config, "environmentCatalog", null);

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_CATALOG_MISSING", report.findings[0].code);
                Assert.AreEqual("Mission catalog is missing: environmentCatalog", report.findings[0].message);
                Assert.IsNull(bundle.Root.transform.Find("Generated"));
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnStaleEntitiesLayoutRevision()
        {
            var bundle = CreatePreviewBundle();
            bundle.Entities.layoutRevisionId = "layout_stale";

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_LAYOUT_REVISION_MISMATCH", report.findings[0].code);
                Assert.AreEqual("Layout revision id mismatch for entities.", report.findings[0].message);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnStaleLayoutGraphRevision()
        {
            var bundle = CreatePreviewBundle();
            bundle.Layout.PortalGraph.layoutRevisionId = "layout_stale";

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_LAYOUT_REVISION_MISMATCH", report.findings[0].code);
                Assert.AreEqual("Layout revision id mismatch for portal graph.", report.findings[0].message);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        [Test]
        public void MaterializeFailsOnMissionIdMismatch()
        {
            var bundle = CreatePreviewBundle();
            bundle.Layout.missionId = "TEST_OtherMission";

            try
            {
                var report = MissionSceneMaterializer.Materialize(bundle.Context, bundle.Config, bundle.Manifest, bundle.Layout, bundle.Entities);

                Assert.AreEqual("FAIL", report.status);
                Assert.AreEqual("SCENE_MISSION_ID_MISMATCH", report.findings[0].code);
                Assert.AreEqual("Mission id mismatch for layout.", report.findings[0].message);
            }
            finally
            {
                DestroyBundle(bundle);
            }
        }

        private static PreviewBundle CreatePreviewBundle()
        {
            var missionId = "TEST_Materializer";
            var layoutRevisionId = "layout_test";

            var root = new GameObject("MissionSceneMaterializerTestRoot");
            var context = root.AddComponent<MissionSceneContext>();

            var config = ScriptableObject.CreateInstance<MissionConfig>();
            SetPrivateField(config, "missionId", missionId);
            SetPrivateField(config, "payloadPath", "UserMissionSources/missions/" + missionId + "/mission_payload.generated.json");
            SetPrivateField(config, "layoutPath", "UserMissionSources/missions/" + missionId + "/mission_layout.generated.json");
            SetPrivateField(config, "entitiesPath", "UserMissionSources/missions/" + missionId + "/mission_entities.generated.json");
            SetPrivateField(config, "verificationSummaryPath", "UserMissionSources/missions/" + missionId + "/verification_summary.json");
            SetPrivateField(config, "generationManifestPath", "UserMissionSources/missions/" + missionId + "/generation_manifest.json");
            var enemyCatalog = CreateCatalog("enemy_catalog", "enemy");
            var environmentCatalog = CreateCatalog("environment_catalog", "environment");
            var objectiveCatalog = CreateCatalog("objective_catalog", "objective");
            SetPrivateField(config, "enemyCatalog", enemyCatalog);
            SetPrivateField(config, "environmentCatalog", environmentCatalog);
            SetPrivateField(config, "objectiveCatalog", objectiveCatalog);

            var manifest = new MissionManifest
            {
                schemaVersion = "bse.generation_manifest.v2.3",
                pipelineVersion = "2.3",
                missionId = missionId,
                status = "PASS",
                requestedSeed = 12345,
                effectiveSeed = 12345,
                layoutRevisionId = layoutRevisionId
            };

            var layout = new MissionLayout
            {
                schemaVersion = "bse.mission_layout.v2.3",
                pipelineVersion = "2.3",
                missionId = missionId,
                layoutRevisionId = layoutRevisionId,
                generator = new MissionLayoutGenerator
                {
                    id = "test_layout_generator",
                    sourcePayloadPath = config.PayloadPath,
                    step = 6,
                    pureData = true
                },
                retryPolicy = new MissionRetryPolicy
                {
                    retryFromStep = 6,
                    retryAction = "generate_layout",
                    placementStep = 5
                },
                LayoutGraph = new LayoutGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    bounds = new[] { 4, 4 },
                    theme = "test",
                    ppu = 128,
                    entryRoomId = "room_a",
                    objectiveRoomIds = Array.Empty<string>(),
                    breachPoints = new[]
                    {
                        new MissionBreachPoint
                        {
                            id = "breach_a",
                            layoutRevisionId = layoutRevisionId,
                            roomId = "room_a",
                            navNodeId = "nav_room_a",
                            kind = "entry_door",
                            side = "south",
                            x = 2f,
                            y = 0f,
                            width = 1f
                        }
                    }
                },
                RoomGraph = new RoomGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    rooms = new[]
                    {
                        new MissionRoom
                        {
                            id = "room_a",
                            layoutRevisionId = layoutRevisionId,
                            tag = "entry",
                            rect = new MissionRect
                            {
                                x = 0f,
                                y = 0f,
                                width = 4f,
                                height = 4f
                            },
                            navNodeId = "nav_room_a"
                        }
                    }
                },
                PortalGraph = new PortalGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    portals = new[]
                    {
                        new MissionPortal
                        {
                            id = "portal_door",
                            layoutRevisionId = layoutRevisionId,
                            fromRoomId = "room_a",
                            toRoomId = "room_a",
                            kind = "door",
                            orientation = "vertical",
                            x = 0f,
                            y = 2f,
                            width = 2f
                        },
                        new MissionPortal
                        {
                            id = "portal_window",
                            layoutRevisionId = layoutRevisionId,
                            fromRoomId = "room_a",
                            toRoomId = "room_a",
                            kind = "window",
                            orientation = "horizontal",
                            x = 2f,
                            y = 3f,
                            width = 2f
                        }
                    }
                },
                CoverGraph = new CoverGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    coverPoints = Array.Empty<MissionCoverPoint>()
                },
                VisibilityGraph = new VisibilityGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    edges = Array.Empty<MissionVisibilityEdge>()
                },
                HearingGraph = new HearingGraphData
                {
                    layoutRevisionId = layoutRevisionId,
                    edges = Array.Empty<MissionHearingEdge>()
                }
            };

            var entities = new MissionEntities
            {
                schemaVersion = "bse.mission_entities.v2.3",
                pipelineVersion = "2.3",
                missionId = missionId,
                layoutRevisionId = layoutRevisionId,
                placementStep = 5,
                requiresLayoutStep = 6,
                actors = new[]
                {
                    new MissionActorEntity
                    {
                        entityId = "enemy_1",
                        kind = "actor",
                        sourceActorId = "enemy",
                        type = "Enemy",
                        roomId = "room_a",
                        navNodeId = "nav_room_a",
                        layoutRevisionId = layoutRevisionId,
                        ownership = new MissionOwnership
                        {
                            owner = "bse-pipeline",
                            generatedBy = "test",
                            missionId = missionId,
                            entityId = "enemy_1",
                            sourceId = "enemy",
                            layoutRevisionId = layoutRevisionId,
                            stableKey = layoutRevisionId + ":enemy_1"
                        }
                    }
                },
                objectives = new[]
                {
                    new MissionObjectiveEntity
                    {
                        entityId = "objective_1",
                        kind = "objective",
                        type = "Secure",
                        roomId = "room_a",
                        navNodeId = "nav_room_a",
                        layoutRevisionId = layoutRevisionId,
                        ownership = new MissionOwnership
                        {
                            owner = "bse-pipeline",
                            generatedBy = "test",
                            missionId = missionId,
                            entityId = "objective_1",
                            sourceId = "objective_1",
                            layoutRevisionId = layoutRevisionId,
                            stableKey = layoutRevisionId + ":objective_1"
                        }
                    }
                }
            };

            return new PreviewBundle(root, context, config, manifest, layout, entities, enemyCatalog, environmentCatalog, objectiveCatalog);
        }

        private static MissionCatalogAsset CreateCatalog(string catalogId, string catalogType)
        {
            var catalog = ScriptableObject.CreateInstance<MissionCatalogAsset>();
            SetPrivateField(catalog, "catalogId", catalogId);
            SetPrivateField(catalog, "catalogType", catalogType);
            SetPrivateField(catalog, "schemaVersion", "bse.catalog.v2.3");
            return catalog;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing field: " + target.GetType().Name + "." + fieldName);
            field!.SetValue(target, value);
        }

        private static void DestroyBundle(PreviewBundle bundle)
        {
            if (bundle.Config != null)
            {
                UnityEngine.Object.DestroyImmediate(bundle.Config);
            }

            if (bundle.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(bundle.Root);
            }

            if (bundle.EnemyCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(bundle.EnemyCatalog);
            }

            if (bundle.EnvironmentCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(bundle.EnvironmentCatalog);
            }

            if (bundle.ObjectiveCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(bundle.ObjectiveCatalog);
            }
        }

        private sealed class PreviewBundle
        {
            public PreviewBundle(GameObject root, MissionSceneContext context, MissionConfig config, MissionManifest manifest, MissionLayout layout, MissionEntities entities, MissionCatalogAsset enemyCatalog, MissionCatalogAsset environmentCatalog, MissionCatalogAsset objectiveCatalog)
            {
                Root = root;
                Context = context;
                Config = config;
                Manifest = manifest;
                Layout = layout;
                Entities = entities;
                EnemyCatalog = enemyCatalog;
                EnvironmentCatalog = environmentCatalog;
                ObjectiveCatalog = objectiveCatalog;
            }

            public GameObject Root { get; }
            public MissionSceneContext Context { get; }
            public MissionConfig Config { get; }
            public MissionManifest Manifest { get; }
            public MissionLayout Layout { get; }
            public MissionEntities Entities { get; }
            public MissionCatalogAsset EnemyCatalog { get; }
            public MissionCatalogAsset EnvironmentCatalog { get; }
            public MissionCatalogAsset ObjectiveCatalog { get; }
        }
    }
}
