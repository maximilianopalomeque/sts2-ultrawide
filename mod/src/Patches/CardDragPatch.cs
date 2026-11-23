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
/// is read relative to its parent -- which is the combat interface, and which this mod
/// has pulled in from the edges. The two measurements stack, and the card sits the
/// width of that inset to the right of the pointer, far enough at 32:9 to land on the
/// end-turn button.
///
/// Corrected after the fact rather than at each call site: the value just written is
/// read back and rebased onto the parent, which needs none of the three formulas
/// repeated here and so cannot drift away from them.
/// </summary>
internal static class CardDragPatch
{
    [HarmonyPatch(typeof(NMouseCardPlay), "LerpToMouse")]
    [HarmonyPostfix]
    private static void Drag(NHandCardHolder cardHolder)
        => Rebase(cardHolder, Mouse(cardHolder));

    [HarmonyPatch(typeof(NCardPlay), "CenterCard")]
    [HarmonyPostfix]
    private static void Centre(NCardPlay __instance) => Rebase(__instance.Holder, null);

    [HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
    [HarmonyPostfix]
    private static void Play(NCardPlay __instance) => Rebase(__instance.Holder, null);

    private static Vector2? Mouse(Node node)
        => node.IsInsideTree() ? node.GetViewport().GetMousePosition() : null;

    /// <summary>
    /// <paramref name="expected"/> is where the patched method would have put the card
    /// if it really did measure against the screen; the centring ones are recognised by
    /// landing on the middle of the canvas instead. Anything else means this call left
    /// the position alone -- TryPlayCard only moves the card for targeted cards -- and
    /// rebasing a value that was already correct would walk the hand off to the left.
    /// </summary>
    private static void Rebase(NHandCardHolder? holder, Vector2? expected)
    {
        if (holder == null || !GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree())
            return;

        if (holder.GetParent() is not Control parent)
            return;

        Vector2 origin = parent.GlobalPosition;
        if (origin.IsZeroApprox())
            return; // nothing above it has been moved, so the two spaces already agree

        if (AccessTools.Field(typeof(NHandCardHolder), "_targetPosition")?.GetValue(holder)
            is not Vector2 target)
            return;

        bool measuredAgainstScreen = expected.HasValue
            ? target.IsEqualApprox(expected.Value)
            : Mathf.Abs(target.X - holder.GetViewportRect().Size.X * 0.5f) < 1f;

        if (!measuredAgainstScreen)
            return;

        holder.SetTargetPosition(target - origin);
    }
}
