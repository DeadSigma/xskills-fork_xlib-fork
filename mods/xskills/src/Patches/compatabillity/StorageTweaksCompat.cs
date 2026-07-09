using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace XSkills
{
    public static class StorageTweaksCompat
    {
        public static ICoreAPI Api;

        [ThreadStatic]
        private static bool isSortingExtra;

        public static void ApplyPatch(ICoreAPI api)
        {
            Api = api;
            Type sortSystemType = null;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType("StorageTweaks.SortSystem", false);
                    if (t != null)
                    {
                        sortSystemType = t;
                        break;
                    }
                }
                catch { }
            }

            if (sortSystemType != null)
            {
                MethodInfo original = AccessTools.Method(sortSystemType, "SortInventoryInternal");
                // Используем новые имена, чтобы PatchAll() их игнорировал
                MethodInfo transpiler = AccessTools.Method(typeof(StorageTweaksCompat), nameof(SortTranspiler));
                MethodInfo prefix = AccessTools.Method(typeof(StorageTweaksCompat), nameof(SortPrefix));
                MethodInfo postfix = AccessTools.Method(typeof(StorageTweaksCompat), nameof(SortPostfix));

                if (original != null)
                {
                    try
                    {
                        Harmony harmony = new Harmony("com.xskills.storagetweaks");

                        harmony.Unpatch(original, HarmonyPatchType.Transpiler, "com.xskills.storagetweaks");
                        harmony.Unpatch(original, HarmonyPatchType.Prefix, "com.xskills.storagetweaks");
                        harmony.Unpatch(original, HarmonyPatchType.Postfix, "com.xskills.storagetweaks");

                        harmony.Patch(original,
                            prefix: new HarmonyMethod(prefix),
                            postfix: new HarmonyMethod(postfix),
                            transpiler: new HarmonyMethod(transpiler));

                    }
                    catch (Exception ex)
                    {
                        api.Logger.Error($"XSkills: Ошибка при патчинге StorageTweaks: {ex}");
                    }
                }
            }
        }

        public static IEnumerable<CodeInstruction> SortTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo proxyBestSuitedMethod = AccessTools.Method(typeof(StorageTweaksCompat), nameof(GetBestSuitedSlotProxy));

            foreach (var code in instructions)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand is MethodInfo method && method.Name == "GetBestSuitedSlot")
                {
                    yield return new CodeInstruction(OpCodes.Call, proxyBestSuitedMethod);
                    continue;
                }
                yield return code;
            }
        }

        public static WeightedSlot GetBestSuitedSlotProxy(IInventory inv, ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots)
        {
            WeightedSlot weightedSlot = inv.GetBestSuitedSlot(sourceSlot, op, skipSlots);

            if (weightedSlot.slot == null && inv.ClassName == GlobalConstants.backpackInvClassName)
            {
                IPlayer player = GetPlayerFromInventory(inv);
                if (player != null && player.InventoryManager.GetOwnInventory("xskillshotbar") is XSkillsPlayerInventory xInv && !xInv.IsFixed)
                {
                    weightedSlot = xInv.GetBestSuitedSlot(sourceSlot, op, skipSlots);
                }
            }
            return weightedSlot;
        }

        private static IPlayer GetPlayerFromInventory(IInventory targetInv)
        {
            if (targetInv == null || Api?.World == null) return null;
            foreach (IPlayer player in Api.World.AllPlayers)
            {
                if (player?.InventoryManager != null && player.InventoryManager.GetInventory(targetInv.InventoryID) == targetInv)
                    return player;
            }
            return null;
        }

        private static IInventory GetInventoryFromArgs(object[] args, IServerPlayer player)
        {
            IInventory inv = args.OfType<IInventory>().FirstOrDefault();
            if (inv != null) return inv;

            if (player != null)
            {
                string id = args.OfType<string>().FirstOrDefault();
                if (id != null) return player.InventoryManager.GetInventory(id);
            }
            return null;
        }

        public static void SortPrefix(object[] __args)
        {
            if (isSortingExtra) return;

            try
            {
                IServerPlayer player = __args.OfType<IServerPlayer>().FirstOrDefault();
                IInventory inventory = GetInventoryFromArgs(__args, player);

                if (player != null && inventory != null && inventory.ClassName == GlobalConstants.backpackInvClassName)
                {
                    if (player.InventoryManager.GetOwnInventory("xskillshotbar") is XSkillsPlayerInventory xInv && !xInv.IsFixed)
                    {
                        ConsolidateInventories(xInv, inventory);
                    }
                }
            }
            catch (Exception ex)
            {
                Api?.Logger.Error($"[XSkills-StorageTweaks] Ошибка в SortPrefix: {ex}");
            }
        }

        public static void SortPostfix(MethodBase __originalMethod, object[] __args)
        {
            if (isSortingExtra) return;

            try
            {
                IServerPlayer player = __args.OfType<IServerPlayer>().FirstOrDefault();
                IInventory inventory = GetInventoryFromArgs(__args, player);

                if (player != null && inventory != null && inventory.ClassName == GlobalConstants.backpackInvClassName)
                {
                    if (player.InventoryManager.GetOwnInventory("xskillshotbar") is XSkillsPlayerInventory xInv && !xInv.IsFixed)
                    {
                        isSortingExtra = true;
                        try
                        {
                            object[] newArgs = new object[__args.Length];
                            for (int i = 0; i < __args.Length; i++)
                            {
                                if (__args[i] == inventory)
                                    newArgs[i] = xInv;
                                else if (__args[i] is string str && str == inventory.InventoryID)
                                    newArgs[i] = xInv.InventoryID;
                                else
                                    newArgs[i] = __args[i];
                            }

                            __originalMethod.Invoke(null, newArgs);
                        }
                        finally
                        {
                            isSortingExtra = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Api?.Logger.Error($"[XSkills-StorageTweaks] Ошибка в SortPostfix: {ex}");
            }
        }

        private static void ConsolidateInventories(IInventory fromInv, IInventory toInv)
        {
            if (Api?.World == null) return;
            int totalMoved = 0;

            // Сначала объединяем неполные стаки
            foreach (var fromSlot in fromInv)
            {
                if (fromSlot.Empty) continue;
                foreach (var toSlot in toInv)
                {
                    if (toSlot.Empty) continue;
                    if (toSlot.Itemstack.Equals(Api.World, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                    {
                        int moved = fromSlot.TryPutInto(Api.World, toSlot, fromSlot.StackSize);
                        if (moved > 0)
                        {
                            totalMoved += moved;
                            Api.Logger.Debug($"[XSkills-StorageTweaks] Слияние стаков: перемещено {moved}x {toSlot.Itemstack.GetName()} в рюкзак.");
                        }
                        if (fromSlot.Empty) break;
                    }
                }
            }

            // Оставшиеся предметы закидываем в пустые слоты
            foreach (var fromSlot in fromInv)
            {
                if (fromSlot.Empty) continue;
                foreach (var toSlot in toInv)
                {
                    if (toSlot.Empty)
                    {
                        int moved = fromSlot.TryPutInto(Api.World, toSlot, fromSlot.StackSize);
                        if (moved > 0)
                        {
                            totalMoved += moved;
                        }
                        if (fromSlot.Empty) break;
                    }
                }
            }

            if (totalMoved > 0)
            {
                foreach (var slot in fromInv) slot.MarkDirty();
                foreach (var slot in toInv) slot.MarkDirty();
            }
        }
    }
}