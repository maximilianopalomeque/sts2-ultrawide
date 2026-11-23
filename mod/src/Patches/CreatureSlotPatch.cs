using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Sts2Ultrawide.Patches;

/// <summary>
/// Keeps slot-placed enemies under their own artwork.
///
/// Most enemies carry their own sprite, so wherever the game puts the creature node is
/// where the enemy appears and its health bar follows. A handful of bosses are drawn in
/// the room background instead -- the Kaiser Crab's two claws are one shared Spine file
/// living in the scenery -- and the creature nodes only mark where the game thinks they
/// are, placed at Marker2Ds in a separate encounter scene.
///
/// Those two things are in different containers, and only the background is grown to
/// cover a wider canvas. The claws therefore travel out to the edges of the screen
/// while their health bars and intents stay behind in the middle, which is what makes
/// this one fight look broken when every other one is fine.
///
/// Applying the background's own transform to the markers puts them back together. Read
/// from the marker every time rather than nudging the creature, so repeated calls land
/// in the same place.
/// </summary>
internal static class CreatureSlotPatch
{
    [HarmonyPatch(typeof(NCombatRoom), "PositionCreaturesWithSlots")]
    [HarmonyPostfix]
    private static void AfterInitialPlacement(NCombatRoom __instance)
        => Callable.From(() => Reposition(__instance)).CallDeferred();

    [HarmonyPatch(typeof(NCombatRoom), "AddCreature")]
    [HarmonyPostfix]
    private static void AfterAddCreature(NCombatRoom __instance)
        => Callable.From(() => Reposition(__instance)).CallDeferred();

    private static void Reposition(NCombatRoom room)
    {
        if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
            return;

        float factor = Ultrawide.WideFactor(room.GetTree()?.Root) * UltrawideConfig.BackgroundScaleMultiplier;
        if (factor <= 1.0001f)
            return;

        var slots = (Control?)AccessTools.Property(typeof(NCombatRoom), "EncounterSlots")?.GetValue(room);
        if (slots == null || !GodotObject.IsInstanceValid(slots))
            return;

        // The same anchor the background is grown from, so the two move as one.
        Vector2 canvas = room.GetViewportRect().Size;
        var anchor = new Vector2(canvas.X * 0.5f, canvas.Y * UltrawideConfig.BackgroundVerticalAnchor);

        if (AccessTools.Field(typeof(NCombatRoom), "_creatureNodes")?.GetValue(room)
            is not System.Collections.IEnumerable nodes)
            return;

        foreach (object? node in nodes)
        {
            if (node is NCreature creature && GodotObject.IsInstanceValid(creature))
                Place(creature, slots, anchor, factor);
        }
    }

    private static void Place(NCreature creature, Control slots, Vector2 anchor, float factor)
    {
        string? slotName = creature.Entity?.SlotName;
        if (string.IsNullOrEmpty(slotName))
            return; // positioned by the game, not by a marker

        if (slots.GetNodeOrNull<Marker2D>(slotName) is not { } marker)
            return;

        creature.GlobalPosition = anchor + (marker.GlobalPosition - anchor) * factor;
    }
}
