using ImGuiNET;
using System;
using System.Numerics;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded
{
    public partial class xSkillGraphicalUI
    {
        static readonly Vector4 c_fieldHover = hexToVec4("4a3726");
        static readonly Vector4 c_brown = hexToVec4("684c3c");

        PlayerSkill transferSkill;
        string transferName = "";
        int transferLevels = 0;
        int transferXp = 0;
        int transferOpenFrame = -1;
        bool transferTyping = false;

        bool transferOpen => transferSkill != null;

        private void OpenTransfer(PlayerSkill skill)
        {
            // ник между открытиями сохраняется
            transferSkill = skill;
            transferLevels = 0;
            transferXp = 0;
            transferOpenFrame = ImGui.GetFrameCount();

            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/pagesub.ogg"), false, .3f);
        }

        private void CloseTransfer(bool playSound = true)
        {
            if (!transferOpen) return;

            transferSkill = null;
            transferTyping = false;

            if (playSound) api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/pagesub.ogg"), false, .3f);
        }

        private void ConfirmTransfer(PlayerSkill skill, string target)
        {
            XLevelingClient client = XLeveling.Instance(api)?.IXLevelingAPI as XLevelingClient;
            if (client == null || client.TransferCooldownRemaining > 0f) return;

            client.SendPackage(new ExperienceTransferPackage(skill.Skill.Id, target, transferLevels, transferXp));

            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/upgraded.ogg"), false, .3f);
            CloseTransfer(false);
        }

        // по клику на название ветки в meta spec открывается окно передачи
        private bool DrawTransferHover(PlayerSkill skill, float x, float y, float w, ref string hoverId)
        {
            float h = TextSize(fSubtitleGold, skill.Skill.DisplayName).Y;
            if (!mouseHover(x - _ui(4), y - _ui(2), x + w, y + h + _ui(2))) return false;

            string id = "_Transfer:" + skill.Skill.Name;
            if (hoveringID != id) api.Gui.PlaySound("tick", false, .5f);
            hoverId = id;

            drawSetColor(c_gold, .25f);
            drawImage9patch(Sprite("elements", "glow"), x - _ui(16), y - _ui(12), w + _ui(24), h + _ui(24), 15);
            drawSetColor(c_white);

            hoveringTooltip = new(Lang.Get("xskillgilded:transferTitle"), Lang.Get("xskillgilded:transferHint"));

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) OpenTransfer(skill);
            return true;
        }

        private void DrawTransferPopup(float windowWidth, float windowHeight)
        {
            if (!transferOpen) return;

            const float boxScale = 2f;      // размер окна относительно первой версии
            const float itemScale = 1.2f;   // размер текста, полей и кнопок

            PlayerSkill skill = transferSkill;
            XLevelingClient xlevelingClient = XLeveling.Instance(api)?.IXLevelingAPI as XLevelingClient;
            int cooldownSeconds = (int)Math.Ceiling(xlevelingClient?.TransferCooldownRemaining ?? 0f);

            // в кадре открытия клики не принимаются - тот же клик мог попасть в кнопку окна
            mouseBlocked = ImGui.GetFrameCount() == transferOpenFrame;

            int maxXp = Math.Max(0, (int)Math.Floor(skill.Experience));
            transferXp = Math.Clamp(transferXp, 0, maxXp);

            // Уровни с занятыми очками способностей не отдаются
            int maxLevels = Math.Max(0, Math.Min(skill.Level - skill.Skill.MinLevel, skill.AbilityPoints));
            float remainingXp = skill.Experience - transferXp;
            while (maxLevels > 0 && remainingXp >= skill.Skill.GetRequiredExperience(skill.Level - maxLevels + 1))
            {
                maxLevels--;
            }
            transferLevels = Math.Clamp(transferLevels, 0, maxLevels);

            string lTitle = Lang.Get("xskillgilded:transferTitle");
            string lPlayer = Lang.Get("xskillgilded:transferPlayer");
            string lLevels = Lang.Get("xskillgilded:transferLevels");
            string lXp = Lang.Get("xskillgilded:transferXp");
            string lConfirm = Lang.Get("xskillgilded:transferConfirm");
            string lCancel = Lang.Get("xskillgilded:transferCancel");

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
                 | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

            ImGui.PushStyleColor(ImGuiCol.FrameBg, c_dkgrey);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, c_fieldHover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, c_fieldHover);
            ImGui.PushStyleColor(ImGuiCol.Border, c_brown);
            ImGui.PushStyleColor(ImGuiCol.Text, c_white);
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, c_grey);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(c_gold.X, c_gold.Y, c_gold.Z, .35f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(_ui(8 * itemScale), _ui(5 * itemScale)));

            // scarab масштабируется через baseScale и после окна возвращается обратно, ImGui-шрифт - через SetWindowFontScale
            float sTitle = fTitleGold.baseScale;
            float sSubGold = fSubtitleGold.baseScale;
            float sSub = fSubtitle.baseScale;
            if (!useInternalTextDrawer)
            {
                fTitleGold.baseScale = sTitle * itemScale;
                fSubtitleGold.baseScale = sSubGold * itemScale;
                fSubtitle.baseScale = sSub * itemScale;
            }

            // свой child после "Ability" - иначе иконки перков рисуются поверх окна
            ImGui.SetCursorPos(new(0, 0));
            ImGui.BeginChild("TransferPopup", new(windowWidth, windowHeight), false, flags);
            try
            {
                ImGui.SetWindowFontScale(itemScale);

                windowPosX = ImGui.GetWindowPos().X;
                windowPosY = ImGui.GetWindowPos().Y;

                float rowH = ImGui.GetFrameHeight();
                float titleH = TextSize(fTitleGold, lTitle).Y;
                float labelW = Math.Max(TextSize(fSubtitle, lPlayer).X, Math.Max(TextSize(fSubtitle, lLevels).X, TextSize(fSubtitle, lXp).X)) + _ui(24 * itemScale);

                float pad = _ui(24) * boxScale;
                float pw = _ui(460) * boxScale;

                // высота берётся от прежней компоновки, но не опускается ниже нужной элементам
                float ph0 = _ui(24) * 2 + titleH / itemScale + _ui(18) + (rowH / itemScale + _ui(12)) * 4;
                float phMin = pad * 2 + titleH + _ui(6 * itemScale) + rowH * 5 + _ui(12 * itemScale) * 6;
                float ph = Math.Max(ph0 * boxScale, phMin);

                float px = (float)Math.Round((windowWidth - pw) / 2);
                float py = (float)Math.Round((windowHeight - ph) / 2);

                // меню под окном затемняется
                drawSetColor(new Vector4(0, 0, 0, .55f));
                drawImage(Sprite("elements", "pixel"), 0, 0, windowWidth, windowHeight);
                drawSetColor(c_white);

                drawImage(Sprite("elements", "bg"), px, py, pw, ph);

                float x = px + pad;
                float y = py + pad;
                float w = pw - pad * 2;
                float cx = x + labelW;

                drawTextFont(fTitleGold, lTitle, x, y + titleH, HALIGN.Left, VALIGN.Bottom);
                drawTextFont(fSubtitleGold, skill.Skill.DisplayName, x + w, y + titleH, HALIGN.Right, VALIGN.Bottom);
                y += titleH + _ui(6 * itemScale);

                drawSetColor(c_brown);
                drawImage(Sprite("elements", "tooltip_sep"), x, y, w, 1);
                drawSetColor(c_white);

                float by = py + ph - pad - rowH;
                float unit = (by - y - rowH * 4) / 6;
                y += unit;

                DrawTransferLabel(lPlayer, x, y, rowH);
                ImGui.SetCursorPos(new(cx, y));
                ImGui.SetNextItemWidth(x + w - cx);
                ImGui.InputTextWithHint("##xsgTransferName", Lang.Get("xskillgilded:transferPlayerHint"), ref transferName, 32, ImGuiInputTextFlags.CharsNoBlank);
                bool typing = ImGui.IsItemActive();
                y += rowH + unit;

                DrawTransferLabel(lLevels, x, y, rowH);
                float valW = _ui(48 * itemScale);
                if (DrawTransferButton("-", cx, y, rowH, rowH, transferLevels > 0))
                {
                    transferLevels--;
                    api.Gui.PlaySound("tick", false, .5f);
                }
                drawTextFont(fSubtitle, transferLevels.ToString(), cx + rowH + valW / 2, y + rowH / 2, HALIGN.Center, VALIGN.Center);
                if (DrawTransferButton("+", cx + rowH + valW, y, rowH, rowH, transferLevels < maxLevels))
                {
                    transferLevels++;
                    api.Gui.PlaySound("tick", false, .5f);
                }
                drawSetColor(c_grey);
                drawTextFont(fSubtitle, "Lv. " + skill.Level, x + w, y + rowH / 2, HALIGN.Right, VALIGN.Center);
                drawSetColor(c_white);
                y += rowH + unit;

                DrawTransferLabel(lXp, x, y, rowH);
                float xpW = _ui(120 * itemScale);
                ImGui.SetCursorPos(new(cx, y));
                ImGui.SetNextItemWidth(xpW);
                ImGui.InputInt("##xsgTransferXp", ref transferXp, 0, 0);
                typing |= ImGui.IsItemActive();
                transferXp = Math.Clamp(transferXp, 0, maxXp);
                drawSetColor(c_grey);
                drawTextFont(fSubtitle, "/ " + maxXp + " xp", cx + xpW + _ui(12 * itemScale), y + rowH / 2, HALIGN.Left, VALIGN.Center);
                drawSetColor(c_white);
                y += rowH + unit;

                string cooldownText = cooldownSeconds > 0
                    ? Lang.Get("xlib:transfercooldownremaining", FormatTransferCooldown(cooldownSeconds))
                    : Lang.Get("xlib:transfercooldownready");
                drawSetColor(cooldownSeconds > 0 ? c_grey : c_white);
                drawTextFont(fSubtitle, cooldownText, x + w / 2, y + rowH / 2, HALIGN.Center, VALIGN.Center);
                drawSetColor(c_white);

                transferTyping = typing;

                string target = transferName.Trim();
                bool canConfirm = cooldownSeconds <= 0
                    && target.Length > 0
                    && !target.Equals(api.World.Player.PlayerName, StringComparison.OrdinalIgnoreCase)
                    && (transferLevels > 0 || transferXp > 0);

                float confirmW = Math.Max(_ui(120 * itemScale), TextSize(fSubtitle, lConfirm).X + _ui(32 * itemScale));
                float cancelW = Math.Max(_ui(120 * itemScale), TextSize(fSubtitle, lCancel).X + _ui(32 * itemScale));
                float bx = x + w - confirmW;

                bool confirm = DrawTransferButton(lConfirm, bx, by, confirmW, rowH, canConfirm);
                bool cancel = DrawTransferButton(lCancel, bx - _ui(12 * itemScale) - cancelW, by, cancelW, rowH, true);

                drawImage9patch(Sprite("elements", "frame"), px, py, pw, ph, 60);

                if (confirm) ConfirmTransfer(skill, target);
                else if (cancel) CloseTransfer();
            }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor(7);
                fTitleGold.baseScale = sTitle;
                fSubtitleGold.baseScale = sSubGold;
                fSubtitle.baseScale = sSub;
                windowPosX = windowX;
                windowPosY = windowY;
            }
        }

        private static string FormatTransferCooldown(int totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            int hours = (int)time.TotalHours;
            return $"{hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        private void DrawTransferLabel(string text, float x, float y, float rowH)
        {
            drawSetColor(c_grey);
            drawTextFont(fSubtitle, text, x, y + rowH / 2, HALIGN.Left, VALIGN.Center);
            drawSetColor(c_white);
        }

        // неактивная кнопка рисуется полупрозрачной
        private bool DrawTransferButton(string text, float x, float y, float w, float h, bool enabled)
        {
            bool hover = enabled && mouseHover(x, y, x + w, y + h);

            drawSetColor(c_white, enabled ? 1f : .35f);
            drawImage9patch(Sprite("elements", "button_idle"), x, y, w, h, 2);

            if (hover)
            {
                drawImage9patch(Sprite("elements", "button_idle_hovering"), x - 1, y - 1, w + 2, h + 2, 2);
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    drawImage9patch(Sprite("elements", "button_pressing"), x, y, w, h, 2);
            }

            drawTextFont(fSubtitle, text, x + w / 2, y + h / 2, HALIGN.Center, VALIGN.Center);
            drawSetColor(c_white);

            return hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        }

        // размер текста меряется так же, как в drawTextFont
        private static Vector2 TextSize(Font font, string text)
        {
            return useInternalTextDrawer ? ImGui.CalcTextSize(text) : font.CalcTextSize(text);
        }
    }
}