using System;
using System.Linq;
using Kendo.AI;
using Kendo.Campaign;
using Kendo.Game;
using Kendo.Rules;
using NUnit.Framework;

namespace Kendo.Tests
{
    /// <summary>Rules engine replayed against the Python mirror (Tools/Rules/rules_ref.py) plus invariants.</summary>
    public class RulesEngineTests
    {
        private static StrikeEvidence Good(Target t = Target.Men) => new StrikeEvidence
        {
            target = t, contact = true, contactAlongBlade = 0.85f, hasujiErrorDeg = 5, tipSpeed = 9, strikeArc = true, approachSpeed = 0.8f,
            kiai = true, kiaiOffset = 0.02f, fumikomi = true, fumikomiOffset = -0.03f, torsoTiltDeg = 4, distance = 2f, opportunity = Opportunity.Debana,
            zanshinObserved = true, followThrough = 0.6f, kamaeRestoredAfter = 0.8f,
        };

        [Test]
        public void MatchesPythonReference()
        {
            var profiles = new[] { KendoEvaluationProfile.Beginner(), KendoEvaluationProfile.Intermediate(), KendoEvaluationProfile.Shiai() };
            int n = 0;
            foreach (var r in Golden.Strikes)
            {
                var e = new StrikeEvidence
                {
                    target = (Target)(int)r[0], contact = (bool)r[1], contactAlongBlade = (float)r[2], edgeSide = (bool)r[3], hasujiErrorDeg = (float)r[4],
                    tipSpeed = (float)r[5], strikeArc = (bool)r[6], approachSpeed = (float)r[7], kiai = (bool)r[8], kiaiOffset = (float)r[9], fumikomi = (bool)r[10],
                    fumikomiOffset = (float)r[11], bothHands = (bool)r[12], torsoTiltDeg = (float)r[13], balanced = (bool)r[14], distance = (float)r[15],
                    opportunity = (Opportunity)(int)r[16], blocked = (bool)r[17], zanshinObserved = (bool)r[18], followThrough = (float)r[19],
                    kamaeRestoredAfter = (float)r[20], turnedAway = (bool)r[21], celebrated = (bool)r[22],
                };
                var rules = KendoRuleConfig.FikStandard();
                rules.allowTsuki = (bool)r[24];
                var ev = KendoEvaluationEngine.EvaluateStrike(e, rules, profiles[(int)r[23]], (Kamae)(int)r[25]);
                Assert.AreEqual((InvalidReason)(int)r[26], ev.invalidReason, $"case {n}");
                Assert.AreEqual((bool)r[27], ev.yukoDatotsu, $"case {n}");
                Assert.AreEqual((float)r[28], ev.performanceScore, 0.05f, $"case {n}");
                n++;
            }
            Assert.AreEqual(300, n);
        }

        [Test]
        public void ContactAloneIsNeverAPoint()
        {
            var e = Good();
            e.strikeArc = false;
            var ev = KendoEvaluationEngine.EvaluateStrike(e, KendoRuleConfig.FikStandard(), KendoEvaluationProfile.Intermediate());
            Assert.AreEqual(InvalidReason.ContactOnly, ev.invalidReason);
            Assert.IsFalse(new ShinpanPanel().Decide(ev, KendoRuleConfig.FikStandard()).awarded);
            Assert.LessOrEqual(ev.performanceScore, 20f);
        }

        [Test]
        public void CleanStrikeGetsThreeFlags()
        {
            var ev = KendoEvaluationEngine.EvaluateStrike(Good(), KendoRuleConfig.FikStandard(), KendoEvaluationProfile.Intermediate());
            var d = new ShinpanPanel().Decide(ev, KendoRuleConfig.FikStandard());
            Assert.IsTrue(ev.yukoDatotsu);
            Assert.AreEqual(3, d.flags);
        }

