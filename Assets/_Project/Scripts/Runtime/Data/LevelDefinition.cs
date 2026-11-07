using Kendo.Core;
using UnityEngine;

namespace Kendo.Data
{
    /// <summary>One campaign level (GDD §5). Pure data – no gameplay logic.</summary>
    [CreateAssetMenu(menuName = "Kendo/Data/Level Definition", fileName = "Level_")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string levelId;
        [SerializeField] private int order;
        [SerializeField] private string displayName;
        [SerializeField] private DifficultyTier difficulty;
        [SerializeField, TextArea(2, 6)] private string purpose;
        [SerializeField] private KendoSkill primarySkills;
        [SerializeField] private KendoSceneId scene;
        [Tooltip("Story location from GDD §13.4.")]
        [SerializeField] private string locationDescription;
        [SerializeField] private BadgeDefinition rewardBadge;
        [SerializeField] private OpponentProfile opponent;

        [Header("Conversations")]
        [SerializeField] private ConversationDefinition briefing;
        [SerializeField] private ConversationDefinition victory;
        [SerializeField] private ConversationDefinition defeat;

        public string LevelId => levelId;
        public int Order => order;
        public string DisplayName => displayName;
        public DifficultyTier Difficulty => difficulty;
        public string Purpose => purpose;
        public KendoSkill PrimarySkills => primarySkills;
        public KendoSceneId Scene => scene;
        public string LocationDescription => locationDescription;
        public BadgeDefinition RewardBadge => rewardBadge;
        public OpponentProfile Opponent => opponent;
        public ConversationDefinition Briefing => briefing;
        public ConversationDefinition Victory => victory;
        public ConversationDefinition Defeat => defeat;
    }
}
