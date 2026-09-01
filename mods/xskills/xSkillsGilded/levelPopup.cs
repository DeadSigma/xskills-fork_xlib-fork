using Cairo;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XEffects;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{

    public class LevelPopup
    {
        ICoreClientAPI api;
        private ImGuiModSystem imguiModSystem;
        PlayerSkill skill;

        float timer = 0;
        bool showing = true;

        float windowWidth;
        float windowHeight;

        public LevelPopup(ICoreClientAPI api, PlayerSkill skill)
        {
            this.api = api;
            this.skill = skill;

            imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
            imguiModSystem.Draw += Draw;
            imguiModSystem.Closed += Close;
        }

        public CallbackGUIStatus Draw(float deltaSecnds)
        {
            if (!showing) return CallbackGUIStatus.DontGrabMouse;

            // Свой масштаб, а не тот, что остался от последней отрисовки окна навыков
            uiScale = xSkillGraphicalUI.LevelPopupUiScale;
            windowWidth = _ui(xSkillGraphicalUI.LevelPopupBaseWidth);
            windowHeight = _ui(xSkillGraphicalUI.LevelPopupBaseHeight);

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            // Позиция из конфига (правится по хоткею), либо центр сверху, если игрок её не двигал
            Vector2 pos = xSkillGraphicalUI.GetLevelPopupPos(viewport, windowWidth, windowHeight);

            ImGui.SetNextWindowSize(new(windowWidth, windowHeight));
            ImGui.SetNextWindowPos(pos);
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
                 | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

            // Уникальное имя окна для каждого навыка, чтобы ImGui не путался
            ImGui.Begin($"levelPopup_{skill.Skill.Id}", flags);
            try
            {
                drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
                drawImage(Sprite("elements", "level_up_glow"), 0, 0, windowWidth, windowHeight);
                drawSetColor(c_white, invLerp2(timer, 0f, .5f, 3.5f, 4f));
                float ww = invLerp(timer, 0f, .75f) * (windowWidth - _ui(80));
                drawImage(Sprite("elements", "level_sep"), windowWidth / 2 - ww / 2, windowHeight / 2 - _ui(64), ww, _ui(64));
                drawSetColor(c_white);

                LoadedTexture skillIcon = Sprite("skillicon", skill.Skill.Name);
                if (skillIcon.TextureId != 0)
                {
                    drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
                    drawImage(Sprite("elements", "level_up_glow"), windowWidth / 2 - _ui(40), windowHeight / 2 - _ui(40), _ui(80), _ui(80));
                    drawSetColor(c_gold, invLerp2(timer, 0f, 1f, 3.5f, 4f));
                    drawImage(skillIcon, windowWidth / 2 - _ui(16), windowHeight / 2 - _ui(16), _ui(32), _ui(32));
                    drawSetColor(c_white);
                }

                string lvUpText = $"{skill.Skill.DisplayName} Level up";
                drawSetColor(c_white, invLerp2(timer, 0f, .3f, 3.5f, 4f));
                Vector2 lvUpText_s = drawTextFont(fTitleGold, lvUpText, windowWidth / 2, windowHeight / 2 - _ui(16), HALIGN.Center, VALIGN.Bottom);
                drawSetColor(c_white);

              

                // Правильный ID хоткея и защита от вылета
                var hk = api.Input.GetHotKeyByCode("xSkillGilded_v2");
                string hotkeyText = hk != null
                    ? Lang.Get("xskillgilded:skilltree-hotkey-mapped", hk.CurrentMapping.ToString())
                    : Lang.Get("xskillgilded:skilltree-hotkey-unmapped");

                drawSetColor(c_white, invLerp2(timer, .3f, .6f, 3.5f, 4f) * .8f);
                Vector2 hotkeyText_s = drawTextFont(fSubtitle, hotkeyText, windowWidth / 2, windowHeight / 2 + _ui(16), HALIGN.Center, VALIGN.Top);
                drawSetColor(c_white);

            }
            catch (Exception ex)
            {
                api.Logger.Error($"[xSkillGilded] Ошибка в levelPopup: {ex}");
            }
            finally
            {
                // Гарантированное закрытие окна
                ImGui.End();
            }

            timer += deltaSecnds;
            if (timer >= 4f)
            {
                showing = false;

                // Без отписки каждый ап уровня навсегда добавляет ещё один вызов Draw на кадр
                // и держит этот объект живым: подписка на событие - сильная ссылка
                imguiModSystem.Draw -= Draw;
                imguiModSystem.Closed -= Close;
            }

            return CallbackGUIStatus.DontGrabMouse;
        }

        float smoothstep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        float invLerp(float time, float from, float to)
        {
            float a = Math.Clamp((time - from) / (to - from), 0f, 1f);
            return smoothstep(a);
        }

        float invLerp2(float time, float from0, float to0, float from1, float to1)
        {
            float a = Math.Min(Math.Clamp((time - from0) / (to0 - from0), 0f, 1f),
                          1f - Math.Clamp((time - from1) / (to1 - from1), 0f, 1f));
            return smoothstep(a);
        }

        private void Close() { }

    }
}