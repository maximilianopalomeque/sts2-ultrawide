using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps the draw and discard piles at the corners of the interface.
///
/// Both remember where the scene put them -- `_showPosition = Position` -- and animate
/// between that and a hidden spot just off it. They read that position in _Ready, and
/// Godot readies children before parents, so it is taken before the combat interface
/// above them has been narrowed. Writing it back afterwards is what does the damage:
/// assigning Position to a Control that is anchored rewrites its offsets, so the
/// discard pile, anchored to the right-hand edge, had its offset baked at 1166 instead
/// of the -94 the scene gives it -- exactly the 1260 the canvas is wider than the band
/// -- and spent the fight 536 pixels off the right of the screen.
///
/// The authored offsets are read before the class can overwrite them, and the pair of
/// positions rebuilt from them once the interface has settled. The draw pile is
/// anchored to the left and never moved, but it goes through the same path: the point
/// is that the position comes from the anchors rather than from whatever the node
/// happened to be at.
/// </summary>
internal static class PileButtonPatch
{
    private const string AuthoredMeta = "uw_authored_offset";

    [HarmonyPatch(typeof(NDiscardPileButton), "_Ready")]
    [HarmonyPrefix]
    private static void RememberDiscard(NDiscardPileButton __instance) => Remember(__instance);

    [HarmonyPatch(typeof(NDrawPileButton), "_Ready")]
    [HarmonyPrefix]
    private static void RememberDraw(NDrawPileButton __instance) => Remember(__instance);

    [HarmonyPatch(typeof(NDiscardPileButton), "_Ready")]
    [HarmonyPostfix]
    private static void FixDiscard(NDiscardPileButton __instance)
        => Schedule(__instance, typeof(NDiscardPileButton));

    [HarmonyPatch(typeof(NDrawPileButton), "_Ready")]
    [HarmonyPostfix]
    private static void FixDraw(NDrawPileButton __instance)
        => Schedule(__instance, typeof(NDrawPileButton));

    private static void Remember(Control button)
    {
        if (!button.HasMeta(AuthoredMeta))
            button.SetMeta(AuthoredMeta, new Vector2(button.OffsetLeft, button.OffsetTop));
    }

    /// <summary>
    /// Twice deferred, then again on every resize: the narrowing lands in the layout
    /// pass after this, and the inset changes with the aspect ratio.
    /// </summary>
    private static void Schedule(Control button, System.Type type)
    {
        void Apply()
        {
            if (GodotObject.IsInstanceValid(button) && button.IsInsideTree())
                Rebuild(button, type);
        }

        Callable.From(() => Callable.From(Apply).CallDeferred()).CallDeferred();
        HudClampPatches.FollowWindow(button, Apply);
    }

    private static void Rebuild(Control button, System.Type type)
    {
        if (!button.HasMeta(AuthoredMeta) || button.GetParent() is not Control parent)
            return;

        var showField = AccessTools.Field(type, "_showPosition");
        var hideField = AccessTools.Field(type, "_hidePosition");
        if (showField?.GetValue(button) is not Vector2 show ||
            hideField?.GetValue(button) is not Vector2 hide)
            return;

        // Taken from the pair as they stand, so whichever direction each pile hides in
        // is preserved without repeating the numbers here.
        Vector2 hideDelta = hide - show;

        Vector2 authored = button.GetMeta(AuthoredMeta).AsVector2();
        var anchored = new Vector2(parent.Size.X * button.AnchorLeft + authored.X,
                                   parent.Size.Y * button.AnchorTop + authored.Y);

        if (anchored.IsEqualApprox(show))
            return;

        showField.SetValue(button, anchored);
        hideField.SetValue(button, anchored + hideDelta);

        // The pile is already sliding in when this runs, and that animation captured
        // the old destination -- correcting the fields alone leaves it to finish its
        // journey off the edge of the screen. The slide is stopped and the pile put
        // where it was going, which is where it belongs for the rest of the fight.
        if (AccessTools.Field(typeof(NCombatCardPile), "_positionTween")?.GetValue(button)
            is Tween tween && GodotObject.IsInstanceValid(tween))
            tween.Kill();

        button.Position = anchored;
    }
}
