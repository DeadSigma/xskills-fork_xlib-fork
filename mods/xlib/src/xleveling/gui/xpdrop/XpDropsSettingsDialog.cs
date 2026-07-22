using System;
using System.Globalization;
using Vintagestory.API.Client;

namespace PandaXPDrops
{
    /// <summary>
    /// Диалоговое окно настроек для HUD выпадения опыта.
    /// Позволяет изменять все параметры конфигурации.
    /// </summary>
    public class XpDropsSettingsDialog : GuiDialog
    {
        private readonly XpDropConfig config;
        private readonly Action onSave;

        // Ключи полей, которые соответствуют свойствам конфига. По ним же ищем переводы в Lang.
        private readonly string[] keys = new string[] {
            "Enabled", "BarRightMargin", "BarTopMargin", "BarScale", "MinBarWidth", "BarHeight", "Padding", "TextGap",
            "TextSpawnBelowBar", "TextSpawnOffsetX", "DropScale", "DropSpacing", "BarIdleTimeout", "BarFadeDuration", "DropLifetime", "FadeStartPct",
            "AccumulationWindow", "SurvivalBatchInterval", "MinimumXp", "FloatSpeed", "FontSize", "IconSize", "IgnoredSkills"
        };

        /// <summary>Код комбинации клавиш. Установлен в null.</summary>
        public override string ToggleKeyCombinationCode => null;

        /// <summary>Порядок отрисовки. Поверх режима редактирования.</summary>
        public override double DrawOrder => 0.98;

