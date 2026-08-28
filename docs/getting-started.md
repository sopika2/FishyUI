[Documentation index](index.md)

# Getting started

From an empty project to a window in game.

## What you need

- A BepInEx plugin project, the same way you would build any other mod
- FishyUI installed, and `FishyUI.dll` referenced from your project

The dll to reference is the one inside the FishyUI download, or the one your
Thunderstore profile already has under `BepInEx/plugins/FishyUI/`.

```xml
<Reference Include="FishyUI">
  <HintPath>path\to\FishyUI.dll</HintPath>
  <Private>false</Private>
</Reference>
```

`Private=false` matters. The dll is already on the player's machine, your mod
should not ship its own copy.

## A window

Widgets are cloned from the game's own menus and those have to have existed once:

```csharp
using BepInEx;
using BepInEx.Configuration;
using FishyUI;
using UnityEngine;

[BepInPlugin("you.yourmod", "Your Mod", "1.0.0")]
[BepInDependency("com.sopika.fishyui")]
public class Plugin : BaseUnityPlugin
{
    ConfigEntry<KeyboardShortcut> _key;
    Window _win;

    void Awake()
    {
        _key = Config.Bind("Input", "Menu key", new KeyboardShortcut(KeyCode.F9), "");
    }

    void Update()
    {
        if (!_key.Value.IsDown()) return;
        if (_win == null && Native.Ready)
        {
            _win = Window.Create("yourmod.main", "Your Mod", 640f, 560f);
            _win.Rows()
                .Header("Live")
                .Bar("Fuel", () => _fuel)
                .Readout("Caught", () => _caught + " fish")
                .Header("Settings")
                .Toggle("Enabled", cfgEnabled)
                .Slider("Strength", cfgStrength)
                .Button("Do the thing", DoTheThing);
        }
        if (_win != null) _win.Toggle();
    }
}
```

That window drags by its heading, resizes from any corner, remembers where the
player left it, frees the mouse while it is open and closes on Escape.

`Bar` and `Readout` rows fetch their own values every frame, so there is nothing to
update. More than one `Tab("name")` instead of `Rows()` gives the window a tab
strip.

## When the rows are not enough

`Page.Custom` puts a row of your own making inside the row system, and `Native`
hands out the game's widgets for anything else:

```csharp
page.Custom("Rating", 1f, cell =>
{
    var stars = Native.Grid(cell, 5, 60f);
    for (int i = 1; i <= 5; i++)
    {
        int n = i;
        Native.Button(stars.Next(), new string('*', n), () => Rate(n));
    }
});
```

Everything you get back is a real Unity object. The full list is in
[REFERENCE.md](../REFERENCE.md), along with running FishyUI as an optional
dependency.
