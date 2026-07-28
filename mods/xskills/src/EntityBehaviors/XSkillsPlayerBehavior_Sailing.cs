// Код из мода pandaxskills от пользователя Pandarific

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using XLib.XLeveling;

namespace XSkills
{
    public partial class XSkillsPlayerBehavior
    {
        public void ApplySailingAbilities()
        {
            if (this.sailing == null)
            {
                return;
            }
            EntityStats stats = this.entity.Stats;
            if (stats == null)
            {
                return;
            }
            PlayerSkillSet behavior = this.entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[this.sailing.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            PlayerAbility playerAbility = playerSkill[this.sailing.IronForearmsId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                stats.Set("xskills-ratlineStaminaCostReduction", "ability-ironforearms", playerAbility.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("xskills-ratlineStaminaCostReduction", "ability-ironforearms");
            }
            PlayerAbility playerAbility2 = playerSkill[this.sailing.RiggerId];
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                stats.Set("xskills-ratlineClimbSpeed", "ability-rigger", playerAbility2.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("xskills-ratlineClimbSpeed", "ability-rigger");
            }
            PlayerAbility playerAbility3 = playerSkill[this.sailing.ReinforcedDeckingId];
            if (playerAbility3 != null && playerAbility3.Tier > 0)
            {
                stats.Set("xskills-boatDurabilityBonus", "ability-reinforceddecking", playerAbility3.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("xskills-boatDurabilityBonus", "ability-reinforceddecking");
            }
            PlayerAbility playerAbility4 = playerSkill[this.sailing.CargoCaptainId];
            if (playerAbility4 != null && playerAbility4.Tier > 0)
            {
                stats.Set("xskills-cargoCapacityBonus", "ability-cargocaptain", playerAbility4.FValue(0, 0f), false);
                return;
            }
            stats.Remove("xskills-cargoCapacityBonus", "ability-cargocaptain");
        }
    }
}