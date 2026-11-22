using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Grows background art to cover the widened canvas.
///
/// Background layers are authored against a canvas no wider than 21:9 and sit centred,
/// so on a wider canvas they leave bare strips down both sides. Every room keeps its
/// artwork under a single container, so one scale per room covers the lot.
///
/// _Ready is hooked as well as any resize handler, because a resize handler only fires
/// when the window actually changes size - entering a room at a size that was already
/// correct never calls it, which would leave that room's background unscaled.
/// </summary>
internal static class BackgroundPatches
{
    private static void Grow(Node instance, Control? container, string what)
    {
        if (container == null)
        {
            Log.Warn("[Ultrawide] Background container for " + what + " not found; leaving it alone.");
            return;
        }

        Ultrawide.GrowToCoverCanvas(container, Ultrawide.WideFactor(instance.GetWindow()));
    }

    private static void GrowField(Node instance, string fieldName)
        => Grow(instance, AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance) as Control, fieldName);

    private static void GrowPath(Node instance, string path)
        => Grow(instance, instance.GetNodeOrNull<Control>(path), path);

    // ---- main menu -------------------------------------------------------------

    /// <summary>
    /// The menu's logo is parented under the background container, so scaling the
    /// background would drag the title up off the top of the screen with it. Pin the
    /// logo's global transform across the change so only the artwork behind it moves.
    /// </summary>
    private static void GrowMenuBg(NMainMenuBg instance)
    {
        var logo = AccessTools.Field(typeof(NMainMenuBg), "_logo")?.GetValue(instance) as Node2D;
        Transform2D? pinned = logo?.GlobalTransform;

        GrowField(instance, "_bg");

        if (logo != null && pinned.HasValue)
            logo.GlobalTransform = pinned.Value;
    }

    [HarmonyPatch(typeof(NMainMenuBg), "ScaleBgIfNarrow")]
    [HarmonyPostfix]
    private static void NMainMenuBg_ScaleBgIfNarrow(NMainMenuBg __instance) => GrowMenuBg(__instance);

    [HarmonyPatch(typeof(NMainMenuBg), "_Ready")]
    [HarmonyPostfix]
    private static void NMainMenuBg_Ready(NMainMenuBg __instance) => GrowMenuBg(__instance);

    // ---- combat ----------------------------------------------------------------

    [HarmonyPatch(typeof(NCombatSceneContainer), "OnWindowChange")]
    [HarmonyPostfix]
    private static void NCombatSceneContainer_OnWindowChange(NCombatSceneContainer __instance)
        => GrowField(__instance, "_bgContainer");

    [HarmonyPatch(typeof(NCombatSceneContainer), "_Ready")]
    [HarmonyPostfix]
    private static void NCombatSceneContainer_Ready(NCombatSceneContainer __instance)
        => GrowField(__instance, "_bgContainer");

    // ---- shop and rest site ----------------------------------------------------

    [HarmonyPatch(typeof(NStandardMerchantRoom), "_Ready")]
    [HarmonyPostfix]
    private static void NStandardMerchantRoom_Ready(NStandardMerchantRoom __instance)
        => GrowPath(__instance, "SceneContainer/BgContainer");

    [HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
    [HarmonyPostfix]
    private static void NRestSiteRoom_Ready(NRestSiteRoom __instance)
        => GrowPath(__instance, "BgContainer");
}
