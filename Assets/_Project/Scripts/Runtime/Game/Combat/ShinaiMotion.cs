using Kendo.AI;
using UnityEngine;

namespace Kendo.Game
{
    public enum ShinaiMode { Chudan, Sageto, Strike, Block, Stunned }
    public enum StrikePhase { None, Raise, Cut, Follow, Return }

    /// <summary>
    /// Shinai choreography in the fighter's local frame (feet at origin, +Z forward). Shared by the player and every
    /// AI fighter so both strike with the same timings (KendoTimings). Poses: grip = left-hand end of the tsuka,
    /// rotation with +Y along the blade and +Z toward the tsuru (the cutting edge is −Z).
    /// The furikaburi height adapts to the ceiling; strikes aim the monouchi at the target point.
    /// </summary>
    public sealed class ShinaiMotion
    {
        public float scale = 1f;            // body height / 1.75 m
        public float length = 1.2f;
        public float speedScale = 1f;
        public float ceilingLocal = 99f;    // ceiling height above the feet
        public bool smallFurikaburi;
        public Vector2 aim;                 // fine aim (deg): x = yaw, y = pitch
        public float lookPitch;             // head pitch (deg, + = down) – tilts the kensen half as much
        public float rollNoiseDeg;          // technique imperfection (AI) applied to the cut
        public ShinaiMode Mode { get; private set; } = ShinaiMode.Chudan;
        public StrikePhase Phase { get; private set; }
        public DuelAction Strike { get; private set; }
        public float PhaseTime { get; private set; }
        public float StrikeTime { get; private set; }  // since the strike started
        public bool HighBlock { get; private set; }
        public bool ImpactPlanned => Mode == ShinaiMode.Strike && (Phase == StrikePhase.Raise || Phase == StrikePhase.Cut);
        public float PlannedImpactTime => (float)((KendoTimings.Raise(Strike) + KendoTimings.Strike(Strike)) / speedScale);
        public Pose Current { get; private set; }

        private Pose from, raise, impact, follow;
        private Vector3 target;
        private bool sageto;
        private float stunTimer, blockHeldTime;

        public bool InChudan => Mode == ShinaiMode.Chudan && !sageto;
        public bool InSageto => Mode == ShinaiMode.Chudan && sageto;
        public bool CanStrike => Mode == ShinaiMode.Chudan && !sageto || Mode == ShinaiMode.Block;
        public bool Busy => Mode == ShinaiMode.Strike || Mode == ShinaiMode.Stunned;

        public ShinaiMotion() { Current = Chudan(); }

        // ------------------------------------------------------------------ key poses

        private Vector3 S(float x, float y, float z) => new Vector3(x * scale, y * scale, z * scale);

        public Vector3 ThroatPoint => S(0f, 1.45f, 1.9f);

        public Pose Chudan()
        {
            var grip = S(0f, 1.02f, 0.22f);
            var dir = (ThroatPoint - grip).normalized;
            dir = Quaternion.Euler(-(aim.y) + lookPitch * 0.5f, aim.x, 0f) * dir;
            return Make(grip, dir, Vector3.up);
        }

        public Pose Sageto()
        {
            var grip = S(-0.21f, 0.93f, 0.10f);
            var dir = new Vector3(0.12f, -0.55f, 0.83f).normalized;
            return Make(grip, dir, Vector3.down);
        }

        /// <summary>Builds a pose: +Y along <paramref name="blade"/>, +Z (tsuru) toward <paramref name="tsuruHint"/>.</summary>
        public static Pose Make(Vector3 grip, Vector3 blade, Vector3 tsuruHint)
        {
            blade.Normalize();
            var z = Vector3.ProjectOnPlane(tsuruHint, blade);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.forward, blade);
            return new Pose(grip, Quaternion.LookRotation(z.normalized, blade));
        }

        private Pose RaisePose(DuelAction a)
        {
            float pitchDeg;
            Vector3 grip;
            switch (a)
            {
                case DuelAction.Kote: grip = S(0f, 1.42f, 0.30f); pitchDeg = 55f; break;
                case DuelAction.Do: grip = S(0.04f, 1.68f, 0.14f); pitchDeg = 92f; break;
                case DuelAction.Tsuki: grip = S(0f, 1.0f, 0.12f); pitchDeg = 12f; break;
                default: grip = S(0f, 1.72f, 0.12f); pitchDeg = 100f; break;
            }
            if (smallFurikaburi && a != DuelAction.Tsuki) { grip = S(0f, 1.45f, 0.26f); pitchDeg = Mathf.Min(pitchDeg, 60f); }
            // ceiling: keep the kensen below the ceiling (temple hall: 2.58 m)
            float limit = ceilingLocal - 0.10f;
            float tipY = grip.y + length * Mathf.Sin(Mathf.Min(pitchDeg, 90f) * Mathf.Deg2Rad);
            if (tipY > limit)
            {
                grip.y = Mathf.Min(grip.y, Mathf.Max(1.30f * scale, limit - 0.45f * length));
                float s = Mathf.Clamp((limit - grip.y) / length, -0.2f, 1f);
                pitchDeg = Mathf.Min(pitchDeg, Mathf.Asin(s) * Mathf.Rad2Deg);
            }
            var dir = Quaternion.AngleAxis(-pitchDeg, Vector3.right) * Vector3.forward;
            if (a == DuelAction.Do) dir = Quaternion.AngleAxis(20f, Vector3.up) * dir;
            return Make(grip, dir, Vector3.back);
        }

