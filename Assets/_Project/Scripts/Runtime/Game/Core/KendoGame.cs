using System;
using Kendo.AI;
using Kendo.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Kendo.Game
{
    /// <summary>
    /// The persistent game root ([KendoGame], DontDestroyOnLoad). Sole owner of: save service, campaign progress,
    /// scene flow, audio, input router, UI, EventSystem, and the player model used by the AI (behaviour log +
    /// imitation forest + personal A2C policy). Created by Boot, or automatically when any scene is played directly.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class KendoGame : MonoBehaviour
    {
        public static KendoGame Instance { get; private set; }

        public KendoGameConfig Config { get; private set; }
        public SaveService Saves { get; private set; }
        public CampaignProgress Campaign { get; private set; }
        public SceneFlow Flow { get; private set; }
        public AudioService Audio { get; private set; }
        public KendoInputRouter Input { get; } = new KendoInputRouter();
        public KendoUiRoot Ui { get; private set; }
        public SaveLoadStatus LoadStatus { get; private set; }

        // player model for Agents 2 and 3
        public PlayerBehaviourLog Behaviour { get; private set; }
        public AdaptiveRandomForest Imitation { get; private set; }
        public ActorCritic BasePolicy { get; private set; }
        public ActorCritic PersonalPolicy { get; set; }

        private bool paused;
        private float lastAutoSave;

        public bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                Time.timeScale = paused ? 0f : 1f;
            }
        }

        public PlayerSettings Settings => Saves?.Data?.settings ?? new PlayerSettings();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null) Create();
        }

        public static KendoGame Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[KendoGame]");
            var g = go.AddComponent<KendoGame>();
            return g;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 1f;

            Config = KendoGameConfig.Load();
            if (Config == null) Config = ScriptableObject.CreateInstance<KendoGameConfig>(); // explained by the boot screen

            Saves = SaveService.CreateDefault();
            LoadStatus = Saves.Load();
            Campaign = new CampaignProgress(Saves.Data);

            Audio = gameObject.AddComponent<AudioService>();
            Audio.Init(Config, Saves.Data.settings);
            ApplyDisplaySettings();

            EnsureEventSystem();
            Ui = gameObject.AddComponent<KendoUiRoot>();
            Ui.Init(this);
            Flow = gameObject.AddComponent<SceneFlow>();
            Flow.Init(this);

            var xr = new XRKendoInput();
            Input.EnableXR(xr);
            gameObject.AddComponent<XRSupport>();

            LoadPlayerModel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        public void ApplyDisplaySettings()
        {
            var s = Settings;
            if (s.quality >= 0 && s.quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(s.quality, true);
            if (!Application.isEditor) Screen.fullScreen = s.fullscreen;
            Audio?.Apply(s);
        }

        /// <summary>Saves the campaign (and returns false if the disk refused – play continues, the HUD warns).</summary>
        public bool Save()
        {
            bool ok = Saves.Save();
            if (!ok && Ui != null) Ui.Toast("Progress could not be saved: " + Saves.LastError);
            return ok;
        }

        public void NewGame()
        {
            Saves.NewGame();
            Campaign = new CampaignProgress(Saves.Data);
            Behaviour = new PlayerBehaviourLog();
            Imitation = NewForest();
            PersonalPolicy = null;
            Saves.SavePersonalPolicy("");
        }

        private static AdaptiveRandomForest NewForest() => new AdaptiveRandomForest(KendoTimings.ObservationSize, KendoTimings.ActionCount, seed: 2024);

        private void LoadPlayerModel()
        {
            Behaviour = Saves.LoadBehaviour();
            Imitation = NewForest();
            for (int i = 0; i < Behaviour.Count; i++) Imitation.Learn(Behaviour.observations[i], Behaviour.actions[i]);
            try
            {
                if (Config.opponentPolicy != null) BasePolicy = ActorCritic.FromJson(Config.opponentPolicy.text);
            }
            catch (Exception e) { Debug.LogWarning($"[Kendo] Opponent policy could not be loaded: {e.Message}"); }
            try
            {
                var personal = Saves.LoadPersonalPolicy();
                if (!string.IsNullOrEmpty(personal)) PersonalPolicy = ActorCritic.FromJson(personal);
            }
            catch (Exception) { PersonalPolicy = null; }
        }

        /// <summary>Agent 2 observes the player: one (observation, action) pair per decision.</summary>
        public void ObservePlayer(double[] obs, DuelAction action)
        {
            Behaviour.Add(obs, (int)action);
            Imitation.Learn(obs, (int)action);
        }

        public void SavePlayerModel()
        {
            Saves.SaveBehaviour(Behaviour);
            if (PersonalPolicy != null) Saves.SavePersonalPolicy(PersonalPolicy.ToJson());
        }

        private void Update()
        {
            if (Saves?.Data != null) Saves.Data.playSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime - lastAutoSave > 120f)
            {
                lastAutoSave = Time.unscaledTime;
                Saves?.Save();
            }
        }

        private void OnApplicationQuit()
        {
            Saves?.Save();
            if (Behaviour != null) SavePlayerModel();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) Saves?.Save();
        }

        public void Quit()
        {
            Save();
            SavePlayerModel();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
