using System;
using System.Collections;
using System.Collections.Generic;
using Kendo.Campaign;
using UnityEngine;
using UnityEngine.UI;

namespace Kendo.Game
{
    /// <summary>
    /// All UI lives here, on the persistent root: fade, loading screen, toasts, interaction prompt, controls overlay,
    /// the master dialogue panel (SUTEMI mock-up layout), menus and HUDs. Gameplay asks <see cref="BlocksGameplay"/>.
    /// </summary>
    public sealed class KendoUiRoot : MonoBehaviour
    {
        private KendoGame game;
        private Canvas hudCanvas, dialogueCanvas, menuCanvas, fadeCanvas;
        private Image fade;
        private GameObject loading;
        private Image loadingBar;
        private Text loadingTip;
        private Text toast;
        private float toastUntil;
        private Text prompt;
        private GameObject controlsOverlay;
        private Text objective;

        // dialogue
        private GameObject dialogue;
        private Text dialogueHeader, dialogueText, dialogueSpeaker;
        private Image portrait;
        private Button dialogueContinue;
        private List<DialogueLine> lines;
        private int lineIndex;
        private float typed;
        private Action dialogueDone;

        public readonly List<GameObject> menuStack = new List<GameObject>();
        public LessonHud LessonHud { get; private set; }
        public CombatHud CombatHud { get; private set; }
        public Menus Menus { get; private set; }

        public bool DialogueOpen => dialogue != null && dialogue.activeSelf;
        /// <summary>True if the last dialogue was closed with Tab (skip) rather than read to the end.</summary>
        public bool LastDialogueSkipped { get; private set; }
        public bool MenuOpen => menuStack.Count > 0;
        public bool BlocksGameplay => DialogueOpen || MenuOpen || (game != null && game.Flow != null && game.Flow.Busy);
        public Transform MenuLayer => menuCanvas.transform;
        public Transform HudLayer => hudCanvas.transform;

        public void Init(KendoGame g)
        {
            game = g;
            hudCanvas = KendoUi.CreateCanvas("HUD", 10, transform);
            dialogueCanvas = KendoUi.CreateCanvas("Dialogue", 20, transform);
            menuCanvas = KendoUi.CreateCanvas("Menus", 30, transform);
            fadeCanvas = KendoUi.CreateCanvas("Fade", 100, transform);

            fade = KendoUi.Image(fadeCanvas.transform, "Fade", Color.black);
            fade.raycastTarget = false;
            fade.color = new Color(0, 0, 0, 1);

            BuildLoading();
            BuildToastAndPrompt();
            BuildControlsOverlay();
            BuildDialogue();
            LessonHud = new LessonHud(hudCanvas.transform);
            CombatHud = new CombatHud(hudCanvas.transform);
            Menus = new Menus(this, g);
            StartCoroutine(Fade(0f, 0.8f));
        }

        // ------------------------------------------------------------------ fade / loading / toast

        public IEnumerator Fade(float target, float seconds)
        {
            float start = fade.color.a;
            float t = 0f;
            fade.raycastTarget = true;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                fade.color = new Color(0, 0, 0, Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, target);
            fade.raycastTarget = target > 0.5f;
        }

