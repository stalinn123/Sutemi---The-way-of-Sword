using UnityEngine;

namespace Kendo.Characters
{
    public enum EquipmentSlot
    {
        Men,   // helmet
        Kote,  // gloves
        Do,    // breastplate
        Tare,  // waist protector
    }

    /// <summary>Marks a bogu (armour) prefab and the slot it occupies. Data only.</summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentPiece : MonoBehaviour
    {
        [SerializeField] private EquipmentSlot slot = EquipmentSlot.Men;
        [Tooltip("Local offset applied when attached to a character's head socket.")]
        [SerializeField] private Vector3 attachOffset;

        public EquipmentSlot Slot => slot;
        public Vector3 AttachOffset => attachOffset;
    }
}
