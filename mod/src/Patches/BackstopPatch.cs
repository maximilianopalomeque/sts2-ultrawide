using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps the dimming behind modal windows covering the whole display.
///
/// Every popup - pause, settings, card inspection - dims the game behind it with a
/// backstop that fills its parent. Once the interface is narrowed that parent no longer
/// reaches the edges, so the middle of the screen dims and two bright strips of
/// undimmed game are left down the sides.
///
/// Patching the container types rather than naming individual nodes means modals the
/// game creates later are covered too, and each one is its own class so a change to any
/// of them cannot take the others with it.
/// </summary>
internal static class ModalBackstopPatch
{
    [HarmonyPatch(typeof(NModalContainer), "_Ready")]
    [HarmonyPostfix]
    private static void Modal(NModalContainer __instance)
        => Defer(__instance, "Backstop");

    [HarmonyPatch(typeof(NInspectCardScreen), "_Ready")]
    [HarmonyPostfix]
    private static void InspectCard(NInspectCardScreen __instance)
        => Defer(__instance, "Backstop");

    /// <summary>
    /// Deferred: the narrowing above this node is applied during the same layout pass,
    /// so reading the parent's position any earlier can give a stale answer.
    /// </summary>
    private static void Defer(Node owner, string child)
    {
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(owner) && owner.IsInsideTree())
                HudClampPatches.SpanFullCanvas(owner.GetNodeOrNull<Control>(child));
        }).CallDeferred();
    }
}
