using System.Collections.Generic;
using System.IO;
using Kendo.Core;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Kendo.EditorTools.Import
{
    /// <summary>
    /// Applies <see cref="KendoImportRules"/> to the supplied FBX files:
    /// scale calibration, no cameras/lights, cleanup of stray nodes, shinai extraction,
    /// static flags for environments and relinking of external textures by file name.
    /// </summary>
    public sealed class KendoModelPostprocessor : AssetPostprocessor
    {
        // Bump when the post-processing logic changes so Unity reimports the models.
        private const uint Version = 2;

        public const string GripMarker = "GripEnd";
        public const string TsubaMarker = "Tsuba";
        public const string TipMarker = "Tip";

        private Dictionary<string, string> textureIndex;

        public override uint GetVersion() => Version;

        // Run after URP's own FBX material-description preprocessor so we only fill in what it could not resolve.
        public override int GetPostprocessOrder() => 10;

        private void OnPreprocessModel()
        {
            if (!KendoImportRules.TryGet(assetPath, out var rule)) return;
            var importer = (ModelImporter)assetImporter;

            // Apply rules only on first import so later inspector tweaks survive.
            // Kendo > Assets > Reapply Import Rules forces them again.
            if (!importer.importSettingsMissing) return;
            ApplyRule(importer, rule);
        }

        public static void ApplyRule(ModelImporter importer, ModelImportRule rule)
        {
            importer.globalScale = rule.GlobalScale;
            importer.useFileScale = true;
            importer.bakeAxisConversion = rule.BakeAxisConversion;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = true;
            importer.importBlendShapes = false;
            importer.preserveHierarchy = true;
            importer.sortHierarchyByName = true;

            // the character is skinned at runtime (KendokaBody), which reads its vertices
            importer.isReadable = rule.Role == AssetRole.Character;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            importer.indexFormat = ModelImporterIndexFormat.Auto; // several temple meshes exceed 65k vertices
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = rule.GenerateLightmapUVs;
            importer.addCollider = false; // colliders are authored as simple primitives, never from 2.5M-tri meshes

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;

            importer.animationType = rule.AnimationType;
            importer.importAnimation = rule.ImportAnimation;
            if (rule.ImportAnimation)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.resampleCurves = true;
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!KendoImportRules.TryGet(assetPath, out var rule)) return;

            foreach (var nodeName in rule.DestroyNodes)
            {
                foreach (var node in FindAll(root.transform, nodeName))
                {
                    // Unity null-check: a parent destroyed earlier in this loop takes its children with it.
                    if (node != null && node != root.transform) Object.DestroyImmediate(node.gameObject);
                }
            }

            foreach (var nodeName in rule.DisableNodes)
            {
                foreach (var node in FindAll(root.transform, nodeName)) node.gameObject.SetActive(false);
            }

            if (rule.Shinai != null) ExtractShinai(root.transform, rule.Shinai);

            if (rule.Role == AssetRole.Environment) MarkEnvironmentStatic(root, rule);

            if (rule.AnimationType == ModelImporterAnimationType.None && root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                context.LogImportWarning($"{Path.GetFileName(assetPath)} now contains skinned meshes. " +
                                         "Update its rule in KendoImportRules to Humanoid/Generic and run Kendo > Assets > Reapply Import Rules.");
            }
        }

        // ---------------------------------------------------------------- shinai

        private void ExtractShinai(Transform root, ShinaiExtraction spec)
        {
            var parts = new List<Transform>();
            foreach (var nodeName in spec.Nodes)
            {
                var t = FindFirst(root, nodeName);
                if (t != null) parts.Add(t);
            }

            var blade = FindFirst(root, spec.BladeNode);
            var grip = FindFirst(root, spec.GripNode);
            if (parts.Count == 0 || blade == null || grip == null)
            {
                context.LogImportWarning($"Shinai nodes not found in {Path.GetFileName(assetPath)} – the sword was not extracted.");
                return;
            }

            var bladeBounds = WorldMeshBounds(blade);
            var gripBounds = WorldMeshBounds(grip);
            var axis = bladeBounds.center - gripBounds.center;
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.up;

            // Extent of every sword part along the blade axis gives the butt (grip end) and the tip.
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var part in parts)
            {
                ProjectOnAxis(WorldMeshBounds(part), bladeBounds.center, axis, ref min, ref max);
            }

            var gripEnd = bladeBounds.center + axis * min;
            var tip = bladeBounds.center + axis * max;

            var group = new GameObject(spec.GroupName).transform;
            group.SetParent(root, false);
            group.SetPositionAndRotation(gripEnd, Quaternion.FromToRotation(Vector3.up, axis));
            foreach (var part in parts) part.SetParent(group, true);

            CreateMarker(group, GripMarker, gripEnd);
            CreateMarker(group, TipMarker, tip);
            var tsuba = FindFirst(group, spec.TsubaNode);
            CreateMarker(group, TsubaMarker, tsuba != null ? WorldMeshBounds(tsuba).center : gripEnd);
        }

        private static void CreateMarker(Transform parent, string markerName, Vector3 worldPosition)
        {
            var marker = new GameObject(markerName).transform;
            marker.SetParent(parent, false);
            marker.position = worldPosition;
            marker.localRotation = Quaternion.identity;
        }

        private static void ProjectOnAxis(Bounds b, Vector3 origin, Vector3 axis, ref float min, ref float max)
        {
            var e = b.extents;
            for (var i = 0; i < 8; i++)
            {
                var corner = b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var d = Vector3.Dot(corner - origin, axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }

        /// <summary>World-space bounds from MeshFilters (Renderer.bounds is not reliable during import).</summary>
        public static Bounds WorldMeshBounds(Transform t)
        {
            var hasBounds = false;
            var result = new Bounds(t.position, Vector3.zero);
            foreach (var filter in t.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var mb = mesh.bounds;
                var e = mb.extents;
                for (var i = 0; i < 8; i++)
                {
                    var local = mb.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var world = filter.transform.TransformPoint(local);
                    if (!hasBounds)
                    {
                        result = new Bounds(world, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(world);
                    }
                }
            }
            return result;
        }

        // ---------------------------------------------------------------- environment

        private static void MarkEnvironmentStatic(GameObject root, ModelImportRule rule)
        {
            var layer = LayerMask.NameToLayer(KendoLayers.Environment);
            var dynamicRoots = new HashSet<Transform>();
            foreach (var nodeName in rule.DynamicNodes)
            {
                foreach (var node in FindAll(root.transform, nodeName)) dynamicRoots.Add(node);
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (layer >= 0) t.gameObject.layer = layer;
                if (IsUnderAny(t, dynamicRoots)) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic |
                    (rule.GenerateLightmapUVs ? StaticEditorFlags.ContributeGI : 0));
            }
        }

        private static bool IsUnderAny(Transform t, HashSet<Transform> roots)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (roots.Contains(p)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- materials / textures

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!KendoImportRules.TryGet(assetPath, out _)) return;

            // Re-run this import whenever textures are added/removed under the Art folder.
            context.DependsOnCustomDependency(KendoTextureIndex.DependencyName);

            AssignIfMissing(description, material, "DiffuseColor", new[] { "_BaseMap", "_MainTex" }, null);
            if (!AssignIfMissing(description, material, "NormalMap", new[] { "_BumpMap" }, "_NORMALMAP"))
            {
                AssignIfMissing(description, material, "Bump", new[] { "_BumpMap" }, "_NORMALMAP");
            }
            AssignIfMissing(description, material, "EmissiveColor", new[] { "_EmissionMap" }, "_EMISSION");
            // Roughness/metallic maps from Poliigon/Arnold are separate greyscale files; URP Lit expects
            // a packed metallic(R)/smoothness(A) map. Packing is a follow-up task (see AssetInspectionReport).
        }

        private bool AssignIfMissing(MaterialDescription description, Material material, string property, string[] slots, string keyword)
        {
            if (!description.TryGetProperty(property, out TexturePropertyDescription texProp)) return false;

            string slot = null;
            foreach (var s in slots)
            {
                if (material.HasProperty(s)) { slot = s; break; }
            }
            if (slot == null || material.GetTexture(slot) != null) return false;

            var texture = texProp.texture;
            var sourcePath = !string.IsNullOrEmpty(texProp.path) ? texProp.path : texProp.relativePath;
            if (texture == null) texture = ResolveByFileName(sourcePath);
            if (texture == null)
            {
                context.LogImportWarning($"[{Path.GetFileName(assetPath)}] material '{material.name}': texture " +
                                         $"'{Path.GetFileName(NormalizeSlashes(sourcePath))}' ({property}) not found. " +
                                         $"Copy it anywhere under {KendoImportRules.ArtRoot} and the model reimports automatically.");
                return false;
            }

            material.SetTexture(slot, texture);
            if (keyword != null) material.EnableKeyword(keyword);
            if (slot == "_BaseMap" && material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            return true;
        }

        private Texture ResolveByFileName(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return null;
            var stem = Path.GetFileNameWithoutExtension(Path.GetFileName(NormalizeSlashes(rawPath)));
            if (string.IsNullOrEmpty(stem)) return null;

            textureIndex ??= KendoTextureIndex.Build();
            if (!textureIndex.TryGetValue(stem.ToLowerInvariant(), out var texturePath)) return null;

            context.DependsOnSourceAsset(texturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        // ---------------------------------------------------------------- helpers

        private static string NormalizeSlashes(string path) => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');

        public static Transform FindFirst(Transform root, string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nodeName) return t;
            }
            return null;
        }

        public static List<Transform> FindAll(Transform root, string nodeName)
        {
            var result = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nodeName) result.Add(t);
            }
            return result;
        }
    }
}
