using System;
using System.Collections.Generic;
using System.Text;
using Kendo.Rules;

namespace Kendo.Campaign
{
    /// <summary>
    /// The master's feedback after an attempt or a bout. Every line is derived from evaluation results (criteria, measured
    /// details and stored statistics) – no random or generic filler. Structure (GDD §6): what went well, what went wrong,
    /// the principle missed, how to improve, what to practise next.
    /// </summary>
    public sealed class MasterFeedback
    {
        public bool success;
        public string headline;
        public readonly List<string> correct = new List<string>();
        public readonly List<string> incorrect = new List<string>();
        public string principle = "";
        public string improve = "";
        public string next = "";
        public Criterion? mainProblem;

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(headline);
            if (correct.Count > 0) sb.AppendLine("Good: " + string.Join("; ", correct) + ".");
            if (incorrect.Count > 0) sb.AppendLine("Not yet: " + string.Join("; ", incorrect) + ".");
            if (!string.IsNullOrEmpty(principle)) sb.AppendLine("Principle: " + principle);
            if (!string.IsNullOrEmpty(improve)) sb.AppendLine("How to improve: " + improve);
            if (!string.IsNullOrEmpty(next)) sb.AppendLine("Next: " + next);
            return sb.ToString().TrimEnd();
        }

        public static string Name(Criterion c)
        {
            switch (c)
            {
                case Criterion.DeliberateStrike: return "a real strike";
                case Criterion.ValidTarget: return "the right target";
                case Criterion.StrikingPart: return "the monouchi";
                case Criterion.Hasuji: return "hasuji";
                case Criterion.Spirit: return "kiai";
                case Criterion.Posture: return "posture";
                case Criterion.Zanshin: return "zanshin";
                case Criterion.KiKenTaiItchi: return "ki-ken-tai-itchi";
                case Criterion.Distance: return "distance";
                case Criterion.Timing: return "timing";
                case Criterion.BodyDrive: return "body drive";
                case Criterion.Sharpness: return "sharpness";
                case Criterion.Grip: return "grip";
                case Criterion.KensenLine: return "kensen line";
                case Criterion.LeftHand: return "left hand position";
                case Criterion.Feet: return "feet";
                case Criterion.Stillness: return "stillness";
                case Criterion.BowDepth: return "bow depth";
                case Criterion.BowHold: return "pause in the bow";
                case Criterion.Facing: return "facing";
                case Criterion.ShinaiAtSide: return "shinai at the side";
                case Criterion.Gaze: return "eyes";
                case Criterion.OkuriAshi: return "okuri-ashi";
                case Criterion.KamaeKept: return "kamae while moving";
                case Criterion.Accuracy: return "accuracy";
                case Criterion.Control: return "control";
                default: return c.ToString();
            }
        }

