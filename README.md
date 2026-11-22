# Slay the Spire 2 — Ultrawide Support

Native support for displays wider than 21:9. Slay the Spire 2 caps its canvas at 21:9 and
pillarboxes anything wider, so a 32:9 monitor plays the game on half its screen. This mod
lifts that cap and makes the artwork, the rooms and the HUD behave at any width.

![Combat at 5120x1440](docs/combat.png)

## What it does

- **Lifts the 21:9 cap.** The canvas is built at the display's real aspect ratio, so a
  5120x1440 screen renders 5120 pixels wide with no black bars.
- **Covers the screen with artwork.** Backgrounds in combat, shops, rest sites, event and
  Ancient scenes, and the main menu are scaled to fill the wider canvas.
- **Keeps the HUD readable.** Health, energy, the hand, the piles and the end-turn button
  stay within a 21:9 band in the middle of the screen rather than scattering to the far
  corners — configurable, including off.
- **Leaves your saves alone.** The game normally moves saves to a separate `modded`
  profile as soon as any mod loads. While the only mods present are cosmetic, this keeps
  using your normal save.

Only the `Auto` aspect ratio setting is affected. Pick any explicit ratio in
*Settings → Graphics* and the game behaves exactly as it does unmodded.

## Multiplayer

**Other players do not need this mod.** It declares `affects_gameplay: false`, and the
game's own handshake only compares gameplay-relevant mods between players — a mismatch in
cosmetic ones is logged and allowed. The mod registers no new cards, relics or models, so
the model-ID hash players exchange is untouched.

## Install

1. Make sure *Settings → Graphics → Aspect Ratio* is set to **Auto**.
2. Copy the `ultrawide` folder into `Slay the Spire 2/mods/`, so you end up with:

   ```
   Slay the Spire 2/mods/ultrawide/ultrawide.dll
   Slay the Spire 2/mods/ultrawide/ultrawide.json
   ```

3. Launch the game. It will ask once whether to load mods — accept, then relaunch.

## Configuration

Everything works out of the box. To tune it, create `ultrawide.json` in the game's user
directory — `%APPDATA%\SlayTheSpire2\ultrawide.json` on Windows — and restart the game.
See [`mod/ultrawide.example.json`](mod/ultrawide.example.json).

| Key | Default | What it does |
| --- | --- | --- |
| `background_vertical_anchor` | `1.0` | Where room backgrounds are anchored vertically when they grow. Widening always costs vertical view; `1.0` takes it all out of the sky and keeps the ground. `0.5` centres, `0.0` keeps the sky. |
| `ancient_vertical_anchor` | `0.5` | Same for event and Ancient illustrations. These are single paintings with the subject in the middle, so they are centred rather than anchored low. |
| `background_scale_multiplier` | `1.06` | Overscan. A few per cent of bleed so no room ends up a sliver short at one edge. |
| `hud_max_ratio` | `2.3888888` | Widest aspect the HUD may spread across. `2.3888888` is 21:9. Set to `0` to let the HUD use the whole display. |

## Building

Requires the [.NET SDK 9](https://dotnet.microsoft.com/download) or newer. The project
references `sts2.dll`, `GodotSharp.dll` and `0Harmony.dll` straight from the game install
and copies none of them.

```sh
cd mod
dotnet build -c Release
```

If the game is not at the default Steam location, pass the path:

```sh
dotnet build -c Release -p:Sts2Dir="D:/Games/Slay the Spire 2"
```

The output is `mod/bin/Release/ultrawide.dll`. Copy it next to `ultrawide.json` in the
game's `mods/ultrawide/` folder.

## How it works

The 21:9 cap is the constant `DisplayVars.maxWideRatio = 2.3888888f`, which the compiler
inlines into every site that uses it — so the constant itself cannot be patched and each
consumer is handled instead. Everything is done with Harmony at runtime; no game file is
modified.

| Patch | What it addresses |
| --- | --- |
| `ContentScalePatch` | Builds the content scale at the display's real ratio instead of clamping to 2580x1080. |
| `BackgroundPatch` | Scales room, shop, rest-site and menu backgrounds to cover the wider canvas. |
| `AncientBgPatch` | Extrapolates the Ancient background layout past its hand-tuned 21:9 setup. |
| `HudClampPatch` | Narrows the combat HUD, the top bar and the map controls to a comfortable width. |
| `ViewportRatioPatch` | Re-bases the buttons that position themselves as a fraction of the viewport. |
| `SavePathPatch` | Keeps the vanilla save directory while only cosmetic mods are loaded. |

Each patch is applied on its own and skipped if it no longer matches the build you are
running, so a change in one part of the game disables that patch rather than the mod.

## Compatibility

Developed against **v0.113.1**. Slay the Spire 2 is in Early Access and its internals move;
if a future build changes one of the methods above, that patch logs a warning and switches
itself off while the rest keeps working. Check the log at
`%APPDATA%\SlayTheSpire2\logs\godot.log` for lines tagged `[Ultrawide]`.

## Credits

Built by maxipalomeque. Slay the Spire 2 is by Mega Crit. This mod ships no game assets.

## License

[MIT](LICENSE)
