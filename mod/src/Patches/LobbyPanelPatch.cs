using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Re-centres the multiplayer lobby's loose panels.
///
/// The lobby puts the saved run's details in a panel anchored to the middle of the
/// screen, and the backdrop for the other players' list beside it at plain absolute
/// coordinates -- 906 to 1333, written for a 1920-wide canvas. The two therefore only
/// line up at 1920. Every ratio past that pulls them apart, until at 32:9 the backdrop
/// is most of a thousand pixels left of the names it belongs to and overlaps the panel
/// next to it instead.
///
/// Shifting the absolute ones by half the difference between the canvas and the design
/// width puts them back beside what they belong to, at any width.
/// </summary>
[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "AfterMultiplayerStarted")]
internal static class LobbyPanelPatch
{
    /// <summary>The canvas these absolute coordinates were written against.</summary>
    private const float DesignWidth = 1920f;

    private const string BasePositionMeta = "uw_base_position";

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerLoadGameScreen __instance)
        => Callable.From(() => Apply(__instance)).CallDeferred();

    private static void Apply(Control screen)
    {
        if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
            return;

        float canvasWidth = screen.GetViewportRect().Size.X;
        float shift = (canvasWidth - DesignWidth) * 0.5f;
        if (shift <= 0.5f)
            return;

        foreach (Node child in screen.GetChildren())
        {
            // Anchored children already follow the canvas; it is only the ones pinned
            // to absolute coordinates that stay behind.
            if (child is not Control c || c.AnchorLeft != 0f || c.AnchorRight != 0f)
                continue;

            if (!c.HasMeta(BasePositionMeta))
                c.SetMeta(BasePositionMeta, c.Position);

            Vector2 basePosition = c.GetMeta(BasePositionMeta).AsVector2();
            c.Position = new Vector2(basePosition.X + shift, basePosition.Y);
        }
    }
}