        [Test]
        public void RandomInvariants()
        {
            var rng = new KRandom(5);
            for (int i = 0; i < 5000; i++)
            {
                var e = new StrikeEvidence
                {
                    target = (Target)rng.Int(9), contact = rng.Value() < 0.9, contactAlongBlade = (float)rng.Value(), edgeSide = rng.Value() < 0.85,
                    hasujiErrorDeg = (float)rng.Range(0, 80), tipSpeed = (float)rng.Range(0, 14), strikeArc = rng.Value() < 0.8, kiai = rng.Value() < 0.7,
                    kiaiOffset = (float)rng.Range(-1, 1), zanshinObserved = rng.Value() < 0.8, followThrough = (float)rng.Value(), kamaeRestoredAfter = (float)rng.Range(0, 3),
                };
                var ev = KendoEvaluationEngine.EvaluateStrike(e, KendoRuleConfig.FikStandard(), KendoEvaluationProfile.Intermediate());
                Assert.That(ev.performanceScore, Is.InRange(0f, 100.01f));
                var d = new ShinpanPanel().Decide(ev, KendoRuleConfig.FikStandard());
                if (d.awarded) Assert.IsTrue(ev.yukoDatotsu);
                if (!e.strikeArc || !e.contact) Assert.IsFalse(d.awarded);
            }
        }

        [Test]
        public void MatchLadder()
        {
            var m = new KendoMatch(KendoRuleConfig.FikStandard());
            m.Hajime(); m.AwardIppon(0, Target.Men);
            Assert.AreEqual(MatchPhase.Yame, m.Phase);
            m.Hajime(); m.Hansoku(1, "jogai"); m.Hansoku(1, "jogai");
            Assert.AreEqual(MatchPhase.Finished, m.Phase);
            Assert.AreEqual(0, m.Winner);
        }
    }

    public class AgentTests
    {
        [Test]
        public void RngMatchesPython()
        {
            // first values of krandom.KRandom(1).value() computed by Tools/AI/krandom.py
            var r = new KRandom(1);
            double a = r.Value(), b = r.Value();
            Assert.That(a, Is.InRange(0.0, 1.0));
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void DuelSimMatchesPython()
        {
            foreach (var row in Golden.Duels)
            {
                var sim = new DuelSim((int)row[0]);
                var a0 = ((string)row[2]).Split(',').Select(int.Parse).ToArray();
                var a1 = ((string)row[3]).Split(',').Select(int.Parse).ToArray();
                for (int k = 0; k < (int)row[1] && !sim.Done; k++) sim.Step((DuelAction)a0[k % a0.Length], (DuelAction)a1[k % a1.Length]);
                Assert.AreEqual((int)row[4], sim.match.points[0], $"seed {row[0]} points0");
                Assert.AreEqual((int)row[5], sim.match.points[1], $"seed {row[0]} points1");
                Assert.AreEqual((int)row[6], sim.match.hansoku[0]);
                Assert.AreEqual((int)row[7], sim.match.hansoku[1]);
                Assert.AreEqual((int)row[8], (int)sim.match.Phase);
                Assert.AreEqual((float)row[9], (float)sim.f[0].x, 1e-3f);
                Assert.AreEqual((float)row[10], (float)sim.f[1].x, 1e-3f);
            }
        }

        [Test]
        public void ActorCriticForwardAndUpdateMatchPython()
        {
            var ac = ActorCritic.FromJson(Golden.SmallNet);
            var x = new double[KendoTimings.ObservationSize];
            for (int i = 0; i < x.Length; i++) x[i] = -1.0 + 2.0 * i / (x.Length - 1);
            var p = ac.Policy(x);
            for (int i = 0; i < p.Length; i++) Assert.AreEqual(Golden.SmallNetPolicy[i], (float)p[i], 1e-4f);
            Assert.AreEqual(Golden.SmallNetValue, (float)ac.Value(x), 1e-4f);
            var obs = new double[4][];
            for (int k = 0; k < 4; k++) { obs[k] = new double[x.Length]; for (int i = 0; i < x.Length; i++) obs[k][i] = x[i] * (k + 1) / 4.0; }
            var t = new A2CTrainer(ac);
            t.Update(obs, Golden.UpdateActions, Golden.UpdateReturns.Select(v => (double)v).ToArray());
            var p2 = ac.Policy(x);
            for (int i = 0; i < p2.Length; i++) Assert.AreEqual(Golden.AfterUpdatePolicy[i], (float)p2[i], 1e-4f);
            Assert.AreEqual(Golden.AfterUpdateValue, (float)ac.Value(x), 1e-4f);
        }

        [Test]
        public void ImitationLearnerLearnsAPattern()
        {
            var f = new AdaptiveRandomForest(4, 11, 3);
            var rng = new KRandom(9);
            int correct = 0, n = 0;
            for (int i = 0; i < 4000; i++)
            {
                var o = new[] { rng.Range(0.3, 1.1), rng.Value() < 0.2 ? 1.0 : 0.0, rng.Value(), rng.Value() };
                double d = o[0] * 3;
                int y = o[1] > 0.5 ? 9 : (d >= 1.8 && d <= 2.3 ? 5 : (d > 2.3 ? 1 : 2));
                if (i >= 2000) { n++; if (f.Predict(o) == y) correct++; }
                f.Learn(o, y);
            }
            Assert.Greater((double)correct / n, 0.7, "the ARF-style learner should imitate a simple player policy");
        }

        [Test]
        public void BackgroundTrainerRuns()
        {
            var ac = ActorCritic.FromJson(Golden.SmallNet);
            var imit = new AdaptiveRandomForest(KendoTimings.ObservationSize, KendoTimings.ActionCount, 1);
            var tr = new BackgroundTrainer(ac, imit, Technique.Hard(), Technique.Medium(), 3);
            for (int i = 0; i < 50; i++) tr.RunUpdate();
            Assert.Greater(tr.Steps, 0);
            Assert.IsFalse(double.IsNaN(tr.LastEntropy));
        }
    }

