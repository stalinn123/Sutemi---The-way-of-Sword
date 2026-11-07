using UnityEngine;

namespace Kendo.Game
{
    /// <summary>Marker placed on the environment instance by the scene builder (holds the generated colliders).</summary>
    public sealed class EnvironmentRoot : MonoBehaviour
    {
        public EnvironmentKind kind;
    }
}
