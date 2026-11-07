using System.Collections.Generic;
using UnityEngine;

namespace Kendo.Game
{
    public enum BoneId { Hips, Spine, Chest, Neck, Head, UpperArmL, LowerArmL, HandL, UpperArmR, LowerArmR, HandR, UpperLegL, LowerLegL, FootL, UpperLegR, LowerLegR, FootR, Count }

    /// <summary>
    /// Runtime auto-rig for the supplied (unrigged) KendoMaster mesh. Joint positions were measured from the body mesh
    /// (Tools/AssetPrep/measure_joints.py) and are stored as fractions of the full height (1.808 m incl. the hat), so the
    /// rig refits at any import scale. Skin weights: inverse distance⁴ to bone segments with left/right gating and
    /// per-part rules (hat/mask/eyes rigid to the head, flask to the hips, footwear to the feet, cape to the torso).
    /// The skinned meshes are cached and shared by every kendoka in the scene.
    /// </summary>
    public sealed class KendokaBody : MonoBehaviour
    {
        public const float ReferenceHeight = 1.808f;

        // measured joints (metres at the reference height; x = character's right, z = forward, feet at y = 0)
        private static readonly Vector3 HipsJ = new Vector3(0f, 0.92f, -0.01f), SpineJ = new Vector3(0f, 1.08f, -0.01f), ChestJ = new Vector3(0f, 1.28f, 0f),
            NeckJ = new Vector3(0f, 1.50f, 0f), HeadJ = new Vector3(0f, 1.60f, 0.02f), HeadTopJ = new Vector3(0f, 1.80f, 0.02f),
            ShoulderJ = new Vector3(0.17f, 1.40f, -0.01f), ElbowJ = new Vector3(0.37f, 1.25f, 0f), WristJ = new Vector3(0.54f, 1.13f, 0.05f), HandEndJ = new Vector3(0.66f, 1.00f, 0.09f),
            HipJ = new Vector3(0.10f, 0.88f, -0.01f), KneeJ = new Vector3(0.15f, 0.46f, -0.06f), AnkleJ = new Vector3(0.17f, 0.09f, -0.04f), ToeJ = new Vector3(0.21f, 0.02f, 0.10f);

        public Transform[] bones = new Transform[(int)BoneId.Count];
        public Quaternion[] bindLocal = new Quaternion[(int)BoneId.Count];
        public Vector3[] bindLocalPos = new Vector3[(int)BoneId.Count];
        public float scale = 1f;     // height / reference height
        public float height;
        public bool skinned;
        public Transform model;      // the instantiated FBX
        public Transform headTop;
        public readonly Dictionary<string, Renderer> parts = new Dictionary<string, Renderer>();

        public Transform this[BoneId b] => bones[(int)b];

