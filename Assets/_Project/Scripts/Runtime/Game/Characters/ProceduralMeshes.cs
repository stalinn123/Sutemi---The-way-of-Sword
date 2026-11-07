using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kendo.Game
{
    /// <summary>
    /// Meshes the supplied assets do not contain: the opponents' do (chest protector), the player's kote (gloves) and
    /// gi sleeves, and training guides (footprints, kensen ring, floor marks). Generated once and cached.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static Mesh doMesh, koteMesh, sleeveMesh, footprintMesh, ringMesh, discMesh, fingersMesh;
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static Material Lit(string name, Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            string key = name + ColorUtility.ToHtmlStringRGBA(color);
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var cfg = KendoGameConfig.Load();
            m = cfg != null && cfg.litTemplate != null ? new Material(cfg.litTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.name = name;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            materials[key] = m;
            return m;
        }

        public static Material Unlit(string name, Color color, bool transparent = true)
        {
            string key = "U" + name + ColorUtility.ToHtmlStringRGBA(color);
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var cfg = KendoGameConfig.Load();
            m = cfg != null && cfg.unlitTemplate != null ? new Material(cfg.unlitTemplate) : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            m.name = name;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (transparent && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            materials[key] = m;
            return m;
        }

        /// <summary>Do: a lacquered chest shell (curved, wider at the bottom) with a cloth upper part, 0.42 × 0.40 m.</summary>
        public static Mesh Do()
        {
            if (doMesh != null) return doMesh;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            const int cols = 24, rows = 10;
            for (int r = 0; r <= rows; r++)
            {
                float y = r / (float)rows;                       // 0 bottom .. 1 top
                float width = Mathf.Lerp(0.23f, 0.18f, y);       // half width
                float bulge = Mathf.Lerp(0.16f, 0.12f, y) + 0.02f * Mathf.Sin(y * Mathf.PI);
                for (int c = 0; c <= cols; c++)
                {
                    float a = Mathf.Lerp(-1.25f, 1.25f, c / (float)cols); // radians around the torso
                    var p = new Vector3(Mathf.Sin(a) * width, y * 0.40f, Mathf.Cos(a) * bulge);
                    v.Add(p);
                    n.Add(new Vector3(Mathf.Sin(a) / width, 0f, Mathf.Cos(a) / bulge).normalized);
                }
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int i = r * (cols + 1) + c;
                    t.AddRange(new[] { i, i + cols + 1, i + 1, i + 1, i + cols + 1, i + cols + 2 });
                }
            doMesh = new Mesh { name = "Do_Procedural" };
            doMesh.SetVertices(v);
            doMesh.SetNormals(n);
            doMesh.SetTriangles(t, 0);
            doMesh.RecalculateBounds();
            return doMesh;
        }

        /// <summary>Lathe helper: profile (radius, z) revolved about +Z.</summary>
        private static Mesh Lathe(string name, Vector2[] profile, int segments, float flatten = 1f)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, Mathf.Sin(a) * profile[i].x * flatten, profile[i].y));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int a = i * (segments + 1) + s;
                    int b = a + segments + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Kote: padded glove, wrist cuff to fist, along +Z (0.20 m), slightly flattened.</summary>
        public static Mesh Kote() => koteMesh != null ? koteMesh : (koteMesh = Lathe("Kote_Procedural", new[]
        {
            new Vector2(0.001f, -0.09f), new Vector2(0.050f, -0.085f), new Vector2(0.060f, -0.05f), new Vector2(0.055f, 0.0f),
            new Vector2(0.048f, 0.04f), new Vector2(0.052f, 0.07f), new Vector2(0.045f, 0.10f), new Vector2(0.025f, 0.12f), new Vector2(0.001f, 0.125f),
        }, 18, 0.8f));

        /// <summary>Forearm in a gi sleeve along +Z (unit length, scaled at runtime), wide at the elbow.</summary>
        public static Mesh Sleeve() => sleeveMesh != null ? sleeveMesh : (sleeveMesh = Lathe("Sleeve_Procedural", new[]
        {
            new Vector2(0.075f, 0f), new Vector2(0.07f, 0.3f), new Vector2(0.058f, 0.7f), new Vector2(0.05f, 1f),
        }, 16));

        public static Mesh Footprint()
        {
            if (footprintMesh != null) return footprintMesh;
            // sole outline (left foot, +Z toes), flat on the floor
            var outline = new[]
            {
                new Vector2(0.00f, -0.12f), new Vector2(0.035f, -0.11f), new Vector2(0.045f, -0.05f), new Vector2(0.04f, 0.02f), new Vector2(0.05f, 0.08f),
                new Vector2(0.03f, 0.13f), new Vector2(-0.01f, 0.135f), new Vector2(-0.04f, 0.10f), new Vector2(-0.035f, 0.03f), new Vector2(-0.03f, -0.05f), new Vector2(-0.035f, -0.11f),
            };
            var v = new List<Vector3> { Vector3.zero };
            foreach (var p in outline) v.Add(new Vector3(p.x, 0f, p.y));
            var t = new List<int>();
            for (int i = 1; i <= outline.Length; i++) t.AddRange(new[] { 0, i, i % outline.Length + 1 });
            footprintMesh = new Mesh { name = "Footprint" };
            footprintMesh.SetVertices(v);
            footprintMesh.SetTriangles(t, 0);
            footprintMesh.RecalculateNormals();
            return footprintMesh;
        }

        public static Mesh Ring()
        {
            if (ringMesh != null) return ringMesh;
            var v = new List<Vector3>();
            var t = new List<int>();
            const int seg = 40, tube = 8;
            const float R = 0.07f, r = 0.008f;
            for (int i = 0; i <= seg; i++)
                for (int j = 0; j <= tube; j++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f, b = j / (float)tube * Mathf.PI * 2f;
                    v.Add(new Vector3((R + r * Mathf.Cos(b)) * Mathf.Cos(a), (R + r * Mathf.Cos(b)) * Mathf.Sin(a), r * Mathf.Sin(b)));
                }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a = i * (tube + 1) + j, c = a + tube + 1;
                    t.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            ringMesh = new Mesh { name = "KensenRing" };
            ringMesh.SetVertices(v);
            ringMesh.SetTriangles(t, 0);
            ringMesh.RecalculateNormals();
            return ringMesh;
        }

        public static Mesh Disc()
        {
            if (discMesh != null) return discMesh;
            var v = new List<Vector3> { Vector3.zero };
            var t = new List<int>();
            const int seg = 40;
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f)); }
            for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
            discMesh = new Mesh { name = "FloorDisc" };
            discMesh.SetVertices(v);
            discMesh.SetTriangles(t, 0);
            discMesh.RecalculateNormals();
            return discMesh;
        }

        public static GameObject Spawn(string name, Mesh mesh, Material mat, Transform parent, bool shadows = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
