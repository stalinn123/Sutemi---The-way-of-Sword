using System;

namespace Kendo.Rules
{
    [Serializable]
    public sealed class KamaeEvidence
    {
        public Kamae kamae = Kamae.Chudan;
        public bool bothHands = true;
        public float kensenOffset;        // m between the kensen line and the opponent's throat
        public float kensenLateralDeg;    // left/right deviation from the centre line
        public float leftHandForward;     // m in front of the navel
        public float leftHandHeightError; // m above/below navel height
        public float torsoTiltDeg;
        public float stanceLength = 0.2f; // m the right foot is ahead of the left
        public float stanceWidth = 0.1f;  // m sideways between the feet
        public float heldSeconds;         // continuous steady time
    }

    [Serializable]
    public sealed class ReiEvidence
    {
        public float bowAngleDeg;
        public float targetAngleDeg = 15f; // ~15° to a partner/teacher, ~30° to the kamiza (commonly taught; configurable)
        public float holdSeconds;
        public float facingErrorDeg;
        public bool shinaiAtSide = true;   // sageto / taito with the left hand
        public bool requireGaze;           // keep eyes on the partner when bowing to a person
        public bool gazeOnPartner = true;
        public bool feetStill = true;
    }

    [Serializable]
    public sealed class SuriashiEvidence
    {
        public int steps;
        public int stepsWithOkuriAshi;
        public int crossedFeet;
        public float maxGazePitchDeg;
        public float kamaeKeptFraction = 1f;
        public float markerError;          // m from the requested end mark
        public int extraSteps;             // steps beyond the requested sequence (overshoot / stumbling)
        public int hops;                   // any airborne movement
    }

    /// <summary>
    /// The single authoritative evaluator used by lessons, combat, opponent AI, match scoring, master feedback and RL reward.
    /// Pure C# (no UnityEngine) so it runs identically in the game, the duel simulator and the Python mirror
    /// (Tools/Rules/rules_ref.py). Official criteria come from FIK Regulations Art. 12/13; coaching criteria and all numbers
    /// are design values from <see cref="KendoEvaluationProfile"/>.
    /// </summary>
    public static class KendoEvaluationEngine
    {
        public static readonly float[] StrikeWeights =
        {
            10f, // DeliberateStrike
            10f, // ValidTarget
            12f, // StrikingPart
            10f, // Hasuji
            10f, // Spirit
            8f,  // Posture
            10f, // Zanshin
            10f, // KiKenTaiItchi
            6f,  // Distance
            5f,  // Timing
            4f,  // BodyDrive
            5f,  // Sharpness
        };

        public static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        public static float Ramp(float x, float bad, float good) => Math.Abs(good - bad) < 1e-6f ? (x >= good ? 1f : 0f) : Clamp01((x - bad) / (good - bad));

        public static bool TargetAllowed(Target t, KendoRuleConfig rules, Kamae opponentKamae)
        {
            switch (t)
            {
                case Target.None: return false;
                case Target.Tsuki: return rules.allowTsuki;
                case Target.KoteLeft: return !(rules.leftKoteOnlyAgainstNonChudan && opponentKamae == Kamae.Chudan);
                default: return true;
            }
        }

        public static string TargetName(Target t)
        {
            switch (t)
            {
                case Target.Men: return "men";
                case Target.MenRight: return "right men";
                case Target.MenLeft: return "left men";
                case Target.KoteRight: return "kote";
                case Target.KoteLeft: return "left kote";
                case Target.DoRight: return "do";
                case Target.DoLeft: return "gyaku-do";
                case Target.Tsuki: return "tsuki";
                default: return "no target";
            }
        }

        public static bool IsMen(Target t) => t == Target.Men || t == Target.MenLeft || t == Target.MenRight;
        public static bool IsKote(Target t) => t == Target.KoteRight || t == Target.KoteLeft;
        public static bool IsDo(Target t) => t == Target.DoRight || t == Target.DoLeft;