        /// <summary>Инициализирует окно настроек.</summary>
        public XpDropsSettingsDialog(ICoreClientAPI capi, XpDropConfig config, Action onSave) : base(capi)
        {
            this.config = config;
            this.onSave = onSave;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds[] tb = new ElementBounds[keys.Length];
            ElementBounds[] ib = new ElementBounds[keys.Length];

            for (int i = 0; i < keys.Length; i++)
            {
                int col = i / 8; // 3 колонки (0, 1, 2)
                int row = i % 8; // 8 строк в колонке

                tb[i] = ElementBounds.Fixed(col * 290, 40 + row * 40 + 5, 180, 30);
                ib[i] = ElementBounds.Fixed(col * 290 + 180, 40 + row * 40, 90, 30);
                bgBounds.WithChildren(tb[i], ib[i]);
            }

            ElementBounds resetBtnBounds = ElementBounds.Fixed(0, 400, 270, 30);

            ElementBounds saveBtnBounds = ElementBounds.Fixed(580, 400, 270, 30);
            bgBounds.WithChildren(resetBtnBounds, saveBtnBounds);

            dialogBounds.WithChild(bgBounds);

            var compo = capi.Gui.CreateCompo("xpdrops-settings", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(XpDropsLang.Get("settings-title"), () => TryClose());

            for (int i = 0; i < keys.Length; i++)
            {
                // Загружаем перевод названия из lang файла
                string localizedLabel = XpDropsLang.Get("setting-" + keys[i].ToLowerInvariant()); 
                compo.AddStaticText(localizedLabel, CairoFont.WhiteSmallText(), tb[i]);

                // Пытаемся получить описание
                string hoverText = XpDropsLang.GetIfExists("setting-" + keys[i].ToLowerInvariant() + "-desc"); 

                // Если перевод найден (не null и не пустой), добавляем зону наведения
                if (!string.IsNullOrEmpty(hoverText))
                {
                    // 250 - это максимальная ширина подсказки в пикселях до переноса текста на новую строку
                    compo.AddHoverText(hoverText, CairoFont.WhiteDetailText(), 250, tb[i]);
                }

                if (keys[i] == "Enabled")
                {
                    compo.AddSwitch(OnEnableDummy, ib[i].FlatCopy().WithFixedWidth(50), keys[i]);
                }
                else
                {
                    compo.AddTextInput(ib[i], null, CairoFont.WhiteSmallText(), keys[i]);
                }
            }

            compo.AddButton(XpDropsLang.Get("settings-btn-reset"), OnResetClicked, resetBtnBounds);
            compo.AddButton(XpDropsLang.Get("settings-btn-save"), OnSaveClicked, saveBtnBounds);
            SingleComposer = compo.Compose();

            // Заполняем интерфейс текущими значениями
            PopulateUI(this.config);
        }

        /// <summary>Заполняет текстовые поля значениями из переданного конфига.</summary>
        private void PopulateUI(XpDropConfig src)
        {
            SingleComposer.GetSwitch("Enabled").On = src.Enabled;
            SetFieldValue("BarRightMargin", src.BarRightMargin);
            SetFieldValue("BarTopMargin", src.BarTopMargin);
            SetFieldValue("BarScale", src.BarScale);
            SetFieldValue("MinBarWidth", src.MinBarWidth);
            SetFieldValue("BarHeight", src.BarHeight);
            SetFieldValue("Padding", src.Padding);
            SetFieldValue("TextGap", src.TextGap);
            SetFieldValue("TextSpawnBelowBar", src.TextSpawnBelowBar);
            SetFieldValue("TextSpawnOffsetX", src.TextSpawnOffsetX);
            SetFieldValue("DropScale", src.DropScale);
            SetFieldValue("DropSpacing", src.DropSpacing);
            SetFieldValue("BarIdleTimeout", src.BarIdleTimeout);
            SetFieldValue("BarFadeDuration", src.BarFadeDuration);
            SetFieldValue("DropLifetime", src.DropLifetime);
            SetFieldValue("FadeStartPct", src.FadeStartPct);
            SetFieldValue("AccumulationWindow", src.AccumulationWindow);
            SetFieldValue("SurvivalBatchInterval", src.SurvivalBatchInterval);
            SetFieldValue("MinimumXp", src.MinimumXp);
            SetFieldValue("FloatSpeed", src.FloatSpeed);
            SetFieldValue("FontSize", src.FontSize);
            SetFieldValue("IconSize", src.IconSize);

            string ignoredStr = src.IgnoredSkills != null ? string.Join(", ", src.IgnoredSkills) : "";
            SingleComposer.GetTextInput("IgnoredSkills").SetValue(ignoredStr);
        }

        private void SetFieldValue(string key, float value) => SingleComposer.GetTextInput(key).SetValue(value.ToString("0.#####", CultureInfo.InvariantCulture));
        private void SetFieldValue(string key, double value) => SingleComposer.GetTextInput(key).SetValue(value.ToString("0.#####", CultureInfo.InvariantCulture));

        private void OnEnableDummy(bool on) { /* Заглушка, сохранение пойдет через кнопку */ }

        private bool OnResetClicked()
        {
            // Берем абсолютно чистый конфиг со значениями по умолчанию и вставляем их в UI
            PopulateUI(new XpDropConfig());
            return true;
        }

        private bool OnSaveClicked()
        {
            config.Enabled = SingleComposer.GetSwitch("Enabled").On;

            config.BarRightMargin = ParseFloat("BarRightMargin", config.BarRightMargin);
            config.BarTopMargin = ParseFloat("BarTopMargin", config.BarTopMargin);
            config.BarScale = ParseFloat("BarScale", config.BarScale);
            config.MinBarWidth = ParseFloat("MinBarWidth", config.MinBarWidth);
            config.BarHeight = ParseFloat("BarHeight", config.BarHeight);
            config.Padding = ParseFloat("Padding", config.Padding);
            config.TextGap = ParseFloat("TextGap", config.TextGap);
            config.TextSpawnBelowBar = ParseFloat("TextSpawnBelowBar", config.TextSpawnBelowBar);
            config.TextSpawnOffsetX = ParseFloat("TextSpawnOffsetX", config.TextSpawnOffsetX);
            config.DropScale = ParseFloat("DropScale", config.DropScale);
            config.DropSpacing = ParseFloat("DropSpacing", config.DropSpacing);

            config.BarIdleTimeout = ParseDouble("BarIdleTimeout", config.BarIdleTimeout);
            config.BarFadeDuration = ParseDouble("BarFadeDuration", config.BarFadeDuration);
            config.DropLifetime = ParseDouble("DropLifetime", config.DropLifetime);
            config.FadeStartPct = ParseDouble("FadeStartPct", config.FadeStartPct);
            config.AccumulationWindow = ParseDouble("AccumulationWindow", config.AccumulationWindow);
            config.SurvivalBatchInterval = ParseDouble("SurvivalBatchInterval", config.SurvivalBatchInterval);

            config.MinimumXp = ParseFloat("MinimumXp", config.MinimumXp);
            config.FloatSpeed = ParseFloat("FloatSpeed", config.FloatSpeed);
            config.FontSize = ParseFloat("FontSize", config.FontSize);
            config.IconSize = ParseFloat("IconSize", config.IconSize);

            string ignored = SingleComposer.GetTextInput("IgnoredSkills").GetText();
            if (!string.IsNullOrWhiteSpace(ignored))
            {
                config.IgnoredSkills = ignored.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < config.IgnoredSkills.Length; i++)
                {
                    config.IgnoredSkills[i] = config.IgnoredSkills[i].Trim().ToLowerInvariant();
                }
            }
            else
            {
                config.IgnoredSkills = new string[0];
            }

            config.Sanitize();

            onSave?.Invoke();
            PandaXPDropsSystem.DropManager?.InvalidateTextures();

            TryClose();
            return true;
        }

        private float ParseFloat(string key, float fallback)
        {
            string text = SingleComposer.GetTextInput(key).GetText();
            if (float.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out float val))
                return val;
            return fallback;
        }

        private double ParseDouble(string key, double fallback)
        {
            string text = SingleComposer.GetTextInput(key).GetText();
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                return val;
            return fallback;
        }
    }
}