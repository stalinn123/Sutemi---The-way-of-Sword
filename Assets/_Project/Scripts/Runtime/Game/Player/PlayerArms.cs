using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// The player's visible body in first person: gi sleeves and kote (gloves) from the shoulders to the shinai grip,
    /// solved with two-bone IK every frame so both hands stay on the tsuka. Also carries the player's kote hit zone.
    /// The supplied assets contain no player body, so these meshes are generated (ProceduralMeshes).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class PlayerArms : MonoBehaviour
    {
        private PlayerRig rig;
        private Transform leftForearm, rightForearm, leftKote, rightKote, leftUpper, rightUpper;
        private const float Upper = 0.30f, Fore = 0.27f;

        public void Init(PlayerRig r)
        {
            rig = r;
            var sleeve = ProceduralMeshes.Lit("Gi_Indigo", new Color(0.09f, 0.12f, 0.26f), 0.15f);
            var kote = ProceduralMeshes.Lit("Kote_Indigo", new Color(0.12f, 0.13f, 0.22f), 0.35f);
            leftUpper = Part("UpperArm.L", ProceduralMeshes.Sleeve(), sleeve);
            rightUpper = Part("UpperArm.R", ProceduralMeshes.Sleeve(), sleeve);
            leftForearm = Part("Forearm.L", ProceduralMeshes.Sleeve(), sleeve);
            rightForearm = Part("Forearm.R", ProceduralMeshes.Sleeve(), sleeve);
            leftKote = Part("Kote.L", ProceduralMeshes.Kote(), kote);
            rightKote = Part("Kote.R", ProceduralMeshes.Kote(), kote);
            // red palm accent like real kote
            var palm = ProceduralMeshes.Spawn("Palm", ProceduralMeshes.Kote(), ProceduralMeshes.Lit("Kote_Palm", new Color(0.55f, 0.12f, 0.1f), 0.4f), rightKote);
            palm.transform.localScale = new Vector3(0.9f, 0.9f, 0.7f);
            palm.transform.localPosition = new Vector3(0f, -0.01f, 0.02f);
            var zone = new GameObject("KoteZone").transform;
            zone.SetParent(rightForearm, false);
            rig.AddKoteZone(rightForearm, new Vector3(0f, 0f, 0.55f), new Vector3(0f, 0f, 0.95f));
        }

        private Transform Part(string name, Mesh mesh, Material mat)
        {
            var go = ProceduralMeshes.Spawn(name, mesh, mat, transform, true);
            go.layer = Layers.Characters;
            return go.transform;
        }

        private void LateUpdate()
        {
            if (rig == null || rig.fighter == null || rig.fighter.rig == null) return;
            var sr = rig.fighter.rig;
            var root = rig.transform;
            float bowBend = rig.bowAngle * Mathf.Deg2Rad;
            var bowOffset = new Vector3(0f, -(1.42f - 0.95f) * (1f - Mathf.Cos(bowBend)), (1.42f - 0.95f) * Mathf.Sin(bowBend));
            var lS = root.TransformPoint(new Vector3(-0.19f, 1.42f, 0.02f) + bowOffset);
            var rS = root.TransformPoint(new Vector3(0.19f, 1.42f, 0.02f) + bowOffset);
            Arm(lS, sr.LeftHandPoint, sr.BladeDir, -1f, leftUpper, leftForearm, leftKote, true);
            bool rightOn = sr.RightHandOn;
            var rTarget = rightOn ? sr.RightHandPoint : root.TransformPoint(new Vector3(0.24f, 0.98f, 0.05f));
            Arm(rS, rTarget, rightOn ? sr.BladeDir : Vector3.down, 1f, rightUpper, rightForearm, rightKote, rightOn);
        }

        private void Arm(Vector3 shoulder, Vector3 palm, Vector3 blade, float side, Transform upper, Transform fore, Transform kote, bool gripping)
        {
            var root = rig.transform;
            var wrist = palm - (gripping ? Vector3.ProjectOnPlane(palm - shoulder, blade).normalized * 0.06f : Vector3.up * 0.06f);
            var toW = wrist - shoulder;
            float d = Mathf.Clamp(toW.magnitude, 0.05f, Upper + Fore - 0.005f);
            var dir = toW.normalized;
            var pole = root.TransformDirection(new Vector3(side * 0.7f, -0.7f, -0.1f));
            var perp = Vector3.ProjectOnPlane(pole, dir).normalized;
            float cosA = Mathf.Clamp((Upper * Upper + d * d - Fore * Fore) / (2f * Upper * d), -1f, 1f);
            var elbow = shoulder + dir * cosA * Upper + perp * Mathf.Sqrt(1f - cosA * cosA) * Upper;
            var w = shoulder + dir * d;
            Place(upper, shoulder, elbow, 1.0f);
            Place(fore, elbow, w, 0.9f);
            kote.position = w;
            var kdir = gripping ? (palm - w) : Vector3.down;
            if (kdir.sqrMagnitude < 1e-6f) kdir = fore.forward;
            kote.rotation = Quaternion.LookRotation(kdir.normalized, gripping ? -blade : root.forward);
        }

        private static void Place(Transform t, Vector3 a, Vector3 b, float thickness)
        {
            var v = b - a;
            float len = Mathf.Max(0.01f, v.magnitude);
            t.position = a;
            t.rotation = Quaternion.LookRotation(v / len, Vector3.up);
            t.localScale = new Vector3(thickness, thickness, len);
        }
    }
}
