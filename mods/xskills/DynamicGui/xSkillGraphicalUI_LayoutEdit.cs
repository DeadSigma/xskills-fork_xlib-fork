using ImGuiNET;
using System;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{
    /// <summary>Элемент интерфейса, который можно двигать и масштабировать в режиме правки</summary>
    public enum EnumGildedElement
    {
        /// <summary>Под курсором ничего нет</summary>
        None,

        /// <summary>Окно навыков</summary>
        Window,

        /// <summary>Полоса иконок эффектов</summary>
        EffectBox,

        /// <summary>Уведомление о новом уровне</summary>
        LevelPopup
    }


    /// <summary>
    /// Режим правки расположения: рамки вокруг элементов, перетаскивание мышью, размер колесом, запись конфига на выходе.
    /// </summary>
    /// <remarks>
    /// Рисуется в собственном полноэкранном прозрачном окне ImGui. Внутри окна навыков это было бы невозможно:
    /// <c>drawImage</c> ходит через <c>ImGui.SetCursorPos</c>, то есть координаты локальны для окна, а всё, что
    /// выходит за его границы, ImGui обрезает - эффект-бокс и попап живут в других углах экрана
    /// Настоящий контент окна навыков в этом режиме не выполняется вовсе: весь UI сидит на сырых
    /// <c>ImGui.IsMouseClicked</c> внутри <c>mouseHover(...)</c>, и протащить мышь по абилке значило бы потратить очки
    /// Включается снаружи (хоткеем другого мода) через <see cref="ToggleLayoutEdit"/>.
    /// </remarks>
    public partial class xSkillGraphicalUI
    {
        /// <summary>Нижняя граница масштаба окна и попапа</summary>
        public const float MinLayoutScale = 0.4f;

        /// <summary>Верхняя граница масштаба окна и попапа. Окно всё равно вписывается в экран</summary>
        public const float MaxLayoutScale = 2f;

        /// <summary>Базовая ширина попапа уровня до умножения на масштаб</summary>
        public const float LevelPopupBaseWidth = 560f;

        /// <summary>Базовая высота попапа уровня до умножения на масштаб</summary>
        public const float LevelPopupBaseHeight = 160f;

        private const float LayoutScaleStep = 0.05f;
        private const float MinEffectBoxSize = 12f;
        private const float MaxEffectBoxSize = 256f;

        /// <summary>Прямоугольник в абсолютных экранных координатах ImGui</summary>
        private readonly struct Rect
        {
            /// <summary>Левый край</summary>
            public readonly float X;

            /// <summary>Верхний край</summary>
            public readonly float Y;

            /// <summary>Ширина</summary>
            public readonly float W;

            /// <summary>Высота</summary>
            public readonly float H;

            /// <summary>Создаёт прямоугольник</summary>
            /// <param name="x">Левый край</param>
            /// <param name="y">Верхний край</param>
            /// <param name="w">Ширина</param>
            /// <param name="h">Высота</param>
            public Rect(float x, float y, float w, float h)
            {
                X = x; Y = y; W = w; H = h;
            }

            /// <summary>Находится ли точка внутри</summary>
            /// <param name="p">Точка в абсолютных координатах</param>
            /// <returns><c>true</c>, если точка внутри</returns>
            public bool Contains(Vector2 p) => p.X >= X && p.X <= X + W && p.Y >= Y && p.Y <= Y + H;
        }

        /// <summary>Идёт ли сейчас правка расположения</summary>
        public bool layoutEditMode { get; private set; }

        /// <summary>
        /// Статическое зеркало <see cref="layoutEditMode"/> для классов, у которых нет ссылки на эту систему
        /// (<see cref="EffectBox"/> рисуется своим рендерером)
        /// </summary>
        public static bool layoutEditActive { get; private set; }

        private EnumGildedElement layoutDragging;
        private string layoutExitKeyName;

        /// <summary>Пользовательский множитель размера окна навыков в допустимых границах</summary>
        public float LayoutScale => config == null ? 1f : Math.Clamp(config.windowScale, MinLayoutScale, MaxLayoutScale);

        /// <summary>Пользовательский множитель размера попапа уровня в допустимых границах</summary>
        public static float LevelPopupScale => config == null ? 1f : Math.Clamp(config.levelPopupScale, MinLayoutScale, MaxLayoutScale);

        /// <summary>Итоговый <c>uiScale</c> попапа уровня: масштаб GUI игры и пользовательский множитель</summary>
        public static float LevelPopupUiScale => ClientSettings.GUIScale * LevelPopupScale;

        /// <summary>
        /// Включает/выключает режим правки. Вызывается другим модом по рефлексии
        /// </summary>
        /// <param name="exitKeyName">Клавиша для экранной подсказки, например "F6". Может быть <c>null</c></param>
        /// <returns>
        /// <c>false</c>, если окно навыков закрыто и править нечего - вызывающий может уйти в свой редактор
        /// </returns>
        public bool ToggleLayoutEdit(string exitKeyName)
        {
            if (!isOpen) return false;

            layoutExitKeyName = exitKeyName;
            SetLayoutEdit(!layoutEditMode);
            return true;
        }

        /// <summary>Переключает режим правки и на выходе пишет конфиг</summary>
        /// <param name="on">Целевое состояние</param>
        public void SetLayoutEdit(bool on)
        {
            if (layoutEditMode == on) return;

            layoutEditMode = on;
            layoutEditActive = on;
            layoutDragging = EnumGildedElement.None;
            if (!on) SaveLayout();
        }

        /// <summary>Сохраняет позиции и масштабы в обычный конфиг мода</summary>
        private void SaveLayout()
        {
            try
            {
                api?.StoreModConfig(config, configFileName);
            }
            catch (Exception ex)
            {
                api?.Logger.Error($"[xSkillGilded] Не удалось сохранить {configFileName}: {ex}");
            }
        }

        /// <summary>
        /// Расчёт размера окна навыков: масштаб GUI игры на пользовательский множитель, ужатый до размеров экрана
        /// Единая точка для <c>Draw</c> и <see cref="DrawLayoutEdit"/>
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Итоговая ширина в пикселях</param>
        /// <param name="windowHeight">Итоговая высота в пикселях</param>
        private void ComputeLayoutSize(ImGuiViewportPtr viewport, out int windowWidth, out int windowHeight)
        {
            uiScale = ClientSettings.GUIScale * LayoutScale;

            const float padding = 64f;
            float maxW = viewport.Size.X - padding;
            float maxH = viewport.Size.Y - padding;

            if (windowBaseWidth * uiScale > maxW || windowBaseHeight * uiScale > maxH)
            {
                uiScale = Math.Min(maxW / windowBaseWidth, maxH / windowBaseHeight);
            }

            windowWidth = (int)(windowBaseWidth * uiScale);
            windowHeight = (int)(windowBaseHeight * uiScale);
        }

        /// <summary>
        /// Левый верхний угол окна навыков: сохранённая позиция либо центр экрана, пока игрок его не двигал
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна</param>
        /// <param name="windowHeight">Текущая высота окна</param>
        /// <returns>Абсолютная позиция, зажатая в границы viewport</returns>
        private Vector2 GetLayoutWindowPos(ImGuiViewportPtr viewport, int windowWidth, int windowHeight)
        {
            if (config == null || !config.windowPosSet)
            {
                Vector2 center = viewport.GetCenter();
                return ClampToViewport(viewport, new Vector2(center.X - windowWidth / 2f, center.Y - windowHeight / 2f), windowWidth, windowHeight);
            }

            return ClampToViewport(viewport, new Vector2(viewport.Pos.X + config.windowX, viewport.Pos.Y + config.windowY), windowWidth, windowHeight);
        }

        /// <summary>
        /// Левый верхний угол попапа уровня: сохранённая позиция либо центр сверху
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="width">Текущая ширина попапа</param>
        /// <param name="height">Текущая высота попапа</param>
        /// <returns>Абсолютная позиция, зажатая в границы viewport</returns>
        public static Vector2 GetLevelPopupPos(ImGuiViewportPtr viewport, float width, float height)
        {
            if (config == null || !config.levelPopupPosSet)
            {
                float defaultY = viewport.Pos.Y + 8f * LevelPopupUiScale;
                return ClampToViewport(viewport, new Vector2(viewport.Pos.X + viewport.Size.X / 2f - width / 2f, defaultY), width, height);
            }

            return ClampToViewport(viewport, new Vector2(viewport.Pos.X + config.levelPopupX, viewport.Pos.Y + config.levelPopupY), width, height);
        }

        /// <summary>
        /// Не даёт элементу уехать за экран. Позиция, сохранённая на большем мониторе, иначе оставила бы элемент
        /// за пределами видимой области без всякой возможности его достать
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="pos">Желаемая абсолютная позиция</param>
        /// <param name="width">Ширина элемента</param>
        /// <param name="height">Высота элемента</param>
        /// <returns>Зажатая позиция</returns>
        private static Vector2 ClampToViewport(ImGuiViewportPtr viewport, Vector2 pos, float width, float height)
        {
            float maxX = viewport.Pos.X + Math.Max(0f, viewport.Size.X - width);
            float maxY = viewport.Pos.Y + Math.Max(0f, viewport.Size.Y - height);

            return new Vector2(Math.Clamp(pos.X, viewport.Pos.X, maxX), Math.Clamp(pos.Y, viewport.Pos.Y, maxY));
        }

        /// <summary>Прямоугольник окна навыков</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна</param>
        /// <param name="windowHeight">Текущая высота окна</param>
        /// <returns>Абсолютный прямоугольник</returns>
        private Rect GetWindowRect(ImGuiViewportPtr viewport, int windowWidth, int windowHeight)
        {
            Vector2 pos = GetLayoutWindowPos(viewport, windowWidth, windowHeight);
            return new Rect(pos.X, pos.Y, windowWidth, windowHeight);
        }

        /// <summary>
        /// Прямоугольник первой ячейки эффект-бокса. Повторяет сдвиг из <see cref="EffectBox.OnRenderFrame"/>:
        /// при ориентации 2 и 3 точка отсчёта - правый/нижний край, а не левый верхний.
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <returns>Абсолютный прямоугольник одной ячейки</returns>
        private static Rect GetEffectBoxRect(ImGuiViewportPtr viewport)
        {
            float size = Math.Clamp(config.effectBoxSize, MinEffectBoxSize, MaxEffectBoxSize);
            float x = config.effectBoxOriginX;
            float y = config.effectBoxOriginY;

            if (config.effectBoxOrientation == 2) x -= size;
            else if (config.effectBoxOrientation == 3) y -= size;

            return new Rect(viewport.Pos.X + x, viewport.Pos.Y + y, size, size);
        }

        /// <summary>Прямоугольник попапа уровня</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <returns>Абсолютный прямоугольник</returns>
        private static Rect GetLevelPopupRect(ImGuiViewportPtr viewport)
        {
            float scale = LevelPopupUiScale;
            float width = LevelPopupBaseWidth * scale;
            float height = LevelPopupBaseHeight * scale;
            Vector2 pos = GetLevelPopupPos(viewport, width, height);

            return new Rect(pos.X, pos.Y, width, height);
        }

        /// <summary>
        /// Полная замена <c>Draw</c> на время правки: прозрачное окно во весь экран, силуэты всех трёх элементов,
        /// рамки и подсказка
        /// </summary>
        /// <returns>Всегда <see cref="CallbackGUIStatus.GrabMouse"/> - редактору нужен курсор</returns>
        private CallbackGUIStatus DrawLayoutEdit()
        {
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            ComputeLayoutSize(viewport, out int windowWidth, out int windowHeight);

            // Масштаб самого редактора (подписи, подсказка): размеры элементов считаются отдельно, каждый со своим
            uiScale = ClientSettings.GUIScale;
            if (!useInternalTextDrawer)
            {
                fTitleGold.baseScale = _ui(1);
                fSubtitle.baseScale = _ui(1);
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            ImGui.SetNextWindowSize(viewport.Size);
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.SetNextWindowPos(viewport.Pos);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings;

            ImGui.Begin("xSkillGilded_layoutedit", flags);
            try
            {
                // Начало координат хелперов: локальные координаты этого окна = абсолютные минус угол viewport
                windowPosX = viewport.Pos.X;
                windowPosY = viewport.Pos.Y;

                UpdateLayoutEdit(viewport, windowWidth, windowHeight);

                EnumGildedElement hot = layoutDragging != EnumGildedElement.None
                    ? layoutDragging
                    : HitTest(viewport, windowWidth, windowHeight, ImGui.GetIO().MousePos);

                DrawWindowGhost(viewport, GetWindowRect(viewport, windowWidth, windowHeight), hot == EnumGildedElement.Window);
                DrawLevelPopupGhost(viewport, GetLevelPopupRect(viewport), hot == EnumGildedElement.LevelPopup);
                DrawEffectBoxGhost(viewport, GetEffectBoxRect(viewport), hot == EnumGildedElement.EffectBox);
                DrawLayoutHint(viewport);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[xSkillGilded] Ошибка в DrawLayoutEdit: {ex}");
            }
            finally
            {
                ImGui.End();
                ImGui.PopStyleVar(3);
                drawSetColor(c_white);
            }

            return CallbackGUIStatus.GrabMouse;
        }

        /// <summary>Элемент под точкой. Мелкие проверяются первыми: попап и бокс лежат поверх окна навыков</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна навыков</param>
        /// <param name="windowHeight">Текущая высота окна навыков</param>
        /// <param name="point">Точка в абсолютных координатах</param>
        /// <returns>Найденный элемент или <see cref="EnumGildedElement.None"/></returns>
        private EnumGildedElement HitTest(ImGuiViewportPtr viewport, int windowWidth, int windowHeight, Vector2 point)
        {
            if (GetEffectBoxRect(viewport).Contains(point)) return EnumGildedElement.EffectBox;
            if (GetLevelPopupRect(viewport).Contains(point)) return EnumGildedElement.LevelPopup;
            if (GetWindowRect(viewport, windowWidth, windowHeight).Contains(point)) return EnumGildedElement.Window;

            return EnumGildedElement.None;
        }

        /// <summary>
        /// Перетаскивание и колесо, вручную по сырому состоянию мыши.
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна навыков</param>
        /// <param name="windowHeight">Текущая высота окна навыков</param>
        /// <remarks>
        /// Штатный перенос окна ImGui не используется: позиция всё равно каждый кадр приходит из конфига, а
        /// таскать окно без заголовка - значит зависеть от того, где ImGui считает свободное место
        /// </remarks>
        private void UpdateLayoutEdit(ImGuiViewportPtr viewport, int windowWidth, int windowHeight)
        {
            if (config == null) return;

            AdoptCurrentPositions(viewport, windowWidth, windowHeight);

            ImGuiIOPtr io = ImGui.GetIO();
            EnumGildedElement hot = layoutDragging != EnumGildedElement.None
                ? layoutDragging
                : HitTest(viewport, windowWidth, windowHeight, io.MousePos);

            if (hot != EnumGildedElement.None && io.MouseWheel != 0f) ApplyWheel(hot, io.MouseWheel > 0f ? 1f : -1f);

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                if (layoutDragging == EnumGildedElement.None) layoutDragging = hot;
                if (layoutDragging != EnumGildedElement.None) ApplyDrag(layoutDragging, io.MouseDelta);
            }
            else layoutDragging = EnumGildedElement.None;

            ClampPositions(viewport, windowWidth, windowHeight);
        }

        /// <summary>
        /// Первый кадр правки: забирает текущие позиции элементов в конфиг, иначе первое же перетаскивание
        /// телепортировало бы элемент в левый верхний угол
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна навыков</param>
        /// <param name="windowHeight">Текущая высота окна навыков</param>
        private void AdoptCurrentPositions(ImGuiViewportPtr viewport, int windowWidth, int windowHeight)
        {
            if (!config.windowPosSet)
            {
                Vector2 pos = GetLayoutWindowPos(viewport, windowWidth, windowHeight);
                config.windowX = (int)(pos.X - viewport.Pos.X);
                config.windowY = (int)(pos.Y - viewport.Pos.Y);
                config.windowPosSet = true;
            }

            if (!config.levelPopupPosSet)
            {
                Rect popup = GetLevelPopupRect(viewport);
                config.levelPopupX = (int)(popup.X - viewport.Pos.X);
                config.levelPopupY = (int)(popup.Y - viewport.Pos.Y);
                config.levelPopupPosSet = true;
            }
        }

        /// <summary>Сдвигает элемент на дельту мыши</summary>
        /// <param name="element">Элемент</param>
        /// <param name="delta">Смещение курсора за кадр</param>
        private void ApplyDrag(EnumGildedElement element, Vector2 delta)
        {
            switch (element)
            {
                case EnumGildedElement.Window:
                    config.windowX += (int)delta.X;
                    config.windowY += (int)delta.Y;
                    break;

                case EnumGildedElement.EffectBox:
                    config.effectBoxOriginX += delta.X;
                    config.effectBoxOriginY += delta.Y;
                    break;

                case EnumGildedElement.LevelPopup:
                    config.levelPopupX += (int)delta.X;
                    config.levelPopupY += (int)delta.Y;
                    break;
            }
        }

        /// <summary>Меняет размер элемента на один щелчок колеса</summary>
        /// <param name="element">Элемент</param>
        /// <param name="direction">1 - вверх, -1 - вниз</param>
        private void ApplyWheel(EnumGildedElement element, float direction)
        {
            switch (element)
            {
                case EnumGildedElement.Window:
                    config.windowScale = ClampScale(LayoutScale + LayoutScaleStep * direction);
                    break;

                case EnumGildedElement.LevelPopup:
                    config.levelPopupScale = ClampScale(LevelPopupScale + LayoutScaleStep * direction);
                    break;

                case EnumGildedElement.EffectBox:
                    // Размер бокса задан в пикселях, а не множителем: шаг пропорциональный, но не меньше пикселя
                    float size = config.effectBoxSize;
                    float step = Math.Max(1f, size * LayoutScaleStep);
                    config.effectBoxSize = Math.Clamp((float)Math.Round(size + step * direction), MinEffectBoxSize, MaxEffectBoxSize);
                    break;
            }
        }

        /// <summary>Не даёт ни одному элементу уползти за экран, в том числе после смены разрешения</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="windowWidth">Текущая ширина окна навыков</param>
        /// <param name="windowHeight">Текущая высота окна навыков</param>
        private void ClampPositions(ImGuiViewportPtr viewport, int windowWidth, int windowHeight)
        {
            config.windowX = (int)Math.Clamp(config.windowX, 0f, Math.Max(0f, viewport.Size.X - windowWidth));
            config.windowY = (int)Math.Clamp(config.windowY, 0f, Math.Max(0f, viewport.Size.Y - windowHeight));

            // У бокса точка отсчёта не совпадает с углом ячейки при ориентациях 2 и 3, поэтому правится
            // не позиция, а дельта: так математика одна на все четыре ориентации
            Rect box = GetEffectBoxRect(viewport);
            Vector2 boxClamped = ClampToViewport(viewport, new Vector2(box.X, box.Y), box.W, box.H);
            config.effectBoxOriginX += boxClamped.X - box.X;
            config.effectBoxOriginY += boxClamped.Y - box.Y;

            Rect popup = GetLevelPopupRect(viewport);
            Vector2 popupClamped = ClampToViewport(viewport, new Vector2(popup.X, popup.Y), popup.W, popup.H);
            config.levelPopupX += (int)(popupClamped.X - popup.X);
            config.levelPopupY += (int)(popupClamped.Y - popup.Y);
        }

        /// <summary>Округляет масштаб до шага и держит его в допустимых границах</summary>
        /// <param name="value">Желаемый масштаб</param>
        /// <returns>Зажатый масштаб</returns>
        private static float ClampScale(float value)
            => (float)Math.Clamp(Math.Round(value, 2), MinLayoutScale, MaxLayoutScale);

        /// <summary>Силуэт окна навыков: настоящий фон и рамка нужного размера</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="rect">Прямоугольник окна</param>
        /// <param name="hot">Курсор над элементом или элемент тащат</param>
        private void DrawWindowGhost(ImGuiViewportPtr viewport, Rect rect, bool hot)
        {
            float x = rect.X - viewport.Pos.X;
            float y = rect.Y - viewport.Pos.Y;

            drawSetColor(c_white);
            drawImage(Sprite("elements", "bg"), x, y, rect.W, rect.H);
            drawImage9patch(Sprite("elements", "frame"), x, y, rect.W, rect.H, 60);

            DrawElementFrame(viewport, rect, hot, Lang.Get("xskillgilded:layout-edit-el-window"));
        }

        /// <summary>Силуэт попапа уровня: свечение и разделитель, как в настоящем</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="rect">Прямоугольник попапа</param>
        /// <param name="hot">Курсор над элементом или элемент тащат</param>
        private void DrawLevelPopupGhost(ImGuiViewportPtr viewport, Rect rect, bool hot)
        {
            float x = rect.X - viewport.Pos.X;
            float y = rect.Y - viewport.Pos.Y;
            float scale = LevelPopupUiScale;

            drawSetColor(c_dkgrey, 0.8f);
            drawImage(Sprite("elements", "level_up_glow"), x, y, rect.W, rect.H);

            drawSetColor(c_white);
            float sepWidth = rect.W - 80f * scale;
            drawImage(Sprite("elements", "level_sep"), x + rect.W / 2f - sepWidth / 2f, y + rect.H / 2f - 64f * scale, sepWidth, 64f * scale);

            drawSetColor(c_gold);
            drawTextFont(fTitleGold, Lang.Get("xskillgilded:layout-edit-el-levelpopup"), x + rect.W / 2f, y + rect.H / 2f, HALIGN.Center, VALIGN.Center);

            DrawElementFrame(viewport, rect, hot, Lang.Get("xskillgilded:layout-edit-el-levelpopup"));
        }

        /// <summary>
        /// Силуэт эффект-бокса: три пустых ячейки, чтобы была видна не только точка отсчёта, но и направление
        /// раскладки, заданное <c>effectBoxOrientation</c>
        /// </summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="rect">Прямоугольник первой ячейки</param>
        /// <param name="hot">Курсор над элементом или элемент тащат</param>
        private void DrawEffectBoxGhost(ImGuiViewportPtr viewport, Rect rect, bool hot)
        {
            float gap = rect.W + 8f * ClientSettings.GUIScale;
            float stepX = 0f;
            float stepY = 0f;

            if (config.effectBoxOrientation == 0) stepX = gap;
            else if (config.effectBoxOrientation == 1) stepY = gap;
            else if (config.effectBoxOrientation == 2) stepX = -gap;
            else if (config.effectBoxOrientation == 3) stepY = -gap;

            float x = rect.X - viewport.Pos.X;
            float y = rect.Y - viewport.Pos.Y;

            for (int i = 0; i < 3; i++)
            {
                drawSetColor(c_white, i == 0 ? 1f : 0.35f);
                drawImage(Sprite("elements", "abilitybox_frame_idle"), x + stepX * i, y + stepY * i, rect.W, rect.H);
            }

            DrawElementFrame(viewport, rect, hot, Lang.Get("xskillgilded:layout-edit-el-effectbox"));
        }

        /// <summary>Заливка, обводка и подпись элемента</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        /// <param name="rect">Прямоугольник элемента</param>
        /// <param name="hot">Курсор над элементом или элемент тащат</param>
        /// <param name="label">Подпись над рамкой</param>
        private void DrawElementFrame(ImGuiViewportPtr viewport, Rect rect, bool hot, string label)
        {
            float x = rect.X - viewport.Pos.X;
            float y = rect.Y - viewport.Pos.Y;
            float thickness = Math.Max(2f, 2f * ClientSettings.GUIScale);
            LoadedTexture pixel = Sprite("elements", "pixel");

            drawSetColor(c_gold, hot ? 0.25f : 0.08f);
            drawImage(pixel, x, y, rect.W, rect.H);

            // Обводка из четырёх полосок, а не 9patch: он рвётся, когда рамка меньше своих же углов.
            drawSetColor(c_gold, hot ? 1f : 0.6f);
            drawImage(pixel, x, y, rect.W, thickness);
            drawImage(pixel, x, y + rect.H - thickness, rect.W, thickness);
            drawImage(pixel, x, y + thickness, thickness, rect.H - thickness * 2f);
            drawImage(pixel, x + rect.W - thickness, y + thickness, thickness, rect.H - thickness * 2f);

            drawSetColor(hot ? c_gold : c_white);
            drawTextFont(fSubtitle, label, x + rect.W / 2f, y - _ui(2), HALIGN.Center, VALIGN.Bottom);
            drawSetColor(c_white);
        }

        /// <summary>Подсказка внизу экрана: сверху её занял бы попап уровня</summary>
        /// <param name="viewport">Главный viewport ImGui</param>
        private void DrawLayoutHint(ImGuiViewportPtr viewport)
        {
            float cx = viewport.Size.X / 2f;
            float cy = viewport.Size.Y - _ui(48);

            drawSetColor(c_gold);
            drawTextFont(fTitleGold, Lang.Get("xskillgilded:layout-edit-title"), cx, cy, HALIGN.Center, VALIGN.Bottom);

            drawSetColor(c_white);
            drawTextFont(fSubtitle, Lang.Get("xskillgilded:layout-edit-hint", layoutExitKeyName ?? "F6"), cx, cy + _ui(4), HALIGN.Center, VALIGN.Top);
        }
    }
}