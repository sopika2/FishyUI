using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishyUI
{
    internal enum GameScreen { Overlay, Pause, Options, ServerSettings }

    internal static class Place
    {
        class Spot
        {
            public GameScreen Where;
            public Corner Corner;
            public Vector2 Offset;
            public Vector2 Size;
            public Action<RectTransform> Build;
            public GameObject Live;
            public Transform Parent;

            public bool Inserted;
            public string Neighbour;
            public bool Before;
            public string Label;
            public Action Click;
        }

        public static bool AvoidOverlap = true;

        static readonly List<Spot> Spots = new List<Spot>();

        public static void Button(GameScreen where, Corner corner, Vector2 offset, float width, float height,
            string label, Action onClick)
            => Add(where, corner, offset, new Vector2(width, height), cell => Native.Button(cell, label, onClick));

        public static void Label(GameScreen where, Corner corner, Vector2 offset, float width, float height, string text)
            => Add(where, corner, offset, new Vector2(width, height), cell => Native.Label(cell, text));

        public static void Panel(GameScreen where, Corner corner, Vector2 offset, float width, float height,
            Action<RectTransform> fill = null)
            => Add(where, corner, offset, new Vector2(width, height), cell =>
            {
                RectTransform frame = Widgets.CloneFrame(cell);
                if (frame == null) return;
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
                if (fill != null) fill(frame);
            });

        public static void Custom(GameScreen where, Corner corner, Vector2 offset, float width, float height,
            Action<RectTransform> build)
            => Add(where, corner, offset, new Vector2(width, height), build);

        public static RectTransform CellOn(Transform parent, Corner corner, Vector2 offset, float width, float height)
            => MakeCell(parent, corner, offset, new Vector2(width, height));

        public static void InsertButton(GameScreen where, string neighbour, bool before, string label, Action onClick)
        {
            var spot = new Spot
            {
                Where = where,
                Inserted = true,
                Neighbour = neighbour,
                Before = before,
                Label = label,
                Click = onClick,
            };
            Spots.Add(spot);
            Raise(spot);
        }

        static void Add(GameScreen where, Corner corner, Vector2 offset, Vector2 size, Action<RectTransform> build)
        {
            var spot = new Spot
            {
                Where = where,
                Corner = corner,
                Offset = offset,
                Size = size,
                Build = build,
            };
            Spots.Add(spot);
            Raise(spot);
        }

        internal static void Rebuild()
        {
            foreach (Spot s in Spots) Raise(s);
        }

        static void Raise(Spot s)
        {
            Transform parent = Target(s.Where);
            if (parent == null) return;
            if (s.Live != null && s.Parent == parent) return;
            if (s.Live != null) UnityEngine.Object.Destroy(s.Live);
            s.Live = null;

            if (s.Inserted)
            {
                s.Live = Insert.Into(parent, s.Neighbour, s.Before, s.Label, s.Click);
                if (s.Live != null) s.Parent = parent;
                return;
            }

            RectTransform cell = MakeCell(parent, s.Corner, s.Offset, s.Size);
            if (cell == null) return;
            s.Live = cell.gameObject;
            s.Parent = parent;
            if (AvoidOverlap) Shove(cell, s.Corner);
            try { s.Build(cell); }
            catch (Exception e) { Trouble.Note("something placed on " + s.Where + " failed: " + e.Message); }
        }

        static void Shove(RectTransform cell, Corner corner)
        {
            Vector2 a = HudElement.AnchorOf(corner);
            Vector2 asked = cell.anchoredPosition;
            float step = cell.sizeDelta.y + 10f;
            float dir = a.y > 0.5f ? -1f : 1f;
            var corners = new Vector3[4];
            for (int tries = 0; tries < 3; tries++)
            {
                if (!Taken(cell, corners)) return;
                cell.anchoredPosition += new Vector2(0f, step * dir);
            }
            if (!Taken(cell, corners)) return;
            cell.anchoredPosition = asked;
            Trouble.Note("two mods want the same spot, one of them is sitting on the other");
        }

        static bool Taken(RectTransform cell, Vector3[] corners)
        {
            Rect mine = WorldRect(cell, corners);
            foreach (Transform sib in cell.parent)
            {
                if (sib == cell.transform || !sib.gameObject.activeInHierarchy) continue;
                if (sib.name != "Placed") continue;
                var rt = sib as RectTransform;
                if (rt == null) continue;
                if (mine.Overlaps(WorldRect(rt, corners))) return true;
            }
            return false;
        }

        static Rect WorldRect(RectTransform rt, Vector3[] corners)
        {
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[2].y);
            return new Rect(minX, minY, Mathf.Abs(corners[2].x - corners[0].x), Mathf.Abs(corners[2].y - corners[0].y));
        }

        static RectTransform MakeCell(Transform parent, Corner corner, Vector2 offset, Vector2 size)
        {
            if (parent == null) return null;
            var go = new GameObject("Placed", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Vector2 a = HudElement.AnchorOf(corner);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(
                a.x > 0.5f ? -offset.x : offset.x,
                a.y > 0.5f ? -offset.y : offset.y);
            rt.SetAsLastSibling();
            return rt;
        }

        static Transform Target(GameScreen where)
        {
            if (where == GameScreen.Overlay) return Hud.Root;
            GameObject screen = Injector.ScreenOf(where);
            return screen != null ? screen.transform : null;
        }
    }
}
