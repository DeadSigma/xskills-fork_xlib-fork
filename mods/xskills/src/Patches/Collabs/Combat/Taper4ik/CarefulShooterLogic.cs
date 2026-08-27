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

            // Исправлено: теперь не будет срабатывать на "rainbow-sword" или "elbow"
            return path == "bow" || path.StartsWith("bow-") || path.EndsWith("-bow") || type == "itembow";
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
        public static bool Prefix(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount)
        {
            // Если урон уже отменен другим модом, не вмешиваемся
            if (amount <= 0 || itemslot?.Itemstack == null) return true;

            if (!CarefulShooterRules.IsCarefulShooterDurabilityTarget(itemslot.Itemstack)) return true;
            if (world?.Side != EnumAppSide.Server) return true;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return true;

            PlayerAbility ability = CarefulShooterRules.GetAbility(player);
            if (ability == null || ability.Tier <= 0) return true;

            int chance = ability.SkillDependentValue();
            if (chance <= 0) return true;

            if (world.Rand.NextDouble() < (chance / 100f))
            {
                // Установка amount в 0 и возврат true - максимально совместимый способ отменить урон.
                // Это позволяет выполниться префиксам и постфиксам других модов, но сам метод DamageItem не нанесет урона, так как amount равен 0.
                amount = 0;
            }

            return true;
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

        // Также запрашиваем точные параметры OnHeldInteractStop
        public static void Prefix(ItemSlot slot, EntityAgent byEntity, ref SlingUseState __state)
        {
            __state = null;
            if (byEntity?.World?.Side != EnumAppSide.Server) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            if (!CarefulShooterRules.IsSling(slot?.Itemstack)) return;

            ItemSlot ammoSlot = CarefulShooterRules.FindFirstAmmoSlot(player, s => CarefulShooterRules.CanPreserveAmmo(s.Itemstack));
            if (ammoSlot?.Itemstack == null) return;

            __state = new SlingUseState
            {
                PreservableAmmoBefore = ammoSlot.Itemstack.Clone(),
                PreservableAmmoQuantityBefore = ammoSlot.Itemstack.StackSize
            };
        }

        public static void Postfix(EntityAgent byEntity, SlingUseState __state)
        {
            if (__state?.PreservableAmmoBefore == null) return;
            if (byEntity?.World?.Side != EnumAppSide.Server) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            PlayerAbility ability = CarefulShooterRules.GetAbility(player);
            if (ability == null || ability.Tier <= 0) return;

            int chance = ability.Value(3);
            if (chance <= 0 || byEntity.World.Rand.NextDouble() >= (chance / 100f)) return;

            ItemSlot ammoSlot = CarefulShooterRules.FindFirstAmmoSlot(player, s =>
                s.Itemstack != null && s.Itemstack.Equals(player.Entity.World, __state.PreservableAmmoBefore, GlobalConstants.IgnoredStackAttributes));

            int current = ammoSlot?.Itemstack?.StackSize ?? 0;
            if (current >= __state.PreservableAmmoQuantityBefore) return;

            ItemStack restored = __state.PreservableAmmoBefore.Clone();
            restored.StackSize = 1;
            if (!player.InventoryManager.TryGiveItemstack(restored, true))
            {
                byEntity.World.SpawnItemEntity(restored, player.Entity.Pos.XYZ.Add(0, 0.5, 0));
            }
        }
    }
}