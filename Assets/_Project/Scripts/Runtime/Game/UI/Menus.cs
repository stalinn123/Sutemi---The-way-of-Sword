using System;
using System.Collections.Generic;
using Kendo.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace Kendo.Game
{
    /// <summary>Main menu, pause, settings, controls, badges, credits, confirmation, lesson board, level board, results and lesson feedback.</summary>
    public sealed class Menus
    {
        private readonly KendoUiRoot root;
        private readonly KendoGame game;
        private GameObject main, pause, settings, controls, badges, credits, confirm, lessonBoard, levelBoard, result, feedback;
        private Text confirmText, badgesText, resultTitle, resultBody, feedbackTitle, feedbackBody, lessonBoardText, levelBoardText;
        private Button continueButton, confirmYes, resultPrimary, resultSecondary, feedbackContinue, feedbackDemo;
        private Action confirmAction, resultPrimaryAction, resultSecondaryAction, feedbackContinueAction, feedbackDemoAction;
        private readonly List<Button> lessonButtons = new List<Button>();
        private readonly List<Button> levelButtons = new List<Button>();
        public Action<int> LessonChosen;
        public Action<int> StageChosen;

        public Menus(KendoUiRoot root, KendoGame game)
        {
            this.root = root;
            this.game = game;
        }

        private GameObject Screen(string name, Vector2 min, Vector2 max, string heading)
        {
            var dim = KendoUi.Image(root.MenuLayer, name, new Color(0, 0, 0, 0.55f));
            var p = KendoUi.Panel(dim.transform, "Panel", min, max);
            KendoUi.Heading(KendoUi.Rect(p.transform, "Heading", new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f), Vector2.zero, Vector2.zero), "H", heading, 46);
            KendoUi.Heading(KendoUi.Rect(p.transform, "Brand", new Vector2(0.7f, 0.88f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero), "B", "SUTEMI", 30, TextAnchor.MiddleRight);
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private static Transform Body(GameObject screen) => screen.transform.Find("Panel");

        public void Back()
        {
            if (root.menuStack.Count == 0) return;
            var top = root.menuStack[root.menuStack.Count - 1];
            if (top == main || top == result) return; // the main menu and result screens are closed by their buttons
            if (top == feedback) { feedbackContinueAction?.Invoke(); return; }
            root.PopMenu(top);
            if (top == pause) game.Paused = false;
        }

        // ------------------------------------------------------------------ main menu

        public void OpenMain()
        {
            if (main == null)
            {
                main = KendoUi.Fill(root.MenuLayer, "MainMenu").gameObject;
                var bg = KendoUi.Image(main.transform, "Shade", new Color(0, 0, 0, 0.25f));
                bg.raycastTarget = false;
                var title = KendoUi.Rect(main.transform, "Title", new Vector2(0.05f, 0.7f), new Vector2(0.6f, 0.92f), Vector2.zero, Vector2.zero);
                KendoUi.Heading(title, "T1", "Kendo", 110);
                var sub = KendoUi.Rect(main.transform, "Sub", new Vector2(0.05f, 0.62f), new Vector2(0.6f, 0.72f), Vector2.zero, Vector2.zero);
                KendoUi.Heading(sub, "T2", "The Master's Legacy", 52);
                var col = KendoUi.Rect(main.transform, "Buttons", new Vector2(0.06f, 0.08f), new Vector2(0.3f, 0.58f), Vector2.zero, Vector2.zero);
                string[] labels = { "Continue", "New Game", "Badges", "Settings", "Controls", "Credits", "Quit" };
                Action[] acts = { () => game.Flow.Continue(), NewGameClicked, OpenBadges, OpenSettings, OpenControls, OpenCredits, () => game.Quit() };
                for (int i = 0; i < labels.Length; i++)
                {
                    var a = acts[i];
                    var b = KendoUi.PillButton(KendoUi.Row(col, labels[i], i, labels.Length, 1f, 0f, 0.2f), "B", labels[i], () => a());
                    if (i == 0) continueButton = b;
                }
            }
            continueButton.interactable = game.Saves.HasSave && game.Campaign.data.story.introSeen;
            root.PushMenu(main);
        }

        public void CloseMain() { if (main != null) root.PopMenu(main); }

        private void NewGameClicked()
        {
            if (game.Saves.HasSave && game.Campaign.data.story.introSeen)
                Confirm("Start a new campaign? Your current progress, badges and the opponents' knowledge of you will be erased.", StartNewGame);
            else StartNewGame();
        }

        private void StartNewGame()
        {
            game.NewGame();
            game.Flow.Go(KendoSceneNames.TrainingDojo);
        }

        // ------------------------------------------------------------------ pause

        public void OpenPause()
        {
            string scene = game.Flow.CurrentScene;
            if (scene == KendoSceneNames.Boot || scene == KendoSceneNames.MainMenu) return;
            if (pause == null)
            {
                pause = Screen("Pause", new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.85f), "Paused");
                var col = KendoUi.Rect(Body(pause), "Buttons", new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.82f), Vector2.zero, Vector2.zero);
                string[] labels = { "Resume", "Controls", "Settings", "Badges", "Return to the dojo", "Main menu", "Quit" };
                Action[] acts =
                {
                    () => { root.PopMenu(pause); game.Paused = false; }, OpenControls, OpenSettings, OpenBadges,
                    () => { root.CloseAllMenus(); game.Flow.Go(KendoSceneNames.TrainingDojo); },
                    () => { root.CloseAllMenus(); game.Save(); game.Flow.Go(KendoSceneNames.MainMenu); },
                    () => game.Quit(),
                };
                for (int i = 0; i < labels.Length; i++)
                {
                    var a = acts[i];
                    KendoUi.PillButton(KendoUi.Row(col, labels[i], i, labels.Length, 1f, 0f, 0.2f), "B", labels[i], () => a(), 28);
                }
            }
            var ret = Body(pause).Find("Buttons/Return to the dojo");
            if (ret != null) ret.gameObject.SetActive(scene != KendoSceneNames.TrainingDojo);
            game.Paused = true;
            root.PushMenu(pause);
        }

        // ------------------------------------------------------------------ settings

        public void OpenSettings()
        {
            if (settings != null) { KendoUi.Destroy(settings); settings = null; }
            settings = Screen("Settings", new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.94f), "Settings");
            var s = game.Settings;
            var body = KendoUi.Rect(Body(settings), "Rows", new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero);
            var rows = new List<(string, Action<Transform>)>
            {
                ("Master volume", t => KendoUi.Slider(t, "S", 0, 1, s.masterVolume, v => { s.masterVolume = v; game.Audio.Apply(s); })),
                ("Music volume", t => KendoUi.Slider(t, "S", 0, 1, s.musicVolume, v => { s.musicVolume = v; game.Audio.Apply(s); })),
                ("Effects volume", t => KendoUi.Slider(t, "S", 0, 1, s.sfxVolume, v => { s.sfxVolume = v; game.Audio.Apply(s); })),
                ("Voice volume", t => KendoUi.Slider(t, "S", 0, 1, s.voiceVolume, v => s.voiceVolume = v)),
                ("Mouse sensitivity", t => KendoUi.Slider(t, "S", 0.2f, 3f, s.mouseSensitivity, v => s.mouseSensitivity = v)),
                ("Invert mouse Y", t => KendoUi.Toggle(t, "T", s.invertY, v => s.invertY = v)),
                ("Footwork pace", t => KendoUi.Slider(t, "S", 0.75f, 1.25f, s.footworkPace, v => s.footworkPace = v)),
                ("Smooth footwork (instead of steps)", t => KendoUi.Toggle(t, "T", s.smoothFootwork, v => s.smoothFootwork = v)),
                ("Field of view", t => KendoUi.Slider(t, "S", 55, 95, s.fov, v => s.fov = v)),
                ("Small furikaburi always", t => KendoUi.Toggle(t, "T", s.lowFurikaburi, v => s.lowFurikaburi = v)),
                ("Text speed", t => KendoUi.Slider(t, "S", 0.5f, 2f, s.textSpeed, v => s.textSpeed = v)),
                ("Hints", t => KendoUi.Toggle(t, "T", s.showHints, v => s.showHints = v)),
                ("Fullscreen", t => KendoUi.Toggle(t, "T", s.fullscreen, v => { s.fullscreen = v; game.ApplyDisplaySettings(); })),
                ("Graphics quality", t => KendoUi.Slider(t, "S", 0, Mathf.Max(0, QualitySettings.names.Length - 1), Mathf.Max(0, s.quality < 0 ? QualitySettings.GetQualityLevel() : s.quality),
                    v => { s.quality = Mathf.RoundToInt(v); game.ApplyDisplaySettings(); })),
            };
            for (int i = 0; i < rows.Count; i++)
            {
                var r = KendoUi.Row(body, rows[i].Item1, i, rows.Count, 1f, 0f, 0.15f);
                KendoUi.Label(KendoUi.Rect(r, "L", new Vector2(0, 0), new Vector2(0.55f, 1), Vector2.zero, Vector2.zero), "Text", rows[i].Item1, 24, KendoUi.Sand);
                rows[i].Item2(KendoUi.Rect(r, "C", new Vector2(0.58f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero));
            }
            var close = KendoUi.Rect(Body(settings), "Close", new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.1f), Vector2.zero, Vector2.zero);
            KendoUi.PillButton(close, "B", "Save & close", () =>
            {
                s.Clamp();
                game.Audio.Apply(s);
                game.Save();
                root.PopMenu(settings);
            }, 28);
            root.PushMenu(settings);
        }

        // ------------------------------------------------------------------ controls / credits / badges

        public void OpenControls()
        {
            if (controls == null)
            {
                controls = Screen("Controls", new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.92f), "Controls");
                var t = KendoUi.Label(KendoUi.Rect(Body(controls), "Text", new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.84f), Vector2.zero, Vector2.zero), "T",
                    KendoUiRoot.ControlsText(false) + "\n\n<b>VR (OpenXR, untested)</b>\nGrips: hold the shinai with both hands · swing to strike\nLeft stick: suriashi · Right stick: snap turn\nA: kiai · B: interact · X: kamae · Y: bow · Menu: pause",
                    26, KendoUi.Sand, TextAnchor.UpperLeft);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                KendoUi.PillButton(KendoUi.Rect(Body(controls), "Close", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.11f), Vector2.zero, Vector2.zero), "B", "Close", () => root.PopMenu(controls), 28);
            }
            root.PushMenu(controls);
        }

        public void OpenCredits()
        {
            if (credits == null)
            {
                credits = Screen("Credits", new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.92f), "Credits");
                KendoUi.Label(KendoUi.Rect(Body(credits), "Text", new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.84f), Vector2.zero, Vector2.zero), "T", CreditsText, 26, KendoUi.Sand, TextAnchor.UpperCenter);
                KendoUi.PillButton(KendoUi.Rect(Body(credits), "Close", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.11f), Vector2.zero, Vector2.zero), "B", "Close", () => root.PopMenu(credits), 28);
            }
            root.PushMenu(credits);
        }

        public const string CreditsText =
            "<b>KENDO: THE MASTER'S LEGACY</b>\n\n" +
            "Game design document, character, shinai, men, badges and environments: supplied project assets\n" +
            "(training dojo, Horyu-ji temple replica, final environment and shiai-jo)\n\n" +
            "Rules: FIK Regulations of Kendo Shiai and Shinpan (interpreted; thresholds are design values)\n" +
            "Opponent AI: after Fuchs, Gieseke & Dockhorn (2024), \"Personalized Dynamic Difficulty Adjustment –\n" +
            "Imitation Learning Meets Reinforcement Learning\" – rule-based agent, adaptive random forest imitation, A2C\n\n" +
            "Engine: Unity 6.3 · Music and sound: procedurally generated for this project\n\n" +
            "Thank you for training.";

        public void OpenBadges()
        {
            if (badges == null)
            {
                badges = Screen("Badges", new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f), "Badges");
                badgesText = KendoUi.Label(KendoUi.Rect(Body(badges), "Text", new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.84f), Vector2.zero, Vector2.zero), "T", "", 28, KendoUi.Sand, TextAnchor.UpperLeft);
                KendoUi.PillButton(KendoUi.Rect(Body(badges), "Close", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.11f), Vector2.zero, Vector2.zero), "B", "Close", () => root.PopMenu(badges), 28);
            }
            var sb = new System.Text.StringBuilder();
            var c = game.Campaign;
            if (!string.IsNullOrEmpty(c.data.title)) sb.AppendLine($"Title: <b>{c.data.title}</b>\n");
            foreach (var b in CampaignContent.Badges)
            {
                bool have = c.HasBadge(b.index);
                sb.AppendLine(have ? KendoUi.Colored($"[earned] {b.name}", KendoUi.Sand) : KendoUi.Colored($"[    ] {b.name}  (not yet earned)", KendoUi.SandDim));
                sb.AppendLine("     " + b.description + "\n");
            }
            sb.AppendLine($"Lessons completed: {c.LessonsCompleted}/7");
            badgesText.text = sb.ToString();
            root.PushMenu(badges);
        }

        // ------------------------------------------------------------------ confirm

        public void Confirm(string question, Action yes)
        {
            if (confirm == null)
            {
                confirm = Screen("Confirm", new Vector2(0.3f, 0.32f), new Vector2(0.7f, 0.68f), "Are you sure?");
                confirmText = KendoUi.Label(KendoUi.Rect(Body(confirm), "Text", new Vector2(0.07f, 0.35f), new Vector2(0.93f, 0.82f), Vector2.zero, Vector2.zero), "T", "", 28, KendoUi.Sand, TextAnchor.MiddleCenter);
                confirmYes = KendoUi.PillButton(KendoUi.Rect(Body(confirm), "Yes", new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.26f), Vector2.zero, Vector2.zero), "B", "Yes", () => { root.PopMenu(confirm); confirmAction?.Invoke(); }, 28);
                KendoUi.PillButton(KendoUi.Rect(Body(confirm), "No", new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.26f), Vector2.zero, Vector2.zero), "B", "No", () => root.PopMenu(confirm), 28);
            }
            confirmText.text = question;
            confirmAction = yes;
            root.PushMenu(confirm);
        }

        // ------------------------------------------------------------------ lesson board

        public void OpenLessonBoard()
        {
            if (lessonBoard == null)
            {
                lessonBoard = Screen("LessonBoard", new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.94f), "Lessons");
                var col = KendoUi.Rect(Body(lessonBoard), "List", new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero);
                for (int i = 0; i < CampaignContent.LessonCount; i++)
                {
                    int idx = i;
                    var row = KendoUi.Row(col, "Lesson" + i, i, CampaignContent.LessonCount, 1f, 0f, 0.18f);
                    var b = KendoUi.PillButton(row, "B", "", () => { root.PopMenu(lessonBoard); LessonChosen?.Invoke(idx); }, 26);
                    lessonButtons.Add(b);
                }
                KendoUi.PillButton(KendoUi.Rect(Body(lessonBoard), "Close", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.11f), Vector2.zero, Vector2.zero), "B", "Close", () => root.PopMenu(lessonBoard), 28);
            }
            var c = game.Campaign;
            for (int i = 0; i < lessonButtons.Count; i++)
            {
                var l = CampaignContent.Lessons[i];
                var lp = c.data.lessons[i];
                bool open = c.CanStartLesson(i) == GateResult.Open;
                string state = lp.completed ? $"done - revisit · best {lp.bestScore:0}" : (open ? (lp.exercise > 0 || lp.successes > 0 ? "resume" : "start") : "locked");
                KendoUi.SetLabel(lessonButtons[i], $"{i + 1}. {l.title} — {state}");
                lessonButtons[i].interactable = open;
            }
            root.PushMenu(lessonBoard);
        }

        // ------------------------------------------------------------------ level board

        public void OpenLevelBoard()
        {
            if (levelBoard == null)
            {
                levelBoard = Screen("LevelBoard", new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), "Leave the dojo");
                var col = KendoUi.Rect(Body(levelBoard), "List", new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero);
                for (int i = 0; i < CampaignContent.StageCount; i++)
                {
                    int idx = i;
                    var row = KendoUi.Row(col, "Stage" + i, i, CampaignContent.StageCount, 1f, 0f, 0.2f);
                    levelButtons.Add(KendoUi.PillButton(row, "B", "", () => { root.PopMenu(levelBoard); StageChosen?.Invoke(idx); }, 26));
                }
                levelBoardText = KendoUi.Label(KendoUi.Rect(Body(levelBoard), "Info", new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.21f), Vector2.zero, Vector2.zero), "T", "", 22, KendoUi.SandDim, TextAnchor.MiddleCenter);
                KendoUi.PillButton(KendoUi.Rect(Body(levelBoard), "Close", new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.1f), Vector2.zero, Vector2.zero), "B", "Stay", () => root.PopMenu(levelBoard), 28);
            }
            var c = game.Campaign;
            string info = "";
            for (int i = 0; i < levelButtons.Count; i++)
            {
                var st = CampaignContent.Stages[i];
                var sp = c.data.stages[i];
                bool open = c.CanEnterStage(i) == GateResult.Open;
                string state = sp.completed ? $"done - best {sp.bestScore:0}" : (open ? "open" : "locked");
                KendoUi.SetLabel(levelButtons[i], $"{st.title} — {state}");
                levelButtons[i].interactable = open;
                if (!open && info == "") info = c.LockReason(i);
            }
            levelBoardText.text = info;
            root.PushMenu(levelBoard);
        }

        // ------------------------------------------------------------------ results / feedback

        public void ShowResult(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            if (result == null)
            {
                result = Screen("Result", new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f), "");
                resultTitle = Body(result).Find("Heading/H").GetComponent<Text>();
                resultBody = KendoUi.Label(KendoUi.Rect(Body(result), "Body", new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero), "T", "", 27, KendoUi.Sand, TextAnchor.UpperLeft);
                resultPrimary = KendoUi.PillButton(KendoUi.Rect(Body(result), "Primary", new Vector2(0.52f, 0.03f), new Vector2(0.92f, 0.12f), Vector2.zero, Vector2.zero), "B", "", () => { root.PopMenu(result); resultPrimaryAction?.Invoke(); }, 28);
                resultSecondary = KendoUi.PillButton(KendoUi.Rect(Body(result), "Secondary", new Vector2(0.08f, 0.03f), new Vector2(0.48f, 0.12f), Vector2.zero, Vector2.zero), "B", "", () => { root.PopMenu(result); resultSecondaryAction?.Invoke(); }, 28);
            }
            resultTitle.text = title.ToUpperInvariant();
            resultBody.text = body;
            KendoUi.SetLabel(resultPrimary, primary);
            resultPrimaryAction = onPrimary;
            resultSecondary.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            KendoUi.SetLabel(resultSecondary, secondary ?? "");
            resultSecondaryAction = onSecondary;
            root.PushMenu(result);
        }

        public bool FeedbackOpen => feedback != null && feedback.activeSelf;

        /// <summary>Lesson attempt feedback: what was right, what was wrong, principle, how to improve, what next.</summary>
        public void ShowFeedback(MasterFeedback fb, float score, string progress, Action onContinue, Action onDemo)
        {
            if (feedback == null)
            {
                feedback = KendoUi.Rect(root.MenuLayer, "Feedback", new Vector2(0.52f, 0.2f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero).gameObject;
                KendoUi.Image(feedback.transform, "Panel", KendoUi.PanelTranslucent, KendoUi.Rounded);
                feedbackTitle = KendoUi.Heading(KendoUi.Rect(feedback.transform, "Title", new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.97f), Vector2.zero, Vector2.zero), "T", "", 30);
                feedbackTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                feedbackBody = KendoUi.Label(KendoUi.Rect(feedback.transform, "Body", new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero), "B", "", 23, KendoUi.Sand, TextAnchor.UpperLeft);
                feedbackContinue = KendoUi.PillButton(KendoUi.Rect(feedback.transform, "Continue", new Vector2(0.52f, 0.03f), new Vector2(0.95f, 0.14f), Vector2.zero, Vector2.zero), "B", "Continue (F)", () => feedbackContinueAction?.Invoke(), 24);
                feedbackDemo = KendoUi.PillButton(KendoUi.Rect(feedback.transform, "Demo", new Vector2(0.05f, 0.03f), new Vector2(0.48f, 0.14f), Vector2.zero, Vector2.zero), "B", "Watch again (G)", () => feedbackDemoAction?.Invoke(), 24);
                feedback.SetActive(false);
            }
            feedbackTitle.text = (fb.success ? "+ " : "x ") + fb.headline;
            feedbackTitle.color = fb.success ? KendoUi.Good : KendoUi.Sand;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>Score {score:0}</b>   ·   {progress}");
            if (fb.correct.Count > 0) { sb.AppendLine(KendoUi.Colored("<b>Correct</b>", KendoUi.Good)); foreach (var c in fb.correct) sb.AppendLine("  • " + c); }
            if (fb.incorrect.Count > 0) { sb.AppendLine(KendoUi.Colored("<b>Needs work</b>", KendoUi.Bad)); foreach (var c in fb.incorrect) sb.AppendLine("  • " + c); }
            if (fb.principle.Length > 0) sb.AppendLine("<b>Principle:</b> " + fb.principle);
            if (fb.improve.Length > 0) sb.AppendLine("<b>How to improve:</b> " + fb.improve);
            if (fb.next.Length > 0) sb.AppendLine("<b>Next:</b> " + fb.next);
            feedbackBody.text = sb.ToString();
            feedbackContinueAction = () => { root.PopMenu(feedback); onContinue?.Invoke(); };
            feedbackDemoAction = () => { root.PopMenu(feedback); onDemo?.Invoke(); };
            root.PushMenu(feedback);
        }

        public void CloseFeedback() { if (feedback != null) root.PopMenu(feedback); }
        public void ContinueFeedback() { if (FeedbackOpen) feedbackContinueAction?.Invoke(); }
        public void DemoFromFeedback() { if (FeedbackOpen) feedbackDemoAction?.Invoke(); }
    }
}
