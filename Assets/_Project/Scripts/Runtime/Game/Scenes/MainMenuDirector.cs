using System.Collections;
using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>01_MainMenu: the dojo as a backdrop with the master in his presenting pose; New game / Continue / Settings…</summary>
    public sealed class MainMenuDirector : SceneDirector
    {
        private Camera menuCam;
        private float t;

        protected override void Start()
        {
            base.Start();
            menuCam = new GameObject("MenuCamera").AddComponent<Camera>();
            menuCam.gameObject.AddComponent<AudioListener>();
            menuCam.fieldOfView = 50f;
            var master = KendokaFactory.Create(KendokaRole.Master, layout.masterSpot, Quaternion.Euler(0f, layout.masterYaw, 0f), cfg, CampaignContent.MasterName);
            if (master != null)
            {
                master.Frozen = true;
                master.Motion.ForceSageto(true);
                master.presentArm = 1f;
            }
            game.Audio.PlayMusic(cfg.musicDojo);
            game.Audio.PlayAmbience(cfg.ambienceDojo);
            game.Ui.SetObjective("");
            game.Ui.ShowControlsOverlay(false);
            game.Ui.Menus.OpenMain();
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (menuCam == null) return;
            var c = layout.masterSpot + Vector3.up * 1.3f;
            menuCam.transform.position = c + new Vector3(Mathf.Sin(t * 0.05f) * 0.8f + 1.4f, 0.25f, 3.6f);
            menuCam.transform.LookAt(c + Vector3.left * 0.9f);
        }

        private void OnDestroy()
        {
            if (game != null && game.Ui != null) game.Ui.Menus.CloseMain();
        }
    }
}
