using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishyUI
{
    internal static class Frost
    {
        public static readonly Color Panel = new Color(0.09f, 0.14f, 0.22f, 0.96f);
        public static readonly Color Control = new Color(0.17f, 0.25f, 0.37f, 1f);

        public static void Apply(Transform root, List<Graphic> frameBits, bool frosted,
            Color panel, Color control, Dictionary<Graphic, Material> mats, Dictionary<Graphic, Color> cols)
        {
            if (root == null) return;

            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                if (!mats.ContainsKey(g))
                {
                    mats[g] = g.material;
                    cols[g] = g.color;
                }
                g.material = frosted ? mats[g] : null;
                if (frosted) g.color = cols[g];
            }
            if (frosted) return;

            foreach (Graphic g in frameBits)
                if (g != null) g.color = panel;

            foreach (Selectable s in root.GetComponentsInChildren<Selectable>(true))
            {
                Graphic back = BackgroundOf(s);
                if (back != null) back.color = control;
            }
        }

        static Graphic BackgroundOf(Selectable s)
        {
            Slider sl = s as Slider;
            if (sl == null) return s.targetGraphic;
            foreach (Image img in sl.GetComponentsInChildren<Image>(true))
            {
                if (sl.fillRect != null && img.rectTransform.IsChildOf(sl.fillRect)) continue;
                if (sl.handleRect != null && img.rectTransform.IsChildOf(sl.handleRect)) continue;
                return img;
            }
            return null;
        }
    }
}