        public static StrikeEvaluation EvaluateStrike(StrikeEvidence e, KendoRuleConfig rules, KendoEvaluationProfile p, Kamae opponentKamae = Kamae.Chudan)
        {
            var ev = new StrikeEvaluation { target = e.target };
            if (!e.contact || e.target == Target.None)
            {
                ev.criteria.Add(new CriterionResult(Criterion.ValidTarget, true, false, 0f, FeedbackTopic.Target, "the shinai did not reach a target"));
                ev.invalidReason = InvalidReason.Missed;
                ev.performanceScore = 0f;
                ev.officialMargin = -1f;
                return ev;
            }

            // ---- official criteria (Art. 12) ----
            float speedRamp = Ramp(e.tipSpeed, 0.6f * p.minTipSpeed, p.minTipSpeed);
            bool deliberate = e.strikeArc && e.tipSpeed >= 0.6f * p.minTipSpeed;
            float sDeliberate = deliberate ? 0.5f + 0.5f * speedRamp : 0.2f * speedRamp;
            Add(ev, Criterion.DeliberateStrike, true, deliberate, sDeliberate, FeedbackTopic.Seme,
                deliberate ? $"a real strike at {e.tipSpeed:0.0} m/s" : (e.strikeArc ? $"too slow ({e.tipSpeed:0.0} m/s)" : "no swing - the shinai only touched"));

            bool allowed = TargetAllowed(e.target, rules, opponentKamae);
            Add(ev, Criterion.ValidTarget, true, allowed, allowed ? 1f : 0f, FeedbackTopic.Target,
                allowed ? $"{TargetName(e.target)} is a valid target" : $"{TargetName(e.target)} is not allowed here");

            bool monouchi = e.contactAlongBlade >= p.monouchiStart;
            bool part = monouchi && e.edgeSide;
            float sPart = Ramp(e.contactAlongBlade, p.monouchiStart - 0.2f, p.monouchiStart) * (e.edgeSide ? 1f : 0.3f);
            Add(ev, Criterion.StrikingPart, true, part, sPart, FeedbackTopic.Monouchi,
                $"contact at {Math.Round(e.contactAlongBlade * 100f)} % of the blade" + (e.edgeSide ? "" : ", on the wrong side"));

            bool hasuji = e.hasujiErrorDeg <= p.maxHasujiDeg;
            Add(ev, Criterion.Hasuji, true, hasuji, Clamp01(1f - e.hasujiErrorDeg / (2f * p.maxHasujiDeg)), FeedbackTopic.Hasuji,
                $"blade edge {Math.Round(e.hasujiErrorDeg)}° off the cutting line");

            bool spirit = e.kiai && e.kiaiOffset >= p.kiaiEarliest && e.kiaiOffset <= p.kiaiLatest;
            Add(ev, Criterion.Spirit, true, spirit, spirit ? 1f : (e.kiai ? 0.4f : 0f), FeedbackTopic.Kiai,
                spirit ? "kiai with the strike" : (e.kiai ? "kiai was not with the strike" : "no kiai"));

            bool posture = e.bothHands && e.balanced && e.torsoTiltDeg <= p.maxTorsoTiltDeg;
            float sPosture = (e.bothHands ? 0.5f : 0f) + (e.balanced ? 0.2f : 0f) + 0.3f * Clamp01(1f - e.torsoTiltDeg / (2f * p.maxTorsoTiltDeg));
            Add(ev, Criterion.Posture, true, posture, sPosture, FeedbackTopic.Posture,
                !e.bothHands ? "one hand left the grip" : (!e.balanced ? "off balance" : $"torso tilt {Math.Round(e.torsoTiltDeg)}°"));

            bool zanshin = e.zanshinObserved && e.followThrough >= p.minFollowThrough && e.kamaeRestoredAfter <= p.maxKamaeRestore
                           && !e.turnedAway && !(e.celebrated && rules.revokeOnCelebration);
            float z1 = Clamp01(e.followThrough / Math.Max(0.01f, p.minFollowThrough));
            float z2 = e.kamaeRestoredAfter <= p.maxKamaeRestore ? 1f : Clamp01(1f - (e.kamaeRestoredAfter - p.maxKamaeRestore) / p.maxKamaeRestore);
            float sZanshin = e.zanshinObserved ? 0.4f * z1 + 0.4f * z2 + ((e.turnedAway || e.celebrated) ? 0f : 0.2f) : 0f;
            Add(ev, Criterion.Zanshin, true, zanshin, sZanshin, FeedbackTopic.Zanshin,
                !e.zanshinObserved ? "no zanshin observed" :
                (e.celebrated ? "celebrated instead of staying ready" :
                (e.turnedAway ? "lowered the guard after the strike" :
                $"followed through {e.followThrough:0.00} m, back in kamae after {Math.Min(e.kamaeRestoredAfter, 9.9f):0.0} s")));

            // ---- coaching criteria (never change validity) ----
            bool kiaiIn = e.kiai && Math.Abs(e.kiaiOffset) <= p.kktiWindow;
            bool footIn = e.fumikomi && Math.Abs(e.fumikomiOffset) <= p.kktiWindow;
            bool kkti = kiaiIn && (footIn || !p.requireFumikomiForKkti);
            Add(ev, Criterion.KiKenTaiItchi, false, kkti, kkti ? 1f : ((kiaiIn || footIn) ? 0.5f : 0f), FeedbackTopic.KiKenTaiItchi,
                kkti ? "voice, sword and foot together" :
                (!e.fumikomi ? "no fumikomi step with the strike" :
                (!kiaiIn ? $"kiai {Offset(e.kiai, e.kiaiOffset)}" : $"foot {Offset(true, e.fumikomiOffset)}")));

            bool inDistance = e.distance >= p.idealDistanceMin && e.distance <= p.idealDistanceMax;
            float outside = e.distance < p.idealDistanceMin ? p.idealDistanceMin - e.distance : Math.Max(0f, e.distance - p.idealDistanceMax);
            Add(ev, Criterion.Distance, false, inDistance, inDistance ? 1f : Clamp01(1f - outside / 0.6f), FeedbackTopic.Maai,
                inDistance ? $"started from {e.distance:0.00} m" : (e.distance < p.idealDistanceMin ? $"too close ({e.distance:0.00} m)" : $"too far ({e.distance:0.00} m)"));

            float sTiming = TimingScore(e.opportunity);
            Add(ev, Criterion.Timing, false, sTiming >= 0.5f, sTiming, FeedbackTopic.Timing, OpportunityText(e.opportunity));

            bool drive = e.approachSpeed >= p.minApproachSpeed;
            Add(ev, Criterion.BodyDrive, false, drive, Clamp01(e.approachSpeed / (2f * p.minApproachSpeed)), FeedbackTopic.BodyDrive,
                drive ? "body moved into the strike" : "the body stayed behind the shinai");

            bool sharp = e.tipSpeed >= p.minTipSpeed;
            Add(ev, Criterion.Sharpness, false, sharp, Ramp(e.tipSpeed, 0.5f * p.minTipSpeed, p.goodTipSpeed), FeedbackTopic.Sharpness,
                $"tip speed {e.tipSpeed:0.0} m/s");

            // ---- validity ----
            if (!deliberate) ev.invalidReason = InvalidReason.ContactOnly;
            else if (!allowed) ev.invalidReason = InvalidReason.TargetNotAllowed;
            else if (!monouchi) ev.invalidReason = InvalidReason.NotMonouchi;
            else if (!e.edgeSide) ev.invalidReason = InvalidReason.WrongSide;
            else if (!hasuji) ev.invalidReason = InvalidReason.BadHasuji;
            else if (!spirit) ev.invalidReason = InvalidReason.NoSpirit;
            else if (!posture) ev.invalidReason = InvalidReason.BadPosture;
            else if (!zanshin) ev.invalidReason = InvalidReason.NoZanshin;
            else if (e.blocked) ev.invalidReason = InvalidReason.Blocked;
            else ev.invalidReason = InvalidReason.None;

            ev.yukoDatotsu = ev.invalidReason == InvalidReason.None;

            float total = 0f;
            float minOfficial = 1f;
            for (int i = 0; i < ev.criteria.Count; i++)
            {
                var c = ev.criteria[i];
                total += StrikeWeights[(int)c.criterion] * c.score;
                if (c.official && c.score < minOfficial) minOfficial = c.score;
            }
            if (e.blocked) total *= 0.6f;
            if (ev.invalidReason == InvalidReason.ContactOnly) total = Math.Min(total, p.contactOnlyScoreCap);
            ev.performanceScore = (float)Math.Round(total, 2);
            ev.officialMargin = ev.yukoDatotsu ? minOfficial - 0.5f : -1f;
            ev.passed = ev.yukoDatotsu && ev.performanceScore >= p.passScore;
            return ev;
        }

