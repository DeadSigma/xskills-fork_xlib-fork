using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public static class KnapsterIntegration
    {
        public static void ApplyPatches(Harmony harmony, ICoreAPI api)
        {
            if (!api.ModLoader.IsModEnabled("knapster")) return;

            var patchType = AccessTools.TypeByName("Knapster.Features.EasySmithing.Patches.EasySmithingUniversalPatches");
            if (patchType == null) return;

            var onHitSuccess = AccessTools.Method(patchType, "OnHitSuccess");
            var hitSuccessPrefix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(OnHitSuccess_Prefix)));
            if (onHitSuccess != null) harmony.Patch(onHitSuccess, prefix: hitSuccessPrefix);

            var processSplit = AccessTools.Method(patchType, "ProcessSplit");
            var processRemoveSlag = AccessTools.Method(patchType, "ProcessRemoveSlag");

            var recoveryPrefix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(MetalRecovery_Prefix)));
            var recoveryPostfix = new HarmonyMethod(AccessTools.Method(typeof(KnapsterIntegration), nameof(MetalRecovery_Postfix)));

            if (processSplit != null) harmony.Patch(processSplit, prefix: recoveryPrefix, postfix: recoveryPostfix);
            if (processRemoveSlag != null) harmony.Patch(processRemoveSlag, prefix: recoveryPrefix, postfix: recoveryPostfix);
        }

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

            IPlayer realPlayer = anvil.GetUsedByPlayer();
            anvil.CheckIfFinished(realPlayer);

            return false;
        }

        private static int CountMetalVoxels(BlockEntityAnvil anvil)
        {
            if (anvil?.Voxels == null) return 0;
            int count = 0;
            int xLen = anvil.Voxels.GetLength(0);
            int yLen = anvil.Voxels.GetLength(1);
            int zLen = anvil.Voxels.GetLength(2);

            for (int x = 0; x < xLen; x++)
            {
                for (int y = 0; y < yLen; y++)
                {
                    for (int z = 0; z < zLen; z++)
                    {
                        if (anvil.Voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal) count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// ДО удара: запоминаем всё, потому что после удара заготовки может уже не быть!
        /// </summary>
        public static void MetalRecovery_Prefix(BlockEntityAnvil anvil, IPlayer byPlayer, ref object __state)
        {
            if (anvil?.WorkItemStack == null)
            {
                __state = null;
                return;
            }

            int voxels = CountMetalVoxels(anvil);
            int splits = anvil.GetSplitCount();
            string baseMat = "iron";
            float temp = 0f;

            if (anvil.WorkItemStack.Collectible is IAnvilWorkable workable)
            {
                baseMat = workable.GetBaseMaterial(anvil.WorkItemStack)?.Collectible?.LastCodePart() ?? "iron";
                if (baseMat == "steel" && !anvil.Api.ModLoader.IsModEnabled("smithingplus")) baseMat = "blistersteel";
                if (baseMat == "ironbloom") baseMat = "iron";

                temp = anvil.WorkItemStack.Collectible.GetTemperature(anvil.Api.World, anvil.WorkItemStack);
            }

            IPlayer player = byPlayer ?? anvil.GetUsedByPlayer();

            // Сохраняем "слепок" состояния в массив
            __state = new object[] { voxels, splits, baseMat, temp, player };
        }

        /// <summary>
        /// ПОСЛЕ удара: достаем память и выдаем награду, даже если наковальня очистилась.
        /// </summary>
        public static void MetalRecovery_Postfix(BlockEntityAnvil anvil, ref object __state)
        {
            if (__state is not object[] stateData) return;

            // Распаковываем наши сохраненные данные
            int oldVoxels = (int)stateData[0];
            int oldSplits = (int)stateData[1];
            string baseMaterial = (string)stateData[2];
            float temp = (float)stateData[3];
            IPlayer realPlayer = (IPlayer)stateData[4];

            if (realPlayer == null) return;

            int currentVoxels = CountMetalVoxels(anvil);
            int metalRemoved = oldVoxels - currentVoxels;

            // Если вокселей вдруг стало 0 (или пропала целая гора), значит предмет завершился и наковальня очищена!
            // В таком случае мы точно знаем, что игрок отсёк 1 завершающий кусок металла.
            if (metalRemoved > 5 || (oldVoxels > 0 && currentVoxels == 0))
            {
                metalRemoved = 1;
            }

            // Если удалили шлак (или вообще ничего) - выходим
            if (metalRemoved <= 0) return;

            Metalworking metalworking = XLeveling.Instance(anvil.Api)?.GetSkill("metalworking") as Metalworking;
            PlayerSkill playerSkill = realPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id];
            if (playerSkill == null || metalworking == null) return;

            var config = metalworking.Config as MetalworkingConfig;
            if (metalworking.MetalRecoveryId == -1) return;

            PlayerAbility ability = playerSkill.PlayerAbilities[metalworking.MetalRecoveryId];
            if (ability == null || ability.Tier <= 0) return;

            int divideBy = ability.Value(0);
            if (divideBy <= 0) return;

            // Считаем прогресс по отсечению
            int currentSplits = oldSplits + metalRemoved;

            if (currentSplits >= divideBy)
            {
                int bitsCount = currentSplits / divideBy;
                int newSplits = currentSplits % divideBy;

                // Записываем остаток, только если заготовка еще жива
                if (anvil?.WorkItemStack != null)
                {
                    anvil.SetSplitCount(newSplits);
                }

                // Спавним кусочек металла на сервере 
                if (anvil.Api.Side == EnumAppSide.Server)
                {
                    string domain = (config?.useVanillaBits ?? true) ? "game" : "xskills";
                    AssetLocation bitCode = new AssetLocation(domain, "metalbit-" + baseMaterial);
                    Item bitItem = anvil.Api.World.GetItem(bitCode);

                    if (bitItem != null)
                    {
                        ItemStack bitStack = new ItemStack(bitItem, bitsCount);
                        bitItem.SetTemperature(anvil.Api.World, bitStack, temp);

                        if (!realPlayer.InventoryManager.TryGiveItemstack(bitStack))
                        {
                            anvil.Api.World.SpawnItemEntity(bitStack, anvil.Pos.ToVec3d().Add(0.5, 1.5, 0.5));
                        }
                    }
                }
            }
            else
            {
                // Записываем прогресс, только если заготовка еще жива
                if (anvil?.WorkItemStack != null)
                {
                    anvil.SetSplitCount(currentSplits);
                }
            }
        }
    }
}