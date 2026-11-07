using System;
using System.Collections.Generic;
using Kendo.Core;
using UnityEditor;

namespace Kendo.EditorTools.Import
{
    public enum AssetRole
    {
        Character,
        Equipment,
        Badge,
        Environment,
    }

    /// <summary>How to split the shinai out of the combined master + sword file.</summary>
    public sealed class ShinaiExtraction
    {
        public string GroupName = "Shinai";
        /// <summary>FBX node names that make up the sword (shaft, leather grip, tsuba, bindings).</summary>
        public string[] Nodes = Array.Empty<string>();
        public string GripNode;
        public string TsubaNode;
        public string BladeNode;
    }

    /// <summary>Per-asset import settings, kept in code so they are reviewable and versioned.</summary>
    public sealed class ModelImportRule
    {
        public string Folder;
        public string FileName;
        public AssetRole Role;
        public float GlobalScale = 1f;
        public bool ImportAnimation;
        public ModelImporterAnimationType AnimationType = ModelImporterAnimationType.None;
        public bool GenerateLightmapUVs;
        public bool BakeAxisConversion = true;
        /// <summary>Nodes removed on import (stray or scene-dressing geometry that must never reach the game).</summary>
        public string[] DestroyNodes = Array.Empty<string>();
        /// <summary>Nodes kept but deactivated (probable helpers/cutters – reversible).</summary>
        public string[] DisableNodes = Array.Empty<string>();
        /// <summary>Nodes that stay dynamic in an otherwise static environment.</summary>
        public string[] DynamicNodes = Array.Empty<string>();
        public ShinaiExtraction Shinai;
        public string Notes;

        public string AssetPath => $"{KendoImportRules.ArtRoot}/{Folder}/{FileName}";
    }

    public static class KendoImportRules
    {
        public const string ArtRoot = "Assets/_Project/Art";

        public static readonly ModelImportRule KendoMaster = new ModelImportRule
        {
            Folder = "Characters/KendoMaster/Models",
            FileName = "KendoMaster_And_Shinai.fbx",
            Role = AssetRole.Character,
            GlobalScale = AssetCalibration.KendoMasterScale,
            // Source has no armature/skin. The two "Take 001" stacks have zero duration – nothing to import.
            // Switch to Humanoid after the mesh is rigged and re-exported.
            ImportAnimation = false,
            AnimationType = ModelImporterAnimationType.None,
            DestroyNodes = new[] { "Plane" }, // 335 m ground plane used for the Blender render
            Shinai = new ShinaiExtraction
            {
                Nodes = new[] { "Cylinder", "Cylinder.001", "Torus", "Torus.001", "Torus.002", "Torus.003", "Torus.005" },
                BladeNode = "Cylinder",      // bamboo shaft, 3.2 m in source
                GripNode = "Cylinder.001",   // leather tsuka
                TsubaNode = "Torus.005",     // guard ring
            },
            Notes = "Master + shinai, Blender 4.4.3, ~2.44x life size, unrigged, textures external (not embedded).",
        };

        public static readonly ModelImportRule MenHelmet = new ModelImportRule
        {
            Folder = "Characters/Equipment/Models",
            FileName = "Bogu_Men_Helmet.fbx",
            Role = AssetRole.Equipment,
            GlobalScale = AssetCalibration.MenHelmetScale,
            Notes = "Kendo men (helmet), 115 mesh objects, 9 flat-colour materials, no textures.",
        };

        public static readonly ModelImportRule[] Badges = CreateBadgeRules();

        public static readonly ModelImportRule TrainingDojo = new ModelImportRule
        {
            Folder = "Environments/TrainingDojo/Models",
            FileName = "TrainingDojo_Interior.fbx",
            Role = AssetRole.Environment,
            GlobalScale = AssetCalibration.TrainingDojoScale,
            GenerateLightmapUVs = true, // only ~26k triangles, cheap to unwrap
            Notes = "Hub dojo: 991 small meshes, 23 flat-colour materials, 10 lights (not imported).",
        };

        public static readonly ModelImportRule Temple = new ModelImportRule
        {
            Folder = "Environments/Temple/Models",
            FileName = "Temple_HoryujiReplica.fbx",
            Role = AssetRole.Environment,
            GlobalScale = AssetCalibration.TempleScale,
            GenerateLightmapUVs = false, // 2.5M triangles – unwrap would take very long; use light probes / APV
            Notes = "Horyu-ji temple replica (Maya/Arnold origin), 2.53M tris, 47 external textures.",
        };

        public static readonly ModelImportRule FinalEnvironment = new ModelImportRule
        {
            Folder = "Environments/FinalEnvironment/Models",
            FileName = "FinalEnvironment.fbx",
            Role = AssetRole.Environment,
            GlobalScale = AssetCalibration.FinalEnvironmentScale,
            ImportAnimation = true,                          // "Insect|fly" 16.8 s loop
            AnimationType = ModelImporterAnimationType.Generic,
            DisableNodes = new[] { "Sphere" },               // 720 m HDRI sky dome – replace with a skybox material
            DynamicNodes = new[] { "Insect" },
            Notes = "Landscape + temple (2.44x) + 819 foliage card instances, 2.56M tris, 121 external textures.",
        };

        private static ModelImportRule[] CreateBadgeRules()
        {
            var rules = new ModelImportRule[4];
            for (var i = 0; i < rules.Length; i++)
            {
                rules[i] = new ModelImportRule
                {
                    Folder = "Props/Badges/Models",
                    FileName = $"Badge_0{i + 1}.fbx",
                    Role = AssetRole.Badge,
                    GlobalScale = AssetCalibration.BadgeScale,
                    // "Object" is a stray copy of the centre torus floating 24 m above the badge.
                    DestroyNodes = new[] { "Object" },
                    // Material-less / degenerate meshes – almost certainly Blender helpers or boolean cutters.
                    DisableNodes = new[] { "Circle", "Cylinder.001", "Torus", "Torus.001", "Torus.002", "Cube.010" },
                    Notes = "Identical geometry across all four badges except the 'Text' mesh. Materials carry no colour.",
                };
            }
            return rules;
        }

        public static IReadOnlyList<ModelImportRule> All { get; } = BuildAll();

        private static ModelImportRule[] BuildAll()
        {
            var list = new List<ModelImportRule> { KendoMaster, MenHelmet };
            list.AddRange(Badges);
            list.Add(TrainingDojo);
            list.Add(Temple);
            list.Add(FinalEnvironment);
            return list.ToArray();
        }

        public static bool TryGet(string assetPath, out ModelImportRule rule)
        {
            rule = null;
            if (string.IsNullOrEmpty(assetPath)) return false;
            var normalized = assetPath.Replace('\\', '/');
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.AssetPath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    rule = candidate;
                    return true;
                }
            }
            return false;
        }

        public static bool IsUnderArtRoot(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.Replace('\\', '/').StartsWith(ArtRoot + "/", StringComparison.OrdinalIgnoreCase);
    }
}
