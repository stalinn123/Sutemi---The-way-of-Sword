using System;
using System.Collections.Generic;
using Kendo.Rules;

namespace Kendo.AI
{
    /// <summary>Discrete actions shared by the player model, the rule-based agent, the imitation agent and the A2C agent.</summary>
    public enum DuelAction { Wait = 0, StepForward, StepBack, StepLeft, StepRight, Men, Kote, Do, Tsuki, BlockHigh, BlockDo }

    public enum DuelPhase { Ready = 0, Stepping, Striking, Recovering, Blocking, Stunned }

    /// <summary>Timings shared by the game's shinai/footwork motion and the duel simulator (keep equal to Tools/AI/duel_sim.py).</summary>
    public static class KendoTimings
    {
        public const int ActionCount = 11;
        public const int ObservationSize = 26;
        public const double StepSeconds = 0.30, SettleSeconds = 0.06;
        public const double StepForward = 0.30, StepBack = 0.25, StepSide = 0.25;
        public const double Lunge = 0.40, FollowSeconds = 0.40, ReturnSeconds = 0.35;
        public const double BlockRaise = 0.08, BlockHold = 0.50, StunSeconds = 0.30, CounterWindow = 0.60;
        public const double DecisionDt = 0.10, SubDt = 0.05;

        public static bool IsStrike(DuelAction a) => a == DuelAction.Men || a == DuelAction.Kote || a == DuelAction.Do || a == DuelAction.Tsuki;

        public static double Raise(DuelAction a)
        {
            switch (a) { case DuelAction.Men: return 0.18; case DuelAction.Kote: return 0.12; case DuelAction.Do: return 0.18; case DuelAction.Tsuki: return 0.10; default: return 0; }
        }

        public static double Strike(DuelAction a)
        {
            switch (a) { case DuelAction.Men: return 0.12; case DuelAction.Kote: return 0.10; case DuelAction.Do: return 0.14; case DuelAction.Tsuki: return 0.14; default: return 0; }
        }

        public static double ReachMin(DuelAction a)
        {
            switch (a) { case DuelAction.Men: return 1.05; case DuelAction.Kote: return 1.00; case DuelAction.Do: return 0.95; case DuelAction.Tsuki: return 1.05; default: return 0; }
        }

        public static double ReachMax(DuelAction a)
        {
            switch (a) { case DuelAction.Men: return 1.80; case DuelAction.Kote: return 1.70; case DuelAction.Do: return 1.65; case DuelAction.Tsuki: return 1.75; default: return 0; }
        }

        public static Target TargetOf(DuelAction a)
        {
            switch (a) { case DuelAction.Men: return Target.Men; case DuelAction.Kote: return Target.KoteRight; case DuelAction.Do: return Target.DoRight; case DuelAction.Tsuki: return Target.Tsuki; default: return Target.None; }
        }

        public static double BlockCover(DuelAction block, DuelAction strike)
        {
            if (block == DuelAction.BlockHigh) return strike == DuelAction.Men || strike == DuelAction.Tsuki ? 1.0 : (strike == DuelAction.Kote ? 0.5 : 0.0);
            if (block == DuelAction.BlockDo) return strike == DuelAction.Do ? 1.0 : (strike == DuelAction.Kote ? 0.5 : 0.0);
            return 0.0;
        }
    }

    /// <summary>Per-fighter technique quality (design values; randomised during training).</summary>
    [Serializable]
    public sealed class Technique
    {
        public double speed = 8.5, hasujiSigma = 12.0, kiaiProb = 0.85, timingSigma = 0.08, tiltSigma = 6.0, zanshinProb = 0.8, edgeProb = 0.97, drive = 1.0, guard = 0.3;

        public static Technique Easy() => new Technique { speed = 7.0, hasujiSigma = 18.0, kiaiProb = 0.7, zanshinProb = 0.6, guard = 0.15, timingSigma = 0.12 };
        public static Technique Medium() => new Technique { speed = 8.0, hasujiSigma = 13.0, kiaiProb = 0.85, zanshinProb = 0.75, guard = 0.3, timingSigma = 0.09 };
        public static Technique Hard() => new Technique { speed = 9.0, hasujiSigma = 10.0, kiaiProb = 0.9, zanshinProb = 0.85, guard = 0.4, timingSigma = 0.07 };
        public static Technique Boss() => new Technique { speed = 10.0, hasujiSigma = 8.0, kiaiProb = 0.95, zanshinProb = 0.9, guard = 0.5, timingSigma = 0.05 };
    }

