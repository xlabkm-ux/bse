using System;
using System.IO;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MissionRuntimeLoader : MonoBehaviour
    {
        [SerializeField] private MissionConfig initialMission;
        [SerializeField] private MissionSceneBuilder sceneBuilder;
        [SerializeField] private bool loadOnStart = true;

        private GameObject currentRoot;
        private string lastError = "";
        private string currentMissionId = "";

        public GameObject CurrentRoot => currentRoot;
        public string LastError => lastError;
        public string CurrentMissionId => currentMissionId;
        public bool HasLoadedMission => currentRoot != null;

        private void Start()
        {
            if (loadOnStart && initialMission != null)
            {
                LoadMission(initialMission);
            }
        }

        public bool LoadMission(MissionConfig config)
        {
            lastError = "";
            currentMissionId = config.MissionId;

            if (sceneBuilder == null)
            {
                sceneBuilder = GetComponent<MissionSceneBuilder>();
            }

            if (sceneBuilder == null)
            {
                sceneBuilder = gameObject.AddComponent<MissionSceneBuilder>();
            }

            try
            {
                var manifestPath = AbsoluteProjectPath(config.GenerationManifestPath);
                if (!File.Exists(manifestPath))
                {
                    return Fail("Manifest not found: " + config.GenerationManifestPath);
                }

                var manifest = JsonUtility.FromJson<MissionManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || !string.Equals(manifest.status, "PASS", StringComparison.Ordinal))
                {
                    return Fail("Manifest status is not PASS for " + config.MissionId);
                }

                var payloadPath = AbsoluteProjectPath(config.PayloadPath);
                var layoutPath = AbsoluteProjectPath(config.LayoutPath);
                var entitiesPath = AbsoluteProjectPath(config.EntitiesPath);
                if (!File.Exists(payloadPath) || !File.Exists(layoutPath) || !File.Exists(entitiesPath))
                {
                    return Fail("Payload, layout, or entities artifact is missing for " + config.MissionId);
                }

                _ = File.ReadAllText(payloadPath);
                var layout = JsonUtility.FromJson<MissionLayout>(File.ReadAllText(layoutPath));
                var entities = JsonUtility.FromJson<MissionEntities>(File.ReadAllText(entitiesPath));
                if (layout == null || entities == null)
                {
                    return Fail("Layout or entities artifact is unreadable for " + config.MissionId);
                }

                ClearCurrentRoot();
                currentRoot = sceneBuilder.Build(config, manifest, layout, entities);
                return true;
            }
            catch (Exception ex)
            {
                ClearCurrentRoot();
                return Fail(ex.Message);
            }
        }

        public void ClearCurrentRoot()
        {
            if (currentRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(currentRoot);
            }
            else
            {
                DestroyImmediate(currentRoot);
            }

            currentRoot = null;
        }

        private bool Fail(string message)
        {
            lastError = message;
            Debug.LogError(message);
            return false;
        }

        private static string AbsoluteProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

    }
}
