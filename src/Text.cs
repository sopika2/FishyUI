using System;

namespace FishyUI
{
    internal static class Text
    {
        public static Func<string, string> Translate;

        internal static string Say(string text)
        {
            if (Translate == null || string.IsNullOrEmpty(text)) return text;
            try { return Translate(text) ?? text; }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("translation failed, using the original: " + e.Message);
                Translate = null;
                return text;
            }
        }
    }
}
