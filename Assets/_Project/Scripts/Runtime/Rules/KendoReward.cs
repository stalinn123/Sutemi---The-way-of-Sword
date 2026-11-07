namespace Kendo.Rules
{
    /// <summary>
    /// Reinforcement-learning reward derived only from engine outputs (paper: reward for reducing the opponent's
    /// "hit points", penalty for losing one's own – here ippon replace hit points). Shaping terms are design values.
    /// </summary>
    public static class KendoReward
    {
        public const float Ippon = 1.0f;
        public const float Conceded = -1.0f;
        public const float Win = 1.0f;
        public const float Loss = -1.0f;
        public const float HansokuPenalty = -0.3f;
        public const float StepCost = -0.001f;
        public const float ValidButNotAwarded = 0.05f;
        /// <summary>Strike that reached no target (teaches distance). Design value.</summary>
        public const float Whiff = -0.02f;

        /// <summary>Reward for one of our strikes after the panel's decision.</summary>
        public static float ForOwnStrike(StrikeEvaluation ev, IpponDecision d)
        {
            if (d.awarded) return Ippon;
            if (ev == null || ev.invalidReason == InvalidReason.ContactOnly || ev.invalidReason == InvalidReason.Missed) return 0f;
            // partial credit proportional to how many official criteria were met, never more than a fraction of an ippon
            int ok = 0, n = 0;
            foreach (var c in ev.criteria) if (c.official) { n++; if (c.passed) ok++; }
            return n == 0 ? 0f : ValidButNotAwarded * ok / n;
        }

        /// <summary>Shaping for a strike that was not awarded (the ippon itself is rewarded through the score).</summary>
        public static float Shaping(StrikeEvaluation ev, IpponDecision d)
        {
            if (d.awarded) return 0f;
            return ev != null && ev.invalidReason == InvalidReason.Missed ? Whiff : ForOwnStrike(ev, d);
        }

        public static float ForOpponentIppon() => Conceded;
        public static float ForHansoku() => HansokuPenalty;
        public static float ForMatchEnd(int winner, int self) => winner < 0 ? 0f : (winner == self ? Win : Loss);
    }
}
