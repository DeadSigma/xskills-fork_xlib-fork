using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace XSkills
{
    /// <summary>
    /// Слот-грид для HUD сумки охотника, у которого ОТКЛЮЧЁН всплывающий тултип предмета
    /// (большой info-box перекрывал слоты содержимого при наведении на сумку).
    ///
    /// Тултип рисует движок по слоту, о котором базовый грид сообщает вызовом
    /// api.Input.TriggerOnMouseEnterSlot(...) внутри OnMouseMove. Готового флага для отключения
    /// нет, поэтому переопределяем OnMouseMove и повторяем только отслеживание наведения
    /// (для подсветки слота), НЕ вызывая TriggerOnMouseEnterSlot - тогда подсказка не появляется.
    ///
    /// Побочный эффект: не работает «раздача перетаскиванием» (drag-distribute) - она в базовом
    /// гриде завязана на приватные поля и здесь не воспроизводится. Для маленького HUD это
    /// некритично; обычные клики (положить/забрать) работают через OnMouseDown как обычно.
    /// </summary>
    public class GuiElementHunterBagSlotGrid : GuiElementItemSlotGrid
    {
        public GuiElementHunterBagSlotGrid(ICoreClientAPI capi, IInventory inventory, Action<object> sendPacket, int cols, int[] visibleSlots, ElementBounds bounds)
            : base(capi, inventory, sendPacket, cols, visibleSlots, bounds)
        {
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            // Курсор вне грида - сбрасываем наведение и выходим (без уведомления системы подсказок).
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y))
            {
                hoverSlotId = -1;
                return;
            }

            for (int i = 0; i < SlotBounds.Length && i < renderedSlots.Count; i++)
            {
                if (!SlotBounds[i].PointInside(args.X, args.Y)) continue;

                int nowHoverSlotid = renderedSlots.GetKeyAtIndex(i);
                ItemSlot nowHoverSlot = inventory[nowHoverSlotid];

                if (nowHoverSlotid != hoverSlotId && nowHoverSlot != null)
                {
                    hoverInv = nowHoverSlot.Inventory;
                    // НАМЕРЕННО не вызываем api.Input.TriggerOnMouseEnterSlot(nowHoverSlot):
                    // именно это подавляет большой тултип предмета.
                }
                if (nowHoverSlotid != hoverSlotId) tabbedSlotId = -1;

                hoverSlotId = nowHoverSlotid;
                return;
            }

            hoverSlotId = -1;
        }
    }
}
