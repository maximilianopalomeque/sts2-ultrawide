using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Re-bases the buttons that slide in from the screen edge.
///
/// These compute where to sit as a fraction of the whole viewport:
///     ShowPos => _showPosRatio * _viewport.GetVisibleRect().Size
/// but the value is assigned to Position, which Godot reads relative to the parent. The
/// two only agree while the parent is the full canvas, and on a wide display the result
/// is a button pinned to the far edge - or, once the combat HUD is narrowed, one that
/// animates clean off the screen entirely.
///
/// We put them back on the same fraction along the band the HUD is allowed to use, then
/// convert that to the coordinates their parent expects. With the HUD unclamped the band
/// is the whole canvas and nothing changes, so this is inert for anyone not using it.
/// </summary>
internal static class ViewportRatioPatches
{
    private static void Rebase(Control node, ref Vector2 result)
    {
        if (node.GetParent() is not Control parent)
            return;

        Vector2 canvas = node.GetViewportRect().Size;
        if (canvas.X <= 0f || canvas.Y <= 0f)
            return;

        float allowed = UltrawideConfig.HudMaxRatio > 0f
            ? Mathf.Min(canvas.X, canvas.Y * UltrawideConfig.HudMaxRatio)
            : canvas.X;

        float inset = (canvas.X - allowed) * 0.5f;
        float wantedOnScreen = inset + result.X / canvas.X * allowed;

        // Position is parent-relative, and that parent may itself already be inset.
        result.X = wantedOnScreen - parent.GlobalPosition.X;
    }

    [HarmonyPatch(typeof(NEndTurnButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void EndTurn_ShowPos(NEndTurnButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);

    [HarmonyPatch(typeof(NEndTurnButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void EndTurn_HidePos(NEndTurnButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);

    [HarmonyPatch(typeof(NPingButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Ping_ShowPos(NPingButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);

    [HarmonyPatch(typeof(NPingButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Ping_HidePos(NPingButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);

    [HarmonyPatch(typeof(NProceedButton), "ShowPos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Proceed_ShowPos(NProceedButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);

    [HarmonyPatch(typeof(NProceedButton), "HidePos", MethodType.Getter)]
    [HarmonyPostfix]
    private static void Proceed_HidePos(NProceedButton __instance, ref Vector2 __result)
        => Rebase(__instance, ref __result);
}
