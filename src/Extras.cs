using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Toast
    {
        public static bool Frosted;
        public static Color PanelColour = Frost.Panel;
        public static Corner Where = Corner.BottomRight;
        public static float Seconds = 3f;

        static readonly List<ToastBox> Live = new List<ToastBox>();

        public static void Show(string text) => Show(text, Seconds, Frosted);

        public static void Show(string text, float seconds) => Show(text, seconds, Frosted);

        public static void Show(string text, float seconds, bool frosted)
        {
            Widgets w = Injector.W;
            if (w == null) { Plugin.Log.LogInfo("toast before any menu existed: " + text); return; }
            RectTransform rt = Widgets.CloneFrame(Hud.Root);
            if (rt == null) return;
            rt.name = "Toast";
            rt.sizeDelta = new Vector2(300f, 52f);

            var frame = new List<Graphic>();
            foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(true)) frame.Add(g);

            TMP_Text label = w.MakeLabel(rt, text, w.Fs * 0.34f, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0f), new Vector2(0.94f, 1f));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            float needed = label.GetPreferredValues(text).x + 34f;
            rt.sizeDelta = new Vector2(Mathf.Clamp(needed, 180f, 460f), 52f);

            if (!frosted)
                Frost.Apply(rt, frame, false, PanelColour, PanelColour,
                    new Dictionary<Graphic, Material>(), new Dictionary<Graphic, Color>());

            ToastBox box = rt.gameObject.AddComponent<ToastBox>();
            box.Life = Mathf.Max(0.5f, seconds);
            Live.Add(box);
            Restack();
        }

        public static void Clear()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null) UnityEngine.Object.Destroy(Live[i].gameObject);
            Live.Clear();
        }

        internal static void Gone(ToastBox box)
        {
            Live.Remove(box);
            Restack();
        }

        static void Restack()
        {
            Vector2 a = HudElement.AnchorOf(Where);
            bool fromTop = a.y > 0.6f;
            float x = a.x > 0.6f ? -28f : (a.x < 0.4f ? 28f : 0f);
            float y = fromTop ? -28f : 28f;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var rt = (RectTransform)Live[i].transform;
                rt.anchorMin = rt.anchorMax = a;
                rt.pivot = a;
                rt.anchoredPosition = new Vector2(x, y);
                y += (fromTop ? -1f : 1f) * (rt.sizeDelta.y + 8f);
            }
        }
    }

    internal class ToastBox : MonoBehaviour
    {
        public float Life = 3f;
        float _age;
        CanvasGroup _group;

        void Awake()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float left = Life - _age;
            if (_group != null) _group.alpha = left < 0.5f ? Mathf.Clamp01(left / 0.5f) : 1f;
            if (_age < Life) return;
            Toast.Gone(this);
            Destroy(gameObject);
        }

        void OnDestroy() => Toast.Gone(this);
    }

    internal static class Dialog
    {
        public static void Confirm(string title, string message, Action onYes, Action onNo = null)
            => Build(title, message, onYes, onNo, true);

        public static void Info(string title, string message, Action onClose = null)
            => Build(title, message, onClose, null, false);

        public static void Prompt(string title, string message, string start, Action<string> onOk)
        {
            Widgets w = Injector.W;
            Window win = Window.Create("dialog:" + title, title, 620f, 360f);
            if (win == null || w == null) return;
            win.Resizable = false;
            win.MinSize = new Vector2(620f, 360f);
            win.Center();

            TMP_Text label = w.MakeLabel(win.Body, message, w.Fs * 0.42f, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.62f), new Vector2(0.96f, 1f));
            label.textWrappingMode = TextWrappingModes.Normal;

            string typed = start ?? "";
            TMP_InputField box = w.MakeInput(win.Body, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.56f),
                typed, v => typed = v);
            w.MakeButton(win.Body, "OK", w.Fs * 0.45f, new Vector2(0.08f, 0.06f), new Vector2(0.47f, 0.3f),
                () =>
                {
                    if (box != null) typed = box.text;
                    win.Destroy();
                    if (onOk != null) Run(() => onOk(typed));
                });
            w.MakeButton(win.Body, "Cancel", w.Fs * 0.45f, new Vector2(0.53f, 0.06f), new Vector2(0.92f, 0.3f), win.Destroy);
            win.Show();
        }

        static void Build(string title, string message, Action yes, Action no, bool askKind)
        {
            Widgets w = Injector.W;
            Window win = Window.Create("dialog:" + title, title, 620f, 340f);
            if (win == null || w == null) return;
            win.Resizable = false;
            win.MinSize = new Vector2(620f, 340f);
            win.Center();

            TMP_Text label = w.MakeLabel(win.Body, message, w.Fs * 0.45f, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.42f), new Vector2(0.96f, 1f));
            label.textWrappingMode = TextWrappingModes.Normal;

            if (askKind)
            {
                w.MakeButton(win.Body, "Yes", w.Fs * 0.45f, new Vector2(0.08f, 0.08f), new Vector2(0.47f, 0.32f),
                    () => { win.Destroy(); Run(yes); });
                w.MakeButton(win.Body, "No", w.Fs * 0.45f, new Vector2(0.53f, 0.08f), new Vector2(0.92f, 0.32f),
                    () => { win.Destroy(); Run(no); });
            }
            else
            {
                w.MakeButton(win.Body, "OK", w.Fs * 0.45f, new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.32f),
                    () => { win.Destroy(); Run(yes); });
            }
            win.Show();
        }

        static void Run(Action a)
        {
            if (a == null) return;
            try { a(); }
            catch (Exception e) { Plugin.Log.LogWarning("dialog callback failed: " + e); }
        }
    }

    internal class TooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text;
        static GameObject _box;
        static TMP_Text _label;
        static TooltipHover _owner;

        public void OnPointerEnter(PointerEventData e)
        {
            _owner = this;
            if (_box == null) MakeBox();
            if (_box == null) return;
            _label.text = Text;
            _box.SetActive(true);
            _box.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_owner != this) return;
            _owner = null;
            if (_box != null) _box.SetActive(false);
        }

        void OnDisable() => OnPointerExit(null);

        void MakeBox()
        {
            Widgets w = Injector.W;
            RectTransform rt = Widgets.CloneFrame(Hud.Root);
            if (rt == null || w == null) return;
            rt.name = "Tooltip";
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(420f, 74f);
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            _label = w.MakeLabel(rt, "", w.Fs * 0.4f, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f));
            _label.textWrappingMode = TextWrappingModes.Normal;
            _box = rt.gameObject;
            _box.AddComponent<TooltipFollow>();
        }
    }

    internal class TooltipFollow : MonoBehaviour
    {
        void Update()
        {
            var rt = (RectTransform)transform;
            RectTransform canvas = Hud.Root;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, Input.mousePosition, null, out local)) return;
            local += new Vector2(18f, 18f);
            float maxX = canvas.rect.width * 0.5f - rt.sizeDelta.x;
            float maxY = canvas.rect.height * 0.5f - rt.sizeDelta.y;
            local.x = Mathf.Min(local.x, maxX);
            local.y = Mathf.Min(local.y, maxY);
            rt.anchoredPosition = local + canvas.rect.size * 0.5f;
        }
    }
}
