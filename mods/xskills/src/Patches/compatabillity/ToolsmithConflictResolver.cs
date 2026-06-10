using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace XSkills
{
    public static class ToolsmithConflictResolver
    {
        public static bool TryPlaceOn_Prefix(Item __instance, ItemStack stack, BlockEntityAnvil beAnvil, ref ItemStack __result)
        {
            var xSkillsBehavior = stack.Collectible.GetBehavior<MetalBitAnvilBehavior>();
            if (xSkillsBehavior != null)
            {
                __result = xSkillsBehavior.TryPlaceOn(stack, beAnvil);
                return false;
            }
            return true;
        }

        public static bool GetMatchingRecipes_Prefix(Item __instance, ItemStack stack, ref List<SmithingRecipe> __result)
        {
            var xSkillsBehavior = stack.Collectible.GetBehavior<MetalBitAnvilBehavior>();
            if (xSkillsBehavior != null)
            {
                __result = xSkillsBehavior.GetMatchingRecipes(stack);
                return false; // Отключаем логику поиска рецептов Toolsmith
            }
            return true;
        }

        public static bool GetRequiredAnvilTier_Prefix(Item __instance, ItemStack stack, ref int __result)
        {
            var xSkillsBehavior = stack.Collectible.GetBehavior<MetalBitAnvilBehavior>();
            if (xSkillsBehavior != null)
            {
                __result = xSkillsBehavior.GetRequiredAnvilTier(stack);
                return false;
            }
            return true;
        }

        public static bool CanWork_Prefix(Item __instance, ItemStack stack, ref bool __result)
        {
            var xSkillsBehavior = stack.Collectible.GetBehavior<MetalBitAnvilBehavior>();
            if (xSkillsBehavior != null)
            {
                __result = xSkillsBehavior.CanWork(stack);
                return false;
            }
            return true;
        }
    }
}