using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using XSkills;

namespace XSkills
{
    /// <summary>Сохраняемая позиция слота (в немасштабированных GUI-единицах)</summary>
    public class HunterBagSlotLayout
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Scale { get; set; } = 1.0;          
        public bool Enabled { get; set; } = true;        
        public bool HideWhenInvClosed { get; set; } = false;
        public bool AlwaysExpanded { get; set; } = false;
        public bool HasValue { get; set; }
        public int Version { get; set; } = 0;
    }

    public class GuiDialogHunterBag : GuiDialog
    {
        private const string LayoutConfigFile = "XLeveling/gui/hunterbagslotlayout.json";

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

        private double lastGuiScale = -1;
        private int lastFrameWidth = -1;
        private int lastFrameHeight = -1;

        public override string ToggleKeyCombinationCode => null;
        public override EnumDialogType DialogType => EnumDialogType.HUD;
        public override bool PrefersUngrabbedMouse => false;

        /// <summary>
        /// Определяет, должен ли HUD получать события мыши.
        /// Блокируем клики, если слот сейчас визуально скрыт настройками, чтобы невидимый слот не перехватывал курсор.
        /// </summary>
        /// <returns><c>true</c>, если окно открыто и должно обрабатывать клики.</returns>
        public override bool ShouldReceiveMouseEvents() => IsOpened() && CanRenderThisFrame();

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
                // Если версия конфига меньше 1 (то есть это старый файл сохранения)
                if (layout.Version < 1)
                {
                    // Жестко сбрасываем на новые идеальные координаты
                    posX = 490.33;
                    posY = -62.56;

                    // Обновляем конфиг в памяти и перезаписываем файл игроку
                    layout.X = posX;
                    layout.Y = posY;
                    layout.Version = 1;
                    capi.StoreModConfig(layout, LayoutConfigFile);
                }
                else
                {
                    // Если версия 1 и выше, значит игрок уже использует новую систему центрирования
                    posX = layout.X;
                    posY = layout.Y;
                }
            }
            else
            {
                // Для новых игроков (или если файл конфига был удален)
                posX = 490.33;
                posY = -62.56;
                layout = new HunterBagSlotLayout { X = posX, Y = posY, HasValue = false, Version = 1 };
            }
        }

        private void ComposeDialog()
        {
            int total = inventory?.Count ?? 0;
            if (inventory == null || total == 0) { composedSlotCount = 0; return; }

            int bagCount = BagSlotCount();
            bool isExpanded = expanded || (layout != null && layout.AlwaysExpanded);

            composedSlotCount = (isExpanded && total > bagCount) ? total : bagCount;
            if (composedSlotCount < 1) composedSlotCount = 1;
            if (composedSlotCount > total) composedSlotCount = total;

            int count = composedSlotCount;
            int contentCount = Math.Max(0, count - bagCount);

            double scale = RuntimeEnv.GUIScale <= 0 ? 1.0 : RuntimeEnv.GUIScale;
            double screenWUnscaled = capi.Render.FrameWidth / scale;
            double screenHUnscaled = capi.Render.FrameHeight / scale;

            double absX = (screenWUnscaled / 2.0) + posX;
            double absY = screenHUnscaled + posY;

            int cols = Math.Min(count, 8);
            int rows = (count + cols - 1) / cols;
            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, cols, rows);

            // Защита от ухода за края экрана (на всякий случай)
            absX = Math.Max(0.0, Math.Min(absX, screenWUnscaled - slotBounds.fixedWidth));
            absY = Math.Max(0.0, Math.Min(absY, screenHUnscaled - slotBounds.fixedHeight));

            int[] slotIndices = new int[count];
            for (int i = 0; i < count; i++) slotIndices[i] = i;

            double anchorX = absX;

            bool growLeft = isExpanded && contentCount > 0 && (absX + slotBounds.fixedWidth > screenWUnscaled);
            if (growLeft)
            {
                cols = count;
                rows = 1;
                slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 0, cols, rows);

                double cellW = slotBounds.fixedWidth / count;
                anchorX = Math.Max(0.0, absX - contentCount * cellW);

                int k = 0;
                for (int i = bagCount; i < count; i++) slotIndices[k++] = i;
                for (int i = 0; i < bagCount; i++) slotIndices[k++] = i;
            }

            ElementBounds childBounds = ElementBounds.Fill.WithFixedPadding(0);
            childBounds.BothSizing = ElementSizing.FitToChildren;
            childBounds.WithChildren(slotBounds);

            dialogBounds = ElementBounds.Fixed(anchorX, absY, 0, 0);
            dialogBounds.BothSizing = ElementSizing.FitToChildren;
            dialogBounds.WithChildren(childBounds);

            SingleComposer?.Dispose();

            GuiComposer composer = capi.Gui.CreateCompo("hunterbaggui", dialogBounds)
                .BeginChildElements(childBounds);

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
                    Lang.Get("xskills:ability-hunterbag"),
                    GetScreenRect,
                    SetTopLeft,
                    null,               // Передаем null, так как кастомный масштаб для слотов ломает клики
                    CommitLayout);
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
            double scale = RuntimeEnv.GUIScale <= 0 ? 1.0 : RuntimeEnv.GUIScale;
            double screenW = capi.Render.FrameWidth / scale;
            double screenH = capi.Render.FrameHeight / scale;

            posX = (screenX / scale) - (screenW / 2.0);
            posY = (screenY / scale) - screenH;

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
                layout.Version = 1;
                capi.StoreModConfig(layout, LayoutConfigFile);
            }
            catch { }
        }

        /// <summary>
        /// Проверяет, должен ли HUD отрисовываться в текущем кадре.
        /// Учитывает настройки отключения и автоматического скрытия.
        /// </summary>
        /// <returns><c>true</c>, если HUD нужно нарисовать, иначе <c>false</c>.</returns>
        private bool CanRenderThisFrame()
        {
            // 1. Полное отключение слота (если убрали галочку Enabled)
            if (layout == null || !layout.Enabled) return false;

            // 2. Скрытие, когда закрыт инвентарь (если стоит галочка HideWhenInvClosed)
            if (layout.HideWhenInvClosed)
            {
                // Надежный способ: ищем любое открытое окно меню (инвентарь, сундук, крафт).
                // HUD-интерфейсы имеют тип HUD, а обычные диалоговые окна (инвентари) - Dialog.
                bool isAnyMenuOpen = capi.Gui.OpenedGuis.Exists(dlg => dlg.DialogType == EnumDialogType.Dialog);
                if (!isAnyMenuOpen) return false;
            }

            // 3. Стандартная проверка наличия слотов
            return SingleComposer != null
                && composedSlotCount > 0
                && inventory != null
                && inventory.Count >= composedSlotCount;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (!CanRenderThisFrame()) return;

            if (lastGuiScale != RuntimeEnv.GUIScale || lastFrameWidth != capi.Render.FrameWidth || lastFrameHeight != capi.Render.FrameHeight)
            {
                lastGuiScale = RuntimeEnv.GUIScale;
                lastFrameWidth = capi.Render.FrameWidth;
                lastFrameHeight = capi.Render.FrameHeight;
                ComposeDialog();
            }

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
        private void SetScale(float newScale)
        {
            // Ограничиваем масштаб, чтобы слот не стал слишком мелким или огромным
            layout.Scale = Math.Max(0.5, Math.Min((double)newScale, 2.0));
            ComposeDialog(); // Пересобираем UI с новым размером
        }
        public void ReloadSettings()
        {
            LoadLayout();
            ComposeDialog();
        }
    }
}