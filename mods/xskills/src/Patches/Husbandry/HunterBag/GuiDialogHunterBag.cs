using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using XSkills;

namespace xskills.src.Patches.Husbandry.HunterBag
{
    /// <summary>Сохраняемая позиция слота (в немасштабированных GUI-единицах)</summary>
    public class HunterBagSlotLayout
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool HasValue { get; set; }
    }

    public class GuiDialogHunterBag : GuiDialog
    {
        private const string LayoutConfigFile = "xskills/hunterbagslotlayout.json";

        private readonly IInventory inventory;
        private int composedSlotCount;

        // Свёрнут ли HUD: показываем только слот(ы) сумки, слоты содержимого прячем до наведения
        private bool expanded;

        // Позиция левого-верхнего угла слота в немасштабированных единицах (как fixedX/fixedY у ElementBounds)
        private double posX;
        private double posY;

        private ElementBounds dialogBounds;
        private HunterBagSlotLayout layout;

        // Токен регистрации в F6-редакторе
        private object editToken;

        public override string ToggleKeyCombinationCode => null;
        public override EnumDialogType DialogType => EnumDialogType.HUD;
        public override bool PrefersUngrabbedMouse => false;

        // HUD должен получать события мыши, чтобы работали наведение (разворот) и клики по слоту
        public override bool ShouldReceiveMouseEvents() => IsOpened();

        public GuiDialogHunterBag(ICoreClientAPI capi, IInventory inventory) : base(capi)
        {
            this.inventory = inventory;

            // Когда сумку кладут/забирают, число слотов меняется - пересобираем HUD
            // Событие приходит из OnItemSlotModified (вне рендера), поэтому пересборка безопасна
            if (inventory is HunterBagInventory hbi) hbi.SlotCountChanged += OnInventorySlotCountChanged;

            LoadLayout();
            ComposeDialog();
        }

        private void LoadLayout()
        {
            try { layout = capi.LoadModConfig<HunterBagSlotLayout>(LayoutConfigFile); }
            catch { layout = null; }

            if (layout != null && layout.HasValue)
            {
                posX = layout.X;
                posY = layout.Y;
            }
            else
            {
                // По умолчанию - правый нижний угол (в немасштабированных единицах)
                double scale = RuntimeEnv.GUIScale;
                if (scale <= 0) scale = 1.0;
                posX = Math.Max(0.0, capi.Render.FrameWidth / scale - 100.0);
                posY = Math.Max(0.0, capi.Render.FrameHeight / scale - 120.0);
                layout = new HunterBagSlotLayout { X = posX, Y = posY, HasValue = false };
            }
        }

        private void ComposeDialog()
        {
            int total = inventory?.Count ?? 0;

            // SingleComposer = null нелегален в этом движке (сеттер разыменовывает value),
            // поэтому при отсутствии слотов просто не собираем композицию
            if (inventory == null || total == 0) { composedSlotCount = 0; return; }

            int bagCount = BagSlotCount();

            // Свёрнуто рисуем только слот(ы) сумки; развёрнуто (при наведении) - все слоты
            composedSlotCount = (expanded && total > bagCount) ? total : bagCount;
            if (composedSlotCount < 1) composedSlotCount = 1;
            if (composedSlotCount > total) composedSlotCount = total;

            int count = composedSlotCount;
            int contentCount = Math.Max(0, count - bagCount);

            double scale = RuntimeEnv.GUIScale;
            if (scale <= 0) scale = 1.0;
            double screenWUnscaled = capi.Render.FrameWidth / scale;

            // По умолчанию рост вправо: сетка с переносом до 8 в ряд, слоты сумки идут первыми
            int cols = Math.Min(count, 8);
            int rows = (count + cols - 1) / cols;
            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, cols, rows);

            int[] slotIndices = new int[count];
            for (int i = 0; i < count; i++) slotIndices[i] = i;

            double anchorX = posX;

            // Если развёрнуто и вправо сетка не помещается на экране - растём ВЛЕВО одной строкой, оставляя слот сумки на прежнем месте (он становится крайним справа), чтобы он не уезжал из-под курсора
            bool growLeft = expanded && contentCount > 0 && (posX + slotBounds.fixedWidth > screenWUnscaled);
            if (growLeft)
            {
                cols = count;
                rows = 1;
                slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, cols, rows);

                double cellW = slotBounds.fixedWidth / count;
                anchorX = Math.Max(0.0, posX - contentCount * cellW);

                // Порядок: сперва слоты содержимого, затем слот(ы) сумки - так сумка оказывается справа
                int k = 0;
                for (int i = bagCount; i < count; i++) slotIndices[k++] = i;
                for (int i = 0; i < bagCount; i++) slotIndices[k++] = i;
            }

            // Контейнер без фона (без рамки), только для расчёта размера
            ElementBounds childBounds = ElementBounds.Fill.WithFixedPadding(0);
            childBounds.BothSizing = ElementSizing.FitToChildren;
            childBounds.WithChildren(slotBounds);

            // Позиция левого-верхнего угла (при росте влево якорь сдвинут так, чтобы сумка осталась на месте)
            dialogBounds = ElementBounds.Fixed(anchorX, posY, 0, 0);
            dialogBounds.BothSizing = ElementSizing.FitToChildren;
            dialogBounds.WithChildren(childBounds);

            // Освобождаем прежний композер перед пересборкой (сеттер SingleComposer его не диспозит)
            SingleComposer?.Dispose();

            GuiComposer composer = capi.Gui.CreateCompo("hunterbaggui", dialogBounds)
                .BeginChildElements(childBounds);

            // Свой грид с отключённым тултипом (эквивалент .AddItemSlotGrid, но без всплывающей подсказки)
            composer.AddInteractiveElement(
                new GuiElementHunterBagSlotGrid(capi, inventory, SendInvPacket, cols, slotIndices, slotBounds),
                "hunterbagslot");

            SingleComposer = composer
                .EndChildElements()
                .Compose();
        }

        // Интеграция с F6-редактором PandaXPDrops

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            if (editToken == null)
            {
                editToken = HunterBagLayoutBridge.Register(
                    Lang.Get("xskills:ability-hunterbagperk"),
                    GetScreenRect,      // Func<double[]> -> [x,y,w,h] в реальных пикселях
                    SetTopLeft,         // Action<double,double> -> экранные пиксели
                    null,               // масштаб не поддерживаем
                    CommitLayout);      // сохранение при закрытии редактора
            }
        }

        public override void OnGuiClosed()
        {
            HunterBagLayoutBridge.Unregister(editToken);
            editToken = null;
            base.OnGuiClosed();
        }

        public override void Dispose()
        {
            if (inventory is HunterBagInventory hbi) hbi.SlotCountChanged -= OnInventorySlotCountChanged;
            HunterBagLayoutBridge.Unregister(editToken);
            editToken = null;
            base.Dispose();
        }

        // Сумку положили/забрали - число слотов изменилось - пересобираем HUD
        private void OnInventorySlotCountChanged()
        {
            // Нет содержимого - принудительно свернуть
            int total = inventory?.Count ?? 0;
            if (total <= BagSlotCount()) expanded = false;
            ComposeDialog();
        }

        // Сколько слотов занимает сама сумка (остальное - её содержимое).
        private int BagSlotCount()
        {
            if (inventory is HunterBagInventory hbi) return Math.Max(1, hbi.BagSlotCount);
            return inventory?.Count ?? 1;
        }

        /// <summary>Экранный прямоугольник слота в реальных пикселях: [x, y, w, h], либо null</summary>
        private double[] GetScreenRect()
        {
            var b = SingleComposer?.Bounds;
            if (b == null) return null;

            // renderX/renderY/OuterWidth/OuterHeight - уже в масштабированных экранных пикселях
            // Если на твоей версии API имена иные, здесь единственное место для правки
            return new double[] { b.renderX, b.renderY, b.OuterWidth, b.OuterHeight };
        }

        /// <summary>Ставит слот левым-верхним углом в экранную точку (реальные пиксели) и пересобирает</summary>
        private void SetTopLeft(double screenX, double screenY)
        {
            double scale = RuntimeEnv.GUIScale;
            if (scale <= 0) scale = 1.0;

            posX = screenX / scale;
            posY = screenY / scale;

            // Пересборка вне стадии рендера (вызывается из OnMouseMove редактора)
            ComposeDialog();
        }

        /// <summary>Сохраняет текущую позицию на диск (вызывается при закрытии редактора)</summary>
        private void CommitLayout()
        {
            try
            {
                layout ??= new HunterBagSlotLayout();
                layout.X = posX;
                layout.Y = posY;
                layout.HasValue = true;
                capi.StoreModConfig(layout, LayoutConfigFile);
            }
            catch { }
        }

        // Рендер (без пересборки, не трогаем SingleComposer)

        private bool CanRenderThisFrame()
        {
            return SingleComposer != null
                && composedSlotCount > 0
                && inventory != null
                && inventory.Count >= composedSlotCount;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (!CanRenderThisFrame()) return;
            base.OnRenderGUI(deltaTime);
        }

        public override void OnFinalizeFrame(float dt)
        {
            if (!CanRenderThisFrame()) return;
            base.OnFinalizeFrame(dt);
        }

        // Пока идёт редактирование F6, не перехватываем мышь сами
        // Иначе клик по слоту уйдёт в перетаскивание предметов, а не в редактор

        public override void OnMouseDown(MouseEvent args)
        {
            if (HunterBagLayoutBridge.IsEditing) return;
            base.OnMouseDown(args);
        }

        public override void OnMouseMove(MouseEvent args)
        {
            if (HunterBagLayoutBridge.IsEditing) return;

            UpdateHoverExpansion(args.X, args.Y);
            base.OnMouseMove(args);
        }

        // Разворачиваем HUD только пока курсор над ним; иначе сворачиваем до слота(ов) сумки
        private void UpdateHoverExpansion(double mouseX, double mouseY)
        {
            bool hasContent = (inventory?.Count ?? 0) > BagSlotCount();

            bool over = false;
            double[] r = GetScreenRect();
            if (r != null)
                over = mouseX >= r[0] && mouseX <= r[0] + r[2] && mouseY >= r[1] && mouseY <= r[1] + r[3];

            bool want = hasContent && over;
            if (want != expanded)
            {
                expanded = want;
                ComposeDialog();
            }
        }

        public override void OnMouseUp(MouseEvent args)
        {
            if (HunterBagLayoutBridge.IsEditing) return;
            base.OnMouseUp(args);
        }

        private void SendInvPacket(object packet)
        {
            capi.Network.SendPacketClient(packet);
        }
    }
}