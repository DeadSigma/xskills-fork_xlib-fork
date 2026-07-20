using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using XLib.XLeveling;

namespace PandaXPDrops
{
    /// <summary>
    /// Помощник локализации для функции выпадения опыта (XP drops)
    /// </summary>
    /// <remarks>
    /// Каждый ключ находится в одном домене и с одним префиксом, поэтому записи можно объединить в общие
    /// языковые файлы xlibfork без конфликтов с собственными ключами XLib. <see cref="Domain"/> - единственное место, которое нужно
    /// изменить, если когда-либо изменится mod id
    /// </remarks>
    internal static class XpDropsLang
    {
        /// <summary>Домен ассетов, т.е. mod id сборки, в которую объединяются эти файлы</summary>
        public const string Domain = "xlibfork";

        /// <summary>Общий префикс для каждого ключа этой функции</summary>
        private const string Prefix = Domain + ":xpdrops-";

        /// <summary>Перевод ключа или сам ключ, если перевод отсутствует</summary>
        /// <param name="key">Ключ без домена и префикса, например <c>toggle-on</c></param>
        /// <param name="args">Необязательные аргументы форматирования</param>
        /// <returns>Переведенный текст</returns>
        public static string Get(string key, params object[] args) => Lang.Get(Prefix + key, args);

        /// <summary>Перевод ключа или <c>null</c>, если запись не существует</summary>
        /// <param name="key">Ключ без домена и префикса</param>
        /// <returns>Переведенный текст или <c>null</c></returns>
        public static string GetIfExists(string key) => Lang.GetIfExists(Prefix + key);
    }


    /// <summary>
    /// Клиентская точка входа плавающего HUD выпадения опыта
    /// </summary>
    /// <remarks>
    /// Правила, обеспечивающие безопасность внутри общей сборки <c>xlibfork</c>:
    /// <list type="bullet">
    ///   <item><description>Загружается только на <see cref="EnumAppSide.Client"/>. Сборка также загружается на
    ///   выделенных серверах, где код Cairo/GL никогда не должен выполняться</description></item>
    ///   <item><description>Имеет собственный Harmony id (<see cref="HarmonyId"/>), который должен отличаться
    ///   от id, с которым патчит сам XLib, поэтому отмена патчей никогда не затронет чужие патчи</description></item>
    ///   <item><description>Никогда не вызывает <c>PatchAll</c> и не содержит классов с аннотацией <c>[HarmonyPatch]</c>:
    ///   в противном случае вызов <c>PatchAll</c> для всей сборки из другой системы (например, спуфера mod id) подхватил бы
    ///   <see cref="XpGainPatch"/> под вторым id и выполнил бы постфикс дважды</description></item>
    /// </list>
    /// </remarks>
    public class PandaXPDropsSystem : ModSystem
    {
        /// <summary>Приватный Harmony id. Не должен конфликтовать с id, который использует сам XLib</summary>
        private const string HarmonyId = "pandaxpdrops";

        /// <summary>Файл конфигурации клиента внутри <c>VintagestoryData/ModConfig</c></summary>
        /// <remarks>Намеренно сохранено оригинальное имя - его переименование в тихом режиме сбросит настройки всех пользователей</remarks>
        private const string ConfigFile = "XLeveling/xLibGuiSettings.json";

        /// <summary>Код горячей клавиши для переключения отображения/скрытия</summary>
        private const string ToggleHotkeyCode = "xpdropstoggle";

        /// <summary>Клавиша по умолчанию для переключения отображения/скрытия</summary>
        private const GlKeys ToggleKey = GlKeys.F7;

        /// <summary>Клавиша по умолчанию для режима редактирования макета</summary>
        private const GlKeys EditModeKey = GlKeys.F6;

        /// <summary>Менеджер выпадений в реальном времени, или <c>null</c> вне мира</summary>
        public static XpDropManager DropManager { get; private set; }

        /// <summary>Загруженная конфигурация клиента, или <c>null</c> до выполнения <see cref="StartClientSide"/></summary>
        public static XpDropConfig Config { get; private set; }

        /// <summary>Логгер для статического кода патча, действителен между запуском и уничтожением (dispose)</summary>
        internal static ILogger Logger { get; private set; }

        private ICoreClientAPI capi;
        private Harmony harmony;
        private XpDropsHud hud;
        private XpDropsEditDialog editDialog;

        /// <summary>Необязательная связь с окном xSkills Gilded, <c>null</c>, если тот мод не установлен</summary>
        private GildedLayoutBridge gilded;

