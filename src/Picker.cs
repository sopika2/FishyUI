using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FishyUI
{
    internal class Picker
    {
        readonly Widgets _w;
        readonly float _fs;
        readonly GameObject _root;

        TMP_Text _title;
        Image _swatch;
        Slider _sR, _sG, _sB, _sA;
        TMP_InputField _hex;
        Image _paletteImage;
        RectTransform _paletteMarker;
        Color[] _paletteColors;
        bool _paletteTried;

        Color _cur;
        Color _def;
        Action<Color> _apply;

        public Picker(Widgets w, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            _w = w;
            _fs = w.Fs;

            RectTransform rt = Widgets.Area(parent, "PickerOverlay", aMin, aMax);
            _root = rt.gameObject;
            Image bg = _root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.1f, 0.97f);
            _root.AddComponent<RectMask2D>();

            _title = _w.MakeLabel(rt, "", _fs * 0.48f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0.02f, 0.87f), new Vector2(0.56f, 1f));
            _title.overflowMode = TextOverflowModes.Ellipsis;

            Image outline = Widgets.MakeImage(rt, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.58f, 0.88f), new Vector2(0.76f, 0.99f));
            _swatch = Widgets.MakeImage(outline.transform, Color.white, Vector2.zero, Vector2.one);
            var srt = (RectTransform)_swatch.transform;
            srt.offsetMin = new Vector2(3f, 3f);
            srt.offsetMax = new Vector2(-3f, -3f);

            _w.MakeButton(rt, "Done", _fs * 0.42f, new Vector2(0.79f, 0.87f), new Vector2(0.98f, 1f), Close);

            RectTransform palCell = Widgets.Area(rt, "Palette", new Vector2(0.02f, 0.18f), new Vector2(0.42f, 0.84f));
            _paletteImage = palCell.gameObject.AddComponent<Image>();
            _paletteImage.preserveAspect = true;
            _paletteImage.raycastTarget = true;
            var click = palCell.gameObject.AddComponent<PalettePointer>();
            click.Owner = this;
            RectTransform marker = Widgets.Area(palCell, "Marker", Vector2.zero, Vector2.zero);
            Image mImg = marker.gameObject.AddComponent<Image>();
            mImg.color = Color.white;
            mImg.raycastTarget = false;
            marker.sizeDelta = new Vector2(10f, 10f);
            _paletteMarker = marker;
            marker.gameObject.SetActive(false);

            string[] names = { "R", "G", "B", "A" };
            var made = new Slider[4];
            for (int i = 0; i < 4; i++)
            {
                float top = 0.82f - i * 0.17f;
                _w.MakeLabel(rt, names[i], _fs * 0.45f, TextAlignmentOptions.Midline,
                    new Vector2(0.44f, top - 0.14f), new Vector2(0.5f, top));
                int ch = i;
                made[i] = _w.MakeSlider(rt, new Vector2(0.51f, top - 0.14f), new Vector2(0.98f, top), v => OnChannel(ch, v));
            }
            _sR = made[0]; _sG = made[1]; _sB = made[2]; _sA = made[3];

            _w.MakeLabel(rt, "Hex", _fs * 0.42f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0.44f, 0f), new Vector2(0.53f, 0.13f));
            _hex = _w.MakeInput(rt, new Vector2(0.54f, 0f), new Vector2(0.78f, 0.13f), "FFFFFF", OnHex);
            if (_hex != null) _hex.characterLimit = 8;
            _w.MakeButton(rt, "Default", _fs * 0.4f, new Vector2(0.80f, 0f), new Vector2(0.98f, 0.13f), () => Apply(_def));

            _root.SetActive(false);
        }

        public void Open(string label, Color current, Color def, Action<Color> apply)
        {
            _cur = current;
            _def = def;
            _apply = apply;
            _title.text = "Editing:  " + label;
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
            Refresh();
            EnsurePalette();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            _apply = null;
        }

        void Refresh()
        {
            if (_sR != null) _sR.SetValueWithoutNotify(_cur.r);
            if (_sG != null) _sG.SetValueWithoutNotify(_cur.g);
            if (_sB != null) _sB.SetValueWithoutNotify(_cur.b);
            if (_sA != null) _sA.SetValueWithoutNotify(_cur.a);
            if (_hex != null) _hex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGBA(_cur));
            if (_swatch != null) _swatch.color = _cur;
        }

        void Apply(Color c)
        {
            _cur = c;
            Refresh();
            try { _apply?.Invoke(c); }
            catch (Exception e) { Plugin.Log.LogWarning(e.ToString()); }
        }

        void OnChannel(int ch, float v)
        {
            Color c = _cur;
            if (ch == 0) c.r = v;
            else if (ch == 1) c.g = v;
            else if (ch == 2) c.b = v;
            else c.a = v;
            Apply(c);
        }

        void OnHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) { Refresh(); return; }
            if (ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c))
                Apply(c);
            else
                Refresh();
        }

        void EnsurePalette()
        {
            if (_paletteColors != null || _paletteTried) return;
            _paletteTried = true;
            try
            {
                var picker = Resources.FindObjectsOfTypeAll<ColorPicker>().FirstOrDefault();
                if (picker == null) { HidePalette(); return; }
                Image img = Refl.Get<Image>(picker, typeof(ColorPicker), "_image");
                if (img == null || img.sprite == null)
                    img = picker.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.sprite != null);
                Sprite sprite = img != null ? img.sprite : null;
                if (sprite == null) { HidePalette(); return; }

                Texture2D src = sprite.texture;
                RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var full = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                full.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0);
                full.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Rect texRect = sprite.textureRect;
                _paletteColors = new Color[32 * 32];
                var display = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        int px = (int)(texRect.x + (x + 0.5f) / 32f * texRect.width);
                        int py = (int)(texRect.y + (y + 0.5f) / 32f * texRect.height);
                        Color c = full.GetPixel(px, py);
                        c.a = 1f;
                        _paletteColors[y * 32 + x] = c;
                        display.SetPixel(x, y, c);
                    }
                }
                display.Apply(false);
                UnityEngine.Object.Destroy(full);
                _paletteImage.sprite = Sprite.Create(display, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));
                _paletteImage.color = Color.white;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("palette harvest failed (sliders still work): " + e.Message);
                HidePalette();
            }
        }

        void HidePalette()
        {
            if (_paletteImage != null) _paletteImage.color = new Color(1f, 1f, 1f, 0.06f);
        }

        public void OnPaletteClick(Vector2 localPoint, RectTransform rt)
        {
            if (_paletteColors == null) return;
            Rect r = rt.rect;
            float u = Mathf.Clamp01((localPoint.x - r.x) / r.width);
            float v = Mathf.Clamp01((localPoint.y - r.y) / r.height);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * 32f), 0, 31);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * 32f), 0, 31);
            Color c = _paletteColors[y * 32 + x];
            c.a = _cur.a;
            Apply(c);
            if (_paletteMarker != null)
            {
                _paletteMarker.gameObject.SetActive(true);
                _paletteMarker.anchorMin = _paletteMarker.anchorMax = new Vector2((x + 0.5f) / 32f, (y + 0.5f) / 32f);
                _paletteMarker.anchoredPosition = Vector2.zero;
            }
        }
    }
}
