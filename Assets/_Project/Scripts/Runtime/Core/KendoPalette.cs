using UnityEngine;

namespace Kendo.Core
{
    /// <summary>
    /// UI colours sampled from the supplied mock-ups (Docs/Reference/UI_Mockup_*.png):
    /// deep indigo panel with sand-coloured distressed display type and pill buttons.
    /// </summary>
    public static class KendoPalette
    {
        public static readonly Color32 PanelIndigo = new Color32(0x00, 0x27, 0x3D, 0xFF); // #00273D
        public static readonly Color32 Sand = new Color32(0xE7, 0xB4, 0x7B, 0xFF);        // #E7B47B (text + buttons)
        public static readonly Color32 ButtonText = PanelIndigo;
        public static readonly Color32 Backdrop = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    }
}
