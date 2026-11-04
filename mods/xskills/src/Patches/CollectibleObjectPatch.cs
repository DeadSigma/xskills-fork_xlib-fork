using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(CollectibleObject))]
    public class CollectibleObjectPatch
    {
        [ThreadStatic]
        private static bool suppressTryMergeQualityHandling;

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

        public class MergeQualityState
        {
            public float SourceQuality;
            public float SinkQuality;
            public float SourceBaseQuality;
            public float SinkBaseQuality;
            public bool SourceHadQuality;
            public bool SinkHadQuality;
            public bool SourceHadBaseQuality;
            public bool SinkHadBaseQuality;
            public bool TempQualityOverrideApplied;
            public bool TempBaseQualityOverrideApplied;
            public bool ShouldAverage;
        }

        [HarmonyPatch("TryMergeStacks")]
        public static bool Prefix(out MergeQualityState __state, ItemStackMergeOperation op)
        {
            __state = null;
            if (suppressTryMergeQualityHandling) return true;

            if (op?.SourceSlot?.Itemstack == null || op.SinkSlot?.Itemstack == null) return true;
            if (op.SourceSlot.Itemstack.Collectible != op.SinkSlot.Itemstack.Collectible) return true;
            if (op.SinkSlot.MaxSlotStackSize <= 1) return true;
            if (!(XSkills.Instance?.XLeveling?.Config?.mergeQualities ?? false)) return true;

            ITreeAttribute sourceAttributes = op.SourceSlot.Itemstack.Attributes;
            ITreeAttribute sinkAttributes = op.SinkSlot.Itemstack.Attributes;
            bool sourceHasQuality = sourceAttributes?.HasAttribute("quality") ?? false;
            bool sinkHasQuality = sinkAttributes?.HasAttribute("quality") ?? false;
            bool sourceHasBaseQuality = sourceAttributes?.HasAttribute("xskillsBaseQuality") ?? false;
            bool sinkHasBaseQuality = sinkAttributes?.HasAttribute("xskillsBaseQuality") ?? false;
            if (!sourceHasQuality && !sinkHasQuality && !sourceHasBaseQuality && !sinkHasBaseQuality) return true;

            float sourceQuality = sourceAttributes?.GetFloat("quality", 0.0f) ?? 0.0f;
            float sinkQuality = sinkAttributes?.GetFloat("quality", 0.0f) ?? 0.0f;
            float sourceBaseQuality = sourceAttributes?.GetFloat("xskillsBaseQuality", 0.0f) ?? 0.0f;
            float sinkBaseQuality = sinkAttributes?.GetFloat("xskillsBaseQuality", 0.0f) ?? 0.0f;

            __state = new MergeQualityState
            {
                SourceQuality = sourceQuality,
                SinkQuality = sinkQuality,
                SourceBaseQuality = sourceBaseQuality,
                SinkBaseQuality = sinkBaseQuality,
                SourceHadQuality = sourceHasQuality,
                SinkHadQuality = sinkHasQuality,
                SourceHadBaseQuality = sourceHasBaseQuality,
                SinkHadBaseQuality = sinkHasBaseQuality,
                ShouldAverage = true
            };

            if (Math.Abs(sourceQuality - sinkQuality) > 0.0001f)
            {
                op.SourceSlot.Itemstack.Attributes.SetFloat("quality", sinkQuality);
                __state.TempQualityOverrideApplied = true;
            }

            bool baseQualityDiffers = sourceHasBaseQuality != sinkHasBaseQuality || Math.Abs(sourceBaseQuality - sinkBaseQuality) > 0.0001f;
            if (baseQualityDiffers)
            {
                if (sinkHasBaseQuality) op.SourceSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", sinkBaseQuality);
                else op.SourceSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
                __state.TempBaseQualityOverrideApplied = true;
            }

            return true;
        }

        [HarmonyPatch("TryMergeStacks")]
        public static void Postfix(MergeQualityState __state, ItemStackMergeOperation op)
        {
            if (__state == null || op?.SourceSlot == null || op.SinkSlot == null) return;

            if (__state.TempQualityOverrideApplied && op.SourceSlot.Itemstack?.Attributes != null)
            {
                if (__state.SourceHadQuality) op.SourceSlot.Itemstack.Attributes.SetFloat("quality", __state.SourceQuality);
                else op.SourceSlot.Itemstack.Attributes.RemoveAttribute("quality");
            }

            if (__state.TempBaseQualityOverrideApplied && op.SourceSlot.Itemstack?.Attributes != null)
            {
                if (__state.SourceHadBaseQuality) op.SourceSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", __state.SourceBaseQuality);
                else op.SourceSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
            }

            if (op.MovedQuantity <= 0) return;

            if (!__state.ShouldAverage) return;
            if (op.SinkSlot.Itemstack?.Attributes == null || op.SinkSlot.Itemstack.StackSize <= 0) return;

            int moved = op.MovedQuantity;
            int newSize = op.SinkSlot.Itemstack.StackSize;
            int oldSinkSize = newSize - moved;
            if (oldSinkSize < 0) return;

            float quality = (__state.SourceQuality * moved + __state.SinkQuality * oldSinkSize) / newSize;
            if (quality > 0.0f) op.SinkSlot.Itemstack.Attributes.SetFloat("quality", quality);
            else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("quality");

            if (__state.SourceHadBaseQuality || __state.SinkHadBaseQuality)
            {
                float baseQuality = (__state.SourceBaseQuality * moved + __state.SinkBaseQuality * oldSinkSize) / newSize;
                if (baseQuality > 0.0f) op.SinkSlot.Itemstack.Attributes.SetFloat("xskillsBaseQuality", baseQuality);
                else op.SinkSlot.Itemstack.Attributes.RemoveAttribute("xskillsBaseQuality");
            }
        }

        [HarmonyPatch(typeof(ItemSlot), "ActivateSlot")]
        public class ItemSlotActivateTracePatch
        {
            public class ActivateTraceState
            {
                public bool CanApplyMergeAveraging;
                public bool AppliedTempOverride;
                public bool AppliedQualityOverride;
                public bool AppliedBaseQualityOverride;
                public IAttribute SourceTempAttribute;
                public float SourceQuality;
                public float SourceBaseQuality;
                public bool SourceHadQuality;
                public bool SourceHadBaseQuality;
                public bool SinkHadBaseQuality;
                public float SinkQuality;
                public float SinkBaseQuality;
                public float SourceTemperature;
                public float SinkTemperature;
                public bool SourceHadTemperature;
                public bool SinkHadTemperature;
            }

            static bool IsMetalPlate(ItemStack stack)
            {
                string path = stack?.Collectible?.Code?.Path ?? string.Empty;
                return path.Contains("metalplate");
            }

            static void Prefix(ItemSlot __instance, ItemSlot sourceSlot, ref ItemStackMoveOperation op, out ActivateTraceState __state)
            {
                __state = new ActivateTraceState
                {
                    CanApplyMergeAveraging = false,
                    AppliedTempOverride = false,
                    AppliedQualityOverride = false,
                    AppliedBaseQualityOverride = false
                };

                ICoreAPI api = __instance?.Inventory?.Api ?? sourceSlot?.Inventory?.Api;
                if (api?.Side != EnumAppSide.Server) return;

                ItemStack sink = __instance?.Itemstack;
                ItemStack source = sourceSlot?.Itemstack;
                if (!IsMetalPlate(sink) && !IsMetalPlate(source)) return;

                if (source != null && sink != null && source.Collectible == sink.Collectible && (XSkills.Instance?.XLeveling?.Config?.mergeQualities ?? false))
                {
                    __state.CanApplyMergeAveraging = true;
                    ITreeAttribute sourceAttr = source.Attributes;
                    ITreeAttribute sinkAttr = sink.Attributes;

                    __state.SourceHadQuality = sourceAttr?.HasAttribute("quality") ?? false;
                    __state.SourceHadBaseQuality = sourceAttr?.HasAttribute("xskillsBaseQuality") ?? false;
                    __state.SinkHadBaseQuality = sinkAttr?.HasAttribute("xskillsBaseQuality") ?? false;
                    __state.SourceQuality = sourceAttr?.GetFloat("quality", 0.0f) ?? 0.0f;
                    __state.SourceBaseQuality = sourceAttr?.GetFloat("xskillsBaseQuality", 0.0f) ?? 0.0f;
                    __state.SinkQuality = sinkAttr?.GetFloat("quality", 0.0f) ?? 0.0f;
                    __state.SinkBaseQuality = sinkAttr?.GetFloat("xskillsBaseQuality", 0.0f) ?? 0.0f;

                    ITreeAttribute sourceTempTree = sourceAttr?.GetTreeAttribute("temperature");
                    ITreeAttribute sinkTempTree = sinkAttr?.GetTreeAttribute("temperature");
                    __state.SourceHadTemperature = sourceTempTree != null;
                    __state.SinkHadTemperature = sinkTempTree != null;
                    __state.SourceTemperature = sourceTempTree?.GetFloat("temperature", 0.0f) ?? 0.0f;
                    __state.SinkTemperature = sinkTempTree?.GetFloat("temperature", 0.0f) ?? 0.0f;

                    if (__state.SourceHadTemperature != __state.SinkHadTemperature || Math.Abs(__state.SourceTemperature - __state.SinkTemperature) > 0.0001f)
                    {
                        __state.SourceTempAttribute = sourceAttr?["temperature"]?.Clone();
                        if (sinkAttr?.HasAttribute("temperature") ?? false) sourceAttr["temperature"] = sinkAttr["temperature"].Clone();
                        else sourceAttr?.RemoveAttribute("temperature");
                        __state.AppliedTempOverride = true;
                    }

                    if (Math.Abs(__state.SourceQuality - __state.SinkQuality) > 0.0001f)
                    {
                        sourceAttr?.SetFloat("quality", __state.SinkQuality);
                        __state.AppliedQualityOverride = true;
                    }

                    bool baseQualityDiffers = __state.SourceHadBaseQuality != __state.SinkHadBaseQuality || Math.Abs(__state.SourceBaseQuality - __state.SinkBaseQuality) > 0.0001f;
                    if (baseQualityDiffers)
                    {
                        if (sinkAttr?.HasAttribute("xskillsBaseQuality") ?? false) sourceAttr?.SetFloat("xskillsBaseQuality", __state.SinkBaseQuality);
                        else sourceAttr?.RemoveAttribute("xskillsBaseQuality");
                        __state.AppliedBaseQualityOverride = true;
                    }

                    if (__state.AppliedTempOverride || __state.AppliedQualityOverride || __state.AppliedBaseQualityOverride)
                    {
                        suppressTryMergeQualityHandling = true;
                    }
                }

            }

            static void Postfix(ItemSlot __instance, ItemSlot sourceSlot, ItemStackMoveOperation op, ActivateTraceState __state)
            {
                if (__state == null) return;

                ICoreAPI api = __instance?.Inventory?.Api ?? sourceSlot?.Inventory?.Api;
                if (api?.Side != EnumAppSide.Server) return;

                ItemStack sink = __instance?.Itemstack;
                ItemStack source = sourceSlot?.Itemstack;

                if (__state.AppliedTempOverride && source?.Attributes != null)
                {
                    if (__state.SourceTempAttribute != null) source.Attributes["temperature"] = __state.SourceTempAttribute;
                    else source.Attributes.RemoveAttribute("temperature");
                }

                if (__state.AppliedQualityOverride && source?.Attributes != null)
                {
                    if (__state.SourceHadQuality) source.Attributes.SetFloat("quality", __state.SourceQuality);
                    else source.Attributes.RemoveAttribute("quality");
                }

                if (__state.AppliedBaseQualityOverride && source?.Attributes != null)
                {
                    if (__state.SourceHadBaseQuality) source.Attributes.SetFloat("xskillsBaseQuality", __state.SourceBaseQuality);
                    else source.Attributes.RemoveAttribute("xskillsBaseQuality");
                }

                if (__state.CanApplyMergeAveraging && (op?.MovedQuantity ?? 0) > 0 && sink?.Attributes != null)
                {
                    int moved = op.MovedQuantity;
                    int newSize = sink.StackSize;
                    int oldSinkSize = newSize - moved;
                    if (oldSinkSize >= 0 && newSize > 0)
                    {
                        float quality = (__state.SourceQuality * moved + __state.SinkQuality * oldSinkSize) / newSize;
                        if (quality > 0.0f) sink.Attributes.SetFloat("quality", quality);
                        else sink.Attributes.RemoveAttribute("quality");

                        if (__state.SourceHadBaseQuality || __state.SinkHadBaseQuality)
                        {
                            float baseQuality = (__state.SourceBaseQuality * moved + __state.SinkBaseQuality * oldSinkSize) / newSize;
                            if (baseQuality > 0.0f) sink.Attributes.SetFloat("xskillsBaseQuality", baseQuality);
                            else sink.Attributes.RemoveAttribute("xskillsBaseQuality");
                        }

                        if (__state.SourceHadTemperature || __state.SinkHadTemperature)
                        {
                            float mergedTemperature = (__state.SourceTemperature * moved + __state.SinkTemperature * oldSinkSize) / newSize;
                            sink.Collectible?.SetTemperature(api.World, sink, mergedTemperature, false);
                        }
                    }
                }

                if (__state.AppliedTempOverride || __state.AppliedQualityOverride || __state.AppliedBaseQualityOverride)
                {
                    suppressTryMergeQualityHandling = false;
                }
            }
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
            BlockEntity blockEntity = world?.BlockAccessor.GetBlockEntity(inv.Pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();

            int cooked = (outputSlot.Itemstack?.StackSize ?? 0) - __state.stackSize;
            if (ownable?.Owner == null || cooked <= 0) return;
            DoSmeltCooking(ownable.Owner, outputSlot, cooked, __state.quality);
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