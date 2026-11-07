using Kendo.Characters;
using Kendo.Core;
using Kendo.EditorTools.Import;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>
    /// Builds game-ready prefabs from the supplied FBX files. The supplied master and shinai meshes are used
    /// directly (mesh and material references point into the FBX), so re-exporting the FBX and re-running
    /// this step updates the prefabs without replacing the art.
    /// </summary>
    public static class KendoPrefabBuilder
    {
        public const string MasterPrefab = AssetFolders.Prefabs + "/Characters/KendoMaster.prefab";
        public const string ShinaiPrefab = AssetFolders.Prefabs + "/Props/Shinai.prefab";
        public const string MenPrefab = AssetFolders.Prefabs + "/Props/Bogu_Men.prefab";
        public const string TrainingDojoPrefab = AssetFolders.Prefabs + "/Environments/Env_TrainingDojo.prefab";
        public const string TemplePrefab = AssetFolders.Prefabs + "/Environments/Env_Temple.prefab";
        public const string FinalEnvironmentPrefab = AssetFolders.Prefabs + "/Environments/Env_FinalEnvironment.prefab";
        public static string BadgePrefab(int number) => $"{AssetFolders.Prefabs}/Props/Badge_0{number}.prefab";

        private const string CharacterRootNode = "Personaje_low";
        private const string FaceMaskNode = "Masc_low";
        private const string BodyNode = "Cuerpo_low";

        private enum Pivot { BottomCenter, Center, AsAuthored }

        public static void BuildAll()
        {
            BuildMasterAndShinai();
            BuildWrapped(KendoImportRules.MenHelmet, MenPrefab, "Bogu_Men", Pivot.BottomCenter, go => go.AddComponent<EquipmentPiece>());
            for (var i = 0; i < KendoImportRules.Badges.Length; i++)
            {
                BuildWrapped(KendoImportRules.Badges[i], BadgePrefab(i + 1), $"Badge_0{i + 1}", Pivot.Center, null);
            }
            BuildWrapped(KendoImportRules.TrainingDojo, TrainingDojoPrefab, "Env_TrainingDojo", Pivot.AsAuthored, null);
            BuildWrapped(KendoImportRules.Temple, TemplePrefab, "Env_Temple", Pivot.AsAuthored, null);
            BuildWrapped(KendoImportRules.FinalEnvironment, FinalEnvironmentPrefab, "Env_FinalEnvironment", Pivot.AsAuthored, null);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ master + shinai

        public static void BuildMasterAndShinai()
        {
            var rule = KendoImportRules.KendoMaster;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(rule.AssetPath);
            if (model == null) { Debug.LogError($"[Kendo] Missing {rule.AssetPath}"); return; }

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            GameObject masterRoot = null;
            Transform shinai = null;
            try
            {
                var character = KendoModelPostprocessor.FindFirst(temp.transform, CharacterRootNode);
                shinai = temp.transform.Find(rule.Shinai.GroupName);
                if (character == null || shinai == null)
                {
                    Debug.LogError("[Kendo] Master or Shinai node missing – reimport the model so KendoModelPostprocessor runs " +
                                   "(Kendo > Assets > Reapply Import Rules).");
                    return;
                }

                // Shinai: root at grip end, +Y towards the tip.
                var shinaiAuthoredPosition = shinai.position;
                var shinaiAuthoredRotation = shinai.rotation;
                shinai.SetParent(null, true);
                shinai.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                shinai.name = "Shinai";
                SetLayerRecursive(shinai.gameObject, KendoLayers.Shinai);
                var shinaiModel = shinai.gameObject.AddComponent<ShinaiModel>();
                var gripEnd = shinai.Find(KendoModelPostprocessor.GripMarker);
                var tip = shinai.Find(KendoModelPostprocessor.TipMarker);
                using (var edit = new SerializedEdit(shinaiModel))
                {
                    edit.Set("gripEnd", gripEnd)
                        .Set("tsuba", shinai.Find(KendoModelPostprocessor.TsubaMarker))
                        .Set("tip", tip)
                        .Set("length", gripEnd != null && tip != null ? Vector3.Distance(gripEnd.position, tip.position) : 0f);
                }
                var shinaiAsset = SavePrefab(shinai.gameObject, ShinaiPrefab);

                // Master: feet on origin, facing +Z.
                masterRoot = new GameObject("KendoMaster");
                character.SetParent(masterRoot.transform, true);
                var bounds = RendererBounds(character);
                var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                character.position -= offset;
                var restPosition = shinaiAuthoredPosition - offset;
                var restRotation = shinaiAuthoredRotation;

                var mask = KendoModelPostprocessor.FindFirst(character, FaceMaskNode);
                if (mask != null && RendererBounds(mask).center.z < RendererBounds(character).center.z)
                {
                    // Character faces -Z after axis conversion: turn it (and its resting shinai) to face +Z.
                    var turn = Quaternion.AngleAxis(180f, Vector3.up);
                    character.SetPositionAndRotation(turn * character.position, turn * character.rotation);
                    restPosition = turn * restPosition;
                    restRotation = turn * restRotation;
                }
                SetLayerRecursive(masterRoot, KendoLayers.Characters);

                bounds = RendererBounds(character);
                var sockets = new GameObject("Sockets").transform;
                sockets.SetParent(masterRoot.transform, false);
                var body = KendoModelPostprocessor.FindFirst(character, BodyNode);
                var bodyBounds = body != null ? RendererBounds(body) : bounds;
                var head = CreateSocket(sockets, "Head", mask != null ? RendererBounds(mask).center : new Vector3(0f, bounds.max.y * 0.93f, 0f));
                // Estimated from the A-pose silhouette until hand bones exist (character's right = +X when facing +Z).
                var handHeight = bodyBounds.min.y + bodyBounds.size.y * 0.55f;
                var handReach = bodyBounds.extents.x * 0.95f;
                var rightHand = CreateSocket(sockets, "RightHand_Estimated", new Vector3(bodyBounds.center.x + handReach, handHeight, bodyBounds.center.z));
                var leftHand = CreateSocket(sockets, "LeftHand_Estimated", new Vector3(bodyBounds.center.x - handReach, handHeight, bodyBounds.center.z));
                var rest = CreateSocket(sockets, "ShinaiRest", restPosition);
                rest.rotation = restRotation;

                var shinaiInstance = (GameObject)PrefabUtility.InstantiatePrefab(shinaiAsset);
                shinaiInstance.transform.SetParent(rest, false);

                var characterModel = masterRoot.AddComponent<KendoCharacterModel>();
                using (var edit = new SerializedEdit(characterModel))
                {
                    edit.SetEnum("role", CharacterRole.Master)
                        .SetEnum("rigState", CharacterRigState.Unrigged)
                        .Set("modelRoot", character)
                        .Set("animator", character.GetComponentInChildren<Animator>())
                        .Set("headSocket", head)
                        .Set("rightHandSocket", rightHand)
                        .Set("leftHandSocket", leftHand)
                        .Set("shinaiRestSocket", rest)
                        .Set("measuredHeight", bounds.size.y);
                }

                SavePrefab(masterRoot, MasterPrefab);
                Debug.Log($"[Kendo] KendoMaster height {bounds.size.y:0.00} m, shinai length {shinaiModel.Length:0.00} m.");
            }
            finally
            {
                if (masterRoot != null) Object.DestroyImmediate(masterRoot);
                if (shinai != null) Object.DestroyImmediate(shinai.gameObject);
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        // ------------------------------------------------------------------ generic wrapper

        private static void BuildWrapped(ModelImportRule rule, string prefabPath, string rootName, Pivot pivot, System.Action<GameObject> configure)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(rule.AssetPath);
            if (model == null) { Debug.LogError($"[Kendo] Missing {rule.AssetPath}"); return; }

            var root = new GameObject(rootName);
            try
            {
                // Keep the model as a nested prefab instance so FBX reimports flow through.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                if (pivot != Pivot.AsAuthored)
                {
                    var b = RendererBounds(instance.transform);
                    var target = pivot == Pivot.BottomCenter ? new Vector3(b.center.x, b.min.y, b.center.z) : b.center;
                    instance.transform.position -= target;
                }
                configure?.Invoke(root);
                SavePrefab(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject SavePrefab(GameObject go, string path)
        {
            AssetFolders.Ensure(System.IO.Path.GetDirectoryName(path));
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path, out var ok);
            if (!ok) Debug.LogError($"[Kendo] Failed to save {path}");
            return asset;
        }

        private static Transform CreateSocket(Transform parent, string socketName, Vector3 position)
        {
            var t = new GameObject(socketName).transform;
            t.SetParent(parent, false);
            t.position = position;
            return t;
        }

        /// <summary>Bounds of active renderers only (disabled helper nodes are ignored).</summary>
        public static Bounds RendererBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void SetLayerRecursive(GameObject go, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
