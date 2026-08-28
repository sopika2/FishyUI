using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    internal enum Corner { TopLeft, Top, TopRight, Left, Middle, Right, BottomLeft, Bottom, BottomRight }

    internal class HudElement
    {
        internal static readonly List<HudElement> All = new List<HudElement>();

        public GameObject Root;
        public RectTransform Body;
        public string Id;
        public string Title;
        public bool AutoHeight = true;

        internal Corner Where = Corner.TopLeft;
        internal Vector2 Offset;
        internal float Size = 1f;
        internal float Fade = 1f;
        internal bool On = true;
        internal bool Framed = true;

        public bool ShowInMenus;

        RectTransform _rt;
        CanvasGroup _group;
        RowBuilder _rows;
        Page _page;
        int _builtVersion = -1;
        readonly List<Graphic> _frameBits = new List<Graphic>();
        readonly Dictionary<Graphic, Material> _mats = new Dictionary<Graphic, Material>();
        readonly Dictionary<Graphic, Color> _cols = new Dictionary<Graphic, Color>();
        bool _frosted = true;
        Color _panelColour = Frost.Panel;
        Color _controlColour = Frost.Control;

        public static HudElement Create(string id, string title, float width, float height, Corner corner = Corner.TopLeft)
        {
            Widgets w = Injector.W;
            if (w == null)
            {
                Plugin.Log.LogWarning("FishyUI.HudElement before any menu existed, try again once the game has shown one");
                return null;
            }
            RectTransform rt = Widgets.CloneFrame(Hud.Root);
            if (rt == null) return null;

            var el = new HudElement();
            el.Id = string.IsNullOrEmpty(id) ? title : id;
            el.Title = string.IsNullOrEmpty(title) ? el.Id : title;
            el.Root = rt.gameObject;
            el._rt = rt;
            el.Where = corner;
            rt.name = "Hud_" + el.Id;
            rt.sizeDelta = new Vector2(width, height);

            foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(true)) el._frameBits.Add(g);

            el._group = rt.gameObject.AddComponent<CanvasGroup>();
            el._group.blocksRaycasts = false;
            rt.gameObject.AddComponent<HudDrag>().Owner = el;

            el.Body = Widgets.Area(rt, "Body", Vector2.zero, Vector2.one);
            el.Body.offsetMin = new Vector2(12f, 12f);
            el.Body.offsetMax = new Vector2(-12f, -12f);

            All.Add(el);
            HudStore.Restore(el);
            HudPage.Register(el);
            el.Apply();
            return el;
        }

        public Page Rows()
        {
            if (_page == null)
            {
                _page = new Page(Title);
                RectTransform viewport = Widgets.Area(Body, "Rows", Vector2.zero, Vector2.one);
                _rows = new RowBuilder(Injector.W, viewport, null);
                _rows.Page = _page;
            }
            return _page;
        }

        internal void Tick()
        {
            if (_rows == null || _page == null) return;
            if (_page.Version == _builtVersion) return;
            _builtVersion = _page.Version;
            Refresh();
        }

        public void Refresh()
        {
            if (_rows == null) return;
            _rows.Build();
            ApplyFrost();
            if (AutoHeight && _rt != null)
                _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, _rows.ContentHeight + 24f);
            Apply();
        }

        public void Destroy()
        {
            All.Remove(this);
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
            _rt = null;
        }

        public void Show() { On = true; Apply(); HudStore.Remember(this); }
        public void Hide() { On = false; Apply(); HudStore.Remember(this); }
        public bool Visible => On;

        public bool ShowFrame
        {
            get { return Framed; }
            set { Framed = value; Apply(); }
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

        internal void ApplyFrost()
        {
            if (_frosted && _mats.Count == 0) return;
            Frost.Apply(_rt, Framed ? _frameBits : new List<Graphic>(),
                _frosted, _panelColour, _controlColour, _mats, _cols);
        }

        internal void Apply()
        {
            if (_rt == null) return;
            Vector2 a = AnchorOf(Where);
            _rt.anchorMin = _rt.anchorMax = a;
            _rt.pivot = a;
            _rt.anchoredPosition = Offset;
            _rt.localScale = Vector3.one * Mathf.Clamp(Size, 0.4f, 3f);
            bool arranging = HudPage.Arranging;
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp(Fade, 0.1f, 1f);
                _group.blocksRaycasts = arranging;
            }
            foreach (Graphic g in _frameBits)
                if (g != null) g.enabled = Framed || arranging;
            if (Root != null) Root.SetActive(On && (ShowInMenus || !Injector.GameMenuOpen()));
            ClampToScreen();
        }

        internal void Drag(Vector2 delta)
        {
            Offset += delta;
            ClampToScreen();
        }

        void ClampToScreen()
        {
            if (_rt == null) return;
            RectTransform canvas = Hud.Root;
            Vector2 a = AnchorOf(Where);
            Vector2 size = _rt.sizeDelta * Mathf.Clamp(Size, 0.4f, 3f);
            float baseX = (a.x - 0.5f) * canvas.rect.width;
            float baseY = (a.y - 0.5f) * canvas.rect.height;
            float left = baseX + Offset.x - size.x * a.x;
            float bottom = baseY + Offset.y - size.y * a.y;
            float minLeft = -canvas.rect.width * 0.5f;
            float minBottom = -canvas.rect.height * 0.5f;
            Offset += new Vector2(
                Mathf.Clamp(left, minLeft, minLeft + canvas.rect.width - size.x) - left,
                Mathf.Clamp(bottom, minBottom, minBottom + canvas.rect.height - size.y) - bottom);
            _rt.anchoredPosition = Offset;
        }

        internal void ResetPlace()
        {
            Offset = Vector2.zero;
            Size = 1f;
            Fade = 1f;
            Apply();
            HudStore.Remember(this);
        }

        internal static Vector2 AnchorOf(Corner c)
        {
            switch (c)
            {
                case Corner.Top: return new Vector2(0.5f, 1f);
                case Corner.TopRight: return new Vector2(1f, 1f);
                case Corner.Left: return new Vector2(0f, 0.5f);
                case Corner.Middle: return new Vector2(0.5f, 0.5f);
                case Corner.Right: return new Vector2(1f, 0.5f);
                case Corner.BottomLeft: return new Vector2(0f, 0f);
                case Corner.Bottom: return new Vector2(0.5f, 0f);
                case Corner.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0f, 1f);
            }
        }
    }

    internal class HudDrag : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        public HudElement Owner;

        public void OnPointerDown(PointerEventData e)
        {
            if (HudPage.Arranging) HudPage.Last = Owner;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Owner == null || !HudPage.Arranging) return;
            HudPage.Last = Owner;
            Owner.Drag(e.delta / Hud.Scale);
        }

        public void OnEndDrag(PointerEventData e) => HudStore.Remember(Owner);
    }

    internal static class HudPage
    {
        public static bool Arranging;
        public static HudElement Last;
        static Page _page;

        public static void Tick()
        {
            if (!Arranging || Last == null) return;
            float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10f : 1f;
            var move = new Vector2();
            if (Input.GetKey(KeyCode.LeftArrow)) move.x -= step;
            if (Input.GetKey(KeyCode.RightArrow)) move.x += step;
            if (Input.GetKey(KeyCode.UpArrow)) move.y += step;
            if (Input.GetKey(KeyCode.DownArrow)) move.y -= step;
            if (move == Vector2.zero) return;
            Last.Drag(move);
            HudStore.Remember(Last);
        }

        static readonly string[] CornerNames =
        {
            "Top left", "Top", "Top right", "Left", "Middle", "Right",
            "Bottom left", "Bottom", "Bottom right",
        };

        public static void Register(HudElement el)
        {
            if (_page == null)
            {
                _page = Options.Page("HUD");
                _page.Header("Arranging")
                     .Toggle("Move hud pieces", false, on =>
                     {
                         Arranging = on;
                         foreach (HudElement e in HudElement.All) e.Apply();
                         if (on) Toast.Show("Leave the menu, then drag. arrow keys nudge");
                     })
                     .Label("turn this on, close the menu, then drag. arrows nudge, shift is faster");
            }
            _page.Header(el.Title)
                 .Toggle("Show", el.On, v => { el.On = v; el.Apply(); HudStore.Remember(el); })
                 .Slider("Size", el.Size, 0.5f, 2f, v => { el.Size = v; el.Apply(); HudStore.Remember(el); })
                 .Slider("Fade", el.Fade, 0.2f, 1f, v => { el.Fade = v; el.Apply(); HudStore.Remember(el); })
                 .Dropdown("Corner", CornerNames, (int)el.Where, i =>
                 {
                     el.Where = (Corner)i;
                     el.Offset = Vector2.zero;
                     el.Apply();
                     HudStore.Remember(el);
                 })
                 .Button("Put it back", el.ResetPlace);
        }
    }

    internal static class HudStore
    {
        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>();
        static bool _loaded;
        static bool _dirty;

        static string FilePath => Path.Combine(Paths.ConfigPath, "fishyui.hud.cfg");

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    Lines[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("could not read saved hud spots: " + e.Message); }
        }

        public static void Restore(HudElement el)
        {
            if (el == null) return;
            Load();
            if (!Lines.TryGetValue(el.Id, out string v)) return;
            string[] p = v.Split(',');
            if (p.Length < 6) return;
            if (!int.TryParse(p[0], out int corner)) return;
            float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float size);
            float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float fade);
            el.Where = (Corner)Mathf.Clamp(corner, 0, 8);
            el.Offset = new Vector2(x, y);
            if (size > 0.1f) el.Size = size;
            if (fade > 0.05f) el.Fade = fade;
            el.On = p[5] != "0";
        }

        public static void Remember(HudElement el)
        {
            if (el == null) return;
            Load();
            Lines[el.Id] = string.Format(CultureInfo.InvariantCulture, "{0},{1:0},{2:0},{3:0.##},{4:0.##},{5}",
                (int)el.Where, el.Offset.x, el.Offset.y, el.Size, el.Fade, el.On ? "1" : "0");
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
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch (Exception e) { Plugin.Log.LogWarning("could not save hud spots: " + e.Message); }
        }
    }
}
