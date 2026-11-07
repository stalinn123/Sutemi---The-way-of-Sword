using System.Collections.Generic;
using System.IO;
using System.Text;
using Kendo.Characters;
using Kendo.Core;
using Kendo.EditorTools.Import;
using Kendo.EditorTools.Setup;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Validation
{
    /// <summary>
    /// Measures the imported assets inside Unity and writes Docs/Generated/AssetValidationReport.md:
    /// importer settings vs rules, real-world size, triangle counts, rig state, clips and untextured materials.
    /// </summary>
    public static class KendoAssetValidator
    {
        private const string ReportPath = "Docs/Generated/AssetValidationReport.md";
        private const int QuestTriangleBudget = 1_000_000;

        public static void RunAndWriteReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Asset validation report").AppendLine();
            sb.AppendLine("| Asset | Scale ok | Size (m) | Triangles | Skinned | Clips | Materials w/o base texture | Notes |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            var warnings = new List<string>();
            foreach (var rule in KendoImportRules.All) sb.AppendLine(Row(rule, warnings));

            sb.AppendLine().AppendLine("## Prefab calibration").AppendLine();
            CheckPrefabs(sb, warnings);

            sb.AppendLine().AppendLine("## Warnings").AppendLine();
            if (warnings.Count == 0) sb.AppendLine("None.");
            foreach (var w in warnings) sb.AppendLine($"- {w}");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Docs/Generated");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[Kendo] Validation written to {ReportPath} ({warnings.Count} warnings).");
            foreach (var w in warnings) Debug.LogWarning("[Kendo] " + w);
        }

        private static string Row(ModelImportRule rule, List<string> warnings)
        {
            var name = rule.FileName;
            if (!(AssetImporter.GetAtPath(rule.AssetPath) is ModelImporter importer))
            {
                warnings.Add($"{name}: file missing at {rule.AssetPath}");
                return $"| {name} | – | – | – | – | – | – | MISSING |";
            }

            var scaleOk = Mathf.Approximately(importer.globalScale, rule.GlobalScale) && !importer.importLights && !importer.importCameras;
            if (!scaleOk) warnings.Add($"{name}: importer differs from rule (run Kendo > Assets > Reapply Import Rules).");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(rule.AssetPath);
            var instance = (GameObject)Object.Instantiate(model);
            try
            {
                var size = KendoPrefabBuilder.RendererBounds(instance.transform).size;
                var tris = 0L;
                foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null) tris += CountTriangles(mf.sharedMesh);
                }
                var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                var clips = 0;
                foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(rule.AssetPath))
                {
                    if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__")) clips++;
                }

                var untextured = new HashSet<string>();
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        if (tex == null) untextured.Add(m.name);
                    }
                }

                if (rule.Role == AssetRole.Environment && tris > QuestTriangleBudget)
                    warnings.Add($"{name}: {tris:N0} triangles – above a ~{QuestTriangleBudget:N0} standalone-VR budget; decimate or target PC VR.");
                if (rule.Role == AssetRole.Character && skinned == 0)
                    warnings.Add($"{name}: no skinned mesh / rig – Humanoid animation is impossible until it is rigged.");

                return $"| {name} | {(scaleOk ? "yes" : "NO")} | {size.x:0.00} × {size.y:0.00} × {size.z:0.00} | {tris:N0} | {skinned} | {clips} | {untextured.Count} | {rule.Notes} |";
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void CheckPrefabs(StringBuilder sb, List<string> warnings)
        {
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.MasterPrefab);
            if (master != null && master.TryGetComponent<KendoCharacterModel>(out var model))
            {
                var h = model.MeasuredHeight;
                var ok = h >= AssetCalibration.MasterHeightRange.x && h <= AssetCalibration.MasterHeightRange.y;
                sb.AppendLine($"- KendoMaster height: {h:0.00} m ({(ok ? "ok" : "OUT OF RANGE")}), rig: {model.RigState}");
                if (!ok) warnings.Add($"KendoMaster height {h:0.00} m outside {AssetCalibration.MasterHeightRange}.");
            }
            else sb.AppendLine("- KendoMaster prefab not built.");

            var shinai = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.ShinaiPrefab);
            if (shinai != null && shinai.TryGetComponent<ShinaiModel>(out var s))
            {
                var ok = s.Length >= AssetCalibration.ShinaiLengthRange.x && s.Length <= AssetCalibration.ShinaiLengthRange.y;
                sb.AppendLine($"- Shinai length: {s.Length:0.00} m ({(ok ? "ok" : "OUT OF RANGE")})");
                if (!ok) warnings.Add($"Shinai length {s.Length:0.00} m outside {AssetCalibration.ShinaiLengthRange}.");
            }
            else sb.AppendLine("- Shinai prefab not built.");
        }

        private static long CountTriangles(Mesh mesh)
        {
            long total = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles) total += mesh.GetIndexCount(i) / 3;
            }
            return total;
        }
    }
}
