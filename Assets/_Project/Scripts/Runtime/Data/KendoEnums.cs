using System;

namespace Kendo.Data
{
    /// <summary>GDD §5 difficulty ladder.</summary>
    public enum DifficultyTier
    {
        Easy = 0,
        Medium = 1,
        MediumHard = 2,
        Hard = 3,
        VeryHard = 4,
    }

    /// <summary>Skills named as "Primary Skills" per level in GDD §5.</summary>
    [Flags]
    public enum KendoSkill
    {
        None = 0,
        Movement = 1 << 0,
        Stance = 1 << 1,
        BasicAttack = 1 << 2,
        BasicDefense = 1 << 3,
        Timing = 1 << 4,
        Defense = 1 << 5,
        AttackCombinations = 1 << 6,
        Positioning = 1 << 7,
        Strategy = 1 << 8,
        Observation = 1 << 9,
        Counterattack = 1 << 10,
        Reaction = 1 << 11,
        AdvancedTiming = 1 << 12,
        CompleteSkillSet = 1 << 13,
    }

    /// <summary>
    /// Movements taught in the tutorial (GDD §4.2), using standard kendo terms.
    /// Only identifiers here – evaluation logic belongs to the Training module.
    /// </summary>
    public enum KendoTechnique
    {
        ChudanNoKamae,      // basic stance, shinai held centre-line
        ShinaiGrip,         // holding and positioning the shinai
        SuriashiForward,
        SuriashiBackward,
        SuriashiLeft,
        SuriashiRight,
        MenUchi,            // head strike – the basic attack motion
        KoteUchi,           // wrist strike
        DoUchi,             // torso strike
        Tsuki,              // throat thrust (advanced)
        Uke,                // block / defensive receive
        OjiWaza,            // counterattack
        Zanshin,            // reset / recover and remain alert after an action
    }

    public enum Speaker
    {
        Master,
        FormerStudent,
        Player,
        Narrator,
    }

    /// <summary>Conversation moments listed in GDD §13.2.</summary>
    public enum ConversationTrigger
    {
        Opening,
        Tutorial,
        PreLevel,
        PostVictory,
        PostDefeat,
        ReadyForUltimateTest,
        FinalMission,
        FormerStudentEncounter,
        Ending,
    }

    /// <summary>Correction categories the master uses after a failed level (GDD §6.2).</summary>
    public enum FailureReason
    {
        PoorTiming,
        TooAggressive,
        FailedToBlock,
        OpeningAfterAttack,
        IncorrectPositioning,
        MissedAttackPattern,
    }
}
