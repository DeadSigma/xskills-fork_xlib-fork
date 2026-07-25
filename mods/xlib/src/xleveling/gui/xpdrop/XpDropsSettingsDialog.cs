using System;
using System.Globalization;
using Vintagestory.API.Client;

namespace PandaXPDrops
{
    /// <summary>
    /// Класс-двойник для конфигурации сумки. 
    /// Позволяет библиотеке xlib читать и сохранять настройки из файла сумки (xskills), не создавая прямую ссылку на саму модификацию.
    /// </summary>
    public class BagConfigProxy
    {
        /// <summary>Позиция слота по оси X на экране.</summary>
        public double X { get; set; }

        /// <summary>Позиция слота по оси Y на экране.</summary>
        public double Y { get; set; }

        /// <summary>Масштаб слота (временно не поддерживается движком для интерактивных слотов).</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>Включен ли визуальный интерфейс слота для сумки.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Скрывать ли слот с экрана, когда все меню и инвентари закрыты.</summary>
        public bool HideWhenInvClosed { get; set; } = false;
        /// <summary>Позволяет слотам всегда быть видными</summary>
        public bool AlwaysExpanded { get; set; } = false;

        /// <summary>Флаг, указывающий, была ли позиция изменена пользователем или используются значения по умолчанию.</summary>
        public bool HasValue { get; set; }
    }

    /// <summary>
    /// Диалоговое окно настроек для HUD выпадения опыта.
    /// Интегрирует как настройки самого опыта, так и визуальные настройки слота сумки охотника.
    /// </summary>
    public class XpDropsSettingsDialog : GuiDialog
    {
        private readonly XpDropConfig config;
        private readonly Action onSave;

        /// <summary>Прокси-конфигурация для хранения макета сумки</summary>
        private BagConfigProxy hbLayout;

        /// <summary>Массив ключей всех настроек, используемых для генерации элементов интерфейса</summary>
        private readonly string[] keys = new string[] {
            "Enabled", "BarRightMargin", "BarTopMargin", "BarScale", "MinBarWidth", "BarHeight", "Padding", "TextGap",
            "TextSpawnBelowBar", "TextSpawnOffsetX", "DropScale", "DropSpacing", "BarIdleTimeout", "BarFadeDuration", "DropLifetime", "FadeStartPct",
            "AccumulationWindow", "SurvivalBatchInterval", "MinimumXp", "FloatSpeed", "FontSize", "IconSize", "IgnoredSkills",
            "HunterBagEnabled", "HunterBagHideWhenClosed", "HunterBagAlwaysExpanded"
        };

        /// <summary>Код комбинации клавиш. Установлен в null.</summary>
        public override string ToggleKeyCombinationCode => null;

        /// <summary>Порядок отрисовки. Поверх режима редактирования.</summary>
        public override double DrawOrder => 0.98;

        /// <summary>
        /// Инициализирует окно настроек, загружая основной конфиг и конфиг сумки.
        /// </summary>
        public XpDropsSettingsDialog(ICoreClientAPI capi, XpDropConfig config, Action onSave) : base(capi)
        {
            this.config = config;
            this.onSave = onSave;

            try { hbLayout = capi.LoadModConfig<BagConfigProxy>("xskills/hunterbagslotlayout.json") ?? new BagConfigProxy(); }
            catch { hbLayout = new BagConfigProxy(); }

            SetupDialog();
        }

        /// <summary>
        /// Собирает графический интерфейс диалога настроек, размещая текстовые поля и переключатели по сетке.
        /// </summary>
        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds[] tb = new ElementBounds[keys.Length];
            ElementBounds[] ib = new ElementBounds[keys.Length];

            // Всего 25 ключей, разбиваем на 3 колонки по 9 строк
            for (int i = 0; i < keys.Length; i++)
            {
                int col = i / 9;
                int row = i % 9;

                tb[i] = ElementBounds.Fixed(col * 290, 40 + row * 40 + 5, 180, 30);
                ib[i] = ElementBounds.Fixed(col * 290 + 180, 40 + row * 40, 90, 30);
                bgBounds.WithChildren(tb[i], ib[i]);
            }

