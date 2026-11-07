using System.Collections;
using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>08_Ending: the master's final words, the Master's Legacy badge, credits.</summary>
    public sealed class EndingDirector : SceneDirector
    {
        protected override void Start()
        {
            base.Start();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var rig = SpawnPlayer(layout.playerSpawn, layout.playerYaw);
            rig.InputEnabled = false;
            var master = KendokaFactory.Create(KendokaRole.Master, layout.masterSpot, Quaternion.Euler(0f, layout.masterYaw, 0f), cfg, CampaignContent.MasterName);
            if (master != null) { master.Frozen = true; master.Motion.ForceSageto(true); master.opponent = rig.fighter; }
            if (cfg.badges.Length > 4 && cfg.badges[4] != null)
            {
                var b = Instantiate(cfg.badges[4]);
                b.transform.position = layout.masterSpot + new Vector3(0f, 1.25f, 0.55f);
                b.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                b.AddComponent<Spin>();
            }
            game.Audio.PlayMusic(cfg.musicDojo);
            yield return new WaitForSeconds(1f);
            if (master != null) master.bowDeg = 15f;
            yield return new WaitForSeconds(1.2f);
            if (master != null) master.bowDeg = 0f;
            var c = game.Campaign;
            yield return Talk(MasterDialogue.Ending(c), "The Master's Legacy");
            c.data.story.endingSeen = true;
            game.Save();
            bool closed = false;
            game.Ui.Menus.ShowResult("The Master's Legacy",
                $"You completed the campaign.\n\nBadges: {c.data.badges.Count}/5   ·   Title: {c.data.title}\nLessons: {c.LessonsCompleted}/7   ·   Time trained: {(int)(c.data.playSeconds / 60)} min\n\n" + Menus.CreditsText,
                "Return to the dojo", () => { closed = true; game.Flow.Go(KendoSceneNames.TrainingDojo); }, "Main menu", () => { closed = true; game.Flow.Go(KendoSceneNames.MainMenu); });
            while (!closed) yield return null;
        }
    }

    public sealed class Spin : MonoBehaviour
    {
        public float speed = 30f;
        private void Update() => transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.World);
    }
}
