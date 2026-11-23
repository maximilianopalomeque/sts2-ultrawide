using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Brings the confirm tick back on screen, and keeps its hidden position off it.
///
/// The tick measures from the right-hand edge of the canvas:
///     _showPos = NGame.Instance.Size - _posOffset
/// but that value is assigned to Position, which is read relative to the parent. Once
/// the interface is narrowed the parent no longer starts at the left edge of the screen,
/// so the two measurements stack and the button lands several hundred pixels past the
/// right edge - invisible, and with it no way to confirm an upgrade at the forge.
///
/// It hides by sliding a further 180 pixels right, which has the same problem as the
/// back arrow in reverse: that destination has to clear the real canvas, not the band.
///
/// The mirror of this for the back arrow lives in <see cref="BackButtonPatch"/>, which
/// has to write GlobalPosition rather than Position - the two buttons animate through
/// different properties despite being otherwise symmetrical.
/// </summary>
internal static class ConfirmButtonPatch
{
    /// <summary>Extra clearance so the hidden tick is comfortably past the edge.</summary>
    private const float HideMargin = 80f;

    /// <summary>
    /// Corrected as a prefix on show and hide, not just on window changes. The preview
    /// panel this button sits in is positioned when it opens, long after any resize, and
    /// both animations capture the destination the moment they start - so the value has
    /// to be right before vanilla reads it, and recomputed against wherever the panel
    /// has actually ended up.
    /// </summary>
    [HarmonyPatch(typeof(NConfirmButton), "OnEnable")]
    [HarmonyPrefix]
    private static void BeforeShow(NConfirmButton __instance) => Apply(__instance, place: false);

    [HarmonyPatch(typeof(NConfirmButton), "OnDisable")]
    [HarmonyPrefix]
    private static void BeforeHide(NConfirmButton __instance) => Apply(__instance, place: false);

    /// <summary>
    /// Once now and once at the end of the frame. The button recomputes itself on the
    /// same resize signal that re-narrows the interface around it, and the order
    /// between the two is not ours to pick -- measured going the wrong way in both
    /// directions, so the button was always working from the previous ratio's parent
    /// and parking itself somewhere visible. The deferred pass sees the parent where
    /// it finally ended up.
    /// </summary>
    [HarmonyPatch(typeof(NConfirmButton), "OnWindowChange")]
    [HarmonyPostfix]
    private static void AfterWindowChange(NConfirmButton __instance)
    {
        Apply(__instance, place: true);
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(__instance))
                Apply(__instance, place: true);
        }).CallDeferred();
    }

    private static void Apply(NConfirmButton button, bool place)
    {
        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
            return;

        // Not skipped when there is no inset to apply. The ratio can be changed while
        // the game is running, and bailing out at the narrow ratios leaves whatever
        // this method wrote at a wider one in place -- which is how a stray confirm
        // tick ends up sitting in the middle of a fight. With an inset of zero the
        // arithmetic below reproduces the game's own placement anyway.
        float inset = HudClampPatches.InsetFor(button);

        var showField = AccessTools.Field(typeof(NConfirmButton), "_showPos");
        var hideField = AccessTools.Field(typeof(NConfirmButton), "_hidePos");
        if (showField?.GetValue(button) is not Vector2 show ||
            hideField?.GetValue(button) is not Vector2 hide)
            return;

        Vector2 canvas = button.GetViewportRect().Size;
        float parentX = (button.GetParent() as Control)?.GlobalPosition.X ?? 0f;
        float width = Math.Max(button.Size.X, 200f);

        // The inset is taken from _posOffset, which vanilla derives from the button's own
        // layout and we never touch. Deriving it from _showPos instead would read back a
        // value this method had already written, so the tick would march further left on
        // every call.
        float gap = AccessTools.Field(typeof(NConfirmButton), "_posOffset")
            ?.GetValue(button) is Vector2 posOffset ? posOffset.X : canvas.X - show.X;
        var shown = new Vector2(canvas.X - inset - gap - parentX, show.Y);
        var hidden = new Vector2(canvas.X + width + HideMargin - parentX, hide.Y);

        showField.SetValue(button, shown);
        hideField.SetValue(button, hidden);

        if (!place)
            return;

        var enabled = AccessTools.Field(typeof(NConfirmButton), "_isEnabled");
        bool isEnabled = enabled?.GetValue(button) is bool b && b;
        button.Position = isEnabled ? shown : hidden;
    }
}