        public static float TimingScore(Opportunity o)
        {
            switch (o)
            {
                case Opportunity.Debana:
                case Opportunity.AfterBlock:
                case Opportunity.AfterOpponentStrike:
                case Opportunity.OpponentRetreating: return 1f;
                case Opportunity.OpponentStatic: return 0.6f;
                case Opportunity.IntoReadyKamae: return 0.2f;
                default: return 0.5f;
            }
        }

        public static string OpportunityText(Opportunity o)
        {
            switch (o)
            {
                case Opportunity.Debana: return "struck as the opponent started to move (debana)";
                case Opportunity.AfterBlock: return "countered after receiving (oji-waza)";
                case Opportunity.AfterOpponentStrike: return "struck as the opponent's technique ended";
                case Opportunity.OpponentRetreating: return "struck as the opponent retreated";
                case Opportunity.OpponentStatic: return "struck a still target";
                case Opportunity.IntoReadyKamae: return "attacked straight into a ready guard";
                default: return "no particular opportunity";
            }
        }

        private static string Offset(bool present, float offset)
        {
            if (!present) return "missing";
            return offset < 0f ? $"{-offset:0.00} s early" : $"{offset:0.00} s late";
        }

        private static void Add(Evaluation ev, Criterion c, bool official, bool passed, float score, FeedbackTopic topic, string detail)
            => ev.criteria.Add(new CriterionResult(c, official, passed, score, topic, detail));