        /// <summary>Только для клиента - ничего из этого не должно загружаться на выделенном сервере</summary>
        /// <param name="forSide">Сторона, для которой загрузчик модов в данный момент выполняет загрузку</param>
        /// <returns><c>true</c> на стороне клиента</returns>
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        /// <summary>Загружает конфигурацию, создает менеджера, HUD и диалог редактирования, регистрирует обе горячие клавиши и перехватывает XLib</summary>
        /// <param name="api">Клиентское API</param>
        /// <remarks>
        /// Всё создается даже при выключенном отображении: горячая клавиша показа/скрытия должна продолжать работать,
        /// иначе скрытый HUD никогда нельзя было бы вернуть без ручного редактирования файла конфигурации
        /// </remarks>
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            Logger = api.Logger;
            Config = LoadConfig(api);

            DropManager = new XpDropManager(api, Config);
            hud = new XpDropsHud(api, DropManager);
            editDialog = new XpDropsEditDialog(api, DropManager, SaveConfig);
            gilded = GildedLayoutBridge.TryCreate(api);

            // Для компоновки HUD требуется загруженный мир и сущность игрока
            api.Event.LevelFinalize += hud.ComposeAndOpen;

            api.Input.RegisterHotKey(ToggleHotkeyCode, XpDropsLang.Get("hotkey-toggle"), ToggleKey, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler(ToggleHotkeyCode, ToggleDisplay);

            api.Input.RegisterHotKey(XpDropsEditDialog.HotkeyCode, XpDropsLang.Get("hotkey-editmode"), EditModeKey, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler(XpDropsEditDialog.HotkeyCode, ToggleEditMode);

            harmony = new Harmony(HarmonyId);
            try
            {
                if (XpGainPatch.Apply(harmony)) Logger.Notification("[PandaXPDrops] Experience hook applied.");
            }
            catch (Exception ex)
            {
                Logger.Error("[PandaXPDrops] Failed to apply the experience hook:\n{0}", ex);
            }
        }

        /// <summary>Точка входа, используемая <see cref="XpGainPatch"/> для каждого пакета опыта локального игрока</summary>
        /// <param name="skillId">ID навыка XLib, равный индексу в <c>PlayerSkillSet.PlayerSkills</c></param>
        /// <param name="skillName">Внутреннее (строчное) имя навыка, например <c>mining</c></param>
        /// <param name="xpAmount">Опыт, полученный с этим пакетом</param>
        /// <param name="progressFraction">Прогресс до следующего уровня, от 0 до 1</param>
        /// <param name="level">Текущий уровень навыка</param>
        public static void OnXpGained(int skillId, string skillName, float xpAmount, float progressFraction, int level)
        {
            DropManager?.AddDrop(skillId, skillName, xpAmount, progressFraction, level);
        }

        /// <summary>Показывает/скрывает весь HUD и сразу же сохраняет выбор</summary>
        /// <param name="comb">Комбинация клавиш, вызвавшая срабатывание горячей клавиши</param>
        /// <returns><c>true</c> - клавиша всегда поглощается</returns>
        private bool ToggleDisplay(KeyCombination comb)
        {
            if (Config == null) return false;

            Config.Enabled = !Config.Enabled;
            if (!Config.Enabled) DropManager?.Reset();

            SaveConfig();
            capi.ShowChatMessage(XpDropsLang.Get(Config.Enabled ? "toggle-on" : "toggle-off"));
            return true;
        }

        /// <summary>
        /// Распределяет горячую клавишу редактирования: пока окно xSkills Gilded открыто, оно забирает её себе,
        /// иначе правится макет выпадений опыта.
        /// </summary>
        /// <param name="comb">Комбинация клавиш, вызвавшая срабатывание горячей клавиши</param>
        /// <returns><c>true</c> - клавиша всегда поглощается</returns>
        /// <remarks>
        /// Gilded - окно ImGui: подвинуть себя может только оно само и только пока видимо. Когда оно закрыто -
        /// или мод вообще не установлен - для собственного редактора этого мода ничего не меняется.
        /// </remarks>
        private bool ToggleEditMode(KeyCombination comb)
        {
            if (gilded != null && gilded.IsWindowOpen && gilded.TryToggleLayoutEdit(HotkeyName(XpDropsEditDialog.HotkeyCode)))
            {
                return true;
            }

            if (editDialog == null) return false;

            if (editDialog.IsOpened()) editDialog.TryClose();
            else editDialog.TryOpen();

            return true;
        }

        /// <summary>Текущая привязка горячей клавиши, для подстановки в сообщения</summary>
        /// <param name="code">Код горячей клавиши</param>
        /// <returns>Комбинация клавиш текстом, либо сам код, если клавиша не зарегистрирована</returns>
        private string HotkeyName(string code)
            => capi?.Input.GetHotKeyByCode(code)?.CurrentMapping?.ToString() ?? code;

        /// <summary>Записывает текущую конфигурацию обратно на диск</summary>
        private void SaveConfig()
        {
            try
            {
                capi?.StoreModConfig(Config, ConfigFile);
            }
            catch (Exception ex)
            {
                Logger?.Error("[PandaXPDrops] Could not save {0}:\n{1}", ConfigFile, ex);
            }
        }

        /// <summary>Читает конфигурацию, возвращается к настройкам по умолчанию при отсутствующем/поврежденном файле и ограничивает значения</summary>
        /// <param name="api">Клиентское API</param>
        /// <returns>Никогда не равная null, очищенная конфигурация</returns>
        private static XpDropConfig LoadConfig(ICoreClientAPI api)
        {
            XpDropConfig config = null;
            try
            {
                config = api.LoadModConfig<XpDropConfig>(ConfigFile);
            }
            catch (Exception ex)
            {
                api.Logger.Error("[PandaXPDrops] Broken config, falling back to defaults:\n{0}", ex);
            }

            if (config == null)
            {
                config = new XpDropConfig();
                api.StoreModConfig(config, ConfigFile);
            }

            config.Sanitize();
            return config;
        }

        /// <summary>Удаляет перехватчик, оба диалога и каждую текстуру GPU. Выполняется при выходе из мира</summary>
        public override void Dispose()
        {
            gilded = null;

            editDialog?.TryClose();
            editDialog?.Dispose();
            editDialog = null;

            if (capi != null && hud != null) capi.Event.LevelFinalize -= hud.ComposeAndOpen;

            hud?.TryClose();
            hud?.Dispose();
            hud = null;

            DropManager?.Dispose();
            DropManager = null;
            Config = null;

            if (harmony != null) XpGainPatch.Remove(harmony);
            harmony = null;

            capi = null;
            Logger = null;
            base.Dispose();
        }
    }


