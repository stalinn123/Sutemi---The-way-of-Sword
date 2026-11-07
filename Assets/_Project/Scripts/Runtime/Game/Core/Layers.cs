using Kendo.Core;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>Runtime layer indices (created by Kendo ▸ Build Game; fall back to Default so nothing breaks).</summary>
    public static class Layers
    {
        private static int L(string name) { int i = LayerMask.NameToLayer(name); return i < 0 ? 0 : i; }
        public static int Environment => L(KendoLayers.Environment);
        public static int Characters => L(KendoLayers.Characters);
        public static int Shinai => L(KendoLayers.Shinai);

        /// <summary>Solid geometry the player and the shinai collide with (Environment layer, plus Default if the layer is missing).</summary>
        public static LayerMask EnvironmentMask
        {
            get
            {
                int i = LayerMask.NameToLayer(KendoLayers.Environment);
                return i < 0 ? (LayerMask)1 : (LayerMask)(1 << i);
            }
        }
    }
}
