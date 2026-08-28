using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FishyUI
{
    public static class Native
    {
        public static bool Ready => Injector.W != null;

        public static float FontSize => Injector.W != null ? Injector.W.Fs : 36f;

        public static TMP_Text Label(Transform parent, string text, float size = 0f)
        {
            if (!Check()) return null;
            return Injector.W.MakeLabel(parent, text, size > 0f ? size : Injector.W.Fs * 0.5f,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one);
        }

        public static Button Button(Transform parent, string label, Action onClick)
        {
            if (!Check()) return null;
            return Injector.W.MakeButton(parent, label, Injector.W.Fs * 0.45f, Vector2.zero, Vector2.one, onClick);
        }

        public static Toggle Toggle(Transform parent, bool value, Action<bool> onChanged)
        {
            if (!Check()) return null;
            return Injector.W.MakeToggle(parent, Vector2.zero, Vector2.one, value, onChanged);
        }

        public static Slider Slider(Transform parent, float value, float min, float max, Action<float> onChanged)
        {
            if (!Check()) return null;
            Slider s = Injector.W.MakeSlider(parent, Vector2.zero, Vector2.one, onChanged);
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(value);
            return s;
        }

        public static TMP_InputField Input(Transform parent, string text, Action<string> onEndEdit)
        {
            if (!Check()) return null;
            return Injector.W.MakeInput(parent, Vector2.zero, Vector2.one, text, onEndEdit);
        }

        public static Slider Bar(Transform parent, float value)
        {
            if (!Check()) return null;
            Slider s = Injector.W.MakeSlider(parent, Vector2.zero, Vector2.one, v => { });
            s.interactable = false;
            if (s.handleRect != null) s.handleRect.gameObject.SetActive(false);
            s.SetValueWithoutNotify(Mathf.Clamp01(value));
            return s;
        }

        public static Image Icon(Transform parent, Sprite sprite)
        {
            RectTransform cell = Widgets.Area(parent, "Icon", Vector2.zero, Vector2.one);
            Image img = cell.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static void Tooltip(Component target, string text)
        {
            if (target == null) return;
            var hover = target.gameObject.GetComponent<TooltipHover>();
            if (hover == null) hover = target.gameObject.AddComponent<TooltipHover>();
            hover.Text = text;
        }

        public static Stack Column(Transform parent, float rowHeight = 0f)
            => new Stack(parent, rowHeight > 0f ? rowHeight : FontSize * 1.7f, 1);

        public static Stack Grid(Transform parent, int columns, float rowHeight = 0f)
            => new Stack(parent, rowHeight > 0f ? rowHeight : FontSize * 1.7f, Mathf.Max(1, columns));

        public class Stack
        {
            readonly Transform _parent;
            readonly float _rowHeight;
            readonly int _columns;
            int _next;

            public float Gap = 4f;

            internal Stack(Transform parent, float rowHeight, int columns)
            {
                _parent = parent;
                _rowHeight = rowHeight;
                _columns = columns;
            }

            public RectTransform Next()
            {
                int row = _next / _columns;
                int col = _next % _columns;
                _next++;
                float w = 1f / _columns;
                var go = new GameObject("Cell", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_parent, false);
                rt.anchorMin = new Vector2(col * w, 1f);
                rt.anchorMax = new Vector2((col + 1) * w, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(Gap, -(row + 1) * _rowHeight + Gap);
                rt.offsetMax = new Vector2(-Gap, -row * _rowHeight);
                return rt;
            }

            public float Height => Mathf.Ceil((float)_next / _columns) * _rowHeight;
        }

        public static TMP_Text Title(Transform parent, string text)
        {
            if (!Check()) return null;
            return Injector.W.MakeTitle(parent, text, Vector2.zero, Vector2.one);
        }

        public static void Choose(RectTransform anchor, string[] options, int current, Action<int> pick)
        {
            if (!Check()) return;
            Chooser.Open(Injector.W, anchor, options, current, pick);
        }

        static Picker _loosePicker;

        public static void PickColour(string label, Color current, Color defaultColour, Action<Color> apply)
        {
            if (!Check()) return;
            if (_loosePicker == null)
                _loosePicker = new Picker(Injector.W, Hud.Root, new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.8f));
            _loosePicker.Open(label, current, defaultColour, apply);
        }

        public static void CaptureKey(Action<KeyboardShortcut> done)
        {
            if (!Check() || done == null) return;
            LooseKeys keys = Hud.Root.GetComponent<LooseKeys>();
            if (keys == null) keys = Hud.Root.gameObject.AddComponent<LooseKeys>();
            keys.Pending = done;
        }

        public static RectTransform Scroll(Transform parent)
        {
            if (!Check()) return null;
            RectTransform viewport = Widgets.Area(parent, "Scroll", Vector2.zero, Vector2.one);
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Widgets.Area(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(-16f, 0f);
            ScrollRect sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 60f;
            Injector.W.MakeScrollbar(viewport, sr);
            return content;
        }

        static bool Check()
        {
            if (Injector.W != null) return true;
            Plugin.Log.LogWarning("FishyUI.Native called before any options screen existed");
            return false;
        }
    }
}

namespace FishyUI
{
    internal class LooseKeys : MonoBehaviour
    {
        public Action<KeyboardShortcut> Pending;

        void OnGUI()
        {
            if (Pending == null) return;
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            Action<KeyboardShortcut> done = Pending;
            Pending = null;
            KeyCode k = e.keyCode;
            e.Use();
            if (k == KeyCode.Escape) return;
            if (k == KeyCode.Backspace || k == KeyCode.Delete) { Run(done, KeyboardShortcut.Empty); return; }
            var mods = new System.Collections.Generic.List<KeyCode>();
            bool isMod = k == KeyCode.LeftControl || k == KeyCode.RightControl
                || k == KeyCode.LeftShift || k == KeyCode.RightShift
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt;
            if (!isMod)
            {
                if (e.control) mods.Add(KeyCode.LeftControl);
                if (e.shift) mods.Add(KeyCode.LeftShift);
                if (e.alt) mods.Add(KeyCode.LeftAlt);
            }
            Run(done, new KeyboardShortcut(k, mods.ToArray()));
        }

        static void Run(Action<KeyboardShortcut> done, KeyboardShortcut ks)
        {
            try { done(ks); }
            catch (Exception ex) { Trouble.Note("a key capture callback threw: " + ex.Message); }
        }
    }
}
