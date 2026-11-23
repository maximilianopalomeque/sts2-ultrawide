using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Covers the canvas on Ancient and event backgrounds.
///
/// NAncientBgContainer interpolates position and scale between hand-tuned 4:3, 16:9 and
/// 21:9 setups, but clamps the ratio at 21:9, so a wider display keeps the 21:9 offset
/// and ends up with the artwork shoved to one side and short of the far edge.
///
/// Rather than invent new numbers we reuse the game's own 21:9 setup, which is known to
/// fit exactly, and map that result onto the wider canvas. Writing the transform out in
/// full is what lets us keep the bottom edge pinned: a plain scale about the centre
/// would eat the foreground and the sky in equal measure.
/// </summary>
[HarmonyPatch(typeof(NAncientBgContainer), "OnWindowChange")]
internal static class AncientBgPatch
{
    /// <summary>The ratio NAncientBgContainer clamps to - its widest tuned setup.</summary>
    private const float VanillaRatioMax = 2.3333f;

    /// <summary>
    /// Ancients whose subject sits high in the frame. The anchor is the line the scale
    /// pivots on, so putting it above the top of the screen pushes the artwork down:
    /// Vakuu and Nonupeipe are framed so tightly that anchoring at the very top still
    /// leaves their faces above the edge, and they need the picture moved rather than a
    /// kinder crop. The rest are composed around the middle and keep the configured
    /// anchor.
    ///
    /// Matched on the artwork's own path. An Ancient's background is a texture, not a
    /// scene, so there is no node named after it to look for -- which is what made the
    /// first attempt at this silently do nothing.
    /// </summary>
    private static readonly (string Key, float Anchor)[] _topHeavy =
    {
        ("vakuu", -0.45f),
        ("nonupeipe", -0.45f),
    };

    private static float AnchorFor(Node container)
    {
        string? path = FindTexturePath(container, 0);

        foreach ((string key, float anchor) in _topHeavy)
        {
            if (path != null && path.Contains(key, StringComparison.OrdinalIgnoreCase))
                return anchor;

            foreach (Node child in container.GetChildren())
            {
                if (child.Name.ToString().Contains(key, StringComparison.OrdinalIgnoreCase))
                    return anchor;
            }
        }
        return UltrawideConfig.AncientVerticalAnchor;
    }

    private static string? FindTexturePath(Node node, int depth)
    {
        if (depth > 4)
            return null;

        foreach (Node child in node.GetChildren())
        {
            if (child is TextureRect { Texture: not null } rect && rect.Texture.ResourcePath.Length > 0)
                return rect.Texture.ResourcePath;
            if (child is Sprite2D { Texture: not null } sprite && sprite.Texture.ResourcePath.Length > 0)
                return sprite.Texture.ResourcePath;

            string? found = FindTexturePath(child, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }

    [HarmonyPrefix]
    private static bool Prefix(NAncientBgContainer __instance)
    {
        Vector2 size = __instance.Size;
        if (size.X <= 0 || size.Y <= 0)
            return true;

        float ratio = size.X / size.Y;
        if (ratio <= VanillaRatioMax)
            return true; // within the range vanilla was tuned for

        var pos219 = (Vector2?)AccessTools.Field(typeof(NAncientBgContainer), "_pos219")?.GetValue(__instance);
        var scale219 = (Vector2?)AccessTools.Field(typeof(NAncientBgContainer), "_scale219")?.GetValue(__instance);
        if (!pos219.HasValue || !scale219.HasValue)
            return true; // can't read the reference setup; leave vanilla alone

        // How much wider we are than the widest layout the game knows how to build.
        float k = ratio / VanillaRatioMax;

        Vector2 scale = scale219.Value * k;

        // Horizontally: pivot on the canvas centre and solve for the offset that makes
        // the 21:9 layout land exactly on the wider canvas.
        float pivotX = size.X * 0.5f;
        float posX = pos219.Value.X * k + pivotX * (scale.X - 1f);

        // Vertically: pivot on the chosen anchor line so that line does not move, which
        // keeps the ground and horizon in place and takes the crop out of the sky.
        float anchorY = size.Y * AnchorFor(__instance);
        float pivotY = anchorY - pos219.Value.Y;

        // Never let the anchor pull the artwork off an edge. A Control scaled about a
        // pivot has its top at pos.y + pivot.y * (1 - scale.y); solving that for the
        // edges turns the anchor into a request rather than an instruction, so a value
        // chosen to lift a face into frame cannot leave a black band behind it.
        float posY = pos219.Value.Y;
        float top = posY + pivotY * (1f - scale.Y);
        if (top > 0f)
            posY -= top;

        float bottom = posY + pivotY + (size.Y - pivotY) * scale.Y;
        if (bottom < size.Y)
            posY += size.Y - bottom;

        __instance.PivotOffset = new Vector2(pivotX, pivotY);
        __instance.Scale = scale;
        __instance.Position = new Vector2(posX, posY);
        return false;
    }
}

/// <summary>
/// Re-runs the Ancient background layout once its artwork is actually there.
///
/// NAncientBgContainer lays itself out in _Ready, but the Ancient's background scene is
/// instantiated into it later, while the event sets up its visuals. Anything that needs
/// to know *which* Ancient is on screen -- such as the per-Ancient framing above -- is
/// therefore looking at an empty container and quietly falls back to the default, which
/// is why two attempts at that framing appeared to do nothing at all.
/// </summary>
[HarmonyPatch(typeof(NAncientEventLayout), "InitializeVisuals")]
internal static class AncientBgRefreshPatch
{
    [HarmonyPostfix]
    private static void Postfix(NAncientEventLayout __instance)
    {
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(__instance) || !__instance.IsInsideTree())
                return;
            if (__instance.GetNodeOrNull<NAncientBgContainer>("%AncientBgContainer") is not { } container)
                return;

            AccessTools.Method(typeof(NAncientBgContainer), "OnWindowChange")?.Invoke(container, null);
        }).CallDeferred();
    }
}
