using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PandaXPDrops
{
    /// <summary>Элемент HUD, который можно перемещать и изменять в размере в режиме редактирования</summary>
    public enum EnumXpDropsElement
    {
        /// <summary>Ничего под курсором</summary>
        None,

        /// <summary>Полоса навыка</summary>
        Bar,

        /// <summary>Область появления плавающих меток</summary>
        Drops
    }


    /// <summary>
    /// Редактор макета для HUD выпадения опыта: рисует рамку вокруг каждого элемента, перетаскивает их левой кнопкой мыши и изменяет размер колесиком мыши. Записывает конфигурацию при закрытии
    /// </summary>
    /// <remarks>
    /// Это обычный <see cref="GuiDialog"/>, намеренно не являющийся частью <see cref="XpDropsHud"/>:
    /// <see cref="HudElement"/> не может освободить курсор мыши, открытый диалог делает это автоматически
    /// Пока он открыт, менеджер работает в режиме <see cref="XpDropManager.EditPreview"/>, иначе нечего было бы
    /// перетаскивать - элементы видны только сразу после получения опыта
    /// Открытие выполняется обработчиком горячих клавиш системы модов, закрытие дополнительно по ESC и по
    /// <see cref="ToggleKeyCombinationCode"/>
    /// </remarks>
    public class XpDropsEditDialog : GuiDialog
    {
        /// <summary>Код горячей клавиши, зарегистрированный системой модов</summary>
        public const string HotkeyCode = "xpdropsedit";

        /// <summary>Изменение размера за один щелчок колесика</summary>
        private const float ScaleStep = 0.05f;

        /// <summary>Глубина отрисовки рамок - над полосой (90) и метками (91)</summary>
        private const float FrameZ = 95f;

        private readonly XpDropManager manager;
        private readonly Action onSave;

        private LoadedTexture whiteTexture;
        private EnumXpDropsElement dragging = EnumXpDropsElement.None;
        private double dragOffsetX;
        private double dragOffsetY;

        /// <summary>Создает диалог</summary>
        /// <param name="capi">Клиентское API</param>
        /// <param name="manager">Менеджер, владеющий макетом</param>
        /// <param name="onSave">Вызывается при закрытии диалога, записывает конфигурацию</param>
        public XpDropsEditDialog(ICoreClientAPI capi, XpDropManager manager, Action onSave) : base(capi)
        {
            this.manager = manager;
            this.onSave = onSave;
        }

        /// <summary>Позволяет диалогу закрыться самому при повторном нажатии горячей клавиши редактирования</summary>
        public override string ToggleKeyCombinationCode => HotkeyCode;

        /// <summary>Отрисовывается поверх HUD (0.95)</summary>
        public override double DrawOrder => 0.96;

        /// <summary>Запрашивает события мыши только пока открыт</summary>
        /// <returns><c>true</c> пока открыт</returns>
        public override bool ShouldReceiveMouseEvents() => IsOpened();

        /// <summary>Входит в режим предпросмотра, чтобы элементы были видны при позиционировании</summary>
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            if (SingleComposer == null)
            {
                ElementBounds rootBounds = ElementBounds.Fixed(10, 10, 200, 40).WithAlignment(EnumDialogArea.LeftTop);
                ElementBounds buttonBounds = ElementBounds.Fixed(0, 0, 160, 30);

                rootBounds.WithChild(buttonBounds);

                SingleComposer = capi.Gui
                    .CreateCompo("pandaxpdrops-edit", rootBounds)
                    .AddButton(XpDropsLang.Get("settings-btn-open"), OnSettingsClicked, buttonBounds)
                    .Compose();
            }

            manager.EditPreview = true;
            capi.ShowChatMessage(XpDropsLang.Get("editmode-hint", HotkeyName(HotkeyCode)));
        }

        // Переменная для хранения ссылки на открытое окно настроек
        private XpDropsSettingsDialog settingsDialog;

        /// <summary>Открывает меню настроек мода, предотвращая появление дубликатов</summary>
        /// <returns>Возвращает true, подтверждая перехват клика</returns>
        private bool OnSettingsClicked()
        {
            if (settingsDialog != null && settingsDialog.IsOpened())
            {
                return true;
            }

            settingsDialog = new XpDropsSettingsDialog(capi, manager.Config, onSave);
            settingsDialog.TryOpen();
            return true;
        }

        /// <summary>Выходит из режима предпросмотра и сохраняет макет</summary>
        public override void OnGuiClosed()
        {
            dragging = EnumXpDropsElement.None;
            manager.EditPreview = false;
            onSave?.Invoke();
            capi.ShowChatMessage(XpDropsLang.Get("editmode-saved"));

            base.OnGuiClosed();
        }

        /// <summary>Рисует рамки вокруг каждого элемента и сам пользовательский интерфейс</summary>
        /// <param name="deltaTime">Дельта кадра в секундах</param>
        public override void OnRenderGUI(float deltaTime)
        {
            // Вызов базового метода отрисовывает элементы интерфейса
            base.OnRenderGUI(deltaTime);

            EnsureWhiteTexture();

            EnumXpDropsElement hovered = dragging != EnumXpDropsElement.None
                ? dragging
                : HitTest(capi.Input.MouseX, capi.Input.MouseY);

            DrawFrame(manager.GetBarRect(), 0.35, 0.85, 0.35, hovered == EnumXpDropsElement.Bar);
            DrawFrame(manager.GetDropSpawnRect(), 1.0, 0.6, 0.15, hovered == EnumXpDropsElement.Drops);
        }

        /// <summary>Начинает перетаскивание при захвате элемента</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseDown(MouseEvent args)
        {
            EnumXpDropsElement hit = HitTest(args.X, args.Y);
            if (hit == EnumXpDropsElement.None)
            {
                base.OnMouseDown(args);
                return;
            }

            GuiRect rect = GetRect(hit);
            dragging = hit;
            dragOffsetX = args.X - rect.X;
            dragOffsetY = args.Y - rect.Y;
            args.Handled = true;
        }

        /// <summary>Останавливает перетаскивание</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseUp(MouseEvent args)
        {
            if (dragging != EnumXpDropsElement.None)
            {
                dragging = EnumXpDropsElement.None;
                args.Handled = true;
                return;
            }

            base.OnMouseUp(args);
        }

        /// <summary>Преобразует перетаскиваемую позицию на экране обратно в значения конфигурации</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseMove(MouseEvent args)
        {
            if (dragging == EnumXpDropsElement.None)
            {
                base.OnMouseMove(args);
                return;
            }

            XpDropConfig config = manager.Config;
            double scale = RuntimeEnv.GUIScale;
            double frameW = capi.Render.FrameWidth;
            double frameH = capi.Render.FrameHeight;

            GuiRect rect = GetRect(dragging);
            double x = Math.Clamp(args.X - dragOffsetX, 0.0, Math.Max(0.0, frameW - rect.W));
            double y = Math.Clamp(args.Y - dragOffsetY, 0.0, Math.Max(0.0, frameH - rect.H));

            if (dragging == EnumXpDropsElement.Bar)
            {
                // полоса привязана к своему правому краю
                config.BarRightMargin = (float)((frameW - (x + rect.W)) / scale);
                config.BarTopMargin = (float)(y / scale);
            }
            else
            {
                // метки привязаны относительно полосы, поэтому они следуют за ней при перемещении полосы
                GuiRect bar = manager.GetBarRect();
                config.TextSpawnOffsetX = (float)((x + rect.W / 2.0 - bar.CenterX) / scale);
                config.TextSpawnBelowBar = (float)((y - bar.Y) / scale);
            }

            args.Handled = true;
        }

        /// <summary>Изменяет размер элемента под курсором</summary>
        /// <param name="args">Событие колесика мыши</param>
        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            EnumXpDropsElement hit = HitTest(capi.Input.MouseX, capi.Input.MouseY);
            if (hit == EnumXpDropsElement.None)
            {
                base.OnMouseWheel(args);
                return;
            }

            float step = args.delta > 0 ? ScaleStep : -ScaleStep;
            XpDropConfig config = manager.Config;

            if (hit == EnumXpDropsElement.Bar) config.BarScale = ClampScale(config.BarScale + step);
            else config.DropScale = ClampScale(config.DropScale + step);

            manager.InvalidateTextures();
            args.SetHandled();
        }

        /// <summary>Освобождает текстуру рамки</summary>
        public override void Dispose()
        {
            whiteTexture?.Dispose();
            whiteTexture = null;
            base.Dispose();
        }

        /// <summary>Текущая назначенная клавиша для горячей клавиши, для использования в сообщениях</summary>
        /// <param name="code">Код горячей клавиши</param>
        /// <returns>Комбинация клавиш в виде текста или сам код, если он не зарегистрирован</returns>
        private string HotkeyName(string code)
            => capi.Input.GetHotKeyByCode(code)?.CurrentMapping?.ToString() ?? code;

        /// <summary>Округляет до целых шагов и удерживает значение в настроенных пределах</summary>
        /// <param name="value">Запрашиваемый масштаб</param>
        /// <returns>Ограниченный масштаб</returns>
        private static float ClampScale(float value)
            => (float)Math.Clamp(Math.Round(value, 2), XpDropConfig.MinElementScale, XpDropConfig.MaxElementScale);

        /// <summary>Текущий экранный прямоугольник элемента</summary>
        /// <param name="element">Запрашиваемый элемент</param>
        /// <returns>Прямоугольник; пустой для <see cref="EnumXpDropsElement.None"/></returns>
        private GuiRect GetRect(EnumXpDropsElement element)
        {
            if (element == EnumXpDropsElement.Bar) return manager.GetBarRect();
            if (element == EnumXpDropsElement.Drops) return manager.GetDropSpawnRect();

            return new GuiRect(0, 0, 0, 0);
        }

        /// <summary>Элемент под экранной позицией. Меньшая область меток имеет приоритет над полосой</summary>
        /// <param name="x">Координата X экрана</param>
        /// <param name="y">Координата Y экрана</param>
        /// <returns>Элемент под курсором или <see cref="EnumXpDropsElement.None"/></returns>
        private EnumXpDropsElement HitTest(double x, double y)
        {
            if (manager.GetDropSpawnRect().Contains(x, y)) return EnumXpDropsElement.Drops;
            if (manager.GetBarRect().Contains(x, y)) return EnumXpDropsElement.Bar;

            return EnumXpDropsElement.None;
        }

        /// <summary>Полупрозрачная заливка плюс четырехсторонняя граница</summary>
        /// <param name="rect">Прямоугольник для обводки</param>
        /// <param name="r">Красный 0..1</param>
        /// <param name="g">Зеленый 0..1</param>
        /// <param name="b">Синий 0..1</param>
        /// <param name="hovered">Наведен ли курсор на элемент или перетаскивается ли он</param>
        private void DrawFrame(GuiRect rect, double r, double g, double b, bool hovered)
        {
            if (rect.W < 1.0 || rect.H < 1.0) return;

            double t = Math.Max(2.0, 2.0 * RuntimeEnv.GUIScale);
            double fillAlpha = hovered ? 0.22 : 0.08;
            double edgeAlpha = hovered ? 1.0 : 0.65;

            FillRect(rect.X, rect.Y, rect.W, rect.H, r, g, b, fillAlpha);
            FillRect(rect.X, rect.Y, rect.W, t, r, g, b, edgeAlpha);
            FillRect(rect.X, rect.Y + rect.H - t, rect.W, t, r, g, b, edgeAlpha);
            FillRect(rect.X, rect.Y + t, t, rect.H - 2.0 * t, r, g, b, edgeAlpha);
            FillRect(rect.X + rect.W - t, rect.Y + t, t, rect.H - 2.0 * t, r, g, b, edgeAlpha);
        }

        /// <summary>Растягивает белую текстуру 1x1 в цветной прямоугольник</summary>
        /// <param name="x">Левый край</param>
        /// <param name="y">Верхний край</param>
        /// <param name="w">Ширина</param>
        /// <param name="h">Высота</param>
        /// <param name="r">Красный 0..1</param>
        /// <param name="g">Зеленый 0..1</param>
        /// <param name="b">Синий 0..1</param>
        /// <param name="a">Альфа 0..1</param>
        private void FillRect(double x, double y, double w, double h, double r, double g, double b, double a)
        {
            if (w <= 0.0 || h <= 0.0) return;

            // предварительно умноженная альфа: rgb нужно умножить на a
            capi.Render.Render2DTexturePremultipliedAlpha(
                whiteTexture.TextureId, x, y, w, h, FrameZ,
                new Vec4f((float)(r * a), (float)(g * a), (float)(b * a), (float)a));
        }

        /// <summary>Создает единственный белый пиксель, из которого растягивается каждый прямоугольник рамки</summary>
        private void EnsureWhiteTexture()
        {
            if (whiteTexture != null && whiteTexture.TextureId != 0) return;

            using (ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1))
            using (Context ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                ctx.Paint();

                LoadedTexture tex = whiteTexture ?? new LoadedTexture(capi);
                capi.Gui.LoadOrUpdateCairoTexture(surface, false, ref tex);
                whiteTexture = tex;
            }
        }
    }
}