using System.Collections;
using Kendo.Campaign;
using Kendo.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kendo.Tests
{
    /// <summary>Scene-level tests (need Kendo ▸ Build Game to have produced the scenes).</summary>
    public class GameFlowPlayTests
    {
        private static IEnumerator Load(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene)) Assert.Ignore($"{scene} not in the build list – run Kendo ▸ Build Game.");
            SceneManager.LoadScene(scene);
            for (int i = 0; i < 30; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator BootReachesMainMenuWithOneRoot()
        {
            yield return Load(KendoSceneNames.Boot);
            float t = 0;
            while (SceneManager.GetActiveScene().name != KendoSceneNames.MainMenu && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(KendoSceneNames.MainMenu, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, Object.FindObjectsByType<KendoGame>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator DojoSpawnsPlayerMasterAndMotodachi()
        {
            yield return Load(KendoSceneNames.TrainingDojo);
            KendoGame.Instance.Ui.CloseDialogue();
            Assert.IsNotNull(PlayerRig.Active);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerRig>(FindObjectsSortMode.None).Length);
            var dojo = Object.FindAnyObjectByType<DojoDirector>();
            Assert.IsNotNull(dojo.Master);
            Assert.IsNotNull(dojo.Motodachi);
            Assert.IsTrue(dojo.Master.body.skinned, "master mesh must be readable and skinned");
            Assert.IsNotNull(PlayerRig.Active.fighter.rig.shinai, "player holds the supplied shinai");
        }

        [UnityTest]
        public IEnumerator SuriashiMovesThePlayerOneStep()
        {
            yield return Load(KendoSceneNames.TrainingDojo);
            var game = KendoGame.Instance;
            game.Ui.CloseDialogue();
            game.Ui.CloseAllMenus();
            var input = new ScriptedKendoInput();
            game.Input.Override = input;
            var rig = PlayerRig.Active;
            var start = rig.transform.position;
            input.next.move = new Vector2(0, 1);
            yield return null;
            input.next.move = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            float moved = Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(rig.transform.position.x, 0, rig.transform.position.z));
            game.Input.Override = null;
            Assert.AreEqual(0.30f, moved, 0.06f, "one okuri-ashi forward step");
        }

        [UnityTest]
        public IEnumerator LevelOneStartsABoutWithAnOpponent()
        {
            var game = KendoGame.Instance != null ? KendoGame.Instance : KendoGame.Create();
            for (int i = 0; i < 7; i++) game.Campaign.CompleteLesson(i, 80);
            yield return Load(KendoSceneNames.Level1);
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelDirector>());
            Assert.AreEqual(2, Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None).Length - 1, "player, opponent and master");
        }
    }
}
