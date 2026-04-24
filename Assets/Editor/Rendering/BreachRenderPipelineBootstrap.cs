using System.IO;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BreachScenarioEngine.Editor.Rendering
{
    public static class BreachRenderPipelineBootstrap
    {
        private const string SettingsFolder = "Assets/Settings/Rendering";
        private const string RendererPath = SettingsFolder + "/Breach_2D_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/Breach_URP_RenderGraph.asset";
        private static int _attempts;

        [InitializeOnLoadMethod]
        private static void ScheduleEnsureRenderPipeline()
        {
            EditorApplication.delayCall += TryEnsureRenderPipeline;
        }

        private static void TryEnsureRenderPipeline()
        {
            try
            {
                EnsureAssets(assignPipeline: true);
            }
            catch (Exception ex)
            {
                _attempts++;
                if (_attempts < 5)
                {
                    EditorApplication.delayCall += TryEnsureRenderPipeline;
                    return;
                }

                Debug.LogException(ex);
            }
        }

        [MenuItem("Breach/Rendering/Ensure URP 2D Render Graph Pipeline")]
        public static void EnsureFromMenu()
        {
            EnsureAssets(assignPipeline: true);
        }

        private static void EnsureAssets(bool assignPipeline)
        {
            Directory.CreateDirectory(SettingsFolder);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                renderer.name = "Breach_2D_Renderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Breach_URP_RenderGraph";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            if (assignPipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                EditorUtility.SetDirty(pipeline);
                EditorUtility.SetDirty(renderer);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
