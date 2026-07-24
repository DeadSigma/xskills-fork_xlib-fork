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

        // Перетаскиваемый внешний элемент (слот сумки из XSkills и т.п.)
        private XpDropsLayoutEditor.Editable draggingExt;

        private double dragOffsetX;
        private double dragOffsetY;

        /// <summary>Создает диалог редактирования макета</summary>
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

        /// <summary>Отрисовывается поверх HUD</summary>
        public override double DrawOrder => 0.96;

        /// <summary>Запрашивает события мыши только пока открыт</summary>
        /// <returns><c>true</c> пока открыт</returns>
        public override bool ShouldReceiveMouseEvents() => IsOpened();

        /// <summary>Входит в режим предпросмотра и объявляет о начале редактирования для внешних HUD</summary>
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

            // Сообщаем внешним HUD, что идёт редактирование (они отключат свой перехват мыши)
            XpDropsLayoutEditor.IsEditing = true;

            capi.ShowChatMessage(XpDropsLang.Get("editmode-hint", HotkeyName(HotkeyCode)));
        }

        // Ссылка на открытое окно настроек
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

        /// <summary>Выходит из режима предпросмотра, сохраняет макет и внешние элементы</summary>
        public override void OnGuiClosed()
        {
            dragging = EnumXpDropsElement.None;
            draggingExt = null;
            manager.EditPreview = false;

            // Даём внешним элементам сохранить свою позицию и снимаем флаг редактирования
            XpDropsLayoutEditor.IsEditing = false;
            CommitExternals();

            onSave?.Invoke();
            capi.ShowChatMessage(XpDropsLang.Get("editmode-saved"));

            base.OnGuiClosed();
        }

        /// <summary>Рисует рамки вокруг каждого элемента (своих и внешних) и сам интерфейс</summary>
        /// <param name="deltaTime">Дельта кадра в секундах</param>
        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            EnsureWhiteTexture();

            EnumXpDropsElement hovered = dragging != EnumXpDropsElement.None
                ? dragging
                : HitTest(capi.Input.MouseX, capi.Input.MouseY);

            DrawFrame(manager.GetBarRect(), 0.35, 0.85, 0.35, hovered == EnumXpDropsElement.Bar);
            DrawFrame(manager.GetDropSpawnRect(), 1.0, 0.6, 0.15, hovered == EnumXpDropsElement.Drops);

            // Рамки внешних элементов
            XpDropsLayoutEditor.Editable hovExt = draggingExt
                ?? ((dragging == EnumXpDropsElement.None) ? HitTestExternal(capi.Input.MouseX, capi.Input.MouseY) : null);

            var items = XpDropsLayoutEditor.Items;
            for (int i = 0; i < items.Count; i++)
            {
                double[] r = SafeRect(items[i]);
                if (r == null) continue;
                DrawFrame(new GuiRect(r[0], r[1], r[2], r[3]), 0.35, 0.6, 1.0, items[i] == hovExt);
            }
        }

        /// <summary>Начинает перетаскивание при захвате элемента (сначала свои, затем внешние)</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseDown(MouseEvent args)
        {
            EnumXpDropsElement hit = HitTest(args.X, args.Y);
            if (hit != EnumXpDropsElement.None)
            {
                GuiRect rect = GetRect(hit);
                dragging = hit;
                dragOffsetX = args.X - rect.X;
                dragOffsetY = args.Y - rect.Y;
                args.Handled = true;
                return;
            }

            XpDropsLayoutEditor.Editable ext = HitTestExternal(args.X, args.Y);
            if (ext != null)
            {
                double[] r = SafeRect(ext);
                if (r != null)
                {
                    draggingExt = ext;
                    dragOffsetX = args.X - r[0];
                    dragOffsetY = args.Y - r[1];
                    args.Handled = true;
                    return;
                }
            }

            base.OnMouseDown(args);
        }

        /// <summary>Останавливает перетаскивание</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseUp(MouseEvent args)
        {
            if (dragging != EnumXpDropsElement.None || draggingExt != null)
            {
                dragging = EnumXpDropsElement.None;
                draggingExt = null;
                args.Handled = true;
                return;
            }

            base.OnMouseUp(args);
        }

        /// <summary>Преобразует перетаскиваемую позицию обратно в значения конфигурации или в позицию внешнего элемента</summary>
        /// <param name="args">Событие мыши</param>
        public override void OnMouseMove(MouseEvent args)
        {
            if (draggingExt != null)
            {
                double[] r = SafeRect(draggingExt);
                if (r != null)
                {
                    double frameW = capi.Render.FrameWidth;
                    double frameH = capi.Render.FrameHeight;
                    double x = Math.Clamp(args.X - dragOffsetX, 0.0, Math.Max(0.0, frameW - r[2]));
                    double y = Math.Clamp(args.Y - dragOffsetY, 0.0, Math.Max(0.0, frameH - r[3]));
                    try { draggingExt.SetTopLeft(x, y); } catch { }
                }
                args.Handled = true;
                return;
            }

            if (dragging == EnumXpDropsElement.None)
            {
                base.OnMouseMove(args);
                return;
            }

            XpDropConfig config = manager.Config;
            double scale = RuntimeEnv.GUIScale;
            double fw = capi.Render.FrameWidth;
            double fh = capi.Render.FrameHeight;

            GuiRect rect = GetRect(dragging);
            double nx = Math.Clamp(args.X - dragOffsetX, 0.0, Math.Max(0.0, fw - rect.W));
            double ny = Math.Clamp(args.Y - dragOffsetY, 0.0, Math.Max(0.0, fh - rect.H));

            if (dragging == EnumXpDropsElement.Bar)
            {
                config.BarRightMargin = (float)((fw - (nx + rect.W)) / scale);
                config.BarTopMargin = (float)(ny / scale);
            }
            else
            {
                GuiRect bar = manager.GetBarRect();
                config.TextSpawnOffsetX = (float)((nx + rect.W / 2.0 - bar.CenterX) / scale);
                config.TextSpawnBelowBar = (float)((ny - bar.Y) / scale);
            }

            args.Handled = true;
        }

        /// <summary>Изменяет размер элемента под курсором (своего или внешнего, если тот это поддерживает)</summary>
        /// <param name="args">Событие колесика мыши</param>
        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            EnumXpDropsElement hit = HitTest(capi.Input.MouseX, capi.Input.MouseY);
            if (hit != EnumXpDropsElement.None)
            {
                float step = args.delta > 0 ? ScaleStep : -ScaleStep;
                XpDropConfig config = manager.Config;

                if (hit == EnumXpDropsElement.Bar) config.BarScale = ClampScale(config.BarScale + step);
                else config.DropScale = ClampScale(config.DropScale + step);

                manager.InvalidateTextures();
                args.SetHandled();
                return;
            }

            XpDropsLayoutEditor.Editable ext = HitTestExternal(capi.Input.MouseX, capi.Input.MouseY);
            if (ext != null)
            {
                if (ext.OnScale != null)
                {
                    try { ext.OnScale(args.delta > 0 ? ScaleStep : -ScaleStep); } catch { }
                }
                args.SetHandled();
                return;
            }

            base.OnMouseWheel(args);
        }

        /// <summary>Освобождает текстуру рамки</summary>
        public override void Dispose()
        {
            whiteTexture?.Dispose();
            whiteTexture = null;
            base.Dispose();
        }

        // Хелперы для внешних элементов

        /// <summary>Безопасно берёт прямоугольник внешнего элемента ([x,y,w,h]) или null</summary>
        private static double[] SafeRect(XpDropsLayoutEditor.Editable e)
        {
            try
            {
                double[] r = e?.GetRect?.Invoke();
                return (r != null && r.Length == 4 && r[2] >= 1.0 && r[3] >= 1.0) ? r : null;
            }
            catch { return null; }
        }

        /// <summary>Внешний элемент под экранной точкой (последний зарегистрированный имеет приоритет)</summary>
        private XpDropsLayoutEditor.Editable HitTestExternal(double x, double y)
        {
            var items = XpDropsLayoutEditor.Items;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                double[] r = SafeRect(items[i]);
                if (r != null && x >= r[0] && x <= r[0] + r[2] && y >= r[1] && y <= r[1] + r[3]) return items[i];
            }
            return null;
        }

        /// <summary>Просит все внешние элементы сохранить свою позицию</summary>
        private static void CommitExternals()
        {
            var items = XpDropsLayoutEditor.Items;
            for (int i = 0; i < items.Count; i++)
            {
                try { items[i].OnCommit?.Invoke(); } catch { }
            }
        }

        private string HotkeyName(string code)
            => capi.Input.GetHotKeyByCode(code)?.CurrentMapping?.ToString() ?? code;

        private static float ClampScale(float value)
            => (float)Math.Clamp(Math.Round(value, 2), XpDropConfig.MinElementScale, XpDropConfig.MaxElementScale);

        private GuiRect GetRect(EnumXpDropsElement element)
        {
            if (element == EnumXpDropsElement.Bar) return manager.GetBarRect();
            if (element == EnumXpDropsElement.Drops) return manager.GetDropSpawnRect();

            return new GuiRect(0, 0, 0, 0);
        }

        private EnumXpDropsElement HitTest(double x, double y)
        {
            if (manager.GetDropSpawnRect().Contains(x, y)) return EnumXpDropsElement.Drops;
            if (manager.GetBarRect().Contains(x, y)) return EnumXpDropsElement.Bar;

            return EnumXpDropsElement.None;
        }

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

        private void FillRect(double x, double y, double w, double h, double r, double g, double b, double a)
        {
            if (w <= 0.0 || h <= 0.0) return;

            capi.Render.Render2DTexturePremultipliedAlpha(
                whiteTexture.TextureId, x, y, w, h, FrameZ,
                new Vec4f((float)(r * a), (float)(g * a), (float)(b * a), (float)a));
        }

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