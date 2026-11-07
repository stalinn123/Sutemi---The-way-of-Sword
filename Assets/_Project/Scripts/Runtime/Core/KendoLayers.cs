using UnityEngine;

namespace Kendo.Core
{
    /// <summary>
    /// Physics/render layer names reserved for the game. Created by Kendo > Setup > Configure Project Settings.
    /// Indices are looked up by name at runtime so the project never hard-codes layer numbers.
    /// </summary>
    public static class KendoLayers
    {
        public const string Environment = "Environment";
        public const string Characters = "Characters";
        public const string Shinai = "Shinai";
        public const string HitZone = "HitZone";
        public const string XRInteractable = "XRInteractable";
        public const string Teleport = "Teleport";

        public static readonly string[] All = { Environment, Characters, Shinai, HitZone, XRInteractable, Teleport };

        /// <summary>First user layer slot the setup tool will fill (slots 8–31).</summary>
        public const int FirstReservedIndex = 8;

        public static int IndexOf(string layerName) => LayerMask.NameToLayer(layerName);
    }
}
