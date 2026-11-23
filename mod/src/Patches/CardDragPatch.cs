using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Puts the card you are playing back under the cursor.
///
/// A card in hand is moved by handing its holder a target position, and three of the
/// places that do so measure against the screen rather than the hand: the drag passes
/// the mouse position straight through, and the two that lift a card clear of the hand
/// use half the viewport width. Those are canvas coordinates, but a holder's position
/// is read relative to its parent -- the combat interface, which this mod has pulled in
/// from the edges. The two measurements stack, and the card sits the width of that
/// inset to the right of the pointer, far enough at 32:9 to land on the end-turn
/// button.
///
/// The value is corrected on its way in rather than rewritten afterwards. Writing a
/// corrected position after the fact does not work: the drag sets the target every
/// frame, so the game and the correction end up as two alternating targets and the card
/// settles halfway between them -- measurably so, at the exact midpoint.
///
/// Two conditions have to hold before a value is touched, and both are needed. The
/// flag narrows it to those three methods; the value check then picks out the one call
/// each of them makes against the screen. Playing a card re-lays out the rest of the
/// hand from inside TryPlayCard, in hand-relative coordinates, and rebasing those too
/// sends the whole hand six hundred units off the left of the screen.
/// </summary>
internal static class CardDragPatch
{
    /// <summary>Set while one of the screen-measured callers is running.</summary>
    [ThreadStatic] private static bool _measuredAgainstScreen;

    [HarmonyPatch(typeof(NMouseCardPlay), "LerpToMouse")]
    [HarmonyPrefix]
    private static void DragBefore() => _measuredAgainstScreen = true;

    [HarmonyPatch(typeof(NMouseCardPlay), "LerpToMouse")]
    [HarmonyPostfix]
    private static void DragAfter() => _measuredAgainstScreen = false;

    [HarmonyPatch(typeof(NCardPlay), "CenterCard")]
    [HarmonyPrefix]
    private static void CentreBefore() => _measuredAgainstScreen = true;

    [HarmonyPatch(typeof(NCardPlay), "CenterCard")]
    [HarmonyPostfix]
    private static void CentreAfter() => _measuredAgainstScreen = false;

    [HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
    [HarmonyPrefix]
    private static void PlayBefore() => _measuredAgainstScreen = true;

    [HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
    [HarmonyPostfix]
    private static void PlayAfter() => _measuredAgainstScreen = false;

    [HarmonyPatch(typeof(NHandCardHolder), "SetTargetPosition")]
    [HarmonyPrefix]
    private static void Rebase(NHandCardHolder __instance, ref Vector2 position)
    {
        if (!_measuredAgainstScreen)
            return;

        if (!GodotObject.IsInstanceValid(__instance) || !__instance.IsInsideTree())
            return;

        if (__instance.GetParent() is not Control parent)
            return;

        if (!MeasuredAgainstScreen(__instance, position))
            return;

        position -= parent.GlobalPosition;
    }

    /// <summary>
    /// Whether this is one of the two values those methods compute from the screen --
    /// the mouse, or the middle of the canvas -- rather than a hand layout position
    /// that happens to pass through while they run.
    /// </summary>
    private static bool MeasuredAgainstScreen(Node holder, Vector2 position)
    {
        Vector2 canvas = holder.GetViewport().GetVisibleRect().Size;
        if (Mathf.Abs(position.X - canvas.X * 0.5f) < 1f)
            return true;

        return position.IsEqualApprox(holder.GetViewport().GetMousePosition());
    }
}
