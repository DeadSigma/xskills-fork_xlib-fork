using System;
using System.Collections.Generic;
using ACulinaryArtillery;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// The patch for the BlockEntityMixingBowl class.
    /// Compatibility for the ACulinaryArtillery mod.
    /// </summary>
    /// <seealso cref="XSkills.ManualPatch" />
    public class BlockEntityMixingBowlPatch : ManualPatch
    {
        public static void Apply(Harmony harmony, Type type, XSkills xSkills)
        {
            if (xSkills == null) return;
            Skill skill;
            xSkills.Skills.TryGetValue("cooking", out skill);
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return;
            Type patch = typeof(BlockEntityMixingBowlPatch);

            if (cooking[cooking.CanteenCookId].Enabled)
            {
                PatchMethod(harmony, type, patch, "GetMatchingMixingRecipe");
            }
            PatchMethod(harmony, type, patch, "mixInput");

            InventoryMixingBowlPatch.Apply(harmony, typeof(InventoryMixingBowl), xSkills);
            ItemSlotMixingBowlPatch.Apply(harmony, typeof(ItemSlotMixingBowl), xSkills);
        }

        public static void GetMatchingMixingRecipePrefix(BlockEntityMixingBowl __instance, out int __state)
        {
            __state = __instance.Pot?.MaxServingSize ?? 0;
            IPlayer player = ResolvePlayer(__instance);
            if (player?.Entity == null || __state == 0) return;

            Cooking cooking = __instance.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerSkill skill = player.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id];
            PlayerAbility ability = skill?[cooking.CanteenCookId];
            if (ability != null) __instance.Pot.MaxServingSize += (int)(__instance.Pot.MaxServingSize * ability.FValue(0));
        }

        public static void GetMatchingMixingRecipePostfix(BlockEntityMixingBowl __instance, int __state)
        {
            if (__instance.Pot != null) __instance.Pot.MaxServingSize = __state;
        }

        public static void mixInputPrefix(out CookingState __state, BlockEntityMixingBowl __instance)
        {
            InventoryBase inv = __instance.Inventory;
            List<ItemStack> stacks = new List<ItemStack>();
            __state = new CookingState();
            if (inv == null) return;

            __state.quality = inv[1].Itemstack?.Attributes.GetFloat("quality") ?? 0.0f;
            __state.outputStackSize = __instance.OutputStack?.StackSize ?? 0;

            for (int ii = 2; ii <= 7; ++ii)
            {
                if (!inv[ii].Empty) stacks.Add(inv[ii].Itemstack);
            }
            if (stacks.Count > 0) __state.stacks = stacks.ToArray();
        }

        public static void mixInputPostfix(CookingState __state, BlockEntityMixingBowl __instance)
        {
            if (__instance.Api.Side != EnumAppSide.Server) return;

            IPlayer byPlayer = ResolvePlayer(__instance);
            if (byPlayer?.Entity == null) return;

            Cooking cooking = byPlayer.Entity.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            ItemSlot outputSlot = __instance.OutputSlot;
            ItemStack outputStack = outputSlot?.Itemstack;
            int cooked = (outputStack?.StackSize ?? 0) - __state.outputStackSize;
            if (cooked <= 0) return;

            bool isContainer = outputStack.Collectible is IBlockMealContainer
                               || outputStack.Collectible is BlockLiquidContainerBase;

            // качество/свежесть/опыт; для контейнеров (суп/жидкость) тут же растут порции/объём
            cooking.ApplyAbilities(outputSlot, byPlayer, __state.quality, cooked, __state.stacks);

            // контейнеры обрабатываются внутри ApplyAbilities на месте - стак не трогаем
            if (isContainer || outputSlot.Itemstack == null) return;

            // штучные предметы / тесто: считаем бонус сами и кладём прямо в слот
            PlayerAbility dilution = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.DilutionId];
            if (dilution == null || dilution.Tier <= 0) return;

            int baseTotal = __state.outputStackSize + cooked;
            float bonusF = cooked * dilution.SkillDependentFValue();
            int bonus = (int)bonusF + (__instance.Api.World.Rand.NextDouble() < (bonusF - (int)bonusF) ? 1 : 0);

            int target = baseTotal + bonus;
            if (outputSlot.MaxSlotStackSize < target) outputSlot.MaxSlotStackSize = target;
            outputSlot.Itemstack.StackSize = target;
            outputSlot.MarkDirty();

        }

        /// <summary>
        /// Returns the player who is currently mixing the bowl (read from the private
        /// playersMixing field), used as a fallback when no Owner is set.
        /// </summary>
        private static IPlayer ResolvePlayer(BlockEntityMixingBowl be)
        {

            // штатный владелец (XskillsOwnable)
            IPlayer p = be.GetBehavior<BlockEntityBehaviorOwnable>()?.Owner;
            if (p?.Entity != null) { return p; }

            //тот, кто сейчас крутит миску (приватное поле ACA)
            var trav = Traverse.Create(be).Field("playersMixing");
            var playersMixing = trav.GetValue<Dictionary<string, long>>();
            if (playersMixing != null)
                foreach (var uid in playersMixing.Keys)
                {
                    IPlayer plr = be.Api.World.PlayerByUid(uid);
                    if (plr?.Entity != null) { return plr; }
                }

            // тот, у кого открыт GUI миски
            var opened = be.Inventory?.openedByPlayerGUIds;
            if (opened != null)
                foreach (var uid in opened)
                {
                    IPlayer plr = be.Api.World.PlayerByUid(uid);
                    if (plr?.Entity != null) { return plr; }
                }

            return null;
        }
    }//!class BlockEntityMixingBowlPatch
}//!namespace XSkills