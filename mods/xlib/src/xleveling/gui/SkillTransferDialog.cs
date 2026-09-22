using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace XLib.XLeveling
{
    internal class SkillTransferDialog : GuiDialog
    {
        private readonly XLevelingClient client;
        private readonly PlayerSkill skill;

        private string targetName = "";
        private int transferLevels;
        private int transferXp;

        public override string ToggleKeyCombinationCode => null;
        public override bool UnregisterOnClose => true;

        internal SkillTransferDialog(XLevelingClient client, PlayerSkill skill)
            : base(client.XLeveling.Api as ICoreClientAPI)
        {
            this.client = client;
            this.skill = skill;
            ComposeDialog();
        }

        private void ComposeDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);

            ElementBounds skillBounds = ElementBounds.Fixed(0, 38, 360, 24);
            ElementBounds playerLabelBounds = ElementBounds.Fixed(0, 76, 120, 24);
            ElementBounds playerInputBounds = ElementBounds.Fixed(120, 72, 240, 30);
            ElementBounds levelsLabelBounds = ElementBounds.Fixed(0, 116, 120, 24);
            ElementBounds levelsInputBounds = ElementBounds.Fixed(120, 112, 120, 30);
            ElementBounds xpLabelBounds = ElementBounds.Fixed(0, 156, 120, 24);
            ElementBounds xpInputBounds = ElementBounds.Fixed(120, 152, 120, 30);
            ElementBounds limitsBounds = ElementBounds.Fixed(0, 194, 360, 22);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 222, 360, 42);
            ElementBounds cancelBounds = ElementBounds.Fixed(116, 274, 110, 28);
            ElementBounds confirmBounds = ElementBounds.Fixed(238, 274, 122, 28);

            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                skillBounds,
                playerLabelBounds,
                playerInputBounds,
                levelsLabelBounds,
                levelsInputBounds,
                xpLabelBounds,
                xpInputBounds,
                limitsBounds,
                statusBounds,
                cancelBounds,
                confirmBounds
            );

            SingleComposer = capi.Gui.CreateCompo("XLevelingSkillTransfer", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("xlib:transferexperience"), OnTitleBarCloseClicked)
                .AddStaticText(
                    Lang.Get("xlib:skill") + ": " + skill.Skill.DisplayName,
                    CairoFont.WhiteSmallishText(),
                    skillBounds)
                .AddStaticText(Lang.Get("xlib:transferplayer"), CairoFont.WhiteDetailText(), playerLabelBounds)
                .AddTextInput(playerInputBounds, OnTargetChanged, CairoFont.WhiteDetailText(), "TransferPlayer")
                .AddStaticText(Lang.Get("xlib:transferlevels"), CairoFont.WhiteDetailText(), levelsLabelBounds)
                .AddNumberInput(levelsInputBounds, OnLevelsChanged, CairoFont.WhiteDetailText(), "TransferLevels")
                .AddStaticText(Lang.Get("xlib:experience"), CairoFont.WhiteDetailText(), xpLabelBounds)
                .AddNumberInput(xpInputBounds, OnXpChanged, CairoFont.WhiteDetailText(), "TransferXp")
                .AddDynamicText("", CairoFont.WhiteDetailText(), limitsBounds, "TransferLimits")
                .AddDynamicText("", CairoFont.WhiteDetailText(), statusBounds, "TransferStatus")
                .AddSmallButton(Lang.Get("xlib:transfercancel"), OnCancel, cancelBounds)
                .AddSmallButton(Lang.Get("xlib:transferconfirm"), OnConfirm, confirmBounds)
                .Compose();

            GuiElementTextInput playerInput = SingleComposer.GetTextInput("TransferPlayer");
            playerInput.SetMaxLength(32);
            playerInput.SetPlaceHolderText(Lang.Get("xlib:transferplayerhint"));

            GuiElementNumberInput levelsInput = SingleComposer.GetNumberInput("TransferLevels");
            levelsInput.IntMode = true;
            levelsInput.Interval = 1f;
            levelsInput.SetValue(0f);

            GuiElementNumberInput xpInput = SingleComposer.GetNumberInput("TransferXp");
            xpInput.IntMode = true;
            xpInput.Interval = 1f;
            xpInput.SetValue(0f);

            UpdateLimits();
        }

        private void OnTargetChanged(string value)
        {
            targetName = value?.Trim() ?? "";
            SetStatus("");
        }

        private void OnLevelsChanged(string value)
        {
            transferLevels = ReadInt(value);
            SetStatus("");
            UpdateLimits();
        }

        private void OnXpChanged(string value)
        {
            transferXp = ReadInt(value);
            SetStatus("");
            UpdateLimits();
        }

        private void UpdateLimits()
        {
            int maxXp = Math.Max(0, (int)Math.Floor(skill.Experience));
            int xp = Math.Clamp(transferXp, 0, maxXp);
            int maxLevels = GetMaxLevels(xp);

            string text = Lang.Get("xlib:transferlimits", maxLevels, maxXp);

            SingleComposer?.GetDynamicText("TransferLimits")?.SetNewText(text);
        }

        private int GetMaxLevels(int xp)
        {
            int maxLevels = Math.Max(
                0,
                Math.Min(skill.Level - skill.Skill.MinLevel, skill.AbilityPoints)
            );

            float remainingXp = skill.Experience - Math.Clamp(xp, 0, Math.Max(0, (int)Math.Floor(skill.Experience)));

            while (maxLevels > 0 &&
                   remainingXp >= skill.Skill.GetRequiredExperience(skill.Level - maxLevels + 1))
            {
                maxLevels--;
            }

            return maxLevels;
        }

        private bool OnConfirm()
        {
            string target = SingleComposer.GetTextInput("TransferPlayer")?.GetText()?.Trim() ?? targetName;
            int levels = Math.Max(0, (int)Math.Floor(SingleComposer.GetNumberInput("TransferLevels")?.GetValue() ?? 0f));
            int xp = Math.Max(0, (int)Math.Floor(SingleComposer.GetNumberInput("TransferXp")?.GetValue() ?? 0f));

            if (string.IsNullOrWhiteSpace(target))
            {
                SetStatus(Lang.Get("xlib:transfererrorplayer"));
                return true;
            }

            if (string.Equals(target, capi.World.Player?.PlayerName, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus(Lang.Get("xlib:transfererrorself"));
                return true;
            }

            int maxXp = Math.Max(0, (int)Math.Floor(skill.Experience));
            if (xp > maxXp)
            {
                SetStatus(Lang.Get("xlib:transfererrorxp"));
                return true;
            }

            int maxLevels = GetMaxLevels(xp);
            if (levels > maxLevels)
            {
                SetStatus(Lang.Get("xlib:transfererrorlevels"));
                return true;
            }

            if (levels == 0 && xp == 0)
            {
                SetStatus(Lang.Get("xlib:transfererrorempty"));
                return true;
            }

            client.SendPackage(new ExperienceTransferPackage(skill.Skill.Id, target, levels, xp));
            TryClose();
            return true;
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }

        private void SetStatus(string text)
        {
            SingleComposer?.GetDynamicText("TransferStatus")?.SetNewText(text ?? "");
        }

        private static int ReadInt(string value)
        {
            if (!float.TryParse(value, out float parsed)) return 0;
            return Math.Max(0, (int)Math.Floor(parsed));
        }

    }
}