        /// <summary>(principle, how to improve) per criterion.</summary>
        public static (string principle, string improve) Teaching(Criterion c)
        {
            switch (c)
            {
                case Criterion.DeliberateStrike: return ("A touch is not a cut. Yuko-datotsu needs a deliberate strike with speed and intent.", "Raise the shinai (furikaburi) and cut through the target – do not push or tap.");
                case Criterion.ValidTarget: return ("Only men, kote, do and tsuki are targets, and only where they are permitted.", "Look at the target before you strike – your cut follows your eyes. Use the key for that target.");
                case Criterion.StrikingPart: return ("The datotsu-bu is the monouchi – the top part of the shinai – on the blade side.", "You are too close. Strike from a little further away so the top quarter of the shinai lands.");
                case Criterion.Hasuji: return ("Hasuji: the edge, opposite the tsuru, must travel along the line of the cut.", "Keep both hands on the centre line and cut straight; do not twist the wrists at the end.");
                case Criterion.Spirit: return ("A valid strike is made \"in high spirits\": the kiai calls the target as you strike.", "Press Space as the shinai lands – not before you start, not after you finish.");
                case Criterion.Posture: return ("Correct posture: both hands on the tsuka, back straight, balanced.", "Do not lean into the strike – let the legs carry you forward and keep the body upright.");
                case Criterion.Zanshin: return ("Zanshin – the remaining mind. A strike without readiness afterwards is not complete.", "After the cut keep moving through (hold W), then turn and point the kensen at your opponent again quickly. Never lower the shinai or celebrate.");
                case Criterion.KiKenTaiItchi: return ("Ki-ken-tai-itchi: spirit, sword and body arrive together.", "Press W, Space and the strike key as one movement. Watch the timing bars: foot and voice must sit on the impact line.");
                case Criterion.Distance: return ("Maai decides everything: from issoku-itto-no-maai one step reaches the target.", "Start from the gold distance mark – about one step plus a shinai length away.");
                case Criterion.Timing: return ("Strike at an opportunity: as the opponent moves, retreats, or finishes a technique.", "Wait for the opponent to commit, block, then answer – do not attack straight into a ready kamae.");
                case Criterion.BodyDrive: return ("The body leads the strike; the shinai does not reach out alone.", "Step in with W as you strike so your whole body moves toward the target.");
                case Criterion.Sharpness: return ("A good cut is sharp – fast at the moment of impact, then relaxed.", "Make the cut a short, quick snap at the end rather than a slow push.");
                case Criterion.Grip: return ("Chudan uses both hands: the left hand at the end of the tsuka, the right behind the guard.", "Press X to return to chudan with both hands on the shinai.");
                case Criterion.KensenLine: return ("The kensen points at the opponent's throat on the centre line – it is your shield and your threat.", "Hold the right mouse button and bring the tip onto the gold ring.");
                case Criterion.LeftHand: return ("The left fist sits about one fist in front of the navel; it controls the shinai.", "Keep the left hand low and centred – do not raise both hands to aim.");
                case Criterion.Feet: return ("Right foot forward, left foot behind, about a fist apart, left heel slightly raised.", "Step onto the footprints and let the rear foot settle behind before you hold the stance.");
                case Criterion.Stillness: return ("A calm kamae is stable – it does not drift or wave.", "Once the kensen is on the line, stop moving the mouse and hold for a moment.");
                case Criterion.BowDepth: return ("About 30° to the kamiza, about 15° to a teacher or partner.", "Watch the depth meter while holding B and release at the gold line.");
                case Criterion.BowHold: return ("A bow pauses at the bottom – it is not a nod.", "Keep B held for a moment at the bottom of the bow before rising.");
                case Criterion.Facing: return ("Bow squarely to whom you greet.", "Turn to face the kamiza or the master fully before you bow.");
                case Criterion.ShinaiAtSide: return ("During rei the shinai rests at the left side in sageto.", "Press X before bowing to lower the shinai to your left side.");
                case Criterion.Gaze: return ("When bowing to a person you keep your eyes on them – never on the floor.", "Keep the camera level on the master's face while bowing.");
                case Criterion.OkuriAshi: return ("Okuri-ashi: the rear foot always follows at once; the feet never cross.", "Let each step finish before starting the next – tap the key, do not hold it down.");
                case Criterion.KamaeKept: return ("Kamae is kept while moving; the kensen stays on the centre line.", "Stay in chudan while stepping – do not drop into sageto or swing the tip.");
                case Criterion.Accuracy: return ("Footwork must be precise to control distance.", "Count the steps and stop on the mark.");
                case Criterion.Control: return ("Controlled movement: no extra or hurried steps.", "Step only as often as the exercise asks.");
                default: return ("", "");
            }
        }

        private static string NextFor(Criterion c, LessonDef lesson)
        {
            switch (c)
            {
                case Criterion.Spirit:
                case Criterion.KiKenTaiItchi: return "Lesson 6 (Ki-Ken-Tai-Itchi) trains the kiai and the foot with the strike.";
                case Criterion.Zanshin: return "Lesson 7 (Zanshin) trains what happens after the strike.";
                case Criterion.Distance:
                case Criterion.StrikingPart: return "Practise suriashi (Lesson 4) to control your distance.";
                case Criterion.Feet:
                case Criterion.OkuriAshi: return "Repeat Lesson 4 (Suriashi) until the rear foot follows by itself.";
                case Criterion.KensenLine:
                case Criterion.Grip: return "Revisit Lesson 3 (Chudan-no-Kamae).";
                default: return lesson != null ? $"Repeat this exercise of {lesson.title} until it is calm and correct." : "Repeat the exercise.";
            }
        }

        /// <summary>Criteria ordered by how much they matter for this exercise: required first, then official, then by score.</summary>
        private static List<CriterionResult> Ranked(Evaluation ev, Criterion[] required, bool failedOnly)
        {
            var list = new List<CriterionResult>();
            foreach (var c in ev.criteria) if (!failedOnly || !c.passed) list.Add(c);
            list.Sort((a, b) =>
            {
                int ra = Array.IndexOf(required, a.criterion) >= 0 ? 0 : 1, rb = Array.IndexOf(required, b.criterion) >= 0 ? 0 : 1;
                if (ra != rb) return ra.CompareTo(rb);
                if (a.official != b.official) return a.official ? -1 : 1;
                return a.score.CompareTo(b.score);
            });
            return list;
        }

