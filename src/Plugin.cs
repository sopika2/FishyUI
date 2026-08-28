using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace FishyUI
{
    internal enum Placement { Tabs, RightEdge, LeftEdge }

    [BepInPlugin(Guid, "FishyUI", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sopika.fishyui";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<Placement> PlacementCfg;
        internal static ConfigEntry<bool> EscClosesCfg;
        static Plugin _instance;
        Harmony _harmony;

        void Awake()
        {
            _instance = this;
            Log = Logger;

            var cfg = new ConfigFile(Path.Combine(Paths.ConfigPath, "fishyui.cfg"), true, Info.Metadata);
            PlacementCfg = cfg.Bind("General", "Button placement", Placement.Tabs,
                "Where the Mods entry sits on the options screen. Tabs adds a fifth tab, " +
                "RightEdge and LeftEdge hang a vertical button off the frame. " +
                "Takes effect when the screen is next created.");
            EscClosesCfg = cfg.Bind("General", "Escape closes windows", true,
                "With a mod window open, escape closes it instead of pausing the game.");

            _harmony = new Harmony(Guid);
            try
            {
                var target = AccessTools.Method(typeof(PauseManager), "Awake");
                if (target == null) throw new Exception("PauseManager.Awake not found");
                _harmony.Patch(target, postfix: new HarmonyMethod(typeof(Plugin), nameof(PauseAwake)));
                Log.LogInfo("FishyUI " + Version + " loaded");
            }
            catch (Exception e)
            {
                Log.LogError("patch failed, pages will not appear: " + e);
            }

            try
            {
                var cam = AccessTools.Method(typeof(PlayerCamera), "Update");
                if (cam == null) throw new Exception("PlayerCamera.Update not found");
                _harmony.Patch(cam, prefix: new HarmonyMethod(typeof(Plugin), nameof(CamGate)));
            }
            catch (Exception e)
            {
                Log.LogWarning("camera hold not available: " + e.Message);
            }

            try
            {
                var toggle = AccessTools.Method(typeof(PauseManager), "TogglePause", new Type[0]);
                if (toggle == null) throw new Exception("PauseManager.TogglePause() not found");
                _harmony.Patch(toggle, prefix: new HarmonyMethod(typeof(Plugin), nameof(PauseGate)));
            }
            catch (Exception e)
            {
                Log.LogWarning("escape handling not available: " + e.Message);
            }
        }

        static bool CamGate() => InputGuard.Holds == 0;

        static bool PauseGate()
        {
            if (EscClosesCfg == null || !EscClosesCfg.Value) return true;
            if (InputGuard.Holds == 0) return true;
            Window top = WindowManager.Topmost();
            if (top == null) return true;
            top.Hide();
            return false;
        }

        static void PauseAwake(PauseManager __instance)
        {
            if (_instance != null) _instance.StartCoroutine(DeferInject(__instance));
        }

        static IEnumerator DeferInject(PauseManager pm)
        {
            yield return null;
            if (pm == null) yield break;
            try { Injector.TryInject(pm); }
            catch (Exception e) { Log.LogWarning("inject failed: " + e); }
        }
    }
}
