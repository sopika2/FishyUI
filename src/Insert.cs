using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Insert
    {
        public static GameObject Into(Transform screen, string neighbour, bool before, string label, Action onClick)
        {
            Button near = Find(screen, neighbour);
            if (near == null)
            {
                Trouble.Note("could not find a button called '" + neighbour + "' to sit next to");
                return null;
            }
            Transform parent = near.transform.parent;
            if (parent == null) return null;

            List<RectTransform> before_ = Buttons(parent);
            Vector2 first = before_.Count > 0 ? before_[0].anchoredPosition : Vector2.zero;
            Vector2 last = before_.Count > 0 ? before_[before_.Count - 1].anchoredPosition : Vector2.zero;

            GameObject clone = UnityEngine.Object.Instantiate(near.gameObject, parent);
            clone.name = "Placed";
            Widgets.StripLocalization(clone);
            Button btn = clone.GetComponent<Button>();
            Widgets.KillPersistent(btn.onClick);
            btn.onClick.AddListener(() =>
            {
                try { if (onClick != null) onClick(); }
                catch (Exception e) { Trouble.Note("a button placed in a menu threw: " + e.Message); }
            });
            foreach (TMP_Text t in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                t.text = Text.Say(label);
                if (t.enableAutoSizing) continue;
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = t.fontSize * 0.5f;
            }

            int index = near.transform.GetSiblingIndex() + (before ? 0 : 1);
            clone.transform.SetSiblingIndex(index);

            if (parent.GetComponent<LayoutGroup>() == null) Spread(parent, first, last);
            return clone;
        }

        static Button Find(Transform screen, string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            foreach (Button b in screen.GetComponentsInChildren<Button>(true))
            {
                foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (string.IsNullOrEmpty(t.text)) continue;
                    if (t.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0) return b;
                }
            }
            return null;
        }

        static List<RectTransform> Buttons(Transform parent)
        {
            var list = new List<RectTransform>();
            foreach (Transform child in parent)
            {
                if (child.GetComponent<Button>() == null) continue;
                var rt = child as RectTransform;
                if (rt != null) list.Add(rt);
            }
            list.Sort((a, b) =>
            {
                int byY = b.anchoredPosition.y.CompareTo(a.anchoredPosition.y);
                return byY != 0 ? byY : a.anchoredPosition.x.CompareTo(b.anchoredPosition.x);
            });
            return list;
        }

        static void Spread(Transform parent, Vector2 first, Vector2 last)
        {
            List<RectTransform> now = Buttons(parent);
            if (now.Count < 2) return;
            bool vertical = Mathf.Abs(first.y - last.y) >= Mathf.Abs(first.x - last.x);
            now.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            for (int i = 0; i < now.Count; i++)
            {
                float t = now.Count == 1 ? 0f : (float)i / (now.Count - 1);
                Vector2 at = Vector2.Lerp(first, last, t);
                now[i].anchoredPosition = vertical
                    ? new Vector2(now[i].anchoredPosition.x, at.y)
                    : new Vector2(at.x, now[i].anchoredPosition.y);
            }
        }
    }
}
