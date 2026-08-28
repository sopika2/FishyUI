# FishyUI reference

Everything in 0.1.0. The namespace is `FishyUI`, the dependency is:

```csharp
[BepInDependency("com.sopika.fishyui")]
```

The library draws nothing on its own.

## Window

`Window.Create(title, width, height)` or `Window.Create(id, title, width, height)`.
The id is what its position is saved under. It starts hidden and returns `null` if
no menu has existed yet, on the first key press.

| Member | What it does |
| --- | --- |
| `Rows()` | Settings style rows in the window, see below. |
| `Tab(name)` | A named tab of rows. The strip appears once there are two. |
| `ScrollBody()` | A scrolling area to fill with your own widgets instead. |
| `Body` | The content area, if you are placing things yourself. |
| `Root` | The whole window, if you need the object itself. |
| `Id` | What its position is saved under. |
| `Show()` / `Hide()` / `Toggle()` / `Visible` | Opening and closing. |
| `OpenWhenReady()` | Opens as soon as the game allows. |
| `Refresh()` | Rebuild the rows now, for rows added while it is open. |
| `Destroy()` | Take it away for good, its saved position is kept. |
| `Focus()` / `Center()` | Bring to front, or put back in the middle. |
| `Title` | Change the heading. |
| `Resizable`, `MinSize` | Whether and how far it can be resized. |
| `Collapsed`, `SetCollapsed(bool)`, `ToggleCollapse()` | Rolled up to its heading. |
| `OpenInMenus` | Let it open and stay up over the game's own screens. |
| `Frosted` | The game's blur behind the panel. False for a plain one. |
| `PanelColour`, `ControlColour` | The colours used when the blur is off. |
| `Opacity` | How solid the whole window is. |

What a window does by itself: drags by its heading, resizes from any
corner or edge, collapses on a double click, comes to the front when clicked, stays
on screen, remembers its position, size, collapsed state and which tab you left it
on (`BepInEx/config/fishyui.windows.cfg`), frees the mouse and holds the player,
camera and hotkeys while open, closes on Escape instead of pausing, and tucks away
while the game's own screens are up unless `OpenInMenus` says otherwise.

`Frosted = false` pairs with `OpenInMenus`: the game's frosted panels show the world
behind them rather than what is really there, so a blurred window over a menu looks
wrong.

## Rows

`Rows()` and `Tab()` hand back a page. Every method returns it, so calls chain.

| Row | What it shows |
| --- | --- |
| `Header(text)` | A section heading with a line under it. |
| `Label(text)` | A line of text. |
| `Space(rows)` | A gap. |
| `Toggle(label, entry)` or `(label, value, onChanged)` | A checkbox. |
| `Slider(label, entry)` / `(label, entry, min, max)` / `(label, value, min, max, onChanged)` | A slider with a typed value box, `float` or `int`. |
| `Dropdown(label, enumEntry)` or `(label, options, index, onChanged)` | Opens a list, scrolling past eight options. |
| `Input(label, entry)` or `(label, value, onChanged)` | A text box. |
| `Colour(label, entry)` or `(label, value, onChanged)` | Opens a picker fed by the game's own palette. |
| `Keybind(label, entry)` | Click, press a key. Esc cancels, Backspace clears. `KeyboardShortcut` or `KeyCode`. |
| `Button(label, onClick)` / `Buttons(...)` | One button, or several on one row. |
| `Bar(label, () => value)` | A bar that keeps itself up to date. Give back 0 to 1. |
| `Readout(label, () => text)` | A line of text that keeps itself up to date. |
| `Search(hint)` | A box that filters the rows below it by name. |
| `Custom(label, heightRows, build)` | A row of your own making. The cell is yours, and it scrolls, filters and stacks like any other row. |
| `Tip(text)` or `Tip(entry)` | A note on hover for the row above. |
| `Clear()` / `RowCount` | Start over, or count what is there. |

Rows bound to a `ConfigEntry` read and write it directly, so BepInEx saves as usual.
Rows given a callback keep their value themselves and hand it to you on change. A
row of yours that throws is caught and logged, the rest of the window still builds.

## Native

The game's own controls, for `ScrollBody`, `Custom` rows, or anything of your own.
Each fills the parent you pass, so size the parent and the widget follows.

| Call | What it makes |
| --- | --- |
| `Native.Label(parent, text, size)` | A line of text. |
| `Native.Title(parent, text)` | A heading in the game's panel style. |
| `Native.Button(parent, label, onClick)` | A button. |
| `Native.Toggle(parent, value, onChanged)` | A checkbox. |
| `Native.Slider(parent, value, min, max, onChanged)` | A slider. |
| `Native.Input(parent, text, onEndEdit)` | A text box. |
| `Native.Bar(parent, value)` | The game's slider with the handle off, for showing a number. |
| `Native.Icon(parent, sprite)` | A picture. |
| `Native.Tooltip(widget, text)` | A framed note on hover. |
| `Native.Choose(anchor, options, current, pick)` | The dropdown list, under anything of yours. |
| `Native.PickColour(label, current, default, apply)` | The colour picker, over the middle of the screen. |
| `Native.CaptureKey(done)` | The next key pressed, with its held modifiers. |
| `Native.Scroll(parent)` | A scrolling area with the slim bar. |
| `Native.Column(parent, rowHeight)` / `Native.Grid(parent, columns, rowHeight)` | Hand out cells one after another. Call `Next()` for each. |
| `Native.Ready` | False until the game has shown a menu once, which is where the donors come from. |
| `Native.FontSize` | The size the game uses on its options buttons. |

## The player's files

`fishyui.cfg` has one setting that matters here, Escape closes windows.
`fishyui.windows.cfg` holds where windows were left. Plain text, safe to delete.

## Optional dependency

To run with or without FishyUI, keep the calls in their own method and stop it
being inlined, or the missing type takes the whole method with it:

```csharp
[BepInDependency("com.sopika.fishyui", BepInDependency.DependencyFlags.SoftDependency)]

void Awake()
{
    if (Chainloader.PluginInfos.ContainsKey("com.sopika.fishyui")) BuildUI();
}

[MethodImpl(MethodImplOptions.NoInlining)]
void BuildUI() => MakeMyWindow();
```

## Worth knowing

- Everything is cloned from the game's own widgets and kept alive after the menu
  they came from is gone, so building UI mid game is fine.
- Build windows lazily rather than in `Awake`, `Native.Ready` says when.
- Controllers walk between the controls once the player pushes a stick or an arrow.
- When a built in piece is not enough you drop down a layer: rows sit on `Native`, `Native` sits on plain cells,
  and every widget you get back is a real Unity object.
