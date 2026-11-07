using System.Collections;
using Kendo.Campaign;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Kendo.Game
{
    /// <summary>
    /// Base of every scene director: makes sure the game root exists, the supplied environment is present (the scene
    /// builder bakes it into the scene with colliders; a runtime fallback instantiates it), sets lighting and music.
    /// </summary>
    public abstract class SceneDirector : MonoBehaviour
    {
        protected KendoGame game;
        protected SceneLayout layout;
        protected KendoGameConfig cfg;
        public string SceneName => gameObject.scene.name;

        protected virtual void Awake()
        {
            game = KendoGame.Instance != null ? KendoGame.Instance : KendoGame.Create();
            cfg = game.Config;
            layout = SceneLayout.For(SceneName);
        }

        protected virtual void Start()
        {
            EnsureEnvironment();
            ApplyLighting();
            game.Paused = false;
        }

        protected void EnsureEnvironment()
        {
            if (layout.environment == EnvironmentKind.None) return;
            if (FindAnyObjectByType<EnvironmentRoot>() != null) return;
            GameObject prefab = layout.environment == EnvironmentKind.Dojo ? cfg.trainingDojo : layout.environment == EnvironmentKind.Temple ? cfg.temple : cfg.finalEnvironment;
            if (prefab == null)
            {
                game.Ui.Toast("Environment asset missing – run Kendo ▸ Build Game.", 8f);
                return;
            }
            var env = Instantiate(prefab);
            env.name = "Environment (runtime)";
            env.AddComponent<EnvironmentRoot>().kind = layout.environment;
            // runtime fallback colliders: a floor and the court boundary walls (the built scene has mesh colliders)
            var floor = new GameObject("Floor").AddComponent<BoxCollider>();
            floor.transform.position = layout.courtCenter + Vector3.down * 0.25f;
            floor.size = new Vector3(40f, 0.5f, 40f);
            floor.gameObject.layer = Layers.Environment;
        }

        protected void ApplyLighting()
        {
            var sunGo = GameObject.Find("Sun") ?? new GameObject("Sun");
            var sun = sunGo.GetComponent<Light>();
            if (sun == null) sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = layout.sun;
            sun.intensity = layout.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(layout.sunEuler);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = layout.ambientSky;
            RenderSettings.ambientEquatorColor = layout.ambientEquator;
            RenderSettings.ambientGroundColor = layout.ambientGround;
            RenderSettings.fog = layout.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = layout.fogColor;
            RenderSettings.fogDensity = layout.fogDensity;
            for (int i = 0; i < layout.lanterns.Length; i++)
            {
                var l = new GameObject("Lantern" + i).AddComponent<Light>();
                l.type = LightType.Point;
                l.color = layout.lanternColor;
                l.range = 7f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
                l.transform.position = layout.lanterns[i];
                l.gameObject.AddComponent<Flicker>();
            }
        }

        protected PlayerRig SpawnPlayer(Vector3 pos, float yaw)
        {
            if (PlayerRig.Active != null) Destroy(PlayerRig.Active.gameObject);
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.GetComponentInParent<PlayerRig>() == null) c.gameObject.SetActive(false);
            var rig = PlayerRig.Create(pos, Quaternion.Euler(0f, yaw, 0f), cfg, Layers.EnvironmentMask);
            SceneManager.MoveGameObjectToScene(rig.gameObject, gameObject.scene);
            return rig;
        }

        protected IEnumerator Talk(System.Collections.Generic.List<DialogueLine> lines, string header)
        {
            bool done = false;
            game.Ui.ShowDialogue(lines, header, () => done = true);
            while (!done) yield return null;
        }

        protected void ShowControlsHint()
        {
            if (game.Settings.showControlsOverlay) game.Ui.ShowControlsOverlay(true);
        }
    }

    /// <summary>Subtle candle-like flicker for lanterns.</summary>
    public sealed class Flicker : MonoBehaviour
    {
        private Light l;
        private float baseI, seed;
        private void Awake() { l = GetComponent<Light>(); baseI = l.intensity; seed = Random.value * 10f; }
        private void Update() { l.intensity = baseI * (0.9f + 0.1f * Mathf.PerlinNoise(seed, Time.time * 3f)); }
    }
}
