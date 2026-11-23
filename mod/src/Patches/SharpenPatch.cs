using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Recovers some of the sharpness lost to widening.
///
/// Covering a wider canvas means scaling the background art up, and Godot resamples it
/// bilinearly, which is the main reason an enlarged background reads as mushy. A contrast
/// adaptive sharpening pass over the room costs almost nothing and puts much of that
/// definition back.
///
/// It is applied to the room, not the whole frame: the pass sits between the room and the
/// run HUD, so backgrounds and creatures are sharpened while text, cards and icons - which
/// are drawn at native resolution and need no help - are left exactly as the game drew
/// them. The filter is contrast adaptive, so it works hardest on the flat, soft regions
/// and backs off around edges that are already crisp rather than ringing them.
/// </summary>
internal static class SharpenPatch
{
    private const string ShaderCode = @"
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float sharpness : hint_range(0.0, 1.0) = 0.35;

// Sampling radius in screen pixels. A one-pixel kernel cannot see blur that is spread
// over two or three, which is exactly what upscaling the background produces, so the
// radius is matched to how far the art was enlarged.
uniform float radius : hint_range(1.0, 4.0) = 1.0;

// AMD FidelityFX Contrast Adaptive Sharpening.
void fragment() {
    vec2 ts = radius / vec2(textureSize(screen_tex, 0));
    vec2 uv = SCREEN_UV;

    vec3 a = texture(screen_tex, uv + vec2(-ts.x, -ts.y)).rgb;
    vec3 b = texture(screen_tex, uv + vec2(  0.0, -ts.y)).rgb;
    vec3 c = texture(screen_tex, uv + vec2( ts.x, -ts.y)).rgb;
    vec3 d = texture(screen_tex, uv + vec2(-ts.x,   0.0)).rgb;
    vec3 e = texture(screen_tex, uv).rgb;
    vec3 f = texture(screen_tex, uv + vec2( ts.x,   0.0)).rgb;
    vec3 g = texture(screen_tex, uv + vec2(-ts.x,  ts.y)).rgb;
    vec3 h = texture(screen_tex, uv + vec2(  0.0,  ts.y)).rgb;
    vec3 i = texture(screen_tex, uv + vec2( ts.x,  ts.y)).rgb;

    vec3 mn = min(min(min(d, e), min(f, b)), h);
    mn += min(mn, min(min(a, c), min(g, i)));

    vec3 mx = max(max(max(d, e), max(f, b)), h);
    mx += max(mx, max(max(a, c), max(g, i)));

    vec3 amp = sqrt(clamp(min(mn, 2.0 - mx) / max(mx, vec3(1e-5)), 0.0, 1.0));
    vec3 w = amp * (-1.0 / mix(10.0, 6.0, sharpness));
    vec3 rcp = 1.0 / (1.0 + 4.0 * w);

    COLOR = vec4(clamp((b * w + d * w + f * w + h * w + e) * rcp, 0.0, 1.0), 1.0);
}
";

    [HarmonyPatch(typeof(NRun), "_Ready")]
    [HarmonyPostfix]
    private static void NRun_Ready(NRun __instance)
    {
        if (UltrawideConfig.Sharpness <= 0f)
            return;

        // Only worth doing where the art is actually being enlarged.
        if (Ultrawide.WideFactor(__instance.GetWindow()) <= 1.0001f)
            return;

        if (__instance.GetNodeOrNull("UltrawideSharpen") != null)
            return;

        Control? room = __instance.GetNodeOrNull<Control>("%RoomContainer");
        if (room == null)
        {
            Log.Warn("[Ultrawide] RoomContainer not found; sharpening skipped.");
            return;
        }

        var shader = new Shader { Code = ShaderCode };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("sharpness", UltrawideConfig.Sharpness);

        // Match the kernel to how far the art is actually being stretched, so the filter
        // works at the scale the softness lives at rather than one pixel below it.
        float radius = Mathf.Clamp(Ultrawide.WideFactor(__instance.GetWindow()) * 1.6f, 1f, 4f);
        material.SetShaderParameter("radius", radius);

        // Sized explicitly rather than by anchors: this Control hangs off a
        // BackBufferCopy, which is a Node2D, so there is no parent Control rect for
        // anchors to resolve against and it would stay zero-sized and draw nothing.
        // The game sizes its own radial-blur rect the same way.
        Vector2 canvas = __instance.GetViewportRect().Size;

        var overlay = new ColorRect
        {
            Name = "Pass",
            Material = material,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = canvas,
            Size = canvas,
            Position = Vector2.Zero,
        };

        // BackBufferCopy is what makes the frame so far readable as a screen texture;
        // the game uses the same pairing for its own radial blur.
        var buffer = new BackBufferCopy
        {
            Name = "UltrawideSharpen",
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport,
        };
        buffer.AddChild(overlay);

        __instance.AddChild(buffer);
        __instance.MoveChild(buffer, room.GetIndex() + 1);

        Log.Info("[Ultrawide] Sharpening pass enabled, strength " + UltrawideConfig.Sharpness
                 + ", radius " + radius.ToString("0.00") + " px.");
    }
}
