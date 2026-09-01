using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public static class BlockEntityCauldronFirepitPatch
    {
        public static void Apply(Harmony harmony, Type type, XSkills xskills)
        {
            if (type == null)
            {
                return;
            }

            MethodInfo originalOnSpoonCheck = type.GetMethod(
                "OnSpoonCheck",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public
            );

            MethodInfo postfixOnSpoonCheck =
                typeof(BlockEntityCauldronFirepitPatch).GetMethod(
                    nameof(OnSpoonCheck_Postfix),
                    BindingFlags.Static | BindingFlags.Public
                );

            if (originalOnSpoonCheck != null && postfixOnSpoonCheck != null)
            {
                harmony.Patch(
                    originalOnSpoonCheck,
                    postfix: new HarmonyMethod(postfixOnSpoonCheck)
                );
            }

            MethodInfo originalSmelt = typeof(BlockEntityFirepit).GetMethod(
                "smeltItems",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            MethodInfo prefixSmelt =
                typeof(BlockEntityCauldronFirepitPatch).GetMethod(
                    nameof(SmeltItems_Prefix),
                    BindingFlags.Static | BindingFlags.Public
                );

            MethodInfo postfixSmelt =
                typeof(BlockEntityCauldronFirepitPatch).GetMethod(
                    nameof(SmeltItems_Postfix),
                    BindingFlags.Static | BindingFlags.Public
                );

            if (originalSmelt != null && prefixSmelt != null && postfixSmelt != null)
            {
                harmony.Patch(
                    originalSmelt,
                    prefix: new HarmonyMethod(prefixSmelt),
                    postfix: new HarmonyMethod(postfixSmelt)
                );
            }
        }

        // метод получения навыка алхимии
        private static PlayerSkill GetAlchemySkill(IPlayer player, out Alchemy alchemySkill)
        {
            alchemySkill = XSkills.Instance.Skills["alchemy"] as Alchemy;
            if (player == null || alchemySkill == null) return null;

            var skillSet = player.Entity.GetBehavior<PlayerSkillSet>();
            return skillSet?[alchemySkill.Id];
        }

        // безопасное получение владельца
        private static IPlayer GetOwner(BlockEntityFirepit be)
        {
            var ownable = be.GetBehavior<BlockEntityBehaviorOwnable>();
            if (ownable == null) return null;

            PropertyInfo ownerProp = ownable.GetType().GetProperty("Owner");
            object ownerObj = ownerProp?.GetValue(ownable);

            if (ownerObj is IPlayer player) return player;
            if (ownerObj is string uid) return be.Api.World.PlayerByUid(uid);

            return null;
        }

        public static void OnSpoonCheck_Postfix(BlockEntityFirepit __instance, float dt)
        {
            if (__instance.GetType().Name != "BlockEntityCauldronFirepit") return;

            FieldInfo cookingTimeField = typeof(BlockEntityFirepit).GetField("inputStackCookingTime", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (cookingTimeField == null) return;

            float currentCookingTime = (float)cookingTimeField.GetValue(__instance);
            if (currentCookingTime <= 0) return;

            IPlayer player = GetOwner(__instance);
            PlayerSkill playerSkill = GetAlchemySkill(player, out Alchemy alchemy);
            if (playerSkill == null) return;

            PlayerAbility brewingSpeed = playerSkill[alchemy.BrewingSpeedId];
            if (brewingSpeed != null && brewingSpeed.Tier > 0)
            {
                float speedBonus = brewingSpeed.Ability.Values[brewingSpeed.Tier - 1] / 100f;
                float extraTime = dt * speedBonus;
                cookingTimeField.SetValue(__instance, currentCookingTime + extraTime);
            }
        }

        public class SmeltState
        {
            public PlayerSkill Skill;
            public Alchemy Alchemy;
            public ItemStack[] IngredientsBefore;
            public int OutputAmountBefore;
            public float OutputQualityBefore;
        }

        public static void SmeltItems_Prefix(BlockEntityFirepit __instance, out SmeltState __state)
        {
            __state = null;
            if (__instance.GetType().Name != "BlockEntityCauldronFirepit") return;

            IPlayer player = GetOwner(__instance);
            PlayerSkill playerSkill = GetAlchemySkill(player, out Alchemy alchemy);
            if (playerSkill == null) return;

            var inv = __instance.Inventory as InventorySmelting;
            if (inv == null) return;

            int outputAmountBefore =
            inv[2]?.Itemstack?.StackSize ?? 0;

            float outputQualityBefore =
             inv[2]?.Itemstack?.Attributes
                 .GetFloat("quality", 0f)
             ?? 0f;

            ItemStack[] savedIngredients = new ItemStack[inv.CookingSlots.Length];
            for (int i = 0; i < inv.CookingSlots.Length; i++)
            {
                savedIngredients[i] = inv.CookingSlots[i].Itemstack?.Clone();
            }

            __state = new SmeltState
            {
                Skill = playerSkill,
                Alchemy = alchemy,
                IngredientsBefore = savedIngredients,

                OutputAmountBefore = outputAmountBefore,
                OutputQualityBefore = outputQualityBefore
            };
        }

        public static void SmeltItems_Postfix(BlockEntityFirepit __instance, SmeltState __state)
        {
            if (__state == null || __instance.GetType().Name != "BlockEntityCauldronFirepit") return;

            var inv = __instance.Inventory as InventorySmelting;
            if (inv == null) return;

            PlayerAbility efficiency = __state.Skill[__state.Alchemy.IngredientEfficiencyId];

            // больше зелья
            ItemSlot xpOutputSlot = inv[2];

            int outputAmountAfter = xpOutputSlot?.Itemstack?.StackSize ?? 0;

            // реальный объем варки
            int baseBrewedAmount = outputAmountAfter - __state.OutputAmountBefore;

            PlayerAbility tenuation = __state.Skill[__state.Alchemy.TenuationId];

            if (baseBrewedAmount > 0 && tenuation != null && tenuation.Tier > 0 && !xpOutputSlot.Empty)
            {
                // нормализованное значение
                float bonus = tenuation.SkillDependentFValue();
                float scaledAmount = baseBrewedAmount * (1f + bonus);

                // округление дробной части
                int totalBrewed = (int)scaledAmount;
                float remainder = scaledAmount - totalBrewed;

                if (__instance.Api.World.Rand.NextDouble() < remainder)
                {
                    totalBrewed++;
                }

                int bonusAmount = totalBrewed - baseBrewedAmount;

                if (bonusAmount > 0)
                {
                    // защита от переполнения слота
                    int freeSpace = xpOutputSlot.MaxSlotStackSize - xpOutputSlot.Itemstack.StackSize;
                    int actualBonus = Math.Min(bonusAmount, freeSpace);

                    if (actualBonus > 0)
                    {
                        xpOutputSlot.Itemstack.StackSize += actualBonus;
                        xpOutputSlot.MarkDirty();
                        __instance.MarkDirty(true);
                    }
                }
            }

            // качество зелья
            PlayerAbility potionQuality = __state.Skill[__state.Alchemy.PotionQualityId];

            if (baseBrewedAmount > 0 && potionQuality != null && potionQuality.Tier > 0 && !xpOutputSlot.Empty)
            {
                // макс уровень - 25
                float newBatchQuality = Math.Min(__state.Skill.Level, 25) * 0.1f;

                // первый этап формулы
                newBatchQuality = Math.Min(newBatchQuality * potionQuality.Value(0), potionQuality.Value(1) * 0.5f - 1.0f);

                // случайный бонус до 100%
                newBatchQuality = Math.Min(newBatchQuality + (float)__instance.Api.World.Rand.NextDouble() * newBatchQuality, potionQuality.Value(1) - 2.0f);
                newBatchQuality = (float)Math.Round(newBatchQuality, 2);

                // смешивание зелий
                float finalQuality = newBatchQuality;

                if (__state.OutputAmountBefore > 0)
                {
                    float totalQuality = __state.OutputQualityBefore * __state.OutputAmountBefore + newBatchQuality * baseBrewedAmount;
                    int totalAmount = __state.OutputAmountBefore + baseBrewedAmount;

                    if (totalAmount > 0)
                    {
                        finalQuality = totalQuality / totalAmount;
                    }
                }

                finalQuality = (float)Math.Round(finalQuality, 2);

                xpOutputSlot.Itemstack.Attributes.SetFloat("quality", finalQuality);
                xpOutputSlot.MarkDirty();
                __instance.MarkDirty(true);
            }

            // опыт за крафт
            outputAmountAfter = xpOutputSlot?.Itemstack?.StackSize ?? 0;
            int brewedAmount = outputAmountAfter - __state.OutputAmountBefore;

            if (brewedAmount > 0)
            {
                float experience = brewedAmount * 0.01f;
                PlayerAbility alchemist = __state.Skill[__state.Alchemy.AlchemistId];

                if (alchemist != null && alchemist.Tier > 0)
                {
                    int experienceBonus = alchemist.Value(0);
                    experience *= 1f + experienceBonus / 100f;
                }

                __state.Skill.AddExperience(experience);
            }

            PlayerAbility potency = __state.Skill[__state.Alchemy.PotentPotionId];
            Random rand = __instance.Api.World.Rand;

            if (efficiency != null && efficiency.Tier > 0)
            {
                int saveChance = efficiency.Value(0);
                int savePercent = efficiency.Value(1);

                // один бросок на варку
                int roll = rand.Next(100);

                if (roll < saveChance)
                {
                    // поиск активных слотов
                    int[] eligibleIndices = new int[inv.CookingSlots.Length];
                    int eligibleCount = 0;

                    for (int i = 0; i < inv.CookingSlots.Length; i++)
                    {
                        ItemStack oldStack = __state.IngredientsBefore[i];

                        if (oldStack == null || oldStack.StackSize <= 0)
                            continue;

                        eligibleIndices[eligibleCount] = i;
                        eligibleCount++;
                    }

                    if (eligibleCount > 0)
                    {
                        // выбор одного слота
                        int selectedSlot = eligibleIndices[rand.Next(eligibleCount)];
                        ItemStack oldStack = __state.IngredientsBefore[selectedSlot];

                        int amountToSave = (int)Math.Ceiling(oldStack.StackSize * savePercent / 100f);
                        amountToSave = Math.Max(1, amountToSave);
                        amountToSave = Math.Min(amountToSave, oldStack.StackSize);

                        ItemStack savedStack = oldStack.Clone();
                        savedStack.StackSize = amountToSave;

                        // возврат в исходный слот
                        ItemSlot selectedCookingSlot = inv.CookingSlots[selectedSlot];
                        selectedCookingSlot.Itemstack = savedStack;
                        selectedCookingSlot.MarkDirty();
                        __instance.MarkDirty(true);
                    }
                }
            }
        }
    }
}