    [Serializable]
    public sealed class DuelParams
    {
        public double courtHalf = 4.5, startDistance = 2.8, trackingNoise = 0.0;
        public int[] latencySteps = { 0, 0 };
        public double[] speedScale = { 1.0, 1.0 };
        public double[] moveScale = { 1.0, 1.0 };
        public double matchSeconds = 120.0;
        public int pointsToWin = 2;
        public bool allowTsuki = true;
        public string profile = "Intermediate";
    }

    public sealed class DuelFighter
    {
        public double x, y, facing;
        public Technique tech;
        public DuelPhase phase = DuelPhase.Ready;
        public double t, dur, impactAt, stepDx, stepDy, strikeStartDist, counterUntil = -1.0;
        public DuelAction action = DuelAction.Wait, blockKind = DuelAction.Wait;
        public bool impactDone;
        public Opportunity oppAtStart;
        public bool hasPending;
        public double pendingTime;
        public StrikeEvaluation pendingEval;
        public IpponDecision pendingDecision;
    }

    /// <summary>
    /// Abstract kendo duel used for agent training (in the background during play, and offline in Python) and for tests.
    /// Exact port of Tools/AI/duel_sim.py; hits are judged by <see cref="KendoEvaluationEngine"/> and <see cref="ShinpanPanel"/>.
    /// </summary>
    public sealed class DuelSim
    {
        public readonly KRandom rng;
        public readonly DuelParams p;
        public readonly KendoRuleConfig rules;
        public readonly KendoEvaluationProfile profile;
        public readonly KendoMatch match;
        public readonly Technique[] tech = new Technique[2];
        public readonly DuelFighter[] f = new DuelFighter[2];
        public readonly StrikeEvaluation[] lastEval = new StrikeEvaluation[2];
        private readonly ShinpanPanel panel = new ShinpanPanel();
        private readonly List<double[]>[] history = { new List<double[]>(), new List<double[]>() };
        public double time;
        private readonly double[] shaped = new double[2];

        public DuelSim(long seed, DuelParams parameters = null, Technique t0 = null, Technique t1 = null)
        {
            rng = new KRandom(seed);
            p = parameters ?? new DuelParams();
            rules = new KendoRuleConfig { allowTsuki = p.allowTsuki, pointsToWin = p.pointsToWin, matchSeconds = (float)p.matchSeconds, enchoSeconds = 30f };
            profile = p.profile == "Beginner" ? KendoEvaluationProfile.Beginner() : (p.profile == "Shiai" ? KendoEvaluationProfile.Shiai() : KendoEvaluationProfile.Intermediate());
            tech[0] = t0 ?? new Technique();
            tech[1] = t1 ?? new Technique();
            match = new KendoMatch(rules);
            ResetPositions();
            match.Hajime();
        }

        public bool Done => match.Phase == MatchPhase.Finished;

        public void ResetPositions()
        {
            double d = p.startDistance;
            f[0] = new DuelFighter { x = -d / 2, facing = 1.0, tech = tech[0] };
            f[1] = new DuelFighter { x = d / 2, facing = -1.0, tech = tech[1] };
            history[0].Clear();
            history[1].Clear();
        }

