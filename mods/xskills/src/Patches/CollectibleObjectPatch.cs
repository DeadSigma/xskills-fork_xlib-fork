using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(CollectibleObject))]
    public class CollectibleObjectPatch
    {
        [ThreadStatic]
        private static IPlayer heldInteractPlayer;

        //public static bool Prepare(MethodBase original)
        //{
        //    XSkills xSkills = XSkills.Instance;
        //    if (xSkills == null) return false;

        //    if (original.Name == "TryMergeStacks")
        //    {
        //        return !xSkills.XLeveling.Config.mergeQualities;
        //    }
        //    else return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("SetTemperature")]
        public static void SetTemperaturePrefix(IWorldAccessor world, ItemStack itemstack, float temperature, bool delayCooldown = true)
        {
            if (itemstack?.Attributes == null) return;
        }

        [HarmonyPatch("OnBlockBrokenWith")]
        public static void Prefix(ref Block __state, IWorldAccessor world, BlockSelection blockSel)
        {
            __state = world.BlockAccessor.GetBlock(blockSel.Position);
        }

        [HarmonyPatch("OnBlockBrokenWith")]
        public static void Postfix(CollectibleObject __instance, Block __state, IWorldAccessor world, Entity byEntity, ItemSlot itemslot)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = (byEntity as EntityPlayer)?.Player;
            ItemStack itemstack = itemslot.Itemstack;
            if (itemstack == null || byPlayer == null) return;
            if (__state == null) return;
            DropBonusBehavior beh = null;

            foreach (BlockBehavior beh2 in __state.BlockBehaviors)
            {
                beh = beh2 as DropBonusBehavior;
                if (beh != null) break;
            }

            Item tool = itemstack.Item;
            if (tool == null || beh == null) return;
            if ((beh.Tool != tool.Tool) && !tool.Code.Path.Contains("paxel")) return;

            //durability
            if (__instance.DamagedBy != null && __instance.DamagedBy.Contains(EnumItemDamageSource.BlockBreaking))
            {
                //for multiplayer server the clients sometimes don't set the forestry skill properly
                //and i don't know why. This should fix it. 
                if (beh.Skill == null)
                {
                    if (beh is XSkillsCharcoalBehavior charcoalBehavior)
                    {
                        charcoalBehavior.Forestry = XLeveling.Instance(world.Api)?.GetSkill("forestry") as Forestry;
                    }
                    if (beh.Skill == null) return;
                }

                PlayerAbility playerAbility = byEntity.GetBehavior<PlayerSkillSet>()?[beh.Skill.Id]?[beh.Skill.DurabilityId];

                if (playerAbility != null && (playerAbility.SkillDependentFValue() >= world.Rand.NextDouble()))
                {
                    int leftDurability = itemstack.Attributes.GetInt("durability", __instance.GetMaxDurability(itemstack));
                    leftDurability += 1;
                    itemstack.Attributes.SetInt("durability", leftDurability);
                    itemslot.MarkDirty();
                }
            }
        }

        [HarmonyPatch("GetHeldItemInfo")]
        public static void Postfix(ItemSlot inSlot, StringBuilder dsc)
        {
            float quality = inSlot?.Itemstack?.Attributes.TryGetFloat("quality") ?? 0.0f;
            QualityUtil.AddQualityString(quality, dsc);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnHeldInteractStep")]
        public static void OnHeldInteractStepPrefix(out IPlayer __state, EntityAgent byEntity)
        {
            __state = heldInteractPlayer;
            heldInteractPlayer = (byEntity as EntityPlayer)?.Player;
        }

        [HarmonyFinalizer]
        [HarmonyPatch("OnHeldInteractStep")]
        public static Exception OnHeldInteractStepFinalizer(IPlayer __state, Exception __exception)
        {
            heldInteractPlayer = __state;
            return __exception;
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMaxDurability")]
        public static void Postfix0(ref int __result, ItemStack itemstack)
        {
            float quality = itemstack?.Attributes.TryGetFloat("quality") ?? 0.0f;
            if (quality > 0.0f && __result > 1) __result = (int)(__result * (1.0f + quality * 0.05f));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetAttackPower")]
        public static void Postfix1(ref float __result, ItemStack itemStack)
        {
            float quality = itemStack?.Attributes.TryGetFloat("quality") ?? 0.0f;
            if (quality > 0.0f && __result > 0.5f) __result = (float)(__result * (1.0f + quality * 0.02f));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMiningSpeed")]
        public static void Postfix2(ref float __result, IItemStack itemstack)
        {
            float quality = itemstack?.Attributes.TryGetFloat("quality") ?? 0.0f;
            if (quality > 0.0f) __result = (float)(__result * (1.0f + quality * 0.02f));
        }

        [HarmonyPatch("OnCreatedByCrafting")]
        public static void Postfix(ItemSlot[] allInputSlots, ItemSlot outputSlot)
        {
            if (outputSlot.Itemstack == null) return;
            int maxDurability = outputSlot.Itemstack.Collectible.GetMaxDurability(outputSlot.Itemstack);

            if (maxDurability <= 1) return;
            float quality = 0.0f;
            int count = 0;
            bool useQuality = false;
            foreach (ItemSlot slot in allInputSlots)
            {
                if (slot.Itemstack == null) continue;
                float? inputQuality = slot.Itemstack.Attributes.TryGetFloat("quality");

                if (outputSlot.Itemstack.Collectible == slot.Itemstack.Collectible)
                {
                    quality += (inputQuality ?? 0.0f) * 8.0f;
                    count += 8;
                }
                else if (slot.Itemstack.Collectible.Attributes?.KeyExists("qualityType") ?? false)
                {
                    useQuality = true;
                    quality += inputQuality ?? 0.0f;
                    count++;
                }
                else if (inputQuality != null)
                {
                    useQuality = true;
                    quality += 2 * (inputQuality ?? 0.0f);
                    count += 2;
                }
            }
            if (count > 0 && useQuality)
            {
                quality /= count;
                if (quality > 0.05f)
                {
                    // Запоминаем максимальную прочность ДО применения качества XSkills
                    int oldMax = outputSlot.Itemstack.Collectible.GetMaxDurability(outputSlot.Itemstack);

                    // Применяем качество
                    outputSlot.Itemstack.Attributes.SetFloat("quality", quality);

                    // Вычисляем новую максимальную прочность ПОСЛЕ применения качества
                    int newMax = outputSlot.Itemstack.Collectible.GetMaxDurability(outputSlot.Itemstack);

                    // Удаляем ванильный урон, чтобы предмет считался абсолютно новым
                    outputSlot.Itemstack.Attributes.RemoveAttribute("damage");

                    // ФИКС СОВМЕСТИМОСТИ (Toolsmith + Item Rarity)
                    // Проверяем, использует ли предмет жесткий атрибут "durability" (как делает Toolsmith)
                    if (outputSlot.Itemstack.Attributes.HasAttribute("durability"))
                    {
                        // Берем текущее значение и добавляем только ту разницу, которую дал XSkills
                        int currentDurability = outputSlot.Itemstack.Attributes.GetInt("durability");
                        outputSlot.Itemstack.Attributes.SetInt("durability", currentDurability + (newMax - oldMax));
                    }
                }
            }
        }

        public class TryMergeStacksState
        {
            public bool SourceHasQuality;
            public bool SourceHasBaseQuality;
            public bool SinkHasQuality;
            public bool SinkHasBaseQuality;
            public float SourceQuality;
            public float SourceBaseQuality;
            public float SinkQuality;
            public float SinkBaseQuality;
        }

        private static float GetMergedValue(float sourceValue, float sinkValue, int moved, int oldSinkSize, int newSize)
        {
            return (sourceValue * moved + sinkValue * oldSinkSize) / newSize;
        }

        [HarmonyPrefix]
        [HarmonyPatch("TryMergeStacks")]
        public static void Prefix(out TryMergeStacksState __state, ItemStackMergeOperation op)
        {
            __state = null;

            if (op?.SourceSlot?.Itemstack == null || op.SinkSlot?.Itemstack == null) return;
            if (op.CurrentPriority != EnumMergePriority.AutoMerge && op.CurrentPriority != EnumMergePriority.DirectMerge) return;

            ITreeAttribute sourceAttributes = op.SourceSlot.Itemstack.Attributes;
            ITreeAttribute sinkAttributes = op.SinkSlot.Itemstack.Attributes;
            if (sourceAttributes == null || sinkAttributes == null) return;

            bool sourceHasQuality = sourceAttributes.HasAttribute("quality");
            bool sourceHasBaseQuality = sourceAttributes.HasAttribute("xskillsBaseQuality");
            bool sinkHasQuality = sinkAttributes.HasAttribute("quality");
            bool sinkHasBaseQuality = sinkAttributes.HasAttribute("xskillsBaseQuality");
            bool hasSourceQuality = sourceHasQuality || sourceHasBaseQuality;
            bool hasSinkQuality = sinkHasQuality || sinkHasBaseQuality;
            if (!hasSourceQuality && !hasSinkQuality) return;

            float sourceQuality = sourceAttributes.GetFloat("quality");
            float sinkQuality = sinkAttributes.GetFloat("quality");
            float sourceBaseQuality = sourceAttributes.GetFloat("xskillsBaseQuality");
            float sinkBaseQuality = sinkAttributes.GetFloat("xskillsBaseQuality");

            bool differentQuality = Math.Abs(sourceQuality - sinkQuality) > 0.0001f;
            bool differentBaseQuality = Math.Abs(sourceBaseQuality - sinkBaseQuality) > 0.0001f;
            if (!differentQuality && !differentBaseQuality) return;

            __state = new TryMergeStacksState()
            {
                SourceHasQuality = sourceHasQuality,
                SourceHasBaseQuality = sourceHasBaseQuality,
                SinkHasQuality = sinkHasQuality,
                SinkHasBaseQuality = sinkHasBaseQuality,
                SourceQuality = sourceQuality,
                SourceBaseQuality = sourceBaseQuality,
                SinkQuality = sinkQuality,
                SinkBaseQuality = sinkBaseQuality
            };

            sourceAttributes.RemoveAttribute("quality");
            sourceAttributes.RemoveAttribute("xskillsBaseQuality");
            sinkAttributes.RemoveAttribute("quality");
            sinkAttributes.RemoveAttribute("xskillsBaseQuality");
        }

        [HarmonyPostfix]
        [HarmonyPatch("TryMergeStacks")]
        public static void Postfix(TryMergeStacksState __state, ItemStackMergeOperation op)
        {
            if (op == null || __state == null) return;
            if (op.SourceSlot?.Itemstack?.Attributes != null)
            {
                if (__state.SourceHasQuality) op.SourceSlot.Itemstack.Attributes.SetFloat("quality", __state.SourceQuality);
                else op.SourceSlot.Itemstack.Attributes.RemoveAttribute("quality");

                if (__state.SourceHasBaseQuality) op.SourceSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", __state.SourceBaseQuality);
                else op.SourceSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
            }

            if (op.MovedQuantity <= 0 || op.SinkSlot?.Itemstack?.Attributes == null)
            {
                if (op.SinkSlot?.Itemstack?.Attributes != null)
                {
                    if (__state.SinkHasQuality) op.SinkSlot.Itemstack.Attributes.SetFloat("quality", __state.SinkQuality);
                    else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("quality");

                    if (__state.SinkHasBaseQuality) op.SinkSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", __state.SinkBaseQuality);
                    else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
                }

                return;
            }

            int moved = op.MovedQuantity;
            int newSize = op.SinkSlot.Itemstack.StackSize;
            int oldSinkSize = newSize - moved;
            if (newSize <= 0 || oldSinkSize < 0) return;

            float quality = GetMergedValue(__state.SourceQuality, __state.SinkQuality, moved, oldSinkSize, newSize);

            if (quality > 0.0f) op.SinkSlot.Itemstack.Attributes.SetFloat("quality", quality);
            else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("quality");

            float baseQuality = GetMergedValue(__state.SourceBaseQuality, __state.SinkBaseQuality, moved, oldSinkSize, newSize);

            if (baseQuality > 0.0f) op.SinkSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", baseQuality);
            else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
        }

        public class TryEatStopState
        {
            public float quality;
            public float size;
            public float temperature;
            public EnumFoodCategory foodCategory;

            public TryEatStopState()
            {
                quality = 0.0f;
                size = 0.0f;
                temperature = 0.0f;
                foodCategory = EnumFoodCategory.NoNutrition;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("tryEatStop")]
        public static void tryEatStopPrefix(CollectibleObject __instance, out TryEatStopState __state, float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            __state = new TryEatStopState();
            if (secondsUsed < 0.95f || byEntity?.World.Api.Side == EnumAppSide.Client) return;
            if (byEntity == null || slot?.Itemstack == null) return;
            FoodNutritionProperties nutriProps = __instance.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
            if (nutriProps == null) return;

            __state.quality = slot.Itemstack.Attributes?.GetFloat("quality") ?? 0;
            __state.size = slot.Itemstack.StackSize;
            __state.temperature = slot.Itemstack.Collectible.GetTemperature(byEntity.World, slot.Itemstack);
            __state.foodCategory = nutriProps.FoodCategory;
        }

        [HarmonyPostfix]
        [HarmonyPatch("tryEatStop")]
        public static void tryEatStopPostfix(TryEatStopState __state, float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity?.World.Api.Side == EnumAppSide.Server && __state.foodCategory != EnumFoodCategory.NoNutrition && secondsUsed >= 0.95f)
            {
                Cooking.ApplyQuality(
                    __state.quality, __state.size - (slot.Itemstack?.StackSize ?? 0),
                    __state.temperature, __state.foodCategory,
                    EnumFoodCategory.Unknown, byEntity);
            }
        }

        [HarmonyPatch("DoSmelt")]
        public static void Prefix(out DoSmeltState __state, ItemSlot outputSlot)
        {
            __state = new DoSmeltState();
            __state.stackSize = outputSlot.Itemstack?.StackSize ?? 0;
            __state.quality = outputSlot.Itemstack?.Attributes.GetFloat("quality") ?? 0.0f;
        }

        [HarmonyPatch("DoSmelt")]
        public static void Postfix(DoSmeltState __state, IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot outputSlot)
        {
            InventoryBase inv = cookingSlotsProvider as InventoryBase;
            if (inv == null) return;

            BlockPos pos = inv.Pos;
            if (world?.BlockAccessor == null) return;

            BlockEntity blockEntity = pos == null ? null : world.BlockAccessor.GetBlockEntity(pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();
            IPlayer byPlayer = ownable?.Owner ?? heldInteractPlayer;

            int cooked = (outputSlot.Itemstack?.StackSize ?? 0) - __state.stackSize;
            if (byPlayer == null || cooked <= 0) return;
            DoSmeltCooking(byPlayer, outputSlot, cooked, __state.quality);
        }

        internal static bool DoSmeltCooking(IPlayer byPlayer, ItemSlot outputSlot, int cooked, float quality)
        {
            FoodNutritionProperties nutritionProps = outputSlot.Itemstack?.Collectible.NutritionProps;
            if (nutritionProps == null) return false;

            Cooking cooking = byPlayer.Entity?.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return true;
            cooking.ApplyAbilities(outputSlot, byPlayer, quality, cooked);
            return true;
        }
    }//!class CollectiblePatch
}//!namespace XSkills