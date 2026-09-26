using HarmonyLib;
using System;
using System.Linq;
using Vintagestory.API.Common;

namespace XLib.XLeveling
{
    /// <summary>
    /// Проверка черт рецепта расширяется с учётом extraTraits
    /// </summary>
    [HarmonyPatch(typeof(GridRecipe), nameof(GridRecipe.Matches))]
    public static class GridRecipeTraitPatch
    {
        /// <summary>
        /// Требование черты временно снимается, если она найдена в extraTraits
        /// </summary>
        public static void Prefix(GridRecipe __instance, IPlayer forPlayer, out string __state)
        {
            __state = null;

            string requiredTrait = __instance.RequiresTrait;
            if (requiredTrait == null || forPlayer?.Entity == null) return;

            string[] traits = forPlayer.Entity.WatchedAttributes.GetStringArray("traits");

            // Обычная черта обрабатывается игрой
            if (traits != null && traits.Contains(requiredTrait)) return;

            string[] extraTraits = forPlayer.Entity.WatchedAttributes.GetStringArray("extraTraits");
            if (extraTraits == null || !extraTraits.Contains(requiredTrait)) return;

            __state = requiredTrait;
            __instance.RequiresTrait = null;
        }

        /// <summary>
        /// Требование черты восстанавливается после проверки рецепта
        /// </summary>
        public static void Postfix(GridRecipe __instance, string __state)
        {
            if (__state != null)
            {
                __instance.RequiresTrait = __state;
            }
        }

        /// <summary>
        /// Требование черты восстанавливается при ошибке проверки
        /// </summary>
        public static Exception Finalizer(GridRecipe __instance, string __state, Exception __exception)
        {
            if (__state != null)
            {
                __instance.RequiresTrait = __state;
            }

            return __exception;
        }
    }
}