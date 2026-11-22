using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Raises the 21:9 content-scale ceiling.
///
/// Both OnWindowChange methods consist of nothing but the aspect-ratio branch, so we
/// replace them outright with a prefix rather than correcting them afterwards. Running
/// as a postfix would let vanilla write its 2580x1080 clamp first; that write fires
/// SizeChanged, re-entering the handler, and the two values ping-pong until the GPU
/// runs out of memory.
/// </summary>
internal static class ContentScalePatches
{
    [HarmonyPatch(typeof(NGlobalUi), "OnWindowChange")]
    [HarmonyPrefix]
    private static bool NGlobalUi_OnWindowChange(NGlobalUi __instance)
    {
        if (SaveManager.Instance?.SettingsSave?.AspectRatioSetting != AspectRatioSetting.Auto)
            return true; // explicit ratio: vanilla's method is a no-op anyway

        Ultrawide.Apply(__instance.GetWindow());
        return false;
    }

    [HarmonyPatch(typeof(NMainMenu), "OnWindowChange")]
    [HarmonyPrefix]
    private static bool NMainMenu_OnWindowChange(NMainMenu __instance, bool isAspectRatioAuto)
    {
        if (!isAspectRatioAuto)
            return true;

        Ultrawide.Apply(__instance.GetWindow());
        return false;
    }

    /// <summary>
    /// ApplyDisplaySettings only sets ContentScaleAspect for non-Auto ratios, so switching
    /// to Auto leaves the previous mode in place until something resizes the window. It
    /// emits WindowChange for Auto, but the main menu handler is not always connected yet
    /// at that point, so settle the value here too.
    /// </summary>
    [HarmonyPatch(typeof(NGame), "ApplyDisplaySettings")]
    [HarmonyPostfix]
    private static void NGame_ApplyDisplaySettings(NGame __instance)
    {
        if (SaveManager.Instance?.SettingsSave?.AspectRatioSetting != AspectRatioSetting.Auto)
            return;

        Ultrawide.Apply(__instance.GetWindow());
    }
}
