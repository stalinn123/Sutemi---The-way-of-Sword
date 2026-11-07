using System.Collections.Generic;
using UnityEngine;

namespace Kendo.Data
{
    /// <summary>Ordered list of levels and the opening/ending story beats (GDD §9 / §14).</summary>
    [CreateAssetMenu(menuName = "Kendo/Data/Campaign", fileName = "Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [SerializeField] private string title = "Kendo: The Master's Legacy";
        [SerializeField] private ConversationDefinition opening;
        [SerializeField] private ConversationDefinition tutorial;
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [Tooltip("Granted after Level 4 (GDD §8).")]
        [SerializeField] private string graduationTitle = "KE Perfect Student";
        [SerializeField] private ConversationDefinition finalMission;
        [SerializeField] private ConversationDefinition ending;

        public string Title => title;
        public ConversationDefinition Opening => opening;
        public ConversationDefinition Tutorial => tutorial;
        public IReadOnlyList<LevelDefinition> Levels => levels;
        public string GraduationTitle => graduationTitle;
        public ConversationDefinition FinalMission => finalMission;
        public ConversationDefinition Ending => ending;
    }
}
