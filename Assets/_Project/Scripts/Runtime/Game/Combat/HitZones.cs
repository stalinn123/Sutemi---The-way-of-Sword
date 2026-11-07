using System.Collections.Generic;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>One datotsu-bui volume: a capsule (or sphere when a == b) attached to a transform.</summary>
    public struct HitZone
    {
        public Target target;
        public Transform anchor;
        public Vector3 a, b;     // local to the anchor
        public float radius;

        public Vector3 A => anchor.TransformPoint(a);
        public Vector3 B => anchor.TransformPoint(b);
        public Vector3 Center => (A + B) * 0.5f;
    }

    /// <summary>Anything the shinai can strike: a fighter, the bogu stand or the master as motodachi.</summary>
    public interface IStrikeTarget
    {
        Transform Root { get; }
        IList<HitZone> Zones { get; }
        Kamae CurrentKamae { get; }
        Opportunity OpportunityNow();
        string TargetName { get; }
    }

    public static class HitGeometry
    {
        /// <summary>Closest points between segments p1-q1 and p2-q2 (Ericson, Real-Time Collision Detection 5.1.9).</summary>
        public static float SegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            if (a <= 1e-9f && e <= 1e-9f) { s = t = 0f; c1 = p1; c2 = p2; return (c1 - c2).magnitude; }
            if (a <= 1e-9f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-9f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).magnitude;
        }

        /// <summary>Standard zones for a rigged kendoka (wearing men, kote, do).</summary>
        public static List<HitZone> ForKendoka(KendokaBody body)
        {
            float s = body.scale;
            var z = new List<HitZone>
            {
                new HitZone { target = Target.Men, anchor = body[BoneId.Head], a = new Vector3(0f, 0.13f, 0.02f) * s, b = new Vector3(0f, 0.13f, 0.02f) * s, radius = 0.115f * s },
                new HitZone { target = Target.Tsuki, anchor = body[BoneId.Neck], a = new Vector3(0f, -0.02f, 0.11f) * s, b = new Vector3(0f, 0.04f, 0.12f) * s, radius = 0.045f * s },
                new HitZone { target = Target.KoteRight, anchor = body[BoneId.LowerArmR], a = Vector3.zero, b = Vector3.zero, radius = 0.065f * s },
                new HitZone { target = Target.KoteLeft, anchor = body[BoneId.LowerArmL], a = Vector3.zero, b = Vector3.zero, radius = 0.065f * s },
                new HitZone { target = Target.DoRight, anchor = body[BoneId.Chest], a = new Vector3(0.14f, -0.22f, 0.10f) * s, b = new Vector3(0.12f, -0.08f, 0.12f) * s, radius = 0.09f * s },
                new HitZone { target = Target.DoLeft, anchor = body[BoneId.Chest], a = new Vector3(-0.14f, -0.22f, 0.10f) * s, b = new Vector3(-0.12f, -0.08f, 0.12f) * s, radius = 0.09f * s },
            };
            // kote: the forearm between 55 % and 95 % toward the wrist (set after the bones exist)
            for (int i = 0; i < z.Count; i++)
            {
                var h = z[i];
                if (h.target == Target.KoteRight || h.target == Target.KoteLeft)
                {
                    var hand = body[h.target == Target.KoteRight ? BoneId.HandR : BoneId.HandL];
                    var wristLocal = h.anchor.InverseTransformPoint(hand.position);
                    h.a = wristLocal * 0.55f;
                    h.b = wristLocal * 0.95f;
                    z[i] = h;
                }
            }
            return z;
        }
    }
}
