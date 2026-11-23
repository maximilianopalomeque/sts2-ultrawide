using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Extends the character artwork past 21:9 on character select, the lobby and Dread.
///
/// All three share one script for their backdrop, and it sizes the art by aspect ratio:
/// it interpolates up from 4:3 and then stops, holding 1.1 for everything 16:9 and
/// wider. That one figure is what covers the screen at every ratio the game supports,
/// so past 21:9 the art stops growing while the canvas keeps widening and the piece
/// turns into a panel with the blurred backdrop showing down both sides.
///
/// Carrying the same relationship further -- 1.1 at 21:9, proportionally more beyond --
/// keeps whatever margin the artists built in rather than inventing a new framing. The
/// art is a Spine skeleton rather than a texture, so there is nothing to measure and
/// extrapolating the game's own figure is both the simplest and the most faithful
/// answer; the alternative, scaling until some measured edge reaches the screen, has no
/// edge to measure.
///
/// It grows about its own top edge so heads and raised weapons keep their place and the
/// crop comes off the bottom, which is floor and is half hidden behind the interface.
/// At 21:9 and below nothing is touched.
///
/// Applied on _Ready as well as on the resize the script listens for: that script only
/// ever connects the signal, it never runs the sizing once at startup, so a screen
/// opened while the window is sitting still keeps whatever scale the scene file gave it
/// and a patch on the handler alone never fires.
/// </summary>
internal static class CharacterArtPatch
{
    /// <summary>Widest ratio the vanilla curve accounts for, and the scale it holds there.</summary>
    private const float VanillaRatioMax = 2.3888888f;
    private const float VanillaScale = 1.1f;

    [HarmonyPatch(typeof(NCharacterSelectScreenBg), "OnWindowChange")]
    [HarmonyPrefix]
    private static bool OnWindowChange(NCharacterSelectScreenBg __instance) => !Apply(__instance);

    [HarmonyPatch(typeof(NCharacterSelectScreenBg), "_Ready")]
    [HarmonyPostfix]
    private static void Ready(NCharacterSelectScreenBg __instance)
        => Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(__instance) && __instance.IsInsideTree())
                Apply(__instance);
        }).CallDeferred();

    /// <summary>True when this display is wider than the game sizes the art for.</summary>
    private static bool Apply(NCharacterSelectScreenBg __instance)
    {
        Vector2 size = __instance.GetViewportRect().Size;
        if (size.X <= 0f || size.Y <= 0f)
            return false;

        float ratio = size.X / size.Y;
        if (ratio <= VanillaRatioMax)
            return false; // within the range the game sizes correctly

        // Off by default. Reaching the edges means a third of the height goes, and
        // these pieces put the character in that third -- so whether a border or a
        // crop is the lesser evil is the player's call, not ours.
        if (UltrawideConfig.CharacterArtZoom <= 0f)
            return false;

        float covered = VanillaScale * ratio / VanillaRatioMax;
        __instance.PivotOffset = new Vector2(__instance.Size.X * 0.5f, 0f);
        __instance.Scale = Vector2.One * Mathf.Lerp(VanillaScale, covered, UltrawideConfig.CharacterArtZoom);
        return true;
    }
}