        public double Distance()
        {
            double dx = f[1].x - f[0].x, dy = f[1].y - f[0].y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double[] RawObservation(int i)
        {
            var me = f[i];
            var op = f[1 - i];
            var o = new double[KendoTimings.ObservationSize];
            o[0] = Distance() / 3.0;
            o[1] = me.x * me.facing / p.courtHalf;
            o[2] = op.x * me.facing / p.courtHalf;
            o[3] = op.y - me.y;
            o[4 + (int)me.phase] = 1.0;
            o[10 + (int)op.phase] = 1.0;
            o[16] = me.dur > 0 ? Math.Min(1.0, me.t / me.dur) : 0.0;
            o[17] = op.dur > 0 ? Math.Min(1.0, op.t / op.dur) : 0.0;
            if (op.phase == DuelPhase.Striking)
            {
                if (op.action == DuelAction.Men) o[18] = 1.0;
                else if (op.action == DuelAction.Kote) o[19] = 1.0;
                else if (op.action == DuelAction.Do) o[20] = 1.0;
                else if (op.action == DuelAction.Tsuki) o[21] = 1.0;
            }
            o[22] = time < me.counterUntil ? 1.0 : 0.0;
            o[23] = (match.points[i] - match.points[1 - i]) / 2.0;
            o[24] = match.TimeRemaining / Math.Max(1.0, rules.matchSeconds);
            o[25] = op.phase == DuelPhase.Blocking ? 1.0 : 0.0;
            return o;
        }

        /// <summary>Observation with tracking noise and input latency (domain randomisation).</summary>
        public double[] Observe(int i)
        {
            var o = RawObservation(i);
            if (p.trackingNoise > 0)
            {
                o[0] += rng.Gauss() * p.trackingNoise / 3.0;
                o[3] += rng.Gauss() * p.trackingNoise;
            }
            var h = history[i];
            h.Add(o);
            if (h.Count > p.latencySteps[i] + 1) h.RemoveAt(0);
            return h[0];
        }

        private static bool CanAct(DuelFighter x) => x.phase == DuelPhase.Ready || x.phase == DuelPhase.Blocking;

        /// <summary>True when fighter <paramref name="i"/> can start a new action (agents are only queried then).</summary>
        public bool CanAct(int i) => CanAct(f[i]);

        public void Apply(int i, DuelAction a)
        {
            var x = f[i];
            if (!CanAct(x)) return;
            if (a == DuelAction.Wait)
            {
                if (x.phase == DuelPhase.Blocking && x.t >= KendoTimings.BlockHold) { x.phase = DuelPhase.Ready; x.dur = 0.0; }
                return;
            }
            x.action = a;
            x.t = 0.0;
            double ms = p.moveScale[i];
            switch (a)
            {
                case DuelAction.StepForward:
                case DuelAction.StepBack:
                case DuelAction.StepLeft:
                case DuelAction.StepRight:
                    x.phase = DuelPhase.Stepping;
                    x.dur = (KendoTimings.StepSeconds + KendoTimings.SettleSeconds) / ms;
                    x.stepDx = (a == DuelAction.StepForward ? KendoTimings.StepForward : (a == DuelAction.StepBack ? -KendoTimings.StepBack : 0.0)) * x.facing;
                    x.stepDy = (a == DuelAction.StepLeft ? KendoTimings.StepSide : (a == DuelAction.StepRight ? -KendoTimings.StepSide : 0.0)) * x.facing;
                    break;
                case DuelAction.Men:
                case DuelAction.Kote:
                case DuelAction.Do:
                case DuelAction.Tsuki:
                    double sp = p.speedScale[i];
                    x.phase = DuelPhase.Striking;
                    x.impactAt = (KendoTimings.Raise(a) + KendoTimings.Strike(a)) / sp;
                    x.dur = x.impactAt + KendoTimings.FollowSeconds;
                    x.strikeStartDist = Distance();
                    x.impactDone = false;
                    x.oppAtStart = OpportunityFor(i);
                    break;
                default:
                    x.phase = DuelPhase.Blocking;
                    x.blockKind = a;
                    x.dur = KendoTimings.BlockHold;
                    break;
            }
        }

        public Opportunity OpportunityFor(int i)
        {
            var me = f[i];
            var op = f[1 - i];
            if (time < me.counterUntil) return Opportunity.AfterBlock;
            if (op.phase == DuelPhase.Striking && op.t < 0.12) return Opportunity.Debana;
            if (op.phase == DuelPhase.Stepping && op.stepDx * op.facing > 0 && op.t < 0.15) return Opportunity.Debana;
            if (op.phase == DuelPhase.Recovering || (op.phase == DuelPhase.Striking && op.impactDone)) return Opportunity.AfterOpponentStrike;
            if (op.phase == DuelPhase.Stepping && op.stepDx * op.facing < 0) return Opportunity.OpponentRetreating;
            if (op.phase == DuelPhase.Stunned) return Opportunity.AfterBlock;
            if (op.phase == DuelPhase.Blocking) return Opportunity.IntoReadyKamae;
            return Distance() < 2.3 ? Opportunity.IntoReadyKamae : Opportunity.OpponentStatic;
        }

        private void SubStep(double dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var x = f[i];
                x.t += dt;
                switch (x.phase)
                {
                    case DuelPhase.Stepping:
                        double frac = dt / x.dur;
                        x.x += x.stepDx * frac;
                        x.y += x.stepDy * frac;
                        if (x.t >= x.dur) { x.phase = DuelPhase.Ready; x.dur = 0.0; }
                        break;
                    case DuelPhase.Striking:
                        if (!x.impactDone)
                        {
                            double lunge = KendoTimings.Lunge * x.tech.drive * dt / x.impactAt;
                            if (Distance() > 0.9) x.x += lunge * x.facing;
                            if (x.t >= x.impactAt) { x.impactDone = true; Impact(i); }
                        }
                        else if (x.phase == DuelPhase.Striking)
                        {
                            x.x += Distance() > 0.8 ? 0.5 * dt * x.facing : 0.0;
                        }
                        if (x.phase == DuelPhase.Striking && x.t >= x.dur) { x.phase = DuelPhase.Recovering; x.t = 0.0; x.dur = KendoTimings.ReturnSeconds; }
                        break;
                    case DuelPhase.Recovering:
                    case DuelPhase.Stunned:
                        if (x.t >= x.dur) { x.phase = DuelPhase.Ready; x.dur = 0.0; }
                        break;
                }
            }
            var a = f[0];
            var b = f[1];
            if (b.x - a.x < 0.6)
            {
                double mid = (a.x + b.x) / 2;
                a.x = mid - 0.3;
                b.x = mid + 0.3;
            }
            ResolvePending();
            for (int i = 0; i < 2; i++)
            {
                if (Math.Abs(f[i].x) > p.courtHalf && match.Phase == MatchPhase.Running)
                {
                    match.Hansoku(i, "jogai");
                    AfterPoint();
                    return;
                }
            }
            match.Tick((float)dt);
            time += dt;
        }

