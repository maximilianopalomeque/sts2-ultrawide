using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Ultrawide;

/// <summary>
/// Optional tuning file at user://ultrawide.json. Every field has a sane default,
/// so the file is never required; it exists so values can be tried without a rebuild.
/// </summary>
internal static class UltrawideConfig
{
    public const string Path = "user://ultrawide.json";

    /// <summary>
    /// Where on screen the background scale is anchored vertically, 0 = top, 1 = bottom.
    /// Widening always costs vertical view, and these compositions put the horizon,
    /// ground and creatures along the bottom with spare sky above. Anchoring at the
    /// bottom keeps the lower edge exactly where it was and takes the whole loss out
    /// of the sky, which is the half nobody is looking at.
    /// </summary>
    public static float BackgroundVerticalAnchor = 1.0f;

    /// <summary>
    /// Same idea for Ancient and event illustrations, which need their own value.
    ///
    /// Room backgrounds put the ground along the bottom with spare sky above, so they
    /// want anchoring low. Event art is a single painting with its subject in the middle
    /// of the frame, and anchoring that low slices the subject's head off - so these
    /// default to centre and lose a little from each end instead.
    /// </summary>
    public static float AncientVerticalAnchor = 0.5f;

    /// <summary>
    /// Safety margin on the computed background scale - overscan, in broadcast terms.
    ///
    /// The computed factor is only exact if the art covered its 21:9 canvas exactly, and
    /// several rooms are a whisker short of that to begin with; scaling up carries the
    /// shortfall along with it and leaves a sliver of bare canvas at one edge. A few per
    /// cent of bleed costs almost nothing in crop and guarantees the edges are covered.
    /// </summary>
    public static float BackgroundScaleMultiplier = 1.10f;

    /// <summary>
    /// Whether each background layer is grown individually, on top of the single scale
    /// applied to the container they share.
    ///
    /// Layers within one background are authored at different widths, so a shared scale
    /// leaves the narrow ones short and the ground ends in a hard vertical line partway
    /// across the screen. Growing them individually closes that.
    ///
    /// The trade is that a few bosses keep decorative body parts in those layers - the
    /// Kaiser Crab's claws, for one - and growing the layer carries them further toward
    /// the edges, away from health bars the game already draws detached from them. Turn
    /// this off to keep that art exactly where vanilla puts it and accept the strip.
    /// </summary>
    public static bool LayerCoverage = true;

    /// <summary>
    /// Strength of the sharpening pass over the room, 0 to turn it off.
    ///
    /// Enlarging the background means Godot resamples it bilinearly, which is what makes
    /// a widened background look soft. A contrast adaptive pass puts much of that back.
    /// It only runs on displays wide enough that the art is actually being enlarged.
    /// </summary>
    public static float Sharpness = 0.6f;

    /// <summary>
    /// Widest aspect the HUD may spread across, as width/height, or 0 to let the HUD
    /// use the whole display. 2.389 is 21:9, the widest layout the game itself was
    /// designed for, which keeps the end-turn button and the energy orb within a glance
    /// of each other while the artwork still fills the screen behind them.
    /// </summary>
    public static float HudMaxRatio = 2.3888888f;

    public static void Load()
    {
        try
        {
            if (!FileAccess.FileExists(Path))
            {
                Log.Info("[Ultrawide] No " + Path + "; using defaults.");
                return;
            }

            using FileAccess file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            var json = Json.ParseString(file.GetAsText());
            if (json.VariantType != Variant.Type.Dictionary)
            {
                Log.Warn("[Ultrawide] " + Path + " is not a JSON object; using defaults.");
                return;
            }

            var dict = json.AsGodotDictionary();
            BackgroundVerticalAnchor = Read(dict, "background_vertical_anchor", BackgroundVerticalAnchor, 0f, 1f);
            BackgroundScaleMultiplier = Read(dict, "background_scale_multiplier", BackgroundScaleMultiplier, 0.5f, 3f);
            AncientVerticalAnchor = Read(dict, "ancient_vertical_anchor", AncientVerticalAnchor, 0f, 1f);
            HudMaxRatio = Read(dict, "hud_max_ratio", HudMaxRatio, 0f, 8f);
            Sharpness = Read(dict, "sharpness", Sharpness, 0f, 1f);
            LayerCoverage = ReadBool(dict, "layer_coverage", LayerCoverage);

            Log.Info("[Ultrawide] Config: vertical_anchor=" + BackgroundVerticalAnchor
                     + " scale_multiplier=" + BackgroundScaleMultiplier
                     + " ancient_anchor=" + AncientVerticalAnchor
                     + " hud_max_ratio=" + HudMaxRatio
                     + " sharpness=" + Sharpness
                     + " layer_coverage=" + LayerCoverage);
        }
        catch (Exception e)
        {
            Log.Warn("[Ultrawide] Could not read " + Path + ", using defaults: " + e.Message);
        }
    }

    private static bool ReadBool(Godot.Collections.Dictionary dict, string key, bool fallback)
        => dict.ContainsKey(key) ? dict[key].AsBool() : fallback;

    private static float Read(Godot.Collections.Dictionary dict, string key, float fallback, float min, float max)
    {
        if (!dict.ContainsKey(key))
            return fallback;
        float value = (float)dict[key].AsDouble();
        return Math.Clamp(value, min, max);
    }
}
