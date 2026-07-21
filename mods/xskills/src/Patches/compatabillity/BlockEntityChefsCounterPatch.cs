using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Applies the process-side cooking perks to the Blush and Bins chef's counter.
    ///
    /// The counter cooks meals through a vanilla cooking pot (BlockCookingContainer.DoSmelt)
    /// on an InventorySmelting-derived inventory, so once the block carries the XskillsOwnable
    /// behavior (added in assets/xskills/patches/cooking.json) the existing generic patches
    /// already grant XP, meal quality, Canteen Cook serving size, Well Done, Gourmet, Happy Meal,
    /// Dilution, Desalinate and the Egg Timer notification. Owner assignment is handled by
    /// <see cref="InventorySmeltingPatch"/> on inventory open.
    ///
    /// The only perks left are the two that ride on block/slot specific hooks the counter
    /// reimplements instead of inheriting: Fast Food cook speed (its own MaxCookingTime) and the
    /// Canteen Cook cooking-slot stack size (its burner uses plain ItemSlotWatertight cooking
    /// slots rather than XSkills' ItemSlotCooking). This patch closes those two, mirroring
    /// <see cref="BlockEntityOvenCookingTopPatch"/>.
    /// </summary>
    public class BlockEntityChefsCounterPatch : ManualPatch
    {
        // Concrete type name of the counter's burner inventory. Matched by name so XSkills needs
        // no compile-time reference to Blush and Bins. Its InventoryClassName is the inherited
        // "smelting", so that cannot be used to tell it apart from a firepit.
        private const string BurnerInventoryTypeName = "InventoryChefBurner";

        public static void Apply(Harmony harmony, Type chefsCounterType, XSkills xSkills)
        {
            if (xSkills == null || chefsCounterType == null) return;
            xSkills.Skills.TryGetValue("cooking", out Skill skill);
            Cooking cooking = skill as Cooking;
            if (!(cooking?.Enabled ?? false)) return;

            Type patch = typeof(BlockEntityChefsCounterPatch);

            // Canteen Cook: enlarge the burner's cooking slots for the counter's owner.
            var maxStackGetter = typeof(ItemSlot).GetProperty("MaxSlotStackSize").GetGetMethod();
            harmony.Patch(maxStackGetter, null,
                new HarmonyMethod(patch.GetMethod(nameof(MaxSlotStackSizePostfix))));

            // Fast Food (and Well Done): scale the counter's cook time.
            PatchMethod(harmony, chefsCounterType, patch, "MaxCookingTime");
            // Keep the burner GUI cook-time slider in sync with the scaled time.
            PatchMethod(harmony, chefsCounterType, patch, "SetBurnerDialogValues");
        }

        private static bool IsChefBurner(InventoryBase inventory)
        {
            return inventory != null && inventory.GetType().Name == BurnerInventoryTypeName;
        }

        /// <summary>
        /// Canteen Cook: increases the max stack size of the burner's cooking slots (index 3+)
        /// for the counter's owner. The serving size of the finished meal is already handled by
        /// <see cref="BlockCookingContainerPatch"/>.
        /// </summary>
        public static void MaxSlotStackSizePostfix(ItemSlot __instance, ref int __result)
        {
            if (!IsChefBurner(__instance.Inventory)) return;

            // Slots 0-2 are fuel/input/output; only the cooking slots (3+) hold ingredients.
            int slotId = __instance.Inventory.GetSlotId(__instance);
            if (slotId < 3) return;

            ICoreAPI api = __instance.Inventory.Api;
            if (api?.World == null) return;

            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(__instance.Inventory.Pos);
            EntityPlayer player = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>()?.Owner?.Entity;
            if (player == null) return;

            Cooking cooking = api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerAbility ability = player.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.CanteenCookId];
            if (ability == null) return;

            __result = (int)(__result * (1.0f + ability.FValue(0)));
        }

        /// <summary>
        /// Fast Food / Well Done: scales the counter's cooking time by the same multiplier the
        /// firepit and oven use.
        /// </summary>
        public static void MaxCookingTimePostfix(BlockEntity __instance, ref float __result)
        {
            __result *= CookingUtil.GetCookingTimeMultiplier(__instance);
        }

        /// <summary>
        /// Keeps the burner dialog's cook-time slider consistent with the scaled cooking time.
        /// </summary>
        public static void SetBurnerDialogValuesPostfix(BlockEntity __instance, ITreeAttribute dialogTree)
        {
            if (!dialogTree.HasAttribute("maxOreCookingTime")) return;
            float maxTime = dialogTree.GetFloat("maxOreCookingTime");
            dialogTree.SetFloat("maxOreCookingTime", maxTime * CookingUtil.GetCookingTimeMultiplier(__instance));
        }
    }//!class BlockEntityChefsCounterPatch
}//!namespace XSkills
