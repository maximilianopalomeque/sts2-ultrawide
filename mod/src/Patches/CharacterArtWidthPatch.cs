using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Presents every character's artwork at the same width.
///
/// Each piece is drawn at whatever size its artist worked at and the game shows them
/// all at one fixed scale, so they come out between 2644 and 2897 canvas units wide.
/// Below 21:9 they all overflow the screen and the difference never shows. Past it the
/// border around each one is a different thickness, and picking through the roster
/// makes the frame jump about.
///
/// Measuring the artwork and scaling each to a common width settles it. The difference
/// is thirteen per cent across the roster -- 2592 for the Ironclad against 2937 for the
/// Defect -- so the correction is a few per cent either way, nothing like the third of
/// the picture that covering the screen would cost.
/// </summary>
internal static class CharacterArtWidthPatch
{
    private const float VanillaRatioMax = 2.3888888f;

    /// <summary>What the pieces are drawn against: 2561 wide, shown at the game's 1.1.</summary>
    private const float TargetWidth = 2561f * 1.1f;

    private const string BaseScaleMeta = "uw_art_base_scale";

    [HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
    [HarmonyPostfix]
    private static void CharacterSelect(NCharacterSelectScreen __instance) => Defer(__instance);

    [HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "AfterMultiplayerStarted")]
    [HarmonyPostfix]
    private static void Lobby(NMultiplayerLoadGameScreen __instance) => Defer(__instance);

    [HarmonyPatch(typeof(NDreadScreen), "SetSelectedCharacterVisuals")]
    [HarmonyPostfix]
    private static void Dread(NDreadScreen __instance) => Defer(__instance);

    /// <summary>Deferred twice: the artwork is instantiated during the call, and its
    /// skeleton does not report bounds until it has been through a layout pass.</summary>
    private static void Defer(Node screen)
        => Callable.From(() => Callable.From(() => Apply(screen)).CallDeferred()).CallDeferred();

    private static void Apply(Node screen)
    {
        if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
            return;

        Control? container = screen.GetNodeOrNull<Control>("AnimatedBg")
                             ?? screen.GetNodeOrNull<Control>("%AnimatedBg");
        if (container == null)
            return;

        Vector2 canvas = container.GetViewportRect().Size;
        if (canvas.Y <= 0f || canvas.X / canvas.Y <= VanillaRatioMax)
            return; // every piece overflows this screen anyway

        foreach (Node child in container.GetChildren())
        {
            if (child is Control art)
                Normalise(art);
        }
    }

    private static void Normalise(Control art)
    {
        if (!art.HasMeta(BaseScaleMeta))
            art.SetMeta(BaseScaleMeta, art.Scale);

        Vector2 baseScale = art.GetMeta(BaseScaleMeta).AsVector2();
        art.Scale = baseScale;

        float drawn = DrawnWidth(art);
        if (drawn <= 1f)
            return;

        art.Scale = baseScale * (TargetWidth / drawn);
    }

    /// <summary>
    /// The width the artwork actually covers. A Spine rig has no rect worth reading --
    /// its container's is nearly twice what is drawn -- so its skeleton is asked
    /// directly; the characters drawn as a plain image are measured from their texture.
    /// </summary>
    private static float DrawnWidth(Node node)
    {
        if (node.GetClass() == "SpineSprite" && node.HasMethod("get_skeleton") && node is Node2D sprite)
        {
            // Through the game's own binding rather than a raw call: it knows the
            // method names this Spine build actually exposes, and it keeps the native
            // object alive for the duration of the call.
            using var skeleton = new MegaSkeleton(node.Call("get_skeleton"));
            Rect2 bounds = skeleton.GetBounds();
            if (bounds.Size.X > 1f)
                return bounds.Size.X * Math.Abs(sprite.GetGlobalTransform().Scale.X);
        }

        if (node is TextureRect { Texture: not null } rect)
            return rect.GetGlobalRect().Size.X;

        foreach (Node child in node.GetChildren())
        {
            float found = DrawnWidth(child);
            if (found > 1f)
                return found;
        }
        return 0f;
    }
}
