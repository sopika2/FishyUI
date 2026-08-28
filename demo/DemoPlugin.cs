using BepInEx;
using BepInEx.Configuration;
using FishyUI;
using UnityEngine;

namespace FishyUIDemo
{
    [BepInPlugin("com.sopika.fishyuidemo", "FishyUI Demo", "0.1.0")]
    [BepInDependency(Plugin.Guid)]
    public class DemoPlugin : BaseUnityPlugin
    {
        ConfigEntry<KeyboardShortcut> _menuKey;
        ConfigEntry<KeyboardShortcut> _secondKey;
        ConfigEntry<KeyboardShortcut> _castKey;
        Window _win;
        Window _small;
        float _fuel = 0.75f;
        int _caught = 12;

        void Awake()
        {
            _menuKey = Config.Bind("Input", "Menu key", new KeyboardShortcut(KeyCode.F9), "");
            _secondKey = Config.Bind("Input", "Second window", new KeyboardShortcut(KeyCode.F8), "");
            _castKey = Config.Bind("Input", "Cast key", new KeyboardShortcut(KeyCode.F6), "");
            Logger.LogInfo("demo windows on " + _menuKey.Value + " and " + _secondKey.Value);
        }

        void Update()
        {
            _fuel = Mathf.PingPong(Time.time * 0.05f, 1f);

            if (_menuKey.Value.IsDown())
            {
                if (_win == null) BuildWindow();
                if (_win != null) _win.Toggle();
            }
            if (_secondKey.Value.IsDown())
            {
                if (_small == null) BuildSecondWindow();
                if (_small != null) _small.Toggle();
            }
        }

        void BuildWindow()
        {
            _win = Window.Create("demo.helper", "Fishing Helper", 720f, 640f);
            if (_win == null) return;

            _win.Tab("Rod")
                .Header("Casting")
                .Slider("Reel speed", 1.2f, 0.1f, 3f, v => Logger.LogInfo("reel " + v.ToString("0.00")))
                .Slider("Line length", 45f, 10f, 99f, v => Logger.LogInfo("line " + v.ToString("0")))
                .Toggle("Auto recast", true, v => Logger.LogInfo("auto recast " + v))
                .Tip("throws the line straight back out after a catch")
                .Dropdown("Bait", new[] { "Worm", "Shrimp", "Squid", "Neon lure", "Bread", "Corn", "Maggot", "Prawn", "Sardine", "Glow stick", "Cheese" }, 1, i => Logger.LogInfo("bait " + i))
                .Keybind("Cast key", _castKey)
                .Header("Right now")
                .Bar("Fuel", () => _fuel)
                .Readout("Caught today", () => _caught + " fish")
                .Readout("Clock", () => System.DateTime.Now.ToString("HH:mm:ss"));

            _win.Tab("Boat")
                .Header("Paint")
                .Colour("Hull", new Color(0.55f, 0.75f, 0.95f), c => Logger.LogInfo("hull #" + ColorUtility.ToHtmlStringRGBA(c)))
                .Colour("Trim", Color.white, c => Logger.LogInfo("trim #" + ColorUtility.ToHtmlStringRGBA(c)))
                .Space()
                .Header("Engine")
                .Slider("Top speed", 0.6f, 0f, 1f, v => Logger.LogInfo("speed " + v.ToString("0.00")))
                .Bar("Engine heat", () => Mathf.PingPong(Time.time * 0.12f, 1f));

            _win.Tab("Log")
                .Search("Find a row")
                .Header("Notes")
                .Input("Note to self", "the big one got away", s => Logger.LogInfo("note " + s))
                .Buttons(("Cast", () => Logger.LogInfo("cast")), ("Reel", () => { _caught++; Logger.LogInfo("reel"); }))
                .Custom("Rating", 1f, cell =>
                {
                    Native.Stack stars = Native.Grid(cell, 5, 60f);
                    for (int i = 1; i <= 5; i++)
                    {
                        int n = i;
                        Native.Button(stars.Next(), new string('*', n), () => Logger.LogInfo("rated " + n));
                    }
                })
                .Label("drag the heading, pull any corner or edge, double click to roll up");
        }

        void BuildSecondWindow()
        {
            _small = Window.Create("demo.catch", "Catch Log", 460f, 380f);
            if (_small == null) return;
            _small.MinSize = new Vector2(300f, 220f);
            _small.OpenInMenus = true;
            _small.Frosted = false;
            _small.Rows()
                .Header("Session")
                .Readout("Fish", () => _caught.ToString())
                .Readout("Fuel", () => Mathf.RoundToInt(_fuel * 100f) + "%")
                .Bar("Fuel", () => _fuel)
                .Space()
                .Button("Add one", () => { _caught++; Logger.LogInfo("added one"); });
        }
    }
}
