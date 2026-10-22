// Code from the pandaxskills mod by user Pandarific

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public partial class XSkillsPlayerBehavior
    {
        public void ApplyTailoringAbilities()
        {
            if (this.tailoring == null) return;

            EntityStats stats = this.entity.Stats;
            if (stats == null) return;

            PlayerSkillSet behavior = this.entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = behavior?[this.tailoring.Id];
            if (playerSkill == null) return;

            bool flag = false;
            EntityPlayer entityPlayer = this.entity as EntityPlayer;
            IInventory inventory = entityPlayer?.Player?.InventoryManager?.GetOwnInventory("character");

            if (inventory != null)
            {
                foreach (ItemSlot itemSlot in inventory)
                {
                    if (!(itemSlot is ItemSlotCharacter)) continue;

                    if (itemSlot?.Itemstack?.Collectible?.HasBehavior(
                            typeof(CollectibleBehaviorWearable), true) == true)
                    {
                        CollectibleBehaviorWearable wearable = itemSlot.Itemstack.Collectible.GetCollectibleBehavior(typeof(CollectibleBehaviorWearable), true) as CollectibleBehaviorWearable;
                        if (wearable != null)
                        {
                            EnumCharacterDressType dressType = wearable.GetDressType(itemSlot);
                            if (dressType == EnumCharacterDressType.ArmorHead || dressType == EnumCharacterDressType.ArmorBody || dressType == EnumCharacterDressType.ArmorLegs)
                            {
                                flag = true;
                                break;
                            }
                        }
                    }
                }
            }

            // ReinforcedSeams
            PlayerAbility playerAbility2 = playerSkill[this.tailoring.ReinforcedSeamsId];
            if (playerAbility2 != null && playerAbility2.Tier > 0 && flag)
            {
                stats.Set("damageMul", "ability-reinforcedseams", -playerAbility2.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("damageMul", "ability-reinforcedseams");
            }

            // WanderingTailor
            int num = CountWornWearables(entityPlayer);
            PlayerAbility playerAbility3 = playerSkill[this.tailoring.WanderingTailorId];
            if (playerAbility3 != null && playerAbility3.Tier > 0 && num >= 3)
            {
                float value = playerAbility3.FValue(0, 0f);
                stats.Set("walkspeed", "ability-wanderingtailor", value, false);
                stats.Set("healingeffectivness", "ability-wanderingtailor", value, false);
            }
            else
            {
                stats.Remove("walkspeed", "ability-wanderingtailor");
                stats.Remove("healingeffectivness", "ability-wanderingtailor");
            }

            // Signature Garb
            float signatureGarbBonus;
            if (TryGetSignatureGarbOutfitBonus(entityPlayer, out signatureGarbBonus))
            {
                stats.Set("walkspeed", "ability-signaturegarb", signatureGarbBonus, false);
                stats.Set("healingeffectivness", "ability-signaturegarb", signatureGarbBonus, false);
            }
            else
            {
                stats.Remove("walkspeed", "ability-signaturegarb");
                stats.Remove("healingeffectivness", "ability-signaturegarb");
            }

            // SummerWeaver
            PlayerAbility playerAbility4 = playerSkill[this.tailoring.SummerWeaverId];
            if (playerAbility4 != null && playerAbility4.Tier > 0)
            {
                stats.Set("xskills-coolingBonus", "ability-summerweaver", playerAbility4.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("xskills-coolingBonus", "ability-summerweaver");
            }

            // FiberSorter
            PlayerAbility playerAbility5 = playerSkill[this.tailoring.FiberSorterId];
            if (playerAbility5 != null && playerAbility5.Tier > 0)
            {
                stats.Set("xskills-fiberYieldBonus", "ability-fibersorter", playerAbility5.FValue(0, 0f), false);
            }
            else
            {
                stats.Remove("xskills-fiberYieldBonus", "ability-fibersorter");
            }
        }

        private static int CountWornWearables(EntityPlayer ep)
        {
            int count = 0;
            IInventory inventory = ep?.Player?.InventoryManager?.GetOwnInventory("character");
            if (inventory == null) return 0;

            foreach (ItemSlot slot in inventory)
            {
                if (!(slot is ItemSlotCharacter)) continue;

                if (slot.Itemstack?.Collectible?.HasBehavior(
                        typeof(CollectibleBehaviorWearable), true) == true)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetSignatureGarbOutfitBonus(EntityPlayer ep, out float bonus)
        {
            bonus = 0f;

            IInventory inventory =
                ep?.Player?.InventoryManager?.GetOwnInventory("character");
            if (inventory == null) return false;

            string crafterUid = null;
            int wearableCount = 0;

            foreach (ItemSlot slot in inventory)
            {
                if (!(slot is ItemSlotCharacter)) continue;

                ItemStack stack = slot.Itemstack;
                if (stack?.Collectible?.HasBehavior(
                        typeof(CollectibleBehaviorWearable), true) != true)
                {
                    continue;
                }

                ITreeAttribute attributes = stack.Attributes;
                string itemCrafterUid =
                    attributes?.GetString("xskills-crafterUid");
                int itemTier =
                    attributes?.GetInt("xskills-signaturegarb-tier", 0) ?? 0;
                float itemBonus =
                    attributes?.GetFloat("xskills-signaturegarb-bonus", 0f) ?? 0f;

                // Every equipped wearable must have been crafted while its crafter
                // had Signature Garb.
                if (string.IsNullOrEmpty(itemCrafterUid) || itemTier <= 0)
                {
                    return false;
                }

                if (crafterUid == null)
                {
                    crafterUid = itemCrafterUid;
                    bonus = itemBonus;
                }
                else
                {
                    if (!string.Equals(
                            crafterUid, itemCrafterUid, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    // If the crafter's tier changed between crafts, use the lowest
                    // recorded bonus across the outfit.
                    bonus = Math.Min(bonus, itemBonus);
                }

                wearableCount++;
            }

            return wearableCount >= 3;
        }
    }
}