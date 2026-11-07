using System;
using System.Collections.Generic;
using Kendo.Rules;

namespace Kendo.Campaign
{
    public enum LessonStep { Intro, Explain, Demonstration, Practice, Feedback, Complete }

    [Serializable]
    public sealed class HandlingEvidence
    {
        public bool reachedSageto;
        public float tipBelowGripM;   // how far the kensen is below the grip in sageto
        public bool onLeftSide;
        public float heldSeconds;
        public bool returnedToChudan;
        public bool bothHandsInChudan = true;
    }

    /// <summary>One measured attempt from the game (or a test). Exactly one payload is set, matching the exercise kind.</summary>
    public sealed class LessonAttempt
    {
        public ExerciseKind kind;
        public Target lookedAt;                 // IdentifyTarget
        public HandlingEvidence handling;       // ShinaiHandling
        public StrikeEvidence strike;           // ContactVersusStrike / Strike / KiKenTaiItchi / Zanshin
        public ReiEvidence rei;                 // Rei
        public ReiPlace reiPlace;
        public KamaeEvidence kamae;             // Kamae
        public SuriashiEvidence suriashi;       // Suriashi

        public static LessonAttempt Identify(Target t) => new LessonAttempt { kind = ExerciseKind.IdentifyTarget, lookedAt = t };
        public static LessonAttempt Handling(HandlingEvidence h) => new LessonAttempt { kind = ExerciseKind.ShinaiHandling, handling = h };
        public static LessonAttempt Strike(ExerciseKind k, StrikeEvidence e) => new LessonAttempt { kind = k, strike = e };
        public static LessonAttempt Bow(ReiPlace place, ReiEvidence r) => new LessonAttempt { kind = ExerciseKind.Rei, rei = r, reiPlace = place };
        public static LessonAttempt Stance(KamaeEvidence k) => new LessonAttempt { kind = ExerciseKind.Kamae, kamae = k };
        public static LessonAttempt Footwork(SuriashiEvidence s) => new LessonAttempt { kind = ExerciseKind.Suriashi, suriashi = s };
    }

    public sealed class AttemptResult
    {
        public bool success;
        public bool exerciseComplete;
        public bool lessonComplete;
        public Evaluation evaluation;
        public MasterFeedback feedback;
    }

    /// <summary>
    /// Runs one lesson: introduce → explain → demonstrate (player observes) → practise → evaluate → feedback, repeated
    /// until every exercise has its required successes. Pure logic; the game drives it with measured attempts.
    /// Evaluation always goes through <see cref="KendoEvaluationEngine"/> – a button press alone never succeeds.
    /// </summary>
    public sealed class LessonSession
    {
        public readonly LessonDef def;
        private readonly LessonProgress progress;
        private readonly PerformanceStats stats;
        public LessonStep Step { get; private set; }
        public int LineIndex { get; private set; }
        public int ExerciseIndex { get; private set; }
        public int Successes { get; private set; }
        public int AttemptsThisExercise { get; private set; }
        public int FailuresInARow { get; private set; }
        public AttemptResult LastResult { get; private set; }
        public bool ContactSeen { get; private set; }  // ContactVersusStrike: the touch was observed
        public float ScoreSum { get; private set; }
        public int ScoredAttempts { get; private set; }
        public bool Revisit { get; }
        public readonly List<string> log = new List<string>();

        public LessonSession(LessonDef def, LessonProgress progress, PerformanceStats stats, bool skipIntro = false)
        {
            this.def = def;
            this.progress = progress ?? new LessonProgress();
            this.stats = stats;
            Revisit = this.progress.completed;
            ExerciseIndex = Math.Min(this.progress.exercise, def.exercises.Length - 1);
            Successes = this.progress.successes;
            bool resume = ExerciseIndex > 0 || Successes > 0;
            Step = skipIntro || resume ? LessonStep.Practice : LessonStep.Intro;
            if (Step == LessonStep.Practice && !resume && skipIntro) log.Add("intro skipped");
        }

        public ExerciseDef Exercise => def.exercises[Math.Min(ExerciseIndex, def.exercises.Length - 1)];

        public string[] CurrentLines()
        {
            switch (Step)
            {
                case LessonStep.Intro: return def.intro;
                case LessonStep.Explain: return def.explain;
                case LessonStep.Demonstration: return def.demoNarration;
                case LessonStep.Complete: return def.completion;
                default: return new string[0];
            }
        }

        public string CurrentLine
        {
            get
            {
                var lines = CurrentLines();
                return LineIndex < lines.Length ? lines[LineIndex] : "";
            }
        }

        /// <summary>Target requested for the next success (strike / identify exercises).</summary>
        public Target RequestedTarget
        {
            get
            {
                var t = Exercise.targets;
                return t.Length == 0 ? Target.None : t[Math.Min(Successes, t.Length - 1) % t.Length];
            }
        }

