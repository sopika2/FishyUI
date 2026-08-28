using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Chooser
    {
        static GameObject _open;
        static Window _plain;
        static readonly Dictionary<Graphic, Material> Mats = new Dictionary<Graphic, Material>();
        static readonly Dictionary<Graphic, Color> Cols = new Dictionary<Graphic, Color>();

        public static void Close()
        {
            if (_open != null) UnityEngine.Object.Destroy(_open);
            _open = null;
            _plain = null;
            Mats.Clear();
            Cols.Clear();
        }

        public static void Open(Widgets w, RectTransform anchor, string[] options, int current, Action<int> pick)
        {
            Close();
            if (w == null || anchor == null || options == null || options.Length == 0) return;
            Canvas canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = (RectTransform)canvas.transform;

            var backGo = new GameObject("ChooserBack", typeof(RectTransform));
            var back = (RectTransform)backGo.transform;
            back.SetParent(root, false);
            back.anchorMin = Vector2.zero;
            back.anchorMax = Vector2.one;
            back.offsetMin = Vector2.zero;
            back.offsetMax = Vector2.zero;
            Image sheet = backGo.AddComponent<Image>();
            sheet.color = Color.clear;
            Button away = backGo.AddComponent<Button>();
            away.transition = Selectable.Transition.None;
            away.onClick.AddListener(Close);
            back.SetAsLastSibling();
            _open = backGo;

            RectTransform list = Widgets.CloneFrame(back);
            if (list == null) { Close(); return; }
            list.name = "Chooser";

            float rowH = Mathf.Max(42f, w.Fs * 1.25f);
            int shown = Mathf.Min(options.Length, 8);
            float height = shown * rowH + 16f;
            float width = Mathf.Max(240f, anchor.rect.width);

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[0]);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, canvas.worldCamera, out local);
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(0f, 1f);
            list.sizeDelta = new Vector2(width, height);

            float half = root.rect.height * 0.5f;
            if (local.y - height < -half) local.y = Mathf.Min(local.y + height + anchor.rect.height, half);
            float rightEdge = root.rect.width * 0.5f;
            if (local.x + width > rightEdge) local.x = rightEdge - width;
            list.anchoredPosition = local;

            RectTransform view = Widgets.Area(list, "View", Vector2.zero, Vector2.one);
            view.offsetMin = new Vector2(8f, 8f);
            view.offsetMax = new Vector2(-8f, -8f);
            Image catcher = view.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Widgets.Area(view, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, options.Length * rowH);

            if (options.Length > shown)
            {
                ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = view;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = rowH;
                w.MakeScrollbar(view, scroll);
            }

            WindowFocus owner = anchor.GetComponentInParent<WindowFocus>();
            _plain = owner != null && owner.Owner != null && !owner.Owner.Frosted ? owner.Owner : null;

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var cellGo = new GameObject("Option", typeof(RectTransform));
                var cell = (RectTransform)cellGo.transform;
                cell.SetParent(content, false);
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = new Vector2(1f, 1f);
                cell.pivot = new Vector2(0.5f, 1f);
                cell.offsetMin = new Vector2(0f, -(i + 1) * rowH + 4f);
                cell.offsetMax = new Vector2(options.Length > shown ? -16f : 0f, -i * rowH);
                if (idx == current)
                {
                    Image mark = Widgets.MakeImage(cell, new Color(1f, 1f, 1f, 0.10f), Vector2.zero, Vector2.one);
                    mark.raycastTarget = false;
                }
                w.MakeButton(cell, options[i], w.Fs * 0.42f, Vector2.zero, Vector2.one, () =>
                {
                    Close();
                    if (pick != null) pick(idx);
                });
            }

            if (_plain != null)
            {
                var frame = new List<Graphic>();
                foreach (Graphic g in list.GetComponents<Graphic>()) frame.Add(g);
                Frost.Apply(list, frame, false, _plain.PanelColour, _plain.ControlColour, Mats, Cols);
            }
        }
    }
}
