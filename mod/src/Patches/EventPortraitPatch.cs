using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Fills the screen behind ordinary events.
///
/// An event's artwork is a single 2560-wide portrait in the middle of the screen with
/// nothing drawn beyond it, so on a wide display the event reads as a panel floating in
/// a void rather than a scene.
///
/// The rect is stretched to the canvas and the stretch mode switched from
/// KeepAspectCentred to KeepAspectCovered. Scaling the node instead does nothing
/// visible: centred mode fits the picture inside whatever rect it is given, so the
/// artwork stays its original size with empty space around it however large the node
/// grows. Covered fills the rect and crops the overflow, which is what is wanted here.
///
/// Cropping takes a little off the top and bottom. These are composed illustrations
/// with their subject mid-frame, so that is the cheapest place to lose a few pixels.
///
/// The design rect is recorded on the node before it is touched, because the event VFX
/// are positioned in that coordinate space and have to be mapped onto the new one.
/// <see cref="EventVfxPatch"/>.
/// </summary>
[HarmonyPatch(typeof(NEventLayout), "_Ready")]
internal static class EventPortraitPatch
{
    /// <summary>Metadata key holding the portrait's rect as the artist laid it out.</summary>
    internal const string DesignRectMeta = "uw_portrait_design_rect";

    [HarmonyPostfix]
    private static void Postfix(NEventLayout __instance)
    {
        // Deferred: the portrait's rect is not final until the scene has been laid out.
        Callable.From(() => Apply(__instance)).CallDeferred();
    }

    private static void Apply(NEventLayout layout)
    {
        if (!GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree())
            return;

        if (layout.GetNodeOrNull<Control>("%Portrait") is not TextureRect portrait)
        {
            // The Ancient and combat layouts share this script and have no portrait of
            // their own -- their backgrounds are handled elsewhere -- so only the layout
            // that is supposed to have one is worth reporting.
            if (layout.SceneFilePath.EndsWith("default_event_layout.tscn", StringComparison.Ordinal))
                Log.Warn("[Ultrawide] Event portrait not found; events will not fill the screen.");
            return;
        }

        Vector2 canvas = layout.GetViewportRect().Size;
        if (canvas.X <= 0f || canvas.Y <= 0f)
            return;

        if (canvas.X <= portrait.Size.X * portrait.Scale.X + 1f)
            return; // already as wide as the display

        if (!portrait.HasMeta(DesignRectMeta))
            portrait.SetMeta(DesignRectMeta, portrait.Size);

        portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        portrait.Scale = Vector2.One;
        portrait.PivotOffset = Vector2.Zero;

        // Anchor to the whole parent rather than nudging offsets, so the portrait keeps
        // filling the screen if the window is resized later.
        portrait.AnchorLeft = 0f;
        portrait.AnchorTop = 0f;
        portrait.AnchorRight = 1f;
        portrait.AnchorBottom = 1f;
        portrait.OffsetLeft = 0f;
        portrait.OffsetTop = 0f;
        portrait.OffsetRight = 0f;
        portrait.OffsetBottom = 0f;

        EventVfxPatch.RemapChildren(portrait);
    }
}
