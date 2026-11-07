using System.Collections.Generic;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// The dojo's own bogu display stand (part of the supplied TrainingDojo mesh) as a strike target. Zone positions
    /// were measured from the stand's men, do, throat flap and arm bar meshes (Docs/SceneLayout.md). The stand has no
    /// forearms, so its kote zone is the end of the arm bar – a documented approximation.
    /// </summary>
    public sealed class BoguStandTarget : MonoBehaviour, IStrikeTarget
    {
        private readonly List<HitZone> zones = new List<HitZone>();
        private readonly Dictionary<Target, Transform> markers = new Dictionary<Target, Transform>();
        public Transform Root => transform;
        public IList<HitZone> Zones => zones;
        public Kamae CurrentKamae => Kamae.Chudan;
        public Opportunity OpportunityNow() => Opportunity.OpponentStatic;
        public string TargetName => "bogu stand";

        public static BoguStandTarget Create(Vector3 standBase)
        {
            var go = new GameObject("BoguStandTarget");
            go.transform.position = standBase;
            var t = go.AddComponent<BoguStandTarget>();
            t.Add(Target.Men, new Vector3(0f, 1.40f, 0.04f), 0.13f);
            t.Add(Target.Tsuki, new Vector3(0f, 1.21f, 0.17f), 0.05f);
            t.Add(Target.DoRight, new Vector3(0.12f, 0.92f, 0.10f), 0.10f);
            t.Add(Target.DoLeft, new Vector3(-0.12f, 0.92f, 0.10f), 0.10f);
            t.Add(Target.KoteRight, new Vector3(0.33f, 1.00f, 0.0f), 0.07f);
            // the stand blocks the player's body
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.75f, 0f);
            col.height = 1.5f;
            col.radius = 0.22f;
            go.layer = Layers.Characters;
            return t;
        }

        private void Add(Target target, Vector3 local, float r)
        {
            var a = new GameObject("Zone_" + target).transform;
            a.SetParent(transform, false);
            a.localPosition = local;
            zones.Add(new HitZone { target = target, anchor = a, radius = r });
            markers[target] = a;
        }

        public Vector3 ZonePoint(Target t) => markers.TryGetValue(t, out var m) ? m.position : transform.position + Vector3.up * 1.4f;

        /// <summary>The zone the gaze ray points at (within its radius + 6 cm), for the "name the targets" exercise.</summary>
        public Target Gazed(Ray ray, float maxDistance = 4f)
        {
            Target best = Target.None;
            float bestD = float.MaxValue;
            foreach (var z in zones)
            {
                var c = z.Center;
                float along = Vector3.Dot(c - ray.origin, ray.direction);
                if (along < 0f || along > maxDistance) continue;
                float d = Vector3.Distance(ray.origin + ray.direction * along, c);
                if (d <= z.radius + 0.06f && d < bestD) { bestD = d; best = z.target; }
            }
            return best;
        }
    }
}
