using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace FishyUI
{
    internal static class AutoPages
    {
        public static void Fill(Page p, ConfigFile cfg)
        {
            var sections = new List<string>();
            var bySection = new Dictionary<string, List<ConfigEntryBase>>();
            foreach (ConfigDefinition def in cfg.Keys)
            {
                ConfigEntryBase e = cfg[def];
                if (e == null) continue;
                string sec = def.Section ?? "";
                if (!bySection.TryGetValue(sec, out List<ConfigEntryBase> list))
                {
                    list = new List<ConfigEntryBase>();
                    bySection[sec] = list;
                    sections.Add(sec);
                }
                list.Add(e);
            }

            bool useHeaders = sections.Count > 1;
            foreach (string sec in sections)
            {
                if (useHeaders) p.Header(string.IsNullOrEmpty(sec) ? "General" : sec);
                foreach (ConfigEntryBase e in bySection[sec]) AddEntry(p, e);
            }
        }

        static void AddEntry(Page p, ConfigEntryBase e)
        {
            int before = p.Rows.Count;
            AddRowFor(p, e);
            string note = e.Description != null ? e.Description.Description : null;
            if (!string.IsNullOrEmpty(note) && p.Rows.Count > before) p.Tip(note);
        }

        static void AddRowFor(Page p, ConfigEntryBase e)
        {
            Type t = e.SettingType;
            string label = e.Definition.Key;

            if (t == typeof(bool)) { p.Toggle(label, (ConfigEntry<bool>)e); return; }
            if (t == typeof(string))
            {
                if (TryListOptions(e, out string[] opts))
                {
                    p.AddRow(new Row
                    {
                        Kind = RowKind.Cycle,
                        Label = label,
                        OptionsF = () => opts,
                        Get = () => Mathf.Max(0, Array.IndexOf(opts, (string)e.BoxedValue)),
                        Set = v => e.BoxedValue = opts[(int)v],
                    });
                }
                else p.Input(label, (ConfigEntry<string>)e);
                return;
            }
            if (t == typeof(float) || t == typeof(int) || t == typeof(double))
            {
                float min = 0f, max = 0f;
                if (TryRange(e, ref min, ref max))
                {
                    if (t == typeof(float)) p.Slider(label, (ConfigEntry<float>)e, min, max);
                    else if (t == typeof(int)) p.Slider(label, (ConfigEntry<int>)e, (int)min, (int)max);
                    else NumberInput(p, label, e, min, max, true);
                }
                else
                {
                    NumberInput(p, label, e, float.MinValue, float.MaxValue, false);
                }
                return;
            }
            if (t.IsEnum)
            {
                string[] names = Enum.GetNames(t);
                Array values = Enum.GetValues(t);
                p.AddRow(new Row
                {
                    Kind = RowKind.Cycle,
                    Label = label,
                    OptionsF = () => names,
                    Get = () => Mathf.Max(0, ((IList)values).IndexOf(e.BoxedValue)),
                    Set = v => e.BoxedValue = values.GetValue((int)v),
                });
                return;
            }
            if (t == typeof(Color)) { p.Colour(label, (ConfigEntry<Color>)e); return; }
            if (t == typeof(KeyboardShortcut)) { p.Keybind(label, (ConfigEntry<KeyboardShortcut>)e); return; }
            if (t == typeof(KeyCode)) { p.Keybind(label, (ConfigEntry<KeyCode>)e); return; }

            Plugin.Log.LogDebug("AutoPage skipped '" + label + "' (" + t.Name + ")");
        }

        static void NumberInput(Page p, string label, ConfigEntryBase e, float min, float max, bool clamp)
        {
            Type t = e.SettingType;
            p.AddRow(new Row
            {
                Kind = RowKind.Input,
                Label = label,
                Get = () => Convert.ToDouble(e.BoxedValue).ToString("0.###", CultureInfo.InvariantCulture),
                Set = v =>
                {
                    string s = ((string)v ?? "").Trim().Replace(',', '.');
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return;
                    if (clamp) d = Math.Min(max, Math.Max(min, d));
                    if (t == typeof(int)) e.BoxedValue = (int)Math.Round(d);
                    else if (t == typeof(float)) e.BoxedValue = (float)d;
                    else e.BoxedValue = d;
                },
            });
        }

        public static bool TryRange(ConfigEntryBase e, ref float min, ref float max)
        {
            AcceptableValueBase av = e.Description != null ? e.Description.AcceptableValues : null;
            if (av is AcceptableValueRange<float> f) { min = f.MinValue; max = f.MaxValue; }
            else if (av is AcceptableValueRange<int> i) { min = i.MinValue; max = i.MaxValue; }
            else if (av is AcceptableValueRange<double> d) { min = (float)d.MinValue; max = (float)d.MaxValue; }
            else return false;
            return max > min;
        }

        static bool TryListOptions(ConfigEntryBase e, out string[] options)
        {
            options = null;
            AcceptableValueBase av = e.Description != null ? e.Description.AcceptableValues : null;
            if (av is AcceptableValueList<string> list && list.AcceptableValues != null && list.AcceptableValues.Length > 0)
            {
                options = list.AcceptableValues;
                return true;
            }
            return false;
        }
    }
}
