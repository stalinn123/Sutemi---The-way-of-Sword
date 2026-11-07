using System;
using System.Collections.Generic;

namespace Kendo.Campaign
{
    /// <summary>
    /// Everything that persists between sessions. Plain [Serializable] fields so Unity's JsonUtility can write it
    /// (no dictionaries, no properties). Version-checked on load by <c>Kendo.Game.SaveService</c>.
    /// </summary>
    [Serializable]
    public sealed class KendoSaveData
    {
        public const int CurrentVersion = 6;

        public int version = CurrentVersion;
        public string createdUtc = "";
        public string savedUtc = "";
        public int saveCount;
        public float playSeconds;

        public StoryFlags story = new StoryFlags();
        public List<LessonProgress> lessons = new List<LessonProgress>();
        public List<StageProgress> stages = new List<StageProgress>();
        public List<int> badges = new List<int>();
        public Checkpoint checkpoint = new Checkpoint();
        public PlayerSettings settings = new PlayerSettings();
        public PerformanceStats stats = new PerformanceStats();
        public string title = "";   // "KE Perfect Student" after Level 4

        public static KendoSaveData NewGame(string nowUtc)
        {
            var s = new KendoSaveData { createdUtc = nowUtc, savedUtc = nowUtc };
            s.Normalize();
            return s;
        }

        /// <summary>Repairs lists after loading an older or hand-edited save (never throws).</summary>
        public void Normalize()
        {
            if (story == null) story = new StoryFlags();
            if (lessons == null) lessons = new List<LessonProgress>();
            if (stages == null) stages = new List<StageProgress>();
            if (badges == null) badges = new List<int>();
            if (checkpoint == null) checkpoint = new Checkpoint();
            if (settings == null) settings = new PlayerSettings();
            if (stats == null) stats = new PerformanceStats();
            while (lessons.Count < CampaignContent.LessonCount) lessons.Add(new LessonProgress());
            while (stages.Count < CampaignContent.StageCount) stages.Add(new StageProgress());
            if (lessons.Count > CampaignContent.LessonCount) lessons.RemoveRange(CampaignContent.LessonCount, lessons.Count - CampaignContent.LessonCount);
            if (stages.Count > CampaignContent.StageCount) stages.RemoveRange(CampaignContent.StageCount, stages.Count - CampaignContent.StageCount);
            badges.RemoveAll(b => b < 0 || b >= CampaignContent.BadgeCount);
            var unique = new List<int>();
            foreach (var b in badges) if (!unique.Contains(b)) unique.Add(b);
            badges = unique;
            stats.Normalize();
            settings.Clamp();
            for (int i = 0; i < lessons.Count; i++)
            {
                var l = lessons[i];
                var def = CampaignContent.Lessons[i];
                if (l.exercise < 0 || l.exercise > def.exercises.Length) l.exercise = 0;
                if (l.successes < 0) l.successes = 0;
            }
        }
    }

    [Serializable]
    public sealed class StoryFlags
    {
        public bool introSeen;
        public bool metMaster;
        public bool perfectStudentDeclared;
        public bool finalBriefingSeen;
        public bool journeySeen;
        public bool formerStudentMet;
        public bool endingSeen;
    }

    [Serializable]
    public sealed class LessonProgress
    {
        public bool completed;
        public bool introSeen;
        public int exercise;        // checkpoint: current exercise index
        public int successes;       // checkpoint: successes in the current exercise
        public int attempts;
        public float bestScore;
        public float lastScore;
    }

    [Serializable]
    public sealed class StageProgress
    {
        public bool completed;
        public int attempts;
        public int wins;
        public int losses;
        public float bestScore;
        public float lastScore;
        public int lastWeakness = -1;  // FeedbackTopic of the main problem in the last bout
    }

    [Serializable]
    public sealed class Checkpoint
    {
        public string scene = "02_TrainingDojo";
        public int lesson = -1;     // lesson in progress (-1 none)
        public int stage = -1;      // stage entered (-1 none)
    }

    [Serializable]
    public sealed class PlayerSettings
    {
        public float masterVolume = 0.9f, musicVolume = 0.6f, sfxVolume = 0.9f, voiceVolume = 1.0f;
        public float mouseSensitivity = 1.0f;
        public bool invertY;
        public float footworkPace = 1.0f;
        public bool smoothFootwork;
        public bool showHints = true;
        public bool showControlsOverlay = true;
        public float textSpeed = 1.0f;
        public bool subtitles = true;
        public int quality = -1;      // -1 = keep the platform default
        public bool fullscreen = true;
        public float fov = 70f;
        public bool lowFurikaburi;    // smaller swings under low ceilings (also automatic)
        public float vrHeightOffset;  // XR calibration
        public float vrArmScale = 1f;

        public void Clamp()
        {
            masterVolume = C(masterVolume, 0, 1); musicVolume = C(musicVolume, 0, 1); sfxVolume = C(sfxVolume, 0, 1); voiceVolume = C(voiceVolume, 0, 1);
            mouseSensitivity = C(mouseSensitivity, 0.2f, 3f);
            footworkPace = C(footworkPace, 0.75f, 1.25f);
            textSpeed = C(textSpeed, 0.5f, 2f);
            fov = C(fov, 55f, 95f);
            vrHeightOffset = C(vrHeightOffset, -0.5f, 0.5f);
            vrArmScale = C(vrArmScale, 0.8f, 1.25f);
        }

        private static float C(float v, float a, float b) => float.IsNaN(v) ? a : (v < a ? a : (v > b ? b : v));
    }

    /// <summary>Per-criterion history used by the master to talk about real improvement and recurring mistakes.</summary>
    [Serializable]
    public sealed class PerformanceStats
    {
        public const int CriterionCount = 26;
        public List<int> criterionAttempts = new List<int>();
        public List<int> criterionPasses = new List<int>();
        public List<float> recentScores = new List<float>();   // last 20 evaluated strikes / exercises
        public int strikes, validStrikes, ippon, conceded, blocks, counters, contactsOnly;

        public void Normalize()
        {
            if (criterionAttempts == null) criterionAttempts = new List<int>();
            if (criterionPasses == null) criterionPasses = new List<int>();
            if (recentScores == null) recentScores = new List<float>();
            while (criterionAttempts.Count < CriterionCount) criterionAttempts.Add(0);
            while (criterionPasses.Count < CriterionCount) criterionPasses.Add(0);
            while (recentScores.Count > 20) recentScores.RemoveAt(0);
        }

        public float PassRate(Kendo.Rules.Criterion c)
        {
            int i = (int)c;
            if (i >= criterionAttempts.Count || criterionAttempts[i] == 0) return -1f;
            return (float)criterionPasses[i] / criterionAttempts[i];
        }

        public void Record(Kendo.Rules.Evaluation ev)
        {
            Normalize();
            foreach (var c in ev.criteria)
            {
                int i = (int)c.criterion;
                criterionAttempts[i]++;
                if (c.passed) criterionPasses[i]++;
            }
            recentScores.Add(ev.performanceScore);
            while (recentScores.Count > 20) recentScores.RemoveAt(0);
        }

        /// <summary>Average of the last <paramref name="n"/> scores before the most recent <paramref name="skip"/>.</summary>
        public float Average(int n, int skip = 0)
        {
            int end = recentScores.Count - skip;
            int start = Math.Max(0, end - n);
            if (end <= start) return -1f;
            float s = 0;
            for (int i = start; i < end; i++) s += recentScores[i];
            return s / (end - start);
        }
    }
}
