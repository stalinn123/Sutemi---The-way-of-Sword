using System.Collections;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Campaign;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>
    /// Runs the seven lessons in the dojo: the master introduces and explains (skippable), demonstrates with the real
    /// motion system (the player watches), the player practises, the attempt is measured from the actual player state
    /// (shinai pose, footwork log, bow, strike sensor) and evaluated by the shared rules engine through
    /// <see cref="LessonSession"/>, and the master gives feedback built from that evaluation. Checkpoints are saved
    /// after every successful attempt; completed lessons can be revisited.
    /// </summary>
    public sealed class LessonDirector : MonoBehaviour
    {
        public DojoDirector dojo;
        public bool Running { get; private set; }
        public LessonSession Session { get; private set; }
        public int LessonIndex { get; private set; } = -1;

        private KendoGame game;
        private Guides guides;
        private LessonAttempt attempt;
        private bool wantDemo;
        private StrikeReport lastReport;
        private readonly KendoEvaluationProfile beginner = KendoEvaluationProfile.Beginner();

        private PlayerRig Rig => dojo.Rig;
        private Fighter Player => dojo.Rig.fighter;
        private Fighter Master => dojo.Master;
        private Fighter Motodachi => dojo.Motodachi;
        private BoguStandTarget Stand => dojo.Stand;

        private void Awake()
        {
            game = KendoGame.Instance;
            guides = new GameObject("LessonGuides").AddComponent<Guides>();
        }

        public void Begin(int lesson)
        {
            if (Running) return;
            if (game.Campaign.CanStartLesson(lesson) != GateResult.Open) { game.Ui.Toast("Complete the previous lessons first."); return; }
            StartCoroutine(Run(lesson));
        }

        private IEnumerator Run(int li)
        {
            Running = true;
            LessonIndex = li;
            var c = game.Campaign;
            var def = CampaignContent.Lessons[li];
            yield return Talk(MasterDialogue.BeforeLesson(li, c), def.title);
            c.BeginLesson(li);
            game.Save();
            Session = new LessonSession(def, c.data.lessons[li], c.data.stats);
            game.Ui.LessonHud.Show(true);
            float bestBefore = c.data.lessons[li].bestScore;

            while (Session.Step != LessonStep.Complete)
            {
                UpdateHud();
                switch (Session.Step)
                {
                    case LessonStep.Intro:
                    case LessonStep.Explain:
                    {
                        var lines = new List<DialogueLine>();
                        foreach (var l in Session.CurrentLines()) lines.Add(new DialogueLine(Speaker.Master, l));
                        if (Session.Step == LessonStep.Intro && def.terms.Length > 0)
                        {
                            var sb = new System.Text.StringBuilder("New words: ");
                            for (int i = 0; i < def.terms.Length; i++) sb.Append(i > 0 ? " · " : "").Append($"{def.terms[i].word} = {def.terms[i].meaning}");
                            lines.Add(new DialogueLine(Speaker.Master, sb.ToString()));
                        }
                        var step = Session.Step;
                        yield return Talk(lines, $"Lesson {li + 1} · {def.title}");
                        if (game.Ui.LastDialogueSkipped) Session.Skip();
                        else while (Session.Step == step) Session.Advance();
                        c.MarkLessonIntroSeen(li);
                        break;
                    }
                    case LessonStep.Demonstration:
                        yield return Demonstrate(def.demo, def);
                        if (Session.Step == LessonStep.Demonstration) while (Session.Step == LessonStep.Demonstration) Session.Advance();
                        break;
                    case LessonStep.Practice:
                        yield return Practice(Session.Exercise);
                        break;
                    case LessonStep.Feedback:
                        yield return Feedback();
                        break;
                }
            }

            // complete
            guides.Clear();
            float avg = Session.AverageScore;
            c.CompleteLesson(li, avg);
            game.Save();
            game.Audio.Stinger(game.Config.success);
            var after = MasterDialogue.AfterLesson(li, Session, c.data.stats);
            after.InsertRange(0, MasterDialogue.OnImprovement(bestBefore, avg));
            yield return Talk(after, $"Lesson {li + 1} complete");
            game.Ui.LessonHud.Show(false);
            dojo.ResetLessonActors();
            Running = false;
            LessonIndex = -1;
            game.Ui.SetObjective(c.NextObjective());
            if (c.AllLessonsCompleted && li == CampaignContent.LessonCount - 1)
                game.Ui.Toast("All seven lessons complete – the level board at the entrance is open.", 5f);
        }

        private IEnumerator Talk(List<DialogueLine> lines, string header)
        {
            bool done = false;
            game.Ui.ShowDialogue(lines, header, () => done = true);
            while (!done) yield return null;
        }

        private void UpdateHud()
        {
            var ex = Session.Exercise;
            string step = Session.Step == LessonStep.Practice || Session.Step == LessonStep.Feedback
                ? $"Exercise {Session.ExerciseIndex + 1}/{Session.def.exercises.Length}: {ex.title} · {Session.Successes}/{ex.requiredSuccesses} correct"
                : Session.Step.ToString();
            string instr = ex.instruction;
            if (ex.kind == ExerciseKind.Strike || ex.kind == ExerciseKind.IdentifyTarget || ex.kind == ExerciseKind.ContactVersusStrike)
                instr = $"<b>Target: {KendoEvaluationEngine.TargetName(Session.RequestedTarget).ToUpperInvariant()}</b>\n" + instr;
            if (ex.kind == ExerciseKind.Rei) instr = $"<b>Now: {ReiText(Session.RequestedReiPlace)}</b>\n" + instr;
            game.Ui.LessonHud.Set($"Lesson {Session.def.index + 1}: {Session.def.title}", step, instr, Session.Progress01,
                $"Attempts this exercise: {Session.AttemptsThisExercise}   ·   G: watch again   ·   Esc: pause");
            game.Ui.LessonHud.SetHint(game.Settings.showHints && Session.FailuresInARow >= 2 ? "Hint: " + ex.hint : "");
        }

        private static string ReiText(ReiPlace p) => p == ReiPlace.EntranceToKamiza ? "bow to the kamiza at the entrance (≈30°)" : p == ReiPlace.ToMaster ? "bow to the master (≈15°, eyes on him)" : "leave: bow to the kamiza at the entrance (≈30°)";

        // ------------------------------------------------------------------ demonstrations

        private IEnumerator Demonstrate(DemoKind kind, LessonDef def)
        {
            guides.Clear();
            Rig.InputEnabled = false;
            var m = Master;
            m.Frozen = false;
            m.sensor.targets.Clear();
            m.sensor.rules = KendoRuleConfig.TrainingBout();
            m.sensor.profile = beginner;
            string narration = def.demoNarration.Length > 0 ? def.demoNarration[0] : "";
            game.Ui.LessonHud.SetHint("Watch the master.  " + narration + "   (Tab: skip)");
            var tipMarker = new GameObject("TipMarker").transform;
            tipMarker.SetParent(m.rig.shinai, false);
            tipMarker.localPosition = Vector3.up * m.rig.length;
            guides.Trail(tipMarker);
            System.Action<StrikeReport> show = r => { if (r.target != null) game.Ui.LessonHud.SetHint($"Master's {KendoEvaluationEngine.TargetName(r.evidence.target)}: {(r.evaluation.yukoDatotsu ? "valid strike" : MatchDirector.ReasonText(r.evaluation))} ({r.evaluation.performanceScore:0})"); };
            m.sensor.Evaluated += show;
            skipDemo = false;
            switch (kind)
            {
                case DemoKind.PointTargets: yield return DemoTargets(); break;
                case DemoKind.Rei: yield return DemoRei(); break;
                case DemoKind.Kamae: yield return DemoKamae(); break;
                case DemoKind.Suriashi: yield return DemoSuriashi(); break;
                case DemoKind.Strikes: yield return DemoStrikes(new[] { DuelAction.Men, DuelAction.Kote, DuelAction.Do, DuelAction.Tsuki }, Stand, SceneLayout.DojoStandPractice); break;
                case DemoKind.KiKenTaiItchi: yield return DemoStrikes(new[] { DuelAction.Men, DuelAction.Men }, Motodachi, SceneLayout.DojoPractice + Vector3.forward * 0.35f, true); break;
                case DemoKind.Zanshin: yield return DemoStrikes(new[] { DuelAction.Men }, Motodachi, SceneLayout.DojoPractice + Vector3.forward * 0.35f, true, true); break;
                default: yield return Wait(1f); break;
            }
            m.sensor.Evaluated -= show;
            Time.timeScale = 1f;
            Destroy(tipMarker.gameObject);
            guides.Clear();
            dojo.SendMasterHome();
            Rig.InputEnabled = true;
            game.Ui.LessonHud.SetHint("");
        }

        private bool skipDemo;

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipDemo)
            {
                if (game.Input.Read().skip) { skipDemo = true; Session.Skip(); }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void PlaceMaster(Vector3 pos, Vector3 facing)
        {
            Master.footwork.Cancel();
            Master.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z)));
        }

        private IEnumerator DemoTargets()
        {
            var st = SceneLayout.DojoStandPractice;
            dojo.PlacePlayer(st + new Vector3(1.4f, 0f, 0.9f), Quaternion.LookRotation(SceneLayout.DojoStandA - (st + new Vector3(1.4f, 0f, 0.9f))).eulerAngles.y);
            PlaceMaster(st + new Vector3(-0.6f, 0f, 0.4f), SceneLayout.DojoStandA - st);
            Master.Motion.ForceSageto(true);
            Master.presentArm = 1f;
            foreach (var t in new[] { Target.Men, Target.KoteRight, Target.DoRight, Target.Tsuki })
            {
                guides.Clear();
                guides.HighlightZone(Stand.ZonePoint(t), t == Target.Men ? 0.13f : 0.08f, Rig.head.position - Stand.ZonePoint(t));
                game.Ui.LessonHud.SetHint($"This is {KendoEvaluationEngine.TargetName(t)}." + (t == Target.Tsuki ? "  (Tsuki is not allowed for beginners in a match.)" : ""));
                yield return Wait(1.8f);
                if (skipDemo) yield break;
            }
            Master.presentArm = 0f;
            Master.Motion.ForceSageto(false);
            // touch vs strike, evaluated live by the engine
            Master.sensor.targets.Add(Stand);
            PlaceMaster(st, SceneLayout.DojoStandA - st);
            Master.opponent = Stand;
            game.Ui.LessonHud.SetHint("A touch…");
            for (int i = 0; i < 14 && !skipDemo; i++) { Master.StepCommand(StepDir.Forward, 0.5f); yield return Wait(0.45f); }
            yield return Wait(1.0f);
            PlaceMaster(st, SceneLayout.DojoStandA - st);
            yield return Wait(0.6f);
            game.Ui.LessonHud.SetHint("…and a strike.");
            Time.timeScale = 0.6f;
            Master.StrikeCommand(DuelAction.Men);
            yield return Wait(2.2f);
            Time.timeScale = 1f;
        }

        private IEnumerator DemoRei()
        {
            dojo.PlacePlayer(new Vector3(1.6f, 0.45f, 3.0f), 210f);
            PlaceMaster(SceneLayout.DojoEntranceMark + Vector3.back * 1.2f, Vector3.back);
            Master.Motion.ForceSageto(true);
            yield return Wait(0.8f);
            for (float t = 0; t < 1.2f && !skipDemo; t += Time.deltaTime) { Master.bowDeg = Mathf.Lerp(0, 30, t / 1.2f); yield return null; }
            game.Ui.LessonHud.SetHint("To the kamiza: about 30°. Pause.");
            yield return Wait(1.2f);
            for (float t = 0; t < 1f && !skipDemo; t += Time.deltaTime) { Master.bowDeg = Mathf.Lerp(30, 0, t); yield return null; }
            Master.bowDeg = 0f;
            Master.FaceTowards(Rig.transform.position, 360f);
            yield return Wait(0.6f);
            game.Ui.LessonHud.SetHint("To a person: about 15°, eyes on the partner.");
            Master.opponent = Player;
            for (float t = 0; t < 0.8f && !skipDemo; t += Time.deltaTime) { Master.bowDeg = Mathf.Lerp(0, 15, t / 0.8f); yield return null; }
            yield return Wait(1.0f);
            for (float t = 0; t < 0.8f && !skipDemo; t += Time.deltaTime) { Master.bowDeg = Mathf.Lerp(15, 0, t / 0.8f); yield return null; }
            Master.bowDeg = 0f;
            Master.Motion.ForceSageto(false);
        }

        private IEnumerator DemoKamae()
        {
            var spot = SceneLayout.DojoPractice + Vector3.left * 1.2f;
            dojo.PlacePlayer(SceneLayout.DojoPractice + new Vector3(1.0f, 0f, 0.6f), 200f);
            PlaceMaster(spot, Motodachi.transform.position - spot);
            Master.opponent = Motodachi;
            Master.Motion.ForceSageto(true);
            yield return Wait(0.8f);
            guides.Footprints(spot, Master.transform.eulerAngles.y);
            game.Ui.LessonHud.SetHint("Right foot forward, left behind, heel raised.");
            yield return Wait(1.6f);
            Master.Motion.ForceSageto(false);
            guides.KensenRing(Motodachi.transform.position + Vector3.up * 1.45f + Motodachi.transform.forward * 0.12f, Master.transform.position - Motodachi.transform.position);
            game.Ui.LessonHud.SetHint("Both hands; left fist before the navel; kensen to the throat.");
            yield return Wait(2.4f);
        }

        private IEnumerator DemoSuriashi()
        {
            var start = SceneLayout.DojoSuriashiStart + Vector3.right * 1.5f;
            dojo.PlacePlayer(SceneLayout.DojoSuriashiStart, 160f);
            PlaceMaster(start, Vector3.back);
            Master.opponent = null;
            yield return Wait(0.6f);
            foreach (var d in new[] { StepDir.Forward, StepDir.Forward, StepDir.Back, StepDir.Back, StepDir.Left, StepDir.Right })
            {
                if (skipDemo) yield break;
                var next = Master.transform.position + Master.transform.TransformDirection(FootworkModel.Displacement(d, Master.footwork.scale));
                guides.FloorMark(next, 0.3f, true);
                Master.StepCommand(d);
                yield return Wait(0.75f);
            }
        }

        private IEnumerator DemoStrikes(DuelAction[] strikes, IStrikeTarget target, Vector3 spot, bool fumikomi = false, bool zanshin = false)
        {
            var tpos = target.Root.position;
            var toT = tpos - spot;
            dojo.PlacePlayer(spot + Quaternion.LookRotation(toT) * new Vector3(1.5f, 0f, 0.3f), Quaternion.LookRotation(tpos - (spot + Quaternion.LookRotation(toT) * new Vector3(1.5f, 0f, 0.3f))).eulerAngles.y);
            Master.sensor.targets.Clear();
            Master.sensor.targets.Add(target);
            Master.opponent = target;
            foreach (var a in strikes)
            {
                if (skipDemo) yield break;
                PlaceMaster(fumikomi ? spot + toT.normalized * -0.3f : spot, toT);
                yield return Wait(0.9f);
                Time.timeScale = 0.55f;
                if (fumikomi) Master.StepCommand(StepDir.Forward);
                yield return Wait(fumikomi ? 0.1f : 0f);
                Master.StrikeCommand(a); // the master's technique: kiai on impact, perfect hasuji, full zanshin
                yield return Wait(1.0f);
                Time.timeScale = 1f;
                if (zanshin)
                {
                    game.Ui.LessonHud.SetHint("Through… turn… kamae.");
                    for (int i = 0; i < 3 && !skipDemo; i++) { Master.StepCommand(StepDir.Forward); yield return Wait(0.4f); }
                    for (float t = 0; t < 0.6f; t += Time.deltaTime) { Master.FaceTowards(target.Root.position, 400f * Time.deltaTime); yield return null; }
                }
                yield return Wait(1.6f);
            }
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ practice

        private IEnumerator Practice(ExerciseDef ex)
        {
            attempt = null;
            wantDemo = false;
            guides.Clear();
            var p = Player;
            p.sensor.targets.Clear();
            p.sensor.rules = KendoRuleConfig.TrainingBout();
            p.sensor.profile = beginner;
            Rig.aimPointWorld = null;
            Rig.MovementEnabled = true;
            Rig.StrikesEnabled = true;
            game.Ui.LessonHud.ShowBowMeter(false);
            game.Ui.LessonHud.ShowTiming(false);
            game.Ui.LessonHud.SetChecks("");
            switch (ex.kind)
            {
                case ExerciseKind.IdentifyTarget: yield return ProbeIdentify(); break;
                case ExerciseKind.ShinaiHandling: yield return ProbeHandling(); break;
                case ExerciseKind.ContactVersusStrike: yield return ProbeStrike(ex, Stand, SceneLayout.DojoStandPractice, contactFirst: true); break;
                case ExerciseKind.Rei: yield return ProbeRei(); break;
                case ExerciseKind.Kamae: yield return ProbeKamae(); break;
                case ExerciseKind.Suriashi: yield return ProbeSuriashi(ex); break;
                case ExerciseKind.Strike: yield return ProbeStrike(ex, Stand, SceneLayout.DojoStandPractice, false); break;
                default: yield return ProbeStrike(ex, Motodachi, SceneLayout.DojoPractice, false); break;
            }
            if (wantDemo) { Session.ReplayDemonstration(); yield break; }
            if (attempt == null) yield break;
            var r = Session.Submit(attempt);
            if (r.success && Session.Exercise == ex)
            {
                game.Campaign.RecordLessonCheckpoint(Session.def.index, Session.ExerciseIndex, Session.Successes, r.evaluation != null ? r.evaluation.performanceScore : 0f);
                game.Save();
            }
            game.Audio.Stinger(r.success ? game.Config.success : game.Config.fail);
        }

        private bool CommonPracticeKeys()
        {
            var f = game.Input.Read();
            if (f.replayDemo && !game.Ui.BlocksGameplay) { wantDemo = true; return true; }
            return false;
        }

        private IEnumerator ProbeIdentify()
        {
            var toStand = SceneLayout.DojoStandA - SceneLayout.DojoStandPractice;
            toStand.y = 0f;
            dojo.PlacePlayer(SceneLayout.DojoStandPractice, Quaternion.LookRotation(toStand).eulerAngles.y);
            guides.FloorMark(SceneLayout.DojoStandA, 0.6f, true);
            Rig.MovementEnabled = true;   // the player may step closer; only strikes are off in this exercise
            Rig.StrikesEnabled = false;
            while (attempt == null)
            {
                if (CommonPracticeKeys()) yield break;
                var f = game.Input.Read();
                var ray = new Ray(Rig.head.position, Rig.head.forward);
                var looked = Stand.Gazed(ray);
                game.Ui.LessonHud.SetChecks(looked != Target.None ? $"Looking at: {KendoEvaluationEngine.TargetName(looked)}" : "Looking at: – (look at the bogu stand in front of you; move the mouse)");
                if (f.interact && !game.Ui.BlocksGameplay) attempt = LessonAttempt.Identify(looked);
                yield return null;
            }
        }

        private IEnumerator ProbeHandling()
        {
            var h = new HandlingEvidence();
            float hold = 0f;
            while (attempt == null)
            {
                if (CommonPracticeKeys()) yield break;
                var m = Player.Motion;
                if (m.InSageto && m.PhaseTime > 0.35f)
                {
                    h.reachedSageto = true;
                    var local = Rig.transform.InverseTransformPoint(Player.rig.Grip);
                    h.tipBelowGripM = Player.rig.Grip.y - Player.rig.Tip.y;
                    h.onLeftSide = local.x < -0.05f;
                    bool still = !Player.footwork.Stepping && game.Input.Read().look.sqrMagnitude < 4f;
                    hold = still ? hold + Time.deltaTime : 0f;
                    h.heldSeconds = Mathf.Max(h.heldSeconds, hold);
                }
                if (h.reachedSageto && m.InChudan && m.PhaseTime > 0.35f)
                {
                    h.returnedToChudan = true;
                    h.bothHandsInChudan = Player.rig.RightHandOn;
                    attempt = LessonAttempt.Handling(h);
                }
                game.Ui.LessonHud.SetChecks($"Sageto reached: {(h.reachedSageto ? "yes" : "no")}\nHeld still: {h.heldSeconds:0.0} s\nBack to chudan: {(h.returnedToChudan ? "yes" : "no")}");
                yield return null;
            }
        }

        private IEnumerator ProbeStrike(ExerciseDef ex, IStrikeTarget target, Vector3 spot, bool contactFirst)
        {
            var toT = target.Root.position - spot;
            dojo.PlacePlayer(spot, Quaternion.LookRotation(new Vector3(toT.x, 0, toT.z)).eulerAngles.y);
            if (Player.Motion.InSageto) Player.ToggleKamae();
            Player.opponent = target;
            Player.sensor.targets.Add(target);
            guides.FloorMark(spot, 0.5f, true);
            var want = Session.RequestedTarget;
            Vector3 zone = target.Root.position + Vector3.up * 1.5f;
            foreach (var z in target.Zones) if (z.target == want) { zone = z.Center; break; }
            guides.HighlightZone(zone, want == Target.Men ? 0.13f : 0.08f, Rig.head.position - zone);
            Rig.aimPointWorld = a =>
            {
                var t = KendoTimings.TargetOf(a);
                foreach (var z in target.Zones) if (z.target == t) return z.Center;
                return (Vector3?)null;
            };
            bool kkti = ex.kind == ExerciseKind.KiKenTaiItchi || ex.kind == ExerciseKind.Zanshin;
            StrikeReport got = null;
            System.Action<StrikeReport> onEval = r => got = r;
            System.Action<StrikeReport> onMiss = r => got = r;
            Player.sensor.Evaluated += onEval;
            Player.sensor.Missed += onMiss;
            Player.sensor.Contact += OnContactSound;
            if (contactFirst && !Session.ContactSeen) game.Ui.LessonHud.SetHint("First: a touch. Hold the right mouse button, raise the tip and walk in slowly with W.");
            while (got == null)
            {
                if (CommonPracticeKeys()) break;
                if (Player.sensor.Pending) game.Ui.LessonHud.SetChecks("Zanshin… keep going, turn back, kamae.");
                yield return null;
            }
            Player.sensor.Evaluated -= onEval;
            Player.sensor.Missed -= onMiss;
            Player.sensor.Contact -= OnContactSound;
            if (got == null) yield break;
            lastReport = got;
            var e = got.evidence;
            if (kkti) game.Ui.LessonHud.ShowTiming(true, e.kiai ? e.kiaiOffset : float.NaN, e.fumikomi ? e.fumikomiOffset : float.NaN, beginner.kktiWindow);
            game.Ui.LessonHud.SetChecks(LessonHud.ChecklistFromEvaluation(got.evaluation));
            guides.Flash(got.contactPoint == Vector3.zero ? zone : got.contactPoint, got.evaluation.yukoDatotsu);
            attempt = LessonAttempt.Strike(ex.kind == ExerciseKind.ContactVersusStrike ? ExerciseKind.ContactVersusStrike : ex.kind, e);
            yield return new WaitForSeconds(0.4f);
        }

        private void OnContactSound(StrikeReport r)
        {
            var c = game.Config;
            game.Audio.Sfx(r.evaluation.invalidReason == InvalidReason.ContactOnly ? c.shinaiTouch : c.shinaiHit, r.contactPoint);
        }

        private IEnumerator ProbeRei()
        {
            var want = Session.RequestedReiPlace;
            Vector3 mark = want == ReiPlace.ToMaster ? SceneLayout.DojoReiToMasterMark : SceneLayout.DojoEntranceMark;
            guides.FloorMark(mark, 0.7f, false);
            Master.opponent = Player;
            Master.FaceTowards(Rig.transform.position, 360f);
            bool finished = false;
            float depth = 0f, hold = 0f, minGaze = 180f;
            bool moved = false;
            System.Action<float, float> onBow = (d, h) => { finished = true; depth = d; hold = h; };
            Rig.BowFinished += onBow;
            int stepsAtStart = 0;
            while (!finished)
            {
                if (CommonPracticeKeys()) { Rig.BowFinished -= onBow; yield break; }
                bool atMark = Vector3.Distance(Flat(Rig.transform.position), Flat(mark)) < 0.7f;
                Master.FaceTowards(Rig.transform.position, 90f * Time.deltaTime);
                float target = want == ReiPlace.ToMaster ? 15f : 30f;
                game.Ui.LessonHud.ShowBowMeter(Rig.bowAngle > 0.5f || Rig.Bow != PlayerRig.BowState.Upright, Rig.bowAngle, target);
                if (Rig.Bow == PlayerRig.BowState.Upright) { stepsAtStart = Player.footwork.log.Count; minGaze = 180f; moved = false; }
                else
                {
                    if (Player.footwork.log.Count != stepsAtStart || Player.footwork.Stepping) moved = true;
                    var face = Master.headPoint.position;
                    minGaze = Mathf.Min(minGaze, Vector3.Angle(Rig.head.forward, face - Rig.head.position));
                }
                // master's response bow when the student bows to him
                Master.bowDeg = want == ReiPlace.ToMaster && Rig.bowAngle > 5f && atMark ? Mathf.MoveTowards(Master.bowDeg, 15f, 30f * Time.deltaTime) : Mathf.MoveTowards(Master.bowDeg, 0f, 30f * Time.deltaTime);
                game.Ui.LessonHud.SetChecks($"At the mark: {(atMark ? "yes" : "no – walk to the gold circle")}\nShinai in sageto: {(Player.Motion.InSageto ? "yes" : "no – press X")}\nBow: {Rig.bowAngle:0}°");
                yield return null;
            }
            Rig.BowFinished -= onBow;
            Master.bowDeg = 0f;
            game.Ui.LessonHud.ShowBowMeter(false);
            Vector3 facingWanted = want == ReiPlace.ToMaster ? (Master.transform.position - Rig.transform.position) : Vector3.back; // the kamiza is at −z
            float facingErr = Vector3.Angle(Flat(Rig.transform.forward), Flat(facingWanted));
            ReiPlace place;
            float dEntrance = Vector3.Distance(Flat(Rig.transform.position), Flat(SceneLayout.DojoEntranceMark));
            float dMaster = Vector3.Distance(Flat(Rig.transform.position), Flat(SceneLayout.DojoReiToMasterMark));
            if (dMaster < 0.8f) place = ReiPlace.ToMaster;
            else if (dEntrance < 0.8f) place = want == ReiPlace.LeavingToKamiza ? ReiPlace.LeavingToKamiza : ReiPlace.EntranceToKamiza;
            else place = want == ReiPlace.ToMaster ? ReiPlace.EntranceToKamiza : ReiPlace.ToMaster; // wrong place
            var rei = new ReiEvidence
            {
                bowAngleDeg = depth,
                holdSeconds = hold,
                facingErrorDeg = facingErr,
                shinaiAtSide = Player.Motion.InSageto,
                gazeOnPartner = minGaze < 22f,
                feetStill = !moved,
            };
            attempt = LessonAttempt.Bow(place, rei);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private IEnumerator ProbeKamae()
        {
            var spot = SceneLayout.DojoPractice;
            var toM = Motodachi.transform.position - spot;
            float yaw = Quaternion.LookRotation(Flat(toM)).eulerAngles.y;
            dojo.PlacePlayer(spot + Flat(toM).normalized * -0.6f, yaw); // a step behind the footprints: the player walks onto them
            Player.opponent = Motodachi;
            guides.Footprints(spot, yaw);
            var throat = Motodachi.transform.position + Vector3.up * 1.45f * Motodachi.height / 1.75f + Motodachi.transform.forward * 0.12f;
            foreach (var z in Motodachi.Zones) if (z.target == Target.Tsuki) { throat = z.Center; break; }
            guides.KensenRing(throat, Rig.head.position - throat);
            float held = 0f;
            while (attempt == null)
            {
                if (CommonPracticeKeys()) yield break;
                var f = game.Input.Read();
                var k = MeasureKamae(spot, yaw, throat, held);
                var live = KendoEvaluationEngine.EvaluateKamae(k, beginner);
                bool lineOk = live.Get(Criterion.KensenLine).Value.passed;
                guides.RingState(lineOk);
                bool steady = lineOk && !Player.footwork.Stepping && f.look.sqrMagnitude < 9f && Player.Motion.InChudan;
                held = steady ? held + Time.deltaTime : 0f;
                k.heldSeconds = held;
                live = KendoEvaluationEngine.EvaluateKamae(k, beginner);
                game.Ui.LessonHud.SetChecks(LessonHud.ChecklistFromEvaluation(live) + "\nF: present your kamae");
                if (live.passed || (f.interact && !game.Ui.BlocksGameplay)) attempt = LessonAttempt.Stance(k);
                yield return null;
            }
        }

        private KamaeEvidence MeasureKamae(Vector3 spot, float yaw, Vector3 throat, float held)
        {
            var sr = Player.rig;
            var k = new KamaeEvidence();
            k.kamae = Player.Motion.InSageto ? Kamae.Sageto : Kamae.Chudan;
            k.bothHands = sr.RightHandOn;
            // distance from the throat to the kensen line
            var g = sr.Grip;
            var dir = sr.BladeDir;
            var toT = throat - g;
            k.kensenOffset = Vector3.Cross(dir, toT).magnitude;
            var flatDir = Flat(dir);
            var flatTo = Flat(toT);
            k.kensenLateralDeg = flatDir.sqrMagnitude > 1e-6f && flatTo.sqrMagnitude > 1e-6f ? Vector3.Angle(flatDir, flatTo) : 0f;
            var lh = Rig.transform.InverseTransformPoint(sr.LeftHandPoint);
            k.leftHandForward = lh.z - 0.12f;
            k.leftHandHeightError = lh.y - 1.02f;
            k.torsoTiltDeg = Mathf.Max(0f, Mathf.Abs(Rig.pitch) - 12f) * 0.8f;
            float posErr = Vector3.Distance(Flat(Rig.transform.position), Flat(spot));
            float yawErr = Mathf.Abs(Mathf.DeltaAngle(Rig.yaw, yaw));
            bool feetSet = posErr < 0.16f && yawErr < 15f && !Player.footwork.Stepping && !Player.footwork.Settling;
            bool lastDrawn = Player.footwork.log.Count == 0 || Player.footwork.log[Player.footwork.log.Count - 1].okuriAshi;
            Player.footwork.Feet(out var lf, out var rf);
            k.stanceLength = feetSet && lastDrawn ? rf.z - lf.z : (feetSet ? 0.45f : 0f);
            k.stanceWidth = Mathf.Abs(rf.x - lf.x);
            k.heldSeconds = held;
            return k;
        }

        private IEnumerator ProbeSuriashi(ExerciseDef ex)
        {
            var start = SceneLayout.DojoSuriashiStart;
            const float yaw = 180f;
            dojo.PlacePlayer(start, yaw);
            if (Player.Motion.InSageto) Player.ToggleKamae();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var pos = start;
            var expected = new List<Vector3>();
            foreach (var mv in ex.moves)
            {
                var d = mv == SuriashiMove.Forward ? StepDir.Forward : mv == SuriashiMove.Back ? StepDir.Back : mv == SuriashiMove.Left ? StepDir.Left : StepDir.Right;
                pos += rot * FootworkModel.Displacement(d, Player.footwork.scale);
                expected.Add(pos);
            }
            guides.FloorMark(start, 0.35f, false);
            for (int i = 0; i < expected.Count; i++) guides.FloorMark(expected[i], 0.22f, i < expected.Count - 1);
            Player.footwork.ClearLog();
            float maxPitch = 0f, framesMoving = 0f, framesKept = 0f, idle = 0f;
            int stepsWanted = ex.moves.Length;
            while (attempt == null)
            {
                if (CommonPracticeKeys()) yield break;
                var f = game.Input.Read();
                int steps = Player.footwork.log.Count;
                if (Player.footwork.Stepping || Player.footwork.Settling)
                {
                    framesMoving += 1f;
                    if (Player.Motion.InChudan && Player.Motion.aim.magnitude < 6f) framesKept += 1f;
                    maxPitch = Mathf.Max(maxPitch, Mathf.Abs(Rig.pitch));
                    idle = 0f;
                }
                else idle += Time.deltaTime;
                game.Ui.LessonHud.SetChecks($"Steps: {steps}/{stepsWanted}\nGaze: {Mathf.Abs(Rig.pitch):0}° (keep under 20°)\nF: finish");
                if ((steps >= stepsWanted && idle > 0.8f) || (f.interact && steps > 0 && !game.Ui.BlocksGameplay))
                {
                    var log = Player.footwork.log;
                    var s = new SuriashiEvidence { steps = log.Count };
                    int wrong = 0;
                    for (int i = 0; i < log.Count; i++)
                    {
                        if (log[i].okuriAshi) s.stepsWithOkuriAshi++;
                        if (log[i].crossed) s.crossedFeet++;
                        if (i < ex.moves.Length && !Matches(log[i].dir, ex.moves[i])) wrong++;
                    }
                    s.maxGazePitchDeg = maxPitch;
                    s.kamaeKeptFraction = framesMoving > 0 ? framesKept / framesMoving : 0f;
                    s.markerError = Vector3.Distance(Flat(Rig.transform.position), Flat(expected[expected.Count - 1]));
                    s.extraSteps = Mathf.Max(0, log.Count - stepsWanted) + wrong;
                    attempt = LessonAttempt.Footwork(s);
                }
                yield return null;
            }
        }

        private static bool Matches(StepDir d, SuriashiMove m) =>
            (m == SuriashiMove.Forward && d == StepDir.Forward) || (m == SuriashiMove.Back && d == StepDir.Back) || (m == SuriashiMove.Left && d == StepDir.Left) || (m == SuriashiMove.Right && d == StepDir.Right);

        // ------------------------------------------------------------------ feedback

        private IEnumerator Feedback()
        {
            var r = Session.LastResult;
            var fb = r.feedback;
            var extra = MasterDialogue.OnRepeatedFailure(fb, Session.FailuresInARow);
            if (extra.Count > 0 && Session.FailuresInARow == 3) yield return Talk(extra, "Sensei");
            bool done = false, demo = false;
            var ex = Session.Exercise;
            string progress = $"{Session.Successes}/{ex.requiredSuccesses} correct · attempt {Session.AttemptsThisExercise}";
            game.Ui.Menus.ShowFeedback(fb, r.evaluation != null ? r.evaluation.performanceScore : 0f, progress, () => done = true, () => { done = true; demo = true; });
            while (!done) yield return null;
            game.Ui.LessonHud.ShowTiming(false);
            if (demo) Session.ReplayDemonstration();
            else Session.ContinueAfterFeedback();
        }

        public void Abort()
        {
            if (!Running) return;
            StopAllCoroutines();
            Time.timeScale = 1f;
            guides.Clear();
            game.Ui.LessonHud.Show(false);
            game.Ui.Menus.CloseFeedback();
            Running = false;
            LessonIndex = -1;
            dojo.ResetLessonActors();
            Rig.InputEnabled = true;
        }
    }
}
