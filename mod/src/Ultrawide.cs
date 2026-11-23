using System;
using Godot;

namespace Sts2Ultrawide;

/// <summary>
/// Shared logic for widening the game's canvas past its built-in 21:9 ceiling.
///
/// Vanilla clamps any display wider than <see cref="VanillaMaxWideRatio"/> to a
/// content scale of 2580x1080 (MegaCrit.Sts2.Core.Helpers.DisplayVars.maxWideRatio).
/// That value is a compile-time const inlined at every call site, so it cannot be
/// patched directly - we replace the methods that apply it instead.
/// </summary>
internal static class Ultrawide
{
    /// <summary>Vanilla's wide ceiling: 2580/1080.</summary>
    public const float VanillaMaxWideRatio = 2.3888888f;

    /// <summary>Vanilla's narrow floor: 4:3.</summary>
    public const float VanillaMaxNarrowRatio = 1.3333334f;

    /// <summary>The canvas height the game is laid out against.</summary>
    public const int BaseHeight = 1080;

    /// <summary>
    /// Sanity cap so a bogus window size cannot produce an absurd canvas.
    /// 48:9 triple-monitor is 5.333, so this leaves headroom without being unbounded.
    /// </summary>
    public const float AbsoluteMaxRatio = 6.0f;

    /// <summary>
    /// Re-entrancy guard. Writing ContentScaleSize fires the viewport's SizeChanged
    /// signal synchronously, which lands right back in the handlers we patch. Without
    /// this the vanilla clamp and our widened value ping-pong forever, re-creating
    /// viewport textures until the GPU runs out of memory.
    /// </summary>
    [ThreadStatic] private static bool _applying;

    /// <summary>
    /// Applies the correct content scale for the window's true aspect ratio.
    /// Reproduces vanilla's narrow and normal branches exactly; only the wide branch
    /// differs, using the display's real ratio instead of clamping to 21:9.
    /// </summary>
    public static void Apply(Window? window)
    {
        if (window == null || _applying)
            return;

        Vector2I size = window.Size;
        if (size.X <= 0 || size.Y <= 0)
            return;

        float ratio = (float)size.X / size.Y;

        Window.ContentScaleAspectEnum aspect;
        Vector2I canvas;

        if (ratio > VanillaMaxWideRatio)
        {
            // The branch vanilla clamps. Use the display's actual ratio.
            float clamped = Math.Min(ratio, AbsoluteMaxRatio);
            aspect = Window.ContentScaleAspectEnum.Expand;
            canvas = new Vector2I((int)MathF.Round(BaseHeight * clamped), BaseHeight);
        }
        else if (ratio < VanillaMaxNarrowRatio)
        {
            aspect = Window.ContentScaleAspectEnum.KeepHeight;   // vanilla
            canvas = new Vector2I(1680, 1260);
        }
        else
        {
            aspect = Window.ContentScaleAspectEnum.Expand;       // vanilla
            canvas = new Vector2I(1680, 1080);
        }

        // Writing identical values still fires SizeChanged, so never write a no-op.
        if (window.ContentScaleAspect == aspect && window.ContentScaleSize == canvas)
            return;

        _applying = true;
        try
        {
            window.ContentScaleAspect = aspect;
            window.ContentScaleSize = canvas;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>The canvas width vanilla uses for its widest supported ratio.</summary>
    public const int VanillaWideCanvasWidth = 2580;

    /// <summary>
    /// How much wider our canvas is than the widest one vanilla would ever build.
    /// Background art is sized against that canvas, so multiplying its scale by this
    /// keeps the exact coverage the art had at 21:9 - no more, no less.
    /// </summary>
    public static float WideFactor(Window? window)
    {
        if (window == null)
            return 1f;

        // Derived from the window itself rather than ContentScaleSize: the background
        // nodes listen to the same SizeChanged signal we do and may well run first,
        // while the content scale still holds vanilla's clamped value.
        Vector2I size = window.Size;
        if (size.X <= 0 || size.Y <= 0)
            return 1f;

        float ratio = Math.Min((float)size.X / size.Y, AbsoluteMaxRatio);
        return ratio <= VanillaMaxWideRatio ? 1f : ratio / VanillaMaxWideRatio;
    }

    /// <summary>
    /// Multiplies a node's scale, growing it out from a chosen anchor point on the
    /// canvas rather than from its own top-left corner, so the artwork stays framed
    /// around the part that matters instead of drifting off to one side.
    /// </summary>
    public static void GrowToCoverCanvas(Control? node, float factor)
    {
        if (node == null || factor <= 1.0001f)
            return;

        // The viewport rect is the canvas, and is reliable even during _Ready, when a
        // parent Control may not have been laid out yet and still reports zero size.
        Vector2 canvas = node.GetViewportRect().Size;
        var anchor = new Vector2(canvas.X * 0.5f, canvas.Y * UltrawideConfig.BackgroundVerticalAnchor);
        node.PivotOffset = anchor - node.GlobalPosition;

        node.Scale *= factor * UltrawideConfig.BackgroundScaleMultiplier;
    }

    /// <summary>
    /// Scales one node up until it spans the canvas, and no further.
    ///
    /// Layers within a single background are authored at different widths, so one scale
    /// on their shared container keeps them proportionally different and the narrow ones
    /// still stop short - which reads as the ground simply ending partway across the
    /// screen. Measuring each layer where it actually lands and giving it only the factor
    /// it is missing covers them all without over-zooming the ones that were already fine.
    /// </summary>
    public static void EnsureCoversCanvas(Control? node)
    {
        if (node == null || !node.IsInsideTree())
            return;

        Vector2 canvas = node.GetViewportRect().Size;
        if (canvas.X <= 0 || canvas.Y <= 0)
            return;

        Transform2D world = node.GetGlobalTransform();
        float width = node.Size.X * world.Scale.X;
        if (width <= 1f)
            return; // nothing laid out yet, or not a sized layer

        float left = world.Origin.X;
        float right = left + width;
        float centre = canvas.X * 0.5f;

        // How much each side is short of the edge, scaling about the centre of the canvas.
        float needLeft = left > 0f ? (centre - 0f) / Math.Max(centre - left, 0.001f) : 1f;
        float needRight = right < canvas.X ? (canvas.X - centre) / Math.Max(right - centre, 0.001f) : 1f;
        float need = Math.Max(needLeft, needRight);
        if (need <= 1.0001f)
            return;

        need = Math.Min(need * UltrawideConfig.BackgroundScaleMultiplier, AbsoluteMaxRatio);

        var anchor = new Vector2(centre, canvas.Y * UltrawideConfig.BackgroundVerticalAnchor);
        node.PivotOffset = (anchor - world.Origin) / Math.Max(world.Scale.X, 0.001f);
        node.Scale *= need;
    }
}
