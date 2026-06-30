using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// The patch for the BlockCookingContainer class.
    /// Most methods here manipulate the MaxServingSize field.
    /// Since the GetMatchingCookingRecipe method can not be 
    /// patched due to the lack of access to the player all methods 
    /// that use GetMatchingCookingRecipe must be patched.
    /// </summary>
    [HarmonyPatch(typeof(BlockCookingContainer))]
    public class BlockCookingContainerPatch
    {
        /// <summary>
        /// Prepares the Harmony patch.
        /// Only patches the methods if necessary.
        /// </summary>
        /// <param name="original">The method to be patched.</param>
        /// <returns>whether the method should be patched.</returns>
        public static bool Prepare(MethodBase original)
        {
            XSkills xSkills = XSkills.Instance;
            if (xSkills == null) return false;
            Skill skill;
            xSkills.Skills.TryGetValue("cooking", out skill);
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return false;
            if (original == null) return true;

            switch (original.Name)
            {
                case "CanSmelt":
                    return
                        cooking[cooking.DesalinateId].Enabled ||
                        cooking[cooking.CanteenCookId].Enabled;
                case "DoSmelt":
                    // Обязательно патчим DoSmelt, чтобы всегда вызывать ApplyAbilities 
                    // и выдавать базовый опыт, если навык Cooking в целом включен.
                    return true;
                case "GetOutputText":
                    // GetOutputText нужен в основном для отображения измененного размера порции (CanteenCook)
                    return cooking[cooking.CanteenCookId].Enabled;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Prefix for the CanSmelt method.
        /// Temporarily increases MaxServingSize for the Canteen Cook ability.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("CanSmelt")]
        public static void CanSmeltPrefix(BlockCookingContainer __instance, out int __state, ISlotProvider cookingSlotsProvider)
        {
            __state = CookingUtil.SetMaxServingSize(__instance, cookingSlotsProvider);
        }

        /// <summary>
        /// Postfix for the CanSmelt method.
        /// Restores MaxServingSize and applies the Desalination lock check.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("CanSmelt")]
        public static void CanSmeltPostfix(BlockCookingContainer __instance, ref bool __result, int __state, IWorldAccessor world, ISlotProvider cookingSlotsProvider)
        {
            // Возвращаем размер порции к ванильному значению
            __instance.MaxServingSize = __state;

            if (!__result) return;

            // Проверка лока на опреснение (Desalinate)
            BlockEntityBehaviorOwnable ownable = CookingUtil.GetOwnableFromInventory(cookingSlotsProvider as InventoryBase);
            IPlayer player = ownable?.Owner;
            if (player == null) return;

            Cooking cooking = world.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerSkill playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id];
            if (playerSkill == null) return;

            ItemStack[] stacks = __instance.GetCookingStacks(cookingSlotsProvider, false);
            CookingRecipe recipe = __instance.GetMatchingCookingRecipe(world, stacks, out _);

            if (recipe != null && (recipe.Code == "salt" || recipe.Code == "lime"))
            {
                CookingSkillConfig config = cooking.Config as CookingSkillConfig;
                bool bypass = config?.bypassDesalinationLock ?? false;
                bool isSkillEnabled = cooking[cooking.DesalinateId]?.Enabled ?? false;
                PlayerAbility playerAbility = playerSkill[cooking.DesalinateId];

                // Если перк не вкачан, мы принудительно запрещаем готовку, перекрывая решение ванильной игры
                if (!bypass && isSkillEnabled && (playerAbility == null || playerAbility.Tier <= 0))
                {
                    __result = false;
                }
            }
        }

        /// <summary>
        /// Prefix for the DoSmelt method.
        /// Saves the old value of the MaxServingSize value 
        /// to reset it later. 
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="__state">The state.</param>
        /// <param name="cookingSlotsProvider">The cooking slots provider.</param>
        [HarmonyPrefix]
        [HarmonyPatch("DoSmelt")]
        public static void DoSmeltPrefix(BlockCookingContainer __instance, out int __state, ISlotProvider cookingSlotsProvider)
        {
            __state = CookingUtil.SetMaxServingSize(__instance, cookingSlotsProvider);
        }

        /// <summary>
        /// Postfix for the DoSmelt method.
        /// Applies cooking abilities to the cooked item.
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="__state">The state.</param>
        /// <param name="cookingSlotsProvider">The cooking slots provider.</param>
        /// <param name="outputSlot">The output slot.</param>
        [HarmonyPostfix]
        [HarmonyPatch("DoSmelt")]
        public static void DoSmeltPostfix(BlockCookingContainer __instance, int __state, ISlotProvider cookingSlotsProvider, ItemSlot outputSlot)
        {
            __instance.MaxServingSize = __state;
            IPlayer player = CookingUtil.GetOwnerFromInventory(cookingSlotsProvider as InventoryBase);
            if (player?.Entity == null) return;

            Cooking cooking = player.Entity.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            //  Собираем массив исходных ингредиентов
            ItemStack[] sourceStacks = __instance.GetCookingStacks(cookingSlotsProvider, false);

            // Вызываем ApplyAbilities с полным набором из 6 аргументов
            if (outputSlot?.Itemstack != null)
            {
                float cookedAmount = outputSlot.Itemstack.StackSize;
                cooking.ApplyAbilities(outputSlot, player, 0.0f, cookedAmount, sourceStacks, 1.0f);
            }
            else if (cookingSlotsProvider?.Slots?[0].Itemstack != null)
            {
                float cookedAmount = cookingSlotsProvider.Slots[0]?.StackSize ?? 1.0f;
                cooking.ApplyAbilities(cookingSlotsProvider.Slots[0], player, 0.0f, cookedAmount, sourceStacks, 1.0f);
            }
        }

        /// <summary>
        /// Prefix for the GetOutputText method.
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="__state">The state.</param>
        /// <param name="cookingSlotsProvider">The cooking slots provider.</param>
        [HarmonyPrefix]
        [HarmonyPatch("GetOutputText")]
        public static void GetOutputTextPrefix(BlockCookingContainer __instance, out int __state, ISlotProvider cookingSlotsProvider)
        {
            __state = CookingUtil.SetMaxServingSize(__instance, cookingSlotsProvider);
        }

        /// <summary>
        /// Postfix for the GetOutputText method.
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="__state">The state.</param>
        [HarmonyPostfix]
        [HarmonyPatch("GetOutputText")]
        public static void GetOutputTextPostfix(BlockCookingContainer __instance, int __state)
        {
            __instance.MaxServingSize = __state;
        }
    }//!BlockCookingContainerPatch
}//!namespace XSkills