        private static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>Wrist → hand-end direction in bone space (bones share the root's bind rotation).</summary>
        public static Vector3 HandDirection(bool rightHand) => rightHand ? (HandEndJ - WristJ).normalized : Mirror(HandEndJ - WristJ).normalized;

        public static Vector3 Joint(string name, float s)
        {
            switch (name)
            {
                case "HeadTop": return HeadTopJ * s;
                case "HandEndR": return HandEndJ * s;
                case "HandEndL": return Mirror(HandEndJ) * s;
                case "ToeR": return ToeJ * s;
                case "ToeL": return Mirror(ToeJ) * s;
                default: return Vector3.zero;
            }
        }

        /// <summary>Builds bones under this transform and skins every mesh of the model. The model must already be placed
        /// with its feet at this transform's origin, facing +Z.</summary>
        public void Build(Transform modelRoot, float bodyHeight)
        {
            model = modelRoot;
            height = bodyHeight;
            scale = bodyHeight / ReferenceHeight;
            float s = scale;
            var b = bones;
            b[(int)BoneId.Hips] = MakeBone("Hips", transform, HipsJ * s);
            b[(int)BoneId.Spine] = MakeBone("Spine", b[(int)BoneId.Hips], SpineJ * s);
            b[(int)BoneId.Chest] = MakeBone("Chest", b[(int)BoneId.Spine], ChestJ * s);
            b[(int)BoneId.Neck] = MakeBone("Neck", b[(int)BoneId.Chest], NeckJ * s);
            b[(int)BoneId.Head] = MakeBone("Head", b[(int)BoneId.Neck], HeadJ * s);
            headTop = MakeBone("HeadTop", b[(int)BoneId.Head], HeadTopJ * s);
            b[(int)BoneId.UpperArmR] = MakeBone("UpperArm.R", b[(int)BoneId.Chest], ShoulderJ * s);
            b[(int)BoneId.LowerArmR] = MakeBone("LowerArm.R", b[(int)BoneId.UpperArmR], ElbowJ * s);
            b[(int)BoneId.HandR] = MakeBone("Hand.R", b[(int)BoneId.LowerArmR], WristJ * s);
            b[(int)BoneId.UpperArmL] = MakeBone("UpperArm.L", b[(int)BoneId.Chest], Mirror(ShoulderJ) * s);
            b[(int)BoneId.LowerArmL] = MakeBone("LowerArm.L", b[(int)BoneId.UpperArmL], Mirror(ElbowJ) * s);
            b[(int)BoneId.HandL] = MakeBone("Hand.L", b[(int)BoneId.LowerArmL], Mirror(WristJ) * s);
            b[(int)BoneId.UpperLegR] = MakeBone("UpperLeg.R", b[(int)BoneId.Hips], HipJ * s);
            b[(int)BoneId.LowerLegR] = MakeBone("LowerLeg.R", b[(int)BoneId.UpperLegR], KneeJ * s);
            b[(int)BoneId.FootR] = MakeBone("Foot.R", b[(int)BoneId.LowerLegR], AnkleJ * s);
            b[(int)BoneId.UpperLegL] = MakeBone("UpperLeg.L", b[(int)BoneId.Hips], Mirror(HipJ) * s);
            b[(int)BoneId.LowerLegL] = MakeBone("LowerLeg.L", b[(int)BoneId.UpperLegL], Mirror(KneeJ) * s);
            b[(int)BoneId.FootL] = MakeBone("Foot.L", b[(int)BoneId.LowerLegL], Mirror(AnkleJ) * s);
            for (int i = 0; i < b.Length; i++)
            {
                bindLocal[i] = b[i].localRotation;
                bindLocalPos[i] = b[i].localPosition;
            }
            Skin();
        }

        private Transform MakeBone(string name, Transform parent, Vector3 rootSpacePos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = transform.TransformPoint(rootSpacePos);
            t.rotation = transform.rotation;
            return t;
        }

        // ------------------------------------------------------------------ skinning

        private struct Seg
        {
            public int bone;
            public Vector3 a, b;
            public int side; // -1 left, 0 centre, +1 right
            public float factor;
        }

        private static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        private List<Seg> Segments(string part)
        {
            float s = scale;
            var all = new List<Seg>
            {
                new Seg { bone = (int)BoneId.Hips, a = HipsJ * s, b = SpineJ * s, factor = 1f },
                new Seg { bone = (int)BoneId.Spine, a = SpineJ * s, b = ChestJ * s, factor = 1f },
                new Seg { bone = (int)BoneId.Chest, a = ChestJ * s, b = NeckJ * s, factor = 1f },
                new Seg { bone = (int)BoneId.Neck, a = NeckJ * s, b = HeadJ * s, factor = 1f },
                new Seg { bone = (int)BoneId.Head, a = HeadJ * s, b = HeadTopJ * s, factor = 1f },
                new Seg { bone = (int)BoneId.UpperArmR, a = ShoulderJ * s, b = ElbowJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.LowerArmR, a = ElbowJ * s, b = WristJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.HandR, a = WristJ * s, b = HandEndJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.UpperArmL, a = Mirror(ShoulderJ) * s, b = Mirror(ElbowJ) * s, side = -1, factor = 1f },
                new Seg { bone = (int)BoneId.LowerArmL, a = Mirror(ElbowJ) * s, b = Mirror(WristJ) * s, side = -1, factor = 1f },
                new Seg { bone = (int)BoneId.HandL, a = Mirror(WristJ) * s, b = Mirror(HandEndJ) * s, side = -1, factor = 1f },
                new Seg { bone = (int)BoneId.UpperLegR, a = HipJ * s, b = KneeJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.LowerLegR, a = KneeJ * s, b = AnkleJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.FootR, a = AnkleJ * s, b = ToeJ * s, side = 1, factor = 1f },
                new Seg { bone = (int)BoneId.UpperLegL, a = Mirror(HipJ) * s, b = Mirror(KneeJ) * s, side = -1, factor = 1f },
                new Seg { bone = (int)BoneId.LowerLegL, a = Mirror(KneeJ) * s, b = Mirror(AnkleJ) * s, side = -1, factor = 1f },
                new Seg { bone = (int)BoneId.FootL, a = Mirror(AnkleJ) * s, b = Mirror(ToeJ) * s, side = -1, factor = 1f },
            };
            string p = part.ToLowerInvariant();
            if (p.StartsWith("masc") || p.StartsWith("ojos") || p.StartsWith("sombrero")) return all.FindAll(x => x.bone == (int)BoneId.Head);
            if (p.StartsWith("frasco")) return all.FindAll(x => x.bone == (int)BoneId.Hips);
            if (p.StartsWith("calzado") || p.StartsWith("medias"))
                return all.FindAll(x => x.bone == (int)BoneId.FootL || x.bone == (int)BoneId.FootR || x.bone == (int)BoneId.LowerLegL || x.bone == (int)BoneId.LowerLegR);
            if (p.StartsWith("capa"))
            {
                var c = all.FindAll(x => x.bone <= (int)BoneId.Chest || x.bone == (int)BoneId.UpperLegL || x.bone == (int)BoneId.UpperLegR);
                for (int i = 0; i < c.Count; i++) { var q = c[i]; if (q.bone >= (int)BoneId.UpperLegL) q.factor = 0.35f; c[i] = q; }
                return c;
            }
            return all;
        }

        private static float SegDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        private void Skin()
        {
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            int count = 0;
            foreach (var f in filters)
            {
                var mr = f.GetComponent<MeshRenderer>();
                if (mr == null || f.sharedMesh == null) continue;
                if (f.GetComponentInParent<ShinaiTag>() != null) continue; // the embedded shinai is not part of the body
                string part = f.gameObject.name;
                parts[part] = mr;
                var src = f.sharedMesh;
                if (!src.isReadable)
                {
                    Debug.LogWarning($"[Kendo] {src.name} is not readable – enable Read/Write on KendoMaster_And_Shinai.fbx (Kendo ▸ Build Game does this). The character stays unskinned.");
                    continue;
                }
                var meshToRoot = transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                int key = src.GetInstanceID() ^ (Mathf.RoundToInt(scale * 1000f) << 8);
                if (!cache.TryGetValue(key, out var skinnedMesh) || skinnedMesh == null)
                {
                    skinnedMesh = BuildSkinnedMesh(src, meshToRoot, part);
                    cache[key] = skinnedMesh;
                }
                var go = new GameObject(part + "_Skinned");
                go.transform.SetParent(f.transform.parent, false);
                go.transform.localPosition = f.transform.localPosition;
                go.transform.localRotation = f.transform.localRotation;
                go.transform.localScale = f.transform.localScale;
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = skinnedMesh;
                smr.sharedMaterials = mr.sharedMaterials;
                smr.bones = bones;
                smr.rootBone = bones[(int)BoneId.Hips];
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = mr.shadowCastingMode;
                var lb = skinnedMesh.bounds;
                lb.Expand(0.6f);
                smr.localBounds = lb;
                mr.enabled = false;
                parts[part] = smr;
                count++;
            }
            skinned = count > 0;
        }

        private Mesh BuildSkinnedMesh(Mesh src, Matrix4x4 meshToRoot, string part)
        {
            var mesh = Instantiate(src);
            mesh.name = src.name + "_Skinned";
            var verts = src.vertices;
            var segs = Segments(part);
            var weights = new BoneWeight[verts.Length];
            var w = new float[segs.Count];
            for (int v = 0; v < verts.Length; v++)
            {
                var p = meshToRoot.MultiplyPoint3x4(verts[v]);
                float total = 0f;
                for (int i = 0; i < segs.Count; i++)
                {
                    var sg = segs[i];
                    if (sg.side != 0 && Mathf.Abs(p.x) > 0.04f * scale && Mathf.Sign(p.x) != sg.side) { w[i] = 0f; continue; }
                    float d = SegDistance(p, sg.a, sg.b) + 0.01f;
                    w[i] = sg.factor / (d * d * d * d);
                    total += w[i];
                }
                weights[v] = Top4(segs, w);
            }
            mesh.boneWeights = weights;
            var bind = new Matrix4x4[bones.Length];
            var rootToMesh = meshToRoot.inverse;
            for (int i = 0; i < bones.Length; i++)
            {
                var boneToRoot = transform.worldToLocalMatrix * bones[i].localToWorldMatrix;
                bind[i] = boneToRoot.inverse * meshToRoot;
            }
            mesh.bindposes = bind;
            return mesh;
        }

        private static BoneWeight Top4(List<Seg> segs, float[] w)
        {
            int[] idx = { -1, -1, -1, -1 };
            float[] val = { 0, 0, 0, 0 };
            for (int i = 0; i < segs.Count; i++)
            {
                float x = w[i];
                for (int k = 0; k < 4; k++)
                {
                    if (x > val[k])
                    {
                        for (int m = 3; m > k; m--) { val[m] = val[m - 1]; idx[m] = idx[m - 1]; }
                        val[k] = x;
                        idx[k] = i;
                        break;
                    }
                }
            }
            float sum = val[0] + val[1] + val[2] + val[3];
            if (sum <= 0f) return new BoneWeight { boneIndex0 = (int)BoneId.Hips, weight0 = 1f };
            var bw = new BoneWeight();
            bw.boneIndex0 = idx[0] >= 0 ? segs[idx[0]].bone : 0; bw.weight0 = val[0] / sum;
            bw.boneIndex1 = idx[1] >= 0 ? segs[idx[1]].bone : 0; bw.weight1 = val[1] / sum;
            bw.boneIndex2 = idx[2] >= 0 ? segs[idx[2]].bone : 0; bw.weight2 = val[2] / sum;
            bw.boneIndex3 = idx[3] >= 0 ? segs[idx[3]].bone : 0; bw.weight3 = val[3] / sum;
            return bw;
        }

        public void ResetPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localRotation = bindLocal[i];
                bones[i].localPosition = bindLocalPos[i];
            }
        }

        public void SetPartVisible(string prefix, bool visible)
        {
            foreach (var kv in parts)
                if (kv.Key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) && kv.Value != null) kv.Value.enabled = visible;
        }

        public IEnumerable<Renderer> PartRenderers(string prefix)
        {
            foreach (var kv in parts)
                if (kv.Key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) && kv.Value != null) yield return kv.Value;
        }
    }

    /// <summary>Marks the embedded shinai group inside a character model so the rig ignores it.</summary>
    public sealed class ShinaiTag : MonoBehaviour { }
}
