using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent; 
using XSkills;

[HarmonyPatch(typeof(XSkillsLeavesBehavior), "GetDrops")]
internal static class Patch_XSkillsLeavesBehavior_GetDrops
{
    public static void Postfix(IPlayer byPlayer, ref ItemStack[] __result)
    {
        if (__result == null || __result.Length == 0) return;

        var heldStack = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        bool holdingSaw = false;

        if (heldStack != null && heldStack.Collectible?.Code?.Path is string path)
        {
            holdingSaw = path.IndexOf("saw", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (holdingSaw) return;

        __result = __result
            .Where(stack => stack != null && !(stack.Collectible is BlockLeaves))
            .ToArray();
    }
}