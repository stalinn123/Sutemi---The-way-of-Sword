using System.Collections.Generic;
using Kendo.Core;
using Kendo.Data;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>
    /// Creates the data assets described by the Game Design Document (v1.0): campaign, five levels, five badges,
    /// opponent profiles and the example conversations. Opponent numbers are starting points for tuning.
    /// Re-running updates the assets in place.
    /// </summary>
    public static class KendoDataAssetBuilder
    {
        private const string Levels = AssetFolders.Data + "/Levels";
        private const string Badges = AssetFolders.Data + "/Badges";
        private const string Dialogue = AssetFolders.Data + "/Dialogue";
        private const string Opponents = AssetFolders.Data + "/Opponents";

        private struct LevelSpec
        {
            public string Id, Name, Purpose, Location, BadgeId, BadgeName, BadgeDescription;
            public DifficultyTier Tier;
            public KendoSkill Skills;
            public KendoSceneId Scene;
            public float Speed, Reaction, Pattern, Defense, Counter;
        }

        private static readonly LevelSpec[] Specs =
        {
            new LevelSpec
            {
                Id = "L1", Name = "Basic Training", Tier = DifficultyTier.Easy, Scene = KendoSceneId.Level1,
                Purpose = "The player fights beginner opponents. The focus is on movement, basic attacks, blocking, and understanding the combat loop.",
                Skills = KendoSkill.Movement | KendoSkill.Stance | KendoSkill.BasicAttack | KendoSkill.BasicDefense,
                Location = "Beginner training area near the dojo.",
                BadgeId = "beginner_dojo", BadgeName = "Beginner Dojo Badge", BadgeDescription = "Awarded for completing Level 1 — Basic Training.",
                Speed = 0.25f, Reaction = 0.9f, Pattern = 0.15f, Defense = 0.15f, Counter = 0f,
            },
            new LevelSpec
            {
                Id = "L2", Name = "Improvement Training", Tier = DifficultyTier.Medium, Scene = KendoSceneId.Level2,
                Purpose = "Opponents become faster and begin defending effectively. The player must improve timing and combine offense with defense.",
                Skills = KendoSkill.Timing | KendoSkill.Defense | KendoSkill.AttackCombinations | KendoSkill.Positioning,
                Location = "Dedicated practice hall with a stronger opponent.",
                BadgeId = "improvement", BadgeName = "Improvement Badge", BadgeDescription = "Awarded for completing Level 2 — Improvement Training.",
                Speed = 0.45f, Reaction = 0.65f, Pattern = 0.35f, Defense = 0.45f, Counter = 0.1f,
            },
            new LevelSpec
            {
                Id = "L3", Name = "Skill Training", Tier = DifficultyTier.MediumHard, Scene = KendoSceneId.Level3,
                Purpose = "Opponents use better techniques and react more intelligently. Random attacks become ineffective.",
                Skills = KendoSkill.Strategy | KendoSkill.Observation | KendoSkill.Counterattack | KendoSkill.Reaction,
                Location = "Advanced training area.",
                BadgeId = "skill_mastery", BadgeName = "Skill Mastery Badge", BadgeDescription = "Awarded for completing Level 3 — Skill Training.",
                Speed = 0.6f, Reaction = 0.45f, Pattern = 0.55f, Defense = 0.6f, Counter = 0.4f,
            },
            new LevelSpec
            {
                Id = "L4", Name = "Elite Training", Tier = DifficultyTier.Hard, Scene = KendoSceneId.Level4,
                Purpose = "The player faces highly skilled practitioners capable of advanced attacks and counters.",
                Skills = KendoSkill.CompleteSkillSet | KendoSkill.AdvancedTiming | KendoSkill.Defense | KendoSkill.Counterattack,
                Location = "Elite combat/training hall.",
                BadgeId = "elite_student", BadgeName = "Elite Student Badge", BadgeDescription = "Awarded for completing Level 4 — Elite Training.",
                Speed = 0.78f, Reaction = 0.32f, Pattern = 0.75f, Defense = 0.78f, Counter = 0.65f,
            },
            new LevelSpec
            {
                Id = "FINAL", Name = "The Former Student", Tier = DifficultyTier.VeryHard, Scene = KendoSceneId.FinalBattle,
                Purpose = "The player faces the master's former student. This is a real battle rather than another training exercise.",
                Skills = KendoSkill.CompleteSkillSet,
                Location = "The former student's location, a separate enemy area distinct from the master's dojo.",
                BadgeId = "masters_legacy", BadgeName = "Master's Legacy Badge", BadgeDescription = "Awarded for defeating the former student.",
                Speed = 0.95f, Reaction = 0.2f, Pattern = 0.95f, Defense = 0.92f, Counter = 0.9f,
            },
        };

        public static void BuildAll()
        {
            foreach (var folder in new[] { Levels, Badges, Dialogue, Opponents }) AssetFolders.Ensure(folder);

            var opening = Conversation("Conv_Opening", ConversationTrigger.Opening, "YOUR JOURNEY BEGINS",
                (Speaker.Master, "Welcome, student. This dojo has trained many fighters, but strength alone does not make a true Kendo practitioner. You are here because I believe you may have the discipline to learn."),
                (Speaker.Master, "You have come seeking the way of the sword. Good. But know this — Kendo is not about who strikes first, or who strikes hardest. It is about discipline, respect, and control of the self before control of the blade."),
                (Speaker.Master, "My former student possessed extraordinary talent. But he abandoned discipline and used his strength for the wrong purpose. I am too old to face him now. You must become strong enough to stop him."));

            var tutorial = Conversation("Conv_Tutorial", ConversationTrigger.Tutorial, "THE FIRST MOVEMENT",
                (Speaker.Master, "[Placeholder — GDD §13.2: the master explains the first Kendo movement and why it matters.]"));

            var readOpponent = Conversation("Conv_PreLevel03_ChooseValidOpening", ConversationTrigger.PreLevel, "CHOOSE A VALID OPENING",
                (Speaker.Master, "You understand how to stand and strike. Now, you must learn to read your opponent — their timing, their weakness. This is where Kendo truly begins."));

            var victory = Conversation("Conv_PostVictory", ConversationTrigger.PostVictory, "GREAT FIGHT!!",
                (Speaker.Master, "Your form improves. But do not grow comfortable — comfort is the enemy of progress."),
                (Speaker.Master, "You controlled your distance well. Your timing has improved, but your defense is still open after an attack. The next opponent will punish that mistake. Train carefully, then return to the floor."));

            var defeat = Conversation("Conv_PostDefeat", ConversationTrigger.PostDefeat, "TRY AGAIN",
                (Speaker.Master, "You attacked too quickly and ignored your opponent's movement. Kendo is not about attacking first. Watch, understand, and choose your moment. Try again."));

            var ready = Conversation("Conv_ReadyForUltimateTest", ConversationTrigger.ReadyForUltimateTest, "KE PERFECT STUDENT",
                (Speaker.Master, "You have completed the training. You are no longer the student who entered this dojo. You are ready to face the one who abandoned the path."));

            var encounter = Conversation("Conv_FormerStudentEncounter", ConversationTrigger.FormerStudentEncounter, "THE FORMER STUDENT",
                (Speaker.FormerStudent, "[Placeholder — GDD §13.5: his history with the master, his rejection of the teachings, and his belief that the player cannot win.]"));

            var ending = Conversation("Conv_Ending", ConversationTrigger.Ending, "THE MASTER'S LEGACY",
                (Speaker.Master, "[Placeholder — GDD §13.2: the meaning of the player's victory and the legacy of true Kendo.]"));

            var levels = new List<LevelDefinition>();
            for (var i = 0; i < Specs.Length; i++)
            {
                var s = Specs[i];
                var badge = AssetFolders.LoadOrCreate<BadgeDefinition>($"{Badges}/Badge_{i + 1:00}_{s.BadgeId}.asset");
                var badgePrefab = i < 4 ? AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.BadgePrefab(i + 1)) : null;
                using (var e = new SerializedEdit(badge))
                {
                    e.Set("badgeId", s.BadgeId).Set("order", i + 1).Set("displayName", s.BadgeName)
                     .Set("description", s.BadgeDescription).Set("displayPrefab", badgePrefab);
                }
                if (badgePrefab == null)
                {
                    Debug.LogWarning(i < 4
                        ? $"[Kendo] {s.BadgeName}: prefab not built yet – run Build Prefabs, then Build Data Assets again."
                        : $"[Kendo] {s.BadgeName}: no badge model was supplied (only 4 badge FBX files exist).");
                }

                var opponent = AssetFolders.LoadOrCreate<OpponentProfile>($"{Opponents}/Opponent_{s.Id}.asset");
                using (var e = new SerializedEdit(opponent))
                {
                    e.Set("profileId", s.Id).Set("displayName", i < 4 ? $"{s.Name} Opponent" : "Former Student")
                     .SetEnum("tier", s.Tier).Set("movementSpeed", s.Speed).Set("reactionTimeSeconds", s.Reaction)
                     .Set("attackPatternComplexity", s.Pattern).Set("defenseSkill", s.Defense).Set("counterattackAbility", s.Counter);
                }

                var level = AssetFolders.LoadOrCreate<LevelDefinition>($"{Levels}/Level_{i + 1:00}_{s.Id}.asset");
                using (var e = new SerializedEdit(level))
                {
                    e.Set("levelId", s.Id).Set("order", i + 1).Set("displayName", s.Name).SetEnum("difficulty", s.Tier)
                     .Set("purpose", s.Purpose).SetEnum("primarySkills", s.Skills).SetEnum("scene", s.Scene)
                     .Set("locationDescription", s.Location).Set("rewardBadge", badge).Set("opponent", opponent)
                     .Set("briefing", s.Id == "L3" ? readOpponent : null)
                     .Set("victory", s.Id == "L4" ? ready : victory)
                     .Set("defeat", defeat);
                    if (s.Id == "FINAL") e.Set("briefing", encounter).Set("victory", ending);
                }
                levels.Add(level);
            }

            var campaign = AssetFolders.LoadOrCreate<CampaignDefinition>($"{AssetFolders.Data}/Campaign.asset");
            using (var e = new SerializedEdit(campaign))
            {
                e.Set("opening", opening).Set("tutorial", tutorial).Set("finalMission", ready).Set("ending", ending);
                var list = e.Prop("levels");
                list.arraySize = levels.Count;
                for (var i = 0; i < levels.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Kendo] Data assets built from the GDD.");
        }

        private static ConversationDefinition Conversation(string id, ConversationTrigger trigger, string title, params (Speaker speaker, string text)[] lines)
        {
            var conv = AssetFolders.LoadOrCreate<ConversationDefinition>($"{Dialogue}/{id}.asset");
            using (var e = new SerializedEdit(conv))
            {
                e.Set("conversationId", id).SetEnum("trigger", trigger).Set("title", title);
                var list = e.Prop("lines");
                list.arraySize = lines.Length;
                for (var i = 0; i < lines.Length; i++)
                {
                    var el = list.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("speaker").intValue = (int)lines[i].speaker;
                    el.FindPropertyRelative("text").stringValue = lines[i].text;
                }
            }
            return conv;
        }
    }
}
