using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Procedural animation for rigged kendoka (master, opponents, former student). Every frame the body is posed
    /// from gameplay state: hands are solved onto the shinai grip with two-bone IK (so the shinai can never detach),
    /// feet onto the footwork model's foot positions, plus bow (rei), lean, head look, kneel and gestures.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class KendokaPoser : MonoBehaviour
    {
        public KendokaBody body;
        [Header("Targets (world space)")]
        public bool leftHandOnGrip = true, rightHandOnGrip = true;
        public Vector3 leftGrip, rightGrip;     // palm centres
        public Vector3 bladeDir = Vector3.forward;
        public Vector3 leftFoot, rightFoot;     // ankle ground points
        public bool feetPlanted = true;
        public float bowDeg;                    // forward bend
        public float leanDeg;                   // into a strike
        public float crouch = 0.03f;            // knee bend (m)
        public float kneel;                     // 0..1 defeat / seiza-like kneel
        public Vector3? lookAt;
        public float presentArm;                // 0..1 right-arm presenting gesture (dialogue)

        private float palm;

        private void Awake() { if (body == null) body = GetComponent<KendokaBody>(); }

        private void LateUpdate()
        {
            if (body == null || body.bones[0] == null) return;
            Solve();
        }

        public void Solve()
        {
            var b = body;
            float s = b.scale;
            palm = 0.07f * s;
            b.ResetPose();
            var right = transform.right;

            // hips height (knee bend / kneel)
            float drop = crouch * s + kneel * 0.42f * s;
            b[BoneId.Hips].position += Vector3.down * drop;

            // torso: bow from the waist, lean into strikes
            float bend = bowDeg + leanDeg + kneel * 10f;
            if (Mathf.Abs(bend) > 0.01f)
            {
                b[BoneId.Spine].rotation = Quaternion.AngleAxis(bend * 0.6f, right) * b[BoneId.Spine].rotation;
                b[BoneId.Chest].rotation = Quaternion.AngleAxis(bend * 0.4f, right) * b[BoneId.Chest].rotation;
            }

            // head look (limited)
            if (lookAt.HasValue)
            {
                var head = b[BoneId.Head];
                var to = lookAt.Value - head.position;
                if (to.sqrMagnitude > 0.01f)
                {
                    var local = transform.InverseTransformDirection(to.normalized);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
                    float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg - bend * 0.5f, -30f, 35f);
                    b[BoneId.Neck].rotation = Quaternion.AngleAxis(yaw * 0.4f, transform.up) * Quaternion.AngleAxis(pitch * 0.4f, right) * b[BoneId.Neck].rotation;
                    head.rotation = Quaternion.AngleAxis(yaw * 0.6f, transform.up) * Quaternion.AngleAxis(pitch * 0.6f, right) * head.rotation;
                }
            }

            // arms
            if (leftHandOnGrip) SolveArm(BoneId.UpperArmL, BoneId.LowerArmL, BoneId.HandL, leftGrip, -1f);
            else RelaxArm(BoneId.UpperArmL, BoneId.LowerArmL, -1f, 0f);
            if (rightHandOnGrip) SolveArm(BoneId.UpperArmR, BoneId.LowerArmR, BoneId.HandR, rightGrip, 1f);
            else RelaxArm(BoneId.UpperArmR, BoneId.LowerArmR, 1f, presentArm);

            // legs
            if (feetPlanted)
            {
                SolveLeg(BoneId.UpperLegL, BoneId.LowerLegL, BoneId.FootL, leftFoot, kneel);
                SolveLeg(BoneId.UpperLegR, BoneId.LowerLegR, BoneId.FootR, rightFoot, kneel);
            }
        }

        private void SolveArm(BoneId upper, BoneId lower, BoneId hand, Vector3 palmTarget, float side)
        {
            var b = body;
            var u = b[upper];
            var l = b[lower];
            var h = b[hand];
            // wrist sits a palm behind the grip, on the side of the forearm
            var toBody = (u.position - palmTarget);
            var wristTarget = palmTarget + Vector3.ProjectOnPlane(toBody, bladeDir).normalized * palm;
            var pole = u.position + transform.TransformDirection(new Vector3(side * 0.5f, -0.8f, -0.2f));
            TwoBone(u, l, h, wristTarget, pole);
            // hand points from the wrist to the palm (wrapping the tsuka)
            var cur = HandTip(hand) - h.position;
            var want = palmTarget - h.position;
            if (cur.sqrMagnitude > 1e-6f && want.sqrMagnitude > 1e-6f) h.rotation = Quaternion.FromToRotation(cur, want) * h.rotation;
        }

        private Vector3 HandTip(BoneId hand)
        {
            // bones are created with the root's rotation, so the bind direction wrist → hand end is constant in bone space
            var h = body[hand];
            return h.position + h.rotation * KendokaBody.HandDirection(hand == BoneId.HandR) * 0.12f;
        }

        private void RelaxArm(BoneId upper, BoneId lower, float side, float present)
        {
            // from the A-pose: lower the arm to the side; "present" lifts the forearm forward, palm up (mock-up pose)
            var u = body[upper];
            var down = Quaternion.AngleAxis(side * 50f, transform.forward);
            u.rotation = down * u.rotation;
            if (present > 0f)
            {
                var l = body[lower];
                u.rotation = Quaternion.AngleAxis(-35f * present, transform.right) * Quaternion.AngleAxis(-side * 20f * present, transform.forward) * u.rotation;
                l.rotation = Quaternion.AngleAxis(-60f * present, transform.right) * l.rotation;
            }
        }

        private void SolveLeg(BoneId upper, BoneId lower, BoneId foot, Vector3 ground, float kneelAmount)
        {
            var b = body;
            var u = b[upper];
            var l = b[lower];
            var f = b[foot];
            var ankle = ground + Vector3.up * (0.09f * body.scale);
            var pole = u.position + transform.forward * 0.6f + Vector3.down * 0.2f;
            if (kneelAmount > 0f) ankle = Vector3.Lerp(ankle, ground + Vector3.up * 0.05f - transform.forward * 0.35f * body.scale, kneelAmount);
            TwoBone(u, l, f, ankle, pole);
            f.rotation = transform.rotation * body.bindLocal[(int)foot];
        }

        /// <summary>Analytic two-bone IK: places the end of the chain at the target, bending toward the pole.</summary>
        public static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            float lab = Vector3.Distance(a.position, b.position);
            float lbc = Vector3.Distance(b.position, c.position);
            var A = a.position;
            var toT = target - A;
            float d = Mathf.Clamp(toT.magnitude, 1e-4f, lab + lbc - 1e-4f);
            var dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : a.forward;
            var p = pole - A;
            var perp = (p - Vector3.Dot(p, dir) * dir);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();
            float cosA = Mathf.Clamp((lab * lab + d * d - lbc * lbc) / (2f * lab * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            var elbow = A + dir * (cosA * lab) + perp * (sinA * lab);
            var curAB = b.position - A;
            a.rotation = Quaternion.FromToRotation(curAB, elbow - A) * a.rotation;
            var end = A + dir * d;
            var curBC = c.position - b.position;
            b.rotation = Quaternion.FromToRotation(curBC, end - b.position) * b.rotation;
        }
    }
}
