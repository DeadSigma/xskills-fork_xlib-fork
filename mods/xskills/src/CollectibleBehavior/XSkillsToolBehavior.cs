using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using XLib.XEffects;
using XLib.XLeveling;

namespace XSkills
{
    public class XSkillsToolBehavior : XLibToolBehavior
    {
        public XSkillsToolBehavior(CollectibleObject collObj) : base(collObj)
        { }

        // 1. Переименовали OnGetMaxDurability в GetMaxDurability
        public override int GetMaxDurability(ItemStack itemstack, int durability, ref EnumHandling bhHandling)
        {
            if (durability <= 1) return 0;
            bhHandling = EnumHandling.Handled;
            float quality = itemstack.Attributes.GetFloat("quality", 0.0f);

            // Считаем бонус от качества
            int bonusDurability = (int)(durability * quality * 0.05f);

            // Возвращаем итоговую прочность (базовая + бонус)
            return durability + bonusDurability;
        }

        // 3. Убедились, что тут используется GetMiningSpeed и ItemStack (а не IItemStack)
        public override float GetMiningSpeed(ItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref EnumHandling bhHandling)
        {
            // 4. Вызываем обновленный базовый метод GetMiningSpeed вместо OnGetMiningSpeed
            float result = base.GetMiningSpeed(itemstack, blockSel, block, forPlayer, ref bhHandling);
            float quality = itemstack.Attributes.GetFloat("quality", 0.0f);

            // 5. Поправил баг оригинального мода: теперь качество добавляет 2% за единицу, а не умножает всю скорость на ноль
            return result * (1.0f + quality * 0.02f);
        }

        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling)
        {
            // Этот метод оставляем без изменений, тут всё отлично
            string sskill = null;
            ItemStack itemstack = itemslot.Itemstack;
            if (itemstack == null || byEntity == null) return;
            switch (itemstack.Collectible.Tool)
            {
                case EnumTool.Pickaxe:
                    sskill = "mining";
                    break;
                case EnumTool.Axe:
                    sskill = "forestry";
                    break;
                case EnumTool.Shovel:
                    sskill = "digging";
                    break;
                default:
                    sskill = null;
                    break;
            }
            if (sskill == null) return;

            PlayerSkillSet skillSet = byEntity.GetBehavior<PlayerSkillSet>();
            if (skillSet == null) return;

            CollectingSkill skill = skillSet.XLeveling.GetSkill(sskill) as CollectingSkill;
            if (skill == null) return;

            PlayerAbility ability = skillSet[skill.Id][skill.DurabilityId];
            if (ability != null)
            {
                float bonus = amount * ability.SkillDependentFValue();
                amount -= (int)bonus;
                bonus -= (int)bonus;
                if (bonus >= world.Rand.NextDouble()) amount--;
            }
        }
    }
}