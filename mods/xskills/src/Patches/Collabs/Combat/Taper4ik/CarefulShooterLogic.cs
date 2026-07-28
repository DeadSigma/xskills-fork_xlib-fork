//Код от пользователя Taper4ik
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace xskills.src.Patches.Collabs.Combat.Taper4ik
{
    internal static class CarefulShooterRules
    {
        private static readonly string[] Allowlist = { "game:stone-", "game:looseflint", "game:flint" };
        private static readonly string[] DenyHints = { "meteoriciron", "iron", "steel", "copper", "bronze", "silver", "gold", "ore", "nugget", "ingot", "metal" };

        public static bool IsSling(ItemStack stack)
        {
            if (stack?.Collectible?.Code == null) return false;
            string path = stack.Collectible.Code.Path.ToLowerInvariant();
            string type = stack.Collectible.GetType().Name.ToLowerInvariant();
            return path.Contains("sling") || type.Contains("sling");
        }

        public static bool IsCarefulShooterDurabilityTarget(ItemStack stack)
        {
            if (stack?.Collectible?.Code == null) return false;
            string path = stack.Collectible.Code.Path.ToLowerInvariant();
            string type = stack.Collectible.GetType().Name.ToLowerInvariant();

            if (path.Contains("sling") || type.Contains("sling")) return true;
            if (path.Contains("crossbow") || type.Contains("crossbow")) return true;

            return path == "bow" || path.Contains("bow-") || path.EndsWith("bow", StringComparison.Ordinal) || type == "itembow";
        }

        public static bool CanPreserveAmmo(ItemStack stack)
        {
            if (stack?.Collectible?.Code == null) return false;
            string code = stack.Collectible.Code.ToString().ToLowerInvariant();
            string path = stack.Collectible.Code.Path.ToLowerInvariant();

            if (DenyHints.Any(path.Contains)) return false;
            return Allowlist.Any(code.StartsWith);
        }

        public static PlayerAbility GetAbility(IPlayer player)
        {
            if (player?.Entity == null) return null;
            PlayerSkillSet skillSet = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill combatSkill = skillSet?.FindSkill("combat");
            return combatSkill?.FindAbility("carefulshooter");
        }

        public static ItemSlot FindFirstAmmoSlot(IPlayer player, System.Func<ItemSlot, bool> predicate)
        {
            foreach (InventoryBase inv in player.InventoryManager.InventoriesOrdered)
            {
                foreach (ItemSlot slot in inv)
                {
                    if (predicate(slot)) return slot;
                }
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.DamageItem))]
    internal static class CarefulShooterDurabilityPatch
    {
        public static bool Prefix(object[] __args)
        {
            ItemSlot slot = __args.OfType<ItemSlot>().FirstOrDefault();
            if (slot?.Itemstack == null || !CarefulShooterRules.IsCarefulShooterDurabilityTarget(slot.Itemstack)) return true;

            Entity entity = __args.OfType<Entity>().FirstOrDefault();
            if (entity?.World?.Side != EnumAppSide.Server) return true;

            IPlayer player = (entity as EntityPlayer)?.Player;
            if (player == null) return true;

            PlayerAbility ability = CarefulShooterRules.GetAbility(player);
            if (ability == null || ability.Tier <= 0) return true;

            int chance = ability.SkillDependentValue();
            if (chance <= 0) return true;

            return entity.World.Rand.NextDouble() >= (chance / 100f);
        }
    }

    internal sealed class SlingUseState
    {
        public ItemStack PreservableAmmoBefore { get; init; }
        public int PreservableAmmoQuantityBefore { get; init; }
    }

    [HarmonyPatch]
    internal static class CarefulShooterAmmoPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ItemSling), "OnHeldInteractStop");
        }

        public static void Prefix(object[] __args, ref SlingUseState __state)
        {
            __state = null;
            EntityAgent agent = __args.OfType<EntityAgent>().FirstOrDefault();
            if (agent?.World?.Side != EnumAppSide.Server) return;

            IPlayer player = (agent as EntityPlayer)?.Player;
            if (player == null) return;

            ItemSlot slingSlot = __args.OfType<ItemSlot>().FirstOrDefault();
            if (!CarefulShooterRules.IsSling(slingSlot?.Itemstack)) return;

            ItemSlot ammoSlot = CarefulShooterRules.FindFirstAmmoSlot(player, slot => CarefulShooterRules.CanPreserveAmmo(slot.Itemstack));
            if (ammoSlot?.Itemstack == null) return;

            __state = new SlingUseState
            {
                PreservableAmmoBefore = ammoSlot.Itemstack.Clone(),
                PreservableAmmoQuantityBefore = ammoSlot.Itemstack.StackSize
            };
        }

        public static void Postfix(object[] __args, SlingUseState __state)
        {
            if (__state?.PreservableAmmoBefore == null) return;

            EntityAgent agent = __args.OfType<EntityAgent>().FirstOrDefault();
            if (agent?.World?.Side != EnumAppSide.Server) return;

            IPlayer player = (agent as EntityPlayer)?.Player;
            if (player == null) return;

            PlayerAbility ability = CarefulShooterRules.GetAbility(player);
            if (ability == null || ability.Tier <= 0) return;

            int chance = ability.Value(3);
            if (chance <= 0 || agent.World.Rand.NextDouble() >= (chance / 100f)) return;

            ItemSlot ammoSlot = CarefulShooterRules.FindFirstAmmoSlot(player, slot =>
                slot.Itemstack != null && slot.Itemstack.Equals(player.Entity.World, __state.PreservableAmmoBefore, GlobalConstants.IgnoredStackAttributes));

            int current = ammoSlot?.Itemstack?.StackSize ?? 0;
            if (current >= __state.PreservableAmmoQuantityBefore) return;

            ItemStack restored = __state.PreservableAmmoBefore.Clone();
            restored.StackSize = 1;
            if (!player.InventoryManager.TryGiveItemstack(restored, true))
            {
                agent.World.SpawnItemEntity(restored, player.Entity.Pos.XYZ.Add(0, 0.5, 0));
            }
        }
    }
}