using System.Collections.Generic;
using Kendo.Rules;

namespace Kendo.Campaign
{
    public enum Speaker { Master, FormerStudent, Narrator, Player }

    public struct DialogueLine
    {
        public Speaker speaker;
        public string text;
        public DialogueLine(Speaker s, string t) { speaker = s; text = t; }
    }

    /// <summary>
    /// Story conversations (GDD §13–16). Lines that comment on the player are built from saved progress and statistics,
    /// so the master talks about what actually happened.
    /// </summary>
    public static class MasterDialogue
    {
        private static DialogueLine M(string t) => new DialogueLine(Speaker.Master, t);
        private static DialogueLine N(string t) => new DialogueLine(Speaker.Narrator, t);
        private static DialogueLine F(string t) => new DialogueLine(Speaker.FormerStudent, t);

        public static List<DialogueLine> IntroSequence() => new List<DialogueLine>
        {
            N("Long ago, a legendary kendo master gave his life to teaching the true art of the sword."),
            N("Of all his students, one stood above the rest. The master trained him personally until few could match him."),
            N("But power changed the student. Arrogant, he abandoned the dojo and began to use his skill to hurt others."),
            N("Now the master is old. Too old to face the one he created. He has one hope left: a new student."),
            N("That student is you."),
        };

        public static List<DialogueLine> FirstEnterDojo() => new List<DialogueLine>
        {
            M("Welcome, student. This dojo has trained many fighters, but strength alone does not make a true kendo practitioner. You are here because I believe you may have the discipline to learn."),
            M($"My former student, {CampaignContent.FormerStudentName}, possessed extraordinary talent. But he abandoned discipline and used his strength for the wrong purpose. I am too old to face him now."),
            M("You must become strong enough to stop him. Not with anger – with correct kendo. We begin with the fundamentals: seven lessons. Then you will fight."),
            M("The lesson board is on the wall to my left; the level board waits at the entrance. Your badges will be displayed on the shelf. Speak to me whenever you need guidance."),
        };

        public static List<DialogueLine> Greeting(CampaignProgress p)
        {
            var lines = new List<DialogueLine>();
            int next = p.NextLesson();
            if (next >= 0)
            {
                var l = CampaignContent.Lessons[next];
                lines.Add(M(next == 0 ? "Are you ready to begin? Your first lesson is the meaning of kendo itself." : $"Next is lesson {next + 1}: {l.title}. {l.summary}"));
                return lines;
            }
            for (int s = 0; s < CampaignContent.StageCount; s++)
            {
                if (p.data.stages[s].completed) continue;
                var st = CampaignContent.Stages[s];
                if (s == CampaignContent.StageCount - 1)
                {
                    lines.AddRange(BeforeFinal(p));
                    return lines;
                }
                var sp = p.data.stages[s];
                lines.Add(M(sp.losses > 0
                    ? $"{st.title} still waits. Last time {WeaknessText(sp.lastWeakness)}. Practise that, then go again."
                    : $"You are ready for {st.title}. {st.briefing[0]}"));
                return lines;
            }
            lines.Add(M("You have completed my legacy. The dojo is yours to practise in – revisit any lesson or bout whenever you wish."));
            return lines;
        }

        public static List<DialogueLine> BeforeLesson(int i, CampaignProgress p)
        {
            var l = CampaignContent.Lessons[i];
            var lines = new List<DialogueLine>();
            if (p.data.lessons[i].completed) lines.Add(M($"You want to practise {l.title} again? Good – repetition is how kendo is learned."));
            else if (i > 0)
            {
                float prev = p.data.lessons[i - 1].bestScore;
                if (prev >= 85f) lines.Add(M($"Your {CampaignContent.Lessons[i - 1].title} was strong ({prev:0} points). Now – {l.title}."));
                else lines.Add(M($"Your {CampaignContent.Lessons[i - 1].title} is acceptable ({prev:0} points), but keep working on it. Now – {l.title}."));
            }
            return lines;
        }

        public static List<DialogueLine> AfterLesson(int i, LessonSession session, PerformanceStats stats)
        {
            var l = CampaignContent.Lessons[i];
            var lines = new List<DialogueLine>();
            int attempts = 0;
            foreach (var e in session.log) if (e.Contains(" attempt ")) attempts++;
            lines.Add(M($"{l.title} – complete. {attempts} attempts, average {session.AverageScore:0} points."));
            Criterion? weakest = WeakestCriterion(stats);
            if (weakest.HasValue) lines.Add(M($"Across your training so far, {MasterFeedback.Name(weakest.Value)} is your weakest point. {MasterFeedback.Teaching(weakest.Value).improve}"));
            foreach (var c in l.completion) lines.Add(M(c));
            return lines;
        }

        public static List<DialogueLine> OnRepeatedFailure(MasterFeedback fb, int failures)
        {
            var lines = new List<DialogueLine>();
            if (failures < 3) return lines;
            lines.Add(M(fb.mainProblem.HasValue
                ? $"{failures} times now the same problem: {MasterFeedback.Name(fb.mainProblem.Value)}. Stop. Breathe. Watch me once more."
                : "Stop. Breathe. Watch me once more."));
            return lines;
        }

        public static List<DialogueLine> OnImprovement(float before, float now)
        {
            var lines = new List<DialogueLine>();
            if (now - before >= 15f) lines.Add(M($"Better. From {before:0} to {now:0}. That is what practice does."));
            return lines;
        }

