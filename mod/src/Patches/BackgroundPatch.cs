using System;
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

    /// <summary>Where the logo sits as a fraction of the canvas, before anything moved it.</summary>
    private const string LogoPlaceMeta = "uw_logo_place";

    /// <summary>The logo's on-screen scale before the background was ever grown.</summary>
    private const string LogoSizeMeta = "uw_logo_size";

    /// <summary>
    /// The menu's logo is parented under the background container, so scaling the
    /// background would drag the title with it.
    ///
    /// Putting it back where it was a moment ago is not enough. This runs again on every
    /// aspect-ratio change, and on those the logo is *supposed* to move, because the
    /// canvas it is composed against has changed shape -- restoring the old place walks
    /// it a little further down the screen each time until it is sitting on the menu.
    /// So the place is recorded once, as a fraction of the canvas, and re-derived for
    /// whatever canvas is current.
    /// </summary>
    private static void GrowMenuBg(NMainMenuBg instance)
    {
        var logo = AccessTools.Field(typeof(NMainMenuBg), "_logo")?.GetValue(instance) as Node2D;

        Vector2 canvas = instance.GetViewportRect().Size;
        if (logo == null || canvas.X <= 0f || canvas.Y <= 0f)
        {
            GrowField(instance, "_bg");
            return;
        }

        if (!logo.HasMeta(LogoPlaceMeta))
        {
            logo.SetMeta(LogoPlaceMeta, logo.GlobalPosition / canvas);
            logo.SetMeta(LogoSizeMeta, logo.GetGlobalTransform().Scale);
        }

        GrowField(instance, "_bg");

        // Both halves have to be undone. The logo's own Scale property never changes --
        // what grows is the scale it inherits from the container above it -- so putting
        // its position back while leaving that alone is how it ends up correctly placed
        // and half again too large.
        Vector2 wanted = logo.GetMeta(LogoSizeMeta).AsVector2();
        if (logo.GetParent() is CanvasItem parent)
        {
            Vector2 inherited = parent.GetGlobalTransform().Scale;
            logo.Scale = new Vector2(wanted.X / Math.Max(inherited.X, 0.001f),
                                     wanted.Y / Math.Max(inherited.Y, 0.001f));
        }

        logo.GlobalPosition = logo.GetMeta(LogoPlaceMeta).AsVector2() * canvas;
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