        private void Impact(int i)
        {
            var x = f[i];
            var op = f[1 - i];
            var a = x.action;
            double d = Distance();
            double lo = KendoTimings.ReachMin(a), hi = KendoTimings.ReachMax(a);
            double lateral = Math.Abs(op.y - x.y);
            bool contact = d >= lo && d <= hi && lateral < (a == DuelAction.Kote || a == DuelAction.Do ? 0.45 : 0.35);
            var tq = x.tech;
            var e = new StrikeEvidence { target = KendoTimings.TargetOf(a), contact = contact };
            e.contactAlongBlade = (float)Math.Min(1.0, Math.Max(0.0, 0.45 + 0.5 * (d - lo) / (hi - lo) + 0.05 * rng.Gauss()));
            e.edgeSide = rng.Value() < tq.edgeProb;
            e.hasujiErrorDeg = (float)(Math.Abs(rng.Gauss()) * tq.hasujiSigma + lateral * 20.0);
            e.tipSpeed = (float)(tq.speed * p.speedScale[i] * (1.0 + 0.1 * rng.Gauss()));
            e.strikeArc = true;
            e.approachSpeed = (float)(KendoTimings.Lunge * tq.drive / x.impactAt);
            e.kiai = rng.Value() < tq.kiaiProb;
            e.kiaiOffset = (float)(rng.Gauss() * tq.timingSigma);
            e.fumikomi = tq.drive > 0.2;
            e.fumikomiOffset = (float)(rng.Gauss() * tq.timingSigma);
            e.torsoTiltDeg = (float)(Math.Abs(rng.Gauss()) * tq.tiltSigma);
            e.balanced = true;
            e.distance = (float)x.strikeStartDist;
            e.opportunity = x.oppAtStart;
            double cover = op.phase == DuelPhase.Blocking && op.t >= KendoTimings.BlockRaise ? KendoTimings.BlockCover(op.blockKind, a) : 0.0;
            double passive = op.phase == DuelPhase.Ready && x.oppAtStart == Opportunity.IntoReadyKamae ? op.tech.guard : 0.0;
            if (contact && ((cover > 0 && rng.Value() < cover) || (passive > 0 && rng.Value() < passive)))
            {
                e.blocked = true;
                x.phase = DuelPhase.Stunned;
                x.t = 0.0;
                x.dur = KendoTimings.StunSeconds;
                op.counterUntil = time + KendoTimings.CounterWindow;
                op.phase = DuelPhase.Ready;
                op.dur = 0.0;
            }
            if (rng.Value() < tq.zanshinProb)
            {
                e.zanshinObserved = true;
                e.followThrough = (float)rng.Range(0.35, 0.8);
                e.kamaeRestoredAfter = (float)rng.Range(0.5, 1.3);
            }
            else
            {
                e.zanshinObserved = rng.Value() < 0.5;
                e.followThrough = (float)rng.Range(0.0, 0.2);
                e.kamaeRestoredAfter = (float)rng.Range(1.5, 3.0);
                e.turnedAway = rng.Value() < 0.5;
            }
            var ev = KendoEvaluationEngine.EvaluateStrike(e, rules, profile, Kamae.Chudan);
            var dec = panel.Decide(ev, rules);
            lastEval[i] = ev;
            shaped[i] += KendoReward.Shaping(ev, dec);
            if (!x.hasPending)
            {
                x.hasPending = true;
                x.pendingTime = time;
                x.pendingEval = ev;
                x.pendingDecision = dec;
            }
        }

