using System;
using Kendo.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kendo.EditorTools.Setup
{
    /// <summary>Project-wide settings that can be applied safely from code. XR plug-in setup stays manual (see Docs/Setup.md).</summary>
    public static class KendoProjectSettingsSetup
    {
        private const string UrpAssetPath = AssetFolders.Settings + "/Rendering/Kendo_URP.asset";
        private const string UrpRendererPath = AssetFolders.Settings + "/Rendering/Kendo_URP_Renderer.asset";

        public static void Apply()
        {
            PlayerSettings.companyName = "Kendo Team";
            PlayerSettings.productName = "Kendo: The Master's Legacy";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Standalone headsets (Meta Quest etc.) need IL2CPP + ARM64.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            SetActiveInputHandlerToInputSystem();
            EnsureLayers();
            EnsureUrpAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kendo] Project settings applied. Finish XR Plug-in Management by hand – see Docs/Setup.md.");
        }

        private static void SetActiveInputHandlerToInputSystem()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            using var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 1) return;
            prop.intValue = 1; // 0 = legacy, 1 = Input System package, 2 = both
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Kendo] Active Input Handling set to Input System. Restart the editor when prompted.");
        }

        private static void EnsureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            using var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return;

            foreach (var layerName in KendoLayers.All)
            {
                if (LayerMask.NameToLayer(layerName) >= 0) continue;
                var placed = false;
                for (var i = KendoLayers.FirstReservedIndex; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                    slot.stringValue = layerName;
                    placed = true;
                    break;
                }
                if (!placed) Debug.LogWarning($"[Kendo] No free layer slot for '{layerName}'.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureUrpAsset()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;

            try
            {
                AssetFolders.Ensure(AssetFolders.Settings + "/Rendering");
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
                if (pipeline == null)
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, UrpRendererPath);
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    pipeline.msaaSampleCount = 4; // MSAA 4x is the usual VR baseline
                    AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
                }

                GraphicsSettings.defaultRenderPipeline = pipeline;
                var current = QualitySettings.GetQualityLevel();
                for (var i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(current, false);
                Debug.Log($"[Kendo] URP asset assigned: {UrpAssetPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Kendo] Could not create the URP asset automatically " +
                                 $"({e.Message}). Create one via Assets > Create > Rendering > URP Asset (with Universal Renderer) " +
                                 "and assign it in Project Settings > Graphics and > Quality.");
            }
        }
    }
}
