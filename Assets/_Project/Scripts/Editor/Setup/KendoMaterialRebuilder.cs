using System.Collections.Generic;
using System.IO;
using System.Text;
using Kendo.EditorTools.Import;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>
    /// Blender exported many colours as its 0.8 grey default (they lived in shader nodes FBX cannot carry, see the
    /// asset inspection report). This rebuilds those materials as URP Lit assets with colours reconstructed from the
    /// material names and remaps them on the importer – the FBX file itself is untouched. Authored colours are kept.
    /// </summary>
    public static class KendoMaterialRebuilder
    {
        private struct Look
        {
            public Color color;
            public float smooth, metal;
            public Look(float r, float g, float b, float s = 0.25f, float m = 0f) { color = new Color(r, g, b); smooth = s; metal = m; }
        }

        private static readonly (string key, Look look)[] Palette =
        {
            ("dark wood", new Look(0.24f, 0.15f, 0.09f, 0.35f)), ("light wood", new Look(0.62f, 0.46f, 0.30f, 0.3f)), ("post wood", new Look(0.33f, 0.21f, 0.12f, 0.3f)),
            ("floor a", new Look(0.50f, 0.34f, 0.20f, 0.45f)), ("floor b", new Look(0.46f, 0.31f, 0.18f, 0.45f)), ("floor", new Look(0.48f, 0.33f, 0.19f, 0.45f)),
            ("wood", new Look(0.45f, 0.30f, 0.17f, 0.3f)), ("veneer", new Look(0.55f, 0.40f, 0.25f, 0.35f)), ("tatami", new Look(0.70f, 0.66f, 0.42f, 0.15f)),
            ("plaster", new Look(0.86f, 0.83f, 0.76f, 0.1f)), ("shoji", new Look(0.93f, 0.89f, 0.78f, 0.05f)), ("paper", new Look(0.90f, 0.86f, 0.74f, 0.05f)),
            ("stone", new Look(0.50f, 0.49f, 0.46f, 0.15f)), ("gravel", new Look(0.58f, 0.56f, 0.52f, 0.1f)), ("rock", new Look(0.45f, 0.44f, 0.42f, 0.1f)),
            ("roof", new Look(0.20f, 0.22f, 0.25f, 0.3f)), ("tile", new Look(0.22f, 0.23f, 0.26f, 0.3f)), ("gold", new Look(0.83f, 0.66f, 0.27f, 0.6f, 0.9f)),
            ("bronze", new Look(0.55f, 0.38f, 0.20f, 0.5f, 0.8f)), ("metal", new Look(0.35f, 0.35f, 0.37f, 0.5f, 0.8f)), ("iron", new Look(0.25f, 0.25f, 0.27f, 0.4f, 0.8f)),
            ("rattan", new Look(0.66f, 0.52f, 0.32f, 0.2f)), ("bamboo", new Look(0.76f, 0.66f, 0.40f, 0.35f)), ("fabric", new Look(0.62f, 0.60f, 0.56f, 0.1f)),
            ("cloth", new Look(0.55f, 0.54f, 0.52f, 0.1f)), ("towel", new Look(0.82f, 0.80f, 0.76f, 0.05f)), ("cotton", new Look(0.80f, 0.79f, 0.74f, 0.05f)),
            ("leather", new Look(0.33f, 0.21f, 0.13f, 0.35f)), ("skin", new Look(0.80f, 0.62f, 0.50f, 0.3f)), ("cuerpo", new Look(0.80f, 0.62f, 0.50f, 0.3f)),
            ("grass", new Look(0.26f, 0.38f, 0.18f, 0.1f)), ("leaf", new Look(0.22f, 0.36f, 0.16f, 0.1f)), ("bark", new Look(0.30f, 0.23f, 0.17f, 0.1f)),
            ("ground", new Look(0.40f, 0.36f, 0.30f, 0.1f)), ("plank", new Look(0.48f, 0.33f, 0.20f, 0.35f)), ("rope", new Look(0.70f, 0.62f, 0.45f, 0.1f)),
        };

        private static bool IsBlenderGrey(Color c) => Mathf.Abs(c.r - c.g) < 0.02f && Mathf.Abs(c.g - c.b) < 0.02f && c.r > 0.55f && c.r < 0.85f;

        public static void RemapAll(StringBuilder report)
        {
            foreach (var rule in KendoImportRules.All)
            {
                if (rule.Role == AssetRole.Badge) continue; // badges get their own palettes (KendoBadgeBuilder)
                var importer = AssetImporter.GetAtPath(rule.AssetPath) as ModelImporter;
                if (importer == null) continue;
                int remapped = 0, kept = 0;
                string folder = $"{KendoGameBuilder.Generated}/Materials/{Path.GetFileNameWithoutExtension(rule.FileName)}";
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(rule.AssetPath))
                {
                    if (!(obj is Material src)) continue;
                    Color baseColor = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.color : Color.white);
                    bool textured = src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null;
                    if (textured || !IsBlenderGrey(baseColor)) { kept++; continue; }
                    string n = src.name.ToLowerInvariant();
                    Look? look = null;
                    foreach (var p in Palette) if (n.Contains(p.key)) { look = p.look; break; }
                    if (!look.HasValue) { kept++; continue; }
                    AssetFolders.Ensure(folder);
                    string path = $"{folder}/{Sanitize(src.name)}.mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null)
                    {
                        m = new Material(src) { name = src.name };
                        AssetDatabase.CreateAsset(m, path);
                    }
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", look.Value.color);
                    m.color = look.Value.color;
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", look.Value.smooth);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", look.Value.metal);
                    EditorUtility.SetDirty(m);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
                    remapped++;
                }
                if (remapped > 0) importer.SaveAndReimport();
                report.AppendLine($"- Materials {rule.FileName}: {remapped} reconstructed from names, {kept} kept as authored");
            }
        }

        public static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('.', '_');
        }
    }

    /// <summary>
    /// Badges 1–4 (supplied, identical except their "level N completed" text) get level palettes; badge 5 "Master's
    /// Legacy" is built from Badge_04's geometry with a crimson field, gold rim and a new engraved label (generated
    /// texture Art/UI/Sprites/Badge05_Label.png) over the original text plane.
    /// </summary>
    public static class KendoBadgeBuilder
    {
        private static readonly Color[,] Palettes =
        {
            // rim, field, banner, accent, text
            { new Color(0.62f, 0.40f, 0.20f), new Color(0.46f, 0.28f, 0.14f), new Color(0.30f, 0.12f, 0.08f), new Color(0.80f, 0.55f, 0.35f), new Color(0.95f, 0.85f, 0.6f) },  // bronze
            { new Color(0.78f, 0.79f, 0.82f), new Color(0.55f, 0.57f, 0.62f), new Color(0.12f, 0.16f, 0.30f), new Color(0.92f, 0.92f, 0.95f), new Color(0.97f, 0.97f, 1.0f) },  // silver
            { new Color(0.86f, 0.68f, 0.26f), new Color(0.70f, 0.52f, 0.18f), new Color(0.40f, 0.06f, 0.05f), new Color(0.98f, 0.85f, 0.50f), new Color(1.0f, 0.95f, 0.75f) },  // gold
            { new Color(0.84f, 0.66f, 0.28f), new Color(0.06f, 0.10f, 0.28f), new Color(0.03f, 0.04f, 0.10f), new Color(0.95f, 0.75f, 0.80f), new Color(0.95f, 0.82f, 0.45f) },  // indigo lacquer
            { new Color(0.90f, 0.72f, 0.30f), new Color(0.48f, 0.04f, 0.06f), new Color(0.04f, 0.03f, 0.03f), new Color(0.98f, 0.75f, 0.82f), new Color(0.98f, 0.86f, 0.48f) },  // Master's Legacy
        };

        public static GameObject[] BuildAll(StringBuilder report)
        {
            var result = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                var rule = KendoImportRules.Badges[Mathf.Min(i, 3)];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(rule.AssetPath);
                if (model == null) { report.AppendLine($"- Badge {i + 1}: model missing"); continue; }
                var root = new GameObject($"Badge_0{i + 1}");
                try
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    var b = KendoPrefabBuilder.RendererBounds(inst.transform);
                    inst.transform.position -= b.center;
                    var mats = Materials(i);
                    Renderer text = null;
                    foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    {
                        string n = r.gameObject.name.ToLowerInvariant();
                        int slot = n.StartsWith("text") ? 4 : (n.StartsWith("torus") || n.StartsWith("cylinder") ? 0 : (n.StartsWith("cube") ? 2 : (n.StartsWith("flower") || n.StartsWith("sphere") ? 3 : 1)));
                        if (n.StartsWith("text")) text = r;
                        var arr = new Material[r.sharedMaterials.Length];
                        for (int k = 0; k < arr.Length; k++) arr[k] = mats[slot];
                        r.sharedMaterials = arr;
                    }
                    if (i == 4 && text != null) Label(root.transform, text);
                    string path = $"{AssetFolders.Prefabs}/Props/Badge_0{i + 1}_Game.prefab";
                    AssetFolders.Ensure(Path.GetDirectoryName(path));
                    result[i] = PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine($"- Badge {i + 1}: {path}" + (i == 4 ? " (built from Badge_04 geometry + new label)" : ""));
                }
                finally { Object.DestroyImmediate(root); }
            }
            return result;
        }

        private static Material[] Materials(int badge)
        {
            var m = new Material[5];
            string[] names = { "Rim", "Field", "Banner", "Accent", "Text" };
            string folder = $"{KendoGameBuilder.Generated}/Materials/Badges";
            AssetFolders.Ensure(folder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            for (int k = 0; k < 5; k++)
            {
                string path = $"{folder}/Badge{badge + 1}_{names[k]}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                var c = Palettes[badge, k];
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                bool metal = k == 0 || k == 4 || (k == 1 && badge < 3);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal ? 0.9f : 0.1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", metal ? 0.65f : 0.8f);
                EditorUtility.SetDirty(mat);
                m[k] = mat;
            }
            return m;
        }

        /// <summary>Hides the original "level 4 completed" text and lays the engraved "Master's Legacy" label on its plane.</summary>
        private static void Label(Transform root, Renderer text)
        {
            text.enabled = false;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetFolders.Project + "/Art/UI/Sprites/Badge05_Label.png");
            var b = text.bounds;
            var quad = new GameObject("MastersLegacyLabel");
            quad.transform.SetParent(root, true);
            quad.transform.position = new Vector3(b.center.x, b.max.y + 0.0005f, b.center.z);
            var mesh = new Mesh { name = "LabelQuad" };
            float w = Mathf.Max(b.size.x, 0.01f) * 1.05f, d = Mathf.Max(b.size.z, 0.005f) * 1.6f;
            mesh.vertices = new[] { new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(-w / 2, 0, d / 2), new Vector3(w / 2, 0, d / 2) };
            mesh.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            string meshPath = $"{KendoGameBuilder.Generated}/Models/Badge05_Label.asset";
            AssetFolders.Ensure(Path.GetDirectoryName(meshPath));
            AssetDatabase.CreateAsset(mesh, meshPath);
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Badge05_Label" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            string matPath = $"{KendoGameBuilder.Generated}/Materials/Badges/Badge05_Label.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            quad.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
