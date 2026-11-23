using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps an event's VFX lined up with its artwork after the portrait has been widened.
///
/// Event VFX are parented to the portrait and positioned in the portrait's own 2560x1200
/// design space, so they inherit whatever transform the portrait has. That works in
/// vanilla, where the illustration exactly fills that rect. Once <see cref="EventPortraitPatch"/>
/// stretches the rect to the canvas and switches to KeepAspectCovered, the picture is
/// drawn larger than the rect and shifted vertically, while the VFX children are not --
/// so every sparkle lands in the wrong place.
///
/// The most visible casualty is the shared <c>dark_overlay</c>, a single 360x169 sprite
/// blown up 8x that vignettes the scene. Laid out for the narrow rect it stops around
/// x=2890, which on a 3840 canvas leaves a hard vertical seam with the right of the
/// screen conspicuously unshaded.
///
/// The portrait fills its rect exactly in both cases, so mapping one space onto the
/// other is a uniform scale plus a vertical recentre -- the same transform the artwork
/// itself went through.
/// </summary>
[HarmonyPatch(typeof(NEventLayout), "AddVfxAnchoredToPortrait")]
internal static class EventVfxPatch
{
    private const string BasePosMeta = "uw_vfx_base_pos";
    private const string BaseScaleMeta = "uw_vfx_base_scale";

    [HarmonyPostfix]
    private static void Postfix(NEventLayout __instance, Node? vfx)
    {
        if (vfx is not Node2D node)
            return;

        // Two frames: the caller assigns the VFX's real position *after* this method
        // returns, and the portrait is itself resized on a deferred call.
        Callable.From(() => Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(__instance) || !GodotObject.IsInstanceValid(node))
                return;
            if (__instance.GetNodeOrNull<Control>("%Portrait") is not TextureRect portrait)
                return;

            Remap(portrait, node);
        }).CallDeferred()).CallDeferred();
    }

    /// <summary>Re-applies the mapping to every VFX already hanging off the portrait.</summary>
    internal static void RemapChildren(TextureRect portrait)
    {
        foreach (Node child in portrait.GetChildren())
        {
            if (child is Node2D node)
                Remap(portrait, node);
        }
    }

    private static void Capture(Node2D node)
    {
        node.SetMeta(BasePosMeta, node.Position);
        node.SetMeta(BaseScaleMeta, node.Scale);
    }

    private static void Remap(TextureRect portrait, Node2D node)
    {
        if (!portrait.HasMeta(EventPortraitPatch.DesignRectMeta))
            return; // the portrait was left alone, so the VFX are already correct

        if (!node.HasMeta(BasePosMeta))
            Capture(node);

        Vector2 design = portrait.GetMeta(EventPortraitPatch.DesignRectMeta).AsVector2();
        if (design.X <= 0f || design.Y <= 0f)
            return;

        // Uniform, because KeepAspectCovered scales both axes by whichever factor is
        // larger -- here always the horizontal one, since the rect got wider and shorter.
        float scale = portrait.Size.X / design.X;
        float recentre = (portrait.Size.Y - design.Y * scale) * 0.5f;

        Vector2 basePos = node.GetMeta(BasePosMeta).AsVector2();
        Vector2 baseScale = node.GetMeta(BaseScaleMeta).AsVector2();

        node.Position = new Vector2(basePos.X * scale, basePos.Y * scale + recentre);
        node.Scale = baseScale * scale;

        LocaliseParticles(node);
    }

    /// <summary>
    /// Puts every particle emitter under <paramref name="node"/> into local space.
    ///
    /// Particle quads are sized and placed in global units by default, so an emitter
    /// ignores the scale of the node it hangs from: growing the VFX root moves the
    /// emitters apart but leaves each sprite its original size. The dark overlay is a
    /// single 360x169 sprite blown up to a fixed 2880 canvas units that way, which is
    /// what leaves the hard-edged unshaded strip once the canvas is wider than that.
    ///
    /// Switching to local coordinates makes the particles inherit the node transform,
    /// so the one scale applied above covers sprite size, emission shape and all.
    /// These emitters never move, which is the case the global-space default exists for.
    /// </summary>
    private static void LocaliseParticles(Node node)
    {
        if (node is GpuParticles2D particles && !particles.LocalCoords)
            particles.LocalCoords = true;

        foreach (Node child in node.GetChildren())
            LocaliseParticles(child);
    }
}
