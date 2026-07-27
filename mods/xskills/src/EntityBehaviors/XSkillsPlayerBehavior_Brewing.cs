// Код из мода pandaxskills от пользователя Pandarific

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using XLib.XLeveling;

namespace XSkills
{
    public partial class XSkillsPlayerBehavior
    {
        public void ApplyBrewingAbilities()
        {
            if (this.brewing == null)
            {
                return;
            }
            PlayerSkillSet behavior = this.entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[this.brewing.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            EntityStats stats = this.entity.Stats;
            if (stats != null)
            {
                PlayerAbility playerAbility = playerSkill[this.brewing.BigBatchId];
                if (playerAbility != null && playerAbility.Tier > 0)
                {
                    stats.Set("xskills-barrelCapacity", "ability-bigbatch", playerAbility.FValue(0, 0f), false);
                }
                else
                {
                    stats.Remove("xskills-barrelCapacity", "ability-bigbatch");
                }
                PlayerAbility playerAbility2 = playerSkill[this.brewing.ReputableTavernkeepId];
                if (playerAbility2 != null && playerAbility2.Tier > 0)
                {
                    stats.Set("xskills-tradePriceBonus", "ability-reputabletavernkeep", playerAbility2.FValue(0, 0f), false);
                }
                else
                {
                    stats.Remove("xskills-tradePriceBonus", "ability-reputabletavernkeep");
                }
            }
            if (!this.entity.WatchedAttributes.HasAttribute("intoxication"))
            {
                return;
            }
            float @float = this.entity.WatchedAttributes.GetFloat("intoxication", 0f);
            if (@float <= 0f)
            {
                return;
            }
            PlayerAbility playerAbility3 = playerSkill[this.brewing.TipplersToleranceId];
            if (playerAbility3 != null && playerAbility3.Tier > 0)
            {
                this.entity.WatchedAttributes.SetFloat("intoxication", 0f);
                this.entity.WatchedAttributes.MarkPathDirty("intoxication");
                return;
            }
            PlayerAbility playerAbility4 = playerSkill[this.brewing.HangoverResistanceId];
            if (playerAbility4 != null && playerAbility4.Tier > 0)
            {
                float num = @float * (1f - playerAbility4.FValue(0, 0f));
                if (num != @float)
                {
                    this.entity.WatchedAttributes.SetFloat("intoxication", num);
                    this.entity.WatchedAttributes.MarkPathDirty("intoxication");
                }
            }
        }
    }
}