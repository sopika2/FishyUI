using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Refl
    {
        static readonly Dictionary<string, FieldInfo> Cache = new Dictionary<string, FieldInfo>();

        public static FieldInfo F(Type t, string name)
        {
            string key = t.FullName + "." + name;
            if (!Cache.TryGetValue(key, out FieldInfo fi))
            {
                fi = AccessTools.Field(t, name);
                Cache[key] = fi;
                if (fi == null) Plugin.Log.LogWarning("field not found: " + key);
            }
            return fi;
        }

        public static T Get<T>(object obj, Type t, string name)
        {
            FieldInfo fi = F(t, name);
            if (fi == null) return default;
            object v = fi.GetValue(obj);
            return v is T typed ? typed : default;
        }

        public static T GetS<T>(Type t, string name) => Get<T>(null, t, name);
    }

    internal class Widgets
    {
        public readonly Button BtnDonor;
        public readonly Slider SliderDonor;
        public readonly Toggle ToggleDonor;
        public readonly TMP_InputField InputDonor;
        public readonly TMP_Text LabelDonor;
        public readonly float Fs;

        Widgets(Button btn, Slider slider, Toggle toggle, TMP_InputField input)
        {
            BtnDonor = btn;
            SliderDonor = slider;
            ToggleDonor = toggle;
            InputDonor = input;
            LabelDonor = btn.GetComponentInChildren<TMP_Text>(true);
            Fs = LabelDonor != null ? LabelDonor.fontSize : 36f;
        }

        static Widgets _cached;
        static Transform _holder;
        static GameObject _frameMaster;
        static TMP_Text _titleMaster;

        static Transform Holder()
        {
            if (_holder == null)
            {
                var go = new GameObject("FishyUIDonors");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.SetActive(false);
                _holder = go.transform;
            }
            return _holder;
        }

        public static Widgets Harvest(Button tabTemplate, GameObject screen)
        {
            if (_cached != null && _cached.SliderDonor != null) return _cached;

            ButtonManager bm = Refl.GetS<ButtonManager>(typeof(ButtonManager), "_instance");
            if (bm == null)
            {
                foreach (ButtonManager b in Resources.FindObjectsOfTypeAll<ButtonManager>()) { bm = b; break; }
            }

            Slider slider = null;
            Toggle toggle = null;
            TMP_InputField input = null;
            if (bm != null)
            {
                slider = Refl.Get<Slider>(bm, typeof(ButtonManager), "_masterVolSlider");
                toggle = Refl.Get<Toggle>(bm, typeof(ButtonManager), "_invertXToggle");
                input = Refl.Get<TMP_InputField>(bm, typeof(ButtonManager), "_masterVolInputField");
            }
            if (slider == null) slider = FindDonor<Slider>(screen);
            if (toggle == null) toggle = FindDonor<Toggle>(screen);
            if (input == null) input = FindDonor<TMP_InputField>(screen);
            if (slider == null || toggle == null) throw new Exception("no slider or toggle anywhere to copy from");

            Button btnM = UnityEngine.Object.Instantiate(tabTemplate.gameObject, Holder()).GetComponent<Button>();
            Slider slM = UnityEngine.Object.Instantiate(slider.gameObject, Holder()).GetComponent<Slider>();
            Toggle tgM = UnityEngine.Object.Instantiate(toggle.gameObject, Holder()).GetComponent<Toggle>();
            TMP_InputField inM = input != null
                ? UnityEngine.Object.Instantiate(input.gameObject, Holder()).GetComponent<TMP_InputField>()
                : null;
            _cached = new Widgets(btnM, slM, tgM, inM);
            return _cached;
        }

        static T FindDonor<T>(GameObject screen) where T : Component
        {
            if (screen != null)
            {
                T hit = screen.GetComponentInChildren<T>(true);
                if (hit != null) return hit;
            }
            foreach (T c in Resources.FindObjectsOfTypeAll<T>())
                if (c != null && c.gameObject.scene.IsValid()) return c;
            return null;
        }

        public static void StockFrame(RectTransform frame)
        {
            if (_frameMaster != null || frame == null) return;
            GameObject m = UnityEngine.Object.Instantiate(frame.gameObject, Holder());
            m.name = "FrameMaster";
            StripLocalization(m);

            TMP_Text heading = null;
            foreach (TMP_Text t in m.GetComponentsInChildren<TMP_Text>(true))
            {
                if (heading == null || t.fontSize > heading.fontSize ||
                    (Mathf.Approximately(t.fontSize, heading.fontSize) && t.transform.position.y > heading.transform.position.y))
                    heading = t;
            }
            if (heading != null)
            {
                GameObject tm = UnityEngine.Object.Instantiate(heading.gameObject, Holder());
                tm.name = "TitleMaster";
                StripLocalization(tm);
                _titleMaster = tm.GetComponent<TMP_Text>();
            }
            foreach (var lg in m.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(lg);
            var doomed = new List<GameObject>();
            foreach (Transform child in m.transform)
            {
                bool hasContent =
                    child.GetComponentInChildren<TMP_Text>(true) != null ||
                    child.GetComponentInChildren<Selectable>(true) != null;
                if (hasContent) doomed.Add(child.gameObject);
            }
            foreach (GameObject go in doomed) UnityEngine.Object.DestroyImmediate(go);
            _frameMaster = m;
        }

        public static RectTransform CloneFrame(Transform parent)
        {
            if (_frameMaster == null)
            {
                Plugin.Log.LogWarning("no frame donor yet, the game has to show a menu once first");
                return null;
            }
            GameObject go = UnityEngine.Object.Instantiate(_frameMaster, parent);
            go.name = "Frame";
            go.SetActive(true);
            return (RectTransform)go.transform;
        }

        static void Navigable(Selectable s)
        {
            if (s == null) return;
            Navigation nav = s.navigation;
            nav.mode = Navigation.Mode.Automatic;
            s.navigation = nav;
        }

        public static void StripLocalization(GameObject go)
        {
            foreach (Component comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().FullName.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(comp);
            }
        }

        public static void KillPersistent(UnityEventBase ev)
        {
            if (ev == null) return;
            int n = ev.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
                ev.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        public static RectTransform Area(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image MakeImage(Transform parent, Color c, Vector2 aMin, Vector2 aMax)
        {
            RectTransform cell = Area(parent, "Img", aMin, aMax);
            Image img = cell.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public TMP_Text MakeLabel(Transform parent, string text, float size, TextAlignmentOptions align, Vector2 aMin, Vector2 aMax)
        {
            RectTransform cell = Area(parent, "Label", aMin, aMax);
            TMP_Text t;
            if (LabelDonor != null)
            {
                GameObject go = UnityEngine.Object.Instantiate(LabelDonor.gameObject, cell);
                StripLocalization(go);
                t = go.GetComponent<TMP_Text>();
            }
            else
            {
                var go = new GameObject("Text", typeof(RectTransform));
                go.transform.SetParent(cell, false);
                t = go.AddComponent<TextMeshProUGUI>();
            }
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.alignment = align;
            t.text = Text.Say(text);
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public TMP_Text MakeTitle(Transform parent, string text, Vector2 aMin, Vector2 aMax)
        {
            if (_titleMaster == null)
                return MakeLabel(parent, text, Fs * 0.75f, TextAlignmentOptions.Center, aMin, aMax);

            RectTransform cell = Area(parent, "Title", aMin, aMax);
            GameObject go = UnityEngine.Object.Instantiate(_titleMaster.gameObject, cell);
            StripLocalization(go);
            TMP_Text t = go.GetComponent<TMP_Text>();
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            t.text = Text.Say(text);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (!t.enableAutoSizing)
            {
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = t.fontSize * 0.45f;
            }
            return t;
        }

        public Button MakeButton(Transform parent, string label, float size, Vector2 aMin, Vector2 aMax, Action onClick)
        {
            RectTransform cell = Area(parent, "Btn_" + label, aMin, aMax);
            GameObject go = UnityEngine.Object.Instantiate(BtnDonor.gameObject, cell);
            go.name = "Btn";
            StripLocalization(go);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            Button b = go.GetComponent<Button>();
            KillPersistent(b.onClick);
            b.onClick.AddListener(() => { try { onClick(); } catch (Exception e) { Plugin.Log.LogWarning(e.ToString()); } });
            Navigable(b);
            TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = Text.Say(label);
                t.enableAutoSizing = true;
                t.fontSizeMax = size;
                t.fontSizeMin = size * 0.4f;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return b;
        }

        public Toggle MakeToggle(Transform parent, Vector2 aMin, Vector2 aMax, bool value, Action<bool> onChanged)
        {
            RectTransform cell = Area(parent, "Tgl", aMin, aMax);
            GameObject go = UnityEngine.Object.Instantiate(ToggleDonor.gameObject, cell);
            StripLocalization(go);
            foreach (TMP_Text txt in go.GetComponentsInChildren<TMP_Text>(true))
                UnityEngine.Object.DestroyImmediate(txt.gameObject);
            Toggle t = go.GetComponent<Toggle>();
            t.group = null;
            KillPersistent(t.onValueChanged);
            var rt = (RectTransform)go.transform;
            Vector2 keep = rt.sizeDelta;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = keep;
            rt.localScale = Vector3.one;
            Navigable(t);
            t.SetIsOnWithoutNotify(value);
            t.onValueChanged.AddListener(v => { try { onChanged(v); } catch (Exception e) { Plugin.Log.LogWarning(e.ToString()); } });
            return t;
        }

        public Slider MakeSlider(Transform parent, Vector2 aMin, Vector2 aMax, Action<float> onChanged)
        {
            RectTransform cell = Area(parent, "Sld", aMin, aMax);
            GameObject go = UnityEngine.Object.Instantiate(SliderDonor.gameObject, cell);
            StripLocalization(go);
            Slider sl = go.GetComponent<Slider>();
            KillPersistent(sl.onValueChanged);
            var rt = (RectTransform)go.transform;
            float keepH = rt.sizeDelta.y;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, keepH);
            rt.localScale = Vector3.one;
            Navigable(sl);
            sl.minValue = 0f;
            sl.maxValue = 1f;
            sl.wholeNumbers = false;
            sl.onValueChanged.AddListener(v => { try { onChanged(v); } catch (Exception e) { Plugin.Log.LogWarning(e.ToString()); } });
            return sl;
        }

        public TMP_InputField MakeInput(Transform parent, Vector2 aMin, Vector2 aMax, string text, Action<string> onEndEdit)
        {
            RectTransform cell = Area(parent, "Inp", aMin, aMax);
            if (InputDonor == null) return null;
            GameObject go = UnityEngine.Object.Instantiate(InputDonor.gameObject, cell);
            StripLocalization(go);
            foreach (UIButton ub in go.GetComponentsInChildren<UIButton>(true))
                UnityEngine.Object.DestroyImmediate(ub);
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.localScale = Vector3.one;
            TMP_InputField f = go.GetComponent<TMP_InputField>();
            KillPersistent(f.onEndEdit);
            KillPersistent(f.onValueChanged);
            KillPersistent(f.onSubmit);
            KillPersistent(f.onSelect);
            KillPersistent(f.onDeselect);
            var rt = (RectTransform)go.transform;
            float keepH = rt.sizeDelta.y;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, keepH);
            rt.localScale = Vector3.one;
            f.enabled = false;

            RectTransform area = f.textViewport;
            if (area == null || area == rt)
            {
                var areaGo = new GameObject("Text Area", typeof(RectTransform));
                area = (RectTransform)areaGo.transform;
                area.SetParent(rt, false);
                if (f.textComponent != null) f.textComponent.rectTransform.SetParent(area, false);
                if (f.placeholder != null) f.placeholder.rectTransform.SetParent(area, false);
                f.textViewport = area;
            }
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(10f, 3f);
            area.offsetMax = new Vector2(-10f, -3f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.localScale = Vector3.one;
            if (area.GetComponent<RectMask2D>() == null) area.gameObject.AddComponent<RectMask2D>();
            if (rt.GetComponent<RectMask2D>() == null) rt.gameObject.AddComponent<RectMask2D>();

            foreach (TMP_Text txt in go.GetComponentsInChildren<TMP_Text>(true))
            {
                txt.enableAutoSizing = false;
                txt.fontSize = Fs * 0.42f;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                txt.overflowMode = TextOverflowModes.Overflow;
                var trt = txt.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.localScale = Vector3.one;
            }

            f.contentType = TMP_InputField.ContentType.Standard;
            f.lineType = TMP_InputField.LineType.SingleLine;
            f.richText = false;
            f.characterLimit = 64;
            f.pointSize = Fs * 0.42f;
            f.customCaretColor = true;
            f.selectionColor = new Color(0.35f, 0.55f, 0.95f, 0.5f);
            f.enabled = true;

            Navigable(f);
            f.SetTextWithoutNotify(text ?? "");
            f.onEndEdit.AddListener(v => { try { onEndEdit(v); } catch (Exception e) { Plugin.Log.LogWarning(e.ToString()); } });
            f.onSelect.AddListener(_ => SelectAll(f));
            return f;
        }

        public Scrollbar MakeScrollbar(RectTransform viewport, ScrollRect scroll)
        {
            var barGo = new GameObject("Scrollbar", typeof(RectTransform));
            var bar = (RectTransform)barGo.transform;
            bar.SetParent(viewport, false);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(6f, -8f);
            bar.anchoredPosition = new Vector2(-6f, 0f);
            Image track = barGo.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.07f);

            RectTransform area = Area(bar, "Sliding Area", Vector2.zero, Vector2.one);
            RectTransform handle = Area(area, "Handle", Vector2.zero, Vector2.one);
            Image thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = new Color(1f, 1f, 1f, 0.34f);

            Scrollbar sb = barGo.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = thumb;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return sb;
        }

        public static void SelectAll(TMP_InputField f)
        {
            if (f == null) return;
            f.selectionAnchorPosition = 0;
            f.selectionFocusPosition = f.text.Length;
            f.caretPosition = f.text.Length;
            f.ForceLabelUpdate();
        }
    }

    internal class PalettePointer : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Picker Owner;

        public void OnPointerDown(PointerEventData e) => Send(e);
        public void OnDrag(PointerEventData e) => Send(e);

        void Send(PointerEventData e)
        {
            if (Owner == null) return;
            var rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
                Owner.OnPaletteClick(local, rt);
        }
    }
}