        private void ResolvePending()
        {
            if (!f[0].hasPending && !f[1].hasPending) return;
            for (int i = 0; i < 2; i++)
            {
                var x = f[i];
                if (!x.hasPending) continue;
                var other = f[1 - i];
                if (other.hasPending && Math.Abs(other.pendingTime - x.pendingTime) <= 0.1 && x.pendingDecision.awarded && other.pendingDecision.awarded)
                {
                    match.AiUchi();
                    f[0].hasPending = false;
                    f[1].hasPending = false;
                    return;
                }
                if (time - x.pendingTime >= 0.1 || other.hasPending)
                {
                    x.hasPending = false;
                    if (x.pendingDecision.awarded && match.Phase == MatchPhase.Running)
                    {
                        match.AwardIppon(i, x.pendingEval.target);
                        AfterPoint();
                        return;
                    }
                }
            }
        }

        private void AfterPoint()
        {
            if (match.Phase != MatchPhase.Finished)
            {
                ResetPositions();
                match.Hajime();
            }
        }

        /// <summary>Advance one decision step (0.1 s). Returns the rewards of fighter 0 and fighter 1.</summary>
        public double[] Step(DuelAction a0, DuelAction a1)
        {
            int p0 = match.points[0], p1 = match.points[1], h0 = match.hansoku[0], h1 = match.hansoku[1];
            Apply(0, a0);
            Apply(1, a1);
            double t = 0.0;
            while (t < KendoTimings.DecisionDt - 1e-9 && match.Phase != MatchPhase.Finished)
            {
                SubStep(KendoTimings.SubDt);
                t += KendoTimings.SubDt;
            }
            var r = new double[] { KendoReward.StepCost + shaped[0], KendoReward.StepCost + shaped[1] };
            shaped[0] = 0;
            shaped[1] = 0;
            int g0 = match.points[0] - p0, g1 = match.points[1] - p1;
            r[0] += g0 * KendoReward.Ippon + g1 * KendoReward.Conceded;
            r[1] += g1 * KendoReward.Ippon + g0 * KendoReward.Conceded;
            if (match.hansoku[0] > h0) r[0] += KendoReward.HansokuPenalty;
            if (match.hansoku[1] > h1) r[1] += KendoReward.HansokuPenalty;
            if (Done)
            {
                r[0] += KendoReward.ForMatchEnd(match.Winner, 0);
                r[1] += KendoReward.ForMatchEnd(match.Winner, 1);
            }
            return r;
        }
    }
}
