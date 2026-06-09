using HarmonyLib;
using Vintagestory.API.Common;
using XSkills;
using XLib.XLeveling;

namespace XSkills.Compatibility
{
    // Патчим ванильный метод, но с низким приоритетом, чтобы выполниться после IM
    [HarmonyPatch(typeof(Block), "OnGettingBroken")]
    public class ImmersiveMining
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(IPlayer player, ItemSlot itemslot, ref float dt)
        {
            // Если урон нулевой или игрока нет, выходим
            if (player?.Entity == null || dt <= 0f) return;

            EnumTool? tool = itemslot?.Itemstack?.Item?.Tool;
            if (tool == null) return;

            PlayerSkillSet skillSet = player.Entity.GetBehavior<PlayerSkillSet>();
            if (skillSet == null) return;

            XLeveling xLeveling = XLeveling.Instance(player.Entity.Api);
            float speedMultiplier = 1.0f;

            // Проверяем КИРКУ (Шахтер)
            if (tool == EnumTool.Pickaxe)
            {
                Mining miningSkill = xLeveling.GetSkill("mining") as Mining;
                PlayerAbility speedAbility = skillSet[miningSkill.Id]?.PlayerAbilities[miningSkill.MiningSpeedId];

                if (speedAbility != null && speedAbility.Tier > 0)
                {
                    // Добавляем бонус скорости от xSkills (замени вызов на нужный тебе расчет стаков)
                    speedMultiplier += speedAbility.SkillDependentFValue();
                }
            }
            // Проверяем ТОПОР
            else if (tool == EnumTool.Axe)
            {
                // Замени "Forestry" на точный ID твоего скилла
                Forestry lumberSkill = xLeveling.GetSkill("forestry") as Forestry;
                PlayerAbility speedAbility = skillSet[lumberSkill.Id]?.PlayerAbilities[lumberSkill.MiningSpeedId]; 

                if (speedAbility != null && speedAbility.Tier > 0)
                    speedMultiplier += speedAbility.SkillDependentFValue();
            }
            // Добавить лопату и т.д.
            else if (tool == EnumTool.Shovel)
            {
                // То же самое
            }

            dt *= speedMultiplier;
        }
    }
}