        private Pose ImpactPose(DuelAction a, Vector3 tgt, Pose raised)
        {
            Vector3 anchor;
            switch (a)
            {
                case DuelAction.Kote: anchor = S(0f, 1.15f, 0.30f); break;
                case DuelAction.Do: anchor = S(0.02f, 1.18f, 0.26f); break;
                case DuelAction.Tsuki: anchor = S(0f, 1.12f, 0.36f); break;
                default: anchor = S(0f, 1.32f, 0.26f); break;
            }
            var toT = tgt - anchor;
            var dirA = toT.sqrMagnitude > 1e-6f ? toT.normalized : Vector3.forward;
            var grip = tgt - dirA * (0.85f * length);
            var off = grip - anchor;
            float reach = 0.38f * scale;
            if (off.magnitude > reach) grip = anchor + off.normalized * reach;
            if (grip.z < anchor.z - 0.12f * scale) grip.z = anchor.z - 0.12f * scale;
            var blade = (tgt - grip).sqrMagnitude > 1e-6f ? (tgt - grip).normalized : Vector3.forward;
            Vector3 tsuru;
            if (a == DuelAction.Tsuki) tsuru = Vector3.up;
            else
            {
                var raisedTip = raised.position + raised.rotation * Vector3.up * length;
                var impactTip = grip + blade * length;
                tsuru = raisedTip - impactTip;
            }
            var p = Make(grip, blade, tsuru);
            if (Mathf.Abs(rollNoiseDeg) > 0.01f) p.rotation = Quaternion.AngleAxis(rollNoiseDeg, blade) * p.rotation;
            return p;
        }

        private Pose FollowPose(DuelAction a, Pose hit)
        {
            var blade = hit.rotation * Vector3.up;
            var edge = hit.rotation * Vector3.back;
            var b2 = Quaternion.AngleAxis(a == DuelAction.Tsuki ? 0f : 12f, Vector3.Cross(blade, edge)) * blade;
            return new Pose(hit.position + Vector3.forward * 0.05f * scale + (a == DuelAction.Tsuki ? Vector3.forward * 0.05f : Vector3.zero),
                Quaternion.LookRotation(Vector3.ProjectOnPlane(hit.rotation * Vector3.forward, b2).normalized, b2));
        }