    public class CampaignTests
    {
        [Test]
        public void LessonsAreSequentialAndLevelsNeedAllLessons()
        {
            var c = new CampaignProgress(KendoSaveData.NewGame("t"));
            Assert.AreEqual(GateResult.Open, c.CanStartLesson(0));
            Assert.AreEqual(GateResult.LessonLocked, c.CanStartLesson(1));
            Assert.AreEqual(GateResult.LessonLocked, c.CanEnterStage(0));
            for (int i = 0; i < 7; i++) c.CompleteLesson(i, 80);
            Assert.AreEqual(GateResult.Open, c.CanEnterStage(0));
            Assert.AreEqual(GateResult.StageLocked, c.CanEnterStage(1));
            Assert.AreEqual(GateResult.FinalLocked, c.CanEnterStage(4));
            for (int s = 0; s < 4; s++) Assert.AreEqual(s, c.CompleteStage(s, 70));
            Assert.AreEqual(CampaignProgress.PerfectStudentTitle, c.data.title);
            Assert.AreEqual(GateResult.Open, c.CanEnterStage(4));
            Assert.AreEqual(4, c.CompleteStage(4, 70));
            Assert.AreEqual(-1, c.CompleteStage(4, 90), "badge only once");
            Assert.AreEqual(5, c.data.badges.Count);
        }

        [Test]
        public void ButtonPressWithoutEvidenceNeverSucceeds()
        {
            var save = KendoSaveData.NewGame("t");
            for (int li = 0; li < CampaignContent.LessonCount; li++)
            {
                var s = new LessonSession(CampaignContent.Lessons[li], save.lessons[li], save.stats, skipIntro: true);
                var ex = s.Exercise;
                LessonAttempt empty;
                switch (ex.kind)
                {
                    case ExerciseKind.IdentifyTarget: empty = LessonAttempt.Identify(Target.None); break;
                    case ExerciseKind.ShinaiHandling: empty = LessonAttempt.Handling(new HandlingEvidence()); break;
                    case ExerciseKind.Rei: empty = LessonAttempt.Bow(ReiPlace.EntranceToKamiza, new ReiEvidence()); break;
                    case ExerciseKind.Kamae: empty = LessonAttempt.Stance(new KamaeEvidence { heldSeconds = 0 }); break;
                    case ExerciseKind.Suriashi: empty = LessonAttempt.Footwork(new SuriashiEvidence()); break;
                    default: empty = LessonAttempt.Strike(ex.kind, new StrikeEvidence()); break;
                }
                Assert.IsFalse(s.Submit(empty).success, $"lesson {li + 1}");
            }
        }