    /// <summary>
    /// Клиентская конфигурация HUD выпадения опыта.
    /// </summary>
    /// <remarks>
    /// Все размеры и смещения немасштабированы - значения умножаются на <c>RuntimeEnv.GUIScale</c> при отрисовке,
    /// поэтому HUD сохраняет свои пропорции при любой настройке масштаба GUI.
    /// Значения макета предназначены для редактирования в игре (см. <see cref="XpDropsEditDialog"/>), а не вручную.
    /// </remarks>
    public class XpDropConfig
    {

        /// <summary>
        /// Отрисовывается ли HUD. Переключается в игре горячей клавишей показа/скрытия и сразу сохраняется;
        /// пока он выключен, мод остается загруженным, чтобы горячая клавиша продолжала работать.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Расстояние между правым краем экрана и правым краем полосы. Устанавливается перетаскиванием</summary>
        public float BarRightMargin { get; set; } = 270f;

        /// <summary>Расстояние между верхним краем экрана и верхним краем полосы. Устанавливается перетаскиванием</summary>
        public float BarTopMargin { get; set; } = 10f;

        /// <summary>Множитель размера всей полосы. Устанавливается колесиком мыши в режиме редактирования</summary>
        public float BarScale { get; set; } = 1f;

        /// <summary>Расстояние по вертикали между верхним краем полосы и линией появления меток (текста). Устанавливается перетаскиванием</summary>
        public float TextSpawnBelowBar { get; set; } = 245f;

        /// <summary>Горизонтальное смещение столбца меток относительно центра полосы. Устанавливается перетаскиванием</summary>
        public float TextSpawnOffsetX { get; set; }

        /// <summary>Множитель размера плавающих меток. Устанавливается колесиком мыши в режиме редактирования</summary>
        public float DropScale { get; set; } = 1f;

        /// <summary>Время в секундах, в течение которого полоса остается полностью видимой после последнего получения опыта</summary>
        public double BarIdleTimeout { get; set; } = 8.0;

        /// <summary>Время в секундах, необходимое для угасания полосы после истечения <see cref="BarIdleTimeout"/></summary>
        public double BarFadeDuration { get; set; } = 1.5;

