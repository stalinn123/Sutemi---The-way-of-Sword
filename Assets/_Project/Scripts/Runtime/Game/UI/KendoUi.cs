using System;
using Kendo.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Kendo.Game
{
    /// <summary>
    /// uGUI factory in the style of the supplied SUTEMI mock-ups: deep indigo rounded panels, sand-coloured display text,
    /// sand pill buttons with indigo labels. Everything is built from code so no prefab can go missing.
    /// </summary>
    public static class KendoUi
    {
        public static readonly Color PanelColor = KendoPalette.PanelIndigo;
        public static readonly Color PanelTranslucent = new Color(0f, 0.153f, 0.239f, 0.92f);
        public static readonly Color Sand = KendoPalette.Sand;
        public static readonly Color SandDim = new Color(0.906f, 0.706f, 0.482f, 0.55f);
        public static readonly Color Good = new Color(0.55f, 0.85f, 0.55f);
        public static readonly Color Bad = new Color(0.95f, 0.45f, 0.40f);
        public static readonly Color Red = new Color(0.80f, 0.12f, 0.12f);
        public static readonly Color White = new Color(0.95f, 0.95f, 0.92f);

        private static Sprite rounded, pill, circle;
        private static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                var cfg = KendoGameConfig.Load();
                if (cfg != null && cfg.font != null) font = cfg.font;
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 18, "KendoRounded"));
        public static Sprite Pill => pill != null ? pill : (pill = MakeRounded(64, 31, "KendoPill"));
        public static Sprite Circle => circle != null ? circle : (circle = MakeRounded(64, 32, "KendoCircle"));

        private static Sprite MakeRounded(int size, int radius, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(255 * Mathf.Clamp01(radius - d + 0.5f));
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name, float margin = 0f) =>
            Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(margin, margin), new Vector2(-margin, -margin));

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Fill(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return img;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color? color = null)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color ?? PanelColor;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Fill(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Display heading in the mock-ups' style (upper case, bold, sand).</summary>
        public static Text Heading(Transform parent, string name, string text, int size, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Label(parent, name, text.ToUpperInvariant(), size, Sand, align, FontStyle.Bold);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.5f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button PillButton(Transform parent, string name, string label, UnityAction onClick, int fontSize = 30)
        {
            var rt = Fill(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Pill;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Sand;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.8f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.6f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = colors;
            b.targetGraphic = img;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.onClick.AddListener(() => KendoGame.Instance?.Audio?.Ui(true));
            var t = Label(rt, "Label", label.ToUpperInvariant(), fontSize, KendoPalette.ButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.raycastTarget = false;
            return b;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label.ToUpperInvariant();
        }

        public static Slider Slider(Transform parent, string name, float min, float max, float value, UnityAction<float> onChange)
        {
            var rt = Fill(parent, name);
            var bg = Image(rt, "Background", SandDim, Pill);
            ((RectTransform)bg.transform).anchorMin = new Vector2(0, 0.35f);
            ((RectTransform)bg.transform).anchorMax = new Vector2(1, 0.65f);
            var fillArea = Rect(rt, "Fill Area", new Vector2(0, 0.35f), new Vector2(1, 0.65f), new Vector2(8, 0), new Vector2(-8, 0));
            var fill = Image(fillArea, "Fill", Sand, Pill);
            var handleArea = Rect(rt, "Handle Area", Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
            var handle = Rect(handleArea, "Handle", new Vector2(0, 0), new Vector2(0, 1), new Vector2(-14, 0), new Vector2(14, 0));
            var hi = handle.gameObject.AddComponent<Image>();
            hi.sprite = Circle;
            hi.color = White;
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = handle;
            s.targetGraphic = hi;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            if (onChange != null) s.onValueChanged.AddListener(onChange);
            return s;
        }

        public static Toggle Toggle(Transform parent, string name, bool value, UnityAction<bool> onChange)
        {
            var rt = Fill(parent, name);
            var box = Rect(rt, "Box", new Vector2(0, 0.1f), new Vector2(0, 0.9f), Vector2.zero, new Vector2(48, 0));
            var bg = box.gameObject.AddComponent<Image>();
            bg.sprite = Rounded;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = SandDim;
            var mark = Image(box, "Check", Sand, Rounded);
            ((RectTransform)mark.transform).offsetMin = new Vector2(8, 8);
            ((RectTransform)mark.transform).offsetMax = new Vector2(-8, -8);
            var t = rt.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = mark;
            t.isOn = value;
            if (onChange != null) t.onValueChanged.AddListener(onChange);
            return t;
        }

        /// <summary>Simple vertical layout helper: returns the rect for row <paramref name="i"/> of <paramref name="n"/>.</summary>
        public static RectTransform Row(Transform parent, string name, int i, int n, float top = 1f, float bottom = 0f, float gap = 0.15f)
        {
            float h = (top - bottom) / n;
            float y1 = top - i * h;
            float y0 = y1 - h * (1f - gap);
            return Rect(parent, name, new Vector2(0, y0), new Vector2(1, y1), Vector2.zero, Vector2.zero);
        }

        public static string Colored(string text, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";

        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public static void SafeInvoke(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