            ElementBounds resetBtnBounds = ElementBounds.Fixed(0, 440, 270, 30);
            ElementBounds saveBtnBounds = ElementBounds.Fixed(580, 440, 270, 30);
            bgBounds.WithChildren(resetBtnBounds, saveBtnBounds);

            dialogBounds.WithChild(bgBounds);

            var compo = capi.Gui.CreateCompo("xpdrops-settings", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(XpDropsLang.Get("settings-title"), () => TryClose());

            for (int i = 0; i < keys.Length; i++)
            {
                string localizedLabel = XpDropsLang.Get("setting-" + keys[i].ToLowerInvariant());
                compo.AddStaticText(localizedLabel, CairoFont.WhiteSmallText(), tb[i]);

                string hoverText = XpDropsLang.GetIfExists("setting-" + keys[i].ToLowerInvariant() + "-desc");

                if (!string.IsNullOrEmpty(hoverText))
                {
                    compo.AddHoverText(hoverText, CairoFont.WhiteDetailText(), 250, tb[i]);
                }

                if (keys[i] == "Enabled" || keys[i] == "HunterBagEnabled" || keys[i] == "HunterBagHideWhenClosed" || keys[i] == "HunterBagAlwaysExpanded")
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

            PopulateUI(this.config, this.hbLayout);
        }

        /// <summary>
        /// Заполняет элементы интерфейса текущими значениями из конфигураций.
        /// </summary>
        private void PopulateUI(XpDropConfig src, BagConfigProxy hbSrc)
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

            SingleComposer.GetSwitch("HunterBagEnabled").On = hbSrc.Enabled;
            SingleComposer.GetSwitch("HunterBagHideWhenClosed").On = hbSrc.HideWhenInvClosed;
            SingleComposer.GetSwitch("HunterBagAlwaysExpanded").On = hbSrc.AlwaysExpanded;
        }

        private void SetFieldValue(string key, float value) => SingleComposer.GetTextInput(key).SetValue(value.ToString("0.#####", CultureInfo.InvariantCulture));
        private void SetFieldValue(string key, double value) => SingleComposer.GetTextInput(key).SetValue(value.ToString("0.#####", CultureInfo.InvariantCulture));

        /// <summary>Заглушка для переключателей. Сохранение происходит только по нажатию кнопки.</summary>
        private void OnEnableDummy(bool on) { }

        /// <summary>
        /// Сбрасывает все настройки до значений по умолчанию и обновляет поля ввода.
        /// </summary>
        private bool OnResetClicked()
        {
            PopulateUI(new XpDropConfig(), new BagConfigProxy());
            return true;
        }

        /// <summary>
        /// Считывает значения из полей ввода, применяет их к конфигурациям и сохраняет на диск.
        /// Также вызывает обновление макетов в реальном времени.
        /// </summary>
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

            hbLayout.Enabled = SingleComposer.GetSwitch("HunterBagEnabled").On;
            hbLayout.HideWhenInvClosed = SingleComposer.GetSwitch("HunterBagHideWhenClosed").On;
            hbLayout.AlwaysExpanded = SingleComposer.GetSwitch("HunterBagAlwaysExpanded").On;

            capi.StoreModConfig(hbLayout, "xskills/hunterbagslotlayout.json");

            // Ищем открытое окно сумки (через рефлексию, чтобы xlib не зависел от xskills)
            // и заставляем его применить настройки сразу же.
            var bagDialog = capi.Gui.OpenedGuis.Find(dlg => dlg.GetType().Name.Contains("GuiDialogHunterBag"));
            if (bagDialog != null)
            {
                System.Reflection.MethodInfo reloadMethod = bagDialog.GetType().GetMethod("ReloadSettings");
                reloadMethod?.Invoke(bagDialog, null);
            }

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