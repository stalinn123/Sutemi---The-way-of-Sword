using System;

namespace Kendo.Campaign
{
    public enum GateResult { Open, LessonLocked, StageLocked, FinalLocked, Unknown }

    /// <summary>
    /// The one progression system: lesson order, level unlocks, badges, titles and checkpoints. Operates on
    /// <see cref="KendoSaveData"/>; the game saves after every change it reports. Mirrored by Tools/Campaign/campaign_ref.py.
    /// </summary>
    public sealed class CampaignProgress
    {
        public const string PerfectStudentTitle = "KE Perfect Student";
        public readonly KendoSaveData data;

        public CampaignProgress(KendoSaveData data)
        {
            this.data = data ?? KendoSaveData.NewGame("");
            this.data.Normalize();
        }

        // ---------------------------------------------------------------- lessons

        public int LessonsCompleted
        {
            get { int n = 0; foreach (var l in data.lessons) if (l.completed) n++; return n; }
        }

        public bool AllLessonsCompleted => LessonsCompleted == CampaignContent.LessonCount;

        /// <summary>Lessons are sequential: lesson i opens when lesson i-1 is complete. Completed lessons can always be revisited.</summary>
        public GateResult CanStartLesson(int i)
        {
            if (i < 0 || i >= CampaignContent.LessonCount) return GateResult.Unknown;
            if (data.lessons[i].completed) return GateResult.Open;
            for (int k = 0; k < i; k++) if (!data.lessons[k].completed) return GateResult.LessonLocked;
            return GateResult.Open;
        }

        public int NextLesson()
        {
            for (int i = 0; i < CampaignContent.LessonCount; i++) if (!data.lessons[i].completed) return i;
            return -1;
        }

        public void BeginLesson(int i)
        {
            if (CanStartLesson(i) != GateResult.Open) throw new InvalidOperationException($"lesson {i} is locked");
            data.checkpoint.lesson = i;
            data.checkpoint.scene = KendoSceneNames.TrainingDojo;
            var lp = data.lessons[i];
            if (lp.completed) { lp.exercise = 0; lp.successes = 0; } // revisiting starts fresh, completion is kept
        }

        /// <summary>Checkpoint inside a lesson after each successful attempt.</summary>
        public void RecordLessonCheckpoint(int lesson, int exercise, int successes, float score)
        {
            var lp = data.lessons[lesson];
            lp.exercise = exercise;
            lp.successes = successes;
            lp.lastScore = score;
            if (score > lp.bestScore) lp.bestScore = score;
        }

        public void RecordLessonAttempt(int lesson) => data.lessons[lesson].attempts++;

        public void CompleteLesson(int lesson, float score)
        {
            var lp = data.lessons[lesson];
            lp.completed = true;
            lp.exercise = 0;
            lp.successes = 0;
            if (score > lp.bestScore) lp.bestScore = score;
            if (data.checkpoint.lesson == lesson) data.checkpoint.lesson = -1;
        }

        public void MarkLessonIntroSeen(int lesson) => data.lessons[lesson].introSeen = true;

        // ---------------------------------------------------------------- stages (levels + final)

        /// <summary>Level 1 needs all seven lessons; each level needs the previous one; the final needs all four levels.</summary>
        public GateResult CanEnterStage(int s)
        {
            if (s < 0 || s >= CampaignContent.StageCount) return GateResult.Unknown;
            if (!AllLessonsCompleted) return GateResult.LessonLocked;
            if (s == CampaignContent.StageCount - 1)
            {
                for (int k = 0; k < CampaignContent.LevelCount; k++) if (!data.stages[k].completed) return GateResult.FinalLocked;
                return GateResult.Open;
            }
            if (s > 0 && !data.stages[s - 1].completed) return GateResult.StageLocked;
            return GateResult.Open;
        }

