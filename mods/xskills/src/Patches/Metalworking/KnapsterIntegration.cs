using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System;
using System.Reflection;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Интеграция XSkills с модом Knapster для поддержки автоматической ковки.
    /// Обеспечивает работу перков: Возврат металла, Дубликатор и Мгновенное завершение.
    /// </summary>
    public static class KnapsterIntegration
    {
        public static void ApplyPatches(Harmony harmony, ICoreAPI api)
        {
            // Проверяем наличие мода Knapster
            if (!api.ModLoader.IsModEnabled("knapster")) return;

            // Находим класс с патчами Knapster через рефлексию
            var patchType = AccessTools.TypeByName("Knapster.Features.EasySmithing.Patches.EasySmithingUniversalPatches");
            if (patchType == null) return;

            // 1. Патчим методы удаления вокселей (для работы Metal Recovery)
            var processSplit = AccessTools.Method(patchType, "ProcessSplit");
            var processRemoveSlag = AccessTools.Method(patchType, "ProcessRemoveSlag");
            var recoveryPostfix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(MetalRecovery_Postfix)));

            if (processSplit != null) harmony.Patch(processSplit, postfix: recoveryPostfix);
            if (processRemoveSlag != null) harmony.Patch(processRemoveSlag, postfix: recoveryPostfix);

            // 2. Патчим основной метод обработки удара (для Duplicator и Finishing Touch)
            var processHit = AccessTools.Method(patchType, "ProcessHit");
            var hitPostfix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(ProcessHit_Postfix)));

            if (processHit != null) harmony.Patch(processHit, postfix: hitPostfix);
        }

        /// <summary>
        /// Вызывается после того, как Knapster отколол кусок металла или шлака.
        /// Реализует перк "Возврат металла" (Metal Recovery).
        /// </summary>
        public static void MetalRecovery_Postfix(BlockEntityAnvil anvil, IPlayer byPlayer)
        {
            if (anvil?.WorkItemStack == null || byPlayer == null) return;

            Metalworking metalworking = XLeveling.Instance(anvil.Api)?.GetSkill("metalworking") as Metalworking;
            PlayerSkill playerSkill = byPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id];
            if (playerSkill == null || metalworking == null) return;

            var config = metalworking.Config as MetalworkingConfig;

            // Если ID -1, значит установлен сторонний мод на возврат металла и перк XSkills отключен
            if (metalworking.MetalRecoveryId == -1) return;

            PlayerAbility ability = playerSkill.PlayerAbilities[metalworking.MetalRecoveryId];

            // Проверка условий (не крица, перк изучен)
            if (ability == null || ability.Tier <= 0 || anvil.WorkItemStack.Item is ItemIronBloom) return;

            int divideBy = ability.Value(0);
            if (divideBy <= 0) return;

            // Используем счетчик из BlockEntityAnvilExtension (из BlockEntityAnvilPatch.cs)
            int currentSplits = anvil.GetSplitCount() + 1;

            if (currentSplits >= divideBy)
            {
                int bitsCount = currentSplits / divideBy;
                anvil.SetSplitCount(currentSplits % divideBy);

                string domain = (config?.useVanillaBits ?? true) ? "game" : "xskills";

                // Определяем материал заготовки
                if (anvil.WorkItemStack.Collectible is not IAnvilWorkable workable) return;

                string baseMaterial = workable.GetBaseMaterial(anvil.WorkItemStack).Collectible.LastCodePart();
                // Совместимость со сталью
                if (baseMaterial == "steel" && !anvil.Api.ModLoader.IsModEnabled("smithingplus")) baseMaterial = "blistersteel";

                AssetLocation bitCode = new AssetLocation(domain, "metalbit-" + baseMaterial);
                Item bitItem = anvil.Api.World.GetItem(bitCode);

                if (bitItem != null)
                {
                    ItemStack bitStack = new ItemStack(bitItem, bitsCount);
                    float temp = anvil.WorkItemStack.Collectible.GetTemperature(anvil.Api.World, anvil.WorkItemStack);
                    bitItem.SetTemperature(anvil.Api.World, bitStack, temp);

                    // Даем игроку или спавним рядом
                    if (!byPlayer.InventoryManager.TryGiveItemstack(bitStack))
                    {
                        anvil.Api.World.SpawnItemEntity(bitStack, anvil.Pos.ToVec3d().Add(0.5, 1.5, 0.5));
                    }
                }
            }
            else
            {
                anvil.SetSplitCount(currentSplits);
            }
        }

        /// <summary>
        /// Вызывается после каждого логического шага ковки в Knapster.
        /// Реализует перки "Дубликатор" и "Мгновенное завершение".
        /// </summary>
        public static void ProcessHit_Postfix(BlockEntityAnvil anvil, IPlayer byPlayer, ref object __result)
        {
            if (anvil == null || byPlayer == null || __result == null) return;

            // Получаем Action через рефлексию, т.к. AnvilHitResult - это record из Knapster
            var actionProp = __result.GetType().GetProperty("Action");
            if (actionProp == null) return;
            string actionName = actionProp.GetValue(__result)?.ToString();

            Metalworking metalworking = XLeveling.Instance(anvil.Api)?.GetSkill("metalworking") as Metalworking;
            PlayerSkill playerSkill = byPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id];
            if (playerSkill == null || metalworking == null) return;

            // --- ЛОГИКА ДУБЛИКАТОРА ---
            if (actionName == "ItemCompleted")
            {
                PlayerAbility dupAbility = playerSkill.PlayerAbilities[metalworking.DuplicatorId];
                if (dupAbility != null && dupAbility.Tier > 0)
                {
                    var recipe = anvil.SelectedRecipe;
                    if (recipe != null && metalworking.IsDuplicatable(recipe))
                    {
                        // Проверка шанса
                        if (anvil.Api.World.Rand.NextDouble() < dupAbility.SkillDependentFValue())
                        {
                            ItemStack outStack = recipe.Output.ResolvedItemstack.Clone();
                            float temp = anvil.WorkItemStack?.Collectible.GetTemperature(anvil.Api.World, anvil.WorkItemStack) ?? 20f;
                            outStack.Collectible.SetTemperature(anvil.Api.World, outStack, temp);

                            // Выдача предмета
                            if (!byPlayer.InventoryManager.TryGiveItemstack(outStack))
                            {
                                anvil.Api.World.SpawnItemEntity(outStack, anvil.Pos.ToVec3d().Add(0.5, 1.5, 0.5));
                            }
                        }
                    }
                }
                return;
            }

            // --- ЛОГИКА МГНОВЕННОГО ЗАВЕРШЕНИЯ (Finishing Touch) ---
            if (actionName != "Nothing")
            {
                PlayerAbility finishAbility = playerSkill.PlayerAbilities[metalworking.FinishingTouchId];
                if (finishAbility != null && finishAbility.Tier > 0)
                {
                    // Используем методы расширения из твоего мода
                    float finishedProportion = anvil.FinishedProportion();

                    // Учет настройки защиты от эксплойтов (из твоего конфига)
                    MetalworkingConfig config = metalworking.Config as MetalworkingConfig;
                    if (finishedProportion < 0.0f && (config?.allowFinishingTouchExploit ?? false))
                    {
                        finishedProportion *= -1.0f;
                    }

                    // Формула шанса срабатывания
                    float chanceMult = Math.Min(finishAbility.Value(0) + finishAbility.Value(1) * 0.1f, finishAbility.Value(2)) * 0.01f;

                    if (finishedProportion > 0 && (chanceMult * finishedProportion * finishedProportion) >= anvil.Api.World.Rand.NextDouble())
                    {
                        // Мгновенно подгоняем воксели под рецепт
                        anvil.FinishRecipe();

                        // Принудительно обновляем визуализацию наковальни
                        var regenMethod = AccessTools.Method(typeof(BlockEntityAnvil), "RegenMeshAndSelectionBoxes");
                        regenMethod?.Invoke(anvil, null);

                        anvil.MarkDirty();
                        // На следующей итерации цикла Knapster увидит, что предмет готов.
                    }
                }
            }
        }
    }
}
