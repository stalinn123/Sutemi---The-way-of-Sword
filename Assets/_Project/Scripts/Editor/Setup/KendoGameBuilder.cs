using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kendo.Campaign;
using Kendo.Core;
using Kendo.EditorTools.Import;
using Kendo.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kendo.EditorTools.Setup
{
    /// <summary>
    /// One click: Kendo ▸ Build Game. Applies project settings and import rules, builds the Phase-1 prefabs, the game
    /// shinai, the reconstructed materials, badges 1–5, the game configuration and the nine scenes (environment with
    /// mesh colliders, director, sun), and registers them in the build list. Writes Docs/Generated/BuildReport.md.
    /// The supplied FBX files are never modified – materials are remapped, colliders are added to scene instances.
    /// </summary>
    public static class KendoGameBuilder
    {
        public const string Generated = AssetFolders.Project + "/Generated";
        public const string ConfigPath = AssetFolders.Project + "/Resources/KendoGameConfig.asset";
        public const string PolicyPath = AssetFolders.Project + "/Resources/AI/opponent_a2c.json";
        public const string GameShinaiPrefab = AssetFolders.Prefabs + "/Props/Shinai_Game.prefab";
        private static readonly StringBuilder report = new StringBuilder();

        [MenuItem("Kendo/Build Game", priority = -100)]
        public static void BuildGame()
        {
            report.Clear();
            report.AppendLine("# Build report (Kendo ▸ Build Game)\n");
            report.AppendLine($"Unity {Application.unityVersion} · {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            try
            {
                EditorUtility.DisplayProgressBar("Kendo", "Project settings", 0.05f);
                KendoProjectSettingsSetup.Apply();
                EditorUtility.DisplayProgressBar("Kendo", "Import rules (scale, shinai extraction, read/write)", 0.15f);
                KendoSetupMenu.ReapplyImportRules();
                EditorUtility.DisplayProgressBar("Kendo", "Prefabs", 0.3f);
                KendoPrefabBuilder.BuildAll();
                EditorUtility.DisplayProgressBar("Kendo", "Materials", 0.4f);
                KendoMaterialRebuilder.RemapAll(report);
                EditorUtility.DisplayProgressBar("Kendo", "Shinai", 0.5f);
                var shinai = BuildGameShinai();
                EditorUtility.DisplayProgressBar("Kendo", "Badges", 0.55f);
                var badges = KendoBadgeBuilder.BuildAll(report);
                EditorUtility.DisplayProgressBar("Kendo", "Configuration", 0.6f);
                var cfg = BuildConfig(shinai, badges);
                EditorUtility.DisplayProgressBar("Kendo", "Scenes", 0.7f);
                BuildScenes(cfg);
                report.AppendLine("\nMissing references in the config: " + (string.IsNullOrEmpty(cfg.MissingSummary()) ? "none" : cfg.MissingSummary()));
            }
            catch (Exception e)
            {
                report.AppendLine("\n**FAILED:** " + e);
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Directory.CreateDirectory("Docs/Generated");
                File.WriteAllText("Docs/Generated/BuildReport.md", report.ToString());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log("[Kendo] Build Game finished – see Docs/Generated/BuildReport.md. Open 00_Boot and press Play.");
        }

        /// <summary>Batch mode entry: Unity -batchmode -executeMethod Kendo.EditorTools.Setup.KendoGameBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            BuildGame();
            EditorApplication.Exit(report.ToString().Contains("**FAILED:**") ? 1 : 0);
        }

        // ------------------------------------------------------------------ shinai

        /// <summary>The Phase-1 Shinai prefab (1.31 m as supplied) wrapped and scaled to an adult size-39 shinai (1.20 m).</summary>
        private static GameObject BuildGameShinai()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.ShinaiPrefab);
            if (src == null) { report.AppendLine("- Shinai prefab missing (Phase-1 prefab builder did not run)."); return null; }
            var model = src.GetComponent<Kendo.Characters.ShinaiModel>();
            float len = model != null && model.Length > 0.1f ? model.Length : 1.31f;
            var root = new GameObject("Shinai_Game");
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
                inst.transform.localScale = Vector3.one * (1.20f / len);
                foreach (var c in inst.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                AssetFolders.Ensure(Path.GetDirectoryName(GameShinaiPrefab));
                var asset = PrefabUtility.SaveAsPrefabAsset(root, GameShinaiPrefab);
                report.AppendLine($"- Shinai: supplied mesh {len:0.00} m scaled to 1.20 m → {GameShinaiPrefab}");
                return asset;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ config

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static KendoGameConfig BuildConfig(GameObject shinai, GameObject[] badges)
        {
            AssetFolders.Ensure(Path.GetDirectoryName(ConfigPath));
            var cfg = AssetDatabase.LoadAssetAtPath<KendoGameConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<KendoGameConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }
            cfg.characterModel = Load<GameObject>(KendoImportRules.KendoMaster.AssetPath);
            cfg.shinaiPrefab = shinai;
            cfg.menHelmet = Load<GameObject>(KendoImportRules.MenHelmet.AssetPath);
            cfg.trainingDojo = Load<GameObject>(KendoImportRules.TrainingDojo.AssetPath);
            cfg.temple = Load<GameObject>(KendoImportRules.Temple.AssetPath);
            cfg.finalEnvironment = Load<GameObject>(KendoImportRules.FinalEnvironment.AssetPath);
            cfg.badges = badges;
            cfg.litTemplate = Template("Kendo_Lit", "Universal Render Pipeline/Lit", false);
            cfg.unlitTemplate = Template("Kendo_UnlitTransparent", "Universal Render Pipeline/Unlit", true);
            cfg.masterPortrait = LoadSprite(AssetFolders.Project + "/Art/UI/Sprites/Master_Portrait.png");
            cfg.formerStudentPortrait = LoadSprite(AssetFolders.Project + "/Art/UI/Sprites/FormerStudent_Portrait.png");
            cfg.opponentPolicy = Load<TextAsset>(PolicyPath);
            string a = AssetFolders.Project + "/Audio/";
            cfg.musicDojo = Load<AudioClip>(a + "Music/music_dojo.wav");
            cfg.musicBattle = Load<AudioClip>(a + "Music/music_battle.wav");
            cfg.musicFinal = Load<AudioClip>(a + "Music/music_final.wav");
            cfg.ambienceDojo = Load<AudioClip>(a + "SFX/ambience_dojo.wav");
            cfg.ambienceTemple = Load<AudioClip>(a + "SFX/ambience_temple.wav");
            cfg.shinaiHit = Load<AudioClip>(a + "SFX/shinai_hit.wav");
            cfg.shinaiClash = Load<AudioClip>(a + "SFX/shinai_clash.wav");
            cfg.shinaiTouch = Load<AudioClip>(a + "SFX/shinai_touch.wav");
            cfg.fumikomi = Load<AudioClip>(a + "SFX/fumikomi.wav");
            cfg.footSlide = Load<AudioClip>(a + "SFX/foot_slide.wav");
            cfg.kiaiPlayer = Load<AudioClip>(a + "Dialogue/kiai_player.wav");
            cfg.kiaiOpponent = Load<AudioClip>(a + "Dialogue/kiai_opponent.wav");
            cfg.kiaiBoss = Load<AudioClip>(a + "Dialogue/kiai_boss.wav");
            cfg.bell = Load<AudioClip>(a + "SFX/bell.wav");
            cfg.taiko = Load<AudioClip>(a + "SFX/taiko.wav");
            cfg.uiClick = Load<AudioClip>(a + "SFX/ui_click.wav");
            cfg.uiConfirm = Load<AudioClip>(a + "SFX/ui_confirm.wav");
            cfg.success = Load<AudioClip>(a + "SFX/success.wav");
            cfg.fail = Load<AudioClip>(a + "SFX/fail.wav");
            cfg.flagRaise = Load<AudioClip>(a + "SFX/flag.wav");
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            report.AppendLine($"- Config: {ConfigPath} (policy: {(cfg.opponentPolicy != null ? "yes" : "MISSING")}, portrait: {(cfg.masterPortrait != null ? "yes" : "missing")})");
            return cfg;
        }

        private static Material Template(string name, string shader, bool transparent)
        {
            string path = Generated + "/Materials/" + name + ".mat";
            AssetFolders.Ensure(Generated + "/Materials");
            var m = Load<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find(shader);
                if (sh == null) { report.AppendLine($"- Shader {shader} not found (is URP installed?)"); return null; }
                m = new Material(sh) { name = name };
                if (transparent)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        private static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return Load<Sprite>(path);
        }

        // ------------------------------------------------------------------ scenes

        private sealed class SceneSpec
        {
            public KendoSceneId id;
            public Type director;
            public GameObject environment;
            public EnvironmentKind kind;
        }

        public static void BuildScenes(KendoGameConfig cfg)
        {
            AssetFolders.Ensure(KendoScenes.Folder);
            var specs = new List<SceneSpec>
            {
                new SceneSpec { id = KendoSceneId.Boot, director = typeof(BootDirector) },
                new SceneSpec { id = KendoSceneId.MainMenu, director = typeof(MainMenuDirector), environment = cfg.trainingDojo, kind = EnvironmentKind.Dojo },
                new SceneSpec { id = KendoSceneId.TrainingDojo, director = typeof(DojoDirector), environment = cfg.trainingDojo, kind = EnvironmentKind.Dojo },
                new SceneSpec { id = KendoSceneId.Level1, director = typeof(LevelDirector), environment = cfg.trainingDojo, kind = EnvironmentKind.Dojo },
                new SceneSpec { id = KendoSceneId.Level2, director = typeof(LevelDirector), environment = cfg.trainingDojo, kind = EnvironmentKind.Dojo },
                new SceneSpec { id = KendoSceneId.Level3, director = typeof(LevelDirector), environment = cfg.temple, kind = EnvironmentKind.Temple },
                new SceneSpec { id = KendoSceneId.Level4, director = typeof(LevelDirector), environment = cfg.temple, kind = EnvironmentKind.Temple },
                new SceneSpec { id = KendoSceneId.FinalBattle, director = typeof(LevelDirector), environment = cfg.finalEnvironment, kind = EnvironmentKind.Final },
                new SceneSpec { id = KendoSceneId.Ending, director = typeof(EndingDirector), environment = cfg.trainingDojo, kind = EnvironmentKind.Dojo },
            };
            var build = new List<EditorBuildSettingsScene>();
            foreach (var s in specs)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var director = new GameObject("[Scene] " + KendoScenes.FileName(s.id));
                director.AddComponent(s.director);
                int colliders = 0, skipped = 0;
                if (s.environment != null)
                {
                    var env = (GameObject)PrefabUtility.InstantiatePrefab(s.environment);
                    env.name = "--- Environment --- " + s.environment.name;
                    env.AddComponent<EnvironmentRoot>().kind = s.kind;
                    AddColliders(env, s.kind, out colliders, out skipped);
                }
                else if (s.id != KendoSceneId.Boot)
                {
                    report.AppendLine($"- **{KendoScenes.FileName(s.id)}: environment missing – scene not usable**");
                }
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                if (s.id == KendoSceneId.Boot)
                {
                    var cam = new GameObject("BootCamera").AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = KendoPalette.PanelIndigo;
                    cam.gameObject.AddComponent<AudioListener>();
                }
                string path = KendoScenes.AssetPath(s.id);
                EditorSceneManager.SaveScene(scene, path);
                build.Add(new EditorBuildSettingsScene(path, true));
                report.AppendLine($"- Scene {KendoScenes.FileName(s.id)}: director {s.director.Name}, environment {(s.environment != null ? s.environment.name : "none")}, mesh colliders {colliders} (skipped {skipped} large/decorative meshes)");
            }
            EditorBuildSettings.scenes = build.ToArray();
            EditorSceneManager.OpenScene(KendoScenes.AssetPath(KendoSceneId.Boot));
        }

        /// <summary>
        /// Mesh colliders on the environment instance (walls, pillars, floors, dais, ceilings) so the player, the shinai
        /// and the ceiling-aware furikaburi interact with the real geometry. Very large decorative meshes (roof tiles,
        /// ivy, foliage cards, sky) are skipped.
        /// </summary>
        private static void AddColliders(GameObject env, EnvironmentKind kind, out int added, out int skipped)
        {
            added = 0;
            skipped = 0;
            int envLayer = LayerMask.NameToLayer(KendoLayers.Environment);
            foreach (var mf in env.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                bool decorative = n.Contains("broom") || n.Contains("creeper") || n.Contains("ivy") || n.Contains("sphere") || n.Contains("insect")
                                  || n.Contains("leaf") || n.Contains("foliage") || n.Contains("rhodo") || (kind == EnvironmentKind.Final && n.StartsWith("plane.001"));
                if (mesh.vertexCount > 60000 || decorative || !mf.gameObject.activeInHierarchy) { skipped++; continue; }
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                if (envLayer >= 0) mf.gameObject.layer = envLayer;
                added++;
            }
        }
    }

    /// <summary>Backwards-compatible entry used by the Phase-1 setup menu.</summary>
    public static class KendoSceneBuilder
    {
        public static void BuildAll(bool overwrite)
        {
            var cfg = KendoGameConfig.Load();
            if (cfg == null) { KendoGameBuilder.BuildGame(); return; }
            KendoGameBuilder.BuildScenes(cfg);
        }
    }
}