        public string LockReason(int s)
        {
            switch (CanEnterStage(s))
            {
                case GateResult.LessonLocked: return $"Complete the seven lessons first ({LessonsCompleted}/7).";
                case GateResult.StageLocked: return $"Complete {CampaignContent.Stages[s - 1].title} first.";
                case GateResult.FinalLocked: return "Complete all four levels first.";
                case GateResult.Unknown: return "Unknown stage.";
                default: return "";
            }
        }

        public void EnterStage(int s)
        {
            if (CanEnterStage(s) != GateResult.Open) throw new InvalidOperationException($"stage {s} is locked");
            data.stages[s].attempts++;
            data.checkpoint.stage = s;
            data.checkpoint.scene = CampaignContent.Stages[s].scene;
        }

        /// <summary>Records a won bout. Returns the badge index awarded now, or -1 if it was already owned.</summary>
        public int CompleteStage(int s, float score)
        {
            if (CanEnterStage(s) != GateResult.Open) throw new InvalidOperationException($"stage {s} is locked");
            var sp = data.stages[s];
            sp.completed = true;
            sp.wins++;
            sp.lastScore = score;
            if (score > sp.bestScore) sp.bestScore = score;
            int badge = CampaignContent.Stages[s].badge;
            int awarded = -1;
            if (!data.badges.Contains(badge)) { data.badges.Add(badge); awarded = badge; }
            if (s == CampaignContent.LevelCount - 1) data.title = PerfectStudentTitle;
            data.checkpoint.stage = -1;
            data.checkpoint.scene = s == CampaignContent.StageCount - 1 ? KendoSceneNames.Ending : KendoSceneNames.TrainingDojo;
            return awarded;
        }

        public void FailStage(int s, float score, int weaknessTopic)
        {
            var sp = data.stages[s];
            sp.losses++;
            sp.lastScore = score;
            sp.lastWeakness = weaknessTopic;
            data.checkpoint.stage = s; // retry from the level start
        }

        public bool HasBadge(int b) => data.badges.Contains(b);
        public bool CampaignComplete => data.stages[CampaignContent.StageCount - 1].completed;

        /// <summary>Where "Continue" goes. Never Boot or the menu; falls back to the dojo when the saved stage is locked.</summary>
        public string ResumeScene()
        {
            if (!data.story.introSeen) return KendoSceneNames.TrainingDojo;
            int s = data.checkpoint.stage;
            if (s >= 0 && s < CampaignContent.StageCount && CanEnterStage(s) == GateResult.Open) return CampaignContent.Stages[s].scene;
            if (data.checkpoint.scene == KendoSceneNames.Ending && CampaignComplete) return KendoSceneNames.Ending;
            return KendoSceneNames.TrainingDojo;
        }

        /// <summary>The next thing the master suggests, used by dialogue and the HUD objective line.</summary>
        public string NextObjective()
        {
            int l = NextLesson();
            if (l >= 0) return $"Lesson {l + 1}: {CampaignContent.Lessons[l].title} – speak to {CampaignContent.MasterName} or use the lesson board.";
            for (int s = 0; s < CampaignContent.StageCount; s++)
                if (!data.stages[s].completed) return $"{CampaignContent.Stages[s].title} – use the level board at the entrance.";
            return "The campaign is complete. You may revisit any lesson or level.";
        }
    }

    /// <summary>Scene file names (also in Kendo.Core.KendoScenes – Kendo.Campaign has no engine references).</summary>
    public static class KendoSceneNames
    {
        public const string Boot = "00_Boot";
        public const string MainMenu = "01_MainMenu";
        public const string TrainingDojo = "02_TrainingDojo";
        public const string Level1 = "03_Level1";
        public const string Level2 = "04_Level2";
        public const string Level3 = "05_Level3";
        public const string Level4 = "06_Level4";
        public const string FinalBattle = "07_FinalBattle";
        public const string Ending = "08_Ending";
        public static readonly string[] All = { Boot, MainMenu, TrainingDojo, Level1, Level2, Level3, Level4, FinalBattle, Ending };
    }
}
