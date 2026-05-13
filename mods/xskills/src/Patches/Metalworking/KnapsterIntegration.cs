using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Интеграция XSkills с модом Knapster для поддержки автоматической ковки.
    /// </summary>
    public static class KnapsterIntegration
    {
        public static void ApplyPatches(Harmony harmony, ICoreAPI api)
        {
            if (!api.ModLoader.IsModEnabled("knapster")) return;

            var patchType = AccessTools.TypeByName("Knapster.Features.EasySmithing.Patches.EasySmithingUniversalPatches");
            if (patchType == null) return;

            // 1. Ключевое исправление: Перехватываем OnHitSuccess, чтобы передать реального игрока
            // Это автоматически чинит перки Кузнец, Дубликатор и Мгновенное завершение
            var onHitSuccess = AccessTools.Method(patchType, "OnHitSuccess");
            var hitSuccessPrefix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(OnHitSuccess_Prefix)));
            if (onHitSuccess != null) harmony.Patch(onHitSuccess, prefix: hitSuccessPrefix);

            // 2. Патчим методы удаления вокселей для перка "Возврат металла" (Metal Recovery)
            var processSplit = AccessTools.Method(patchType, "ProcessSplit");
            var processRemoveSlag = AccessTools.Method(patchType, "ProcessRemoveSlag");
            var recoveryPostfix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(MetalRecovery_Postfix)));

            if (processSplit != null) harmony.Patch(processSplit, postfix: recoveryPostfix);
            if (processRemoveSlag != null) harmony.Patch(processRemoveSlag, postfix: recoveryPostfix);
        }

        /// <summary>
        /// Выполняет ту же логику, что и Knapster, но с передачей реального игрока в CheckIfFinished.
        /// </summary>
        public static bool OnHitSuccess_Prefix(BlockEntityAnvil anvil, EnumVoxelMaterial mat, Vec3i usableMetalVoxel, int x, int y, int z)
        {
            if (anvil.Api.World.Side == EnumAppSide.Client)
            {
                var spawnMethod = AccessTools.Method(typeof(BlockEntityAnvil), "spawnParticles");
                if (spawnMethod != null)
                {
                    spawnMethod.Invoke(anvil, new object[] { new Vec3i(x, y, z), mat == EnumVoxelMaterial.Empty ? EnumVoxelMaterial.Metal : mat, null });
                    if (usableMetalVoxel != null)
                    {
                        spawnMethod.Invoke(anvil, new object[] { usableMetalVoxel, EnumVoxelMaterial.Metal, null });
                    }
                }
            }

            var regenMethod = AccessTools.Method(typeof(BlockEntityAnvil), "RegenMeshAndSelectionBoxes");
            regenMethod?.Invoke(anvil, null);

            // ИСПРАВЛЕНИЕ: Берем реального игрока, который положил заготовку (через вашу систему)
            IPlayer realPlayer = anvil.GetUsedByPlayer();

            // Запускаем ванильное завершение ковки с реальным игроком.
            // Теперь нативные патчи XSkills (из BlockEntityAnvilPatch) отработают идеально!
            anvil.CheckIfFinished(realPlayer);

            return false; // Блокируем выполнение оригинального метода Knapster с его "null"
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
            if (metalworking.MetalRecoveryId == -1) return;

            PlayerAbility ability = playerSkill.PlayerAbilities[metalworking.MetalRecoveryId];

            if (ability == null || ability.Tier <= 0 || anvil.WorkItemStack.Item is ItemIronBloom) return;

            int divideBy = ability.Value(0);
            if (divideBy <= 0) return;

            int currentSplits = anvil.GetSplitCount() + 1;

            if (currentSplits >= divideBy)
            {
                int bitsCount = currentSplits / divideBy;
                anvil.SetSplitCount(currentSplits % divideBy);

                string domain = (config?.useVanillaBits ?? true) ? "game" : "xskills";

                if (anvil.WorkItemStack.Collectible is not IAnvilWorkable workable) return;

                string baseMaterial = workable.GetBaseMaterial(anvil.WorkItemStack).Collectible.LastCodePart();
                if (baseMaterial == "steel" && !anvil.Api.ModLoader.IsModEnabled("smithingplus")) baseMaterial = "blistersteel";

                AssetLocation bitCode = new AssetLocation(domain, "metalbit-" + baseMaterial);
                Item bitItem = anvil.Api.World.GetItem(bitCode);

                if (bitItem != null)
                {
                    ItemStack bitStack = new ItemStack(bitItem, bitsCount);
                    float temp = anvil.WorkItemStack.Collectible.GetTemperature(anvil.Api.World, anvil.WorkItemStack);
                    bitItem.SetTemperature(anvil.Api.World, bitStack, temp);

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
    }
}