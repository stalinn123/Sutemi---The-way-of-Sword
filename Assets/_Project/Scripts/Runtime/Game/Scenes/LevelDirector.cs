using System.Collections;
using System.Collections.Generic;
using Kendo.Campaign;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Levels 1–4 and the final battle: briefing (or the journey and the former student's speech), rei, the bout,
    /// the master's performance feedback, badge award, retry or return. Uses the shared match/rules/AI systems.
    /// </summary>
    public sealed class LevelDirector : SceneDirector
    {
        private LevelDef stage;
        private PlayerRig rig;
        private Fighter opponent, master;
        private MatchDirector matchDirector;
        private OpponentBrain brain;
        private bool bowed;

        protected override void Start()
        {
            base.Start();
            int s = SceneFlow.StageOfScene(SceneName);
            stage = CampaignContent.Stage(s);
            if (stage == null) { game.Flow.Go(KendoSceneNames.TrainingDojo); return; }
            if (game.Campaign.CanEnterStage(s) != GateResult.Open)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                game.Ui.Toast($"Editor: {stage.title} is locked in this save ({game.Campaign.LockReason(s)}). Playing anyway; the result is not recorded.", 6f);
#else
                game.Flow.Go(KendoSceneNames.TrainingDojo);
                return;
#endif
            }
            rig = SpawnPlayer(layout.PlayerStart, layout.PlayerStartYaw);
            var role = stage.opponent == OpponentKind.FormerStudent ? KendokaRole.FormerStudent : (stage.index == 1 ? KendokaRole.OpponentWhite : KendokaRole.Opponent);
            opponent = KendokaFactory.Create(role, layout.OpponentStart, Quaternion.Euler(0f, layout.OpponentStartYaw, 0f), cfg, stage.opponentName);
            if (opponent == null) return;
            master = stage.opponent == OpponentKind.FormerStudent ? null : KendokaFactory.Create(KendokaRole.Master, layout.masterSpot, Quaternion.Euler(0f, layout.masterYaw, 0f), cfg, CampaignContent.MasterName);
            if (master != null) { master.Frozen = true; master.Motion.ForceSageto(true); master.opponent = rig.fighter; }
            rig.fighter.Frozen = true;
            opponent.Frozen = true;
            opponent.opponent = rig.fighter;
            rig.fighter.opponent = opponent;
            game.Audio.PlayMusic(stage.opponent == OpponentKind.FormerStudent ? cfg.musicFinal : cfg.musicBattle);
            game.Audio.PlayAmbience(layout.environment == EnvironmentKind.Dojo ? cfg.ambienceDojo : cfg.ambienceTemple);
            game.Ui.SetObjective(stage.title + ": " + stage.objective);
            ShowControlsHint();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            var c = game.Campaign;
            if (stage.opponent == OpponentKind.FormerStudent)
            {
                if (!c.data.story.journeySeen)
                {
                    yield return Journey();
                    c.data.story.journeySeen = true;
                }
                yield return Talk(MasterDialogue.FormerStudentEncounter(c), CampaignContent.FormerStudentName);
                c.data.story.formerStudentMet = true;
                game.Save();
            }
            else
            {
                yield return Talk(MasterDialogue.BeforeStage(stage.index, c), stage.title);
            }

            // rei before the bout
            opponent.Motion.ForceSageto(true);
            rig.fighter.Frozen = false;
            rig.MovementEnabled = false;
            rig.StrikesEnabled = false;
            bowed = false;
            System.Action<float, float> onBow = (d, h) => bowed = true;
            rig.BowFinished += onBow;
            game.Ui.SetPrompt("Rei: press X for sageto, hold B to bow, release, then press B to rise");
            float t = 0f;
            while (!bowed && t < 10f)
            {
                t += Time.deltaTime;
                opponent.bowDeg = Mathf.MoveTowards(opponent.bowDeg, t > 1f && t < 3f ? 15f : 0f, 20f * Time.deltaTime);
                yield return null;
            }
            rig.BowFinished -= onBow;
            opponent.bowDeg = 0f;
            if (!bowed) game.Ui.Toast("You did not bow. Courtesy comes first – the master noticed.");
            game.Ui.SetPrompt("");
            opponent.Motion.ForceSageto(false);
            if (rig.fighter.Motion.InSageto) rig.fighter.ToggleKamae();
            rig.MovementEnabled = true;
            rig.StrikesEnabled = true;
            rig.aimPointWorld = a => null;

            // bout
            brain = opponent.gameObject.AddComponent<OpponentBrain>();
            matchDirector = gameObject.AddComponent<MatchDirector>();
            matchDirector.Begin(rig.fighter, opponent, brain, stage, layout);
            brain.courtCenter = layout.courtCenter;
            brain.courtRot = layout.CourtRotation;
            brain.courtHalfAlong = layout.courtHalf.y;
            brain.Init(opponent, rig.fighter, matchDirector.match, stage.opponent, stage.tier);
            bool done = false, won = false;
            matchDirector.Finished += w => { done = true; won = w; };
            while (!done) yield return null;
            yield return Result(won);
        }

        private IEnumerator Journey()
        {
            // the final journey: leaving the master's dojo for the former student's hall
            var camGo = new GameObject("JourneyCamera");
            var cam = camGo.AddComponent<Camera>();
            rig.cam.enabled = false;
            var lines = new List<DialogueLine>
            {
                new DialogueLine(Speaker.Narrator, "You leave the Training Center at dawn, the master's words in your ears."),
                new DialogueLine(Speaker.Narrator, $"Beyond the mountains, in a hall that once belonged to an old school, {CampaignContent.FormerStudentName} waits."),
                new DialogueLine(Speaker.Narrator, "Torches burn around a court far larger than any you have fought on. This is his territory."),
            };
            bool finished = false;
            game.Ui.ShowDialogue(lines, "The journey", () => finished = true);
            float t = 0f;
            var center = layout.courtCenter;
            while (!finished)
            {
                t += Time.deltaTime;
                float a = t * 0.12f;
                camGo.transform.position = center + new Vector3(Mathf.Cos(a) * 16f, 6f - Mathf.Min(3.5f, t * 0.2f), Mathf.Sin(a) * 16f);
                camGo.transform.LookAt(center + Vector3.up * 1.2f);
                yield return null;
            }
            Destroy(camGo);
            rig.cam.enabled = true;
        }

        private IEnumerator Result(bool won)
        {
            var c = game.Campaign;
            int s = stage.index;
            var fb = MasterFeedback.ForBout(stage, won, matchDirector.match.points[0], matchDirector.match.points[1], matchDirector.playerStrikes, matchDirector.stats);
            float score = matchDirector.PlayerPerformance;
            bool record = c.CanEnterStage(s) == GateResult.Open;
            int badge = -1;
            if (record)
            {
                if (won) badge = c.CompleteStage(s, score);
                else c.FailStage(s, score, fb.mainProblem.HasValue ? (int)fb.mainProblem.Value : -1);
                game.Save();
                game.SavePlayerModel();
            }
            if (won && stage.opponent == OpponentKind.FormerStudent)
            {
                opponent.kneel = 1f;
                yield return Talk(MasterDialogue.FinalVictory(fb), "Victory");
            }
            else if (!won && stage.opponent == OpponentKind.FormerStudent)
                yield return Talk(MasterDialogue.FinalDefeat(fb), "Defeat");
            else if (won)
                yield return Talk(MasterDialogue.AfterStageWin(s, fb, c, badge), "Victory");
            else
                yield return Talk(MasterDialogue.AfterStageLoss(s, fb), "Defeat");
            if (badge >= 0) game.Audio.Stinger(cfg.success); else if (!won) game.Audio.Stinger(cfg.fail);

            string body = fb.ToText() + $"\n\nTraining score this bout (not the match result): {score:0}";
            if (badge >= 0) body += $"\n\nBadge earned: {CampaignContent.Badges[badge].name}";
            if (!record) body += "\n\n(Editor session: result not recorded – this stage is locked in the save.)";
            bool chosen = false;
            if (won && stage.opponent == OpponentKind.FormerStudent)
                game.Ui.Menus.ShowResult("Victory", body, "Continue", () => { chosen = true; game.Flow.Go(KendoSceneNames.Ending); }, null, null);
            else if (won)
                game.Ui.Menus.ShowResult("Victory", body, "Return to the dojo", () => { chosen = true; game.Flow.Go(KendoSceneNames.TrainingDojo); }, "Fight again", () => { chosen = true; game.Flow.Go(SceneName); });
            else
                game.Ui.Menus.ShowResult("Defeat", body, "Retry", () => { chosen = true; game.Flow.Go(SceneName); }, "Return to the dojo", () => { chosen = true; game.Flow.Go(KendoSceneNames.TrainingDojo); });
            while (!chosen) yield return null;
        }
    }
}
