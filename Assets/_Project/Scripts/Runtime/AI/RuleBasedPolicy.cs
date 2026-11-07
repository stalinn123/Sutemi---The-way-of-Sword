using System;

namespace Kendo.AI
{
    public interface IDuelPolicy
    {
        DuelAction Act(double[] observation);
        string Name { get; }
    }

    /// <summary>Configurable fighting style for Agent 1 (rule-based opponent). All values are design parameters.</summary>
    [Serializable]
    public sealed class OpponentStyle
    {
        public double aggression = 0.5, preferredDistance = 2.2, reaction = 0.3, defense = 0.5, counter = 0.4;
        public double[] mix = { 0.45, 0.3, 0.2, 0.05 }; // men, kote, do, tsuki
        public double jitter = 0.15, retreatAfter = 0.3, feint = 0.0, combo = 0.0, debana = 0.0, hiki = 0.0;

        public static OpponentStyle Easy() => new OpponentStyle { aggression = 0.25, reaction = 0.35, defense = 0.2, counter = 0.1, preferredDistance = 2.4, jitter = 0.1 };
        public static OpponentStyle Medium() => new OpponentStyle { aggression = 0.45, reaction = 0.2, defense = 0.45, counter = 0.35, preferredDistance = 2.2 };
        public static OpponentStyle Hard() => new OpponentStyle { aggression = 0.6, reaction = 0.15, defense = 0.6, counter = 0.55, preferredDistance = 2.1, combo = 0.2 };

        /// <summary>The former student: aggressive, feints, renzoku-waza, debana-kote, hiki-waza.</summary>
        public static OpponentStyle FormerStudent() => new OpponentStyle
        {
            aggression = 0.75, reaction = 0.1, defense = 0.55, counter = 0.65, preferredDistance = 2.0,
            mix = new[] { 0.4, 0.35, 0.15, 0.10 }, feint = 0.3, combo = 0.4, debana = 0.35, hiki = 0.4, jitter = 0.25,
        };

        public OpponentStyle Clone()
        {
            var c = (OpponentStyle)MemberwiseClone();
            c.mix = (double[])mix.Clone();
            return c;
        }
    }

    /// <summary>Agent 1 – rule-based opponent. Exact port of Tools/AI/policies.py RuleBasedPolicy.</summary>
    public sealed class RuleBasedPolicy : IDuelPolicy
    {
        public OpponentStyle style;
        private readonly KRandom rng;
        private double reactTimer;
        private bool seenStrike, lastWasStrike;
        private int feinting;

        public RuleBasedPolicy(OpponentStyle style, KRandom rng)
        {
            this.style = style;
            this.rng = rng;
        }

        public string Name => "Rule-based";

        public static int ArgMax(double[] o, int offset, int count)
        {
            int best = 0;
            for (int k = 1; k < count; k++) if (o[offset + k] > o[offset + best]) best = k;
            return best;
        }

        private DuelAction PickStrike(double d)
        {
            var m = (double[])style.mix.Clone();
            if (d > 1.95) m[2] *= 0.3;
            if (d < 1.6) { m[0] *= 0.6; m[1] *= 1.4; }
            double tot = m[0] + m[1] + m[2] + m[3];
            double r = rng.Value() * tot;
            var order = new[] { DuelAction.Men, DuelAction.Kote, DuelAction.Do, DuelAction.Tsuki };
            for (int k = 0; k < 4; k++)
            {
                r -= m[k];
                if (r <= 0) return order[k];
            }
            return DuelAction.Men;
        }

        public DuelAction Act(double[] o)
        {
            var st = style;
            double d = o[0] * 3.0;
            var my = (DuelPhase)ArgMax(o, 4, 6);
            var op = (DuelPhase)ArgMax(o, 10, 6);
            if (my != DuelPhase.Ready && my != DuelPhase.Blocking) return DuelAction.Wait;
            bool counterWindow = o[22] > 0.5;
            if (op == DuelPhase.Striking && o[17] < 0.45)
            {
                if (!seenStrike) { seenStrike = true; reactTimer = 0.0; }
                reactTimer += 0.1;
                if (reactTimer >= st.reaction)
                {
                    if (st.debana > 0 && o[17] < 0.35 && rng.Value() < st.debana && d < 2.3) return DuelAction.Kote;
                    if (rng.Value() < st.defense) return o[20] > 0.5 ? DuelAction.BlockDo : DuelAction.BlockHigh;
                }
            }
            else
            {
                seenStrike = false;
            }
            if (counterWindow && rng.Value() < st.counter) return PickStrike(d);
            if ((op == DuelPhase.Recovering || op == DuelPhase.Stunned) && d >= 1.2 && d <= 2.3 && rng.Value() < st.counter) return PickStrike(d);
            if (lastWasStrike && st.combo > 0 && rng.Value() < st.combo && d < 2.0)
            {
                lastWasStrike = false;
                return DuelAction.Men;
            }
            lastWasStrike = false;
            bool inRange = d >= 1.5 && d <= 2.45;
            if (inRange && rng.Value() < st.aggression * 0.35)
            {
                if (st.feint > 0 && feinting == 0 && rng.Value() < st.feint)
                {
                    feinting = 2;
                    return DuelAction.StepForward;
                }
                lastWasStrike = true;
                feinting = Math.Max(0, feinting - 1);
                return PickStrike(d);
            }
            if (d < 1.4)
            {
                if (st.hiki > 0 && rng.Value() < st.hiki) { lastWasStrike = true; return DuelAction.Men; }
                return rng.Value() < 0.6 ? DuelAction.StepBack : DuelAction.Wait;
            }
            if (feinting > 0) { feinting--; return PickStrike(d); }
            if (o[1] < -0.85) return DuelAction.StepForward;
            if (d > st.preferredDistance + 0.25 && rng.Value() < 0.5 + st.aggression * 0.4) return DuelAction.StepForward;
            if (d < st.preferredDistance - 0.35 && rng.Value() < 0.4) return DuelAction.StepBack;
            if (rng.Value() < st.jitter)
            {
                var moves = new[] { DuelAction.StepLeft, DuelAction.StepRight, DuelAction.StepForward, DuelAction.StepBack };
                return moves[rng.Int(4)];
            }
            return DuelAction.Wait;
        }
    }
}