        public static List<DialogueLine> BeforeStage(int s, CampaignProgress p)
        {
            var st = CampaignContent.Stages[s];
            var lines = new List<DialogueLine>();
            if (s == CampaignContent.StageCount - 1) return BeforeFinal(p);
            foreach (var b in st.briefing) lines.Add(M(b));
            var sp = p.data.stages[s];
            if (sp.losses > 0) lines.Add(M($"You lost here {sp.losses} time{(sp.losses > 1 ? "s" : "")}. {WeaknessAdvice(sp.lastWeakness)}"));
            lines.Add(M($"Objective: {st.objective}"));
            return lines;
        }

        public static List<DialogueLine> AfterStageWin(int s, MasterFeedback fb, CampaignProgress p, int awardedBadge)
        {
            var lines = new List<DialogueLine> { M(fb.headline) };
            if (fb.correct.Count > 0) lines.Add(M("You did well: " + string.Join("; ", fb.correct) + "."));
            if (fb.incorrect.Count > 0) lines.Add(M("But " + string.Join("; ", fb.incorrect) + ". " + fb.improve));
            if (awardedBadge >= 0) lines.Add(M($"Take this – the {CampaignContent.Badges[awardedBadge].name}. It is earned, not given."));
            if (s == CampaignContent.LevelCount - 1)
            {
                lines.Add(M($"You have completed the elite training. From today you are a {CampaignProgress.PerfectStudentTitle}."));
                lines.Add(M("You are ready for the ultimate test. When you are prepared, go to the level board – I will tell you where he waits."));
            }
            else if (fb.next.Length > 0) lines.Add(M(fb.next));
            return lines;
        }

        public static List<DialogueLine> AfterStageLoss(int s, MasterFeedback fb)
        {
            var lines = new List<DialogueLine> { M(fb.headline) };
            if (fb.incorrect.Count > 0) lines.Add(M("What went wrong: " + string.Join("; ", fb.incorrect) + "."));
            if (fb.principle.Length > 0) lines.Add(M(fb.principle));
            if (fb.improve.Length > 0) lines.Add(M(fb.improve));
            if (fb.correct.Count > 0) lines.Add(M("Not everything was bad: " + fb.correct[0] + "."));
            lines.Add(M("Kendo is not about attacking first. Watch, understand, and choose your moment. Try again."));
            return lines;
        }

        public static List<DialogueLine> BeforeFinal(CampaignProgress p)
        {
            var lines = new List<DialogueLine>
            {
                M("You have completed the training. You are no longer the student who entered this dojo."),
                M($"{CampaignContent.FormerStudentName} waits in his own hall beyond the mountains. He is fast, he feints, and he strikes the moment you begin to move."),
            };
            float best = 0;
            int weakest = -1;
            for (int s = 0; s < CampaignContent.LevelCount; s++)
            {
                if (p.data.stages[s].bestScore > best) best = p.data.stages[s].bestScore;
                if (p.data.stages[s].lastWeakness >= 0) weakest = p.data.stages[s].lastWeakness;
            }
            if (weakest >= 0) lines.Add(M($"Remember your old weakness: {WeaknessText(weakest)}. He will look for it."));
            lines.Add(M("You are ready to face the one who abandoned the path."));
            return lines;
        }

        public static List<DialogueLine> FormerStudentEncounter(CampaignProgress p) => new List<DialogueLine>
        {
            F("So the old man found another student. He sent you to do what he no longer can."),
            F("He taught me everything – rei, kamae, zanshin. Ceremony. Kendo is about winning. Nothing else."),
            F($"You trained {p.LessonsCompleted} lessons and four little bouts. I have fought for years. You cannot defeat me."),
            M("(Remember: the one who is calm sees everything.)"),
            F("Enough talk. Take your position."),
        };

        public static List<DialogueLine> FinalDefeat(MasterFeedback fb) => new List<DialogueLine>
        {
            F("You see? Ceremony does not win fights."),
            M(fb.incorrect.Count > 0 ? $"He beat you because {fb.incorrect[0]}. {fb.improve}" : "Do not chase him. Let him come to you."),
            M("Stand up. Bow. And try again."),
        };

        public static List<DialogueLine> FinalVictory(MasterFeedback fb) => new List<DialogueLine>
        {
            F("…Impossible. You struck like he does. Calm. Correct."),
            F("Perhaps I forgot what he tried to teach me."),
            M("You did not defeat him with strength, but with correct kendo – and you showed him respect afterwards. That is the difference between you."),
        };

        public static List<DialogueLine> Ending(CampaignProgress p) => new List<DialogueLine>
        {
            M("When you first came here, you did not know how to bow. Today you carry the way of the sword."),
            M($"Kendo is not about who strikes first or who strikes hardest. {CampaignContent.FormerStudentName} forgot that. You did not."),
            M("This badge is the last I will give. The Master's Legacy. Not because you won – because you understood."),
            M("The dojo is yours now. Teach others as I taught you."),
        };

        private static Criterion? WeakestCriterion(PerformanceStats stats)
        {
            if (stats == null) return null;
            Criterion? worst = null;
            float worstRate = 2f;
            for (int i = 0; i < PerformanceStats.CriterionCount && i < stats.criterionAttempts.Count; i++)
            {
                if (stats.criterionAttempts[i] < 3) continue;
                float r = (float)stats.criterionPasses[i] / stats.criterionAttempts[i];
                if (r < worstRate) { worstRate = r; worst = (Criterion)i; }
            }
            return worstRate < 0.8f ? worst : null;
        }

        public static string WeaknessText(int topic)
        {
            if (topic < 0) return "you lacked a clear plan";
            return MasterFeedback.Name((Criterion)topic) + " let you down";
        }

        public static string WeaknessAdvice(int topic)
        {
            if (topic < 0) return "Choose your moment instead of attacking at random.";
            return MasterFeedback.Teaching((Criterion)topic).improve;
        }
    }
}
