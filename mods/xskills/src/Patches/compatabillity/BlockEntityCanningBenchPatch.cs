using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public class BlockEntityCanningBenchPatch
    {
        public static void Apply(Harmony harmony, Type benchType)
        {
            if (benchType == null) return;

            // Ищем кастомный слот ингредиентов прямо в сборке мода консервов
            Type ingredientSlotType = benchType.Assembly.GetType("IthaniaCannedGoods.Inventory.ItemSlotCanningBenchIngredient");
            Type targetSlotType = ingredientSlotType ?? typeof(ItemSlot);

            // Патчим лимиты слотов (с защитой от непереопределенных методов)
            var maxStackGetter = targetSlotType.GetProperty("MaxSlotStackSize")?.GetGetMethod();
            if (maxStackGetter != null)
            {
                // Если метод не переопределен в кастомном слоте, берем его из того класса, где он объявлен
                if (maxStackGetter.DeclaringType != targetSlotType)
                    maxStackGetter = maxStackGetter.DeclaringType.GetMethod(maxStackGetter.Name);

                var maxStackPostfix = typeof(BlockEntityCanningBenchPatch).GetMethod(nameof(MaxSlotStackSizePostfix), BindingFlags.Static | BindingFlags.Public);
                if (maxStackPostfix != null) harmony.Patch(maxStackGetter, postfix: new HarmonyMethod(maxStackPostfix));
            }

            // Патчим определение владельца стола (с защитой от непереопределенных методов)
            var activateSlot = targetSlotType.GetMethod("ActivateSlot");
            if (activateSlot != null)
            {
                if (activateSlot.DeclaringType != targetSlotType)
                    activateSlot = activateSlot.DeclaringType.GetMethod("ActivateSlot");

                var activateSlotPostfix = typeof(BlockEntityCanningBenchPatch).GetMethod(nameof(ActivateSlotPostfix), BindingFlags.Static | BindingFlags.Public);
                if (activateSlotPostfix != null) harmony.Patch(activateSlot, postfix: new HarmonyMethod(activateSlotPostfix));
            }

            // Патчим процесс упаковки
            var original = benchType.GetMethod("DoPackingResult", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefix = typeof(BlockEntityCanningBenchPatch).GetMethod(nameof(DoPackingResultPrefix), BindingFlags.Static | BindingFlags.Public);
            var postfix = typeof(BlockEntityCanningBenchPatch).GetMethod(nameof(DoPackingResultPostfix), BindingFlags.Static | BindingFlags.Public);

            if (original != null && prefix != null && postfix != null)
            {
                harmony.Patch(original, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            }
        }

        // МЕТОДЫ ИНТЕРФЕЙСА И ЛИМИТОВ СЛОТОВ

        public static void ActivateSlotPostfix(ItemSlot __instance, ref ItemStackMoveOperation op)
        {
            if (__instance.Inventory?.ClassName != "canningbench") return;
            BlockEntity blockEntity = __instance.Inventory.Api?.World?.BlockAccessor?.GetBlockEntity(__instance.Inventory.Pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();
            if (op.ActingPlayer != null && ownable != null)
            {
                ownable.Owner = op.ActingPlayer;
            }
        }

        public static void MaxSlotStackSizePostfix(ItemSlot __instance, ref int __result)
        {
            if (__instance.Inventory?.ClassName != "canningbench") return;

            int slotId = __instance.Inventory.GetSlotId(__instance);
            if (slotId < 1 || slotId > 4) return;

            ICoreAPI api = __instance.Inventory.Api;
            if (api?.World == null) return;

            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(__instance.Inventory.Pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();
            IPlayer player = ownable?.Owner;
            if (player == null) return;

            Cooking cooking = api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerAbility ability = player.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.CanteenCookId];
            if (ability != null && ability.Tier > 0)
            {
                // Value(0) возвращает проценты (34, 67, 100), поэтому делим на 100f
                __result = (int)Math.Round(__result * (1.0f + ability.Value(0) / 100f));
            }
        }

        // ЛОГИКА ПРОЦЕССА ГОТОВКИ

        public class CanningState
        {
            public ItemStack[] InputStacks;
        }

        public static void DoPackingResultPrefix(BlockEntityContainer __instance, out CanningState __state)
        {
            __state = new CanningState();
            List<ItemStack> inputs = new List<ItemStack>();
            InventoryBase inv = __instance.Inventory;
            if (inv != null && inv.Count >= 5)
            {
                // Собираем только НЕПУСТЫЕ слоты, чтобы не крашить расчеты XSkills
                for (int i = 1; i <= 4; i++)
                {
                    if (!inv[i].Empty && inv[i].Itemstack != null)
                    {
                        inputs.Add(inv[i].Itemstack.Clone());
                    }
                }
            }
            __state.InputStacks = inputs.ToArray();
        }

        public static void DoPackingResultPostfix(BlockEntityContainer __instance, string ___packingPlayerUid, CanningState __state)
        {
            InventoryBase inv = __instance.Inventory;
            if (inv == null || inv.Count <= 5 || inv[5].Empty || string.IsNullOrEmpty(___packingPlayerUid)) return;

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;

            IPlayer player = api.World.PlayerByUid(___packingPlayerUid);
            if (player == null) return;

            XSkills xskills = XSkills.Instance;
            Cooking cooking = xskills?.Skills["cooking"] as Cooking;
            if (cooking == null) return;

            ItemSlot outputSlot = inv[5];
            float servings = outputSlot.Itemstack.Attributes.GetFloat("quantityServings", 1.0f);
            string recipeCode = outputSlot.Itemstack.Attributes.GetString("recipeCode");
            RecipeRegistrySystem registry = api.ModLoader.GetModSystem<RecipeRegistrySystem>();
            CookingRecipe recipe = registry?.CookingRecipes?.FirstOrDefault(r => r.Code == recipeCode);

            PlayerSkill skill = player.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id];
            if (skill != null)
            {
                // CANTEENCOOK
                PlayerAbility canteenCook = skill[cooking.CanteenCookId];
                if (canteenCook != null && canteenCook.Tier > 0 && recipe != null)
                {
                    ItemStack[] leftStacks = new ItemStack[4];
                    for (int i = 0; i < 4; i++) leftStacks[i] = inv[i + 1].Itemstack;

                    int extraMatch = 0;
                    if (recipe.Matches(leftStacks, ref extraMatch) && extraMatch > 0)
                    {
                        int bonusLimit = (int)Math.Round(servings * (canteenCook.Value(0) / 100f));
                        if (bonusLimit < 1) bonusLimit = 1;
                        int allowedExtra = Math.Min(extraMatch, bonusLimit);

                        if (allowedExtra > 0)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                if (!inv[j].Empty)
                                {
                                    CookingRecipeIngredient ingred = recipe.GetIngrendientFor(inv[j].Itemstack, Array.Empty<CookingRecipeIngredient>());
                                    CookingRecipeStack matchedStack = ingred?.GetMatchingStack(inv[j].Itemstack);
                                    int consumeCount = (matchedStack != null) ? matchedStack.StackSize : 1;

                                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(inv[j].Itemstack);
                                    if (props != null && ingred != null) consumeCount *= (int)(props.ItemsPerLitre * ingred.PortionSizeLitres);

                                    int totalConsume = Math.Min(consumeCount * allowedExtra, inv[j].Itemstack.StackSize);

                                    if (totalConsume > 0)
                                    {
                                        inv[j].TakeOut(totalConsume);
                                        inv[j].MarkDirty();
                                        ITreeAttribute contents = outputSlot.Itemstack.Attributes.GetTreeAttribute("contents");
                                        if (contents != null)
                                        {
                                            foreach (var kvp in contents)
                                            {
                                                ItemstackAttribute attr = kvp.Value as ItemstackAttribute;
                                                if (attr != null && attr.value != null && attr.value.Equals(api.World, inv[j].Itemstack, GlobalConstants.IgnoredStackAttributes))
                                                {
                                                    attr.value.StackSize += totalConsume;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            servings += allowedExtra;
                        }
                    }
                }

                // HAPPYMEAL
                PlayerAbility happyMeal = skill[cooking.HappyMealId];
                if (happyMeal != null && happyMeal.Tier > 0 && recipe != null)
                {
                    if (happyMeal.SkillDependentFValue() >= api.World.Rand.NextDouble())
                    {
                        ITreeAttribute contents = outputSlot.Itemstack.Attributes.GetTreeAttribute("contents");
                        List<ItemStack> currentInputs = new List<ItemStack>();
                        if (contents != null)
                        {
                            foreach (var kvp in contents)
                            {
                                ItemstackAttribute attr = kvp.Value as ItemstackAttribute;
                                if (attr != null && attr.value != null) currentInputs.Add(attr.value);
                            }
                        }

                        bool allowBad = happyMeal.Tier < 4;
                        ItemStack extraStack = cooking.GetMissingIngredient(currentInputs.ToArray(), recipe, api.World, allowBad);

                        if (extraStack != null)
                        {
                            extraStack.StackSize = (int)servings;
                            if (contents == null)
                            {
                                contents = new TreeAttribute();
                                outputSlot.Itemstack.Attributes["contents"] = contents;
                            }
                            contents[contents.Count.ToString()] = new ItemstackAttribute(extraStack);
                        }
                    }
                }

                // DILUTION
                PlayerAbility dilution = skill[cooking.DilutionId];
                if (dilution != null && dilution.Tier > 0)
                {
                    float mult = 1.0f + dilution.SkillDependentFValue();
                    float scaledServings = servings * mult;
                    float rel = scaledServings - (int)scaledServings;
                    servings = (int)scaledServings + (api.World.Rand.NextDouble() < rel ? 1 : 0);
                }

                outputSlot.Itemstack.Attributes.SetFloat("quantityServings", servings);

                // WELLDONE
                PlayerAbility wellDone = skill[cooking.WellDoneId];
                if (wellDone != null && wellDone.Tier > 0)
                {
                    ITreeAttribute transAttr = outputSlot.Itemstack.Attributes.GetTreeAttribute("transitionstate");
                    if (transAttr != null)
                    {
                        FloatArrayAttribute freshHours = transAttr["freshHours"] as FloatArrayAttribute;
                        FloatArrayAttribute transHours = transAttr["transitionHours"] as FloatArrayAttribute;
                        float multiplier = 1.0f + wellDone.SkillDependentFValue();

                        // Нужно умножать оба параметра, чтобы срок годности сдвинулся корректно
                        if (freshHours != null && freshHours.value.Length > 0) freshHours.value[0] *= multiplier;
                        if (transHours != null && transHours.value.Length > 0) transHours.value[0] *= multiplier;
                    }
                }
            }

            // ОПЫТ И КАЧЕСТВО (с учетом сырых ингредиентов до их уничтожения)
            cooking.ApplyAbilities(outputSlot, player, 0f, 1.0f, __state.InputStacks);

            // ПРИНУДИТЕЛЬНАЯ СИНХРОНИЗАЦИЯ (Чтобы клиент увидел изменения)
            outputSlot.MarkDirty();
            __instance.MarkDirty(true);
            api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);
        }
    }
}