        // ------------------------------------------------------------------ kamae

        public static Evaluation EvaluateKamae(KamaeEvidence k, KendoEvaluationProfile p)
        {
            var ev = new Evaluation();
            bool chudan = k.kamae == Kamae.Chudan;
            Add(ev, Criterion.Grip, false, chudan && k.bothHands, chudan && k.bothHands ? 1f : 0f, FeedbackTopic.Grip,
                !chudan ? "not in chudan" : (k.bothHands ? "both hands on the tsuka" : "one hand only"));
            bool line = k.kensenOffset <= p.maxKensenOffset && k.kensenLateralDeg <= p.maxKensenLateralDeg;
            Add(ev, Criterion.KensenLine, false, line,
                0.5f * Clamp01(1f - k.kensenOffset / (2f * p.maxKensenOffset)) + 0.5f * Clamp01(1f - k.kensenLateralDeg / (2f * p.maxKensenLateralDeg)),
                FeedbackTopic.Kensen, $"kensen {k.kensenOffset * 100f:0} cm from the throat line, {k.kensenLateralDeg:0}° to the side");
            bool hand = k.leftHandForward >= p.leftHandForwardMin && k.leftHandForward <= p.leftHandForwardMax && Math.Abs(k.leftHandHeightError) <= p.leftHandHeightTol;
            Add(ev, Criterion.LeftHand, false, hand, hand ? 1f : 0.3f, FeedbackTopic.LeftHand,
                $"left fist {k.leftHandForward * 100f:0} cm in front of the navel, {k.leftHandHeightError * 100f:+0;-0} cm in height");
            bool posture = k.torsoTiltDeg <= p.maxTorsoTiltDeg;
            Add(ev, Criterion.Posture, false, posture, Clamp01(1f - k.torsoTiltDeg / (2f * p.maxTorsoTiltDeg)), FeedbackTopic.Posture,
                $"torso tilt {k.torsoTiltDeg:0}°");
            bool feet = k.stanceLength >= p.stanceLengthMin && k.stanceLength <= p.stanceLengthMax && k.stanceWidth <= p.stanceWidthMax;
            Add(ev, Criterion.Feet, false, feet, feet ? 1f : 0.3f, FeedbackTopic.Feet,
                k.stanceLength < p.stanceLengthMin ? "feet side by side - right foot should lead" :
                (k.stanceLength > p.stanceLengthMax ? "feet too far apart front to back" :
                (k.stanceWidth > p.stanceWidthMax ? "feet too wide" : "right foot leads, feet about a fist apart")));
            bool still = k.heldSeconds >= p.stillSeconds;
            Add(ev, Criterion.Stillness, false, still, Clamp01(k.heldSeconds / p.stillSeconds), FeedbackTopic.Stillness,
                $"held steady {k.heldSeconds:0.0} s");
            Finish(ev, new[] { 20f, 25f, 15f, 15f, 15f, 10f }, p.passScore);
            return ev;
        }

        // ------------------------------------------------------------------ rei

