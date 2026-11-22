using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps the HUD within a comfortable width while the artwork uses the whole screen.
/// At 32:9 the end-turn button and the energy orb sit nearly four thousand pixels apart,
/// which is a neck exercise rather than a game.
///
/// The combat HUD is narrowed at its container rather than widget by widget. Every piece
/// inside is already positioned relative to that container - anchored to its edges, or
/// assigned a position in code - so one change to the parent moves all of them correctly
/// and keeps working through every later layout pass the game runs. Moving the widgets
/// individually does not survive: the game re-asserts their positions afterwards, and for
/// some it does so by steering anchors rather than offsets, which quietly undoes any
/// offset written from out here.
///
/// Full-screen pieces - the map, the overlay stack, their dimming backdrops - keep the
/// whole display: an overlay that fails to cover the screen looks broken, not comfortable.
/// </summary>
internal static class HudClampPatches
{
    /// <summary>Offsets as authored, so repeated calls stay idempotent.</summary>
    private static readonly ConditionalWeakTable<Control, Origin> _origins = new();

    private sealed class Origin
    {
        public float Left;
        public float Right;
    }

    /// <summary>
    /// Pulls one control inward by <paramref name="inset"/>. The anchor says which way
    /// "inward" is: pinned at 0 it moves right, pinned at 1 it moves left, stretched from
    /// 0 to 1 it is squeezed from both sides, and centred on 0.5 nothing moves.
    /// </summary>
    private static void Fit(Control? node, float inset)
    {
        if (node == null)
            return;

        if (!_origins.TryGetValue(node, out Origin? origin))
        {
            origin = new Origin { Left = node.OffsetLeft, Right = node.OffsetRight };
            _origins.Add(node, origin);
        }

        node.OffsetLeft = origin.Left + inset * (1f - 2f * node.AnchorLeft);
        node.OffsetRight = origin.Right + inset * (1f - 2f * node.AnchorRight);
    }


    /// <summary>
    /// Stretches one child all the way back out to the full canvas while its siblings
    /// stay clamped. The top bar reads as a bar across the screen, not a floating strip,
    /// so its backing plate should still span edge to edge even though the icons on it
    /// are pulled in to where they can be read at a glance.
    /// </summary>
    private static void SpanFullCanvas(Control? node, float inset)
    {
        if (node == null || inset <= 0f || node.GetParent() is not Control parent)
            return;

        float canvasWidth = node.GetViewportRect().Size.X;
        if (canvasWidth <= 0f)
            return;

        // Offsets are measured from the anchor, which sits a fraction along the clamped
        // parent; the parent itself starts one inset in from the left of the canvas.
        node.OffsetLeft = -inset - node.AnchorLeft * parent.Size.X;
        node.OffsetRight = canvasWidth - inset - node.AnchorRight * parent.Size.X;
    }

    /// <summary>Half the width to claw back, or 0 when no clamping applies.</summary>
    private static float InsetFor(CanvasItem item)
    {
        if (UltrawideConfig.HudMaxRatio <= 0f)
            return 0f;

        Vector2 canvas = item.GetViewportRect().Size;
        if (canvas.X <= 0 || canvas.Y <= 0)
            return 0f;

        return Mathf.Max(0f, (canvas.X - canvas.Y * UltrawideConfig.HudMaxRatio) * 0.5f);
    }

    /// <summary>Narrow the combat HUD as a whole; its contents follow on their own.</summary>
    [HarmonyPatch(typeof(NCombatUi), "_Ready")]
    [HarmonyPostfix]
    private static void NCombatUi_Ready(NCombatUi __instance) => Fit(__instance, InsetFor(__instance));

    /// <summary>
    /// The run HUD mixes widgets with full-screen overlays, so narrow only the pieces
    /// meant to hug the edges rather than the container they share.
    /// </summary>
    /// <summary>
    /// The map screen pins its own controls to the screen edges, outside the run HUD, so
    /// it needs the same treatment: at full width the back arrow and the legend end up at
    /// opposite ends of the display with the map itself marooned in the middle.
    /// </summary>
    [HarmonyPatch(typeof(NMapScreen), "_Ready")]
    [HarmonyPostfix]
    private static void NMapScreen_Ready(NMapScreen __instance)
    {
        float inset = InsetFor(__instance);
        if (inset <= 0f)
            return;

        foreach (string path in new[] { "Back", "%MapLegend", "%ShareButton", "%DrawingTools" })
            Fit(__instance.GetNodeOrNull<Control>(path), inset);
    }

    [HarmonyPatch(typeof(NGlobalUi), "_Ready")]
    [HarmonyPostfix]
    private static void NGlobalUi_Ready(NGlobalUi __instance)
    {
        float inset = InsetFor(__instance);
        if (inset <= 0f)
            return;

        // The same unique-name paths the node resolves in its own _Ready.
        foreach (string path in new[] { "%TopBar", "%RelicInventory", "%MultiplayerPlayerContainer" })
            Fit(__instance.GetNodeOrNull<Control>(path), inset);

        // ...then push the top bar's backing plate back out to the full width.
        Control? topBar = __instance.GetNodeOrNull<Control>("%TopBar");
        SpanFullCanvas(topBar?.GetNodeOrNull<Control>("%BgImage"), inset);
    }
}
