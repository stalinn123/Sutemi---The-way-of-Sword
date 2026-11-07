using System;

namespace Kendo.Rules
{
    /// <summary>
    /// Tournament rules. Every switch cites the FIK "Regulations of Kendo Shiai and Shinpan" article it represents;
    /// items marked (verify) are paraphrased and must be checked against the printed regulations before release.
    /// </summary>
    [Serializable]
    public sealed class KendoRuleConfig
    {
        public string name = "FIK standard";
        /// <summary>Art. 13: tsuki is a valid target. Commonly disabled for beginners/juniors (design choice per event).</summary>
        public bool allowTsuki = true;
        /// <summary>Art. 13 / Subsidiary Art. 10: left kote counts only when the opponent is not in chudan (e.g. jodan).</summary>
        public bool leftKoteOnlyAgainstNonChudan = true;
        /// <summary>Art. 7: sanbon-shobu, two ippon win. Set to 1 for ippon-shobu.</summary>
        public int pointsToWin = 2;
        /// <summary>Art. 6: standard match time is 5 minutes (configurable per event).</summary>
        public float matchSeconds = 300f;
        /// <summary>Art. 7(2)/Art. 9: encho (first point wins) when tied and a winner is required.</summary>
        public bool encho = true;
        /// <summary>0 = unlimited encho.</summary>
        public float enchoSeconds = 0f;
        /// <summary>Art. 7: when time expires with one ippon, that fighter wins.</summary>
        public bool leaderWinsAtTime = true;
        /// <summary>Art. 9 hantei (judges' decision) after encho time, if encho is limited.</summary>
        public bool hantei = true;
        /// <summary>Art. 17/18: two hansoku give the opponent one ippon.</summary>
        public int hansokuPerIppon = 2;
        /// <summary>Art. 17: stepping out of the court (jogai) is a hansoku.</summary>
        public bool jogaiIsHansoku = true;
        /// <summary>Art. 17: dropping the shinai is a hansoku.</summary>
        public bool droppedShinaiIsHansoku = true;
        /// <summary>Art. 26: yuko-datotsu needs two or more flags; (verify) one flag counts when the other two abstain.</summary>
        public bool oneFlagWithAbstentionsCounts = true;
        /// <summary>Subsidiary Art. 24: excessive celebration revokes a point (torikeshi).</summary>
        public bool revokeOnCelebration = true;
        /// <summary>Ai-uchi (simultaneous valid strikes) produce no point (shinpan practice).</summary>
        public bool aiUchiNoPoint = true;

        public static KendoRuleConfig FikStandard() => new KendoRuleConfig();

        public static KendoRuleConfig BeginnerBout() => new KendoRuleConfig
        {
            name = "Beginner bout (no tsuki)", allowTsuki = false, matchSeconds = 180f, enchoSeconds = 120f,
        };

        public static KendoRuleConfig TrainingBout() => new KendoRuleConfig
        {
            name = "Training bout", allowTsuki = true, matchSeconds = 240f, enchoSeconds = 120f,
        };

        public static KendoRuleConfig IpponShobu() => new KendoRuleConfig { name = "Ippon-shobu", pointsToWin = 1 };

        public KendoRuleConfig Clone() => (KendoRuleConfig)MemberwiseClone();
    }

    /// <summary>
    /// Measurement thresholds. These are DESIGN values (not FIK rules): the regulations define what a valid strike is,
    /// not how many degrees of hasuji error a judge tolerates.
    /// </summary>
    [Serializable]
    public sealed class KendoEvaluationProfile
    {
        public string name = "Intermediate";
        public float monouchiStart = 0.70f;      // fraction of blade length from the grip end
        public float maxHasujiDeg = 30f;
        public float minTipSpeed = 4.0f;         // m/s
        public float goodTipSpeed = 8.0f;
        public float kiaiEarliest = -0.40f;      // s before impact
        public float kiaiLatest = 0.20f;
        public float kktiWindow = 0.15f;         // |offset| of kiai and fumikomi for ki-ken-tai-itchi
        public bool requireFumikomiForKkti = true;
        public float maxTorsoTiltDeg = 20f;
        public float minFollowThrough = 0.25f;   // m
        public float maxKamaeRestore = 1.6f;     // s
        public float idealDistanceMin = 1.55f;   // m, centre-to-centre when the strike starts
        public float idealDistanceMax = 2.45f;
        public float minApproachSpeed = 0.25f;   // m/s
        public float contactOnlyScoreCap = 20f;
        public float passScore = 70f;            // training pass mark for lessons

        // kamae
        public float maxKensenOffset = 0.12f;    // m from the opponent's throat line
        public float maxKensenLateralDeg = 8f;
        public float leftHandForwardMin = 0.06f; // m in front of the navel
        public float leftHandForwardMax = 0.22f;
        public float leftHandHeightTol = 0.10f;
        public float stanceLengthMin = 0.10f;    // right foot ahead of left
        public float stanceLengthMax = 0.35f;
        public float stanceWidthMax = 0.20f;
        public float stillSeconds = 1.5f;

        // rei
        public float bowTolDeg = 8f;
        public float bowHoldMin = 0.7f;
        public float facingTolDeg = 15f;

        // suriashi
        public float markerTol = 0.18f;
        public float maxGazePitchDeg = 20f;

        public static KendoEvaluationProfile Beginner() => new KendoEvaluationProfile
        {
            name = "Beginner", maxHasujiDeg = 38f, minTipSpeed = 3.2f, kktiWindow = 0.22f, maxTorsoTiltDeg = 25f,
            idealDistanceMin = 1.45f, idealDistanceMax = 2.6f, maxKamaeRestore = 2.2f, minFollowThrough = 0.15f,
            maxKensenOffset = 0.16f, maxKensenLateralDeg = 11f, passScore = 65f, bowTolDeg = 10f, markerTol = 0.22f,
        };

        public static KendoEvaluationProfile Intermediate() => new KendoEvaluationProfile();

        public static KendoEvaluationProfile Shiai() => new KendoEvaluationProfile
        {
            name = "Shiai", maxHasujiDeg = 24f, minTipSpeed = 5.0f, kktiWindow = 0.12f, maxTorsoTiltDeg = 16f,
            maxKamaeRestore = 1.3f, minFollowThrough = 0.3f, passScore = 75f,
        };

        public KendoEvaluationProfile Clone() => (KendoEvaluationProfile)MemberwiseClone();
    }
}
