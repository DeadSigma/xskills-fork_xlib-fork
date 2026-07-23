using System;
using System.Collections.Generic;
using System.Globalization;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PandaXPDrops
{
    /// <summary>Выровненный по осям прямоугольник в реальных пикселях экрана</summary>
    public readonly struct GuiRect
    {
        /// <summary>Левый край</summary>
        public readonly double X;

        /// <summary>Верхний край</summary>
        public readonly double Y;

        /// <summary>Ширина</summary>
        public readonly double W;

        /// <summary>Высота</summary>
        public readonly double H;

        /// <summary>Создает прямоугольник</summary>
        /// <param name="x">Левый край</param>
        /// <param name="y">Верхний край</param>
        /// <param name="w">Ширина</param>
        /// <param name="h">Высота</param>
        public GuiRect(double x, double y, double w, double h)
        {
            X = x; Y = y; W = w; H = h;
        }

        /// <summary>Центр по горизонтали</summary>
        public double CenterX => X + W / 2.0;

        /// <summary>Находится ли точка внутри</summary>
        /// <param name="px">Координата X точки</param>
        /// <param name="py">Координата Y точки</param>
        /// <returns><c>true</c>, если точка находится внутри</returns>
        public bool Contains(double px, double py) => px >= X && px <= X + W && py >= Y && py <= Y + H;
    }


    /// <summary>Одна плавающая метка "+X.X" со своей собственной запеченной текстурой Cairo</summary>
    public class XpDrop
    {
        /// <summary>ID навыка XLib, которому принадлежит эта метка; используется для объединения последующих получений того же навыка</summary>
        public int SkillId;

        /// <summary>Внутреннее (строчное) имя навыка</summary>
        public string SkillName;

        /// <summary>Опыт, отображаемый на метке, суммируется, пока находится в окне накопления</summary>
        public float XpAmount;

        /// <summary>Секунды с момента появления метки</summary>
        public double Age;

        /// <summary>
        /// Немасштабированные пиксели, на которые эта метка начинается <b>ниже</b> линии появления, чтобы метки, созданные в одном тике
        /// (например, бой + животноводство с одного убийства), не отрисовывались друг поверх друга
        /// </summary>
        public float SpawnOffsetY;

        /// <summary>Устанавливается всякий раз, когда изменяется текст, масштаб или масштаб GUI, и <see cref="Texture"/> необходимо перестроить</summary>
        public bool TextureDirty = true;

        /// <summary>Запеченная текстура метки, принадлежащая этому экземпляру</summary>
        public LoadedTexture Texture;

        /// <summary>Пережила ли метка свое настроенное время жизни</summary>
        /// <param name="lifetime">Настроенное время жизни в секундах</param>
        /// <returns><c>true</c>, если метку можно удалить</returns>
        public bool IsExpired(double lifetime) => Age >= lifetime;

        /// <summary>Непрозрачность для текущего возраста: полностью непрозрачная, затем линейное затухание до нуля</summary>
        /// <param name="lifetime">Настроенное время жизни в секундах</param>
        /// <param name="fadeStartPct">Доля времени жизни, после которой начинается затухание</param>
        /// <returns>Альфа от 0 до 1</returns>
        public float GetAlpha(double lifetime, double fadeStartPct)
        {
            if (Age >= lifetime) return 0f;

            double fadeStart = lifetime * fadeStartPct;
            // второе условие защищает от деления на ноль в конфигурациях, отредактированных вручную
            if (Age < fadeStart || lifetime <= fadeStart) return 1f;

            return 1f - (float)((Age - fadeStart) / (lifetime - fadeStart));
        }

        /// <summary>Освобождает текстуру GPU</summary>
        public void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }


    /// <summary>
    /// Статические данные отображения для каждого навыка: иконка предмета/блока и цвет акцента. Отображаемые имена берутся из языковых файлов
    /// </summary>
    /// <remarks>
    /// Неизвестные навыки (из других модов на базе XLib) не являются ошибкой: они возвращаются к серой буквенной иконке и
    /// имени с заглавной буквы, поэтому для работы здесь ничего не нужно регистрировать
    /// </remarks>
    public static class SkillVisuals
    {
        /// <summary>Данные отображения одного известного навыка</summary>
        private readonly struct Entry
        {
            /// <summary>Компоненты цвета акцента от 0 до 1, используются для резервной буквенной иконки</summary>
            public readonly double R, G, B;

            /// <summary>Код предмета или блока, отрисовываемый как иконка полосы</summary>
            public readonly string ItemCode;

            /// <summary>Создает запись отображения</summary>
            /// <param name="r">Красный 0..1</param>
            /// <param name="g">Зеленый 0..1</param>
            /// <param name="b">Синий 0..1</param>
            /// <param name="itemCode">Код предмета или блока для иконки</param>
            public Entry(double r, double g, double b, string itemCode)
            {
                R = r; G = g; B = b;
                ItemCode = itemCode;
            }
        }

        /// <summary>Известные навыки XSkills. Коды предметов разрешаются лениво, неразрешимые деградируют до буквы</summary>
        private static readonly Dictionary<string, Entry> Skills = new Dictionary<string, Entry>
        {
            { "survival",           new Entry(0.85, 0.25, 0.25, "game:firestarter") },
            { "farming",            new Entry(0.30, 0.75, 0.20, "game:seeds-turnip") },
            { "digging",            new Entry(0.60, 0.40, 0.20, "game:shovel-copper") },
            { "forestry",           new Entry(0.15, 0.55, 0.15, "game:sapling-kapok-free") },
            { "mining",             new Entry(0.55, 0.55, 0.60, "game:pickaxe-copper") },
            { "husbandry",          new Entry(0.85, 0.75, 0.25, "game:hide-pelt-fox-red") },
            { "combat",             new Entry(0.70, 0.15, 0.15, "game:blade-falx-copper") },
            { "metalworking",       new Entry(0.75, 0.78, 0.82, "game:anvil-copper") },
            { "pottery",            new Entry(0.78, 0.45, 0.25, "game:bowl-blue-raw") },
            { "cooking",            new Entry(0.90, 0.55, 0.15, "game:claypot-blue-fired") },
            { "fishing",            new Entry(0.55, 0.30, 0.75, "game:creature-fish-reef-wrasse-creole-adult") },
            { "temporaladaptation", new Entry(0.55, 0.30, 0.75, "game:gear-temporal") }
        };

        /// <summary>Нормализует имя навыка в ключ словаря</summary>
        /// <param name="skillName">Имя навыка, может быть <c>null</c></param>
        /// <returns>Ключ в нижнем регистре, никогда не <c>null</c></returns>
        private static string Key(string skillName) => skillName?.ToLowerInvariant() ?? "";

        /// <summary>Цвет акцента навыка</summary>
        /// <param name="skillName">Имя навыка</param>
        /// <returns>RGB от 0 до 1, серый для неизвестных навыков</returns>
        public static (double r, double g, double b) GetColor(string skillName)
            => Skills.TryGetValue(Key(skillName), out Entry e) ? (e.R, e.G, e.B) : (0.5, 0.5, 0.5);

        /// <summary>Код предмета или блока, используемый как иконка полосы</summary>
        /// <param name="skillName">Имя навыка</param>
        /// <returns>Код или <c>null</c> для неизвестных навыков</returns>
        public static string GetItemCode(string skillName)
            => Skills.TryGetValue(Key(skillName), out Entry e) ? e.ItemCode : null;

        /// <summary>Первая буква, рисуется, когда не удалось разрешить иконку предмета</summary>
        /// <param name="skillName">Имя навыка</param>
        /// <returns>Один символ в верхнем регистре или "?"</returns>
        public static string GetIconLetter(string skillName)
            => string.IsNullOrEmpty(skillName) ? "?" : skillName.Substring(0, 1).ToUpperInvariant();

        /// <summary>Имя, отображаемое над полосой прогресса</summary>
        /// <param name="skillName">Имя навыка</param>
        /// <returns>
        /// Перевод <c>xpdrops-skill-&lt;name&gt;</c> или внутреннее имя с заглавной буквы, если запись не
        /// существует - именно это получают навыки других модов XLib
        /// </returns>
        public static string GetDisplayName(string skillName)
        {
            string key = Key(skillName);
            if (key.Length == 0) return "?";

            string translated = XpDropsLang.GetIfExists("skill-" + key);
            if (!string.IsNullOrEmpty(translated)) return translated;

            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }
    }


    /// <summary>
    /// Владеет всем состоянием HUD: полосой навыка, плавающими метками, их текстурами Cairo и макетом экрана
    /// </summary>
    /// <remarks>
    /// Получает тики и отрисовывается из <see cref="XpDropsHud.OnRenderGUI"/>, т.е. только в главном потоке и на клиенте
    /// Здесь нет ничего потокобезопасного, и ничто не должно запускаться на выделенном сервере
    /// <see cref="GetBarRect"/> и <see cref="GetDropSpawnRect"/> являются единственным источником истины для макета:
    /// HUD рисует по ним, а <see cref="XpDropsEditDialog"/> проверяет попадания и перетаскивает по ним
    /// </remarks>
    public class XpDropManager : IDisposable
    {
        /// <summary>Имя навыка, который получает опыт непрерывно и поэтому группируется (batched)</summary>
        private const string SurvivalSkill = "survival";

        /// <summary>Навык, отображаемый в предпросмотре режима редактирования</summary>
        private const string PreviewSkill = "mining";

        /// <summary>ID навыка меток предпросмотра. Отрицательный, поэтому он никогда не сможет объединиться с реальным получением</summary>
        private const int PreviewSkillId = -1;

        /// <summary>Динамический шрифт</summary>
        private static string FontFace => GuiStyle.StandardFontName;

        private readonly ICoreClientAPI capi;
        private readonly XpDropConfig config;
        private readonly List<XpDrop> activeDrops = new List<XpDrop>();
        private readonly Dictionary<string, DummySlot> skillSlots = new Dictionary<string, DummySlot>();

        // группировка выживания
        private float survivalAccumulated;
        private float survivalLastProgress;
        private int survivalLastLevel;
        private int survivalSkillId = -1;
        private double survivalTimer;

        // состояние полосы
        private string barSkillName;
        private float barProgress;
        private int barLevel;
        private double barIdleTimer;
        private bool barVisible;
        private bool barTextureDirty;
        private LoadedTexture barTexture;
        private float lastGuiScale = -1f;

        /// <summary>Конфигурация, с которой был создан этот менеджер</summary>
        public XpDropConfig Config => config;

        /// <summary>Метки, находящиеся в данный момент на экране, отсортированы от старых к новым</summary>
        public IReadOnlyList<XpDrop> ActiveDrops => activeDrops;

        /// <summary>
        /// Пока установлено, полоса никогда не затухает, а фиктивная метка продолжает появляться, поэтому элементы можно
        /// позиционировать, хотя обычно они видны только сразу после получения опыта
        /// </summary>
        public bool EditPreview { get; set; }

        /// <summary>Непрозрачность всей полосы, 0..1</summary>
        public float BarAlpha { get; private set; }

        /// <summary>Навык, который сейчас показывает полоса, или <c>null</c> до первого получения</summary>
        public string BarSkillName => barSkillName;

        /// <summary>ID GL запеченной текстуры полосы, 0 пока ее нет</summary>
        public int BarTextureId => barTexture?.TextureId ?? 0;

        /// <summary>Ширина запеченной текстуры полосы в реальных пикселях</summary>
        public int BarTextureWidth => barTexture?.Width ?? 0;

        /// <summary>Высота запеченной текстуры полосы в реальных пикселях</summary>
        public int BarTextureHeight => barTexture?.Height ?? 0;

        /// <summary>Центр иконки внутри текстуры полосы по оси X. Добавьте к этому позицию полосы на экране</summary>
        public double BarIconCenterX { get; private set; }

        /// <summary>Центр иконки внутри текстуры полосы по оси Y. Добавьте к этому позицию полосы на экране</summary>
        public double BarIconCenterY { get; private set; }

        /// <summary>Длина края, с которой отрисовывается иконка стака предметов, уже масштабированная</summary>
        public float BarIconRenderSize { get; private set; }

        /// <summary>Создает менеджера</summary>
        /// <param name="capi">Клиентское API</param>
        /// <param name="config">Очищенная конфигурация</param>
        public XpDropManager(ICoreClientAPI capi, XpDropConfig config)
        {
            this.capi = capi;
            this.config = config;
        }

        /// <summary>
        /// Обрабатывает одно получение опыта: обновляет полосу и либо объединяет с недавней меткой, либо создает новую.
        /// Получения выживания здесь только накапливаются, см. <see cref="UpdateSurvivalBatch"/>
        /// </summary>
        /// <param name="skillId">ID навыка XLib</param>
        /// <param name="skillName">Внутреннее имя навыка</param>
        /// <param name="xpAmount">Полученный опыт, игнорируется, если равен нулю или отрицательный</param>
        /// <param name="progressFraction">Прогресс до следующего уровня, 0..1</param>
        /// <param name="level">Текущий уровень</param>
        public void AddDrop(int skillId, string skillName, float xpAmount, float progressFraction, int level)
        {
            if (!config.Enabled || xpAmount <= 0f) return;

            // Проверка: находится ли навык в списке игнорируемых
            if (skillName != null && config.IgnoredSkills != null)
            {
                string searchName = skillName.ToLowerInvariant();
                for (int i = 0; i < config.IgnoredSkills.Length; i++)
                {
                    if (config.IgnoredSkills[i] == searchName) return;
                }
            }

            // Выживание поступает постоянно - группируем его, чтобы не спамить экран
            if (string.Equals(skillName, SurvivalSkill, StringComparison.OrdinalIgnoreCase))
            {
                survivalSkillId = skillId;
                survivalAccumulated += xpAmount;
                survivalLastProgress = Math.Clamp(progressFraction, 0f, 1f);
                survivalLastLevel = level;
                return;
            }

            if (xpAmount < config.MinimumXp) return;

            ShowBar(skillName, progressFraction, level);

            for (int i = activeDrops.Count - 1; i >= 0; i--)
            {
                XpDrop existing = activeDrops[i];
                if (existing.SkillId == skillId && existing.Age < config.AccumulationWindow)
                {
                    existing.XpAmount += xpAmount;
                    existing.TextureDirty = true;
                    return;
                }
            }

            SpawnDrop(skillId, skillName, xpAmount);
        }

        /// <summary>Продвигает таймеры, перестраивает грязные текстуры и удаляет устаревшие метки. Вызывать один раз за каждый отрендеренный кадр</summary>
        /// <param name="dt">Дельта кадра в секундах</param>
        public void OnFrame(float dt)
        {
            // Изменение масштаба GUI делает недействительной каждую запеченную текстуру
            float guiScale = RuntimeEnv.GUIScale;
            if (guiScale != lastGuiScale)
            {
                lastGuiScale = guiScale;
                InvalidateTextures();
            }

            UpdateSurvivalBatch(dt);
            if (EditPreview) UpdatePreview();
            UpdateBar(dt);
            UpdateDrops(dt);
        }

        /// <summary>Принудительно перестраивает каждую запеченную текстуру в следующем кадре, например, после изменения размера</summary>
        public void InvalidateTextures()
        {
            barTextureDirty = true;
            for (int i = 0; i < activeDrops.Count; i++) activeDrops[i].TextureDirty = true;
        }

        /// <summary>Очищает все на экране и все ожидающие пакеты. Используется при выключении дисплея</summary>
        public void Reset()
        {
            foreach (XpDrop drop in activeDrops) drop.Dispose();
            activeDrops.Clear();

            barVisible = false;
            BarAlpha = 0f;
            barIdleTimer = 0.0;
            survivalAccumulated = 0f;
            survivalTimer = 0.0;
        }

        /// <summary>Экранный прямоугольник полосы. Привязан к своему правому краю, поэтому при увеличении масштаба он растет влево</summary>
        /// <returns>Прямоугольник; ширина/высота равны нулю, пока текстуры полосы еще нет</returns>
        public GuiRect GetBarRect()
        {
            double scale = RuntimeEnv.GUIScale;
            double w = BarTextureWidth;
            double h = BarTextureHeight;
            double x = capi.Render.FrameWidth - config.BarRightMargin * scale - w;
            double y = config.BarTopMargin * scale;

            return new GuiRect(x, y, w, h);
        }

        /// <summary>
        /// Экранный прямоугольник области, в которой появляется метка. Метки поднимаются из нее, поэтому это якорь, а не
        /// текущая позиция какой-либо метки
        /// </summary>
        /// <returns>Прямоугольник, размер которого соответствует новейшей метке или оценке, пока ни одной метки нет</returns>
        public GuiRect GetDropSpawnRect()
        {
            double scale = RuntimeEnv.GUIScale;
            GuiRect bar = GetBarRect();

            XpDrop newest = activeDrops.Count > 0 ? activeDrops[activeDrops.Count - 1] : null;
            double w, h;
            if (newest?.Texture != null && newest.Texture.Width > 0)
            {
                w = newest.Texture.Width;
                h = newest.Texture.Height;
            }
            else
            {
                w = (config.FontSize * config.DropScale * 2.6 + 12.0) * scale;
                h = EstimateDropRowHeight() * scale;
            }

            double cx = bar.CenterX + config.TextSpawnOffsetX * scale;
            double y = bar.Y + config.TextSpawnBelowBar * scale;

            return new GuiRect(cx - w / 2.0, y, w, h);
        }

        /// <summary>
        /// Собирает опыт выживания и выпускает его в виде одной метки
        /// Таймер работает только пока что-то ожидается, поэтому интервал означает "собирать в течение N секунд,
        /// начиная с первого получения", а не "выпустить первое получение, которое придет через N секунд"
        /// </summary>
        /// <param name="dt">Дельта кадра в секундах</param>
        private void UpdateSurvivalBatch(float dt)
        {
            if (survivalAccumulated <= 0f)
            {
                survivalTimer = 0.0;
                return;
            }

            survivalTimer += dt;
            if (survivalTimer < config.SurvivalBatchInterval) return;

            survivalTimer = 0.0;
            if (survivalAccumulated < config.MinimumXp) return; // продолжаем собирать вместо того, чтобы выбрасывать

            float amount = survivalAccumulated;
            survivalAccumulated = 0f;
            ShowBar(SurvivalSkill, survivalLastProgress, survivalLastLevel);
            SpawnDrop(survivalSkillId, SurvivalSkill, amount);
        }

        /// <summary>Поддерживает полосу активной и одну плавающую метку, пока открыт диалог редактирования</summary>
        private void UpdatePreview()
        {
            barIdleTimer = 0.0;

            // ShowBar помечает текстуру как грязную, поэтому вызываем только тогда, когда вообще нечего показывать
            if (!barVisible) ShowBar(PreviewSkill, 0.62f, 7);
            if (activeDrops.Count == 0) SpawnDrop(PreviewSkillId, PreviewSkill, 12.5f);
        }

        /// <summary>Запускает тайм-аут бездействия, затухание и ленивую перестройку текстуры полосы</summary>
        /// <param name="dt">Дельта кадра в секундах</param>
        private void UpdateBar(float dt)
        {
            if (!barVisible) return;

            barIdleTimer += dt;
            if (barIdleTimer >= config.BarIdleTimeout)
            {
                double fadeElapsed = barIdleTimer - config.BarIdleTimeout;
                BarAlpha = 1f - (float)Math.Min(fadeElapsed / config.BarFadeDuration, 1.0);
                if (BarAlpha <= 0f)
                {
                    BarAlpha = 0f;
                    barVisible = false;
                    return;
                }
            }
            else BarAlpha = 1f;

            if (barTextureDirty)
            {
                barTextureDirty = false;
                GenerateBarTexture();
            }
        }

        /// <summary>Увеличивает возраст меток, перестраивает грязные и удаляет просроченные</summary>
        /// <param name="dt">Дельта кадра в секундах</param>
        private void UpdateDrops(float dt)
        {
            for (int i = activeDrops.Count - 1; i >= 0; i--)
            {
                XpDrop drop = activeDrops[i];
                drop.Age += dt;

                if (drop.IsExpired(config.DropLifetime))
                {
                    drop.Dispose();
                    activeDrops.RemoveAt(i);
                }
                else if (drop.TextureDirty)
                {
                    drop.TextureDirty = false;
                    GenerateDropTexture(drop);
                }
            }
        }

        /// <summary>Переключает полосу на навык, сбрасывает таймер бездействия и помечает текстуру как грязную</summary>
        /// <param name="skillName">Внутреннее имя навыка</param>
        /// <param name="progressFraction">Прогресс до следующего уровня, ограниченный от 0 до 1</param>
        /// <param name="level">Текущий уровень</param>
        private void ShowBar(string skillName, float progressFraction, int level)
        {
            barSkillName = skillName;
            barProgress = Math.Clamp(progressFraction, 0f, 1f);
            barLevel = level;
            barIdleTimer = 0.0;
            barVisible = true;
            BarAlpha = 1f;
            barTextureDirty = true;
        }

        /// <summary>Добавляет новую метку; ее текстура будет создана в следующем кадре</summary>
        /// <param name="skillId">ID навыка XLib</param>
        /// <param name="skillName">Внутреннее имя навыка</param>
        /// <param name="xpAmount">Опыт для отображения</param>
        private void SpawnDrop(int skillId, string skillName, float xpAmount)
        {
            activeDrops.Add(new XpDrop
            {
                SkillId = skillId,
                SkillName = skillName,
                XpAmount = xpAmount,
                SpawnOffsetY = ComputeSpawnOffset()
            });
        }

        /// <summary>Приблизительная немасштабированная высота одной метки, используется до того, как появится ее текстура</summary>
        /// <returns>Высота в немасштабированных пикселях</returns>
        private float EstimateDropRowHeight() => config.FontSize * config.DropScale * 1.2f + 8f;

        /// <summary>
        /// Смещение для предотвращения перекрытия для метки, которая собирается появиться. Все метки поднимаются с одинаковой скоростью,
        /// поэтому мешать может только самая новая: начинаем ниже ее, пока она еще не освободила линию появления
        /// </summary>
        /// <returns>Немасштабированные пиксели для старта ниже линии появления, ограничено тремя строками</returns>
        private float ComputeSpawnOffset()
        {
            if (activeDrops.Count == 0) return 0f;

            XpDrop last = activeDrops[activeDrops.Count - 1];
            float needed = EstimateDropRowHeight() + config.DropSpacing;
            float lastRise = (float)(last.Age * config.FloatSpeed) - last.SpawnOffsetY;
            if (lastRise >= needed) return 0f;

            return Math.Min(needed - lastRise, needed * 3f);
        }

        /// <summary>Разрешает (и кэширует) стак предметов, отрисовываемый как иконка полосы</summary>
        /// <param name="skillName">Внутреннее имя навыка</param>
        /// <returns>Слот или <c>null</c>, если у навыка нет иконки - тогда полоса показывает букву вместо нее</returns>
        public DummySlot GetSkillSlot(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return null;

            string key = skillName.ToLowerInvariant();
            if (skillSlots.TryGetValue(key, out DummySlot cached)) return cached;

            DummySlot slot = null;
            string code = SkillVisuals.GetItemCode(key);
            if (code != null)
            {
                AssetLocation loc = new AssetLocation(code);
                Item item = capi.World.GetItem(loc);
                if (item != null)
                {
                    slot = new DummySlot(new ItemStack(item, 1));
                }
                else
                {
                    Block block = capi.World.GetBlock(loc);
                    if (block != null) slot = new DummySlot(new ItemStack(block, 1));
                    else capi.Logger.Notification("[PandaXPDrops] No item/block '{0}' for skill '{1}', drawing a letter instead.", code, key);
                }
            }

            skillSlots[key] = slot; // null кэшируется намеренно - не разрешаем заново каждый кадр
            return slot;
        }

        /// <summary>Удаляет каждую текстуру, созданную этим менеджером</summary>
        public void Dispose()
        {
            Reset();
            skillSlots.Clear();

            barTexture?.Dispose();
            barTexture = null;
        }


        // генерация текстур

        /// <summary>
        /// Запекает всю полосу (панель, ячейку иконки, имя, номера уровней, полосу прогресса) в одну текстуру и сохраняет
        /// якорь иконки, необходимый HUD для отрисовки стака предметов поверх нее
        /// </summary>
        /// <remarks>
        /// Все внутри текстуры использует <c>GUIScale * BarScale</c>, в то время как позиция на экране в
        /// <see cref="GetBarRect"/> использует обычный масштаб GUI - таким образом изменение размера не сдвигает элемент
        /// </remarks>
        private void GenerateBarTexture()
        {
            double scale = RuntimeEnv.GUIScale * config.BarScale;
            int padding = (int)(config.Padding * scale);
            int barHeight = (int)(config.BarHeight * scale);
            int textGap = (int)(config.TextGap * scale);
            int nameBarGap = (int)(2.0 * scale);
            double fontSize = config.FontSize * scale * 0.65;
            double nameFontSize = fontSize * 0.85;

            string skillDisplayName = SkillVisuals.GetDisplayName(barSkillName);
            string leftLvText = barLevel.ToString(CultureInfo.InvariantCulture);
            string rightLvText = (barLevel + 1).ToString(CultureInfo.InvariantCulture);

            int leftLvW, rightLvW, lvAscent, nameW, nameAscent, nameH;
            using (ImageSurface measure = new ImageSurface(Format.Argb32, 1, 1))
            using (Context mc = new Context(measure))
            {
                mc.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Bold);
                mc.SetFontSize(fontSize);
                TextExtents teL = mc.TextExtents(leftLvText);
                leftLvW = (int)Math.Ceiling(teL.Width + teL.XBearing) + 4;
                TextExtents teR = mc.TextExtents(rightLvText);
                rightLvW = (int)Math.Ceiling(teR.Width + teR.XBearing) + 4;
                lvAscent = (int)Math.Ceiling(mc.FontExtents.Ascent);

                // измерено с тем же шрифтом и размером, с которым оно отрисовывается
                mc.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Normal);
                mc.SetFontSize(nameFontSize);
                TextExtents teN = mc.TextExtents(skillDisplayName);
                nameW = (int)Math.Ceiling(teN.Width + teN.XBearing) + 4;
                FontExtents feN = mc.FontExtents;
                nameAscent = (int)Math.Ceiling(feN.Ascent);
                nameH = (int)Math.Ceiling(feN.Ascent + feN.Descent);
            }

            int barW = (int)(config.MinBarWidth * scale);
            int barRowW = leftLvW + textGap + barW + textGap + rightLvW;
            int barRowH = Math.Max(barHeight, lvAscent + 2);
            int rightContentH = nameH + nameBarGap + barRowH;
            int iconSize = Math.Max(rightContentH, (int)(config.IconSize * scale));
            int contentW = Math.Max(nameW, barRowW);
            int width = padding + iconSize + textGap + contentW + padding;
            int height = padding + iconSize + padding;

            using (ImageSurface surface = new ImageSurface(Format.Argb32, width, height))
            using (Context ctx = new Context(surface))
            {
                // панель
                RoundedRect(ctx, 0, 0, width, height, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.55);
                ctx.Fill();
                RoundedRect(ctx, 1, 1, width - 2, height - 2, 2.0 * scale);
                ctx.SetSourceRGBA(0.18, 0.16, 0.14, 0.92);
                ctx.Fill();

                // глянец на верхней половине
                ctx.Save();
                RoundedRect(ctx, 1, 1, width - 2, height / 2.0, 2.0 * scale);
                ctx.Clip();
                RoundedRect(ctx, 1, 1, width - 2, height - 2, 2.0 * scale);
                ctx.SetSourceRGBA(1, 1, 1, 0.04);
                ctx.Fill();
                ctx.Restore();

                // ячейка иконки - сам стак предметов отрисовывается поверх текстуры HUD'ом
                double iconX = padding;
                double iconY = padding;
                RoundedRect(ctx, iconX, iconY, iconSize, iconSize, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.2);
                ctx.Fill();
                RoundedRect(ctx, iconX, iconY, iconSize, iconSize, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.3);
                ctx.LineWidth = 1.0;
                ctx.Stroke();

                BarIconRenderSize = (float)(iconSize * 0.72);
                BarIconCenterX = iconX + iconSize / 2.0 - BarIconRenderSize * 0.02;
                BarIconCenterY = iconY + iconSize / 2.0 - BarIconRenderSize * 0.08;

                // навыки без разрешимого предмета (или из других модов XLib) получают цветную букву
                if (GetSkillSlot(barSkillName)?.Itemstack == null)
                {
                    DrawIconLetter(ctx, iconX + iconSize / 2.0, iconY + iconSize / 2.0, iconSize);
                }

                double contentX = padding + iconSize + textGap;
                double contentY = padding + (iconSize - rightContentH) / 2.0;

                // имя навыка
                ctx.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Normal);
                ctx.SetFontSize(nameFontSize);
                DrawText(ctx, skillDisplayName, contentX, contentY + nameAscent, 0.72, 0.68, 0.6);

                // уровень | полоса | следующий уровень
                double rowY = contentY + nameH + nameBarGap;
                double lvY = rowY + (barRowH + lvAscent) / 2.0;
                ctx.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(fontSize);
                DrawText(ctx, leftLvText, contentX, lvY, 0.82, 0.78, 0.7);

                double bx = contentX + leftLvW + textGap;
                double by = rowY + (barRowH - barHeight) / 2.0;

                RoundedRect(ctx, bx - 1, by - 1, barW + 2, barHeight + 2, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.5);
                ctx.Fill();
                RoundedRect(ctx, bx, by, barW, barHeight, 1.5 * scale);
                ctx.SetSourceRGBA(0.08, 0.07, 0.06, 0.95);
                ctx.Fill();

                double fillW = barW * barProgress;
                if (fillW > 1.0)
                {
                    ctx.Save();
                    RoundedRect(ctx, bx, by, barW, barHeight, 1.5 * scale);
                    ctx.Clip();
                    RoundedRect(ctx, bx, by, fillW, barHeight, 1.5 * scale);
                    ctx.SetSourceRGBA(0.22, 0.58, 0.12, 1.0);
                    ctx.Fill();
                    ctx.Rectangle(bx, by, fillW, barHeight * 0.45);
                    ctx.SetSourceRGBA(1, 1, 1, 0.12);
                    ctx.Fill();
                    ctx.Restore();
                }

                ctx.Save();
                RoundedRect(ctx, bx, by, barW, barHeight, 1.5 * scale);
                ctx.Clip();
                ctx.Rectangle(bx, by, barW, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.25);
                ctx.Fill();
                ctx.Restore();

                RoundedRect(ctx, bx, by, barW, barHeight, 1.5 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.4);
                ctx.LineWidth = 1.0;
                ctx.Stroke();

                DrawText(ctx, rightLvText, bx + barW + textGap, lvY, 0.82, 0.78, 0.7);

                LoadedTexture tex = barTexture ?? new LoadedTexture(capi);
                capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref tex);
                barTexture = tex;
            }
        }

        /// <summary>Запекает метку "+X.X" одного выпадения, повторно используя существующий объект текстуры, если он есть</summary>
        /// <param name="drop">Выпадение, чей <see cref="XpDrop.Texture"/> (пере)страивается</param>
        private void GenerateDropTexture(XpDrop drop)
        {
            double scale = RuntimeEnv.GUIScale * config.DropScale;
            double fontSize = config.FontSize * scale * 0.85;
            string xpText = "+" + drop.XpAmount.ToString("0.0#", CultureInfo.InvariantCulture);

            int textW, textH;
            using (ImageSurface measure = new ImageSurface(Format.Argb32, 1, 1))
            using (Context mc = new Context(measure))
            {
                mc.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Bold);
                mc.SetFontSize(fontSize);
                TextExtents te = mc.TextExtents(xpText);
                textW = (int)Math.Ceiling(te.Width + te.XBearing) + 8;
                textH = (int)Math.Ceiling(mc.FontExtents.Height) + 4;
            }

            int padX = (int)(6.0 * scale);
            int padY = (int)(3.0 * scale);
            int width = textW + padX * 2;
            int height = textH + padY * 2;

            using (ImageSurface surface = new ImageSurface(Format.Argb32, width, height))
            using (Context ctx = new Context(surface))
            {
                RoundedRect(ctx, 0, 0, width, height, 2.0 * scale);
                ctx.SetSourceRGBA(0, 0, 0, 0.45);
                ctx.Fill();
                RoundedRect(ctx, 1, 1, width - 2, height - 2, 1.5 * scale);
                ctx.SetSourceRGBA(0.18, 0.16, 0.14, 0.88);
                ctx.Fill();

                ctx.Save();
                RoundedRect(ctx, 1, 1, width - 2, height / 2.0, 1.5 * scale);
                ctx.Clip();
                RoundedRect(ctx, 1, 1, width - 2, height - 2, 1.5 * scale);
                ctx.SetSourceRGBA(1, 1, 1, 0.04);
                ctx.Fill();
                ctx.Restore();

                ctx.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(fontSize);
                DrawText(ctx, xpText, padX, padY + ctx.FontExtents.Ascent, 0.9, 0.87, 0.8);

                LoadedTexture tex = drop.Texture ?? new LoadedTexture(capi);
                capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref tex);
                drop.Texture = tex;
            }
        }

        /// <summary>Рисует первую букву навыка по центру ячейки иконки, окрашенную в ее цвет акцента</summary>
        /// <param name="ctx">Целевой контекст</param>
        /// <param name="cx">Центр ячейки иконки по оси X</param>
        /// <param name="cy">Центр ячейки иконки по оси Y</param>
        /// <param name="iconSize">Длина края ячейки иконки; буква использует 55% от нее</param>
        private void DrawIconLetter(Context ctx, double cx, double cy, int iconSize)
        {
            (double r, double g, double b) = SkillVisuals.GetColor(barSkillName);
            string letter = SkillVisuals.GetIconLetter(barSkillName);

            ctx.SelectFontFace(FontFace, FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(iconSize * 0.55);
            TextExtents te = ctx.TextExtents(letter);

            DrawText(ctx, letter, cx - te.Width / 2.0 - te.XBearing, cy - te.Height / 2.0 - te.YBearing, r, g, b);
        }

        /// <summary>Рисует текст с тенью в 1px с текущим шрифтом и размером</summary>
        /// <param name="ctx">Целевой контекст</param>
        /// <param name="text">Текст для отрисовки</param>
        /// <param name="x">Левый край</param>
        /// <param name="y">Базовая линия</param>
        /// <param name="r">Красный 0..1</param>
        /// <param name="g">Зеленый 0..1</param>
        /// <param name="b">Синий 0..1</param>
        private static void DrawText(Context ctx, string text, double x, double y, double r, double g, double b)
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.5);
            ctx.MoveTo(x + 1, y + 1);
            ctx.ShowText(text);

            ctx.SetSourceRGBA(r, g, b, 1.0);
            ctx.MoveTo(x, y);
            ctx.ShowText(text);
        }

        /// <summary>Строит путь скругленного прямоугольника; вызывающий код заливает или обводит его</summary>
        /// <param name="ctx">Целевой контекст</param>
        /// <param name="x">Левый край</param>
        /// <param name="y">Верхний край</param>
        /// <param name="w">Ширина</param>
        /// <param name="h">Высота</param>
        /// <param name="r">Радиус угла, ограниченный половиной более короткой стороны</param>
        private static void RoundedRect(Context ctx, double x, double y, double w, double h, double r)
        {
            r = Math.Min(r, Math.Min(w, h) / 2.0);
            if (r < 0.1)
            {
                ctx.Rectangle(x, y, w, h);
                return;
            }

            const double halfPi = Math.PI / 2.0;
            ctx.NewPath();
            ctx.Arc(x + w - r, y + r, r, -halfPi, 0);
            ctx.Arc(x + w - r, y + h - r, r, 0, halfPi);
            ctx.Arc(x + r, y + h - r, r, halfPi, Math.PI);
            ctx.Arc(x + r, y + r, r, Math.PI, 3.0 * halfPi);
            ctx.ClosePath();
        }
    }
}