        public static MasterFeedback ForAttempt(LessonDef lesson, ExerciseDef ex, LessonSession session, Evaluation ev, bool success, PerformanceStats stats)
        {
            var fb = new MasterFeedback { success = success };
            var required = ex.required ?? new Criterion[0];
            if (ev == null)
            {
                fb.headline = "I saw nothing to evaluate. Perform the exercise first.";
                return fb;
            }

            // special flows
            if (ex.kind == ExerciseKind.ContactVersusStrike && session != null && !success)
            {
                var se = ev as StrikeEvaluation;
                if (se != null && se.invalidReason == InvalidReason.ContactOnly && session.ContactSeen)
                {
                    fb.headline = "Exactly – your shinai touched the men, but that is only contact. No judge would raise a flag.";
                    fb.correct.Add("you touched the target without a strike, so it scored nothing");
                    fb.principle = Teaching(Criterion.DeliberateStrike).principle;
                    fb.improve = "Now step back and make a real men strike with the left mouse button.";
                    fb.next = "A real strike follows.";
                    return fb;
                }
                if (!session.ContactSeen)
                {
                    fb.headline = "First show me a touch, not a strike: walk in slowly until your kensen rests on the men.";
                    fb.improve = "Hold the right mouse button to raise the tip, then step forward with W until it touches.";
                    return fb;
                }
            }

            var se2 = ev as StrikeEvaluation;
            bool wrongTarget = se2 != null && session != null && se2.target != Target.None && !LessonSession.SameTarget(se2.target, session.RequestedTarget);

            foreach (var c in Ranked(ev, required, false))
                if (c.passed && (required.Length == 0 || Array.IndexOf(required, c.criterion) >= 0 || c.official))
                    if (fb.correct.Count < 3) fb.correct.Add($"{Name(c.criterion)} – {c.detail}");
            var failed = Ranked(ev, required, true);
            foreach (var c in failed) if (fb.incorrect.Count < 3) fb.incorrect.Add($"{Name(c.criterion)} – {c.detail}");

            if (wrongTarget)
            {
                fb.success = false;
                fb.headline = $"I asked for {KendoEvaluationEngine.TargetName(session.RequestedTarget)}, you struck {KendoEvaluationEngine.TargetName(se2.target)}.";
                fb.mainProblem = Criterion.ValidTarget;
                fb.incorrect.Insert(0, "wrong target");
                var t = Teaching(Criterion.ValidTarget);
                fb.principle = t.principle;
                fb.improve = t.improve;
                fb.next = "Try again on the target I named.";
                return fb;
            }

            if (success)
            {
                int left = ex.requiredSuccesses - (session != null ? session.Successes : 0);
                fb.headline = left > 0 ? $"Good. That is correct. {left} more." : "Good. That is correct – this exercise is done.";
                // the most valuable remaining weakness, even on success
                foreach (var c in failed)
                {
                    var t = Teaching(c.criterion);
                    fb.mainProblem = c.criterion;
                    fb.principle = t.principle;
                    fb.improve = "Still: " + t.improve;
                    fb.next = NextFor(c.criterion, lesson);
                    break;
                }
                if (fb.mainProblem == null) fb.next = left > 0 ? "Repeat it the same way." : "Continue to the next exercise.";
                AddTrend(fb, stats, ev);
                return fb;
            }

            CriterionResult? main = null;
            foreach (var c in failed)
            {
                if (required.Length == 0 || Array.IndexOf(required, c.criterion) >= 0) { main = c; break; }
            }
            if (main == null && failed.Count > 0) main = failed[0];
            if (main.HasValue)
            {
                var t = Teaching(main.Value.criterion);
                fb.mainProblem = main.Value.criterion;
                fb.headline = $"Not yet – {Name(main.Value.criterion)}: {main.Value.detail}.";
                fb.principle = t.principle;
                fb.improve = t.improve;
                fb.next = session != null && session.FailuresInARow >= 3
                    ? "Watch my demonstration again (press G) and then try once more, slowly."
                    : "Try again.";
            }
            else
            {
                fb.headline = "Almost – the attempt was not clean enough. Try again, calmly.";
                fb.next = "Try again.";
            }
            AddTrend(fb, stats, ev);
            return fb;
        }

        private static void AddTrend(MasterFeedback fb, PerformanceStats stats, Evaluation ev)
        {
            if (stats == null || stats.recentScores.Count < 6) return;
            float recent = stats.Average(3), before = stats.Average(3, 3);
            if (recent < 0 || before < 0) return;
            if (recent - before >= 10f) fb.correct.Add($"your last attempts improved from {before:0} to {recent:0} points");
            else if (before - recent >= 15f) fb.incorrect.Add($"your last attempts dropped from {before:0} to {recent:0} points – slow down");
        }

        // ------------------------------------------------------------------ bouts

