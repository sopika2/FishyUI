using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace FishyUI
{
    internal static class Options
    {
        public static Page Page(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) title = "Unnamed";
            Page p = Registry.Find(title);
            if (p == null)
            {
                p = new Page(title.Trim());
                Registry.Add(p);
            }
            return p;
        }

        public static void Remove(string title)
        {
            Page p = Registry.Find(title);
            if (p != null) Registry.Remove(p);
        }

        public static void Remove(Page page) => Registry.Remove(page);

        public static Page AutoPage(ConfigFile config, string title)
        {
            Page p = Page(title);
            if (config != null) AutoPages.Fill(p, config);
            return p;
        }
    }

    public class Page
    {
        internal readonly string Title;
        internal readonly List<Row> Rows = new List<Row>();
        internal int Version;

        internal Page(string title) { Title = title; }

        public Page Search(string hint = "Search")
            => Add(new Row { Kind = RowKind.Search, Label = hint, Height = 0.9f });

        public Page Header(string text)
            => Add(new Row { Kind = RowKind.Header, Label = text, Height = 0.7f });

        public Page Label(string text)
            => Add(new Row { Kind = RowKind.Label, Label = text, Height = 0.6f });

        public Page Space(float rows = 0.5f)
            => Add(new Row { Kind = RowKind.Space, Height = Mathf.Clamp(rows, 0.1f, 3f) });

        public Page Toggle(string label, ConfigEntry<bool> entry)
            => Add(new Row
            {
                Kind = RowKind.Toggle,
                Label = label,
                Get = () => entry.Value,
                Set = v => entry.Value = (bool)v,
            });

        public Page Toggle(string label, bool value, Action<bool> onChanged)
        {
            bool cur = value;
            return Add(new Row
            {
                Kind = RowKind.Toggle,
                Label = label,
                Get = () => cur,
                Set = v => { cur = (bool)v; onChanged?.Invoke(cur); },
            });
        }

        public Page Slider(string label, ConfigEntry<float> entry)
        {
            float min = 0f, max = 1f;
            AutoPages.TryRange(entry, ref min, ref max);
            return Slider(label, entry, min, max);
        }

        public Page Slider(string label, ConfigEntry<float> entry, float min, float max)
            => Add(new Row
            {
                Kind = RowKind.Slider,
                Label = label,
                Min = min,
                Max = max,
                Get = () => entry.Value,
                Set = v => entry.Value = (float)v,
            });

        public Page Slider(string label, ConfigEntry<int> entry)
        {
            float min = 0f, max = 10f;
            AutoPages.TryRange(entry, ref min, ref max);
            return Slider(label, entry, (int)min, (int)max);
        }

        public Page Slider(string label, ConfigEntry<int> entry, int min, int max)
            => Add(new Row
            {
                Kind = RowKind.Slider,
                Label = label,
                Min = min,
                Max = max,
                Whole = true,
                Get = () => (float)entry.Value,
                Set = v => entry.Value = Mathf.RoundToInt((float)v),
            });

        public Page Slider(string label, float value, float min, float max, Action<float> onChanged)
        {
            float cur = value;
            return Add(new Row
            {
                Kind = RowKind.Slider,
                Label = label,
                Min = min,
                Max = max,
                Get = () => cur,
                Set = v => { cur = (float)v; onChanged?.Invoke(cur); },
            });
        }

        public Page Dropdown<T>(string label, ConfigEntry<T> entry) where T : Enum
        {
            string[] names = Enum.GetNames(typeof(T));
            Array values = Enum.GetValues(typeof(T));
            return Add(new Row
            {
                Kind = RowKind.Cycle,
                Label = label,
                OptionsF = () => names,
                Get = () => Mathf.Max(0, ((IList)values).IndexOf(entry.Value)),
                Set = v => entry.Value = (T)values.GetValue((int)v),
            });
        }

        public Page Dropdown(string label, string[] options, int index, Action<int> onChanged)
        {
            if (options == null || options.Length == 0) options = new[] { "-" };
            int cur = Mathf.Clamp(index, 0, options.Length - 1);
            return Add(new Row
            {
                Kind = RowKind.Cycle,
                Label = label,
                OptionsF = () => options,
                Get = () => cur,
                Set = v => { cur = (int)v; onChanged?.Invoke(cur); },
            });
        }

        public Page Input(string label, ConfigEntry<string> entry)
            => Add(new Row
            {
                Kind = RowKind.Input,
                Label = label,
                Get = () => entry.Value,
                Set = v => entry.Value = (string)v,
            });

        public Page Input(string label, string value, Action<string> onChanged)
        {
            string cur = value ?? "";
            return Add(new Row
            {
                Kind = RowKind.Input,
                Label = label,
                Get = () => cur,
                Set = v => { cur = (string)v; onChanged?.Invoke(cur); },
            });
        }

        public Page Colour(string label, ConfigEntry<Color> entry)
            => Add(new Row
            {
                Kind = RowKind.Colour,
                Label = label,
                Get = () => entry.Value,
                Set = v => entry.Value = (Color)v,
                GetDefault = () => entry.DefaultValue,
            });

        public Page Colour(string label, Color value, Action<Color> onChanged)
        {
            Color cur = value;
            Color def = value;
            return Add(new Row
            {
                Kind = RowKind.Colour,
                Label = label,
                Get = () => cur,
                Set = v => { cur = (Color)v; onChanged?.Invoke(cur); },
                GetDefault = () => def,
            });
        }

        public Page Keybind(string label, ConfigEntry<KeyboardShortcut> entry)
            => Add(new Row
            {
                Kind = RowKind.Keybind,
                Label = label,
                Get = () => entry.Value,
                Set = v => entry.Value = (KeyboardShortcut)v,
            });

        public Page Keybind(string label, ConfigEntry<KeyCode> entry)
            => Add(new Row
            {
                Kind = RowKind.Keybind,
                Label = label,
                KeyOnly = true,
                Get = () => entry.Value,
                Set = v => entry.Value = (KeyCode)v,
            });

        public Page Bar(string label, Func<float> value)
            => Add(new Row { Kind = RowKind.Bar, Label = label, Live = () => value() });

        public Page Readout(string label, Func<string> value)
            => Add(new Row { Kind = RowKind.Readout, Label = label, Live = () => value() });

        public Page Button(string label, Action onClick)
            => Add(new Row { Kind = RowKind.Button, Label = label, Click = onClick });

        public Page Buttons(params (string label, Action onClick)[] buttons)
            => Add(new Row { Kind = RowKind.Buttons, Multi = buttons });

        public Page Custom(string label, float heightRows, Action<RectTransform> build)
            => Add(new Row
            {
                Kind = RowKind.Custom,
                Label = label,
                Height = Mathf.Clamp(heightRows, 0.3f, 10f),
                CustomBuild = build,
            });

        public Page Tip(ConfigEntryBase entry)
        {
            string note = entry != null && entry.Description != null ? entry.Description.Description : null;
            return string.IsNullOrEmpty(note) ? this : Tip(note);
        }

        public Page Tip(string text)
        {
            if (Rows.Count > 0) Rows[Rows.Count - 1].Tip = text;
            return this;
        }

        public Page Clear()
        {
            Rows.Clear();
            Version++;
            Registry.MarkDirty();
            return this;
        }

        public int RowCount => Rows.Count;

        internal Page AddRow(Row r) => Add(r);

        Page Add(Row r)
        {
            Rows.Add(r);
            Version++;
            Registry.MarkDirty();
            return this;
        }
    }

    internal enum RowKind { Header, Label, Space, Toggle, Slider, Cycle, Input, Colour, Keybind, Button, Buttons, Bar, Readout, Search, Custom }

    internal class Row
    {
        public RowKind Kind;
        public string Label;
        public Func<object> Get;
        public Action<object> Set;
        public Func<object> GetDefault;
        public Func<object> Live;
        public string Tip;
        public Action<RectTransform> CustomBuild;
        public float Min, Max;
        public bool Whole;
        public bool KeyOnly;
        public Func<string[]> OptionsF;
        public Action Click;
        public (string label, Action act)[] Multi;
        public float Height = 1f;
    }

    internal static class Registry
    {
        static readonly List<Page> Pages = new List<Page>();
        public static bool Dirty;

        public static void Add(Page p)
        {
            Pages.Add(p);
            MarkDirty();
        }

        public static void Remove(Page p)
        {
            if (p == null || !Pages.Remove(p)) return;
            MarkDirty();
        }

        public static Page Find(string title)
        {
            foreach (Page p in Pages)
                if (string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public static List<Page> All => Pages;
        public static int Count => Pages.Count;

        public static void MarkDirty()
        {
            Dirty = true;
            Injector.OnRegistryChanged();
        }

        public static Dictionary<KeyCode, int> KeyUse()
        {
            var use = new Dictionary<KeyCode, int>();
            foreach (Page p in Pages)
                foreach (Row r in p.Rows)
                {
                    if (r.Kind != RowKind.Keybind || r.Get == null) continue;
                    object v;
                    try { v = r.Get(); } catch { continue; }
                    KeyCode k = v is KeyboardShortcut ks ? ks.MainKey : v is KeyCode kc ? kc : KeyCode.None;
                    if (k == KeyCode.None) continue;
                    use.TryGetValue(k, out int n);
                    use[k] = n + 1;
                }
            return use;
        }
    }
}
