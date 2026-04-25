using System.IO;
using BreachScenarioEngine.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BreachScenarioEngine.Editor.Tests
{
    public sealed class PilotMissionSmokeTests
    {
        private static readonly string[] ConfigPaths =
        {
            "Assets/Data/Mission/MissionConfig/MissionConfig_VS01.asset",
            "Assets/Data/Mission/MissionConfig/MissionConfig_VS02.asset",
            "Assets/Data/Mission/MissionConfig/MissionConfig_VS03.asset"
        };

        [Test]
        public void PilotMissionConfigsExist()
        {
            foreach (var path in ConfigPaths)
            {
                Assert.NotNull(AssetDatabase.LoadAssetAtPath<MissionConfig>(path), path);
            }
        }

        [Test]
        public void PilotGeneratedArtifactsExistAfterPipeline()
        {
            foreach (var path in ConfigPaths)
            {
                var config = AssetDatabase.LoadAssetAtPath<MissionConfig>(path);
                Assert.NotNull(config, path);
                AssertArtifact(config!.PayloadPath);
                AssertArtifact(config.LayoutPath);
                AssertArtifact(config.EntitiesPath);
                AssertArtifact(config.VerificationSummaryPath);
                AssertArtifact(config.GenerationManifestPath);
            }
        }

        [Test]
        public void RuntimeLoaderCreatesGeneratedMissionRoot()
        {
            foreach (var path in ConfigPaths)
            {
                var config = AssetDatabase.LoadAssetAtPath<MissionConfig>(path);
                Assert.NotNull(config, path);

                var host = new GameObject("PilotMissionSmokeHost");
                try
                {
                    var loader = host.AddComponent<MissionRuntimeLoader>();
                    Assert.IsTrue(loader.LoadMission(config!), loader.LastError);
                    Assert.NotNull(loader.CurrentRoot);
                    Assert.AreEqual("GeneratedMissionRoot_" + config!.MissionId, loader.CurrentRoot!.name);
                    var context = loader.CurrentRoot.GetComponent<MissionSceneContext>();
                    Assert.NotNull(context, "MissionSceneContext");
                    Assert.NotNull(context!.Grid, "Grid");
                    Assert.NotNull(context.BaseMap, "BaseMap");
                    Assert.NotNull(context.CollisionMap, "CollisionMap");
                    Assert.NotNull(context.DecorMap, "DecorMap");
                    Assert.NotNull(context.InteractablesMap, "InteractablesMap");
                    Assert.NotNull(context.DoorsRoot, "DoorsRoot");
                    Assert.NotNull(context.WindowsRoot, "WindowsRoot");
                    Assert.NotNull(context.CoversRoot, "CoversRoot");
                    Assert.NotNull(context.EnemiesRoot, "EnemiesRoot");
                    Assert.NotNull(context.OperativesRoot, "OperativesRoot");
                    Assert.NotNull(context.ObjectivesRoot, "ObjectivesRoot");
                    Assert.NotNull(context.HostagesRoot, "HostagesRoot");
                    Assert.NotNull(context.ExtractionRoot, "ExtractionRoot");
                    Assert.NotNull(context.DebugRoot, "DebugRoot");
                    Assert.Greater(loader.CurrentRoot.GetComponentsInChildren<Tilemap>().Length, 0);
                    Assert.Greater(loader.CurrentRoot.GetComponentsInChildren<GeneratedOwnershipMarker>().Length, 0);
                    Assert.Greater(loader.CurrentRoot.GetComponentsInChildren<MissionCompleteTrigger>().Length, 0);
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        private static void AssertArtifact(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.IsTrue(File.Exists(fullPath), relativePath);
        }
    }
}
