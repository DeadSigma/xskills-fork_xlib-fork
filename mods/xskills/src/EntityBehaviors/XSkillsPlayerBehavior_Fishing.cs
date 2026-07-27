using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public partial class XSkillsPlayerBehavior
    {
        public void ApplyFishingAbilities()
        {
            if (this.fishing == null) return;

            EntityStats stats = this.entity.Stats;
            if (stats == null) return;

            PlayerSkillSet behavior = this.entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = behavior?[this.fishing.Id];
            if (playerSkill == null) return;

            EntityPlayer entityPlayer = this.entity as EntityPlayer;
            object obj = null;
            if (entityPlayer != null)
            {
                ItemSlot rightHandItemSlot = entityPlayer.RightHandItemSlot;
                if (rightHandItemSlot != null)
                {
                    ItemStack itemstack = rightHandItemSlot.Itemstack;
                    obj = itemstack?.Collectible;
                }
            }

            bool flag = obj is ItemFishingPole;

            // Код из мода pandaxskills от пользователя Pandarific
            // AncientMarinerId логика
            PlayerAbility playerAbility = playerSkill[this.fishing.AncientMarinerId];
            if (playerAbility != null && playerAbility.Tier > 0 && flag)
            {
                stats.Set("hungerrate", "ability-ancientmariner", -playerAbility.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("hungerrate", "ability-ancientmariner");
            }
        }
    }
}