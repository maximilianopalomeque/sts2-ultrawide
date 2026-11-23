using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Makes every individual background layer reach the edges of the screen.
///
/// Scaling the container the layers share keeps them in proportion to each other, which
/// is not enough: within one background the layers are authored at different widths, so
/// the narrower ones still stop short and the ground ends in a hard vertical line
/// partway across the display, with a blurred backdrop showing beyond it.
///
/// The layers are reached by walking the background's subtree rather than by hooking a
/// layer type, because the two kinds of background are built differently: act
/// backgrounds add NCombatBackgroundLayer nodes at runtime, while each boss ships a
/// hand-authored scene whose layers are plain TextureRects. Walking covers both.
///
/// Only near-full-width nodes are touched. Everything smaller is a prop, a creature or a
/// visual effect that is positioned deliberately and must not be stretched.
///
/// Kept as its own patch class: patches are applied per class, so a change here cannot
/// take the rest of the background scaling down with it.
/// </summary>
[HarmonyPatch(typeof(NCombatSceneContainer), "_Ready")]
internal static class LayerCoveragePatch
{
    /// <summary>A node must be at least this much of the canvas wide to count as a layer.</summary>
    private const float LayerWidthFraction = 0.45f;

    /// <summary>How deep to walk. Layers live a couple of levels down, props go deeper.</summary>
    private const int MaxDepth = 3;

    [HarmonyPostfix]
    private static void Postfix(NCombatSceneContainer __instance)
    {
        // Deferred twice: layer rects are not final until the scene has been laid out,
        // and the container's own scale is applied during that same first pass.
        Callable.From(() => Callable.From(() => Cover(__instance)).CallDeferred()).CallDeferred();
    }

    private static void Cover(NCombatSceneContainer container)
    {
        if (!GodotObject.IsInstanceValid(container) || !container.IsInsideTree())
            return;

        if (!UltrawideConfig.LayerCoverage)
            return;

        var bg = AccessTools.Field(typeof(NCombatSceneContainer), "_bgContainer")?.GetValue(container) as Control;
        if (bg == null)
        {
            Log.Warn("[Ultrawide] Background container not found; per-layer coverage skipped.");
            return;
        }

        float minWidth = container.GetViewportRect().Size.X * LayerWidthFraction;
        Walk(bg, minWidth, 0);
    }

    private static void Walk(Node node, float minWidth, int depth)
    {
        if (depth > MaxDepth)
            return;

        foreach (Node child in node.GetChildren())
        {
            if (child is Control control && control.Size.X * control.GetGlobalTransform().Scale.X >= minWidth)
                Ultrawide.EnsureCoversCanvas(control);
            else
                Walk(child, minWidth, depth + 1);
        }
    }
}
