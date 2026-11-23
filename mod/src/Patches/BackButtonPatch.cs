using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Places the back arrow against the narrowed interface, and keeps its hidden position
/// off the screen.
///
/// The arrow anchors itself to x = 0 of the canvas rather than of its parent:
///     _showPos = new Vector2(0f, GetWindow().ContentScaleSize.Y) - _posOffset
/// so on a wide display it ends up against the very edge, well away from everything
/// else, and on some screens half of it hangs off.
///
/// It hides by sliding a further 180 pixels left of that, which used to put it outside
/// the canvas. Once the interface is narrowed that lands back inside the visible area,
/// and you get two arrows at once: the one that is showing and the one that is supposed
/// to be hidden. The hidden position is therefore pushed clear of the real canvas.
///
/// Both are recomputed from vanilla's values on every call rather than nudged, since
/// OnWindowChange rebuilds them each time and an incremental shift would compound.
/// </summary>
[HarmonyPatch(typeof(NBackButton), "OnWindowChange")]
internal static class BackButtonPatch
{
    /// <summary>Extra clearance so the hidden arrow is comfortably past the edge.</summary>
    private const float HideMargin = 80f;

    [HarmonyPostfix]
    private static void Postfix(NBackButton __instance)
    {
        Apply(__instance);

        // Again once the layout has settled. OnWindowChange can run before the narrowed
        // interface above this button has been resized, and the correction depends on
        // where its parent ends up, so the first pass can be computed against a stale
        // position.
        Callable.From(() => Apply(__instance)).CallDeferred();
    }

    private static void Apply(NBackButton button)
    {
        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
            return;

        float inset = HudClampPatches.InsetFor(button);
        if (inset <= 0f)
            return;

        var showField = AccessTools.Field(typeof(NBackButton), "_showPos");
        var hideField = AccessTools.Field(typeof(NBackButton), "_hidePos");
        if (showField?.GetValue(button) is not Vector2 show ||
            hideField?.GetValue(button) is not Vector2 hide)
            return;

        float width = Math.Max(button.Size.X, 200f);

        // These two are global coordinates, despite OnWindowChange assigning them to
        // Position: the show and hide paths both go through GlobalPosition, and in
        // vanilla the distinction never shows because the parents sit at the origin.
        // Vanilla lets the arrow poke a little past the left edge of the canvas; keep
        // that look, but against the narrowed interface instead.
        var shown = new Vector2(inset - 40f, show.Y);
        var hidden = new Vector2(-(width + HideMargin), hide.Y);

        showField.SetValue(button, shown);
        hideField.SetValue(button, hidden);

        var enabled = AccessTools.Field(typeof(NBackButton), "_isEnabled");
        bool isEnabled = enabled?.GetValue(button) is bool b && b;
        button.GlobalPosition = isEnabled ? shown : hidden;
    }
}
