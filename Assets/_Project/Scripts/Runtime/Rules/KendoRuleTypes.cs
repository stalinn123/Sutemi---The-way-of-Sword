using System;
using System.Collections.Generic;

namespace Kendo.Rules
{
    /// <summary>Datotsu-bui: striking targets on the opponent's kendo-gu (FIK Regulations Art. 13).</summary>
    public enum Target
    {
        None = 0,
        Men = 1,        // shomen
        MenRight = 2,   // sayu-men (above the temple)
        MenLeft = 3,
        KoteRight = 4,
        KoteLeft = 5,   // valid only when the opponent's kamae is not chudan (Art. 13 / Subsidiary Art. 9 interpretation, configurable)
        DoRight = 6,
        DoLeft = 7,     // gyaku-do
        Tsuki = 8,
    }

    public enum Kamae
    {
        Chudan = 0,
        Jodan = 1,
        Gedan = 2,
        Hasso = 3,
        Waki = 4,
        Sageto = 5,
        None = 6,
    }

    /// <summary>Every criterion the engine evaluates. "Official" ones decide yuko-datotsu (Art. 12);
    /// the others are coaching criteria that only change the training score and feedback.</summary>
    public enum Criterion
    {
        DeliberateStrike = 0, // Art. 12 "strike or thrust" – not an accidental contact
        ValidTarget = 1,      // Art. 13 datotsu-bui
        StrikingPart = 2,     // Art. 13 datotsu-bu: monouchi, jinbu side
        Hasuji = 3,           // Art. 12 correct hasuji
        Spirit = 4,           // Art. 12 "in high spirits" – kiai
        Posture = 5,          // Art. 12 "correct posture"
        Zanshin = 6,          // Art. 12 "followed by zanshin"
        KiKenTaiItchi = 7,    // coaching: spirit, sword and body together
        Distance = 8,         // coaching: maai
        Timing = 9,           // coaching: opportunity (datotsu no kikai)
        BodyDrive = 10,       // coaching: body moving into the strike
        Sharpness = 11,       // coaching: tip speed / snap (sae)
        // kamae / rei / suriashi
        Grip = 12,
        KensenLine = 13,
        LeftHand = 14,
        Feet = 15,
        Stillness = 16,
        BowDepth = 17,
        BowHold = 18,
        Facing = 19,
        ShinaiAtSide = 20,
        Gaze = 21,
        OkuriAshi = 22,
        KamaeKept = 23,
        Accuracy = 24,
        Control = 25,
    }

    public enum InvalidReason
    {
        None = 0,
        ContactOnly = 1,      // shinai touched the target without a deliberate strike
        TargetNotAllowed = 2,
        NotMonouchi = 3,
        WrongSide = 4,        // struck with the tsuru / flat side
        BadHasuji = 5,
        NoSpirit = 6,
        BadPosture = 7,
        NoZanshin = 8,
        Blocked = 9,
        AiUchi = 10,
        OpponentAlreadyDefenceless = 11, // Subsidiary Art. 11 situations are configurable
        Missed = 12,
    }

    /// <summary>Datotsu no kikai (striking opportunities) recognised by the coaching criteria.</summary>
    public enum Opportunity
    {
        None = 0,
        Debana = 1,          // as the opponent starts to move
        AfterBlock = 2,      // oji-waza after receiving
        OpponentRetreating = 3,
        OpponentStatic = 4,
        AfterOpponentStrike = 5, // waza no tsukita tokoro – end of the opponent's technique
        IntoReadyKamae = 6,  // attacking straight into a settled, defended kamae (poor)
    }

    public enum FeedbackTopic
    {
        None, Seme, Target, Monouchi, Hasuji, Kiai, Posture, Zanshin, KiKenTaiItchi, Maai, Timing, BodyDrive, Sharpness,
        Grip, Kensen, LeftHand, Feet, Stillness, BowDepth, BowHold, Facing, Sageto, Metsuke, OkuriAshi, Kamae, Accuracy, Control
    }

    /// <summary>Everything measured about one contact. Built from the real shinai trajectory, footwork and input timings
    /// by the game (Kendo.Game.StrikeSensor) and by the duel simulator; never from a button press alone.</summary>
    [Serializable]
    public sealed class StrikeEvidence
    {
        public Target target = Target.None;
        public bool contact;                 // the blade actually touched a target zone
        public float contactAlongBlade;      // 0 = grip end, 1 = kensen
        public bool edgeSide = true;         // contact on the jinbu side (not the tsuru, not the flat)
        public float hasujiErrorDeg;         // angle between cutting edge and travel direction
        public float tipSpeed;               // m/s at contact
        public bool strikeArc;               // a real swing / thrust happened (raise and cut, or extension)
        public float approachSpeed;          // body speed toward the target at impact (m/s)
        public bool kiai;
        public float kiaiOffset = 99f;       // seconds relative to impact (negative = before)
        public bool fumikomi;
        public float fumikomiOffset = 99f;
        public bool bothHands = true;
        public float torsoTiltDeg;
        public bool balanced = true;
        public float distance;               // centre-to-centre distance between fighters at the start of the strike (m)
        public Opportunity opportunity = Opportunity.None;
        public bool blocked;                 // the opponent's shinai or kensen intercepted the strike
        public bool opponentDefenceless;     // fallen / dropped shinai (Subsidiary Art. 11)
        public bool simultaneousWithOpponent; // ai-uchi candidate
        // zanshin (filled after the follow-up window)
        public bool zanshinObserved;
        public float followThrough;          // metres moved past / away after the strike while staying alert
        public float kamaeRestoredAfter = 99f; // seconds until chudan with kensen on the opponent again
        public bool turnedAway;              // turned the back without readiness / lowered the shinai
        public bool celebrated;              // guts pose – FIK Subsidiary Art. 24 example of a strike to be revoked
    }

    [Serializable]
    public struct CriterionResult
    {
        public Criterion criterion;
        public bool official;
        public bool passed;
        public float score;          // 0..1
        public FeedbackTopic topic;
        public string detail;        // measured value in words, e.g. "contact at 41 % of the blade"

        public CriterionResult(Criterion c, bool official, bool passed, float score, FeedbackTopic topic, string detail)
        {
            criterion = c; this.official = official; this.passed = passed; this.score = score < 0f ? 0f : (score > 1f ? 1f : score);
            this.topic = topic; this.detail = detail;
        }
    }

    /// <summary>Result of any evaluation: kamae, rei, suriashi or strike.</summary>
    public class Evaluation
    {
        public readonly List<CriterionResult> criteria = new List<CriterionResult>();
        /// <summary>Training performance 0..100. Never used for match decisions.</summary>
        public float performanceScore;
        public bool passed;

        public CriterionResult? Get(Criterion c)
        {
            foreach (var r in criteria) if (r.criterion == c) return r;
            return null;
        }

        public IEnumerable<CriterionResult> Failed()
        {
            foreach (var r in criteria) if (!r.passed) yield return r;
        }

        public IEnumerable<CriterionResult> Passed()
        {
            foreach (var r in criteria) if (r.passed) yield return r;
        }
    }

    public sealed class StrikeEvaluation : Evaluation
    {
        /// <summary>All official criteria met (Art. 12). A judge panel still has to raise flags.</summary>
        public bool yukoDatotsu;
        public InvalidReason invalidReason;
        public Target target;
        /// <summary>Smallest margin over the official thresholds (−1..1). Judges compare it with their own strictness.</summary>
        public float officialMargin;
    }
}