        public ReiPlace RequestedReiPlace
        {
            get
            {
                var s = Exercise.reiSequence;
                return s.Length == 0 ? ReiPlace.ToMaster : s[Math.Min(Successes, s.Length - 1)];
            }
        }

        /// <summary>0..1 over all exercises (progress indicator).</summary>
        public float Progress01
        {
            get
            {
                int total = 0, done = 0;
                for (int i = 0; i < def.exercises.Length; i++)
                {
                    total += def.exercises[i].requiredSuccesses;
                    if (i < ExerciseIndex) done += def.exercises[i].requiredSuccesses;
                    else if (i == ExerciseIndex) done += Math.Min(Successes, def.exercises[i].requiredSuccesses);
                }
                if (Step == LessonStep.Complete) return 1f;
                return total == 0 ? 0f : (float)done / total;
            }
        }

        /// <summary>Next dialogue line, or the next step when the lines are exhausted.</summary>
        public void Advance()
        {
            var lines = CurrentLines();
            if (Step == LessonStep.Practice || Step == LessonStep.Feedback) return;
            if (LineIndex + 1 < lines.Length) { LineIndex++; return; }
            LineIndex = 0;
            if (Step == LessonStep.Intro) Step = LessonStep.Explain;
            else if (Step == LessonStep.Explain) Step = LessonStep.Demonstration;
            else if (Step == LessonStep.Demonstration) Step = LessonStep.Practice;
        }

        /// <summary>Skip introductory explanation / demonstration and go straight to practice.</summary>
        public void Skip()
        {
            if (Step == LessonStep.Intro || Step == LessonStep.Explain || Step == LessonStep.Demonstration)
            {
                log.Add($"skipped {Step}");
                Step = LessonStep.Practice;
                LineIndex = 0;
            }
        }

        /// <summary>Watch the demonstration again (allowed during practice and feedback).</summary>
        public void ReplayDemonstration()
        {
            if (Step == LessonStep.Practice || Step == LessonStep.Feedback)
            {
                Step = LessonStep.Demonstration;
                LineIndex = 0;
            }
        }

        public AttemptResult Submit(LessonAttempt a)
        {
            if (Step != LessonStep.Practice) throw new InvalidOperationException("attempts are only accepted during practice");
            var ex = Exercise;
            if (a.kind != ex.kind && !(ex.kind == ExerciseKind.ContactVersusStrike && a.kind == ExerciseKind.Strike))
                throw new ArgumentException($"attempt kind {a.kind} does not match exercise {ex.kind}");
            Evaluation ev;
            bool success;
            Evaluate(ex, a, out ev, out success);
            AttemptsThisExercise++;
            if (ev != null && stats != null) stats.Record(ev);
            if (ev != null) { ScoreSum += ev.performanceScore; ScoredAttempts++; }
            progress.attempts++;
            var r = new AttemptResult { success = success, evaluation = ev };
            if (success)
            {
                Successes++;
                FailuresInARow = 0;
                if (Successes >= ex.requiredSuccesses)
                {
                    r.exerciseComplete = true;
                    if (ExerciseIndex + 1 >= def.exercises.Length) r.lessonComplete = true;
                }
            }
            else
            {
                FailuresInARow++;
            }
            r.feedback = MasterFeedback.ForAttempt(def, ex, this, ev, success, stats);
            LastResult = r;
            log.Add($"{ex.id} attempt {AttemptsThisExercise}: {(success ? "success" : "fail")} score {(ev != null ? ev.performanceScore : 0):0}");
            Step = LessonStep.Feedback;
            return r;
        }

        /// <summary>After the feedback panel: next attempt, next exercise, or lesson complete.</summary>
        public void ContinueAfterFeedback()
        {
            if (Step != LessonStep.Feedback) return;
            var r = LastResult;
            if (r != null && r.lessonComplete)
            {
                Step = LessonStep.Complete;
                LineIndex = 0;
                return;
            }
            if (r != null && r.exerciseComplete)
            {
                ExerciseIndex++;
                Successes = 0;
                AttemptsThisExercise = 0;
                ContactSeen = false;
            }
            Step = LessonStep.Practice;
        }

        public float AverageScore => ScoredAttempts > 0 ? ScoreSum / ScoredAttempts : 0f;

        // ------------------------------------------------------------------ evaluation per exercise kind

