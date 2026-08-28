using System.Collections.Generic;

namespace FishyUI
{
    internal static class Trouble
    {
        static readonly List<string> Notes = new List<string>();
        static Page _page;

        public static void Note(string what)
        {
            if (string.IsNullOrEmpty(what) || Notes.Contains(what)) return;
            Notes.Add(what);
            Plugin.Log.LogWarning(what);
        }

        static void Build()
        {
            if (_page == null) _page = Options.Page("Problems");
            _page.Clear();
            _page.Header("Something did not build");
            foreach (string n in Notes) _page.Label(n);
            _page.Space();
            _page.Label("these are logged as well, in the BepInEx log");
        }
    }
}
