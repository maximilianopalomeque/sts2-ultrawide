using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps saves in the vanilla directory while only cosmetic mods are loaded.
///
/// Vanilla routes all saves into a separate "modded" account directory the moment
/// any mod loads, which would hide the player's existing runs behind a purely
/// visual mod. We return the vanilla directory instead - but only while every
/// loaded mod declares affects_gameplay=false. As soon as anything that can touch
/// gameplay is present, MegaCrit's isolation is restored untouched.
/// </summary>
[HarmonyPatch(typeof(UserDataPathProvider), nameof(UserDataPathProvider.GetAccountDir))]
internal static class SavePathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(bool? forceModState, ref string __result)
    {
        // An explicit override means the caller knows what it wants; don't interfere.
        if (forceModState.HasValue)
            return true;

        List<string>? gameplayMods = ModManager.GetGameplayRelevantModNameList();
        if (gameplayMods != null && gameplayMods.Count > 0)
            return true; // a gameplay mod is loaded - keep saves isolated

        __result = string.Empty;
        return false;
    }
}
