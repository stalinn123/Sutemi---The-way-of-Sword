using System.Collections.Generic;
using Kendo.AI;
using UnityEngine;

namespace Kendo.Game
{
    public enum StepDir { None, Forward, Back, Left, Right }

    public struct StepRecord
    {
        public StepDir dir;
        public float startTime;
        public bool okuriAshi;   // the rear foot was drawn up before the next step began
        public bool crossed;     // reversed sideways before the feet were together
    }

    /// <summary>
    /// Suriashi with okuri-ashi: the leading foot slides, the rear foot follows, then a short settle. Distances and
    /// times are KendoTimings (same as the duel simulator). Starting a new step during the settle is allowed (real
    /// players do it) but that step's predecessor is recorded as *not* okuri-ashi – the lesson evaluates it.
    /// Foot positions are exposed for the IK feet and the footprint guides.
    /// </summary>
    public sealed class FootworkModel
    {
        public float scale = 1f;
        public float pace = 1f;
        public bool Stepping => phase == 1;
        public bool Settling => phase == 2;
        public StepDir Dir { get; private set; }
        public float StepT { get; private set; }     // 0..1 within the moving part
        public float LastFrontFootLanding { get; private set; } = -99f;
        public float LastForwardStepStart { get; private set; } = -99f;
        public readonly List<StepRecord> log = new List<StepRecord>();

        private int phase;     // 0 idle, 1 moving, 2 settle
        private float t, moveDur, settleDur;
        private Vector3 delta, moved;
        private bool frontLandedThisStep;

        public float StepDuration => (float)KendoTimings.StepSeconds / pace;

        public static Vector3 Displacement(StepDir d, float scale)
        {
            switch (d)
            {
                case StepDir.Forward: return Vector3.forward * (float)KendoTimings.StepForward * scale;
                case StepDir.Back: return Vector3.back * (float)KendoTimings.StepBack * scale;
                case StepDir.Left: return Vector3.left * (float)KendoTimings.StepSide * scale;
                case StepDir.Right: return Vector3.right * (float)KendoTimings.StepSide * scale;
                default: return Vector3.zero;
            }
        }

        /// <summary>Requests a step. Rejected while the feet are moving; accepted (but marked) during the settle.</summary>
        public bool Step(StepDir d, float now, float lengthScale = 1f)
        {
            if (d == StepDir.None || phase == 1) return false;
            if (phase == 2 && log.Count > 0)
            {
                var prev = log[log.Count - 1];
                prev.okuriAshi = false;
                if ((prev.dir == StepDir.Left && d == StepDir.Right) || (prev.dir == StepDir.Right && d == StepDir.Left)) prev.crossed = true;
                log[log.Count - 1] = prev;
            }
            Dir = d;
            phase = 1;
            t = 0f;
            moveDur = StepDuration;
            settleDur = (float)KendoTimings.SettleSeconds / pace;
            delta = Displacement(d, scale) * lengthScale;
            moved = Vector3.zero;
            frontLandedThisStep = false;
            if (d == StepDir.Forward) LastForwardStepStart = now;
            log.Add(new StepRecord { dir = d, startTime = now, okuriAshi = true });
            if (log.Count > 64) log.RemoveAt(0);
            return true;
        }

        /// <summary>Advances; returns the local root displacement for this frame.</summary>
        public Vector3 Update(float dt, float now)
        {
            if (phase == 0) return Vector3.zero;
            t += dt;
            if (phase == 1)
            {
                StepT = Mathf.Clamp01(t / moveDur);
                // ease-in-out slide
                float e = StepT * StepT * (3f - 2f * StepT);
                var target = delta * e;
                var d = target - moved;
                moved = target;
                if (!frontLandedThisStep && StepT >= 0.6f)
                {
                    frontLandedThisStep = true;
                    if (Dir == StepDir.Forward) LastFrontFootLanding = now;
                }
                if (t >= moveDur) { phase = 2; t = 0f; }
                return d;
            }
            if (t >= settleDur) { phase = 0; Dir = StepDir.None; }
            return Vector3.zero;
        }

        public void Cancel() { phase = 0; Dir = StepDir.None; }

        /// <summary>Feet in the local frame: right foot leads (chudan), the moving foot slides first.</summary>
        public void Feet(out Vector3 left, out Vector3 right)
        {
            var baseR = new Vector3(0.07f, 0f, 0.10f) * scale;
            var baseL = new Vector3(-0.07f, 0f, -0.12f) * scale;
            left = baseL;
            right = baseR;
            if (phase != 1) return;
            // offsets relative to the root, which moves with the average of both feet
            float lead = Mathf.Clamp01(StepT / 0.6f), follow = Mathf.Clamp01((StepT - 0.4f) / 0.6f);
            var full = delta;
            var rootNow = delta * (StepT * StepT * (3f - 2f * StepT));
            bool rightLeads = Dir == StepDir.Forward || Dir == StepDir.Right;
            if (rightLeads) { right = baseR + full * lead - rootNow; left = baseL + full * follow - rootNow; }
            else { left = baseL + full * lead - rootNow; right = baseR + full * follow - rootNow; }
        }

        public void ClearLog() => log.Clear();
    }
}
