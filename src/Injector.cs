using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Injector
    {
        static PauseManager _pm;
        static GameObject _screen;
        static GameObject _modsButton;
        static RectTransform _frame;
        static Transform _tabRow;
        static PanelHost _panel;
        internal static Widgets W;
        static readonly List<GameObject> ContentPanels = new List<GameObject>();
        static readonly HashSet<Button> HookedButtons = new HashSet<Button>();

        public static void TryInject(PauseManager pm)
        {
            _pm = pm;
            try { Inject(pm); }
            catch (Exception e) { Plugin.Log.LogWarning("options inject failed: " + e); }
        }

        public static void OnRegistryChanged()
        {
            if (_pm == null || _modsButton != null || Registry.Count == 0) return;
            try { Inject(_pm); }
            catch (Exception e) { Plugin.Log.LogWarning("late options inject failed: " + e); }
        }

        static void Inject(PauseManager pm)
        {
            GameObject screen = Refl.Get<GameObject>(pm, typeof(PauseManager), "_optionsScreen");
            if (screen == null) { Plugin.Log.LogWarning("PauseManager._optionsScreen is null"); return; }
            if (screen == _screen && _modsButton != null) return;
            _screen = screen;
            _modsButton = null;
            _panel = null;
            ContentPanels.Clear();
            HookedButtons.Clear();

            Canvas.ForceUpdateCanvases();

            Transform tabRow = null;
            int bestScore = 0;
            float bestY = float.MinValue;
            foreach (Transform tr in screen.GetComponentsInChildren<Transform>(true))
            {
                int score = 0;
                foreach (Transform child in tr)
                {
                    Button b = child.GetComponent<Button>();
                    if (b != null && SwitchTargets(b, screen.transform, tr).Count > 0) score++;
                }
                if (score < 2) continue;
                float y = tr.position.y;
                if (score > bestScore || (score == bestScore && y > bestY))
                {
                    bestScore = score;
                    bestY = y;
                    tabRow = tr;
                }
            }
            if (tabRow == null) { Plugin.Log.LogWarning("no tab row found in options screen"); return; }
            _tabRow = tabRow;

            var tabs = new List<Button>();
            foreach (Transform child in tabRow)
            {
                Button b = child.GetComponent<Button>();
                if (b != null) tabs.Add(b);
            }

            Button vanillaTab = null;
            foreach (Button b in tabs)
            {
                List<GameObject> targets = SwitchTargets(b, screen.transform, tabRow);
                if (targets.Count == 0) continue;
                vanillaTab = b;
                foreach (GameObject go in targets)
                    if (!ContentPanels.Contains(go)) ContentPanels.Add(go);
            }
            if (ContentPanels.Count == 0) { Plugin.Log.LogWarning("no content panels discovered"); return; }
            Button template = vanillaTab != null ? vanillaTab : tabs[tabs.Count - 1];

            RectTransform frame = null;
            float bestArea = 0f;
            foreach (GameObject p in ContentPanels)
            {
                var rt = p.transform as RectTransform;
                if (rt == null) continue;
                float a = rt.rect.width * rt.rect.height;
                if (a > bestArea) { bestArea = a; frame = rt; }
            }
            if (frame == null) { Plugin.Log.LogWarning("no content frame found"); return; }
            _frame = frame;

            try { W = Widgets.Harvest(template, screen); }
            catch (Exception e) { Plugin.Log.LogWarning("widget donors missing: " + e.Message); W = null; }
            try { Widgets.StockFrame(frame); }
            catch (Exception e) { Plugin.Log.LogWarning("frame donor missing: " + e.Message); }

            Place.Rebuild();

            if (Registry.Count == 0) return;
            if (W == null) return;

            Placement mode = Plugin.PlacementCfg.Value;
            GameObject clone = null;
            GameObject holder = null;
            RowFit fit = null;
            if (mode == Placement.Tabs)
            {
                try { clone = MakeTab(tabRow, tabs, template, out fit); }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("could not add a tab, using the screen edge: " + e.Message);
                    clone = null;
                    fit = null;
                }
                if (clone == null) mode = Placement.RightEdge;
            }
            if (clone == null)
                clone = MakeEdgeButton(screen, frame, template, mode == Placement.LeftEdge, out holder);

            Button btn = clone.GetComponent<Button>();
            Widgets.KillPersistent(btn.onClick);
            btn.onClick.AddListener(OnModsClicked);

            GameObject shell = UnityEngine.Object.Instantiate(frame.gameObject, frame.parent);
            shell.name = "ModsPanel";
            shell.SetActive(false);
            Widgets.StripLocalization(shell);
            foreach (var lg in shell.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(lg);
            var doomed = new List<GameObject>();
            foreach (Transform child in shell.transform)
            {
                bool hasContent =
                    child.GetComponentInChildren<TMP_Text>(true) != null ||
                    child.GetComponentInChildren<Selectable>(true) != null;
                if (hasContent) doomed.Add(child.gameObject);
            }
            foreach (GameObject go in doomed) UnityEngine.Object.DestroyImmediate(go);

            try
            {
                _panel = shell.AddComponent<PanelHost>();
                _panel.Init(W);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("panel build failed, removing the Mods entry: " + e);
                UnityEngine.Object.Destroy(shell);
                UnityEngine.Object.Destroy(clone);
                if (holder != null) UnityEngine.Object.Destroy(holder);
                _panel = null;
                return;
            }

            if (fit != null) FitRow(tabRow, fit);

            _modsButton = clone;
            Plugin.Log.LogInfo($"Mods entry added ({Registry.Count} page(s) registered)");
        }

        class RowFit
        {
            public List<RectTransform> Order;
            public float Left;
            public float Width;
            public float Gap;
        }

        static GameObject MakeTab(Transform tabRow, List<Button> tabs, Button template, out RowFit fit)
        {
            var corners = new Vector3[4];
            float left = float.MaxValue, right = float.MinValue;
            foreach (Button b in tabs)
            {
                ((RectTransform)b.transform).GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = tabRow.InverseTransformPoint(corners[i]);
                    if (local.x < left) left = local.x;
                    if (local.x > right) right = local.x;
                }
            }
            float span = right - left;
            if (span < 100f) throw new Exception("tab row span looks wrong: " + span);

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, tabRow);
            clone.name = "ModsTab";
            Widgets.StripLocalization(clone);
            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Mods";

            var order = new List<RectTransform>();
            foreach (Button b in tabs.OrderBy(t => t.transform.position.x))
                order.Add((RectTransform)b.transform);
            order.Add((RectTransform)clone.transform);

            int n = order.Count;
            float gap = span * 0.02f;
            fit = new RowFit { Order = order, Left = left, Gap = gap, Width = (span - gap * (n - 1)) / n };
            return clone;
        }

        static void FitRow(Transform tabRow, RowFit fit)
        {
            for (int i = 0; i < fit.Order.Count; i++)
            {
                RectTransform rt = fit.Order[i];
                float cx = fit.Left + fit.Width * 0.5f + i * (fit.Width + fit.Gap);
                Vector3 local = tabRow.InverseTransformPoint(rt.position);
                rt.position = tabRow.TransformPoint(new Vector3(cx, local.y, local.z));
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fit.Width);
                foreach (TMP_Text t in rt.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    if (t.enableAutoSizing) continue;
                    t.enableAutoSizing = true;
                    t.fontSizeMax = t.fontSize;
                    t.fontSizeMin = t.fontSize * 0.4f;
                }
            }
        }

        static GameObject MakeEdgeButton(GameObject screen, RectTransform frame, Button template, bool preferLeft, out GameObject holder)
        {
            holder = new GameObject("ModsButtonHolder", typeof(RectTransform));
            var hrt = (RectTransform)holder.transform;
            hrt.SetParent(frame.parent, false);
            hrt.anchorMin = frame.anchorMin;
            hrt.anchorMax = frame.anchorMax;
            hrt.offsetMin = frame.offsetMin;
            hrt.offsetMax = frame.offsetMax;
            hrt.SetAsLastSibling();

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, hrt);
            clone.name = "ModsButton";
            Widgets.StripLocalization(clone);
            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "Mods";
                if (!label.enableAutoSizing)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMax = label.fontSize;
                    label.fontSizeMin = label.fontSize * 0.5f;
                }
            }

            var tRT = (RectTransform)template.transform;
            var brt = (RectTransform)clone.transform;
            float w = Mathf.Clamp(tRT.rect.width, 160f, 300f);
            float h = Mathf.Clamp(tRT.rect.height, 50f, 110f);
            float outOffset = h * 0.5f + 10f;
            brt.anchorMin = brt.anchorMax = new Vector2(preferLeft ? 0f : 1f, EdgeSlot.Preferred);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(w, h);
            brt.localRotation = Quaternion.Euler(0f, 0f, -90f);
            brt.anchoredPosition = new Vector2(preferLeft ? -outOffset : outOffset, 0f);

            EdgeSlot slot = clone.AddComponent<EdgeSlot>();
            slot.Frame = frame;
            slot.Root = screen.transform;
            slot.OutOffset = outOffset;
            slot.PreferLeft = preferLeft;
            return clone;
        }

        static List<GameObject> SwitchTargets(Button b, Transform screen, Transform row)
        {
            var list = new List<GameObject>();
            UnityEvent ev = b.onClick;
            int n = ev.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
            {
                if (ev.GetPersistentMethodName(i) != "SetActive") continue;
                UnityEngine.Object target = ev.GetPersistentTarget(i);
                GameObject go = target as GameObject;
                if (go == null && target is Component comp) go = comp.gameObject;
                if (go == null) continue;
                if (!go.transform.IsChildOf(screen)) continue;
                if (row != null && go.transform.IsChildOf(row)) continue;
                if (!list.Contains(go)) list.Add(go);
            }
            return list;
        }

        static void OnModsClicked()
        {
            if (_panel == null) return;
            foreach (GameObject p in ContentPanels)
                if (p != null) p.SetActive(false);
            HideOtherPanels();
            _panel.Show();
            HookOutsideButtons();
        }

        static void HideOtherPanels()
        {
            if (_panel == null || _frame == null) return;
            Transform parent = _panel.transform.parent;
            if (parent == null) return;
            foreach (Transform child in parent)
            {
                if (child == _panel.transform) continue;
                if (child == _tabRow) continue;
                if (!child.gameObject.activeSelf) continue;
                if (_modsButton != null && _modsButton.transform.IsChildOf(child)) continue;
                var rt = child as RectTransform;
                if (rt == null) continue;
                if (rt.rect.width < _frame.rect.width * 0.5f) continue;
                if (rt.rect.height < _frame.rect.height * 0.5f) continue;
                if (child.GetComponentInChildren<Selectable>(true) == null) continue;
                child.gameObject.SetActive(false);
            }
        }

        static void HookOutsideButtons()
        {
            if (_screen == null || _panel == null) return;
            foreach (Button b in _screen.GetComponentsInChildren<Button>(true))
            {
                if (HookedButtons.Contains(b)) continue;
                if (b.gameObject == _modsButton) continue;
                if (b.transform.IsChildOf(_panel.transform)) continue;
                HookedButtons.Add(b);
                b.onClick.AddListener(HidePanel);
            }
        }

        static void HidePanel()
        {
            if (_panel != null) _panel.Hide();
        }

        internal static Transform PanelRoot => _panel != null ? _panel.transform : null;

        internal static GameObject ScreenOf(GameScreen which)
        {
            if (_pm == null) return null;
            string field = which == GameScreen.Pause ? "_mainScreen"
                : which == GameScreen.Options ? "_optionsScreen"
                : which == GameScreen.ServerSettings ? "_serverSettingsScreen"
                : null;
            return field == null ? null : Refl.Get<GameObject>(_pm, typeof(PauseManager), field);
        }

        internal static bool GameMenuOpen()
        {
            try { if (GameFlags()) return true; }
            catch (Exception e)
            {
                if (!_flagsGone)
                {
                    _flagsGone = true;
                    Plugin.Log.LogWarning("pause flags unavailable, watching the screen instead: " + e.Message);
                }
            }
            return _screen != null && _screen.activeInHierarchy;
        }

        static bool _flagsGone;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool GameFlags() => PauseManager.IsPaused || MainMenuManager.IsInMenu;
    }

    internal class EdgeSlot : MonoBehaviour
    {
        public const float Preferred = 0.62f;

        public RectTransform Frame;
        public Transform Root;
        public float OutOffset;
        public bool PreferLeft;

        bool _done;

        void OnEnable() { _done = false; }

        void LateUpdate()
        {
            if (_done) return;
            _done = true;
            try { Place(); }
            catch (Exception e) { Plugin.Log.LogWarning("edge button placement failed: " + e.Message); }
        }

        void Place()
        {
            if (Frame == null || Root == null) return;
            var rt = (RectTransform)transform;
            var c = new Vector3[4];

            Frame.GetWorldCorners(c);
            float bottom = c[0].y;
            float top = c[1].y;
            float left = c[0].x;
            float right = c[2].x;
            float height = top - bottom;
            if (height <= 1f) return;

            float scale = height / Mathf.Max(1f, Frame.rect.height);
            float halfW = rt.sizeDelta.y * 0.5f * scale;
            float halfH = rt.sizeDelta.x * 0.5f * scale;
            float gap = 12f * scale;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool onLeft = attempt == 0 ? PreferLeft : !PreferLeft;
                float x = onLeft ? left - OutOffset * scale : right + OutOffset * scale;
                float y = bottom + Preferred * height;
                bool fits = true;
                for (int step = 0; step < 30; step++)
                {
                    RectTransform blocker = FirstHit(rt, x, y, halfW + gap, halfH + gap, c);
                    if (blocker == null) break;
                    blocker.GetWorldCorners(c);
                    y = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y) - gap - halfH;
                    if (y - halfH < bottom + 0.03f * height) { fits = false; break; }
                }
                if (!fits) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(onLeft ? 0f : 1f, (y - bottom) / height);
                rt.anchoredPosition = new Vector2(onLeft ? -OutOffset : OutOffset, 0f);
                if (attempt == 1) Plugin.Log.LogInfo("edge was crowded, Mods button moved to the other side");
                return;
            }
            Plugin.Log.LogWarning("no room on either edge for the Mods button, leaving it at the default spot");
        }

        RectTransform FirstHit(RectTransform self, float x, float y, float halfW, float halfH, Vector3[] c)
        {
            Transform panel = Injector.PanelRoot;
            foreach (Selectable s in Root.GetComponentsInChildren<Selectable>(false))
            {
                var ort = s.transform as RectTransform;
                if (ort == null || ort == self) continue;
                if (ort.IsChildOf(self.parent)) continue;
                if (panel != null && ort.IsChildOf(panel)) continue;
                ort.GetWorldCorners(c);
                float oxMin = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x);
                float oxMax = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
                if (x + halfW < oxMin || x - halfW > oxMax) continue;
                float oyMin = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y);
                float oyMax = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
                if (y + halfH < oyMin || y - halfH > oyMax) continue;
                return ort;
            }
            return null;
        }
    }
}
