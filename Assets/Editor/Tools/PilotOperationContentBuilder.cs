using System.IO;
using BreachScenarioEngine.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace BreachScenarioEngine.Editor.Tools
{
    public static class PilotOperationContentBuilder
    {
        private static readonly MissionSpec[] Missions =
        {
            new("VS01_HostageApartment", "Hostage Apartment", 428193, 5),
            new("VS02_DataRaidOffice", "Data Raid Office", 731204, 5),
            new("VS03_StealthSafehouse", "Stealth Safehouse", 912447, 7)
        };

        [MenuItem("Breach Scenario Engine/Pilot/Rebuild Pilot Content")]
        public static void RebuildPilotContent()
        {
            Directory.CreateDirectory("Assets/Data/Mission/MissionConfig");
            Directory.CreateDirectory("Assets/Prefabs/Pilot");
            Directory.CreateDirectory("Assets/Scenes");

            var configs = new MissionConfig[Missions.Length];
            for (var i = 0; i < Missions.Length; i++)
            {
                configs[i] = CreateOrUpdateMissionConfig(Missions[i]);
            }

            CreatePlaceholderPrefab("RoomFloor_Debug", new Color(0.18f, 0.22f, 0.24f, 0.72f), new Vector2(3.2f, 2.4f), false);
            CreatePlaceholderPrefab("Wall_Debug", new Color(0.78f, 0.82f, 0.84f, 1f), new Vector2(3.2f, 0.18f), false);
            CreatePlaceholderPrefab("Door_Debug", new Color(0.9f, 0.72f, 0.22f, 1f), new Vector2(0.7f, 0.7f), true);
            CreatePlaceholderPrefab("Cover_Debug", new Color(0.28f, 0.55f, 0.42f, 1f), new Vector2(0.45f, 0.45f), false);
            CreatePlaceholderPrefab("Operative_Debug", new Color(0.24f, 0.62f, 0.9f, 1f), new Vector2(0.5f, 0.5f), false);
            CreatePlaceholderPrefab("Enemy_Debug", new Color(0.84f, 0.25f, 0.22f, 1f), new Vector2(0.5f, 0.5f), false);
            CreatePlaceholderPrefab("Hostage_Debug", new Color(0.92f, 0.82f, 0.34f, 1f), new Vector2(0.5f, 0.5f), false);
            CreatePlaceholderPrefab("Objective_Debug", new Color(0.56f, 0.36f, 0.86f, 1f), new Vector2(0.62f, 0.62f), true);

            CreateBootstrapScene(configs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static MissionConfig CreateOrUpdateMissionConfig(MissionSpec spec)
        {
            var assetPath = $"Assets/Data/Mission/MissionConfig/MissionConfig_{spec.ShortCode}.asset";
            var config = AssetDatabase.LoadAssetAtPath<MissionConfig>(assetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MissionConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
            }

            var missionRoot = $"UserMissionSources/missions/{spec.MissionId}";
            var serialized = new SerializedObject(config);
            serialized.FindProperty("missionId").stringValue = spec.MissionId;
            serialized.FindProperty("missionTitle").stringValue = spec.Title;
            serialized.FindProperty("schemaVersion").stringValue = "tb.mission_template.v2.3";
            serialized.FindProperty("initialSeed").intValue = spec.InitialSeed;
            serialized.FindProperty("maxRetries").intValue = spec.MaxRetries;
            serialized.FindProperty("templatePath").stringValue = missionRoot + "/mission_design.template.yaml";
            serialized.FindProperty("payloadPath").stringValue = missionRoot + "/mission_payload.generated.json";
            serialized.FindProperty("layoutPath").stringValue = missionRoot + "/mission_layout.generated.json";
            serialized.FindProperty("entitiesPath").stringValue = missionRoot + "/mission_entities.generated.json";
            serialized.FindProperty("verificationSummaryPath").stringValue = missionRoot + "/verification_summary.json";
            serialized.FindProperty("generationManifestPath").stringValue = missionRoot + "/generation_manifest.json";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void CreatePlaceholderPrefab(string name, Color color, Vector2 scale, bool trigger)
        {
            var path = "Assets/Prefabs/Pilot/" + name + ".prefab";
            var root = new GameObject(name);
            root.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteForPrefab();
            renderer.color = color;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateBootstrapScene(MissionConfig[] configs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MissionBootstrap";

            var cameraObject = new GameObject("Camera2D");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            cameraObject.transform.position = new Vector3(6f, 5f, -10f);

            var bootstrap = new GameObject("MissionBootstrap");
            bootstrap.AddComponent<MissionSceneBuilder>();
            var loader = bootstrap.AddComponent<MissionRuntimeLoader>();
            var selector = bootstrap.AddComponent<PilotMissionSelector>();

            var loaderSerialized = new SerializedObject(loader);
            loaderSerialized.FindProperty("initialMission").objectReferenceValue = configs[0];
            loaderSerialized.FindProperty("loadOnStart").boolValue = true;
            loaderSerialized.ApplyModifiedPropertiesWithoutUndo();

            var selectorSerialized = new SerializedObject(selector);
            selectorSerialized.FindProperty("loader").objectReferenceValue = loader;
            var missionsProperty = selectorSerialized.FindProperty("missions");
            missionsProperty.arraySize = configs.Length;
            for (var i = 0; i < configs.Length; i++)
            {
                missionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
            }

            selectorSerialized.ApplyModifiedPropertiesWithoutUndo();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var scenePath = "Assets/Scenes/MissionBootstrap.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        }

        private static Sprite SpriteForPrefab()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private readonly struct MissionSpec
        {
            public MissionSpec(string missionId, string title, int initialSeed, int maxRetries)
            {
                MissionId = missionId;
                Title = title;
                InitialSeed = initialSeed;
                MaxRetries = maxRetries;
                ShortCode = missionId.Substring(0, 4);
            }

            public string MissionId { get; }
            public string Title { get; }
            public int InitialSeed { get; }
            public int MaxRetries { get; }
            public string ShortCode { get; }
        }
    }
}