        /// <summary>Общее время жизни одной плавающей метки в секундах</summary>
        public double DropLifetime { get; set; } = 3.0;

        /// <summary>Доля от <see cref="DropLifetime"/>, после которой метка начинает угасать, от 0 до 0.95</summary>
        public double FadeStartPct { get; set; } = 0.55;

        /// <summary>Секунды, в течение которых дальнейшие получения того же навыка суммируются в существующую метку</summary>
        public double AccumulationWindow { get; set; } = 1.0;

        /// <summary>Секунды, в течение которых постоянно поступающий опыт выживания собирается перед показом одной метки</summary>
        public double SurvivalBatchInterval { get; set; } = 300.0;

        /// <summary>Получение опыта ниже этого значения никогда не вызывает появление метки</summary>
        public float MinimumXp { get; set; } = 0.1f;

        /// <summary>Скорость подъема метки в немасштабируемых пикселях в секунду</summary>
        public float FloatSpeed { get; set; } = 35f;

        /// <summary>Вертикальный зазор между двумя метками, появившимися близко друг к другу</summary>
        public float DropSpacing { get; set; } = 4f;

        /// <summary>Базовый размер шрифта; текст на полосе использует 65%, а метки 85% от него</summary>
        public float FontSize { get; set; } = 20f;

        /// <summary>Минимальная длина края ячейки иконки предмета внутри полосы</summary>
        public float IconSize { get; set; } = 28f;

        /// <summary>Внутренний отступ панели полосы</summary>
        public float Padding { get; set; } = 8f;

        /// <summary>Высота самой полосы прогресса</summary>
        public float BarHeight { get; set; } = 10f;

        /// <summary>Зазор между иконкой, номерами уровней и полосой прогресса</summary>
        public float TextGap { get; set; } = 8f;

        /// <summary>Ширина полосы прогресса</summary>
        public float MinBarWidth { get; set; } = 180f;

        /// <summary>Нижняя граница для <see cref="BarScale"/> и <see cref="DropScale"/></summary>
        public const float MinElementScale = 0.4f;

        /// <summary>Верхняя граница для <see cref="BarScale"/> и <see cref="DropScale"/></summary>
        public const float MaxElementScale = 3f;

        /// <summary>Hint for players</summary>
        public string _hint_instruction { get; set; } = "Add skill names to the IgnoredSkills array to hide their XP gain notifications.";

        /// <summary>List of all available skills for reference</summary>
        public string[] _hint_available_skills { get; set; } = new string[]
        {
            "survival", "farming", "digging", "forestry", "mining",
            "husbandry", "combat", "metalworking", "pottery", "cooking",
            "fishing", "temporaladaptation"
        };

        /// <summary>Список навыков, для которых не будут показываться уведомления об опыте. Заполняется игроком вручную.</summary>
        public string[] IgnoredSkills { get; set; } = new string[0];

        /// <summary>
        /// Ограничивает значения, отредактированные вручную. Без этого <c>BarFadeDuration</c> равное 0 дает NaN альфу, а
        /// <c>FadeStartPct</c> равное 1 вызывает деление на ноль в <see cref="XpDrop.GetAlpha"/>.
        /// </summary>
        public void Sanitize()
        {
            BarScale = Math.Clamp(BarScale, MinElementScale, MaxElementScale);
            DropScale = Math.Clamp(DropScale, MinElementScale, MaxElementScale);
            BarIdleTimeout = Math.Max(0.0, BarIdleTimeout);
            BarFadeDuration = Math.Max(0.05, BarFadeDuration);
            DropLifetime = Math.Clamp(DropLifetime, 0.25, 120.0);
            FadeStartPct = Math.Clamp(FadeStartPct, 0.0, 0.95);
            AccumulationWindow = Math.Max(0.0, AccumulationWindow);
            SurvivalBatchInterval = Math.Max(1.0, SurvivalBatchInterval);
            MinimumXp = Math.Max(0f, MinimumXp);
            FloatSpeed = Math.Max(0f, FloatSpeed);
            DropSpacing = Math.Max(0f, DropSpacing);
            FontSize = Math.Clamp(FontSize, 6f, 96f);
            IconSize = Math.Clamp(IconSize, 8f, 128f);
            Padding = Math.Clamp(Padding, 0f, 64f);
            BarHeight = Math.Clamp(BarHeight, 2f, 64f);
            TextGap = Math.Clamp(TextGap, 0f, 64f);
            MinBarWidth = Math.Clamp(MinBarWidth, 20f, 1000f);

            // Защита списка игнорируемых навыков
            if (IgnoredSkills == null)
            {
                IgnoredSkills = new string[0];
            }
            else
            {
                for (int i = 0; i < IgnoredSkills.Length; i++)
                {
                    if (IgnoredSkills[i] != null)
                    {
                        IgnoredSkills[i] = IgnoredSkills[i].ToLowerInvariant().Trim();
                    }
                }
            }
        }
    }


