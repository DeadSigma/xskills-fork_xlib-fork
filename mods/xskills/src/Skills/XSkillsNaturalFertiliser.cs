using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public class XSkillsNaturalFertiliser : BlockBehavior
    {
        private Husbandry husbandry;
        private Farming farming;
        private float xp;

        public XSkillsNaturalFertiliser(Block block) : base(block) { }

        public override void Initialize(JsonObject properties)
        {
            this.xp = properties["xp"].AsFloat(1.0f);
            base.Initialize(properties);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.husbandry = XLeveling.Instance(api)?.GetSkill("husbandry") as Husbandry;
            this.farming = XLeveling.Instance(api)?.GetSkill("farming") as Farming;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handling)
        {
            if (byPlayer == null || husbandry == null || xp <= 0.0f) return;
            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
            if (playerSkill == null) return;

            // award full xp to Husbandry
            playerSkill.AddExperience(this.xp);

            // award half of that xp to Farming (if available)
            if (farming != null)
            {
                PlayerSkill farmSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[farming.Id];
                if (farmSkill != null) farmSkill.AddExperience(this.xp * 0.5f);
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
        }
    }
}
