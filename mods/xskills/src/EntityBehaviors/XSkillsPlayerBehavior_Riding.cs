// Код из мода pandaxskills от пользователя Pandarific

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public partial class XSkillsPlayerBehavior
    {
        public void ApplyRidingAbilities()
        {
            if (this.riding == null)
            {
                return;
            }
            if (this.entity.World == null)
            {
                return;
            }
            EntityStats stats = this.entity.Stats;
            if (stats == null)
            {
                return;
            }
            PlayerSkillSet behavior = this.entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[this.riding.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            EntityAgent entityAgent = this.entity as EntityAgent;
            bool flag = ((entityAgent != null) ? entityAgent.MountedOn : null) != null;
            bool flag2 = false;
            if (flag)
            {
                Entity entity = null;
                IMountable mountSupplier = entityAgent.MountedOn.MountSupplier;
                EntityBoat entityBoat = mountSupplier as EntityBoat;
                if (entityBoat != null)
                {
                    entity = entityBoat;
                }
                else
                {
                    EntityBehavior entityBehavior = mountSupplier as EntityBehavior;
                    if (entityBehavior != null)
                    {
                        entity = entityBehavior.entity;
                    }
                }
                if (((entity != null) ? entity.Pos : null) != null)
                {
                    flag2 = (entity.Pos.Motion.LengthSq() > 0.0001);
                }
            }
            PlayerAbility playerAbility = playerSkill[this.riding.TrailProvisionsId];
            if (playerAbility != null && playerAbility.Tier > 0 && flag)
            {
                stats.Set("hungerrate", "ability-trailprovisions", -playerAbility.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("hungerrate", "ability-trailprovisions");
            }
            PlayerAbility playerAbility2 = playerSkill[this.riding.MountedCombatantId];
            if (playerAbility2 != null && playerAbility2.Tier > 0 && flag && flag2)
            {
                stats.Set("meleeWeaponsDamage", "ability-mountedcombatant", playerAbility2.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("meleeWeaponsDamage", "ability-mountedcombatant");
            }
            PlayerAbility playerAbility3 = playerSkill[this.riding.SaddleSmithId];
            if (playerAbility3 != null && playerAbility3.Tier > 0 && flag)
            {
                stats.Set("walkspeed", "ability-saddlesmith", playerAbility3.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("walkspeed", "ability-saddlesmith");
            }
            PlayerAbility playerAbility4 = playerSkill[this.riding.PackAnimalId];
            if (playerAbility4 != null && playerAbility4.Tier > 0 && flag)
            {
                stats.Set("carryWeightLimit", "ability-packanimal", (float)playerAbility4.Value(0, 0), false);
                return;
            }
            stats.Remove("carryWeightLimit", "ability-packanimal");
        }
    }
}