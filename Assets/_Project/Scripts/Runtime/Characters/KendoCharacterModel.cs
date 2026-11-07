using UnityEngine;

namespace Kendo.Characters
{
    public enum CharacterRole
    {
        Master,
        Opponent,
        FormerStudent,
        TrainingAvatar,
    }

    /// <summary>
    /// The supplied KendoMaster FBX contains no armature, skin or animation (see AssetInspectionReport).
    /// The prefab is therefore marked Unrigged until the mesh is rigged and re-exported.
    /// </summary>
    public enum CharacterRigState
    {
        Unrigged,
        Generic,
        Humanoid,
    }

    /// <summary>
    /// Integration point between an imported character model and the game's systems.
    /// Holds references only (model root, sockets, animator); it implements no behaviour.
    /// Sockets are authored by Kendo > Setup > Build Prefabs. On an unrigged mesh they are
    /// estimated from mesh bounds; once a rig exists they should be re-parented to hand/head bones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KendoCharacterModel : MonoBehaviour
    {
        [SerializeField] private CharacterRole role = CharacterRole.Master;
        [SerializeField] private CharacterRigState rigState = CharacterRigState.Unrigged;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;

        [Header("Sockets")]
        [SerializeField] private Transform headSocket;
        [SerializeField] private Transform rightHandSocket;
        [SerializeField] private Transform leftHandSocket;
        [Tooltip("Where the character's own shinai rests when not held (authored pose from the source file).")]
        [SerializeField] private Transform shinaiRestSocket;

        [Header("Measured at prefab build time")]
        [SerializeField] private float measuredHeight;

        public CharacterRole Role => role;
        public CharacterRigState RigState => rigState;
        public Transform ModelRoot => modelRoot;
        public Animator Animator => animator;
        public Transform HeadSocket => headSocket;
        public Transform RightHandSocket => rightHandSocket;
        public Transform LeftHandSocket => leftHandSocket;
        public Transform ShinaiRestSocket => shinaiRestSocket;
        public float MeasuredHeight => measuredHeight;
        public bool CanPlayHumanoidAnimation => rigState == CharacterRigState.Humanoid && animator != null && animator.isHuman;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawSocket(headSocket, Color.cyan);
            DrawSocket(rightHandSocket, Color.green);
            DrawSocket(leftHandSocket, Color.yellow);
            DrawSocket(shinaiRestSocket, Color.magenta);
        }

        private static void DrawSocket(Transform socket, Color color)
        {
            if (socket == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(socket.position, 0.04f);
            Gizmos.DrawLine(socket.position, socket.position + socket.forward * 0.12f);
        }
#endif
    }
}