        private void BuildLoading()
        {
            loading = KendoUi.Fill(fadeCanvas.transform, "Loading").gameObject;
            var title = KendoUi.Heading(KendoUi.Rect(loading.transform, "T", new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.7f), Vector2.zero, Vector2.zero), "Title", "Kendo: The Master's Legacy", 56, TextAnchor.MiddleCenter);
            title.raycastTarget = false;
            var barBg = KendoUi.Panel(loading.transform, "BarBg", new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.47f), KendoUi.SandDim);
            loadingBar = KendoUi.Image(barBg.transform, "Bar", KendoUi.Sand, KendoUi.Rounded);
            loadingBar.type = Image.Type.Filled;
            loadingBar.fillMethod = Image.FillMethod.Horizontal;
            loadingBar.fillAmount = 0f;
            loadingTip = KendoUi.Label(KendoUi.Rect(loading.transform, "Tip", new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.42f), Vector2.zero, Vector2.zero), "Tip", "", 30, KendoUi.Sand, TextAnchor.MiddleCenter, FontStyle.Italic);
            loading.SetActive(false);
        }

        public void ShowLoading(bool on, string tip)
        {
            loading.SetActive(on);
            if (on) { loadingTip.text = tip ?? ""; loadingBar.fillAmount = 0f; }
        }

        public void SetLoadingProgress(float p) { if (loadingBar != null) loadingBar.fillAmount = Mathf.Clamp01(p); }

        private void BuildToastAndPrompt()
        {
            var tp = KendoUi.Panel(hudCanvas.transform, "Toast", new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.94f), KendoUi.PanelTranslucent);
            toast = KendoUi.Label(KendoUi.Fill(tp.transform, "Pad", 12), "Text", "", 28, KendoUi.Sand, TextAnchor.MiddleCenter);
            tp.gameObject.SetActive(false);
            var pp = KendoUi.Rect(hudCanvas.transform, "Prompt", new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.18f), Vector2.zero, Vector2.zero);
            prompt = KendoUi.Label(pp, "Text", "", 32, KendoUi.Sand, TextAnchor.MiddleCenter, FontStyle.Bold);
            prompt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var op = KendoUi.Rect(hudCanvas.transform, "Objective", new Vector2(0.01f, 0.93f), new Vector2(0.6f, 0.99f), Vector2.zero, Vector2.zero);
            objective = KendoUi.Label(op, "Text", "", 24, KendoUi.Sand, TextAnchor.MiddleLeft);
            objective.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        }

        public void Toast(string text, float seconds = 3.5f)
        {
            if (toast == null) return;
            toast.text = text;
            toast.transform.parent.parent.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        public void SetPrompt(string text) { if (prompt != null) prompt.text = text ?? ""; }
        public void SetObjective(string text) { if (objective != null) objective.text = text ?? ""; }

        private void BuildControlsOverlay()
        {
            var p = KendoUi.Panel(hudCanvas.transform, "ControlsOverlay", new Vector2(0.78f, 0.42f), new Vector2(0.995f, 0.92f), KendoUi.PanelTranslucent);
            var t = KendoUi.Label(KendoUi.Fill(p.transform, "Pad", 14), "Text", ControlsText(true), 19, KendoUi.Sand, TextAnchor.UpperLeft);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            controlsOverlay = p.gameObject;
        }

        public static string ControlsText(bool compact)
        {
            string s =
                "<b>CONTROLS</b>  (F1 hides)\n" +
                "W/S  suriashi fwd/back\nA/D  suriashi left/right\nMouse  look\nHold RMB  aim kensen\n" +
                "LMB or Q  men\nE  kote   R  do   T  tsuki\nHold Shift  block\nSpace  kiai\nX  chudan / sageto\n" +
                "Hold B  bow (rei)\nF / Enter  interact\nG  watch demonstration\nTab  skip   Backspace  back\nEsc  pause\nHold Alt  free cursor";
            if (!compact) s = s.Replace("  (F1 hides)", "");
            return s;
        }

        public void ShowControlsOverlay(bool on) { if (controlsOverlay != null) controlsOverlay.SetActive(on); }
        public bool ControlsOverlayVisible => controlsOverlay != null && controlsOverlay.activeSelf;

        // ------------------------------------------------------------------ dialogue (SUTEMI layout)

        private void BuildDialogue()
        {
            dialogue = KendoUi.Rect(dialogueCanvas.transform, "MasterDialogue", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.42f), Vector2.zero, Vector2.zero).gameObject;
            var panel = KendoUi.Image(dialogue.transform, "Panel", KendoUi.PanelColor, KendoUi.Rounded);
            var pr = KendoUi.Rect(dialogue.transform, "Portrait", new Vector2(0.0f, 0.0f), new Vector2(0.2f, 1.25f), new Vector2(10, 0), new Vector2(0, 0));
            portrait = pr.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            dialogueHeader = KendoUi.Heading(KendoUi.Rect(dialogue.transform, "Header", new Vector2(0.22f, 0.78f), new Vector2(0.78f, 0.96f), Vector2.zero, Vector2.zero), "H", "", 42);
            var brand = KendoUi.Heading(KendoUi.Rect(dialogue.transform, "Brand", new Vector2(0.80f, 0.80f), new Vector2(0.98f, 0.96f), Vector2.zero, Vector2.zero), "B", "SUTEMI", 34, TextAnchor.MiddleRight);
            brand.raycastTarget = false;
            dialogueSpeaker = KendoUi.Label(KendoUi.Rect(dialogue.transform, "Speaker", new Vector2(0.22f, 0.66f), new Vector2(0.78f, 0.78f), Vector2.zero, Vector2.zero), "S", "", 24, KendoUi.SandDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            dialogueText = KendoUi.Label(KendoUi.Rect(dialogue.transform, "Text", new Vector2(0.22f, 0.22f), new Vector2(0.97f, 0.68f), Vector2.zero, Vector2.zero), "T", "", 32, KendoUi.Sand, TextAnchor.UpperLeft, FontStyle.Bold);
            var bl = KendoUi.Rect(dialogue.transform, "Continue", new Vector2(0.62f, 0.04f), new Vector2(0.86f, 0.2f), Vector2.zero, Vector2.zero);
            dialogueContinue = KendoUi.PillButton(bl, "Button", "Continue", NextLine, 30);
            var hint = KendoUi.Label(KendoUi.Rect(dialogue.transform, "Hint", new Vector2(0.22f, 0.04f), new Vector2(0.6f, 0.2f), Vector2.zero, Vector2.zero), "H", "F / Enter: continue   Backspace: back   Tab: skip", 20, KendoUi.SandDim, TextAnchor.MiddleLeft);
            hint.raycastTarget = false;
            panel.raycastTarget = true;
            dialogue.SetActive(false);
        }

        public void ShowDialogue(List<DialogueLine> l, string header, Action onDone)
        {
            if (l == null || l.Count == 0) { onDone?.Invoke(); return; }
            lines = l;
            LastDialogueSkipped = false;
            lineIndex = 0;
            typed = 0f;
            dialogueDone = onDone;
            dialogueHeader.text = (header ?? "").ToUpperInvariant();
            dialogue.SetActive(true);
            ShowLine();
        }

        private void ShowLine()
        {
            var line = lines[lineIndex];
            var cfg = game.Config;
            Sprite s = null;
            string who = "";
            switch (line.speaker)
            {
                case Speaker.Master: s = cfg.masterPortrait; who = CampaignContent.MasterName; break;
                case Speaker.FormerStudent: s = cfg.formerStudentPortrait != null ? cfg.formerStudentPortrait : null; who = CampaignContent.FormerStudentName; break;
                case Speaker.Narrator: who = ""; break;
            }
            portrait.sprite = s;
            portrait.enabled = s != null;
            dialogueSpeaker.text = who;
            typed = 0f;
            dialogueText.text = "";
            KendoUi.SetLabel(dialogueContinue, lineIndex == lines.Count - 1 ? "Continue" : "Next");
        }

        private void NextLine()
        {
            if (!DialogueOpen) return;
            var full = lines[lineIndex].text;
            if (dialogueText.text.Length < full.Length) { typed = full.Length; dialogueText.text = full; return; }
            if (lineIndex + 1 < lines.Count) { lineIndex++; ShowLine(); return; }
            CloseDialogue();
        }

        private void PrevLine()
        {
            if (!DialogueOpen || lineIndex == 0) return;
            lineIndex--;
            ShowLine();
            typed = lines[lineIndex].text.Length;
        }

        public void CloseDialogue()
        {
            dialogue.SetActive(false);
            var d = dialogueDone;
            dialogueDone = null;
            d?.Invoke();
        }

        // ------------------------------------------------------------------ menus

        public void PushMenu(GameObject m)
        {
            if (!menuStack.Contains(m)) menuStack.Add(m);
            m.SetActive(true);
            m.transform.SetAsLastSibling();
        }

        public void PopMenu(GameObject m)
        {
            menuStack.Remove(m);
            if (m != null) m.SetActive(false);
        }

        public void CloseAllMenus()
        {
            foreach (var m in menuStack.ToArray()) if (m != null) m.SetActive(false);
            menuStack.Clear();
            if (game != null && game.Paused) game.Paused = false;
        }

        private void Update()
        {
            if (toast != null && Time.unscaledTime > toastUntil) toast.transform.parent.parent.gameObject.SetActive(false);
            var input = game.Input.Read();
            if (input.toggleControls) ShowControlsOverlay(!ControlsOverlayVisible);
            if (DialogueOpen)
            {
                var full = lines[lineIndex].text;
                if (dialogueText.text.Length < full.Length)
                {
                    typed += Time.unscaledDeltaTime * 55f * game.Settings.textSpeed;
                    dialogueText.text = full.Substring(0, Mathf.Min(full.Length, (int)typed));
                }
                if (input.confirm) NextLine();
                else if (input.back) PrevLine();
                else if (input.skip) { LastDialogueSkipped = true; CloseDialogue(); }
                return;
            }
            if (Menus.FeedbackOpen && menuStack.Count > 0 && menuStack[menuStack.Count - 1].name == "Feedback")
            {
                if (input.confirm) { Menus.ContinueFeedback(); return; }
                if (input.replayDemo) { Menus.DemoFromFeedback(); return; }
            }
            if (input.pause)
            {
                if (MenuOpen) Menus.Back();
                else Menus.OpenPause();
            }
            else if (input.back && MenuOpen) Menus.Back();
            bool free = BlocksGameplay || input.freeCursor || !Application.isFocused || game.Paused || game.Input.XRActive;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        /// <summary>The cursor policy without the frame input (used by tests).</summary>
        public static bool CursorShouldBeFree(bool blocking, bool freeHeld, bool focused, bool paused, bool xr) => blocking || freeHeld || !focused || paused || xr;
    }
}
