using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace XSkills
{
    public static class PlayerInventoryLibCompat
    {
        public static void ApplyPatch(ICoreClientAPI capi)
        {
            MethodInfo originalMethod = AccessTools.Method(typeof(GuiComposer), "Compose");
            MethodInfo prefixMethod = AccessTools.Method(typeof(PlayerInventoryLibCompat), nameof(Compose_Prefix));

            if (originalMethod != null && prefixMethod != null)
            {
                Harmony harmony = new Harmony("com.xskills.pilcompat");
                harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
            }
        }

        public static void Compose_Prefix(GuiComposer __instance)
        {
            if (__instance.DialogName != "inventory-backpack") return;

            ICoreClientAPI capi = __instance.Api as ICoreClientAPI;
            if (capi == null) return;

            // Защита от дублирования
            if (__instance.GetElement("xskillshotbar-grid-1") != null) return;

            IInventory xskillsInv = capi.World.Player.InventoryManager.GetOwnInventory("xskillshotbar");
            if (xskillsInv == null || xskillsInv.Count == 0) return;

            // Используем GetElement вместо GetSlotGrid, чтобы избежать InvalidCastException при динамической пересборке PIL
            GuiElement backpackGrid = __instance.GetElement("slotgrid");
            if (backpackGrid == null) return;

            IInventory backpackInv = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
            if (backpackInv == null) return;

            // Вычисляем, на каком слоте закончился ванильный рюкзак
            int startingIndex = 4;
            if (backpackInv.GetType().Name == "BackpackInventory")
            {
                var prop = backpackInv.GetType().GetProperty("VanillaBackpackSlotsCount");
                if (prop != null) startingIndex = (int)prop.GetValue(backpackInv);
            }

            int generalSlotsCount = 0;
            // Если рюкзака нет, цикл просто не выполнится, и count останется 0
            for (int i = startingIndex; i < backpackInv.Count; i++)
            {
                var slot = backpackInv[i];
                if (slot == null || slot.GetType().Name == "PlaceholderItemSlot") continue;

                bool isCategorized = false;
                var configProp = slot.GetType().GetProperty("BackpackSlotConfig");
                if (configProp != null)
                {
                    var config = configProp.GetValue(slot);
                    if (config?.GetType().GetField("BackpackCategory")?.GetValue(config) != null)
                    {
                        isCategorized = true;
                    }
                }
                if (!isCategorized) generalSlotsCount++;
            }

            // Математика сетки PIL (строго 6 колонок)
            int cols = 6;
            int startCol = generalSlotsCount % cols;
            int startRow = generalSlotsCount / cols;

            double slotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;
            double advance = slotSize + pad;

            Action<object> sendPacket = (packet) => capi.Network.SendPacketClient((Packet_Client)packet);

            // Заполняем остаток текущей строки
            int slotsInFirstRow = Math.Min(cols - startCol, xskillsInv.Count);
            if (slotsInFirstRow > 0)
            {
                int[] indices1 = new int[slotsInFirstRow];
                for (int i = 0; i < slotsInFirstRow; i++) indices1[i] = i;

                ElementBounds bounds1 = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, slotsInFirstRow, 1);
                bounds1.fixedX = backpackGrid.Bounds.fixedX + (startCol * advance);
                bounds1.fixedY = backpackGrid.Bounds.fixedY + (startRow * advance);
                bounds1.WithParent(backpackGrid.Bounds.ParentBounds);

                __instance.AddItemSlotGrid(xskillsInv, sendPacket, slotsInFirstRow, indices1, bounds1, "xskillshotbar-grid-1");
            }

            // Переносим оставшиеся слоты на новые строки
            int remainingSlots = xskillsInv.Count - slotsInFirstRow;
            if (remainingSlots > 0)
            {
                int[] indices2 = new int[remainingSlots];
                for (int i = 0; i < remainingSlots; i++) indices2[i] = slotsInFirstRow + i;

                int remRows = (int)Math.Ceiling(remainingSlots / (float)cols);

                ElementBounds bounds2 = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, cols, remRows);
                bounds2.fixedX = backpackGrid.Bounds.fixedX;
                bounds2.fixedY = backpackGrid.Bounds.fixedY + ((startRow + 1) * advance);
                bounds2.WithParent(backpackGrid.Bounds.ParentBounds);

                __instance.AddItemSlotGrid(xskillsInv, sendPacket, cols, indices2, bounds2, "xskillshotbar-grid-2");
            }
        }
    }
}