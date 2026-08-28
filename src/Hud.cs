using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Hud
    {
        static Canvas _canvas;

        public static RectTransform Root
        {
            get
            {
                if (_canvas == null) Build();
                return (RectTransform)_canvas.transform;
            }
        }

        static void Build()
        {
            var go = new GameObject("FishyUIHud", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;

            var scaler = go.AddComponent<CanvasScaler>();
            CanvasScaler game = Resources.FindObjectsOfTypeAll<CanvasScaler>()
                .FirstOrDefault(s => s != scaler && s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize);
            if (game != null)
            {
                scaler.uiScaleMode = game.uiScaleMode;
                scaler.referenceResolution = game.referenceResolution;
                scaler.screenMatchMode = game.screenMatchMode;
                scaler.matchWidthOrHeight = game.matchWidthOrHeight;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<InputGuard>();
            go.AddComponent<WindowManager>();
        }

        public static float Scale
        {
            get
            {
                if (_canvas == null) Build();
                return _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            }
        }

        public static RectTransform Panel(string name, Vector2 anchor, float width, float height)
        {
            RectTransform rt = Widgets.CloneFrame(Root);
            if (rt == null) return null;
            rt.name = name;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }
    }

    internal class InputGuard : MonoBehaviour
    {
        internal static int Holds;

        FieldInfo _field;
        bool _looked;
        bool _setIt;
        bool _menuOpen;
        bool _lastMenu;
        bool _lastArranging;
        bool _cursorSaved;
        CursorLockMode _prevLock;
        bool _prevVisible;

        void Update()
        {
            _menuOpen = Injector.GameMenuOpen();

            int holds = 0;
            bool arranging = HudPage.Arranging;
            if (arranging) holds++;
            if (_menuOpen != _lastMenu || arranging != _lastArranging)
            {
                _lastMenu = _menuOpen;
                _lastArranging = arranging;
                foreach (HudElement el in HudElement.All) el.Apply();
            }
            foreach (HudElement el in HudElement.All) el.Tick();
            HudPage.Tick();
            foreach (Window win in Window.All)
            {
                if (win.Root == null) continue;
                bool show = win.WantsVisible && (!_menuOpen || win.OpenInMenus);
                if (win.Root.activeSelf != show) win.Root.SetActive(show);
                if (show) holds++;
            }
            Holds = holds;

            bool typing = false;
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel.transform.IsChildOf(transform))
            {
                TMP_InputField f = sel.GetComponent<TMP_InputField>();
                typing = f != null && f.isFocused;

                if (typing && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.A))
                    Widgets.SelectAll(sel.GetComponent<TMP_InputField>());
            }
            SetTypingFlag(typing || Holds > 0);
            PadSeed(sel);
        }

        void PadSeed(GameObject selected)
        {
            if (Holds == 0 || selected != null || EventSystem.current == null) return;
            bool moved = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.4f ||
                         Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.4f;
            if (!moved) return;
            foreach (Window win in Window.All)
            {
                if (win.Root == null || !win.Root.activeInHierarchy) continue;
                Selectable first = win.Root.GetComponentInChildren<Selectable>();
                if (first == null) continue;
                EventSystem.current.SetSelectedGameObject(first.gameObject);
                return;
            }
        }

        void LateUpdate()
        {
            if (Holds > 0)
            {
                if (!_cursorSaved)
                {
                    _cursorSaved = true;
                    _prevLock = Cursor.lockState;
                    _prevVisible = Cursor.visible;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (_cursorSaved)
            {
                _cursorSaved = false;
                if (!_menuOpen)
                {
                    Cursor.lockState = _prevLock;
                    Cursor.visible = _prevVisible;
                }
            }
        }

        static FieldInfo FindTypingField()
        {
            FieldInfo f = AccessTools.Field(typeof(ChatManager), "<IsTyping>k__BackingField");
            if (f != null) return f;
            foreach (FieldInfo cand in AccessTools.GetDeclaredFields(typeof(ChatManager)))
                if (cand.FieldType == typeof(bool) &&
                    cand.Name.IndexOf("typing", StringComparison.OrdinalIgnoreCase) >= 0)
                    return cand;
            return null;
        }

        void SetTypingFlag(bool on)
        {
            if (on == _setIt) return;
            if (!_looked)
            {
                _looked = true;
                _field = FindTypingField();
                if (_field == null) Plugin.Log.LogWarning("chat typing flag not found, keys may leak into the game");
            }
            if (_field == null) return;
            try
            {
                object target = null;
                if (!_field.IsStatic)
                {
                    target = Resources.FindObjectsOfTypeAll<ChatManager>().FirstOrDefault();
                    if (target == null) return;
                }
                _field.SetValue(target, on);
                _setIt = on;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("chat typing flag failed: " + e.Message);
                _field = null;
            }
        }
    }
}
