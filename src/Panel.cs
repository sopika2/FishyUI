using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    internal class PanelHost : MonoBehaviour
    {
        Widgets _w;
        float _fs;

        RectTransform _sidebarRoot;
        RowBuilder _rows;
        Picker _picker;

        static int _selected;

        readonly List<Image> _sidebarMarks = new List<Image>();
        int _sidebarCount;

        public void Init(Widgets w)
        {
            _w = w;
            _fs = w.Fs;

            _w.MakeTitle(transform, "Mods", new Vector2(0.25f, 0.925f), new Vector2(0.75f, 0.995f));

            RectTransform sideView = Widgets.Area(transform, "SidebarView", new Vector2(0.025f, 0.03f), new Vector2(0.235f, 0.9f));
            Image sideBg = sideView.gameObject.AddComponent<Image>();
            sideBg.color = Color.clear;
            sideView.gameObject.AddComponent<RectMask2D>();
            _sidebarRoot = Widgets.Area(sideView, "Sidebar", new Vector2(0f, 1f), new Vector2(1f, 1f));
            _sidebarRoot.pivot = new Vector2(0.5f, 1f);
            _sidebarRoot.sizeDelta = new Vector2(0f, 10f);
            ScrollRect sideScroll = sideView.gameObject.AddComponent<ScrollRect>();
            sideScroll.viewport = sideView;
            sideScroll.content = _sidebarRoot;
            sideScroll.horizontal = false;
            sideScroll.movementType = ScrollRect.MovementType.Clamped;
            sideScroll.scrollSensitivity = 45f;
            _w.MakeScrollbar(sideView, sideScroll);

            RectTransform viewport = Widgets.Area(transform, "Rows", new Vector2(0.26f, 0.03f), new Vector2(0.975f, 0.9f));
            _picker = new Picker(_w, transform, new Vector2(0.26f, 0.03f), new Vector2(0.975f, 0.9f));
            _rows = new RowBuilder(_w, viewport, _picker);

            BuildSidebar();
            ShowSelected();
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (Registry.Dirty || _sidebarCount != Registry.Count)
            {
                Registry.Dirty = false;
                BuildSidebar();
            }
            ShowSelected();
        }

        public void Hide()
        {
            if (_rows != null) _rows.Sleep();
            gameObject.SetActive(false);
        }

        void ShowSelected()
        {
            if (_rows == null || Registry.Count == 0) return;
            if (_selected >= Registry.Count) _selected = 0;
            _rows.Page = Registry.All[_selected];
            _rows.Build();
        }

        void BuildSidebar()
        {
            foreach (Transform child in _sidebarRoot) Destroy(child.gameObject);
            _sidebarMarks.Clear();
            _sidebarCount = Registry.Count;
            if (_selected >= _sidebarCount) _selected = 0;

            float rowH = Mathf.Max(46f, _fs * 1.55f);
            float y = 0f;
            for (int i = 0; i < Registry.All.Count; i++)
            {
                int idx = i;
                Page p = Registry.All[i];
                var go = new GameObject("PageBtn", typeof(RectTransform));
                var cell = (RectTransform)go.transform;
                cell.SetParent(_sidebarRoot, false);
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = new Vector2(1f, 1f);
                cell.pivot = new Vector2(0.5f, 1f);
                cell.offsetMin = new Vector2(0f, -(y + rowH - 5f));
                cell.offsetMax = new Vector2(-16f, -y);
                Image mark = Widgets.MakeImage(cell, Color.clear, Vector2.zero, Vector2.one);
                mark.raycastTarget = false;
                _sidebarMarks.Add(mark);
                _w.MakeButton(cell, p.Title, _fs * 0.42f, Vector2.zero, Vector2.one, () =>
                {
                    _selected = idx;
                    RefreshSidebar();
                    ShowSelected();
                });
                y += rowH;
            }
            _sidebarRoot.sizeDelta = new Vector2(0f, y);
            RefreshSidebar();
        }

        void RefreshSidebar()
        {
            for (int i = 0; i < _sidebarMarks.Count; i++)
                if (_sidebarMarks[i] != null)
                    _sidebarMarks[i].color = i == _selected ? new Color(1f, 1f, 1f, 0.10f) : Color.clear;
        }
    }

    internal class RowBuilder
    {
        readonly Widgets _w;
        readonly float _fs;
        readonly float _rowPx;
        readonly RectTransform _content;
        readonly Picker _picker;
        readonly KeyCapture _capture;

        Page _page;
        string _filter = "";

        public Page Page
        {
            get { return _page; }
            set
            {
                if (_page == value) return;
                _page = value;
                _filter = "";
            }
        }
        readonly List<KeyValuePair<Row, RectTransform>> _cells = new List<KeyValuePair<Row, RectTransform>>();

        const float Gutter = 16f;

        public float ContentHeight => _content != null ? _content.sizeDelta.y : 0f;

        public RowBuilder(Widgets w, RectTransform viewport, Picker picker)
        {
            _w = w;
            _fs = w.Fs;
            _rowPx = Mathf.Max(48f, _fs * 1.7f);
            _picker = picker;

            Image bg = viewport.gameObject.AddComponent<Image>();
            bg.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = Widgets.Area(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0f, 10f);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = _rowPx * 0.9f;

            _w.MakeScrollbar(viewport, scroll);

            _capture = viewport.gameObject.AddComponent<KeyCapture>();
            _capture.Owner = this;
        }

        public void Sleep()
        {
            if (_capture != null) _capture.Cancel();
            if (_picker != null) _picker.Close();
            Chooser.Close();
        }

        public void Build()
        {
            Sleep();
            foreach (Transform child in _content) UnityEngine.Object.Destroy(child.gameObject);
            if (Page == null) return;

            Dictionary<KeyCode, int> keyUse = Registry.KeyUse();
            _cells.Clear();
            float y = 0f;
            foreach (Row r in Page.Rows)
            {
                float h = _rowPx * r.Height;
                RectTransform cell = RowCell(y, h);
                _cells.Add(new KeyValuePair<Row, RectTransform>(r, cell));
                try
                {
                    BuildRow(cell, r, keyUse);
                    if (!string.IsNullOrEmpty(r.Tip))
                    {
                        Image hover = Widgets.MakeImage(cell, Color.clear, Vector2.zero, Vector2.one);
                        hover.transform.SetAsFirstSibling();
                        Native.Tooltip(hover, r.Tip);
                    }
                }
                catch (Exception e) { Trouble.Note("row '" + r.Label + "' failed: " + e.Message); }
                y += h;
            }
            _content.anchoredPosition = Vector2.zero;
            Relayout();
        }

        void Relayout()
        {
            float y = 0f;
            foreach (KeyValuePair<Row, RectTransform> kv in _cells)
            {
                if (kv.Value == null) continue;
                bool show = Shown(kv.Key);
                if (kv.Value.gameObject.activeSelf != show) kv.Value.gameObject.SetActive(show);
                if (!show) continue;
                float h = _rowPx * kv.Key.Height;
                kv.Value.offsetMin = new Vector2(6f, -(y + h));
                kv.Value.offsetMax = new Vector2(-Gutter, -y);
                y += h;
            }
            _content.sizeDelta = new Vector2(0f, y + _rowPx * 0.3f);
        }

        bool Shown(Row r)
        {
            if (string.IsNullOrEmpty(_filter) || r.Kind == RowKind.Search) return true;
            if (r.Kind == RowKind.Header || r.Kind == RowKind.Label || r.Kind == RowKind.Space) return false;
            return !string.IsNullOrEmpty(r.Label) &&
                   r.Label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        RectTransform RowCell(float y, float h)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_content, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(6f, -(y + h));
            rt.offsetMax = new Vector2(-Gutter, -y);
            return rt;
        }

        void BuildRow(RectTransform cell, Row r, Dictionary<KeyCode, int> keyUse)
        {
            switch (r.Kind)
            {
                case RowKind.Space:
                    break;

                case RowKind.Header:
                {
                    TMP_Text t = _w.MakeLabel(cell, r.Label, _fs * 0.52f, TextAlignmentOptions.BottomLeft,
                        new Vector2(0f, 0.12f), new Vector2(1f, 1f));
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    Image line = Widgets.MakeImage(cell, new Color(1f, 1f, 1f, 0.22f), new Vector2(0f, 0f), new Vector2(1f, 0.06f));
                    line.raycastTarget = false;
                    break;
                }

                case RowKind.Label:
                {
                    TMP_Text t = _w.MakeLabel(cell, r.Label, _fs * 0.38f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one);
                    t.color = new Color(1f, 1f, 1f, 0.65f);
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    break;
                }

                case RowKind.Toggle:
                {
                    RowLabel(cell, r.Label, 0.80f);
                    _w.MakeToggle(cell, new Vector2(0.84f, 0f), new Vector2(0.98f, 1f), (bool)r.Get(),
                        v => r.Set(v));
                    break;
                }

                case RowKind.Slider:
                {
                    RowLabel(cell, r.Label, 0.38f);
                    float cur = Mathf.Clamp(Convert.ToSingle(r.Get()), r.Min, r.Max);
                    TMP_InputField box = null;
                    Slider sl = _w.MakeSlider(cell, new Vector2(0.40f, 0f), new Vector2(0.82f, 1f), v =>
                    {
                        r.Set(v);
                        if (box != null) box.SetTextWithoutNotify(Num(v, r.Whole));
                    });
                    sl.minValue = r.Min;
                    sl.maxValue = r.Max;
                    sl.wholeNumbers = r.Whole;
                    sl.SetValueWithoutNotify(cur);
                    box = _w.MakeInput(cell, new Vector2(0.85f, 0.08f), new Vector2(0.98f, 0.92f), Num(cur, r.Whole), s =>
                    {
                        if (TryNum(s, out float v))
                        {
                            v = Mathf.Clamp(v, r.Min, r.Max);
                            r.Set(v);
                            sl.SetValueWithoutNotify(v);
                        }
                        if (box != null) box.SetTextWithoutNotify(Num(Mathf.Clamp(Convert.ToSingle(r.Get()), r.Min, r.Max), r.Whole));
                    });
                    break;
                }

                case RowKind.Cycle:
                {
                    RowLabel(cell, r.Label, 0.44f);
                    string[] opts = r.OptionsF();
                    int idx = Mathf.Clamp((int)r.Get(), 0, opts.Length - 1);
                    Button open = _w.MakeButton(cell, opts[idx], _fs * 0.42f,
                        new Vector2(0.46f, 0.08f), new Vector2(0.98f, 0.92f), () => { });
                    TMP_Text val = open.GetComponentInChildren<TMP_Text>(true);
                    open.onClick.AddListener(() =>
                        Chooser.Open(_w, (RectTransform)open.transform, opts, idx, i =>
                        {
                            idx = i;
                            r.Set(i);
                            if (val != null) val.text = opts[i];
                        }));
                    break;
                }

                case RowKind.Custom:
                {
                    if (r.CustomBuild != null) r.CustomBuild(cell);
                    break;
                }

                case RowKind.Search:
                {
                    TMP_InputField box = _w.MakeInput(cell, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), _filter, s2 => { });
                    if (box != null)
                    {
                        box.onValueChanged.AddListener(v =>
                        {
                            _filter = v ?? "";
                            Relayout();
                        });
                        if (box.placeholder is TMP_Text ph) ph.text = r.Label;
                    }
                    break;
                }

                case RowKind.Input:
                {
                    RowLabel(cell, r.Label, 0.38f);
                    _w.MakeInput(cell, new Vector2(0.40f, 0.08f), new Vector2(0.98f, 0.92f), (string)r.Get(),
                        s => r.Set(s));
                    break;
                }

                case RowKind.Colour:
                {
                    RowLabel(cell, r.Label, 0.64f);
                    Color cur = (Color)r.Get();
                    Image outline = Widgets.MakeImage(cell, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.70f, 0.14f), new Vector2(0.98f, 0.86f));
                    Image swatch = Widgets.MakeImage(outline.transform, cur, Vector2.zero, Vector2.one);
                    var srt = (RectTransform)swatch.transform;
                    srt.offsetMin = new Vector2(3f, 3f);
                    srt.offsetMax = new Vector2(-3f, -3f);
                    Button btn = outline.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() =>
                    {
                        if (_picker == null) return;
                        Color def = r.GetDefault != null ? (Color)r.GetDefault() : (Color)r.Get();
                        _picker.Open(r.Label, (Color)r.Get(), def, c =>
                        {
                            r.Set(c);
                            if (swatch != null) swatch.color = c;
                        });
                    });
                    break;
                }

                case RowKind.Keybind:
                {
                    RowLabel(cell, r.Label, 0.55f);
                    Button btn = _w.MakeButton(cell, BindText(r), _fs * 0.42f, new Vector2(0.58f, 0.08f), new Vector2(0.98f, 0.92f), () => { });
                    TMP_Text bl = btn.GetComponentInChildren<TMP_Text>(true);
                    btn.onClick.AddListener(() => _capture.Begin(r, bl));
                    KeyCode main = BindMain(r);
                    if (main != KeyCode.None && keyUse.TryGetValue(main, out int n) && n > 1 && bl != null)
                        bl.color = new Color(0.95f, 0.75f, 0.30f);
                    break;
                }

                case RowKind.Bar:
                {
                    RowLabel(cell, r.Label, 0.38f);
                    Slider bar = _w.MakeSlider(cell, new Vector2(0.40f, 0f), new Vector2(0.98f, 1f), v => { });
                    bar.interactable = false;
                    if (bar.handleRect != null) bar.handleRect.gameObject.SetActive(false);
                    LiveValue live = cell.gameObject.AddComponent<LiveValue>();
                    live.Source = r.Live;
                    live.name = r.Label;
                    live.Bar = bar;
                    break;
                }

                case RowKind.Readout:
                {
                    RowLabel(cell, r.Label, 0.5f);
                    TMP_Text val = _w.MakeLabel(cell, "", _fs * 0.48f, TextAlignmentOptions.MidlineRight,
                        new Vector2(0.52f, 0f), new Vector2(0.98f, 1f));
                    val.overflowMode = TextOverflowModes.Ellipsis;
                    LiveValue live = cell.gameObject.AddComponent<LiveValue>();
                    live.Source = r.Live;
                    live.name = r.Label;
                    live.Text = val;
                    break;
                }

                case RowKind.Button:
                {
                    _w.MakeButton(cell, r.Label, _fs * 0.45f, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), r.Click);
                    break;
                }

                case RowKind.Buttons:
                {
                    if (r.Multi == null || r.Multi.Length == 0) break;
                    float w = 1f / r.Multi.Length;
                    for (int i = 0; i < r.Multi.Length; i++)
                    {
                        var b = r.Multi[i];
                        _w.MakeButton(cell, b.label, _fs * 0.45f,
                            new Vector2(i * w + 0.01f, 0.08f), new Vector2((i + 1) * w - 0.01f, 0.92f), b.act);
                    }
                    break;
                }
            }
        }

        void RowLabel(RectTransform cell, string text, float right)
        {
            TMP_Text t = _w.MakeLabel(cell, text, _fs * 0.48f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f), new Vector2(right, 1f));
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        static string Num(float v, bool whole)
            => whole ? Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

        static bool TryNum(string s, out float v)
            => float.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        public static string BindText(Row r)
        {
            object v = r.Get();
            if (v is KeyCode kc) return kc == KeyCode.None ? "None" : kc.ToString();
            if (v is KeyboardShortcut ks) return ks.MainKey == KeyCode.None ? "None" : ks.ToString();
            return "None";
        }

        static KeyCode BindMain(Row r)
        {
            object v = r.Get();
            if (v is KeyCode kc) return kc;
            if (v is KeyboardShortcut ks) return ks.MainKey;
            return KeyCode.None;
        }
    }

    internal class LiveValue : MonoBehaviour
    {
        public Func<object> Source;
        public string name;
        public Slider Bar;
        public TMP_Text Text;

        void Update()
        {
            if (Source == null) return;
            object v;
            try { v = Source(); }
            catch (Exception e)
            {
                Trouble.Note("readout '" + name + "' failed, dropping it: " + e.Message);
                Source = null;
                return;
            }
            if (Bar != null && v is float f) Bar.SetValueWithoutNotify(Mathf.Clamp01(f));
            else if (Text != null) Text.text = v == null ? "" : v.ToString();
        }
    }

    internal class KeyCapture : MonoBehaviour
    {
        public RowBuilder Owner;

        Row _row;
        TMP_Text _text;
        int _frame;

        public void Begin(Row r, TMP_Text label)
        {
            Cancel();
            _row = r;
            _text = label;
            _frame = Time.frameCount;
            if (label != null) label.text = "press a key";
        }

        public void Cancel()
        {
            if (_row != null && _text != null) _text.text = RowBuilder.BindText(_row);
            _row = null;
            _text = null;
        }

        void OnDisable() { Cancel(); }

        void OnGUI()
        {
            if (_row == null) return;
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            KeyCode k = e.keyCode;
            if (k == KeyCode.Escape) { Cancel(); e.Use(); return; }
            if (k == KeyCode.Backspace || k == KeyCode.Delete) k = KeyCode.None;

            Row r = _row;
            if (r.KeyOnly)
            {
                r.Set(k);
            }
            else if (k == KeyCode.None)
            {
                r.Set(KeyboardShortcut.Empty);
            }
            else
            {
                var mods = new List<KeyCode>();
                bool isMod = k == KeyCode.LeftControl || k == KeyCode.RightControl
                    || k == KeyCode.LeftShift || k == KeyCode.RightShift
                    || k == KeyCode.LeftAlt || k == KeyCode.RightAlt;
                if (!isMod)
                {
                    if (e.control) mods.Add(KeyCode.LeftControl);
                    if (e.shift) mods.Add(KeyCode.LeftShift);
                    if (e.alt) mods.Add(KeyCode.LeftAlt);
                }
                r.Set(new KeyboardShortcut(k, mods.ToArray()));
            }
            e.Use();
            _row = null;
            _text = null;
            if (Owner != null) Owner.Build();
        }

        void Update()
        {
            if (_row != null && Time.frameCount > _frame + 1 && Input.GetMouseButtonDown(0))
                Cancel();

            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.A))
            {
                GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                TMP_InputField f = sel != null ? sel.GetComponent<TMP_InputField>() : null;
                if (f != null && f.isFocused && sel.transform.IsChildOf(transform)) Widgets.SelectAll(f);
            }
        }
    }
}
