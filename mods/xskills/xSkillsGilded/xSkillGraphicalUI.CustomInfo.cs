using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Config;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{
    public partial class xSkillGraphicalUI
    {
        //Количество глав в инфо
        private int customInfoRowsCount = 3;


        private void DrawCustomInfoCenter(float abw, float abh)
        {
            float rowW = abw - _ui(65);
            // Высота строк
            float rowH = _ui(100);
            float startY = _ui(100);

            for (int i = 1; i <= customInfoRowsCount; i++)
            {
                float bx = _ui(16);
                float by = startY + (i - 1) * (rowH + _ui(8));

                Vector4 color = c_grey;
                string frameSpr = "abilitybox_frame_inactive";

                string title = Lang.GetUnformatted($"xskillgilded:custominfo-title-{i}");
                string shortDesc = Lang.GetUnformatted($"xskillgilded:custominfo-shortdesc-{i}");
                string details = Lang.GetUnformatted($"xskillgilded:custominfo-details-{i}");

                if (mouseHover(bx, by, bx + rowW, by + rowH))
                {
                    color = c_white;
                    frameSpr = "abilitybox_frame_active";
                    hoveringTooltip = new TooltipObject(title, details);
                }

                drawImage(Sprite("elements", "abilitybox_bg_info"), bx, by, rowW, rowH);
                drawImage9patch(Sprite("elements", "ability_shadow"), bx, by, rowW, rowH, 30);
                drawImage9patch(Sprite("elements", frameSpr), bx, by, rowW, rowH, 15);

                drawSetColor(color);

                // Масштаб заголовка
                ImGui.SetWindowFontScale(1.6f);
                drawTextFont(fTitleGold, title, bx + _ui(16), by + rowH / 2 - _ui(4), HALIGN.Left, VALIGN.Bottom);
                drawSetColor(c_white);

                // Масштаб описания
                ImGui.SetWindowFontScale(1.0f);
                drawTextFont(fSubtitle, shortDesc, bx + _ui(16), by + rowH / 2 + _ui(8), HALIGN.Left, VALIGN.Top);

                ImGui.SetWindowFontScale(1.0f);
            }
        }

        private void DrawCustomInfoLeft(float sdx, ref float sdy, float sdw)
        {
            string leftTitle = Lang.GetUnformatted("xskillgilded:custominfo-left-title");
            string leftDesc = Lang.GetUnformatted("xskillgilded:custominfo-left-desc");

            float customWidth = _ui(800);

            // Масштаб для левой панельки
            ImGui.SetWindowFontScale(1.4f);

            drawTextFont(fTitleGold, leftTitle, sdx, sdy);

            // Умножаем высоту базовой строки на 1.4f, чтобы следующий текст не налез на заголовок
            sdy += fTitleGold.getLineHeight() * 1.4f + _ui(8);

            var leftDescVTML = VTML.parseVTML(leftDesc);
            float h = drawTextVTML(leftDescVTML, sdx, sdy, customWidth);

            sdy += h + _ui(16);

            ImGui.SetWindowFontScale(1.0f);
        }
    }
}