using UnityEngine;

namespace Kendo.Characters
{
    /// <summary>
    /// Reference frame for the supplied shinai (bamboo sword) mesh extracted from KendoMaster_And_Shinai.fbx.
    /// The prefab root sits at the butt of the grip (tsuka-gashira) with +Y pointing to the tip (kensen),
    /// which is the frame an XR hand attach point and future strike detection will use. No behaviour here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShinaiModel : MonoBehaviour
    {
        [SerializeField] private Transform gripEnd;
        [SerializeField] private Transform tsuba;
        [SerializeField] private Transform tip;
        [SerializeField] private float length;

        [Tooltip("Normalised position along the blade where the valid striking area (monouchi) begins.")]
        [SerializeField, Range(0f, 1f)] private float monouchiStart = 0.67f;

        public Transform GripEnd => gripEnd;
        public Transform Tsuba => tsuba;
        public Transform Tip => tip;
        public float Length => length;
        public float MonouchiStart => monouchiStart;

        public Vector3 BladeDirection =>
            gripEnd != null && tip != null ? (tip.position - gripEnd.position).normalized : transform.up;

        public Vector3 PointAlongBlade(float normalized) =>
            gripEnd != null && tip != null ? Vector3.Lerp(gripEnd.position, tip.position, normalized) : transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (gripEnd == null || tip == null) return;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(gripEnd.position, tip.position);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(PointAlongBlade(monouchiStart), tip.position);
            if (tsuba != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(tsuba.position, 0.04f);
            }
        }
#endif
    }
}
