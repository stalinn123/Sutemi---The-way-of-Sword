using System;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>A measured contact, before and after zanshin was observed.</summary>
    public sealed class StrikeReport
    {
        public Fighter attacker;
        public IStrikeTarget target;
        public StrikeEvidence evidence;
        public StrikeEvaluation evaluation;
        public float contactTime;
        public Vector3 contactPoint;
        public DuelAction intended;
        public bool blockedByShinai;
    }

    /// <summary>
    /// Turns the real shinai trajectory into <see cref="StrikeEvidence"/>: which zone the blade touched (swept test),
    /// where along the blade, which side, hasuji from the actual tip velocity, swing evidence (furikaburi rise or thrust
    /// extension), kiai and fumikomi timing, distance, body drive, posture, opportunity – then watches the attacker for
    /// zanshin and evaluates with <see cref="KendoEvaluationEngine"/>. A collision alone yields ContactOnly.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class StrikeSensor : MonoBehaviour
    {
        public Fighter owner;
        public readonly List<IStrikeTarget> targets = new List<IStrikeTarget>();
        public KendoRuleConfig rules = KendoRuleConfig.TrainingBout();
        public KendoEvaluationProfile profile = KendoEvaluationProfile.Beginner();
        public float zanshinWindow = 1.8f;
        public bool enabledSensing = true;
        /// <summary>Raised when the contact happens (evaluation without zanshin) – used for sounds and blocks.</summary>
        public event Action<StrikeReport> Contact;
        /// <summary>Raised after the zanshin window with the final evaluation.</summary>
        public event Action<StrikeReport> Evaluated;
        /// <summary>Raised when a deliberate strike ended without touching anything.</summary>
        public event Action<StrikeReport> Missed;
        /// <summary>Optional: the opponent's shinai, for blade-on-blade blocks.</summary>
        public Func<ShinaiRig> opposingShinai;
        public Func<bool> opposingBlockRaised;

        private ShinaiRig.Sample prev;
        private bool havePrev;
        private StrikeReport pending;
        private float pendingStart;
        private Vector3 contactRootPos, targetPosAtContact;
        private float maxFollow;
        private float restoredAt = -1f;
        private float awayTime;
        private DuelAction currentStrike;
        private bool strikeContacted;
        private float lastContactTime = -99f;
        private float touchCooldown;

        public bool Pending => pending != null;

        public void BeginStrike(DuelAction a)
        {
            currentStrike = a;
            strikeContacted = false;
        }

        private void LateUpdate()
        {
            if (owner == null || owner.rig == null || owner.rig.shinai == null) return;
            var rig = owner.rig;
            var cur = new ShinaiRig.Sample { time = Time.time, grip = rig.Grip, tip = rig.Tip, tsuru = rig.TsuruDir, rot = rig.shinai.rotation };
            touchCooldown -= Time.deltaTime;
            if (enabledSensing && havePrev && pending == null) DetectContact(prev, cur);
            if (pending != null) TrackZanshin();
            // a strike that finished its cut without contact is a miss (evaluated as such, never a point)
            if (owner.Motion.Mode == ShinaiMode.Strike && owner.Motion.Phase == StrikePhase.Return && currentStrike != DuelAction.Wait && !strikeContacted)
            {
                var r = new StrikeReport { attacker = owner, intended = currentStrike, evidence = new StrikeEvidence { contact = false, target = KendoTimings.TargetOf(currentStrike) } };
                r.evaluation = KendoEvaluationEngine.EvaluateStrike(r.evidence, rules, profile);
                currentStrike = DuelAction.Wait;
                Missed?.Invoke(r);
            }
            prev = cur;
            havePrev = true;
        }

        private void DetectContact(ShinaiRig.Sample a, ShinaiRig.Sample b)
        {
            float L = owner.rig.length;
            const int sub = 5;
            for (int k = 1; k <= sub; k++)
            {
                float u = k / (float)sub;
                var grip = Vector3.Lerp(a.grip, b.grip, u);
                var tip = Vector3.Lerp(a.tip, b.tip, u);
                var tsubaPt = grip + (tip - grip).normalized * 0.29f * L / 1.2f;
                // blade against blade (block / deflection)
                if (opposingShinai != null && owner.Motion.Mode == ShinaiMode.Strike && owner.Motion.Phase == StrikePhase.Cut)
                {
                    var other = opposingShinai();
                    if (other != null && other.shinai != null && opposingBlockRaised != null && opposingBlockRaised())
                    {
                        float dd = HitGeometry.SegmentSegment(tsubaPt, tip, other.Tsuba, other.Tip, out _, out _, out var cp, out _);
                        if (dd < ShinaiRig.Radius * 2.5f)
                        {
                            Register(null, Target.None, grip, tip, cp, a, b, blockedByShinai: true);
                            return;
                        }
                    }
                }
                foreach (var t in targets)
                {
                    if (t == null || t.Root == null) continue;
                    foreach (var z in t.Zones)
                    {
                        if (z.anchor == null) continue;
                        float d = HitGeometry.SegmentSegment(tsubaPt, tip, z.A, z.B, out float s, out _, out var c1, out _);
                        if (d <= z.radius + ShinaiRig.Radius)
                        {
                            Register(t, z.target, grip, tip, c1, a, b, false);
                            return;
                        }
                    }
                }
            }
        }

        private void Register(IStrikeTarget t, Target zone, Vector3 grip, Vector3 tip, Vector3 contact, ShinaiRig.Sample a, ShinaiRig.Sample b, bool blockedByShinai)
        {
            bool striking = owner.Motion.Mode == ShinaiMode.Strike;
            if (!striking && touchCooldown > 0f) return; // a resting shinai against a target reports one touch per second
            float now = Time.time;
            var rig = owner.rig;
            float L = rig.length;
            var bladeDir = (tip - grip).normalized;
            float along = Vector3.Dot(contact - grip, bladeDir) / L;
            float dt = Mathf.Max(1e-4f, b.time - a.time);
            var tipVel = (b.tip - a.tip) / dt;
            var e = new StrikeEvidence { contact = !blockedByShinai, target = zone };
            e.contactAlongBlade = Mathf.Clamp01(along);
            // side of the blade that touched: zone centre relative to the blade axis vs the cutting edge (−tsuru)
            Vector3 edge = -b.tsuru;
            if (t != null)
            {
                Vector3 zc = contact;
                foreach (var z in t.Zones) if (z.target == zone) { zc = z.Center; break; }
                var toZone = Vector3.ProjectOnPlane(zc - contact, bladeDir);
                if (toZone.sqrMagnitude < 1e-6f) toZone = Vector3.ProjectOnPlane(-tipVel, bladeDir) * -1f;
                e.edgeSide = Vector3.Angle(toZone, edge) < 75f || zone == Target.Tsuki;
            }
            var velPerp = Vector3.ProjectOnPlane(tipVel, bladeDir);
            e.hasujiErrorDeg = zone == Target.Tsuki ? 0f : (velPerp.sqrMagnitude > 1e-4f ? Vector3.Angle(velPerp, edge) : 90f);
            e.tipSpeed = Mathf.Max(tipVel.magnitude, rig.PeakTipSpeed(0.08f));
            // swing evidence from the trajectory (not from the button): furikaburi rise, or thrust extension for tsuki
            float rise = rig.RiseAbove(contact.y, 0.6f);
            float thrust = rig.ThrustAlong(bladeDir, 0.35f);
            e.strikeArc = striking && (zone == Target.Tsuki || currentStrike == DuelAction.Tsuki ? thrust >= 0.12f : rise >= 0.18f);
            // body
            e.approachSpeed = owner.aiControlled ? (float)(KendoTimings.Lunge * owner.technique.drive / Mathf.Max(0.1f, owner.Motion.PlannedImpactTime))
                : Mathf.Max(0f, PlayerApproach());
            e.kiai = Mathf.Abs(owner.LastKiaiTime - now) < 1.0f;
            e.kiaiOffset = e.kiai ? owner.LastKiaiTime - now : 99f;
            float land = owner.footwork.LastFrontFootLanding;
            e.fumikomi = Mathf.Abs(land - now) < 0.6f || (owner.aiControlled && owner.technique.drive > 0.2f && striking);
            e.fumikomiOffset = e.fumikomi ? (owner.aiControlled ? (float)(owner.aiFootOffset) : land - now) : 99f;
            e.bothHands = !owner.Motion.InSageto;
            e.torsoTiltDeg = owner.aiControlled ? owner.aiTilt : Mathf.Max(0f, Mathf.Abs(owner.playerPitch) - 12f) * 0.8f;
            e.balanced = owner.Motion.Mode != ShinaiMode.Stunned && !(owner.footwork.Stepping && (owner.footwork.Dir == StepDir.Left || owner.footwork.Dir == StepDir.Right));
            e.distance = striking ? owner.StrikeStartDistance : (t != null ? owner.HorizontalDistance(t.Root.position) : 0f);
            e.opportunity = striking ? owner.StrikeOpportunity : Opportunity.None;
            e.blocked = blockedByShinai;
            if (!striking) touchCooldown = 1f;
            strikeContacted = true;
            lastContactTime = now;
            var report = new StrikeReport { attacker = owner, target = t, evidence = e, contactTime = now, contactPoint = contact, intended = currentStrike, blockedByShinai = blockedByShinai };
            report.evaluation = KendoEvaluationEngine.EvaluateStrike(e, rules, profile, t != null ? t.CurrentKamae : Kamae.Chudan);
            Contact?.Invoke(report);
            if (blockedByShinai) { Finish(report); return; }
            if (report.evaluation.invalidReason == InvalidReason.ContactOnly || !striking) { Finish(report); return; }
            pending = report;
            pendingStart = now;
            contactRootPos = owner.transform.position;
            targetPosAtContact = t != null ? t.Root.position : contact;
            maxFollow = 0f;
            restoredAt = -1f;
            awayTime = 0f;
        }

        private float PlayerApproach()
        {
            // speed of the body toward the target over the last 0.15 s (from the footwork model)
            if (owner.footwork.Stepping && owner.footwork.Dir == StepDir.Forward) return (float)(KendoTimings.StepForward / KendoTimings.StepSeconds) * owner.footwork.pace;
            return Time.time - owner.footwork.LastFrontFootLanding < 0.2f ? 0.6f : 0f;
        }

        private void TrackZanshin()
        {
            float since = Time.time - pendingStart;
            var p = owner.transform.position;
            var moved = p - contactRootPos;
            moved.y = 0f;
            maxFollow = Mathf.Max(maxFollow, moved.magnitude);
            // facing and kensen back on the opponent
            var toT = targetPosAtContact - p;
            if (pending.target != null) toT = pending.target.Root.position - p;
            toT.y = 0f;
            float facing = toT.sqrMagnitude > 1e-4f ? Vector3.Angle(owner.transform.forward, toT) : 0f;
            bool kamae = owner.Motion.InChudan;
            if (pending.target != null && kamae && restoredAt < 0f && since > 0.25f)
            {
                Vector3 throat = pending.target.Root.position + Vector3.up * 1.45f;
                foreach (var z in pending.target.Zones) if (z.target == Target.Tsuki) { throat = z.Center; break; }
                float kensen = Vector3.Angle(owner.rig.BladeDir, throat - owner.rig.Grip);
                if (kensen < 18f && facing < 35f) restoredAt = since;
            }
            if (facing > 100f || owner.Motion.InSageto) awayTime += Time.deltaTime;
            bool done = since >= zanshinWindow || (restoredAt >= 0f && since >= Mathf.Max(0.7f, restoredAt + 0.1f));
            if (owner.Motion.Mode == ShinaiMode.Strike && owner.Motion.StrikeTime < 0.1f && since > 0.3f) done = true; // a new strike began
            if (!done) return;
            var e = pending.evidence;
            e.zanshinObserved = true;
            e.followThrough = maxFollow;
            e.kamaeRestoredAfter = restoredAt >= 0f ? restoredAt : 99f;
            e.turnedAway = awayTime > 0.4f;
            pending.evaluation = KendoEvaluationEngine.EvaluateStrike(e, rules, profile, pending.target != null ? pending.target.CurrentKamae : Kamae.Chudan);
            var r = pending;
            pending = null;
            Finish(r);
        }

        private void Finish(StrikeReport r) => Evaluated?.Invoke(r);

        /// <summary>Drop any pending evaluation (e.g. the bout was stopped by yame).</summary>
        public void Cancel() { pending = null; }
    }
}
