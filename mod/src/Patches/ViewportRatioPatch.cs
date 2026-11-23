using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Re-bases the buttons that slide in from the edge of the screen, and keeps the ones
/// that slide out actually off it.
///
/// These compute where to sit as a fraction of the whole viewport:
///     ShowPos => _showPosRatio * _viewport.GetVisibleRect().Size
/// but the value is assigned to Position, which Godot reads relative to the parent. The
/// two only agree while the parent is the full canvas, so on a wide display the button
/// ends up pinned to the far edge instead of sitting with the rest of the interface.
///
/// Their hidden positions need the opposite treatment. A button is hidden by nudging it
/// a fixed distance past where the canvas used to end - the proceed button moves 400
/// design pixels right, the back arrow 180 left - which stops clearing the screen once
/// the interface is narrowed, leaving a ghost of the button parked at the edge, or two
/// arrows where the shown and hidden ones both end up visible. Hidden positions are
/// therefore pushed clear of the real canvas rather than of the narrowed band.
/// </summary>
internal static class ViewportRatioPatches
{
    /// <summary>Extra clearance so a hidden button is comfortably past the edge.</summary>
    private const float HideMargin = 80f;

    private static bool Frame(Control node, out Vector2 canvas, out float parentX)
    {
        canvas = node.GetViewportRect().Size;
        parentX = (node.GetParent() as Control)?.GlobalPosition.X ?? 0f;
        return canvas.X > 0f && canvas.Y > 0f;
    }

    /// <summary>Places the button the same fraction along the band the HUD may use.</summary>
    private static void Show(Control node, ref Vector2 result)
    {
        if (!Frame(node, out Vector2 canvas, out float parentX))
            return;

        float allowed = UltrawideConfig.HudMaxRatio > 0f
            ? Mathf.Min(canvas.X, canvas.Y * UltrawideConfig.HudMaxRatio)
            : canvas.X;

        float inset = (canvas.X - allowed) * 0.5f;
        result.X = inset + result.X / canvas.X * allowed - parentX;
    }

    /// <summary>Pushes the button clear of the canvas on whichever side it hides toward.</summary>
    private static void Hide(Control node, ref Vector2 result, bool toTheRight)
    {
        if (!Frame(node, out Vector2 canvas, out float parentX))
            return;

        float width = Math.Max(node.Size.X, 200f);
        result.X = toTheRight
            ? canvas.X + HideMargin - parentX
            : -(width + HideMargin) - parentX;
    }

    [HarmonyPatch(typeof(NEndTurnButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void EndTurn_ShowPos(NEndTurnButton __instance, ref Vector2 __result)
        => Show(__instance, ref __result);

    /// <summary>End turn hides downward, so only its horizontal placement needs re-basing.</summary>
    [HarmonyPatch(typeof(NEndTurnButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void EndTurn_HidePos(NEndTurnButton __instance, ref Vector2 __result)
        => Show(__instance, ref __result);

    [HarmonyPatch(typeof(NPingButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Ping_ShowPos(NPingButton __instance, ref Vector2 __result)
        => Show(__instance, ref __result);

    /// <summary>Ping also hides downward.</summary>
    [HarmonyPatch(typeof(NPingButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Ping_HidePos(NPingButton __instance, ref Vector2 __result)
        => Show(__instance, ref __result);

    [HarmonyPatch(typeof(NProceedButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Proceed_ShowPos(NProceedButton __instance, ref Vector2 __result)
        => Show(__instance, ref __result);

    /// <summary>Proceed hides sideways, so it has to clear the right edge of the canvas.</summary>
    [HarmonyPatch(typeof(NProceedButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Proceed_HidePos(NProceedButton __instance, ref Vector2 __result)
        => Hide(__instance, ref __result, toTheRight: true);
}