        private void Evaluate(ExerciseDef ex, LessonAttempt a, out Evaluation ev, out bool success)
        {
            var profile = ex.profile == "Intermediate" ? KendoEvaluationProfile.Intermediate() : KendoEvaluationProfile.Beginner();
            var rules = KendoRuleConfig.TrainingBout();
            rules.allowTsuki = ex.allowTsuki;
            switch (ex.kind)
            {
                case ExerciseKind.IdentifyTarget:
                {
                    var want = RequestedTarget;
                    bool ok = SameTarget(a.lookedAt, want);
                    ev = new Evaluation();
                    ev.criteria.Add(new CriterionResult(Criterion.ValidTarget, false, ok, ok ? 1f : 0f, FeedbackTopic.Target,
                        a.lookedAt == Target.None ? "you were not looking at a target" : $"you chose {KendoEvaluationEngine.TargetName(a.lookedAt)}"));
                    ev.performanceScore = ok ? 100f : 0f;
                    ev.passed = ok;
                    success = ok;
                    return;
                }
                case ExerciseKind.ShinaiHandling:
                {
                    var h = a.handling ?? new HandlingEvidence();
                    ev = new Evaluation();
                    bool side = h.reachedSageto && h.onLeftSide && h.tipBelowGripM > 0.25f;
                    ev.criteria.Add(new CriterionResult(Criterion.ShinaiAtSide, false, side, side ? 1f : (h.reachedSageto ? 0.5f : 0f), FeedbackTopic.Sageto,
                        !h.reachedSageto ? "the shinai never reached sageto" : $"tip {h.tipBelowGripM * 100f:0} cm below the grip, {(h.onLeftSide ? "left" : "right")} side"));
                    bool still = h.heldSeconds >= 1.0f;
                    ev.criteria.Add(new CriterionResult(Criterion.Stillness, false, still, KendoEvaluationEngine.Clamp01(h.heldSeconds), FeedbackTopic.Stillness,
                        $"held {h.heldSeconds:0.0} s"));
                    bool back = h.returnedToChudan && h.bothHandsInChudan;
                    ev.criteria.Add(new CriterionResult(Criterion.Grip, false, back, back ? 1f : 0f, FeedbackTopic.Grip,
                        back ? "returned to chudan with both hands" : "did not return to chudan"));
                    float s = 0;
                    foreach (var c in ev.criteria) s += c.score;
                    ev.performanceScore = (float)Math.Round(100f * s / 3f, 2);
                    ev.passed = side && still && back;
                    success = ev.passed;
                    return;
                }
                case ExerciseKind.Rei:
                {
                    var r = a.rei ?? new ReiEvidence();
                    var want = RequestedReiPlace;
                    r.targetAngleDeg = want == ReiPlace.ToMaster ? 15f : 30f;
                    r.requireGaze = want == ReiPlace.ToMaster;
                    ev = KendoEvaluationEngine.EvaluateRei(r, profile);
                    bool place = a.reiPlace == want;
                    if (!place)
                    {
                        ev.criteria.Add(new CriterionResult(Criterion.Facing, false, false, 0f, FeedbackTopic.Facing, "bowed at the wrong place"));
                        ev.passed = false;
                    }
                    success = ev.passed;
                    return;
                }
                case ExerciseKind.Kamae:
                    ev = KendoEvaluationEngine.EvaluateKamae(a.kamae ?? new KamaeEvidence { heldSeconds = 0 }, profile);
                    success = ev.passed;
                    return;
                case ExerciseKind.Suriashi:
                    ev = KendoEvaluationEngine.EvaluateSuriashi(a.suriashi ?? new SuriashiEvidence(), profile);
                    success = ev.passed;
                    return;
                case ExerciseKind.ContactVersusStrike:
                {
                    var se = KendoEvaluationEngine.EvaluateStrike(a.strike ?? new StrikeEvidence(), rules, profile);
                    ev = se;
                    if (!ContactSeen)
                    {
                        // phase 1: the student must produce an ordinary contact and see that it is not a strike
                        bool touch = se.invalidReason == InvalidReason.ContactOnly && a.strike != null && a.strike.contact;
                        if (touch) ContactSeen = true;
                        success = false;
                        return;
                    }
                    success = se.target != Target.None && SameTarget(se.target, RequestedTarget) && RequiredPassed(se, ex.required);
                    return;
                }
                default: // Strike, KiKenTaiItchi, Zanshin
                {
                    var se = KendoEvaluationEngine.EvaluateStrike(a.strike ?? new StrikeEvidence(), rules, profile);
                    ev = se;
                    success = se.target != Target.None && SameTarget(se.target, RequestedTarget) && RequiredPassed(se, ex.required);
                    return;
                }
            }
        }

        public static bool SameTarget(Target got, Target want)
        {
            if (got == want) return true;
            if (KendoEvaluationEngine.IsMen(got) && want == Target.Men) return true;
            if (KendoEvaluationEngine.IsDo(got) && want == Target.DoRight) return got == Target.DoRight;
            return false;
        }

        public static bool RequiredPassed(Evaluation ev, Criterion[] required)
        {
            foreach (var c in required)
            {
                var r = ev.Get(c);
                if (!r.HasValue || !r.Value.passed) return false;
            }
            return true;
        }
    }
}
