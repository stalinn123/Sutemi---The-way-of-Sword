using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Import
{
    /// <summary>
    /// File-name index of textures under the Art folder. Every supplied FBX references its textures by
    /// absolute paths on the authors' machines (/Users/sannthosh/…, C:/Users/gargi/…) and none are embedded,
    /// so materials are relinked by file name. A custom dependency makes models reimport when textures change.
    /// </summary>
    public static class KendoTextureIndex
    {
        public const string DependencyName = "Kendo/TextureIndex";

        public static Dictionary<string, string> Build()
        {
            var index = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { KendoImportRules.ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                index.TryAdd(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), path);
            }
            return index;
        }

        public static void RegisterCurrentHash()
        {
            var keys = new List<string>(Build().Keys);
            keys.Sort(System.StringComparer.Ordinal);
            var hash = Hash128.Compute(string.Join("|", keys));
            AssetDatabase.RegisterCustomDependency(DependencyName, hash);
        }
    }

    /// <summary>Keeps the texture-index dependency current and sets sensible import defaults for textures.</summary>
    public sealed class KendoTexturePostprocessor : AssetPostprocessor
    {
        private const string UiFolder = KendoImportRules.ArtRoot + "/UI/";

        private void OnPreprocessTexture()
        {
            if (!KendoImportRules.IsUnderArtRoot(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            if (assetPath.Replace('\\', '/').StartsWith(UiFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                return;
            }

            var n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (IsNormalMap(n))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (!IsColorMap(n) && IsLinearData(n))
            {
                importer.sRGBTexture = false;
            }

            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.maxTextureSize = 2048;

            // Standalone headsets (Android): smaller, ASTC-compressed.
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!AnyTexture(imported) && !AnyTexture(deleted) && !AnyTexture(moved) && !AnyTexture(movedFrom)) return;
            EditorApplication.delayCall -= RefreshIndex;
            EditorApplication.delayCall += RefreshIndex;
        }

        private static void RefreshIndex()
        {
            KendoTextureIndex.RegisterCurrentHash();
            AssetDatabase.Refresh();
        }

        private static bool AnyTexture(string[] paths)
        {
            foreach (var p in paths)
            {
                if (!KendoImportRules.IsUnderArtRoot(p)) continue;
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".tif" || ext == ".tiff" || ext == ".exr" || ext == ".psd")
                    return true;
            }
            return false;
        }

        private static bool IsNormalMap(string n) =>
            n.Contains("normal") || n.Contains("_nrm") || n.EndsWith("_nor") || n.EndsWith("_n");

        // Poliigon "..._COL_1K_METALNESS" files are colour maps from the metalness workflow pack.
        private static bool IsColorMap(string n) =>
            n.Contains("basecolor") || n.Contains("_col") || n.Contains("albedo") || n.Contains("diffuse");

        private static readonly string[] LinearTokens =
            { "rough", "metal", "occlusion", "opacity", "height", "disp", "gloss", "mask", "specular" };

        // Only the last two "_" segments are checked, so pack names such as "Poliigon_MetalRust_7642_…"
        // don't count, while "…_Roughness", "…_ROUGHNESS_1K_METALNESS" and "…_2K_Opacity" do.
        private static bool IsLinearData(string n)
        {
            var parts = n.Split('_');
            var start = System.Math.Max(0, parts.Length - 2);
            for (var i = start; i < parts.Length; i++)
            {
                if (parts[i] == "ao") return true;
                foreach (var token in LinearTokens)
                {
                    if (parts[i].Contains(token)) return true;
                }
            }
            return false;
        }
    }
}