    /// <summary>
    /// Постфикс для <c>XLevelingClient.MessageHandler(ExperiencePackage)</c> - пакета, который сервер отправляет
    /// владеющему клиенту при каждом получении опыта.
    /// </summary>
    /// <remarks>
    /// Намеренно <b>не</b> аннотировано <c>[HarmonyPatch]</c> и применяется вручную: внутри общей сборки
    /// xlibfork любой вызов <c>PatchAll(assembly)</c> из другой системы в противном случае подхватил бы этот класс
    /// под чужим Harmony id и выполнил бы постфикс второй раз.
    /// Цель и свойство разрешаются через <see cref="AccessTools"/>, поэтому непубличные члены XLib продолжают работать.
    /// </remarks>
    internal static class XpGainPatch
    {
        private static MethodBase target;
        private static PropertyInfo skillSetProp;
        private static bool patched;
        private static bool errorLogged;

        /// <summary>Применяет постфикс один раз. Безопасно вызывать повторно</summary>
        /// <param name="harmony">Экземпляр, созданный с собственным Harmony id этого мода</param>
        /// <returns><c>true</c>, если перехватчик активен</returns>
        internal static bool Apply(Harmony harmony)
        {
            if (patched) return true;

            target = AccessTools.Method(typeof(XLevelingClient), "MessageHandler", new[] { typeof(ExperiencePackage) });
            skillSetProp = AccessTools.Property(typeof(XLevelingClient), "LocalPlayerSkillSet");
            if (target == null || skillSetProp == null)
            {
                PandaXPDropsSystem.Logger?.Warning("[PandaXPDrops] XLevelingClient.MessageHandler(ExperiencePackage) / LocalPlayerSkillSet not found - XP drops stay off.");
                return false;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(XpGainPatch), nameof(Postfix))));
            patched = true;
            return true;
        }

        /// <summary>
        /// Удаляет только этот постфикс. Сброс <c>patched</c> важен: статическое состояние переживает систему модов,
        /// поэтому в противном случае перехватчик никогда бы не применился повторно при подключении ко второму миру в той же сессии.
        /// </summary>
        /// <param name="harmony">Тот же экземпляр, с которым был вызван <see cref="Apply"/></param>
        internal static void Remove(Harmony harmony)
        {
            if (patched && target != null) harmony.Unpatch(target, HarmonyPatchType.Postfix, harmony.Id);

            patched = false;
            errorLogged = false;
            target = null;
            skillSetProp = null;
        }

        /// <summary>Считывает состояние навыка, которое XLib только что обновил, и пересылает его в HUD</summary>
        /// <param name="__instance">Экземпляр <c>XLevelingClient</c></param>
        /// <param name="package">Пакет опыта. Имя параметра должно совпадать с оригинальным методом</param>
        private static void Postfix(object __instance, ExperiencePackage package)
        {
            try
            {
                if (__instance == null || skillSetProp == null) return;
                if (!(skillSetProp.GetValue(__instance) is PlayerSkillSet skillSet)) return;
                if (package.skillId < 0 || package.skillId >= skillSet.PlayerSkills.Count) return;

                PlayerSkill playerSkill = skillSet.PlayerSkills[package.skillId];
                var skill = playerSkill?.Skill;
                if (skill == null) return;

                float progress = playerSkill.RequiredExperience > 0f
                    ? playerSkill.Experience / playerSkill.RequiredExperience
                    : 0f;

                PandaXPDropsSystem.OnXpGained(package.skillId, skill.Name ?? "unknown", package.experience, progress, playerSkill.Level);
            }
            catch (Exception ex)
            {
                // Косметический HUD никогда не должен ломать обработчик пакетов XLib - но и не должен проглатывать ошибку молча
                if (!errorLogged)
                {
                    errorLogged = true;
                    PandaXPDropsSystem.Logger?.Error("[PandaXPDrops] XP hook failed (logged once):\n{0}", ex);
                }
            }
        }
    }
}