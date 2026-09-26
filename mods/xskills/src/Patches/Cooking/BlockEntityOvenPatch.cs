using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Глиняная печь дополняется механиками кулинарии
    /// </summary>
    public class BlockEntityOvenPatch : ManualPatch
    {
        /// <summary>
        /// Патчи печи применяются при включённом навыке кулинарии
        /// </summary>
        public static void Apply(Harmony harmony, Type ovenType, XSkills xSkills)
        {
            if (xSkills == null) return;

            xSkills.Skills.TryGetValue("cooking", out Skill skill);
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return;

            Type patch = typeof(BlockEntityOvenPatch);
            PatchMethod(harmony, ovenType, patch, "GetBlockInfo");
            PatchMethod(harmony, ovenType, patch, "OnInteract");
            PatchMethod(harmony, ovenType, patch, "IncrementallyBake");
        }

        /// <summary>
        /// Владелец печи обновляется при взаимодействии
        /// </summary>
        public static void OnInteractPostfix(BlockEntity __instance, IPlayer byPlayer)
        {
            BlockEntityBehaviorOwnable ownable = __instance.GetBehavior<BlockEntityBehaviorOwnable>();
            if (ownable == null) return;

            ownable.Owner = byPlayer;
        }

        /// <summary>
        /// Исходный предмет сохраняется до смены стадии выпечки
        /// </summary>
        public static void IncrementallyBakePrefix(
            InventoryOven ___ovenInv,
            ref CookingState __state,
            int slotIndex)
        {
            ItemStack sourceStack = ___ovenInv?[slotIndex]?.Itemstack;

            __state = new CookingState
            {
                quality = sourceStack?.Attributes.GetFloat("quality") ?? 0.0f,
                stacks = sourceStack == null
                    ? Array.Empty<ItemStack>()
                    : new ItemStack[] { sourceStack.Clone() }
            };
        }

        /// <summary>
        /// Навыки кулинарии применяются после смены стадии выпечки
        /// </summary>
        public static void IncrementallyBakePostfix(
            BlockEntity __instance,
            ref CookingState __state,
            InventoryOven ___ovenInv,
            int slotIndex)
        {
            IPlayer byPlayer = __instance.GetBehavior<BlockEntityBehaviorOwnable>()?.Owner;
            if (byPlayer == null) return;

            ItemStack sourceStack = __state.stacks?.Length > 0 ? __state.stacks[0] : null;
            ItemStack outputStack = ___ovenInv?[slotIndex]?.Itemstack;
            if (sourceStack == null || outputStack == null) return;

            bool sameCollectible =
                sourceStack.Collectible?.Code?.Equals(outputStack.Collectible?.Code) == true;

            bool sameBakeLevel =
                sourceStack.Attributes.GetInt("bakeLevel") ==
                outputStack.Attributes.GetInt("bakeLevel");

            if (sameCollectible && sameBakeLevel) return;

            if (outputStack.StackSize < sourceStack.StackSize)
            {
                outputStack.StackSize = sourceStack.StackSize;
            }

            Cooking cooking = byPlayer.Entity?.Api.ModLoader
                .GetModSystem<XLeveling>()?
                .GetSkill("cooking") as Cooking;

            if (cooking == null) return;

            cooking.ApplyAbilities(
                ___ovenInv[slotIndex],
                byPlayer,
                __state.quality,
                2.0f,
                __state.stacks
            );
        }

        /// <summary>
        /// Прогресс выпечки показывается повару
        /// </summary>
        public static void GetBlockInfoPostfix(
            BlockEntity __instance,
            InventoryOven ___ovenInv,
            OvenItemData[] ___bakingData,
            float ___fuelBurnTime,
            IPlayer forPlayer,
            StringBuilder sb)
        {
            Cooking cooking = XLeveling.Instance(__instance.Api)?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerAbility ability =
                forPlayer?.Entity?.GetBehavior<PlayerSkillSet>()?[cooking.Id][cooking.SpecialisationID];

            if (ability == null || ability.Tier < 1) return;

            if (___fuelBurnTime > 0.0f)
            {
                sb.AppendLine(string.Format("Burning: {0:N2} sec", ___fuelBurnTime));
            }

            for (int ii = 0; ii < ___bakingData.Length; ++ii)
            {
                if (___ovenInv[ii]?.Itemstack == null) continue;

                OvenItemData ovenData = ___bakingData[ii];
                BakingProperties props = BakingProperties.ReadFrom(___ovenInv[ii].Itemstack);
                if (props == null || ovenData == null) continue;

                float result = Math.Min(
                    (ovenData.BakedLevel - props.LevelFrom) / (props.LevelTo - props.LevelFrom),
                    1.0f
                );

                sb.AppendLine(Lang.Get("xskills:progress", result));
            }
        }
    }
}
