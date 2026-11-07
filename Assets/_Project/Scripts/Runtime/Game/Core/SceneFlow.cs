using System;
using System.Collections;
using Kendo.Campaign;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kendo.Game
{
    public enum FlowResult { Started, Busy, Locked, NotInBuild, Unknown }

    /// <summary>
    /// Scene transitions: progression gate → fade out → loading screen → async load → fade in. One at a time.
    /// Saves before leaving so a crash during loading never loses progress.
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        private KendoGame game;
        public bool Busy { get; private set; }
        public string CurrentScene => SceneManager.GetActiveScene().name;
        public event Action<string> SceneReady;

        /// <summary>Stage the next level scene should run (set by the level board / continue).</summary>
        public int PendingStage { get; set; } = -1;

        public void Init(KendoGame g) { game = g; }

        public static int StageOfScene(string scene)
        {
            for (int i = 0; i < CampaignContent.Stages.Length; i++) if (CampaignContent.Stages[i].scene == scene) return i;
            return -1;
        }

        public FlowResult Go(string scene, string loadingTip = null)
        {
            if (Busy) return FlowResult.Busy;
            if (Array.IndexOf(KendoSceneNames.All, scene) < 0) return FlowResult.Unknown;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                game.Ui.Toast($"Scene {scene} is not in the build. Run Kendo ▸ Build Game.");
                return FlowResult.NotInBuild;
            }
            int stage = StageOfScene(scene);
            if (stage >= 0)
            {
                if (game.Campaign.CanEnterStage(stage) != GateResult.Open)
                {
                    game.Ui.Toast(game.Campaign.LockReason(stage));
                    return FlowResult.Locked;
                }
                game.Campaign.EnterStage(stage);
                PendingStage = stage;
            }
            else if (scene == KendoSceneNames.TrainingDojo)
            {
                game.Campaign.data.checkpoint.scene = scene;
                game.Campaign.data.checkpoint.stage = -1;
            }
            game.Save();
            StartCoroutine(Load(scene, loadingTip ?? Tip(scene)));
            return FlowResult.Started;
        }

        private static string Tip(string scene)
        {
            switch (scene)
            {
                case KendoSceneNames.TrainingDojo: return "Courtesy first: bow when you enter and when you leave the dojo.";
                case KendoSceneNames.Level1: return "Keep chudan-no-kamae. Strike from the right distance – one step away.";
                case KendoSceneNames.Level2: return "Hold Shift to block, then answer immediately – a counter is an opportunity.";
                case KendoSceneNames.Level3: return "This opponent learned from you. Change your habits.";
                case KendoSceneNames.Level4: return "He adapts after every point. Vary your attacks.";
                case KendoSceneNames.FinalBattle: return "Patience. Let him overreach – then answer. Zanshin, always.";
                case KendoSceneNames.Ending: return "Kendo is not about who strikes first.";
                default: return "";
            }
        }

        private IEnumerator Load(string scene, string tip)
        {
            Busy = true;
            game.Paused = false;
            game.Ui.CloseAllMenus();
            yield return game.Ui.Fade(1f, 0.45f);
            game.Ui.ShowLoading(true, tip);
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (op == null)
            {
                game.Ui.ShowLoading(false, null);
                yield return game.Ui.Fade(0f, 0.3f);
                Busy = false;
                yield break;
            }
            while (!op.isDone)
            {
                game.Ui.SetLoadingProgress(op.progress / 0.9f);
                yield return null;
            }
            yield return null; // let directors run Start()
            game.Ui.ShowLoading(false, null);
            yield return game.Ui.Fade(0f, 0.6f);
            Busy = false;
            SceneReady?.Invoke(scene);
        }

        public void Continue()
        {
            Go(game.Campaign.ResumeScene());
        }
    }
}
