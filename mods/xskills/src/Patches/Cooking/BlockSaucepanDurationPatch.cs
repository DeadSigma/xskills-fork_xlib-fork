using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Adds optional XSkills duration handling to ACA saucepan cooking without a compile-time ACA dependency.
    /// </summary>
    [HarmonyPatch]
    public static class BlockSaucepanDurationPatch
    {
        private static Type saucepanType;
        private static MethodBase targetMethod;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            return FindTargetMethod() != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return FindTargetMethod();
        }

        [HarmonyPostfix]
        public static void GetMeltingDurationPostfix(
            ref float __result,
            IWorldAccessor __0,
            ISlotProvider __1,
            ItemSlot __2)
        {
            IWorldAccessor world = __0;
            ISlotProvider cookingSlotsProvider = __1;
            ItemSlot inputSlot = __2;

            if (__result <= 0.0f || world == null) return;

            InventoryBase inventory = cookingSlotsProvider as InventoryBase;
            if (inventory == null || inventory.Pos == null) return;

            IPlayer player = CookingUtil.GetOwnerFromInventory(inventory);
            if (player?.Entity == null) return;

            Cooking cooking = player.Entity.Api.ModLoader
                .GetModSystem<XLeveling>()?
                .GetSkill("cooking") as Cooking;

            if (cooking == null) return;

            ItemStack saucepanStack = inputSlot?.Itemstack;
            if (saucepanStack != null)
            {
                cooking.TryGetWellDoneBonuses(
                    player,
                    out float shelfLifeBonus,
                    out _
                );

                // Store the pre-completion value so the finished product receives the same Well Done bonus.
                saucepanStack.Attributes.SetFloat(
                    Cooking.WellDoneShelfLifeAttribute,
                    shelfLifeBonus
                );
                saucepanStack.Attributes.SetBool(
                    Cooking.WellDoneSnapshotAttribute,
                    true
                );
            }

            // The vanilla firepit already receives the XSkills multiplier through BlockEntityFirepitPatch.
            // Electrical Progressive uses a separate saucepan duration path, so apply the combined multiplier here.
            if (!IsElectricalProgressiveMachine(world, inventory)) return;

            __result *= cooking.GetCookingTimeMultiplier(player);
        }

        private static bool IsElectricalProgressiveMachine(
            IWorldAccessor world,
            InventoryBase inventory)
        {
            Block block = world.BlockAccessor.GetBlock(inventory.Pos);
            string domain = block?.Code?.Domain;

            if (!string.IsNullOrEmpty(domain) &&
                domain.StartsWith("electricalprogressive", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(inventory.Pos);
            Type type = blockEntity?.GetType();
            if (type == null) return false;

            string fullName = type.FullName ?? string.Empty;
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

            return fullName.IndexOf("ElectricalProgressive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assemblyName.IndexOf("ElectricalProgressive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MethodBase FindTargetMethod()
        {
            if (targetMethod != null) return targetMethod;

            Type type = FindSaucepanType();
            if (type == null) return null;

            targetMethod = AccessTools.Method(
                type,
                "GetMeltingDuration",
                new[]
                {
                    typeof(IWorldAccessor),
                    typeof(ISlotProvider),
                    typeof(ItemSlot)
                }
            );

            return targetMethod;
        }

        private static Type FindSaucepanType()
        {
            if (saucepanType != null) return saucepanType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name ?? string.Empty;

                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.Name != "BlockSaucepan") continue;
                    if (!typeof(CollectibleObject).IsAssignableFrom(type)) continue;

                    string fullName = type.FullName ?? string.Empty;
                    bool culinaryAssembly =
                        assemblyName.IndexOf("Culinary", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool culinaryType =
                        fullName.IndexOf("Culinary", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!culinaryAssembly && !culinaryType) continue;

                    saucepanType = type;
                    return saucepanType;
                }
            }

            return null;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types ?? Array.Empty<Type>();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
