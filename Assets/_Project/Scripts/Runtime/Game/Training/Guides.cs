using System.Collections.Generic;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Visual indications of the correct movement: footprints for the stance, a ring on the opponent's throat for the
    /// kensen, highlighted target zones, numbered floor marks for suriashi, a trail behind the master's kensen during
    /// demonstrations. All generated meshes; removed with <see cref="Clear"/>.
    /// </summary>
    public sealed class Guides : MonoBehaviour
    {
        private readonly List<GameObject> items = new List<GameObject>();
        private readonly List<(Transform t, Vector3 baseScale, float phase)> pulsing = new List<(Transform, Vector3, float)>();
        private Material gold, goldSoft, good, bad;
        private GameObject ring;
        private TrailRenderer trail;

        private void Awake()
        {
            gold = ProceduralMeshes.Unlit("Guide_Gold", new Color(0.906f, 0.706f, 0.482f, 0.85f));
            goldSoft = ProceduralMeshes.Unlit("Guide_GoldSoft", new Color(0.906f, 0.706f, 0.482f, 0.35f));
            good = ProceduralMeshes.Unlit("Guide_Good", new Color(0.55f, 0.9f, 0.55f, 0.85f));
            bad = ProceduralMeshes.Unlit("Guide_Bad", new Color(0.95f, 0.45f, 0.4f, 0.85f));
        }

        public void Clear()
        {
            foreach (var g in items) if (g != null) Destroy(g);
            items.Clear();
            pulsing.Clear();
            ring = null;
            if (trail != null) { Destroy(trail); trail = null; }
        }

        public void Footprints(Vector3 rootPos, float yaw, float scale = 1f)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var r = ProceduralMeshes.Spawn("Footprint.R", ProceduralMeshes.Footprint(), gold, transform);
            r.transform.SetPositionAndRotation(rootPos + rot * new Vector3(0.07f, 0.012f, 0.10f) * scale, rot);
            r.transform.localScale = new Vector3(-1f, 1f, 1f) * scale;
            var l = ProceduralMeshes.Spawn("Footprint.L", ProceduralMeshes.Footprint(), gold, transform);
            l.transform.SetPositionAndRotation(rootPos + rot * new Vector3(-0.07f, 0.012f, -0.12f) * scale, rot);
            l.transform.localScale = Vector3.one * scale;
            items.Add(r);
            items.Add(l);
        }

        public void KensenRing(Vector3 at, Vector3 facing)
        {
            ring = ProceduralMeshes.Spawn("KensenRing", ProceduralMeshes.Ring(), gold, transform);
            ring.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing));
            items.Add(ring);
        }

        public void RingState(bool onTarget)
        {
            if (ring == null) return;
            ring.GetComponent<MeshRenderer>().sharedMaterial = onTarget ? good : gold;
        }

        public void HighlightZone(Vector3 at, float radius, Vector3 towardViewer)
        {
            var g = ProceduralMeshes.Spawn("ZoneHighlight", ProceduralMeshes.Ring(), gold, transform);
            g.transform.SetPositionAndRotation(at, Quaternion.LookRotation(towardViewer));
            g.transform.localScale = Vector3.one * (radius / 0.07f) * 1.15f;
            items.Add(g);
            pulsing.Add((g.transform, g.transform.localScale, Random.value));
        }

        public void FloorMark(Vector3 at, float size, bool soft)
        {
            var g = ProceduralMeshes.Spawn("FloorMark", ProceduralMeshes.Disc(), soft ? goldSoft : gold, transform);
            g.transform.position = at + Vector3.up * 0.011f;
            g.transform.localScale = new Vector3(size, 1f, size);
            items.Add(g);
        }

        public void Trail(Transform tip)
        {
            if (tip == null) return;
            trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.startWidth = 0.035f;
            trail.endWidth = 0.0f;
            trail.sharedMaterial = gold;
            trail.minVertexDistance = 0.02f;
        }

        public void Flash(Vector3 at, bool ok)
        {
            var g = ProceduralMeshes.Spawn("Flash", ProceduralMeshes.Ring(), ok ? good : bad, transform);
            g.transform.position = at;
            g.transform.rotation = Camera.main != null ? Quaternion.LookRotation(Camera.main.transform.position - at) : Quaternion.identity;
            g.transform.localScale = Vector3.one * 1.6f;
            Destroy(g, 0.6f);
        }

        private void Update()
        {
            foreach (var (t, b, p) in pulsing)
                if (t != null) t.localScale = b * (1f + 0.12f * Mathf.Sin((Time.time + p) * 5f));
        }
    }
}
