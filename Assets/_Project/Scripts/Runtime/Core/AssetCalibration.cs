using UnityEngine;

namespace Kendo.Core
{
    /// <summary>
    /// Scale calibration derived from inspecting the supplied FBX files (see Docs/AssetInspectionReport.md).
    /// Every Blender export uses UnitScaleFactor = 1 (cm), so Unity's file scale is 0.01 and the values
    /// below are the extra ModelImporter "Scale Factor" needed to reach real-world metres (1 unit = 1 m, required for VR).
    /// </summary>
    public static class AssetCalibration
    {
        /// <summary>
        /// The Kendo master and the final environment were both authored ~2.44x life size:
        /// the master measures ~4.4 m tall, and the temple inside FinalEnvironment is 2.44x the
        /// same temple in Temple_HoryujiReplica (which is already at real scale). 1 / 2.44 ≈ 0.41.
        /// </summary>
        public const float AuthoringScaleCorrection = 0.41f;

        public const float KendoMasterScale = AuthoringScaleCorrection;   // 4.41 m -> ~1.81 m incl. straw hat
        public const float FinalEnvironmentScale = AuthoringScaleCorrection;
        public const float MenHelmetScale = 0.16f;                         // 3.3 m tall source -> ~0.53 m incl. throat guard
        public const float BadgeScale = 0.025f;                            // 5.33 m disc -> ~0.13 m badge
        public const float TrainingDojoScale = 1f;                         // already real scale (17.2 x 13.2 m foundation)
        public const float TempleScale = 1f;                               // already real scale (~21 x 18 m)

        /// <summary>Top of the floorboards in TrainingDojo_Interior (platform 0.42 m + 0.03 m boards).</summary>
        public const float TrainingDojoFloorHeight = 0.45f;

        // Acceptance ranges used by the validator and edit-mode tests.
        public static readonly Vector2 MasterHeightRange = new Vector2(1.60f, 1.95f);
        public static readonly Vector2 ShinaiLengthRange = new Vector2(1.00f, 1.40f);
        public static readonly Vector2 BadgeDiameterRange = new Vector2(0.08f, 0.20f);
    }
}
