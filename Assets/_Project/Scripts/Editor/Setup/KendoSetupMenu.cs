using Kendo.EditorTools.Import;
using Kendo.EditorTools.Validation;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>Menu entry points. Run "Run Full Setup" once after the first import.</summary>
    public static class KendoSetupMenu
    {
        private const string Root = "Kendo/";

        [MenuItem(Root + "Setup/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            KendoProjectSettingsSetup.Apply();
            ReapplyImportRules(); // layers now exist, so environment layers get assigned
            KendoPrefabBuilder.BuildAll();
            KendoDataAssetBuilder.BuildAll();
            KendoSceneBuilder.BuildAll(overwrite: false);
            KendoAssetValidator.RunAndWriteReport();
        }

        [MenuItem(Root + "Setup/1. Configure Project Settings", priority = 20)]
        public static void ConfigureProjectSettings() => KendoProjectSettingsSetup.Apply();

        [MenuItem(Root + "Setup/2. Build Prefabs", priority = 21)]
        public static void BuildPrefabs() => KendoPrefabBuilder.BuildAll();

        [MenuItem(Root + "Setup/3. Build Data Assets (from GDD)", priority = 22)]
        public static void BuildData() => KendoDataAssetBuilder.BuildAll();

        [MenuItem(Root + "Setup/4. Build Scenes", priority = 23)]
        public static void BuildScenes() => KendoSceneBuilder.BuildAll(overwrite: false);

        [MenuItem(Root + "Setup/4b. Rebuild Scenes (overwrite)", priority = 24)]
        public static void RebuildScenes()
        {
            if (EditorUtility.DisplayDialog("Rebuild scenes", "Overwrite every scene in Assets/_Project/Scenes?", "Overwrite", "Cancel"))
                KendoSceneBuilder.BuildAll(overwrite: true);
        }

        [MenuItem(Root + "Assets/Reapply Import Rules", priority = 40)]
        public static void ReapplyImportRules()
        {
            KendoTextureIndex.RegisterCurrentHash();
            foreach (var rule in KendoImportRules.All)
            {
                if (AssetImporter.GetAtPath(rule.AssetPath) is ModelImporter importer)
                {
                    KendoModelPostprocessor.ApplyRule(importer, rule);
                    importer.SaveAndReimport();
                }
                else
                {
                    Debug.LogWarning($"[Kendo] Missing model {rule.AssetPath}");
                }
            }
        }

        [MenuItem(Root + "Validate/Asset Report", priority = 60)]
        public static void Validate() => KendoAssetValidator.RunAndWriteReport();
    }
}
