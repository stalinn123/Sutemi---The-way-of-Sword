using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Places the supplied shinai prefab from <see cref="ShinaiMotion"/> (or from tracked hands in VR), stops it from
    /// passing through solid environment geometry, and records its trajectory for the strike sensor.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ShinaiRig : MonoBehaviour
    {
        public const float LeftGripAlong = 0.04f, RightGripAlong = 0.24f, Radius = 0.016f;
        public Transform shinai;          // instance of the supplied prefab; root = grip end, +Y = blade
        public Transform frame;           // fighter root the local poses are relative to
        public ShinaiMotion motion = new ShinaiMotion();
        public float length = 1.2f;
        public LayerMask environment;
        public bool externalPose;         // VR: pose set by SetWorldPose
        public bool Obstructed { get; private set; }
        public float LastObstructionTime { get; private set; } = -99f;
        public Vector3 LastObstructionPoint { get; private set; }

        public struct Sample { public float time; public Vector3 grip, tip, tsuru; public Quaternion rot; }
        private readonly Sample[] history = new Sample[48];
        private int head, count;

        public Vector3 Grip => shinai != null ? shinai.position : transform.position;
        public Vector3 BladeDir => shinai != null ? shinai.up : transform.forward;
        public Vector3 Tip => Grip + BladeDir * length;
        public Vector3 Tsuba => Grip + BladeDir * (0.29f * length / 1.2f);
        public Vector3 TsuruDir => shinai != null ? shinai.forward : Vector3.up;
        public Vector3 EdgeDir => -TsuruDir;
        public Vector3 LeftHandPoint => Grip + BladeDir * LeftGripAlong * length / 1.2f;
        public Vector3 RightHandPoint => Grip + BladeDir * RightGripAlong * length / 1.2f;
        public bool RightHandOn => !motion.InSageto;

        public void Init(Transform shinaiInstance, Transform fighterRoot, float len, LayerMask env)
        {
            shinai = shinaiInstance;
            frame = fighterRoot;
            length = len;
            environment = env;
            motion.length = len;
            foreach (var c in shinai.GetComponentsInChildren<Collider>()) c.enabled = false; // contact is computed, not physical
            foreach (var r in shinai.GetComponentsInChildren<Rigidbody>()) { r.isKinematic = true; r.detectCollisions = false; }
        }

        public void SetWorldPose(Vector3 grip, Quaternion rot)
        {
            if (shinai == null) return;
            shinai.SetPositionAndRotation(grip, rot);
            Resolve();
            Record();
        }

        /// <summary>Called by the fighter after the motion update.</summary>
        public void ApplyLocalPose(Pose local)
        {
            if (shinai == null || frame == null || externalPose) return;
            shinai.SetPositionAndRotation(frame.TransformPoint(local.position), frame.rotation * local.rotation);
            Resolve();
            Record();
        }

        /// <summary>If the blade would pass into walls or the ceiling, swing it away about the grip.</summary>
        private void Resolve()
        {
            Obstructed = false;
            if (environment.value == 0) return;
            var g = shinai.position;
            var dir = shinai.up;
            if (!Physics.SphereCast(g, Radius, dir, out var hit, length, environment, QueryTriggerInteraction.Ignore)) return;
            Obstructed = true;
            LastObstructionTime = Time.time;
            LastObstructionPoint = hit.point;
            var axis = Vector3.Cross(dir, hit.normal);
            if (axis.sqrMagnitude < 1e-6f) axis = frame != null ? frame.right : Vector3.right;
            axis.Normalize();
            for (int k = 1; k <= 9; k++)
            {
                var rot = Quaternion.AngleAxis(k * 10f, axis);
                var d2 = rot * dir;
                if (!Physics.SphereCast(g, Radius, d2, out _, length, environment, QueryTriggerInteraction.Ignore))
                {
                    shinai.rotation = rot * shinai.rotation;
                    return;
                }
            }
        }

        private void Record()
        {
            history[head] = new Sample { time = Time.time, grip = Grip, tip = Tip, tsuru = TsuruDir, rot = shinai.rotation };
            head = (head + 1) % history.Length;
            if (count < history.Length) count++;
        }

        public int SampleCount => count;

        /// <summary>k = 0 newest.</summary>
        public Sample Get(int k)
        {
            int i = (head - 1 - k + history.Length * 2) % history.Length;
            return history[i];
        }

        public Vector3 TipVelocity()
        {
            if (count < 2) return Vector3.zero;
            var a = Get(0);
            var b = Get(1);
            float dt = Mathf.Max(1e-4f, a.time - b.time);
            return (a.tip - b.tip) / dt;
        }

        /// <summary>How far the kensen rose above <paramref name="height"/> during the last <paramref name="seconds"/> (furikaburi evidence).</summary>
        public float RiseAbove(float height, float seconds)
        {
            float best = 0f;
            float now = Time.time;
            for (int k = 0; k < count; k++)
            {
                var s = Get(k);
                if (now - s.time > seconds) break;
                best = Mathf.Max(best, s.tip.y - height);
            }
            return best;
        }

        /// <summary>How far the grip moved forward along <paramref name="dir"/> during the last <paramref name="seconds"/> (thrust evidence).</summary>
        public float ThrustAlong(Vector3 dir, float seconds)
        {
            float now = Time.time;
            var latest = Get(0).grip;
            float best = 0f;
            for (int k = 1; k < count; k++)
            {
                var s = Get(k);
                if (now - s.time > seconds) break;
                best = Mathf.Max(best, Vector3.Dot(latest - s.grip, dir));
            }
            return best;
        }

        public float PeakTipSpeed(float seconds)
        {
            float now = Time.time, best = 0f;
            for (int k = 0; k + 1 < count; k++)
            {
                var a = Get(k);
                var b = Get(k + 1);
                if (now - b.time > seconds) break;
                float dt = a.time - b.time;
                if (dt > 1e-4f) best = Mathf.Max(best, (a.tip - b.tip).magnitude / dt);
            }
            return best;
        }
    }
}