        private Pose BlockPose(bool high)
        {
            if (high) return Make(S(0.06f, 1.50f, 0.30f), new Vector3(0.55f, 0.55f, 0.62f), Vector3.back);
            return Make(S(0.10f, 1.05f, 0.28f), new Vector3(0.35f, -0.62f, 0.70f), Vector3.back + Vector3.left);
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Chudan ↔ sageto (X). Only from a settled stance.</summary>
        public bool ToggleSageto()
        {
            if (Mode != ShinaiMode.Chudan) return false;
            sageto = !sageto;
            from = Current;
            PhaseTime = 0f;
            return true;
        }

        public void ForceSageto(bool on) { if (Mode == ShinaiMode.Chudan) { sageto = on; from = Current; PhaseTime = 0f; } }

        public bool StartStrike(DuelAction a, Vector3 targetLocal)
        {
            if (!KendoTimings.IsStrike(a) || !CanStrike) return false;
            Mode = ShinaiMode.Strike;
            Strike = a;
            Phase = StrikePhase.Raise;
            PhaseTime = 0f;
            StrikeTime = 0f;
            target = targetLocal;
            from = Current;
            raise = RaisePose(a);
            impact = ImpactPose(a, target, raise);
            follow = FollowPose(a, impact);
            return true;
        }

        /// <summary>Updates the strike's aim while raising (the cut follows the eyes / opponent movement).</summary>
        public void Retarget(Vector3 targetLocal)
        {
            if (Mode != ShinaiMode.Strike || Phase == StrikePhase.Follow || Phase == StrikePhase.Return) return;
            target = targetLocal;
            impact = ImpactPose(Strike, target, raise);
            follow = FollowPose(Strike, impact);
        }

        public void SetBlock(bool held, bool high)
        {
            if (held && (Mode == ShinaiMode.Chudan && !sageto))
            {
                Mode = ShinaiMode.Block;
                HighBlock = high;
                from = Current;
                PhaseTime = 0f;
                blockHeldTime = 0f;
            }
            else if (!held && Mode == ShinaiMode.Block)
            {
                Mode = ShinaiMode.Chudan;
                from = Current;
                PhaseTime = 0f;
            }
            else if (held && Mode == ShinaiMode.Block) HighBlock = high;
        }

        public float BlockTime => Mode == ShinaiMode.Block ? blockHeldTime : 0f;
        public bool BlockRaised => Mode == ShinaiMode.Block && blockHeldTime >= (float)KendoTimings.BlockRaise;

        /// <summary>The strike was deflected: kuzushi, short stun, back to kamae.</summary>
        public void Deflected()
        {
            Mode = ShinaiMode.Stunned;
            Phase = StrikePhase.None;
            from = Current;
            stunTimer = (float)KendoTimings.StunSeconds;
            PhaseTime = 0f;
        }

        /// <summary>Holds the follow-through longer (zanshin) or skips it (AI technique / player choice).</summary>
        public float followHold = (float)KendoTimings.FollowSeconds;

        // ------------------------------------------------------------------ update

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static Pose Lerp(Pose a, Pose b, float t) => new Pose(Vector3.LerpUnclamped(a.position, b.position, t), Quaternion.SlerpUnclamped(a.rotation, b.rotation, t));

        public Pose Update(float dt)
        {
            PhaseTime += dt;
            switch (Mode)
            {
                case ShinaiMode.Chudan:
                {
                    var goal = sageto ? Sageto() : Chudan();
                    float t = Mathf.Clamp01(PhaseTime / 0.35f);
                    Current = t >= 1f ? goal : Lerp(from, goal, Smooth(t));
                    break;
                }
                case ShinaiMode.Block:
                {
                    blockHeldTime += dt;
                    var goal = BlockPose(HighBlock);
                    float t = Mathf.Clamp01(PhaseTime / (float)KendoTimings.BlockRaise);
                    Current = Lerp(from, goal, Smooth(t));
                    break;
                }
                case ShinaiMode.Stunned:
                {
                    stunTimer -= dt;
                    var goal = Chudan();
                    var knock = new Pose(goal.position + Vector3.up * 0.05f, Quaternion.AngleAxis(-25f, Vector3.right) * goal.rotation);
                    float t = Mathf.Clamp01(PhaseTime / (float)KendoTimings.StunSeconds);
                    Current = t < 0.4f ? Lerp(from, knock, Smooth(t / 0.4f)) : Lerp(knock, goal, Smooth((t - 0.4f) / 0.6f));
                    if (stunTimer <= 0f) { Mode = ShinaiMode.Chudan; from = Current; PhaseTime = 0f; }
                    break;
                }
                case ShinaiMode.Strike:
                    StrikeTime += dt;
                    UpdateStrike();
                    break;
            }
            return Current;
        }

        private void UpdateStrike()
        {
            float raiseT = (float)(KendoTimings.Raise(Strike) / speedScale);
            float cutT = (float)(KendoTimings.Strike(Strike) / speedScale);
            float returnT = (float)KendoTimings.ReturnSeconds;
            switch (Phase)
            {
                case StrikePhase.Raise:
                    Current = Lerp(from, raise, Smooth(Mathf.Clamp01(PhaseTime / raiseT)));
                    if (PhaseTime >= raiseT) { Phase = StrikePhase.Cut; PhaseTime -= raiseT; }
                    break;
                case StrikePhase.Cut:
                {
                    float t = Mathf.Clamp01(PhaseTime / cutT);
                    Current = Lerp(raise, impact, t * (0.6f + 0.4f * t)); // accelerating cut: fastest at impact
                    if (PhaseTime >= cutT) { Phase = StrikePhase.Follow; PhaseTime -= cutT; }
                    break;
                }
                case StrikePhase.Follow:
                    Current = Lerp(impact, follow, Smooth(Mathf.Clamp01(PhaseTime / Mathf.Max(0.05f, followHold))));
                    if (PhaseTime >= followHold) { Phase = StrikePhase.Return; PhaseTime = 0f; from = Current; }
                    break;
                case StrikePhase.Return:
                    Current = Lerp(from, Chudan(), Smooth(Mathf.Clamp01(PhaseTime / returnT)));
                    if (PhaseTime >= returnT) { Mode = ShinaiMode.Chudan; Phase = StrikePhase.None; from = Current; PhaseTime = 1f; }
                    break;
            }
        }

        /// <summary>Equivalent duel phase for observations (Agents 2 and 3).</summary>
        public DuelPhase AsDuelPhase(bool stepping)
        {
            switch (Mode)
            {
                case ShinaiMode.Strike: return Phase == StrikePhase.Return ? DuelPhase.Recovering : DuelPhase.Striking;
                case ShinaiMode.Block: return DuelPhase.Blocking;
                case ShinaiMode.Stunned: return DuelPhase.Stunned;
                default: return stepping ? DuelPhase.Stepping : DuelPhase.Ready;
            }
        }

        public float PhaseProgress
        {
            get
            {
                if (Mode != ShinaiMode.Strike) return Mode == ShinaiMode.Stunned ? 1f - stunTimer / (float)KendoTimings.StunSeconds : 0f;
                float total = PlannedImpactTime + followHold;
                return Mathf.Clamp01(StrikeTime / total);
            }
        }
    }
}
