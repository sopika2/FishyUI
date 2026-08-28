# FishyUI

A window library for How to Fish mods. Windows cloned from the game's real widgets
at runtime, so whatever a mod builds with it looks like the base game made it.

```csharp
var win = Window.Create("Weather", 640f, 600f);
win.Rows().Slider("Storm strength", cfgStorm).Colour("Sky", cfgSky);
win.Toggle();
```

The library draws nothing on its own. Players install it because a mod asked for it.

## Documentation

- [Getting started](docs/getting-started.md), from an empty project to a window
- [REFERENCE.md](REFERENCE.md), everything in the library

## Building from source

Needs the .NET SDK and the game installed. Paths to the game and to a Thunderstore
Mod Manager profile sit at the top of `FishyUI.csproj`, point them at yours.

```
dotnet build FishyUI.csproj -c Release
```

The build deploys the dll straight into the profile's plugin folder. The `demo/`
project is a local test mod, never shipped.
