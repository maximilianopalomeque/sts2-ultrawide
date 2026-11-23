using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Presents the character artwork at 21:9, with the rest of the screen masked off.
///
/// These screens are the one place the mod does not widen. Each piece is a single
/// illustration drawn as a 21:9 sheet with an edge: enlarging one to reach the sides
/// costs a third of its height, and the character lives in that third, while leaving
/// them be gives a border whose thickness changes with every character -- the pieces
/// run anywhere from 2592 to 2937 units wide, and the frame jumps about as you click
/// through the roster.
///
/// Masking to a fixed 21:9 window settles both. The border stops depending on the
/// artwork, because it is no longer the artwork's edge you are looking at, and nothing
/// is cropped or stretched. It reads as a deliberate frame rather than a screen the
/// game failed to fill.
///
/// The mask goes directly above the artwork and below everything else, so the back
/// arrow, the confirm tick and the panels keep drawing over it.
///
/// Character select only. The multiplayer lobby shows the same artwork but builds the
/// screen differently -- its whole scene sits inside a SubViewport -- and a band placed
/// by these coordinates lands in the middle of the picture rather than at its edge.
/// Rather than guess at a second layout that cannot be checked without a second player,
/// that screen is left as the game draws it.
/// </summary>
internal static class CharacterArtMaskPatch
{
    private const float VanillaRatioMax = 2.3888888f;
    private const string MaskName = "UltrawideArtMask";

    /// <summary>
    /// A little more than 21:9, because not every piece fills its own frame. The
    /// Ironclad's and the Silent's stop some fifty units short on the left, so masking
    /// to exactly 21:9 leaves a sliver of the backdrop showing between the band and the
    /// artwork. Taking a fraction more off both sides covers it, and keeps the window
    /// the same for everyone.
    /// </summary>
    private const float Overlap = 64f;

    [HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
    [HarmonyPostfix]
    private static void CharacterSelect(NCharacterSelectScreen __instance) => Defer(__instance);

    private static void Defer(Control screen)
        => Callable.From(() => Apply(screen)).CallDeferred();

    private static void Apply(Control screen)
    {
        if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
            return;

        // Someone who would rather have the artwork cropped than bordered has said so;
        // masking it as well would be the worst of both.
        if (UltrawideConfig.CharacterArtZoom > 0f)
            return;

        Vector2 canvas = screen.GetViewportRect().Size;
        if (canvas.X <= 0f || canvas.Y <= 0f)
            return;

        float inset = (canvas.X - canvas.Y * VanillaRatioMax) * 0.5f + Overlap;
        if (inset <= Overlap + 1f)
            return; // 21:9 or narrower: the artwork already fills the screen

        int above = ArtworkIndex(screen);
        Band(screen, MaskName + "Left", 0f, inset, canvas.Y, above + 1);
        Band(screen, MaskName + "Right", canvas.X - inset, inset, canvas.Y, above + 2);
    }

    /// <summary>Index of the last node that draws artwork, so the mask lands above it.</summary>
    private static int ArtworkIndex(Control screen)
    {
        int index = 0;
        foreach (Node child in screen.GetChildren())
        {
            string name = child.Name.ToString();
            if (name is "StaticBg" or "AnimatedBg" or "SubViewportContainer")
                index = child.GetIndex();
        }
        return index;
    }

    private static void Band(Control screen, string name, float x, float width, float height, int index)
    {
        if (screen.GetNodeOrNull<ColorRect>(name) is not { } band)
        {
            band = new ColorRect
            {
                Name = name,
                Color = Colors.Black,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            screen.AddChild(band);
        }

        band.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        band.Position = new Vector2(x, 0f);
        band.Size = new Vector2(width, height);
        screen.MoveChild(band, Mathf.Min(index, screen.GetChildCount() - 1));
    }
}
