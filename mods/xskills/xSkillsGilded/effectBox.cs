using ImGuiController_OpenTK;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XEffects;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{
    public class EffectBox : IRenderer
    {
        ICoreClientAPI api;
        private ImGuiModSystem imguiModSystem;
        public XLeveling xLeveling;
        public XLevelingClient xLevelingClient;
        public XEffectsSystem xEffect;

        public double RenderOrder => 1;
        public int RenderRange => 0;

        public Effect tooltip;

        float windowWidth = _ui(400);
        float windowHeight = _ui(240);

        //string currentTooltip;
        //List<VTMLblock> tooltipVTML;

        public EffectBox(ICoreClientAPI api)
        {
            this.api = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Ortho);

            imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
            imguiModSystem.Draw += Draw;
            imguiModSystem.Closed += Close;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            ModConfig config = xSkillGraphicalUI.config;
            if (!config.effectBoxEnabled) return;

            // В режиме правки расположения бокс рисует редактор: реальные иконки и подсказка только мешали бы
            if (xSkillGraphicalUI.layoutEditActive)
            {
                tooltip = null;
                return;
            }

            // Свой масштаб: uiScale - глобальный статик, и его выставляет Draw окна навыков
            // Пока окно ни разу не открывали, здесь оставалось бы значение по умолчанию
            uiScale = ClientSettings.GUIScale;

            if (xLeveling == null || xLevelingClient == null) return;
            if (xEffect == null) xEffect = api.ModLoader.GetModSystem<XEffectsSystem>();

            AffectedEntityBehavior affected = api.World.Player.Entity.GetBehavior("Affected") as AffectedEntityBehavior;
            if (affected == null) return;

            ElementBounds window = api.Gui.WindowBounds;
            float windowWidth = (float)window.OuterWidth;
            float windowHeight = (float)window.OuterHeight;

            float fxx = config.effectBoxOriginX;
            float fxy = config.effectBoxOriginY;
            float fxs = config.effectBoxSize;

            if (config.effectBoxOrientation == 2) fxx -= fxs;
            else if (config.effectBoxOrientation == 3) fxy -= fxs;

            float mx = api.Input.MouseX;
            float my = api.Input.MouseY;

            tooltip = null;

            foreach (string affectName in affected.Effects.Keys)
            {
                Effect effect = affected.Effects[affectName];

                api.Render.RenderTexture(Sprite("effecticon", affectName).TextureId, fxx, fxy, fxs, fxs);

                if (effect.Duration > 0)
                {
                    float ratio = effect.Runtime / effect.Duration;
                    api.Render.RenderTexture(Sprite("elements", "pixel").TextureId, fxx, fxy, fxs, fxs * ratio, 50, new(0, 0, 0, 0.5f));
                }

                api.Render.RenderTexture(Sprite("elements", "abilitybox_frame_idle").TextureId, fxx, fxy, fxs, fxs);

                int stack = effect.Stacks;
                int stackMax = effect.MaxStacks;

                if (stackMax > 1f)
                {
                    float sts = _ui(6);
                    float stx = fxx + fxs / 2 - ((stackMax) * sts + (stackMax - 1) * _ui(2)) / 2;
                    float sty = fxy + fxs + _ui(3);

                    for (float i = 0; i < stackMax; i++)
                    {
                        float _stx = stx + i * (sts + _ui(2));
                        api.Render.RenderTexture(Sprite("elements", i < stack ? "skill_stack_on" : "skill_stack_off").TextureId, _stx, sty, sts, sts);
                    }
                }

                if (pointInRectangle(mx, my, fxx, fxy, fxx + fxs, fxy + fxs))
                    tooltip = effect;

                if (config.effectBoxOrientation == 0) fxx += fxs + _ui(8);
                else if (config.effectBoxOrientation == 1) fxy += fxs + _ui(8);
                else if (config.effectBoxOrientation == 2) fxx -= fxs + _ui(8);
                else if (config.effectBoxOrientation == 3) fxy -= fxs + _ui(8);
            }
        }

        public CallbackGUIStatus Draw(float deltaSecnds)
        {
            ModConfig config = xSkillGraphicalUI.config;

            if (!config.effectBoxEnabled) return CallbackGUIStatus.DontGrabMouse;
            if (tooltip == null) return CallbackGUIStatus.DontGrabMouse;

            uiScale = ClientSettings.GUIScale;
            windowWidth = _ui(400);

            ElementBounds window = api.Gui.WindowBounds;
            float screenWidth = (float)window.OuterWidth;
            float screenHeight = (float)window.OuterHeight;

            Vector2 mousePos = ImGui.GetMousePos();
            float mx = mousePos.X;
            float my = mousePos.Y;
            float ww = windowWidth - _ui(48);
            float hh = _ui(240);

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            float minX = viewport.Pos.X;
            float minY = viewport.Pos.Y;
            float maxX = viewport.Pos.X + viewport.Size.X - windowWidth;
            float maxY = viewport.Pos.Y + viewport.Size.Y - windowHeight;

            float wx = Math.Clamp(mx + _ui(16), minX, maxX);
            float wy = Math.Clamp(my + _ui(32), minY, maxY);

            ImGui.SetNextWindowSize(new(windowWidth, windowHeight));
            ImGui.SetNextWindowPos(new(wx, wy));
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
                 | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

            ImGui.Begin("effectBox", flags);
            drawImage(Sprite("elements", "bg"), 0, 0, windowWidth, windowHeight);

            string name = tooltip.GetName();

            float tx = _ui(24);
            float ty = _ui(28);

            float th = fTitleGold.getLineHeight();
            Vector2 s = drawTextFont(fTitleGold, name, tx, ty + th, HALIGN.Left, VALIGN.Bottom);
            ty += th;

            if (tooltip.Duration > 0)
            {
                string _time = FormatTime(tooltip.Duration - tooltip.Runtime) + "/" + FormatTime(tooltip.Duration);
                drawSetColor(c_white);
                drawTextFont(fSubtitle, _time, tx + ww, ty, HALIGN.Right, VALIGN.Bottom);
                ty += _ui(6);

                float ratio = 1 - tooltip.Runtime / tooltip.Duration;
                // drawProgressBar(ratio, tx, ty, ww, _ui(4), c_dkgrey, c_lime);
                drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), tx, ty, ww, _ui(4), 2);
                drawImage9patch(Sprite("elements", "abilitybox_progerss_content"), tx, ty, ww * ratio, _ui(4), 2);
                ty += _ui(10);

            }

            if (tooltip.MaxStacks > 1)
            {
                float ratio = tooltip.Stacks / tooltip.MaxStacks;
                // drawProgressBar(ratio, tx, ty, ww, _ui(4), c_dkgrey, c_lime);
                drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), tx, ty, ww, _ui(4), 2);
                drawImage9patch(Sprite("elements", "abilitybox_progerss_content_white"), tx, ty, ww * ratio, _ui(4), 2);
                ty += _ui(10);

            }

            if (tooltip.Duration == 0 && tooltip.MaxStacks == 0)
            {
                drawImage(Sprite("elements", "tooltip_sep"), tx, ty + _ui(4), ww, 1);
                ty += _ui(16);
            }

            string desc = tooltip.GetDescription().Replace("%", "%%");
            float h = drawTextWrap(desc, tx, ty, HALIGN.Left, VALIGN.Top, ww);
            ty += h + _ui(8);

            if (tooltip.MaxStacks > 1)
            {
                Vector2 mh = drawText(Lang.Get("xeffects:stacks") + ": " + tooltip.Stacks + "/" + tooltip.MaxStacks, tx, ty);
                ty += mh.Y + _ui(8);
            }

            if (tooltip.Interval > 0)
            {
                Vector2 mh = drawText(Lang.Get("xeffects:interval") + ": " + FormatTime(tooltip.Interval), tx, ty);
                ty += mh.Y + _ui(8);
            }

            if (tooltip is DiseaseEffect disease)
            {
                string _dis = disease.HealingRate != 0.0f ? Lang.Get("xeffects:healingrate") + ": " + string.Format("{0:0.00####}", disease.HealingRate * 60.0f) : "";
                Vector2 mh = drawText(_dis, tx, ty);
                ty += mh.Y + _ui(8);
            }

            drawImage9patch(Sprite("elements", "frame"), 0, 0, windowWidth, windowHeight, 60);
            ImGui.End();

            windowHeight = Math.Max(ty + _ui(24), _ui(240));

            return CallbackGUIStatus.DontGrabMouse;
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }

        private void Close() { }
    }
}