using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Puts the targeting arrow back on the card it is dragged from.
///
/// The arrow is drawn by placing its head and twenty body segments at plain local
/// positions, but the two values it interpolates between are global: the mouse, and the
/// card the drag started on. That only lines up while the arrow's own parent sits at the
/// origin, which it does in vanilla and does not once the interface is narrowed -- every
/// point then picks up the band's inset a second time and the tail swings out past the
/// right-hand edge of the screen.
///
/// Rather than correct each point, the arrow is pinned to the canvas origin as it starts
/// drawing, which makes its local space the global one again. It is pinned there and not
/// in _Ready because Godot readies children before parents, so the narrowing of the
/// combat interface lands after the arrow has already placed itself.
/// </summary>
internal static class TargetingArrowPatch
{
    // Both overloads, named explicitly: Harmony will not guess between them.
    [HarmonyPatch(typeof(NTargetingArrow), "StartDrawingFrom", typeof(Vector2), typeof(bool))]
    [HarmonyPostfix]
    private static void FromPoint(NTargetingArrow __instance) => Pin(__instance);

    [HarmonyPatch(typeof(NTargetingArrow), "StartDrawingFrom", typeof(Control), typeof(bool))]
    [HarmonyPostfix]
    private static void FromControl(NTargetingArrow __instance) => Pin(__instance);

    private static void Pin(NTargetingArrow arrow)
    {
        if (GodotObject.IsInstanceValid(arrow) && arrow.IsInsideTree())
            arrow.GlobalPosition = Vector2.Zero;
    }
}