        public static Evaluation EvaluateRei(ReiEvidence r, KendoEvaluationProfile p)
        {
            var ev = new Evaluation();
            float depthErr = Math.Abs(r.bowAngleDeg - r.targetAngleDeg);
            Add(ev, Criterion.BowDepth, false, depthErr <= p.bowTolDeg, Clamp01(1f - depthErr / (2f * p.bowTolDeg)), FeedbackTopic.BowDepth,
                $"bowed {r.bowAngleDeg:0}° (aim for about {r.targetAngleDeg:0}°)");
            Add(ev, Criterion.BowHold, false, r.holdSeconds >= p.bowHoldMin, Clamp01(r.holdSeconds / p.bowHoldMin), FeedbackTopic.BowHold,
                $"paused {r.holdSeconds:0.0} s at the bottom");
            Add(ev, Criterion.Facing, false, r.facingErrorDeg <= p.facingTolDeg, Clamp01(1f - r.facingErrorDeg / (2f * p.facingTolDeg)), FeedbackTopic.Facing,
                $"facing {r.facingErrorDeg:0}° off");
            Add(ev, Criterion.ShinaiAtSide, false, r.shinaiAtSide, r.shinaiAtSide ? 1f : 0f, FeedbackTopic.Sageto,
                r.shinaiAtSide ? "shinai held at the left side" : "the shinai was still raised");
            bool gaze = !r.requireGaze || r.gazeOnPartner;
            Add(ev, Criterion.Gaze, false, gaze, gaze ? 1f : 0f, FeedbackTopic.Metsuke,
                r.requireGaze ? (r.gazeOnPartner ? "eyes kept on the partner" : "looked at the floor") : "eyes lowered to the kamiza");
            Add(ev, Criterion.Stillness, false, r.feetStill, r.feetStill ? 1f : 0f, FeedbackTopic.Stillness,
                r.feetStill ? "feet still" : "moved during the bow");
            Finish(ev, new[] { 30f, 15f, 20f, 15f, 10f, 10f }, p.passScore);
            return ev;
        }

        // ------------------------------------------------------------------ suriashi

        public static Evaluation EvaluateSuriashi(SuriashiEvidence s, KendoEvaluationProfile p)
        {
            var ev = new Evaluation();
            bool okuri = s.steps > 0 && s.stepsWithOkuriAshi == s.steps;
            Add(ev, Criterion.OkuriAshi, false, okuri, s.steps > 0 ? (float)s.stepsWithOkuriAshi / s.steps : 0f, FeedbackTopic.OkuriAshi,
                $"{s.stepsWithOkuriAshi} of {s.steps} steps drew the rear foot up");
            Add(ev, Criterion.Feet, false, s.crossedFeet == 0 && s.hops == 0, s.crossedFeet == 0 && s.hops == 0 ? 1f : 0f, FeedbackTopic.Feet,
                s.hops > 0 ? "feet left the floor" : (s.crossedFeet > 0 ? $"feet crossed {s.crossedFeet}×" : "feet never crossed"));
            Add(ev, Criterion.Gaze, false, s.maxGazePitchDeg <= p.maxGazePitchDeg, Clamp01(1f - s.maxGazePitchDeg / (2f * p.maxGazePitchDeg)), FeedbackTopic.Metsuke,
                $"gaze dipped {s.maxGazePitchDeg:0}°");
            Add(ev, Criterion.KamaeKept, false, s.kamaeKeptFraction >= 0.9f, s.kamaeKeptFraction, FeedbackTopic.Kamae,
                $"kamae held {s.kamaeKeptFraction * 100f:0} % of the time");
            Add(ev, Criterion.Accuracy, false, s.markerError <= p.markerTol, Clamp01(1f - s.markerError / (3f * p.markerTol)), FeedbackTopic.Accuracy,
                $"finished {s.markerError * 100f:0} cm from the mark");
            Add(ev, Criterion.Control, false, s.extraSteps <= 1, s.extraSteps <= 1 ? 1f : Clamp01(1f - (s.extraSteps - 1) / 4f), FeedbackTopic.Control,
                s.extraSteps == 0 ? "no extra steps" : $"{s.extraSteps} extra steps");
            Finish(ev, new[] { 25f, 10f, 15f, 20f, 20f, 10f }, p.passScore);
            return ev;
        }

        private static void Finish(Evaluation ev, float[] weights, float passScore)
        {
            float total = 0f;
            bool all = true;
            for (int i = 0; i < ev.criteria.Count; i++)
            {
                total += weights[i] * ev.criteria[i].score;
                all &= ev.criteria[i].passed;
            }
            ev.performanceScore = (float)Math.Round(total, 2);
            ev.passed = all && ev.performanceScore >= passScore;
        }
    }
}
