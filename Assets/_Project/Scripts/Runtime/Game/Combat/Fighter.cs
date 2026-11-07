using System;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// A kendoka in a bout or lesson – the player or an AI. Owns the shinai motion, footwork and hit zones and executes
    /// the shared commands (step, strike, block, kiai, kamae). Player and AI use exactly the same command path, motion
    /// timings and strike sensor, so the rules engine sees identical evidence for both.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class Fighter : MonoBehaviour, IStrikeTarget
    {
        public int side;                         // 0 = aka (player), 1 = shiro (opponent)
        public string displayName = "Fighter";
        public float height = 1.75f;
        public ShinaiRig rig;
        public FootworkModel footwork = new FootworkModel();
        public KendokaBody body;                 // null for the first-person player
        public KendokaPoser poser;
        public StrikeSensor sensor;
        public IStrikeTarget opponent;
        public Transform headPoint;              // eyes (player camera or head bone)
        public bool aiControlled;
        public Technique technique = Technique.Medium();
        public List<HitZone> zones = new List<HitZone>();
        /// <summary>Player: the cut follows the eyes (local aim point). Null = aim at the opponent's zone.</summary>
        public Func<DuelAction, Vector3?> aimOverride;

        // arena constraints for AI movement
        public Vector3 arenaCenter;
        public Vector2 arenaHalf = new Vector2(99, 99);
        public Quaternion arenaRotation = Quaternion.identity;
        public float separation = 0.65f;

        public float LastKiaiTime { get; private set; } = -99f;
        public float CounterUntil { get; set; } = -99f;
        public float StrikeStartTime { get; private set; } = -99f;
        public float StrikeStartDistance { get; private set; }
        public Opportunity StrikeOpportunity { get; private set; }
        public DuelAction LastCommand { get; private set; }
        public float LastCommandTime { get; private set; } = -99f;
        public bool Frozen;                      // between points / during dialogue
        public event Action<Fighter> Kiai;

        public ShinaiMotion Motion => rig.motion;
        public Transform Root => transform;
        public IList<HitZone> Zones => zones;
        public string TargetName => displayName;
        public Kamae CurrentKamae => Motion.InSageto ? Kamae.Sageto : Kamae.Chudan;
        public DuelPhase Phase => Motion.AsDuelPhase(footwork.Stepping || footwork.Settling);
        public bool CanAct => !Frozen && (Phase == DuelPhase.Ready || Phase == DuelPhase.Blocking) && !footwork.Stepping;
        public Vector3 Forward => transform.forward;

        public void Setup(ShinaiRig shinaiRig, float bodyHeight)
        {
            rig = shinaiRig;
            height = bodyHeight;
            footwork.scale = bodyHeight / 1.75f;
            rig.motion.scale = bodyHeight / 1.75f;
            sensor = gameObject.GetComponent<StrikeSensor>();
            if (sensor == null) sensor = gameObject.AddComponent<StrikeSensor>();
            sensor.owner = this;
        }

        // ------------------------------------------------------------------ commands (shared by player input and AI)

        public bool StepCommand(StepDir d, float lengthScale = 1f)
        {
            if (Frozen || Motion.Mode == ShinaiMode.Stunned) return false;
            bool ok = footwork.Step(d, Time.time, lengthScale);
            if (ok) Note(d == StepDir.Forward ? DuelAction.StepForward : d == StepDir.Back ? DuelAction.StepBack : d == StepDir.Left ? DuelAction.StepLeft : DuelAction.StepRight);
            return ok;
        }

        public bool StrikeCommand(DuelAction a)
        {
            if (Frozen || !Motion.CanStrike) return false;
            var local = (aimOverride != null ? aimOverride(a) : null) ?? TargetPointLocal(a);
            if (aiControlled) PrepareAiStrike(0);
            if (!Motion.StartStrike(a, local)) return false;
            if (aiControlled && pendingKiai) scheduledKiai = Time.time + Motion.PlannedImpactTime + pendingKiaiOffset;
            StrikeStartTime = Time.time;
            StrikeStartDistance = opponent != null ? HorizontalDistance(opponent.Root.position) : 0f;
            StrikeOpportunity = opponent != null ? opponent.OpportunityNow() : Opportunity.None;
            if (Time.time < CounterUntil) StrikeOpportunity = Opportunity.AfterBlock;
            sensor?.BeginStrike(a);
            Note(a);
            return true;
        }

        public void BlockCommand(bool held, bool high)
        {
            if (Frozen) held = false;
            bool was = Motion.Mode == ShinaiMode.Block;
            Motion.SetBlock(held, high);
            if (!was && Motion.Mode == ShinaiMode.Block) Note(high ? DuelAction.BlockHigh : DuelAction.BlockDo);
        }

        public void KiaiCommand()
        {
            LastKiaiTime = Time.time;
            Kiai?.Invoke(this);
            var g = KendoGame.Instance;
            if (g != null && g.Audio != null)
            {
                var c = g.Config;
                var clip = !aiControlled ? c.kiaiPlayer : (technique.speed >= 9.9 ? c.kiaiBoss : c.kiaiOpponent);
                g.Audio.Voice(clip, headPoint != null ? headPoint.position : transform.position + Vector3.up * 1.6f, aiControlled ? 0.95f : 1f);
            }
        }

        private float lastLandingHeard = -99f;

        private void FootSounds()
        {
            var g = KendoGame.Instance;
            if (g == null || g.Audio == null) return;
            float land = footwork.LastFrontFootLanding;
            if (land > lastLandingHeard)
            {
                lastLandingHeard = land;
                bool stamp = Motion.Mode == ShinaiMode.Strike || Time.time - LastKiaiTime < 0.3f;
                g.Audio.Sfx(stamp ? g.Config.fumikomi : g.Config.footSlide, transform.position, stamp ? 1f : 0.5f);
            }
        }

        public bool ToggleKamae() => !Frozen && Motion.ToggleSageto();

        private void Note(DuelAction a)
        {
            LastCommand = a;
            LastCommandTime = Time.time;
        }

        public float HorizontalDistance(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Aim point for a strike in the local frame: the opponent's zone if one exists, else straight ahead.</summary>
        public Vector3 TargetPointLocal(DuelAction a)
        {
            var t = KendoTimings.TargetOf(a);
            if (opponent != null)
            {
                foreach (var z in opponent.Zones)
                {
                    if (z.target != t) continue;
                    var c = z.Center;
                    var local = transform.InverseTransformPoint(c);
                    if (local.z > 0.3f) return local;
                }
            }
            float s = height / 1.75f;
            switch (a)
            {
                case DuelAction.Kote: return new Vector3(-0.12f, 1.10f, 1.75f) * s;
                case DuelAction.Do: return new Vector3(-0.18f, 1.05f, 1.6f) * s;
                case DuelAction.Tsuki: return new Vector3(0f, 1.40f, 1.75f) * s;
                default: return new Vector3(0f, 1.72f, 1.85f) * s;
            }
        }

        /// <summary>Opportunity this fighter presents to an attacker right now (datotsu no kikai).</summary>
        public Opportunity OpportunityNow()
        {
            switch (Phase)
            {
                case DuelPhase.Striking:
                    return Motion.StrikeTime < 0.12f ? Opportunity.Debana : (Motion.Phase == StrikePhase.Follow ? Opportunity.AfterOpponentStrike : Opportunity.None);
                case DuelPhase.Recovering: return Opportunity.AfterOpponentStrike;
                case DuelPhase.Stunned: return Opportunity.AfterBlock;
                case DuelPhase.Blocking: return Opportunity.IntoReadyKamae;
                case DuelPhase.Stepping:
                    if (footwork.Dir == StepDir.Forward && footwork.StepT < 0.5f) return Opportunity.Debana;
                    if (footwork.Dir == StepDir.Back) return Opportunity.OpponentRetreating;
                    return Opportunity.None;
                default:
                    return opponent != null && HorizontalDistance(opponent.Root.position) < 2.3f ? Opportunity.IntoReadyKamae : Opportunity.OpponentStatic;
            }
        }

        // ------------------------------------------------------------------ per-frame

        /// <summary>Updates motion and (for AI fighters) moves the root. The player's rig moves its own CharacterController.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (scheduledKiai > 0f && Time.time >= scheduledKiai) { scheduledKiai = -1f; KiaiCommand(); }
            if (aiControlled && !aiZanshin && Motion.Mode == ShinaiMode.Strike && Motion.Phase == StrikePhase.Return && dropGuardUntil < 0f)
                dropGuardUntil = Time.time + 1.0f;
            if (dropGuardUntil > 0f && Motion.Mode == ShinaiMode.Chudan)
            {
                if (Time.time < dropGuardUntil) { if (!Motion.InSageto) Motion.ForceSageto(true); }
                else { Motion.ForceSageto(false); dropGuardUntil = -1f; }
            }
            if (opponent != null && Motion.ImpactPlanned && Motion.Phase == StrikePhase.Raise)
                Motion.Retarget((aimOverride != null ? aimOverride(Motion.Strike) : null) ?? TargetPointLocal(Motion.Strike));
            var pose = Motion.Update(dt);
            if (aiControlled)
            {
                var d = footwork.Update(dt, Time.time);
                // fumikomi lunge during the strike (AI drive comes from its technique; the player steps with W)
                if (Motion.Mode == ShinaiMode.Strike && (Motion.Phase == StrikePhase.Raise || Motion.Phase == StrikePhase.Cut))
                {
                    float lunge = (float)(KendoTimings.Lunge * technique.drive) * dt / Motion.PlannedImpactTime;
                    d += Vector3.forward * lunge * footwork.scale;
                }
                else if (Motion.Mode == ShinaiMode.Strike && Motion.Phase == StrikePhase.Follow && aiZanshin)
                {
                    d += Vector3.forward * 0.5f * dt; // zanshin: move through
                }
                MoveAi(transform.TransformDirection(d));
                if (opponent != null) FaceTowards(opponent.Root.position, 240f * dt);
            }
            rig.ApplyLocalPose(pose);
            UpdatePoser();
            FootSounds();
        }

        public bool aiZanshin = true;
        public float aiFootOffset, aiTilt, playerPitch;
        private float scheduledKiai = -1f, dropGuardUntil = -1f;
        private KRandom techniqueRng = new KRandom(7);

        /// <summary>AI technique imperfections for the next strike, drawn from its <see cref="Technique"/> (real motion
        /// and timing changes – the sensor then measures them like the player's).</summary>
        public void PrepareAiStrike(long seed)
        {
            var r = techniqueRng;
            Motion.rollNoiseDeg = (float)(r.Gauss() * technique.hasujiSigma * 0.8);
            aiFootOffset = (float)(r.Gauss() * technique.timingSigma);
            aiTilt = (float)(System.Math.Abs(r.Gauss()) * technique.tiltSigma);
            aiZanshin = r.Value() < technique.zanshinProb;
            Motion.followHold = aiZanshin ? (float)KendoTimings.FollowSeconds : 0.12f;
            Motion.speedScale = (float)(technique.speed / 8.5);
            pendingKiai = r.Value() < technique.kiaiProb;
            pendingKiaiOffset = (float)(r.Gauss() * technique.timingSigma);
        }

        private bool pendingKiai;
        private float pendingKiaiOffset;

        public void SeedTechnique(long seed) { techniqueRng = new KRandom(seed); }

        private void MoveAi(Vector3 world)
        {
            var p = transform.position + world;
            if (opponent != null)
            {
                var o = opponent.Root.position;
                var d = p - o;
                d.y = 0f;
                if (d.magnitude < separation) p = o + (d.sqrMagnitude > 1e-6f ? d.normalized : -transform.forward) * separation + Vector3.up * (p.y - o.y);
            }
            // stay inside the arena (+ small margin so jogai is possible but rare)
            var local = Quaternion.Inverse(arenaRotation) * (p - arenaCenter);
            local.x = Mathf.Clamp(local.x, -arenaHalf.x - 0.3f, arenaHalf.x + 0.3f);
            local.z = Mathf.Clamp(local.z, -arenaHalf.y - 0.3f, arenaHalf.y + 0.3f);
            p = arenaCenter + arenaRotation * local;
            p.y = arenaCenter.y;
            transform.position = p;
        }

        public void FaceTowards(Vector3 target, float maxDeg)
        {
            var d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), maxDeg);
        }

        public bool OutsideArena(float margin = 0f)
        {
            var local = Quaternion.Inverse(arenaRotation) * (transform.position - arenaCenter);
            return Mathf.Abs(local.x) > arenaHalf.x + margin || Mathf.Abs(local.z) > arenaHalf.y + margin;
        }

        // kendoka body pose from the gameplay state
        public float bowDeg;
        public float kneel;
        public float presentArm;

        private void UpdatePoser()
        {
            if (poser == null) return;
            poser.leftGrip = rig.LeftHandPoint;
            poser.rightGrip = rig.RightHandPoint;
            poser.bladeDir = rig.BladeDir;
            poser.leftHandOnGrip = true;
            poser.rightHandOnGrip = rig.RightHandOn && presentArm <= 0f;
            poser.presentArm = presentArm;
            footwork.Feet(out var lf, out var rf);
            poser.leftFoot = transform.TransformPoint(lf);
            poser.rightFoot = transform.TransformPoint(rf);
            poser.bowDeg = bowDeg;
            poser.kneel = kneel;
            poser.leanDeg = Motion.Mode == ShinaiMode.Strike && Motion.Phase == StrikePhase.Cut ? 6f : 0f;
            poser.lookAt = opponent != null ? opponent.Root.position + Vector3.up * 1.55f : (Vector3?)null;
        }
    }
}
