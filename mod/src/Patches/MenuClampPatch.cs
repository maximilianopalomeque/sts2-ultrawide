using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Narrows the menu screens that lay themselves out against the edges of the canvas.
///
/// Most menus centre their content, so they sit correctly however wide the display is.
/// A few do not: character stats, run history, the daily challenge and custom mode put
/// one column against the left edge and another against the right. At 32:9 the two ends
/// drift half a metre apart, and the left column runs under the back arrow -- which the
/// mod has pulled inward to the 21:9 band, where everything else lives.
///
/// Narrowing the screen itself reproduces the layout the artists tuned at 21:9 rather
/// than inventing a new one. Screens whose content is already centred are unaffected,
/// because the band is centred on the canvas too.
///
/// Two kinds are left alone: the ones built around a full-bleed illustration, which have
/// no column layout to fix and would only lose artwork, and anything already inside a
/// narrowed container -- the pause menu and its screens live under the run HUD, and
/// narrowing those twice would squeeze them to half the intended width.
/// </summary>
[HarmonyPatch(typeof(NSubmenuStack), "Push")]
internal static class MenuClampPatch
{
    private static readonly HashSet<string> _fullBleed = new()
    {
        "NCharacterSelectScreen",
        "NBlitzScreen",
        "NDreadScreen",
        "NTimelineScreen",
    };

    /// <summary>
    /// A prefix, so the screen is already the right width before it is shown. Pushing a
    /// screen enables its buttons, and the confirm tick measures itself against both the
    /// canvas and its parent as it comes in -- narrow the parent afterwards and the tick
    /// is carried off the right-hand edge with it.
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(NSubmenu screen)
    {
        if (screen == null || _fullBleed.Contains(screen.GetType().Name))
            return;

        if (!screen.IsInsideTree())
        {
            Callable.From(() => { if (GodotObject.IsInstanceValid(screen)) Prefix(screen); }).CallDeferred();
            return;
        }

        float inset = HudClampPatches.InsetFor(screen);
        if (inset <= 0f)
            return;

        // A stack that is itself narrower than the canvas is already inside something
        // that was narrowed -- the pause menu lives under the run HUD -- and narrowing
        // its screens again would halve them.
        float canvasWidth = screen.GetViewportRect().Size.X;
        if (screen.GetParent() is Control parent && parent.Size.X < canvasWidth - 1f)
            return;

        HudClampPatches.Narrow(screen, inset);
    }
}
