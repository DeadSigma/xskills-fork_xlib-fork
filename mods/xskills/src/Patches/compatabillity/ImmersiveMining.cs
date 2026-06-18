using System.Collections.Concurrent;
using HarmonyLib;
using Vintagestory.API.Common;
using XSkills;
using XLib.XLeveling;

namespace XSkills.Compatibility
{
    // Патчим сразу весь класс Block, указывая конкретные методы внутри
    [HarmonyPatch(typeof(Block))]
    public class Block_xSkills_Momentum_Patches
    {
        private static ConcurrentDictionary<string, int> MomentumStacks = new ConcurrentDictionary<string, int>();
        private static ConcurrentDictionary<string, long> LastHitTimes = new ConcurrentDictionary<string, long>();

        // =====================================================================
        // 1. УМНОЖЕНИЕ УРОНА И ОБНОВЛЕНИЕ ТАЙМЕРА (Срабатывает при каждом ударе)
        // =====================================================================
        [HarmonyPatch("OnGettingBroken")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static void Prefix_OnGettingBroken(IPlayer player, ItemSlot itemslot, ref float dt)
        {
            if (player?.Entity == null || dt <= 0f) return;

            EnumTool? tool = itemslot?.Itemstack?.Item?.Tool;
            if (tool != EnumTool.Pickaxe && tool != EnumTool.Axe && tool != EnumTool.Shovel) return;

            PlayerSkillSet skillSet = player.Entity.GetBehavior<PlayerSkillSet>();
            if (skillSet == null) return;

            XLeveling xLeveling = XLeveling.Instance(player.Entity.Api);
            float speedMultiplier = 1.0f;

            // Разделяем память клиента и сервера
            string key = player.Entity.Api.Side.ToString() + "_" + player.PlayerUID;

            if (tool == EnumTool.Pickaxe)
            {
                Mining miningSkill = xLeveling.GetSkill("mining") as Mining;
                if (miningSkill != null)
                {
                    PlayerAbility speedAbility = skillSet[miningSkill.Id]?.PlayerAbilities[miningSkill.MiningSpeedId];

                    if (speedAbility != null && speedAbility.Tier > 0)
                    {
                        long now = player.Entity.World.ElapsedMilliseconds;
                        int durationMs = speedAbility.Value(4) > 0 ? speedAbility.Value(4) * 1000 : 4000;

                        // Сбрасываем стаки, если игрок слишком долго ни по чему не бил
                        if (!LastHitTimes.ContainsKey(key) || (now - LastHitTimes[key]) > durationMs)
                        {
                            MomentumStacks[key] = 0;
                        }

                        // Обновляем таймер при каждом ударе, чтобы комбо не спадало на твердых блоках!
                        LastHitTimes[key] = now;

                        // Вытаскиваем накопленные стаки и применяем множитель
                        int currentStacks = MomentumStacks.ContainsKey(key) ? MomentumStacks[key] : 0;
                        float bonusPerStack = speedAbility.SkillDependentFValue();
                        speedMultiplier += currentStacks * bonusPerStack;
                    }
                }
            }

            // Домножаем урон
            dt *= speedMultiplier;
        }

        // =====================================================================
        // 2. ВЫДАЧА СТАКОВ (Срабатывает ТОЛЬКО когда блок сломан)
        // =====================================================================
        [HarmonyPatch("OnBlockBroken")]
        [HarmonyPostfix]
        public static void Postfix_OnBlockBroken(IPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            // Вытаскиваем инструмент из активного слота
            ItemSlot itemslot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            EnumTool? tool = itemslot?.Itemstack?.Item?.Tool;

            if (tool != EnumTool.Pickaxe && tool != EnumTool.Axe && tool != EnumTool.Shovel) return;

            PlayerSkillSet skillSet = byPlayer.Entity.GetBehavior<PlayerSkillSet>();
            if (skillSet == null) return;

            XLeveling xLeveling = XLeveling.Instance(byPlayer.Entity.Api);
            string key = byPlayer.Entity.Api.Side.ToString() + "_" + byPlayer.PlayerUID;

            if (tool == EnumTool.Pickaxe)
            {
                Mining miningSkill = xLeveling.GetSkill("mining") as Mining;
                if (miningSkill != null)
                {
                    PlayerAbility speedAbility = skillSet[miningSkill.Id]?.PlayerAbilities[miningSkill.MiningSpeedId];

                    if (speedAbility != null && speedAbility.Tier > 0)
                    {
                        int maxStacks = speedAbility.Value(3) > 0 ? speedAbility.Value(3) : 10;

                        if (!MomentumStacks.ContainsKey(key))
                        {
                            MomentumStacks[key] = 0;
                        }

                        // Блок успешно разрушен — выдаем 1 стак Импульса!
                        if (MomentumStacks[key] < maxStacks)
                        {
                            MomentumStacks[key]++;
                        }
                    }
                }
            }
        }
    }
}