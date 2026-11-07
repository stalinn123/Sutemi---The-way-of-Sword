using UnityEngine;

namespace Kendo.Data
{
    /// <summary>
    /// Difficulty knobs from GDD §12: "difficulty should increase through opponent speed, reaction time,
    /// attack patterns, defense, and counterattack ability". Values are designer starting points and are
    /// meant to be adjusted at runtime by the personalised DDA layer (see Scripts/Runtime/AI/README.md).
    /// </summary>
    [CreateAssetMenu(menuName = "Kendo/Data/Opponent Profile", fileName = "Opponent_")]
    public sealed class OpponentProfile : ScriptableObject
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField] private DifficultyTier tier;

        [Header("Baseline difficulty (0 = trivial, 1 = master level)")]
        [SerializeField, Range(0f, 1f)] private float movementSpeed = 0.3f;
        [SerializeField, Min(0.05f)] private float reactionTimeSeconds = 0.9f;
        [SerializeField, Range(0f, 1f)] private float attackPatternComplexity = 0.2f;
        [SerializeField, Range(0f, 1f)] private float defenseSkill = 0.2f;
        [SerializeField, Range(0f, 1f)] private float counterattackAbility = 0f;

        [Header("Personalised dynamic difficulty (Fuchs et al., 2024)")]
        [SerializeField] private bool allowAdaptiveDifficulty = true;
        [Tooltip("How far adaptation may move each knob away from its baseline.")]
        [SerializeField, Range(0f, 0.5f)] private float adaptationRange = 0.15f;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public DifficultyTier Tier => tier;
        public float MovementSpeed => movementSpeed;
        public float ReactionTimeSeconds => reactionTimeSeconds;
        public float AttackPatternComplexity => attackPatternComplexity;
        public float DefenseSkill => defenseSkill;
        public float CounterattackAbility => counterattackAbility;
        public bool AllowAdaptiveDifficulty => allowAdaptiveDifficulty;
        public float AdaptationRange => adaptationRange;
    }
}
