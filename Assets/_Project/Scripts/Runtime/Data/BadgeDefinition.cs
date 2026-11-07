using UnityEngine;

namespace Kendo.Data
{
    /// <summary>A progression badge (GDD §7). Badges are persistent and shown in the dojo badge area.</summary>
    [CreateAssetMenu(menuName = "Kendo/Data/Badge Definition", fileName = "Badge_")]
    public sealed class BadgeDefinition : ScriptableObject
    {
        [SerializeField] private string badgeId;
        [SerializeField] private int order;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [Tooltip("Prefab built from the supplied badge FBX. Empty if no model has been supplied yet.")]
        [SerializeField] private GameObject displayPrefab;
        [SerializeField] private Sprite icon;

        public string BadgeId => badgeId;
        public int Order => order;
        public string DisplayName => displayName;
        public string Description => description;
        public GameObject DisplayPrefab => displayPrefab;
        public Sprite Icon => icon;
        public bool HasModel => displayPrefab != null;
    }
}