        /// <summary>Per-bout summary used after a level (victory or defeat). <paramref name="strikes"/> are the player's evaluated strikes.</summary>
        public static MasterFeedback ForBout(LevelDef stage, bool won, int pointsFor, int pointsAgainst, IList<StrikeEvaluation> strikes, BoutStats bout)
        {
            var fb = new MasterFeedback { success = won };
            var fails = new int[PerformanceStats.CriterionCount];
            var passes = new int[PerformanceStats.CriterionCount];
            int valid = 0, contacts = 0;
            foreach (var s in strikes)
            {
                if (s.invalidReason == InvalidReason.Missed) continue;
                if (s.invalidReason == InvalidReason.ContactOnly) contacts++;
                if (s.yukoDatotsu) valid++;
                foreach (var c in s.criteria) { if (c.passed) passes[(int)c.criterion]++; else fails[(int)c.criterion]++; }
            }
            int landed = 0;
            foreach (var s in strikes) if (s.invalidReason != InvalidReason.Missed) landed++;
            int missed = strikes.Count - landed;

            // best strength: most passed official criterion
            int best = -1, worst = -1;
            for (int i = 0; i < 12; i++)
            {
                if (passes[i] + fails[i] == 0) continue;
                if (best < 0 || passes[i] * (passes[best] + fails[best]) > passes[best] * (passes[i] + fails[i])) best = i;
                if (fails[i] > 0 && (worst < 0 || fails[i] > fails[worst])) worst = i;
            }
            fb.headline = won
                ? $"You won {pointsFor}–{pointsAgainst}. {stage.title} is complete."
                : $"You lost {pointsFor}–{pointsAgainst}. Listen, and then try again.";
            fb.correct.Add($"{valid} of your {strikes.Count} strikes were valid strikes ({landed} reached a target)");
            if (best >= 0) fb.correct.Add($"your {Name((Criterion)best)} was reliable ({passes[best]} of {passes[best] + fails[best]})");
            if (bout != null && bout.blocks > 0) fb.correct.Add($"you blocked {bout.blocks} attacks" + (bout.counters > 0 ? $" and countered {bout.counters} times" : ""));

            // diagnosis in GDD §6.2 terms, from measurements
            Criterion? main = worst >= 0 ? (Criterion?)worst : null;
            string diagnosis = null;
            if (bout != null)
            {
                if (bout.conceded > 0 && bout.concededWhileRecovering * 2 >= bout.conceded) diagnosis = "you left an opening after your attacks – the opponent struck while you were recovering";
                else if (bout.conceded > 0 && bout.blocks == 0 && bout.opponentStrikes >= 3) diagnosis = "you failed to block – you never raised your guard against an attack";
                else if (strikes.Count >= 6 && bout.intoReadyKamae * 2 > strikes.Count) diagnosis = "you attacked too aggressively, straight into a ready guard";
                else if (missed * 2 > strikes.Count && strikes.Count >= 4) diagnosis = "your distance was wrong – most strikes did not reach";
                else if (bout.jogai > 0) diagnosis = "your positioning was poor – you stepped out of the court";
                else if (bout.repeatedPatternConceded >= 2) diagnosis = "you did not recognise the opponent's pattern – the same attack scored twice";
            }
            if (contacts > 0) fb.incorrect.Add($"{contacts} contacts were only touches, not strikes");
            if (main.HasValue) fb.incorrect.Add($"{Name(main.Value)} failed {fails[(int)main.Value]} times");
            if (diagnosis != null) fb.incorrect.Insert(0, diagnosis);

            if (main.HasValue)
            {
                var t = Teaching(main.Value);
                fb.mainProblem = main;
                fb.principle = t.principle;
                fb.improve = t.improve;
            }
            if (diagnosis != null && diagnosis.StartsWith("you left an opening")) fb.improve = "After every strike, keep moving through and return to kamae at once – zanshin protects you. " + fb.improve;
            if (diagnosis != null && diagnosis.StartsWith("you failed to block")) fb.improve = "Hold Shift to raise your guard as the opponent's shinai rises, then answer with a counter. " + fb.improve;
            if (diagnosis != null && diagnosis.StartsWith("you attacked too aggressively")) fb.improve = "Wait for an opportunity – as he moves, after he strikes, or after you block. " + fb.improve;

            int s0 = stage.index;
            fb.next = won
                ? (s0 + 1 < CampaignContent.StageCount ? $"Next: {CampaignContent.Stages[s0 + 1].title}. It will test {string.Join(", ", CampaignContent.Stages[s0 + 1].skills)}." : "The legacy is yours.")
                : (main.HasValue ? NextFor(main.Value, null) + " Then retry the level." : "Retry the level.");
            return fb;
        }
    }

    /// <summary>What happened in one bout (collected by the game's match director).</summary>
    [Serializable]
    public sealed class BoutStats
    {
        public int blocks, counters, conceded, concededWhileRecovering, opponentStrikes, intoReadyKamae, jogai, repeatedPatternConceded;
    }
}
