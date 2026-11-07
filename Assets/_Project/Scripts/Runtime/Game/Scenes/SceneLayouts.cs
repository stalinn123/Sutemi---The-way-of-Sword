using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    public enum EnvironmentKind { None, Dojo, Temple, Final }

    /// <summary>
    /// Positions measured from the supplied FBX files at their calibrated scale (Unity coordinates, metres;
    /// Tools/AssetPrep/kgeo.py + Docs/SceneLayout.md). Dojo floor 0.45 m (floorboards), taped court x ±4, z −3..5,
    /// kamiza/dais at z −5.4..−3.3; bogu stands at x 5.30 / 6.60, z −5.1; bokken rack x −7.06..−4.34, z −5.5.
    /// Temple hall floor 0.30 m, hall clear inside x ±5.5, z ±5.5, ceiling ≈ 2.58 m above the floor.
    /// Final shiai-jo (Plane.005) 18.3 × 15 m centred (−10.793, 4.345, 2.116), centre cross (−11.201, 2.121), axis along x.
    /// </summary>
    public sealed class SceneLayout
    {
        public EnvironmentKind environment;
        public Vector3 courtCenter;
        public Vector2 courtHalf;           // half extents along (x = across, y = along the fighting axis)
        public float courtYaw;              // rotation of the court frame (deg); the fighting axis is the frame's z
        public float startHalfDistance = 1.4f;
        public Vector3 playerSpawn;
        public float playerYaw;
        public Vector3 masterSpot;
        public float masterYaw;
        public Color sun = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1.2f;
        public Vector3 sunEuler = new Vector3(40f, -30f, 0f);
        public Color ambientSky = new Color(0.55f, 0.6f, 0.7f), ambientEquator = new Color(0.45f, 0.42f, 0.38f), ambientGround = new Color(0.2f, 0.18f, 0.15f);
        public bool fog;
        public Color fogColor = new Color(0.6f, 0.62f, 0.66f);
        public float fogDensity = 0.01f;
        public Vector3[] lanterns = new Vector3[0];
        public Color lanternColor = new Color(1f, 0.7f, 0.4f);

        public Quaternion CourtRotation => Quaternion.Euler(0f, courtYaw, 0f);
        public Vector3 PlayerStart => courtCenter + CourtRotation * new Vector3(0f, 0f, startHalfDistance);
        public Vector3 OpponentStart => courtCenter + CourtRotation * new Vector3(0f, 0f, -startHalfDistance);
        public float PlayerStartYaw => courtYaw + 180f;
        public float OpponentStartYaw => courtYaw;

        // ---------------------------------------------------------------- dojo points of interest
        public static readonly Vector3 DojoEntranceMark = new Vector3(0f, 0.45f, 5.2f);
        public static readonly Vector3 DojoMasterHome = new Vector3(0f, 0.45f, -2.3f);
        public static readonly Vector3 DojoReiToMasterMark = new Vector3(0f, 0.45f, -0.9f);
        public static readonly Vector3 DojoLevelBoard = new Vector3(-1.6f, 1.2f, 5.8f);
        public static readonly Vector3 DojoLessonBoard = new Vector3(5.7f, 1.4f, -4.5f);      // nafuda (name plaques) wall
        public static readonly Vector3 DojoBadgeRack = new Vector3(-5.7f, 1.3f, -5.15f);       // bokken rack
        /// <summary>Measured default; DojoDirector re-measures it from the stand's men mesh in the loaded scene.</summary>
        public static Vector3 DojoStandA = new Vector3(5.30f, 0.45f, -5.10f);
        public static readonly Vector3 DojoStandB = new Vector3(6.60f, 0.45f, -5.10f);
        public static Vector3 DojoStandPractice = new Vector3(5.30f, 0.45f, -3.30f);  // 1.8 m in front of stand A
        public static readonly Vector3 DojoMotodachi = new Vector3(0f, 0.45f, 0.4f);           // faces +z
        public static readonly Vector3 DojoPractice = new Vector3(0f, 0.45f, 2.2f);            // faces −z, 1.8 m from the motodachi
        public static readonly Vector3 DojoSuriashiStart = new Vector3(-2.0f, 0.45f, 3.0f);    // faces −z

        public static SceneLayout For(string scene)
        {
            switch (scene)
            {
                case KendoSceneNames.MainMenu:
                    return new SceneLayout { environment = EnvironmentKind.Dojo, courtCenter = new Vector3(0, 0.45f, 1f), playerSpawn = new Vector3(0f, 0.45f, 4.6f), playerYaw = 180f,
                        masterSpot = new Vector3(-0.6f, 0.45f, -0.6f), masterYaw = 165f, sunIntensity = 1.0f, sunEuler = new Vector3(25f, -60f, 0f),
                        lanterns = DojoLanterns };
                case KendoSceneNames.TrainingDojo:
                    return new SceneLayout { environment = EnvironmentKind.Dojo, courtCenter = new Vector3(0, 0.45f, 1f), courtHalf = new Vector2(4f, 4f),
                        playerSpawn = new Vector3(0f, 0.45f, 5.6f), playerYaw = 180f, masterSpot = DojoMasterHome, masterYaw = 0f,
                        sunIntensity = 1.25f, sunEuler = new Vector3(42f, -35f, 0f), lanterns = DojoLanterns };
                case KendoSceneNames.Level1:
                    return new SceneLayout { environment = EnvironmentKind.Dojo, courtCenter = new Vector3(0, 0.45f, 1f), courtHalf = new Vector2(4f, 4f), courtYaw = 0f,
                        masterSpot = new Vector3(-3.4f, 0.45f, 1f), masterYaw = 90f, sun = new Color(1f, 0.92f, 0.8f), sunIntensity = 1.35f, sunEuler = new Vector3(18f, 60f, 0f),
                        lanterns = DojoLanterns };
                case KendoSceneNames.Level2:
                    return new SceneLayout { environment = EnvironmentKind.Dojo, courtCenter = new Vector3(0, 0.45f, 1f), courtHalf = new Vector2(4f, 4f), courtYaw = 0f,
                        masterSpot = new Vector3(-3.4f, 0.45f, 1f), masterYaw = 90f, sun = new Color(1f, 0.62f, 0.38f), sunIntensity = 0.7f, sunEuler = new Vector3(8f, -110f, 0f),
                        fog = true, fogColor = new Color(0.35f, 0.28f, 0.25f), fogDensity = 0.012f, lanterns = DojoLanterns, ambientSky = new Color(0.35f, 0.3f, 0.35f) };
                case KendoSceneNames.Level3:
                    return new SceneLayout { environment = EnvironmentKind.Temple, courtCenter = new Vector3(0, 0.30f, 0f), courtHalf = new Vector2(4.5f, 4.5f), courtYaw = 0f,
                        masterSpot = new Vector3(-3.8f, 0.30f, 0f), masterYaw = 90f, sun = new Color(1f, 0.9f, 0.78f), sunIntensity = 1.1f, sunEuler = new Vector3(35f, 120f, 0f),
                        lanterns = TempleLanterns, ambientSky = new Color(0.5f, 0.5f, 0.55f) };
                case KendoSceneNames.Level4:
                    return new SceneLayout { environment = EnvironmentKind.Temple, courtCenter = new Vector3(0, 0.30f, 0f), courtHalf = new Vector2(5f, 4.8f), courtYaw = 0f,
                        masterSpot = new Vector3(-4.2f, 0.30f, 0f), masterYaw = 90f, sun = new Color(1f, 0.55f, 0.35f), sunIntensity = 0.6f, sunEuler = new Vector3(6f, 100f, 0f),
                        fog = true, fogColor = new Color(0.3f, 0.22f, 0.22f), fogDensity = 0.015f, lanterns = TempleLanterns, lanternColor = new Color(1f, 0.55f, 0.25f), ambientSky = new Color(0.3f, 0.25f, 0.3f) };
                case KendoSceneNames.FinalBattle:
                    return new SceneLayout { environment = EnvironmentKind.Final, courtCenter = new Vector3(-11.201f, 4.345f, 2.121f), courtHalf = new Vector2(7.4f, 9.0f), courtYaw = 90f,
                        masterSpot = new Vector3(-11.2f, 4.345f, 8.4f), masterYaw = 180f, sun = new Color(0.7f, 0.75f, 0.95f), sunIntensity = 0.45f, sunEuler = new Vector3(30f, 200f, 0f),
                        fog = true, fogColor = new Color(0.18f, 0.2f, 0.26f), fogDensity = 0.02f, ambientSky = new Color(0.22f, 0.24f, 0.32f),
                        lanterns = new[] { new Vector3(-3.0f, 5.6f, -4.6f), new Vector3(-3.0f, 5.6f, 8.8f), new Vector3(-19.0f, 5.6f, -4.6f), new Vector3(-19.0f, 5.6f, 8.8f) },
                        lanternColor = new Color(1f, 0.45f, 0.2f) };
                case KendoSceneNames.Ending:
                    return new SceneLayout { environment = EnvironmentKind.Dojo, courtCenter = new Vector3(0, 0.45f, 1f), playerSpawn = new Vector3(0f, 0.45f, 0.6f), playerYaw = 180f,
                        masterSpot = new Vector3(0f, 0.45f, -1.3f), masterYaw = 0f, sun = new Color(1f, 0.6f, 0.35f), sunIntensity = 0.9f, sunEuler = new Vector3(10f, -80f, 0f), lanterns = DojoLanterns };
                default:
                    return new SceneLayout { environment = EnvironmentKind.None };
            }
        }

        private static readonly Vector3[] DojoLanterns = { new Vector3(-6.5f, 3.2f, -4.5f), new Vector3(6.5f, 3.2f, -4.5f), new Vector3(-6.5f, 3.2f, 4.5f), new Vector3(6.5f, 3.2f, 4.5f) };
        private static readonly Vector3[] TempleLanterns = { new Vector3(-4.5f, 2.2f, -4.5f), new Vector3(4.5f, 2.2f, -4.5f), new Vector3(-4.5f, 2.2f, 4.5f), new Vector3(4.5f, 2.2f, 4.5f) };
    }
}
