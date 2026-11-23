using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps the interface within a comfortable width while the artwork uses the whole
/// screen. At 32:9 the end-turn button and the energy orb sit nearly four thousand
/// pixels apart, and a card grid runs right off the edges of where you are looking.
///
/// The narrowing is done once, at the two containers everything else lives under, and
/// the handful of pieces that genuinely want the whole display are pushed back out
/// afterwards. Doing it the other way round - narrowing each widget - means chasing
/// every screen the game has and still missing the ones added later; it also fails for
/// anything the game re-positions after the fact, which is most of the buttons.
///
/// The exceptions are the full-screen backdrops. An overlay that dims the game behind a
/// popup has to cover the display or the edges stay bright and the whole thing reads as
/// broken.
/// </summary>
internal static class HudClampPatches
{
    /// <summary>Pieces under the run HUD that must keep spanning the whole display.</summary>
    private static readonly string[] _fullWidth =
    {
        // The two backstops are not unique-named, so they have to be reached through
        // their containers, which are. Addressing them as "%OverlayBackstop" silently
        // resolves to nothing and leaves the screen dimmed only down the middle.
        "%OverlayScreensContainer/OverlayBackstop",
        "%CapstoneScreenContainer/CapstoneBackstop",
        "%MultiplayerTimeoutOverlay",
        "%AboveTopBarVfxContainer",
    };

    /// <summary>Offsets as authored, so repeated calls stay idempotent.</summary>
    private static readonly ConditionalWeakTable<Control, Origin> _origins = new();

    private sealed class Origin
    {
        public float Left;
        public float Right;
    }

    private static Origin Remember(Control node)
    {
        if (!_origins.TryGetValue(node, out Origin? origin))
        {
            origin = new Origin { Left = node.OffsetLeft, Right = node.OffsetRight };
            _origins.Add(node, origin);
        }
        return origin;
    }

    /// <summary>Half the width to claw back, or 0 when no clamping applies.</summary>
    internal static float InsetFor(CanvasItem item)
    {
        if (UltrawideConfig.HudMaxRatio <= 0f)
            return 0f;

        Vector2 canvas = item.GetViewportRect().Size;
        if (canvas.X <= 0f || canvas.Y <= 0f)
            return 0f;

        return Mathf.Max(0f, (canvas.X - canvas.Y * UltrawideConfig.HudMaxRatio) * 0.5f);
    }

    /// <summary>Pulls a stretched container in from both sides.</summary>
    private static void Narrow(Control? node, float inset)
    {
        if (node == null)
            return;

        Origin origin = Remember(node);
        node.OffsetLeft = origin.Left + inset;
        node.OffsetRight = origin.Right - inset;
    }

    /// <summary>
    /// Pushes one child back out to the full canvas while its parent stays narrowed.
    /// </summary>
    internal static void SpanFullCanvas(Control? node)
    {
        if (node == null || !node.IsInsideTree() || node.GetParent() is not Control parent)
            return;

        float canvasWidth = node.GetViewportRect().Size.X;
        if (canvasWidth <= 0f)
            return;

        // Solve for the offsets that put this node's global rect across the whole canvas.
        // Derived from where the parent actually sits rather than from the inset, so it
        // holds however deeply the node is nested inside the narrowed interface.
        float parentX = parent.GlobalPosition.X;
        node.OffsetLeft = -parentX - node.AnchorLeft * parent.Size.X;
        node.OffsetRight = canvasWidth - parentX - node.AnchorRight * parent.Size.X;
    }

    /// <summary>The whole combat HUD: energy, hand, piles, end turn, ping.</summary>
    [HarmonyPatch(typeof(NCombatUi), "_Ready")]
    [HarmonyPostfix]
    private static void NCombatUi_Ready(NCombatUi __instance) => Narrow(__instance, InsetFor(__instance));

    /// <summary>
    /// The run HUD, and with it every screen, overlay and popup that lives under it.
    /// </summary>
    [HarmonyPatch(typeof(NGlobalUi), "_Ready")]
    [HarmonyPostfix]
    private static void NGlobalUi_Ready(NGlobalUi __instance)
    {
        float inset = InsetFor(__instance);
        if (inset <= 0f)
            return;

        Narrow(__instance, inset);

        foreach (string path in _fullWidth)
        {
            Control? node = __instance.GetNodeOrNull<Control>(path);
            if (node == null)
                Log.Warn("[Ultrawide] '" + path + "' not found; it will stay narrowed.");
            else
                SpanFullCanvas(node);
        }

        // The top bar reads as a bar across the screen rather than a floating strip, so
        // its backing plate spans the display even though the icons on it are pulled in.
        Control? topBar = __instance.GetNodeOrNull<Control>("%TopBar");
        if (topBar == null)
            Log.Warn("[Ultrawide] Top bar not found; its backing plate stays narrowed.");
        else
            SpanFullCanvas(topBar.GetNodeOrNull<Control>("%BgImage"));
    }
}
