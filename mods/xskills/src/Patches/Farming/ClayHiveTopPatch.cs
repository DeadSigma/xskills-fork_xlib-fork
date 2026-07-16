using HarmonyLib;
using System;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class ClayHiveTopPatch : ManualPatch
    {
        public static void Apply(Harmony harmony, Type type, XSkills xSkills)
        {
            if (xSkills == null) return;
            Skill skill;
            xSkills.Skills.TryGetValue("farming", out skill);
            Farming farming = skill as Farming;

            if (!(farming?.Enabled ?? false)) return;
            Type patch = typeof(ClayHiveTopPatch);

            if (farming[farming.BeekeeperId].Enabled)
            {
                // Патчим оба метода для максимальной надежности, 
                // так как замена блока может произойти в любом из них
                PatchMethod(harmony, type, patch, "OnBlockInteractStep");
                PatchMethod(harmony, type, patch, "OnBlockInteractStop");
            }
        }

        // --- Обработка OnBlockInteractStep ---
        public static void OnBlockInteractStepPrefix(IWorldAccessor world, BlockSelection blockSel, out Block __state)
        {
            __state = GetHarvestableBlock(world, blockSel);
        }

        public static void OnBlockInteractStepPostfix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Block __state)
        {
            ProcessHarvest(world, byPlayer, blockSel, __state);
        }

        // --- Обработка OnBlockInteractStop ---
        public static void OnBlockInteractStopPrefix(IWorldAccessor world, BlockSelection blockSel, out Block __state)
        {
            __state = GetHarvestableBlock(world, blockSel);
        }

        public static void OnBlockInteractStopPostfix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Block __state)
        {
            ProcessHarvest(world, byPlayer, blockSel, __state);
        }

        // --- Вспомогательные методы ---
        private static Block GetHarvestableBlock(IWorldAccessor world, BlockSelection blockSel)
        {
            if (world?.Api.Side != EnumAppSide.Server || blockSel == null) return null;
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            // Запоминаем сам блок, если он готов к сбору
            return (block != null && block.Variant.ContainsKey("type") && block.Variant["type"] == "harvestable") ? block : null;
        }

        private static void ProcessHarvest(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Block __state)
        {
            // Если до тика блок не был готов к сбору - выходим
            if (__state == null || world.Api.Side != EnumAppSide.Server || blockSel == null || byPlayer == null) return;

            // 1. Проверяем, изменился ли блок (завершился ли процесс сбора именно в этот тик)
            Block blockNow = world.BlockAccessor.GetBlock(blockSel.Position);
            if (blockNow != null && blockNow.Variant.ContainsKey("type") && blockNow.Variant["type"] == "harvestable") return;

            // 2. Сбор успешно произошел, проверяем инструмент
            EnumTool? tool = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Item?.Tool;
            if (tool == null || tool.Value != EnumTool.Knife) return;

            Farming farming = XLeveling.Instance(world.Api)?.SkillSetTemplate.FindSkill("farming") as Farming;
            if (farming == null) return;

            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[farming.Id];
            if (playerSkill == null) return;

            // Выдача опыта
            XSkillsSkepBehavior beh = world.GetBlock(new AssetLocation("game", "skep-populated-east"))?.GetBehavior<XSkillsSkepBehavior>();
            if (beh != null) playerSkill.AddExperience(beh.xp * 0.20f);

            // 3. Обработка перка Beekeeper
            PlayerAbility playerAbility = playerSkill[farming.BeekeeperId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                ItemStack extraDrop = null;

                // Умный поиск нужного предмета: ищем соты или мёд в родных дропах горшка
                if (__state.Drops != null)
                {
                    foreach (var drop in __state.Drops)
                    {
                        if (drop.ResolvedItemstack != null)
                        {
                            string path = drop.ResolvedItemstack.Collectible.Code.Path;
                            if (path.Contains("comb") || path.Contains("honey"))
                            {
                                CollectibleObject itemObj = drop.ResolvedItemstack.Item ?? (CollectibleObject)drop.ResolvedItemstack.Block;
                                extraDrop = new ItemStack(itemObj, playerAbility.Value(0));
                                break;
                            }
                        }
                    }

                    // Если по ключевым словам ничего не найдено, берем последний элемент дропа (обычно это лут)
                    if (extraDrop == null && __state.Drops.Length > 1)
                    {
                        var lastDrop = __state.Drops[__state.Drops.Length - 1].ResolvedItemstack;
                        if (lastDrop != null)
                        {
                            CollectibleObject itemObj = lastDrop.Item ?? (CollectibleObject)lastDrop.Block;
                            extraDrop = new ItemStack(itemObj, playerAbility.Value(0));
                        }
                    }
                }

                // Фолбэк на ванильные соты, если у блока стороннего мода вообще не прописан Drops
                if (extraDrop == null)
                {
                    Item vanillaHoneycomb = world.GetItem(new AssetLocation("game", "honeycomb"));
                    if (vanillaHoneycomb != null)
                    {
                        extraDrop = new ItemStack(vanillaHoneycomb, playerAbility.Value(0));
                    }
                }

                // 4. Аккуратная выдача предмета игроку (сначала в руки, затем на землю)
                if (extraDrop != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(extraDrop))
                    {
                        world.SpawnItemEntity(extraDrop, byPlayer.Entity.Pos.XYZ.AddCopy(0.5, 0.5, 0.5));
                    }
                }
            }
        }
    }
}