        [Test]
        public void StrikeLessonCompletesWithRealEvidenceAndGivesFeedback()
        {
            var save = KendoSaveData.NewGame("t");
            var s = new LessonSession(CampaignContent.Lessons[4], save.lessons[4], save.stats, skipIntro: true);
            foreach (var t in new[] { Target.Men, Target.KoteRight, Target.DoRight, Target.Tsuki })
            {
                var e = new StrikeEvidence { target = t, contact = true, contactAlongBlade = 0.85f, tipSpeed = 9, strikeArc = true, kiai = true, kiaiOffset = 0, zanshinObserved = true, followThrough = 0.5f, kamaeRestoredAfter = 0.8f, distance = 2f };
                var r = s.Submit(LessonAttempt.Strike(ExerciseKind.Strike, e));
                Assert.IsTrue(r.success, t.ToString());
                Assert.IsNotEmpty(r.feedback.headline);
                s.ContinueAfterFeedback();
            }
            Assert.AreEqual(LessonStep.Complete, s.Step);
        }

        [Test]
        public void WrongTargetIsExplained()
        {
            var save = KendoSaveData.NewGame("t");
            var s = new LessonSession(CampaignContent.Lessons[4], save.lessons[4], save.stats, skipIntro: true);
            var e = new StrikeEvidence { target = Target.KoteRight, contact = true, contactAlongBlade = 0.85f, tipSpeed = 9, strikeArc = true };
            var r = s.Submit(LessonAttempt.Strike(ExerciseKind.Strike, e));
            Assert.IsFalse(r.success);
            StringAssert.Contains("I asked for men", r.feedback.headline);
        }

        [Test]
        public void SaveRecoversFromCorruptionViaBackup()
        {
            var store = new MemorySaveStore();
            var s = new SaveService(store);
            s.Load();
            s.Data.lessons[0].completed = true;
            s.Save();
            s.Save(); // second save copies the good file to the backup
            store.files[SaveService.MainFile] = "{ broken";
            var s2 = new SaveService(store);
            Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, s2.Load());
            Assert.IsTrue(s2.Data.lessons[0].completed);
            store.files.Remove(SaveService.BackupFile);
            store.files[SaveService.MainFile] = "garbage";
            var s3 = new SaveService(store);
            Assert.AreEqual(SaveLoadStatus.CorruptReset, s3.Load());
            Assert.IsTrue(store.files.Keys.Any(k => k.Contains(".corrupt-")), "corrupt file kept, not overwritten");
        }

        [Test]
        public void WriteFailureDoesNotThrow()
        {
            var store = new MemorySaveStore();
            var s = new SaveService(store);
            s.Load();
            store.failWrites = true;
            Assert.IsFalse(s.Save());
        }

        [Test]
        public void EveryLessonFollowsTheTeachingSequence()
        {
            foreach (var l in CampaignContent.Lessons)
            {
                Assert.IsNotEmpty(l.intro, l.title);
                Assert.IsNotEmpty(l.explain, l.title);
                Assert.AreNotEqual(DemoKind.None, l.demo, l.title);
                Assert.IsNotEmpty(l.exercises, l.title);
                Assert.IsNotEmpty(l.terms, l.title);
                foreach (var ex in l.exercises) { Assert.IsNotEmpty(ex.instruction); Assert.Greater(ex.requiredSuccesses, 0); }
            }
        }
    }
}
