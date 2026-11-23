using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Catches every full-screen dimmer the game puts up, including the ones added later.
///
/// Dozens of screens darken whatever is behind them with a rect that fills its parent.
/// Once the interface is narrowed that parent stops at the edge of the band, so the
/// dimming stops there too and leaves two bright strips of undimmed game down the
/// sides -- the death screen was the clearest case, with the combat still lit either
/// side of the red wash.
///
/// Naming each screen would mean a patch per class, a new one for every screen the
/// game adds, and silence whenever one gets renamed. Listening for nodes entering the
/// tree instead covers all of them from one place: every dimmer in the game is a node
/// whose name ends in "Backstop", and the ones that are not full-screen are left alone
/// by the size check below.
/// </summary>
internal static class BackstopSweepPatch
{
    /// <summary>The tree we are already listening to, so a reload does not stack handlers.</summary>
    private static SceneTree? _tree;

    [HarmonyPatch(typeof(NGlobalUi), "_Ready")]
    [HarmonyPostfix]
    private static void Hook(NGlobalUi __instance)
    {
        if (HudClampPatches.InsetFor(__instance) <= 0f)
            return; // nothing is narrowed, so nothing needs pushing back out

        SceneTree? tree = __instance.GetTree();
        if (tree == null || tree == _tree)
            return;

        _tree = tree;
        tree.NodeAdded += OnNodeAdded;

        Sweep(__instance); // the HUD's own backstops are already in the tree by now
    }

    private static void Sweep(Node node)
    {
        if (node is Control control && IsBackstop(control))
            Schedule(control);

        foreach (Node child in node.GetChildren())
            Sweep(child);
    }

    private static void OnNodeAdded(Node node)
    {
        if (node is Control control && IsBackstop(control))
            Schedule(control);
    }

    private static bool IsBackstop(Control control)
        => control.Name.ToString().EndsWith("Backstop", StringComparison.Ordinal);

    /// <summary>
    /// Two frames: a backstop is sized by the layout pass after it enters the tree, and
    /// the narrowing above it lands in that same pass.
    /// </summary>
    private static void Schedule(Control control)
        => Callable.From(() => Callable.From(() => Stretch(control)).CallDeferred()).CallDeferred();

    private static void Stretch(Control control)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree())
            return;

        float canvas = control.GetViewportRect().Size.X;
        if (canvas <= 0f)
            return;

        Rect2 rect = control.GetGlobalRect();

        // Smaller than most of the screen: a local backdrop behind one widget, not a
        // dimmer over the game, and stretching it would cover things it should not.
        if (rect.Size.X < canvas * 0.6f)
            return;

        if (rect.Position.X <= 1f && rect.End.X >= canvas - 1f)
            return; // already reaches both edges

        HudClampPatches.SpanFullCanvas(control);
    }
}
