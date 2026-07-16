using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PandaXPDrops
{
    /// <summary>
    /// Чистый слой отрисовки HUD выпадения опыта - все состояние и макет находятся в <see cref="XpDropManager"/>
    /// </summary>
    /// <remarks>
    /// Не переключается и не фокусируется: он никогда не перехватывает ввод и не имеет привязки клавиш, именно поэтому
    /// редактирование макета находится в отдельном <see cref="XpDropsEditDialog"/> - HUD не может освободить курсор мыши
    /// Менеджер получает тики отсюда, поэтому метки анимируются по таймеру рендеринга, а не по игровому тику
    /// </remarks>
    public class XpDropsHud : HudElement
    {
        private readonly XpDropManager manager;

        /// <summary>Создает HUD. Вызовите <see cref="ComposeAndOpen"/> после загрузки мира</summary>
        /// <param name="capi">Клиентское API</param>
        /// <param name="manager">Менеджер, хранящий состояние полосы и меток</param>
        public XpDropsHud(ICoreClientAPI capi, XpDropManager manager) : base(capi)
        {
            this.manager = manager;
        }

        /// <summary>Нет горячей клавиши - скрытие и показ обрабатываются системой модов, а не системой диалогов</summary>
        public override string ToggleKeyCombinationCode => null;

        /// <summary>Никогда не потребляет ввод мыши</summary>
        /// <returns><c>false</c></returns>
        public override bool ShouldReceiveMouseEvents() => false;

        /// <summary>Никогда не потребляет ввод с клавиатуры</summary>
        /// <returns><c>false</c></returns>
        public override bool ShouldReceiveKeyboardEvents() => false;

        /// <summary>Никогда не забирает фокус у других диалогов</summary>
        public override bool Focusable => false;

        /// <summary>Отрисовывается поверх обычных элементов HUD, ниже оверлея редактирования</summary>
        public override double DrawOrder => 0.95;

        /// <summary>
        /// Открывает диалог с пустым компоновщиком (composer) - каждый пиксель рисуется вручную в <see cref="OnRenderGUI"/>,
        /// компоновщик существует только потому, что он нужен <see cref="GuiDialog"/> для открытия
        /// </summary>
        public void ComposeAndOpen()
        {
            SingleComposer = capi.Gui
                .CreateCompo("pandaxpdrops-hud", ElementBounds.Fixed(0, 0, 0, 0))
                .Compose();

            TryOpen();
        }

        /// <summary>Передает тики менеджеру и рисует полосу, а также каждую плавающую метку</summary>
        /// <param name="deltaTime">Дельта кадра в секундах</param>
        /// <remarks>
        /// Предпросмотр редактирования намеренно переопределяет скрытое состояние: макет остается доступным для
        /// редактирования при выключенном отображении, и его выключение не приводит к тихому возврату к видимому состоянию
        /// </remarks>
        public override void OnRenderGUI(float deltaTime)
        {
            if (manager == null) return;
            if (!manager.Config.Enabled && !manager.EditPreview) return;

            manager.OnFrame(deltaTime);

            RenderBar(manager.GetBarRect());
            RenderDrops(manager.GetDropSpawnRect());
        }

        /// <summary>Рисует запеченную текстуру полосы и иконку стака предметов поверх нее</summary>
        /// <param name="bar">Экранный прямоугольник полосы</param>
        private void RenderBar(GuiRect bar)
        {
            float alpha = manager.BarAlpha;
            int texId = manager.BarTextureId;
            if (alpha <= 0.001f || texId == 0) return;

            capi.Render.Render2DTexturePremultipliedAlpha(
                texId, bar.X, bar.Y, bar.W, bar.H, 90f,
                new Vec4f(alpha, alpha, alpha, alpha));

            DummySlot slot = manager.GetSkillSlot(manager.BarSkillName);
            if (slot?.Itemstack == null) return; // запасной вариант с буквой уже запечен в текстуру полосы

            // затухает вместе с полосой; равно -1 (без оттенка), пока полностью видимо
            int tint = ColorUtil.ColorFromRgba(255, 255, 255, (int)(alpha * 255f));

            capi.Render.RenderItemstackToGui(
                slot,
                bar.X + manager.BarIconCenterX,
                bar.Y + manager.BarIconCenterY,
                100.0,
                manager.BarIconRenderSize,
                tint, true, false, false);
        }

        /// <summary>Рисует каждую живую метку, отцентрированную по зоне появления и поднимающуюся с течением времени</summary>
        /// <param name="spawn">Экранный прямоугольник, в котором появляются метки</param>
        private void RenderDrops(GuiRect spawn)
        {
            XpDropConfig config = manager.Config;
            double scale = RuntimeEnv.GUIScale;
            double centerX = spawn.CenterX;
            IReadOnlyList<XpDrop> drops = manager.ActiveDrops;

            for (int i = 0; i < drops.Count; i++)
            {
                XpDrop drop = drops[i];
                LoadedTexture tex = drop.Texture;
                if (tex == null || tex.TextureId == 0) continue;

                float alpha = drop.GetAlpha(config.DropLifetime, config.FadeStartPct);
                if (alpha <= 0.001f) continue;

                double x = centerX - tex.Width / 2.0;
                double y = spawn.Y + (drop.SpawnOffsetY - drop.Age * config.FloatSpeed) * scale;

                capi.Render.Render2DTexturePremultipliedAlpha(
                    tex.TextureId, x, y, tex.Width, tex.Height, 91f,
                    new Vec4f(alpha, alpha, alpha, alpha));
            }
        }
    }
}