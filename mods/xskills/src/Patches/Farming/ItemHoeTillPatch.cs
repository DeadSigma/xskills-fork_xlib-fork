using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(ItemHoe))]
    [HarmonyPatch("DoTill")]
    public class ItemHoeTillPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EntityAgent byEntity, BlockSelection blockSel, out bool __state)
        {
            __state = false;
            if (byEntity?.World?.Side != EnumAppSide.Server || blockSel == null) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            __state = block != null && !block.Code.Path.Contains("farmland");
        }

        [HarmonyPostfix]
        public static void Postfix(EntityAgent byEntity, BlockSelection blockSel, bool __state)
        {
            // __state == false - либо не сервер, либо цель уже была грядкой до действия
            if (!__state) return;
            if (byEntity?.World?.Side != EnumAppSide.Server || blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            // Проверяем, что блок действительно стал грядкой после действия мотыги
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block == null || !block.Code.Path.Contains("farmland")) return;

            Farming farming = XLeveling.Instance(byEntity.Api)?.GetSkill("farming") as Farming;
            if (farming == null) return;

            PlayerSkill playerSkill = byEntity.GetBehavior<PlayerSkillSet>()?[farming.Id];
            if (playerSkill == null) return;

            playerSkill.AddExperience(0.1f);
        }
    }
}