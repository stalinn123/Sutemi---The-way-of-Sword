using System.Collections;
using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>00_Boot: creates the game root, loads the save safely, reports problems, then opens the main menu.</summary>
    public sealed class BootDirector : SceneDirector
    {
        protected override void Start()
        {
            base.Start();
            StartCoroutine(Boot());
        }

        private IEnumerator Boot()
        {
            yield return null;
            string missing = cfg != null ? cfg.MissingSummary() : "game config";
            if (!string.IsNullOrEmpty(missing))
            {
                game.Ui.Menus.ShowResult("Setup required",
                    "These game assets are not built yet: " + missing + ".\n\nIn the Unity editor run  Kendo ▸ Build Game  (it imports the supplied FBX files, builds the prefabs, the nine scenes and this configuration), then press Play again.",
                    "Continue anyway", () => game.Flow.Go(KendoSceneNames.MainMenu), null, null);
                yield break;
            }
            switch (game.LoadStatus)
            {
                case SaveLoadStatus.RecoveredFromBackup: game.Ui.Toast("Your save file was damaged – progress was restored from the backup.", 6f); break;
                case SaveLoadStatus.CorruptReset: game.Ui.Toast("Your save file could not be read. It was kept as *.corrupt and a new campaign starts.", 7f); break;
                case SaveLoadStatus.FutureVersionReset: game.Ui.Toast("The save comes from a newer version of the game and was set aside.", 7f); break;
                case SaveLoadStatus.StorageUnavailable: game.Ui.Toast("The save folder is not available – progress will not be kept after you quit.", 7f); break;
            }
            yield return new WaitForSeconds(0.4f);
            game.Flow.Go(KendoSceneNames.MainMenu, "");
        }
    }
}
