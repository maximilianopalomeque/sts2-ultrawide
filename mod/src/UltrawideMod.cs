using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2Ultrawide;

/// <summary>
/// Entry point. The game calls <see cref="Init"/> once, at load.
/// </summary>
[ModInitializer(nameof(Init))]
public static class UltrawideMod
{
    public const string ModId = "ultrawide";
    public const string HarmonyId = "maxipalomeque." + ModId;

    public static void Init()
    {
        UltrawideConfig.Load();

        Harmony harmony;
        try
        {
            harmony = new Harmony(HarmonyId);
        }
        catch (Exception e)
        {
            Log.Error("[Ultrawide] Could not create a Harmony instance, mod disabled: " + e);
            return;
        }

        // Patch one class at a time rather than PatchAll, so a single patch that no
        // longer matches the current game build disables only itself. This is what
        // keeps one build of the mod working across the stable and beta branches.
        int applied = 0;
        var failed = new List<string>();

        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.GetCustomAttribute<HarmonyPatch>() == null &&
                !HasPatchMethods(type))
                continue;

            try
            {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception e)
            {
                failed.Add(type.Name);
                Log.Warn("[Ultrawide] Patch '" + type.Name + "' did not apply on this game build "
                         + "and was skipped. The rest of the mod still works. Reason: " + e.Message);
            }
        }

        if (failed.Count == 0)
            Log.Info("[Ultrawide] Loaded. " + applied + " patch group(s) applied.");
        else
            Log.Warn("[Ultrawide] Loaded with " + applied + " patch group(s) applied and "
                     + failed.Count + " skipped: " + string.Join(", ", failed));
    }

    private static bool HasPatchMethods(Type type)
    {
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m.GetCustomAttribute<HarmonyPatch>() != null)
                return true;
        }
        return false;
    }
}
