using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    public class Window
    {
        internal static readonly List<Window> All = new List<Window>();

        const float HeadH = 76f;
        const float TabsH = 58f;
        const float CornerH = 22f;
        const float Pad = 14f;

        public GameObject Root;
        public RectTransform Body;
        public string Id;
        public bool Resizable = true;
        public Vector2 MinSize = new Vector2(320f, 200f);

        internal bool WantsVisible;

        public bool OpenInMenus;

        readonly List<Graphic> _frameBits = new List<Graphic>();
        readonly Dictionary<Graphic, Material> _mats = new Dictionary<Graphic, Material>();
        readonly Dictionary<Graphic, Color> _cols = new Dictionary<Graphic, Color>();
        bool _frosted = true;
        Color _panelColour = Frost.Panel;
        Color _controlColour = Frost.Control;
        CanvasGroup _group;

        RectTransform _rt;
        TMP_Text _title;
        Button _collapseBtn;
        readonly List<GameObject> _corners = new List<GameObject>();
        RectTransform _tabStrip;
        RowBuilder _rows;
        readonly List<Page> _tabs = new List<Page>();
        readonly List<Image> _tabMarks = new List<Image>();
        int _tab;
        int _wantTab = -1;
        bool _collapsed;
        float _openHeight;

        public static Window Create(string title, float width, float height)
            => Create(title, title, width, height);

        public static Window Create(string id, string title, float width, float height)
        {
            Widgets w = Injector.W;
            if (w == null)
            {
                Plugin.Log.LogWarning("FishyUI.Window before any menu existed, try again once the game has shown one");
                return null;
            }
            RectTransform rt = Widgets.CloneFrame(Hud.Root);
            if (rt == null) return null;

            var win = new Window();
            win.Id = string.IsNullOrEmpty(id) ? title : id;
            win.Root = rt.gameObject;
            win._rt = rt;
            rt.name = "Window_" + win.Id;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            win._openHeight = height;

            foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(true)) win._frameBits.Add(g);
            win._group = rt.gameObject.AddComponent<CanvasGroup>();

            Graphic bg = rt.GetComponent<Graphic>();
            if (bg == null)
            {
                Image img = rt.gameObject.AddComponent<Image>();
                img.color = Color.clear;
                bg = img;
            }
            bg.raycastTarget = true;
            rt.gameObject.AddComponent<WindowFocus>().Owner = win;

            RectTransform head = Widgets.Area(rt, "Heading", new Vector2(0f, 1f), new Vector2(1f, 1f));
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(0f, HeadH);
            Image headBg = head.gameObject.AddComponent<Image>();
            headBg.color = Color.clear;
            head.gameObject.AddComponent<WindowDrag>().Owner = win;
            win._title = w.MakeTitle(head, title, new Vector2(0.12f, 0f), new Vector2(0.88f, 1f));
            win._collapseBtn = w.MakeButton(head, "-", w.Fs * 0.45f, Vector2.zero, Vector2.one, win.ToggleCollapse);
            PinRight(win._collapseBtn, 92f);
            PinRight(w.MakeButton(head, "X", w.Fs * 0.45f, Vector2.zero, Vector2.one, win.Hide), 28f);

            win._tabStrip = Widgets.Area(rt, "Tabs", new Vector2(0f, 1f), new Vector2(1f, 1f));
            win._tabStrip.pivot = new Vector2(0.5f, 1f);
            win._tabStrip.anchoredPosition = new Vector2(0f, -HeadH);
            win._tabStrip.sizeDelta = new Vector2(0f, TabsH);
            win._tabStrip.gameObject.SetActive(false);

            win.Body = Widgets.Area(rt, "Body", Vector2.zero, Vector2.one);
            win.LayoutBody();

            win._tabStrip.SetAsLastSibling();
            head.SetAsLastSibling();
            win._corners.Add(win.MakeCorner(rt, 1, -1));
            win._corners.Add(win.MakeCorner(rt, -1, -1));
            win._corners.Add(win.MakeCorner(rt, 1, 1));
            win._corners.Add(win.MakeCorner(rt, -1, 1));
            win._corners.Add(win.MakeEdge(rt, 1, 0));
            win._corners.Add(win.MakeEdge(rt, -1, 0));
            win._corners.Add(win.MakeEdge(rt, 0, 1));
            win._corners.Add(win.MakeEdge(rt, 0, -1));

            rt.gameObject.SetActive(false);
            All.Add(win);
            WindowStore.Restore(win);
            return win;
        }

        static void PinRight(Button b, float inset)
        {
            if (b == null) return;
            var cell = (RectTransform)b.transform.parent;
            cell.anchorMin = cell.anchorMax = new Vector2(1f, 0.5f);
            cell.pivot = new Vector2(1f, 0.5f);
            cell.sizeDelta = new Vector2(56f, 44f);
            cell.anchoredPosition = new Vector2(-inset, 0f);
        }

        GameObject MakeCorner(RectTransform parent, int sx, int sy)
        {
            var go = new GameObject("Corner", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Vector2 anchor = new Vector2(sx > 0 ? 1f : 0f, sy > 0 ? 1f : 0f);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(CornerH, CornerH);
            rt.anchoredPosition = Vector2.zero;
            Image hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            WindowResize res = go.AddComponent<WindowResize>();
            res.Owner = this;
            res.SignX = sx;
            res.SignY = sy;
            return go;
        }

        GameObject MakeEdge(RectTransform parent, int sx, int sy)
        {
            var go = new GameObject("Edge", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (sx != 0)
            {
                float x = sx > 0 ? 1f : 0f;
                rt.anchorMin = new Vector2(x, 0f);
                rt.anchorMax = new Vector2(x, 1f);
                rt.pivot = new Vector2(x, 0.5f);
                rt.sizeDelta = new Vector2(8f, -CornerH * 2f);
            }
            else
            {
                float y = sy > 0 ? 1f : 0f;
                rt.anchorMin = new Vector2(0f, y);
                rt.anchorMax = new Vector2(1f, y);
                rt.pivot = new Vector2(0.5f, y);
                rt.sizeDelta = new Vector2(-CornerH * 2f, 8f);
            }
            rt.anchoredPosition = Vector2.zero;
            Image hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            WindowResize res = go.AddComponent<WindowResize>();
            res.Owner = this;
            res.SignX = sx;
            res.SignY = sy;
            return go;
        }

        void LayoutBody()
        {
            float top = HeadH + (_tabStrip != null && _tabStrip.gameObject.activeSelf ? TabsH : 0f);
            Body.offsetMin = new Vector2(Pad, Pad);
            Body.offsetMax = new Vector2(-Pad, -top);
        }

        public Page Rows() => Tab(null);

        public Page Tab(string name)
        {
            var page = new Page(name ?? Id);
            _tabs.Add(page);
            if (_rows == null)
            {
                Widgets w = Injector.W;
                RectTransform viewport = Widgets.Area(Body, "Rows", Vector2.zero, Vector2.one);
                var picker = new Picker(w, _rt, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.86f));
                _rows = new RowBuilder(w, viewport, picker);
            }
            if (_tabs.Count == 1) _rows.Page = page;
            if (_tabs.Count > 1) BuildTabs();
            return page;
        }

        public RectTransform ScrollBody()
        {
            RectTransform viewport = Widgets.Area(Body, "Scroll", Vector2.zero, Vector2.one);
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Widgets.Area(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(-20f, 0f);
            ScrollRect sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 60f;
            Injector.W.MakeScrollbar(viewport, sr);
            return content;
        }

        void BuildTabs()
        {
            Widgets w = Injector.W;
            foreach (Transform child in _tabStrip) UnityEngine.Object.Destroy(child.gameObject);
            _tabMarks.Clear();
            _tabStrip.gameObject.SetActive(true);
            float step = 1f / _tabs.Count;
            for (int i = 0; i < _tabs.Count; i++)
            {
                int idx = i;
                RectTransform cell = Widgets.Area(_tabStrip, "Tab",
                    new Vector2(i * step + 0.01f, 0.1f), new Vector2((i + 1) * step - 0.01f, 0.95f));
                Image mark = Widgets.MakeImage(cell, Color.clear, Vector2.zero, Vector2.one);
                mark.raycastTarget = false;
                _tabMarks.Add(mark);
                w.MakeButton(cell, _tabs[i].Title, w.Fs * 0.4f, Vector2.zero, Vector2.one, () => SelectTab(idx));
            }
            LayoutBody();
            if (_wantTab > 0 && _wantTab < _tabs.Count) { int t = _wantTab; _wantTab = -1; SelectTab(t); }
            MarkTabs();
        }

        void SelectTab(int i)
        {
            if (i < 0 || i >= _tabs.Count) return;
            _tab = i;
            if (_rows != null) { _rows.Page = _tabs[i]; _rows.Build(); }
            ApplyFrost();
            MarkTabs();
            WindowStore.Remember(this);
        }

        internal int TabIndex => _tab;

        internal void WantTab(int i) { _wantTab = i; }

        void MarkTabs()
        {
            for (int i = 0; i < _tabMarks.Count; i++)
                if (_tabMarks[i] != null)
                    _tabMarks[i].color = i == _tab ? new Color(1f, 1f, 1f, 0.10f) : Color.clear;
        }

        public void Refresh()
        {
            if (_rows != null) _rows.Build();
            ApplyFrost();
        }

        public void Show()
        {
            if (Root == null) return;
            if (Injector.GameMenuOpen() && !OpenInMenus) return;
            WantsVisible = true;
            Root.SetActive(true);
            Focus();
            ClampToScreen();
            if (_rows != null) _rows.Build();
            ApplyFrost();
        }

        public void Hide()
        {
            WantsVisible = false;
            if (_rows != null) _rows.Sleep();
            if (Root != null) Root.SetActive(false);
        }

        public void Toggle()
        {
            if (Root == null) return;
            if (WantsVisible) Hide();
            else Show();
        }

        public bool Visible => Root != null && WantsVisible;

        public string Title
        {
            set { if (_title != null) _title.text = value; }
        }

        public bool Frosted
        {
            get { return _frosted; }
            set { _frosted = value; ApplyFrost(); }
        }

        public Color PanelColour
        {
            get { return _panelColour; }
            set { _panelColour = value; ApplyFrost(); }
        }

        public Color ControlColour
        {
            get { return _controlColour; }
            set { _controlColour = value; ApplyFrost(); }
        }

        public float Opacity
        {
            get { return _group != null ? _group.alpha : 1f; }
            set { if (_group != null) _group.alpha = Mathf.Clamp01(value); }
        }

        internal void ApplyFrost()
        {
            if (_frosted && _mats.Count == 0) return;
            Frost.Apply(_rt, _frameBits, _frosted, _panelColour, _controlColour, _mats, _cols);
        }

        public void OpenWhenReady()
        {
            WantsVisible = true;
            if (_rows != null) _rows.Build();
            ApplyFrost();
        }

        public void Destroy()
        {
            Hide();
            All.Remove(this);
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
            _rt = null;
        }

        public void Focus()
        {
            if (Root != null) Root.transform.SetAsLastSibling();
        }

        public void Center()
        {
            if (_rt == null) return;
            _rt.anchoredPosition = Vector2.zero;
            WindowStore.Remember(this);
        }

        public void ToggleCollapse() => SetCollapsed(!_collapsed);

        public void SetCollapsed(bool on)
        {
            if (_rt == null) return;
            _collapsed = on;
            if (on) _openHeight = _rt.sizeDelta.y;
            Body.gameObject.SetActive(!on);
            if (_tabStrip != null && _tabs.Count > 1) _tabStrip.gameObject.SetActive(!on);
            foreach (GameObject c in _corners) if (c != null) c.SetActive(!on && Resizable);
            _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, on ? HeadH + 8f : Mathf.Max(_openHeight, MinSize.y));
            TMP_Text lbl = _collapseBtn != null ? _collapseBtn.GetComponentInChildren<TMP_Text>(true) : null;
            if (lbl != null) lbl.text = on ? "+" : "-";
            ClampToScreen();
            WindowStore.Remember(this);
        }

        public bool Collapsed => _collapsed;

        internal void Drag(Vector2 delta)
        {
            if (_rt == null) return;
            _rt.anchoredPosition += delta;
            ClampToScreen();
        }

        internal void Resize(Vector2 delta, int signX, int signY)
        {
            if (_rt == null || !Resizable || _collapsed) return;
            RectTransform canvas = Hud.Root;
            Vector2 size = _rt.sizeDelta + new Vector2(delta.x * signX, delta.y * signY);
            size.x = Mathf.Clamp(size.x, MinSize.x, canvas.rect.width);
            size.y = Mathf.Clamp(size.y, MinSize.y, canvas.rect.height);
            Vector2 grew = size - _rt.sizeDelta;
            _rt.sizeDelta = size;
            _rt.anchoredPosition += new Vector2(grew.x * 0.5f * signX, grew.y * 0.5f * signY);
            _openHeight = size.y;
            ClampToScreen();
        }

        internal void ClampToScreen()
        {
            if (_rt == null) return;
            RectTransform canvas = Hud.Root;
            Vector2 half = _rt.sizeDelta * 0.5f;
            float maxX = Mathf.Max(0f, canvas.rect.width * 0.5f - half.x);
            float maxY = Mathf.Max(0f, canvas.rect.height * 0.5f - half.y);
            Vector2 pos = _rt.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, -maxY, maxY);
            _rt.anchoredPosition = pos;
        }

        internal Rect Placement
        {
            get
            {
                if (_rt == null) return new Rect();
                return new Rect(_rt.anchoredPosition.x, _rt.anchoredPosition.y, _rt.sizeDelta.x, _rt.sizeDelta.y);
            }
        }

        internal void ApplyPlacement(Rect r, bool collapsed)
        {
            if (_rt == null) return;
            if (r.width > 1f && r.height > 1f)
            {
                RectTransform canvas = Hud.Root;
                _rt.sizeDelta = new Vector2(
                    Mathf.Clamp(r.width, MinSize.x, canvas.rect.width),
                    Mathf.Clamp(r.height, MinSize.y, canvas.rect.height));
                _openHeight = _rt.sizeDelta.y;
            }
            _rt.anchoredPosition = new Vector2(r.x, r.y);
            if (collapsed) SetCollapsed(true);
            ClampToScreen();
        }
    }

    internal class WindowFocus : MonoBehaviour, IPointerDownHandler
    {
        public Window Owner;

        public void OnPointerDown(PointerEventData e)
        {
            if (Owner != null) Owner.Focus();
        }
    }

    internal class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public Window Owner;

        public void OnPointerDown(PointerEventData e)
        {
            if (Owner != null) Owner.Focus();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Owner != null && e.clickCount == 2) Owner.ToggleCollapse();
        }

        public void OnDrag(PointerEventData e)
        {
            if (Owner != null) Owner.Drag(e.delta / Hud.Scale);
        }

        public void OnEndDrag(PointerEventData e)
        {
            WindowStore.Remember(Owner);
        }
    }

    internal class WindowResize : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        public Window Owner;
        public int SignX = 1;
        public int SignY = -1;

        public void OnPointerDown(PointerEventData e)
        {
            if (Owner != null) Owner.Focus();
        }

        public void OnDrag(PointerEventData e)
        {
            if (Owner != null) Owner.Resize(e.delta / Hud.Scale, SignX, SignY);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (Owner != null) Owner.Refresh();
            WindowStore.Remember(Owner);
        }
    }

    internal static class WindowStore
    {
        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>();
        static bool _loaded;
        static bool _dirty;

        static string Path => System.IO.Path.Combine(Paths.ConfigPath, "fishyui.windows.cfg");

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(Path)) return;
                foreach (string line in File.ReadAllLines(Path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    Lines[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("could not read saved window spots: " + e.Message); }
        }

        public static void Restore(Window win)
        {
            if (win == null) return;
            Load();
            if (!Lines.TryGetValue(win.Id, out string v)) return;
            string[] p = v.Split(',');
            if (p.Length < 5) return;
            if (!float.TryParse(p[0], out float x)) return;
            if (!float.TryParse(p[1], out float y)) return;
            float.TryParse(p[2], out float w);
            float.TryParse(p[3], out float h);
            bool collapsed = p[4] == "1";
            win.ApplyPlacement(new Rect(x, y, w, h), collapsed);
            if (p.Length > 5 && int.TryParse(p[5], out int tab)) win.WantTab(tab);
        }

        public static void Remember(Window win)
        {
            if (win == null) return;
            Load();
            Rect r = win.Placement;
            Lines[win.Id] = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0},{1:0},{2:0},{3:0},{4},{5}", r.x, r.y, r.width, r.height, win.Collapsed ? "1" : "0", win.TabIndex);
            _dirty = true;
        }

        public static void Save()
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in Lines) sb.AppendLine(kv.Key + " = " + kv.Value);
                File.WriteAllText(Path, sb.ToString());
            }
            catch (Exception e) { Plugin.Log.LogWarning("could not save window spots: " + e.Message); }
        }
    }

    internal class WindowManager : MonoBehaviour
    {
        Vector2 _lastSize;
        float _nextSave;

        void Update()
        {
            RectTransform canvas = Hud.Root;
            Vector2 size = canvas.rect.size;
            if (size != _lastSize)
            {
                _lastSize = size;
                foreach (Window win in Window.All) win.ClampToScreen();
            }
            if (Time.unscaledTime >= _nextSave)
            {
                _nextSave = Time.unscaledTime + 2f;
                WindowStore.Save();
                HudStore.Save();
            }
        }

        void OnDestroy() { WindowStore.Save(); HudStore.Save(); }
        void OnApplicationQuit() { WindowStore.Save(); HudStore.Save(); }

        public static Window Topmost()
        {
            Window best = null;
            int bestIndex = -1;
            foreach (Window win in Window.All)
            {
                if (win.Root == null || !win.Root.activeSelf) continue;
                int i = win.Root.transform.GetSiblingIndex();
                if (i > bestIndex) { bestIndex = i; best = win; }
            }
            return best;
        }
    }
}
