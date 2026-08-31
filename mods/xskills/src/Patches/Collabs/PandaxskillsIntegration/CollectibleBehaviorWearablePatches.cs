using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills
{
    [HarmonyPatch(typeof(CollectibleBehaviorWearable))]
    public class CollectibleBehaviorWearablePatches
    {
        public static bool Prepare(MethodBase original)
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnCreatedByCrafting")]
        public static void OnCreatedByCraftingPostfix(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            if (((outputSlot != null) ? outputSlot.Itemstack : null) == null)
            {
                return;
            }
            InventoryBasePlayer inventoryBasePlayer = outputSlot.Inventory as InventoryBasePlayer;
            IPlayer player = (inventoryBasePlayer != null) ? inventoryBasePlayer.Player : null;
            if (((player != null) ? player.Entity : null) == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Tailoring tailoring = ((instance != null) ? instance.Skills["tailoring"] : null) as Tailoring;
            if (tailoring == null)
            {
                return;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[tailoring.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            ITreeAttribute attributes = outputSlot.Itemstack.Attributes;
            if (attributes != null)
            {
                attributes.SetString("xskills-crafterUid", player.PlayerUID);
            }
            float num = 1f;
            PlayerAbility playerAbility = playerSkill[tailoring.DurableWeaveId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                num += playerAbility.FValue(0, 0f);
            }
            bool flag = CollectibleBehaviorWearablePatches.HasInput(inSlots, "leather");
            bool flag2 = CollectibleBehaviorWearablePatches.HasInput(inSlots, "linen") || CollectibleBehaviorWearablePatches.HasInput(inSlots, "cloth") || CollectibleBehaviorWearablePatches.HasInput(inSlots, "flax") || CollectibleBehaviorWearablePatches.HasInput(inSlots, "cotton");
            PlayerAbility playerAbility2 = playerSkill[tailoring.LeatherWorkerId];
            if (playerAbility2 != null && playerAbility2.Tier > 0 && flag)
            {
                num += playerAbility2.FValue(0, 0f);
            }
            PlayerAbility playerAbility3 = playerSkill[tailoring.ClothWeaverId];
            if (playerAbility3 != null && playerAbility3.Tier > 0 && flag2)
            {
                num += playerAbility3.FValue(0, 0f);
            }
            PlayerAbility playerAbility4 = playerSkill[tailoring.ReinforcedSeamsId];
            if (playerAbility4 != null && playerAbility4.Tier > 0)
            {
                float protectionBonus = playerAbility4.FValue(0, 0f);

                outputSlot.Itemstack.Attributes.SetFloat("reinforcedProtection", protectionBonus);
                outputSlot.MarkDirty();
            }
            if (num > 1.001f)
            {
                int durability = outputSlot.Itemstack.Collectible.Durability;
                if (durability > 0)
                {
                    int value = (int)Math.Round((double)((float)durability * num));
                    outputSlot.Itemstack.Attributes.SetInt("maxdurability", value);
                    outputSlot.Itemstack.Attributes.SetInt("durability", value);
                    outputSlot.MarkDirty();
                    IServerPlayer serverPlayer = player as IServerPlayer;
                    if (serverPlayer != null)
                    {
                    }
                }
            }
        }

        private static bool HasInput(ItemSlot[] slots, string substr)
        {
            if (slots == null)
            {
                return false;
            }
            foreach (ItemSlot itemSlot in slots)
            {
                string text;
                if (itemSlot == null)
                {
                    text = null;
                }
                else
                {
                    ItemStack itemstack = itemSlot.Itemstack;
                    if (itemstack == null)
                    {
                        text = null;
                    }
                    else
                    {
                        CollectibleObject collectible = itemstack.Collectible;
                        if (collectible == null)
                        {
                            text = null;
                        }
                        else
                        {
                            AssetLocation code = collectible.Code;
                            text = ((code != null) ? code.Path : null);
                        }
                    }
                }
                string text2 = text;
                if (text2 != null && text2.Contains(substr))
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ConsumeCraftingIngredients")]
        public static void ConsumeCraftingIngredientsPostfix(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase recipe)
        {
            if (((outputSlot != null) ? outputSlot.Itemstack : null) == null || inSlots == null)
            {
                return;
            }
            InventoryBasePlayer inventoryBasePlayer = outputSlot.Inventory as InventoryBasePlayer;
            IPlayer player = (inventoryBasePlayer != null) ? inventoryBasePlayer.Player : null;
            if (((player != null) ? player.Entity : null) == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Tailoring tailoring = ((instance != null) ? instance.Skills["tailoring"] : null) as Tailoring;
            if (tailoring == null)
            {
                return;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[tailoring.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            playerSkill.AddExperience(1f, true);
            IServerPlayer serverPlayer = player as IServerPlayer;
            if (serverPlayer != null)
            {
            }
            PlayerAbility playerAbility = playerSkill[tailoring.PatternMemoryId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                double num = player.Entity.World.Rand.NextDouble();
                double num2 = (double)playerAbility.FValue(0, 0f);
                if (num < num2)
                {
                    foreach (ItemSlot itemSlot in inSlots)
                    {
                        if (((itemSlot != null) ? itemSlot.Itemstack : null) != null)
                        {
                            itemSlot.Itemstack.StackSize++;
                            itemSlot.MarkDirty();
                        }
                    }
                    IServerPlayer serverPlayer2 = player as IServerPlayer;
                    if (serverPlayer2 != null)
                    {

                    }
                    return;
                }
            }
            PlayerAbility playerAbility2 = playerSkill[tailoring.ThriftyCutsId];
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                double num3 = (double)playerAbility2.FValue(0, 0f);
                int num4 = 0;
                foreach (ItemSlot itemSlot2 in inSlots)
                {
                    if (((itemSlot2 != null) ? itemSlot2.Itemstack : null) != null && player.Entity.World.Rand.NextDouble() < num3)
                    {
                        itemSlot2.Itemstack.StackSize++;
                        itemSlot2.MarkDirty();
                        num4++;
                    }
                }
                if (num4 > 0)
                {
                    IServerPlayer serverPlayer3 = player as IServerPlayer;
                    if (serverPlayer3 != null)
                    {

                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetWarmth")]
        public static void GetWarmthPostfix(ItemSlot inslot, ref float __result)
        {
            if (((inslot != null) ? inslot.Inventory : null) == null || __result == 0f)
            {
                return;
            }
            InventoryBasePlayer inventoryBasePlayer = inslot.Inventory as InventoryBasePlayer;
            if (inventoryBasePlayer == null)
            {
                return;
            }
            IPlayer player = inventoryBasePlayer.Player;
            if (((player != null) ? player.Entity : null) == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Tailoring tailoring = ((instance != null) ? instance.Skills["tailoring"] : null) as Tailoring;
            if (tailoring == null)
            {
                return;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[tailoring.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[tailoring.WinterWeaverId] : null);
            }
            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                float num = __result;
                __result *= 1f + playerAbility2.FValue(0, 0f);

            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("CalculateRepairValue")]
        public static void CalculateRepairValuePostfix(ItemSlot[] inSlots, ItemSlot outputSlot, ref float repairValue, ref int matCostPerMatType)
        {
            if (((outputSlot != null) ? outputSlot.Itemstack : null) == null)
            {
                return;
            }
            InventoryBasePlayer inventoryBasePlayer = outputSlot.Inventory as InventoryBasePlayer;
            IPlayer player = (inventoryBasePlayer != null) ? inventoryBasePlayer.Player : null;
            if (((player != null) ? player.Entity : null) == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Tailoring tailoring = ((instance != null) ? instance.Skills["tailoring"] : null) as Tailoring;
            if (tailoring == null)
            {
                return;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[tailoring.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[tailoring.PatchworkId] : null);
            }
            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                float num = repairValue;
                repairValue *= 1f + playerAbility2.FValue(0, 0f);
                IServerPlayer serverPlayer = player as IServerPlayer;
                if (serverPlayer != null)
                {

                }
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch("GetProtectionModifiers")]
        public static void GetProtectionModifiersPostfix(ItemSlot slot, ref ProtectionModifiers __result)
        {
            if (slot == null || slot.Itemstack == null || slot.Itemstack.Attributes == null || __result == null)
            {
                return;
            }

            float bonus = slot.Itemstack.Attributes.GetFloat("reinforcedProtection", 0f);

            if (bonus > 0f)
            {
                ProtectionModifiers mod = new ProtectionModifiers();

                mod.FlatDamageReduction = __result.FlatDamageReduction;
                mod.ProtectionTier = __result.ProtectionTier;
                mod.HighDamageTierResistant = __result.HighDamageTierResistant;
                mod.PerTierFlatDamageReductionLoss = __result.PerTierFlatDamageReductionLoss;
                mod.PerTierRelativeProtectionLoss = __result.PerTierRelativeProtectionLoss;

                mod.RelativeProtection = __result.RelativeProtection + bonus;
                __result = mod;

            }
        }
    }
}