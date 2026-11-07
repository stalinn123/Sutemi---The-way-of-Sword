using Kendo.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Kendo.Game
{
    /// <summary>Lesson HUD: title, step, instruction, progress bar, attempts, live checks, bow meter and ki-ken-tai-itchi timing bars.</summary>
    public sealed class LessonHud
    {
        private readonly GameObject root;
        private readonly Text title, step, instruction, attempts, checks, hint;
        private readonly Image progress;
        private readonly GameObject meter, timing;
        private readonly Image meterFill, meterTarget;
        private readonly Text meterLabel;
        private readonly RectTransform kiaiMark, footMark, strikeMark, window;
        private readonly Text timingLabel;

        public LessonHud(Transform parent)
        {
            var p = KendoUi.Panel(parent, "LessonHud", new Vector2(0.01f, 0.52f), new Vector2(0.36f, 0.91f), KendoUi.PanelTranslucent);
            root = p.gameObject;
            title = KendoUi.Heading(KendoUi.Rect(p.transform, "Title", new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero), "T", "", 28);
            step = KendoUi.Label(KendoUi.Rect(p.transform, "Step", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero), "S", "", 20, KendoUi.SandDim);
            var barBg = KendoUi.Panel(p.transform, "ProgressBg", new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.75f), KendoUi.SandDim);
            progress = KendoUi.Image(barBg.transform, "Fill", KendoUi.Sand, KendoUi.Rounded);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            instruction = KendoUi.Label(KendoUi.Rect(p.transform, "Instruction", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.71f), Vector2.zero, Vector2.zero), "I", "", 21, KendoUi.Sand, TextAnchor.UpperLeft);
            checks = KendoUi.Label(KendoUi.Rect(p.transform, "Checks", new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.38f), Vector2.zero, Vector2.zero), "C", "", 19, KendoUi.Sand, TextAnchor.UpperLeft);
            attempts = KendoUi.Label(KendoUi.Rect(p.transform, "Attempts", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.11f), Vector2.zero, Vector2.zero), "A", "", 18, KendoUi.SandDim);
            hint = KendoUi.Label(KendoUi.Rect(parent, "LessonHint", new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.26f), Vector2.zero, Vector2.zero), "H", "", 22, KendoUi.SandDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // bow depth meter (vertical, right of centre)
            var m = KendoUi.Panel(parent, "BowMeter", new Vector2(0.62f, 0.35f), new Vector2(0.645f, 0.7f), KendoUi.PanelTranslucent);
            meter = m.gameObject;
            meterFill = KendoUi.Image(m.transform, "Fill", KendoUi.Sand, KendoUi.Rounded);
            meterFill.type = Image.Type.Filled;
            meterFill.fillMethod = Image.FillMethod.Vertical;
            meterFill.fillOrigin = (int)Image.OriginVertical.Top;
            var tgt = KendoUi.Rect(m.transform, "Target", new Vector2(-0.4f, 0.5f), new Vector2(1.4f, 0.5f), new Vector2(0, -3), new Vector2(0, 3));
            meterTarget = tgt.gameObject.AddComponent<Image>();
            meterTarget.color = KendoUi.Good;
            meterLabel = KendoUi.Label(KendoUi.Rect(m.transform, "Label", new Vector2(-2f, -0.12f), new Vector2(3f, -0.02f), Vector2.zero, Vector2.zero), "L", "", 20, KendoUi.Sand, TextAnchor.MiddleCenter);
            meter.SetActive(false);

            // timing bars: window around the impact line, marks for kiai and fumikomi
            var tb = KendoUi.Panel(parent, "Timing", new Vector2(0.35f, 0.28f), new Vector2(0.65f, 0.34f), KendoUi.PanelTranslucent);
            timing = tb.gameObject;
            window = KendoUi.Rect(tb.transform, "Window", new Vector2(0.4f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            window.gameObject.AddComponent<Image>().color = new Color(0.906f, 0.706f, 0.482f, 0.35f);
            strikeMark = Mark(tb.transform, "Impact", KendoUi.White);
            kiaiMark = Mark(tb.transform, "Kiai", KendoUi.Good);
            footMark = Mark(tb.transform, "Foot", new Color(0.5f, 0.75f, 1f));
            timingLabel = KendoUi.Label(KendoUi.Rect(tb.transform, "Label", new Vector2(0f, 1.0f), new Vector2(1f, 1.8f), Vector2.zero, Vector2.zero), "L", "impact | kiai (green) · fumikomi (blue)", 18, KendoUi.Sand, TextAnchor.MiddleCenter);
            timing.SetActive(false);
            root.SetActive(false);
        }

        private static RectTransform Mark(Transform parent, string name, Color c)
        {
            var r = KendoUi.Rect(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-3, 0), new Vector2(3, 0));
            r.gameObject.AddComponent<Image>().color = c;
            return r;
        }

        public bool Visible => root.activeSelf;
        public void Show(bool on) { root.SetActive(on); hint.gameObject.SetActive(on); if (!on) { meter.SetActive(false); timing.SetActive(false); } }

        public void Set(string lessonTitle, string stepText, string instr, float progress01, string attemptsText)
        {
            title.text = lessonTitle.ToUpperInvariant();
            step.text = stepText;
            instruction.text = instr;
            progress.fillAmount = Mathf.Clamp01(progress01);
            attempts.text = attemptsText;
        }

        public void SetChecks(string text) => checks.text = text;
        public void SetHint(string text) => hint.text = text ?? "";

        public void ShowBowMeter(bool on, float angle = 0f, float target = 0f)
        {
            meter.SetActive(on);
            if (!on) return;
            meterFill.fillAmount = Mathf.Clamp01(angle / 45f);
            var r = (RectTransform)meterTarget.transform;
            float y = 1f - Mathf.Clamp01(target / 45f);
            r.anchorMin = new Vector2(-0.4f, y);
            r.anchorMax = new Vector2(1.4f, y);
            meterLabel.text = $"{angle:0}° / {target:0}°";
        }

        /// <summary>Offsets in seconds relative to impact; window = ki-ken-tai-itchi tolerance. NaN hides a mark.</summary>
        public void ShowTiming(bool on, float kiaiOffset = float.NaN, float footOffset = float.NaN, float windowSeconds = 0.15f)
        {
            timing.SetActive(on);
            if (!on) return;
            const float span = 0.6f; // ±0.6 s shown
            float w = Mathf.Clamp01(windowSeconds / span) * 0.5f;
            window.anchorMin = new Vector2(0.5f - w, 0f);
            window.anchorMax = new Vector2(0.5f + w, 1f);
            Place(kiaiMark, kiaiOffset, span);
            Place(footMark, footOffset, span);
            timingLabel.text = $"kiai {Fmt(kiaiOffset)} · fumikomi {Fmt(footOffset)}";
        }

        private static string Fmt(float o) => float.IsNaN(o) || Mathf.Abs(o) > 5f ? "–" : (o < 0 ? $"{-o:0.00}s early" : $"{o:0.00}s late");

        private static void Place(RectTransform r, float offset, float span)
        {
            bool show = !float.IsNaN(offset) && Mathf.Abs(offset) <= span;
            r.gameObject.SetActive(show);
            if (!show) return;
            float x = 0.5f + 0.5f * offset / span;
            r.anchorMin = new Vector2(x, 0f);
            r.anchorMax = new Vector2(x, 1f);
        }

        public static string ChecklistFromEvaluation(Evaluation ev)
        {
            if (ev == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var c in ev.criteria)
                sb.AppendLine((c.passed ? KendoUi.Colored("+", KendoUi.Good) : KendoUi.Colored("x", KendoUi.Bad)) + " " + Kendo.Campaign.MasterFeedback.Name(c.criterion));
            return sb.ToString();
        }
    }

    /// <summary>Combat HUD: names, ippon marks with targets, clock, hansoku, shinpan flags, the last strike's verdict.</summary>
    public sealed class CombatHud
    {
        private readonly GameObject root;
        private readonly Text playerName, opponentName, clock, playerPoints, opponentPoints, verdict, banner, status;
        private readonly Image[] flags = new Image[3];
        private float bannerUntil, verdictUntil;

        public CombatHud(Transform parent)
        {
            var top = KendoUi.Panel(parent, "CombatHud", new Vector2(0.25f, 0.9f), new Vector2(0.75f, 0.99f), KendoUi.PanelTranslucent);
            root = top.gameObject;
            playerName = KendoUi.Label(KendoUi.Rect(top.transform, "PN", new Vector2(0.02f, 0.5f), new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero), "T", "", 22, KendoUi.Sand);
            playerPoints = KendoUi.Label(KendoUi.Rect(top.transform, "PP", new Vector2(0.02f, 0f), new Vector2(0.38f, 0.55f), Vector2.zero, Vector2.zero), "T", "", 30, KendoUi.Red, TextAnchor.MiddleLeft, FontStyle.Bold);
            clock = KendoUi.Heading(KendoUi.Rect(top.transform, "Clock", new Vector2(0.4f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero), "T", "", 40, TextAnchor.MiddleCenter);
            opponentName = KendoUi.Label(KendoUi.Rect(top.transform, "ON", new Vector2(0.62f, 0.5f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero), "T", "", 22, KendoUi.Sand, TextAnchor.MiddleRight);
            opponentPoints = KendoUi.Label(KendoUi.Rect(top.transform, "OP", new Vector2(0.62f, 0f), new Vector2(0.98f, 0.55f), Vector2.zero, Vector2.zero), "T", "", 30, KendoUi.White, TextAnchor.MiddleRight, FontStyle.Bold);
            var fl = KendoUi.Rect(parent, "Flags", new Vector2(0.44f, 0.84f), new Vector2(0.56f, 0.89f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 3; i++)
            {
                var r = KendoUi.Rect(fl, "Flag" + i, new Vector2(i / 3f + 0.03f, 0), new Vector2((i + 1) / 3f - 0.03f, 1), Vector2.zero, Vector2.zero);
                flags[i] = r.gameObject.AddComponent<Image>();
                flags[i].sprite = KendoUi.Rounded;
                flags[i].type = Image.Type.Sliced;
                flags[i].enabled = false;
            }
            verdict = KendoUi.Label(KendoUi.Rect(parent, "Verdict", new Vector2(0.3f, 0.7f), new Vector2(0.7f, 0.83f), Vector2.zero, Vector2.zero), "T", "", 26, KendoUi.Sand, TextAnchor.UpperCenter);
            verdict.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            banner = KendoUi.Heading(KendoUi.Rect(parent, "Banner", new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.6f), Vector2.zero, Vector2.zero), "T", "", 80, TextAnchor.MiddleCenter);
            status = KendoUi.Label(KendoUi.Rect(parent, "Status", new Vector2(0.01f, 0.01f), new Vector2(0.5f, 0.06f), Vector2.zero, Vector2.zero), "T", "", 18, KendoUi.SandDim);
            root.SetActive(false);
            banner.text = "";
        }

        public void Show(bool on)
        {
            root.SetActive(on);
            banner.gameObject.SetActive(on);
            verdict.gameObject.SetActive(on);
            status.gameObject.SetActive(on);
            foreach (var f in flags) f.enabled = false;
        }

        public void Set(string player, string opponent, KendoMatch m, string pointsP, string pointsO)
        {
            playerName.text = player;
            opponentName.text = opponent;
            float t = Mathf.Max(0f, m.TimeRemaining);
            clock.text = m.InEncho && m.rules.enchoSeconds <= 0 ? "ENCHO" : $"{(int)(t / 60)}:{(int)(t % 60):00}";
            playerPoints.text = pointsP + (m.hansoku[0] > 0 ? $"  H{m.hansoku[0]}" : "");
            opponentPoints.text = (m.hansoku[1] > 0 ? $"H{m.hansoku[1]}  " : "") + pointsO;
            if (Time.unscaledTime > bannerUntil) banner.text = "";
            if (Time.unscaledTime > verdictUntil) { verdict.text = ""; foreach (var f in flags) f.enabled = false; }
        }

        public void Banner(string text, float seconds = 1.4f) { banner.text = text.ToUpperInvariant(); bannerUntil = Time.unscaledTime + seconds; }

        public void Verdict(string text, int flagsRaised, bool forPlayer, float seconds = 3f)
        {
            verdict.text = text;
            verdictUntil = Time.unscaledTime + seconds;
            for (int i = 0; i < 3; i++)
            {
                flags[i].enabled = true;
                flags[i].color = i < flagsRaised ? (forPlayer ? KendoUi.Red : KendoUi.White) : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            }
        }

        public void Status(string text) => status.text = text;
    }
}
