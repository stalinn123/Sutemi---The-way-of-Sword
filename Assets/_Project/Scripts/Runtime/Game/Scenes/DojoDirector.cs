using System.Collections;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// 02_TrainingDojo – the hub: intro sequence and first meeting, the master (conversations), the lesson board
    /// (nafuda wall), the level board (entrance), the badge display (bokken rack), the bogu stand and the senior
    /// student who receives strikes (motodachi) for the lessons.
    /// </summary>
    public sealed class DojoDirector : SceneDirector
    {
        public PlayerRig Rig { get; private set; }
        public Fighter Master { get; private set; }
        public Fighter Motodachi { get; private set; }
        public BoguStandTarget Stand { get; private set; }
        public LessonDirector Lessons { get; private set; }
        private readonly List<GameObject> badgeDisplays = new List<GameObject>();

        protected override void Start()
        {
            base.Start();
            Rig = SpawnPlayer(layout.playerSpawn, layout.playerYaw);
            Master = KendokaFactory.Create(KendokaRole.Master, layout.masterSpot, Quaternion.Euler(0f, layout.masterYaw, 0f), cfg, CampaignContent.MasterName);
            Motodachi = KendokaFactory.Create(KendokaRole.OpponentWhite, SceneLayout.DojoMotodachi, Quaternion.identity, cfg, "Sempai Aoki (motodachi)");
            LocateStand();
            Stand = BoguStandTarget.Create(SceneLayout.DojoStandA);
            Stand.transform.rotation = Quaternion.LookRotation(Flat(SceneLayout.DojoStandPractice - SceneLayout.DojoStandA));
            if (Master != null)
            {
                Master.technique = new Technique { speed = 9.0, hasujiSigma = 1.5, kiaiProb = 1.0, timingSigma = 0.015, tiltSigma = 2.0, zanshinProb = 1.0, guard = 0.5, drive = 1.0 };
                Master.Frozen = true;
                Master.opponent = Rig.fighter;
                Master.Motion.ForceSageto(true);
                Master.gameObject.AddComponent<Interactable>().prompt = $"Talk to {CampaignContent.MasterName}";
                var it = Master.GetComponent<Interactable>();
                it.action = TalkToMaster;
                it.available = () => Lessons == null || !Lessons.Running;
            }
            if (Motodachi != null)
            {
                Motodachi.Frozen = true;
                Motodachi.opponent = Rig.fighter;
                Motodachi.gameObject.SetActive(true);
            }
            Board(SceneLayout.DojoLessonBoard, "Lesson board – choose a lesson", () => game.Ui.Menus.OpenLessonBoard());
            Board(SceneLayout.DojoLevelBoard, "Level board – leave the dojo", () => game.Ui.Menus.OpenLevelBoard());
            Board(SceneLayout.DojoBadgeRack, "Your badges", () => game.Ui.Menus.OpenBadges());
            Lessons = gameObject.AddComponent<LessonDirector>();
            Lessons.dojo = this;
            game.Ui.Menus.LessonChosen = i => Lessons.Begin(i);
            game.Ui.Menus.StageChosen = s => game.Flow.Go(CampaignContent.Stages[s].scene);
            ShowBadges();
            game.Audio.PlayMusic(cfg.musicDojo);
            game.Audio.PlayAmbience(cfg.ambienceDojo);
            game.Ui.SetObjective(game.Campaign.NextObjective());
            ShowControlsHint();
            StartCoroutine(Arrive());
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v : Vector3.forward; }

        /// <summary>
        /// Finds the real bogu stand in the loaded dojo (the men meshes on the stands) instead of trusting fixed
        /// coordinates, and puts the practice spot 1.8 m in front of it, toward the court.
        /// </summary>
        private void LocateStand()
        {
            Renderer best = null;
            float bestD = float.MaxValue;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Bogu_Men", System.StringComparison.OrdinalIgnoreCase) || r.GetComponentInParent<EnvironmentRoot>() == null) continue;
                float d = Vector3.Distance(r.bounds.center, SceneLayout.DojoStandA + Vector3.up * 1.4f);
                if (d < bestD) { bestD = d; best = r; }
            }
            if (best == null)
            {
                Debug.LogWarning("[Kendo] Bogu stand mesh (Bogu_Men*) not found in the dojo – using measured default " + SceneLayout.DojoStandA);
                return;
            }
            var c = best.bounds.center;
            var standBase = new Vector3(c.x, layout.courtCenter.y, c.z);
            var toCourt = Flat(new Vector3(0f, 0f, layout.courtCenter.z - standBase.z));
            SceneLayout.DojoStandA = standBase;
            SceneLayout.DojoStandPractice = standBase + toCourt.normalized * 1.8f;
            Debug.Log($"[Kendo] Bogu stand found at {standBase}, practice spot {SceneLayout.DojoStandPractice}");
        }

        private void Board(Vector3 at, string prompt, System.Action action)
        {
            var go = new GameObject(prompt);
            go.transform.position = at;
            var it = go.AddComponent<Interactable>();
            it.prompt = prompt;
            it.range = 2.4f;
            it.action = action;
            it.available = () => Lessons == null || !Lessons.Running;
        }

        private void ShowBadges()
        {
            foreach (var b in badgeDisplays) if (b != null) Destroy(b);
            badgeDisplays.Clear();
            for (int i = 0; i < CampaignContent.BadgeCount; i++)
            {
                if (cfg.badges.Length <= i || cfg.badges[i] == null || !game.Campaign.HasBadge(i)) continue;
                var b = Instantiate(cfg.badges[i]);
                b.name = CampaignContent.Badges[i].name;
                b.transform.position = SceneLayout.DojoBadgeRack + new Vector3(-0.9f + i * 0.45f, 0.1f, 0.05f);
                b.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // badge face (+Y in the FBX) toward the room (+Z)
                badgeDisplays.Add(b);
            }
        }

        private IEnumerator Arrive()
        {
            yield return null;
            var c = game.Campaign;
            if (!c.data.story.introSeen)
            {
                yield return IntroCinematic();
                c.data.story.introSeen = true;
                game.Save();
            }
            if (!c.data.story.metMaster)
            {
                yield return Talk(MasterDialogue.FirstEnterDojo(), "Your journey begins");
                c.data.story.metMaster = true;
                game.Save();
                game.Ui.Menus.Confirm("Begin Lesson 1: Introduction to Kendo and Rules?", () => Lessons.Begin(0));
            }
            else if (c.data.checkpoint.lesson >= 0 && c.CanStartLesson(c.data.checkpoint.lesson) == GateResult.Open)
            {
                int li = c.data.checkpoint.lesson;
                game.Ui.Menus.Confirm($"Resume Lesson {li + 1}: {CampaignContent.Lessons[li].title} from your checkpoint?", () => Lessons.Begin(li));
            }
        }

        private IEnumerator IntroCinematic()
        {
            Rig.cam.enabled = false;
            var cam = new GameObject("IntroCamera").AddComponent<Camera>();
            cam.fieldOfView = 55f;
            bool done = false;
            game.Ui.ShowDialogue(MasterDialogue.IntroSequence(), "Kendo: The Master's Legacy", () => done = true);
            float t = 0f;
            var path = new[] { new Vector3(0f, 6.5f, 14f), new Vector3(0f, 3.0f, 9f), new Vector3(0f, 1.8f, 6.5f), new Vector3(0f, 1.7f, 5.6f) };
            while (!done)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 22f) * (path.Length - 1);
                int i = Mathf.Min(path.Length - 2, (int)u);
                cam.transform.position = Vector3.Lerp(path[i], path[i + 1], Mathf.SmoothStep(0f, 1f, u - i));
                cam.transform.LookAt(layout.masterSpot + Vector3.up * 1.4f);
                yield return null;
            }
            Destroy(cam.gameObject);
            Rig.cam.enabled = true;
        }

        private void TalkToMaster()
        {
            var c = game.Campaign;
            Master.presentArm = 1f;
            game.Ui.ShowDialogue(MasterDialogue.Greeting(c), CampaignContent.MasterName, () =>
            {
                Master.presentArm = 0f;
                int next = c.NextLesson();
                if (next >= 0) game.Ui.Menus.Confirm($"Begin Lesson {next + 1}: {CampaignContent.Lessons[next].title}?", () => Lessons.Begin(next));
                else
                {
                    for (int s = 0; s < CampaignContent.StageCount; s++)
                    {
                        if (c.data.stages[s].completed || c.CanEnterStage(s) != GateResult.Open) continue;
                        int stage = s;
                        if (s == CampaignContent.StageCount - 1) c.data.story.finalBriefingSeen = true;
                        game.Ui.Menus.Confirm($"Go to {CampaignContent.Stages[s].title} now?", () => game.Flow.Go(CampaignContent.Stages[stage].scene));
                        break;
                    }
                }
            });
        }

        public void PlacePlayer(Vector3 pos, float yaw)
        {
            var cc = Rig.controller;
            cc.enabled = false;
            Rig.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Rig.yaw = yaw;
            Rig.pitch = 0f;
            Rig.fighter.footwork.Cancel();
            cc.enabled = true;
        }

        public void SendMasterHome()
        {
            if (Master == null) return;
            Master.footwork.Cancel();
            Master.transform.SetPositionAndRotation(layout.masterSpot, Quaternion.Euler(0f, layout.masterYaw, 0f));
            Master.opponent = Rig.fighter;
            Master.bowDeg = 0f;
            Master.presentArm = 0f;
            Master.Frozen = true;
            Master.Motion.ForceSageto(true);
        }

        public void ResetLessonActors()
        {
            SendMasterHome();
            if (Motodachi != null)
            {
                Motodachi.transform.SetPositionAndRotation(SceneLayout.DojoMotodachi, Quaternion.identity);
                Motodachi.footwork.Cancel();
            }
            Rig.fighter.sensor.targets.Clear();
            Rig.fighter.opponent = null;
            Rig.aimPointWorld = null;
            Rig.MovementEnabled = true;
            Rig.StrikesEnabled = true;
            ShowBadges();
        }
    }
}
