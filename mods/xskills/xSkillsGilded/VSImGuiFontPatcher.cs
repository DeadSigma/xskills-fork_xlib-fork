using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace xSkillGilded
{
    // Патчер Запускается до VSImGui (добавляет glyph-ranges и регистрирует нужный шрифт-файл)
    public class VSImGuiFontPatcher : ModSystem
    {
        public override double ExecuteOrder() { return -0.1; }
        public override bool ShouldLoad(EnumAppSide forSide) { return forSide == EnumAppSide.Client; }

        private static ushort[] _unicodeRanges;
        private static GCHandle _unicodeHandle;

        // Имя загруженного шрифта передаётся во второй патчер (PostPatcher),
        // который переключает на него стиль окон xSkillsGilded.
        public static string SuccessfullyLoadedFontName = null;

        // Языки, для которых игровые латинские шрифты (Montserrat и т.п.) не годятся: нужен файл шрифта с CJK-глифами.
        private static readonly string[] CjkLocales = { "zh-cn", "zh-tw", "ja", "ko" };
        private static bool IsCjk(string locale) => Array.IndexOf(CjkLocales, locale) >= 0;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            try
            {
                var vsImGui = api.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>();
                if (vsImGui == null) return;

                var loaderType = typeof(VSImGui.ImGuiModSystem).Assembly.GetType("VSImGui.NativesLoader");
                var loadMethod = loaderType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                loadMethod?.Invoke(null, new object[] { api.Logger, vsImGui });

                IntPtr dummyContext = ImGui.CreateContext();
                ImGui.SetCurrentContext(dummyContext);

                // Широкий диапазон для любых европейских языков:
                // Basic Latin + Latin-1, Latin Extended-A/B, кириллица,
                // Latin Extended Additional (для вьетнамского и т.п.).
                _unicodeRanges = new ushort[] {
                    0x0020, 0x00FF, // Basic Latin + Latin-1 Supplement
                    0x0100, 0x017F, // Latin Extended-A (ł ą ę ż ś ć ń, č š ž, ğ ş ...)
                    0x0180, 0x024F, // Latin Extended-B
                    0x0400, 0x04FF, // Кириллица
                    0x1E00, 0x1EFF, // Latin Extended Additional
                    0
                };
                _unicodeHandle = GCHandle.Alloc(_unicodeRanges, GCHandleType.Pinned);
                nint unicodeRangePtr = _unicodeHandle.AddrOfPinnedObject();

                string currentLocale = Lang.CurrentLocale;

                // 1. Настраиваем glyph-ranges VSImGui через рефлексию
                // VSImGui при запекании берёт диапазон по ключу Lang.CurrentLocale, поэтому важно, чтобы для текущего языка был правильный набор глифов
                var fontManagerType = typeof(VSImGui.API.FontManager);
                var glyphField = fontManagerType.GetField("GlyphRanges", BindingFlags.Static | BindingFlags.NonPublic);

                if (glyphField != null)
                {
                    var dict = (Dictionary<string, nint>)glyphField.GetValue(null);
                    if (dict != null)
                    {
                        // Полные наборы для CJK
                        nint chineseRange = ImGui.GetIO().Fonts.GetGlyphRangesChineseFull();
                        dict["zh-cn"] = chineseRange;
                        dict["zh-tw"] = chineseRange; // в дефолте VSImGui этого ключа нет
                        dict["ja"] = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();
                        dict["ko"] = ImGui.GetIO().Fonts.GetGlyphRangesKorean();

                        // Для текущего не-английского и не-CJK языка ставим широкий латинско-кириллический диапазон, чтобы точно хватило глифов (польский, чешский, кириллица и т.д.).
                        if (currentLocale != "en" && !IsCjk(currentLocale))
                        {
                            dict[currentLocale] = unicodeRangePtr;
                        }
                    }
                }

                // Читаем конфиг напрямую из файла (StartPre - до системы конфигов)
                string configPath = Path.Combine(GamePaths.DataPath, "ModConfig", "xskillsgilded.json");
                bool enableCustomFont = true;   // по умолчанию включено
                string customFontPath = "";

                if (File.Exists(configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(configPath);
                        var parsedConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsedConfig != null)
                        {
                            if (parsedConfig.ContainsKey("EnableCustomFont")) enableCustomFont = Convert.ToBoolean(parsedConfig["EnableCustomFont"]);
                            if (parsedConfig.ContainsKey("CustomFontPath")) customFontPath = parsedConfig["CustomFontPath"]?.ToString() ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        api.Logger.Error("[xSkillGilded] Ошибка чтения конфига в StartPre: " + ex);
                    }
                }

                // Выбираем и подключаем шрифт
                var fontsProp = fontManagerType.GetProperty("Fonts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (fontsProp == null)
                {
                    api.Logger.Warning("[xSkillGilded] Не удалось получить доступ к FontManager.Fonts - шрифт не будет подключён");
                    return;
                }

                var fontsSet = (HashSet<string>)fontsProp.GetValue(null);

                // Английский рисуется scarab-шрифтом, дополнительный шрифт не нужен, почему на русском? Потому что так хочется :D
                if (currentLocale == "en")
                {
                    api.Logger.Event("[xSkillGilded] Локаль 'en' - используется декоративный scarab-шрифт");
                    return;
                }

                if (!enableCustomFont)
                {
                    api.Logger.Event("[xSkillGilded] EnableCustomFont=false - ImGui будет использовать стандартный игровой шрифт (Montserrat)");
                    return;
                }

                // Приоритет 1: пользовательский шрифт из конфига
                if (!string.IsNullOrEmpty(customFontPath) && File.Exists(customFontPath))
                {
                    fontsSet.Add(customFontPath);
                    SuccessfullyLoadedFontName = Path.GetFileNameWithoutExtension(customFontPath);
                    api.Logger.Event($"[xSkillGilded] Загружен пользовательский шрифт: {customFontPath}");
                    return;
                }

                // Приоритет 2: системный шрифт, подходящий под язык
                string systemFont = FindSystemFontForLocale(currentLocale, api.Logger);
                if (systemFont != null)
                {
                    fontsSet.Add(systemFont);
                    SuccessfullyLoadedFontName = Path.GetFileNameWithoutExtension(systemFont);
                    api.Logger.Event($"[xSkillGilded] Для языка '{currentLocale}' применён системный шрифт: {systemFont}");
                    return;
                }

                // Приоритет 3 (только CJK): запасной встроенный cjk_font.ttf из ассетов мода
                if (IsCjk(currentLocale))
                {
                    string bundled = TryUnpackBundledCjk(api);
                    if (bundled != null)
                    {
                        fontsSet.Add(bundled);
                        SuccessfullyLoadedFontName = Path.GetFileNameWithoutExtension(bundled);
                        api.Logger.Event($"[xSkillGilded] Системный CJK-шрифт не найден - распакован запасной cjk_font.ttf.");
                        return;
                    }

                    api.Logger.Warning($"[xSkillGilded] Для '{currentLocale}' не найден ни системный, ни встроенный CJK-шрифт - иероглифы могут не отображаться.");
                    return;
                }

                // Приоритет 4 (европейские): системный не нашёлся - оставляем стандартный игровой Montserrat (латиница + кириллица уже запекаются VSImGui)
                api.Logger.Event($"[xSkillGilded] Системный шрифт для '{currentLocale}' не найден - используется стандартный игровой шрифт.");
            }
            catch (Exception ex)
            {
                api.Logger.Error("[xSkillGilded] Error with StartPre: " + ex);
            }
        }

        // Ищет на диске файл системного шрифта, покрывающий нужный язык
        // Возвращает путь к .ttf/.otf/.ttc или null
        private static string FindSystemFontForLocale(string locale, ILogger logger)
        {
            bool cjk = IsCjk(locale);
            List<string> candidates = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                if (string.IsNullOrEmpty(dir)) dir = @"C:\Windows\Fonts";

                if (cjk)
                {
                    if (locale == "ja")
                    {
                        candidates.Add(Path.Combine(dir, "YuGothM.ttc"));
                        candidates.Add(Path.Combine(dir, "meiryo.ttc"));
                        candidates.Add(Path.Combine(dir, "msgothic.ttc"));
                    }
                    else if (locale == "ko")
                    {
                        candidates.Add(Path.Combine(dir, "malgun.ttf"));
                        candidates.Add(Path.Combine(dir, "malgunsl.ttf"));
                        candidates.Add(Path.Combine(dir, "gulim.ttc"));
                    }
                    else if (locale == "zh-tw")
                    {
                        candidates.Add(Path.Combine(dir, "msjh.ttc"));   // Microsoft JhengHei
                        candidates.Add(Path.Combine(dir, "mingliu.ttc"));
                    }
                    else // zh-cn и прочее
                    {
                        candidates.Add(Path.Combine(dir, "msyh.ttc"));   // Microsoft YaHei
                        candidates.Add(Path.Combine(dir, "simhei.ttf"));
                        candidates.Add(Path.Combine(dir, "simsun.ttc"));
                    }
                    // Универсальные CJK-подстраховки
                    candidates.Add(Path.Combine(dir, "msyh.ttc"));
                    candidates.Add(Path.Combine(dir, "simsun.ttc"));
                }
                else
                {
                    candidates.Add(Path.Combine(dir, "segoeui.ttf"));
                    candidates.Add(Path.Combine(dir, "arial.ttf"));
                    candidates.Add(Path.Combine(dir, "tahoma.ttf"));
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (cjk)
                {
                    candidates.Add("/System/Library/Fonts/PingFang.ttc");
                    candidates.Add("/System/Library/Fonts/Hiragino Sans GB.ttc");
                    candidates.Add("/Library/Fonts/Arial Unicode.ttf");
                }
                else
                {
                    candidates.Add("/Library/Fonts/Arial.ttf");
                    candidates.Add("/System/Library/Fonts/Helvetica.ttc");
                    candidates.Add("/System/Library/Fonts/SFNS.ttf");
                }
            }
            else // Linux и прочее
            {
                if (cjk)
                {
                    candidates.Add("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc");
                    candidates.Add("/usr/share/fonts/opentype/noto/NotoSansCJKsc-Regular.otf");
                    candidates.Add("/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc");
                    candidates.Add("/usr/share/fonts/wenquanyi/wqy-microhei/wqy-microhei.ttc");
                    candidates.Add("/usr/share/fonts/wenquanyi/wqy-zenhei/wqy-zenhei.ttc");
                }
                else
                {
                    candidates.Add("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
                    candidates.Add("/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf");
                    candidates.Add("/usr/share/fonts/liberation/LiberationSans-Regular.ttf");
                    candidates.Add("/usr/share/fonts/TTF/DejaVuSans.ttf");
                }
            }

            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }

            // Запасной вариант: рекурсивно просканировать типичные каталоги шрифтов
            try
            {
                List<string> scanDirs = new List<string>();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string wf = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                    if (!string.IsNullOrEmpty(wf)) scanDirs.Add(wf);
                }
                else
                {
                    scanDirs.Add("/usr/share/fonts");
                    scanDirs.Add("/usr/local/share/fonts");
                    scanDirs.Add("/System/Library/Fonts");
                    scanDirs.Add("/Library/Fonts");
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!string.IsNullOrEmpty(home))
                    {
                        scanDirs.Add(Path.Combine(home, ".fonts"));
                        scanDirs.Add(Path.Combine(home, ".local", "share", "fonts"));
                    }
                }

                string[] cjkHints = { "noto", "cjk", "yahei", "gothic", "mincho", "song", "hei", "kai", "ping", "malgun", "gulim", "batang", "wqy", "sourcehan", "jhenghei", "mingliu", "simsun", "simhei" };
                string[] latinHints = { "dejavusans", "notosans", "arial", "segoeui", "liberationsans", "helvetica", "tahoma" };

                foreach (var dir in scanDirs)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".ttf" && ext != ".otf" && ext != ".ttc") continue;

                        string name = Path.GetFileName(f).ToLowerInvariant();
                        if (cjk)
                        {
                            foreach (var h in cjkHints) if (name.Contains(h)) return f;
                        }
                        else
                        {
                            foreach (var h in latinHints) if (name.Contains(h)) return f;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger?.Warning("[xSkillGilded] Ошибка при поиске системного шрифта: " + e.Message);
            }

            return null;
        }

        // Запасной вариант для CJK: достаёт cjk_font.ttf из ассетов мода и кладёт во временный файл, путь к которому возвращает
        private static string TryUnpackBundledCjk(ICoreAPI api)
        {
            try
            {
                string tempDir = Path.Combine(GamePaths.DataPath, "Cache", "xSkillGilded");
                Directory.CreateDirectory(tempDir);

                const string targetFontName = "cjk_font.ttf";
                string tempPath = Path.Combine(tempDir, "cjk_font_temp.ttf");

                List<string> searchDirectories = new List<string>();

                string standardModsDir = Path.Combine(GamePaths.DataPath, "Mods");
                if (Directory.Exists(standardModsDir)) searchDirectories.Add(standardModsDir);

                string serverModsDir = Path.Combine(GamePaths.DataPath, "ModsByServer");
                if (Directory.Exists(serverModsDir)) searchDirectories.AddRange(Directory.GetDirectories(serverModsDir));

                foreach (string dir in searchDirectories)
                {
                    // Распакованные папки модов
                    foreach (string subDir in Directory.GetDirectories(dir))
                    {
                        string testPath = Path.Combine(subDir, "assets", "xskillgilded", "fonts", targetFontName);
                        if (File.Exists(testPath))
                        {
                            File.Copy(testPath, tempPath, true);
                            return tempPath;
                        }
                    }

                    // .zip архивы модов
                    foreach (string zipFile in Directory.GetFiles(dir, "*.zip"))
                    {
                        try
                        {
                            using (var archive = ZipFile.OpenRead(zipFile))
                            {
                                var entry = archive.GetEntry($"assets/xskillgilded/fonts/{targetFontName}");
                                if (entry != null)
                                {
                                    entry.ExtractToFile(tempPath, true);
                                    return tempPath;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[xSkillGilded] Не удалось распаковать встроенный cjk_font.ttf: " + ex.Message);
            }

            return null;
        }

        public override void Dispose()
        {
            if (_unicodeHandle.IsAllocated) _unicodeHandle.Free();
            base.Dispose();
        }
    }

    // Запускается после VSImGui - переключает стиль окон xSkillsGilded на выбранный шрифт
    public class VSImGuiFontPostPatcher : ModSystem
    {
        public override double ExecuteOrder() { return 0.1; }
        public override bool ShouldLoad(EnumAppSide forSide) { return forSide == EnumAppSide.Client; }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            if (!string.IsNullOrEmpty(VSImGuiFontPatcher.SuccessfullyLoadedFontName))
            {
                try
                {
                    var vsImGui = api.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>();
                    if (vsImGui != null && vsImGui.DefaultStyle != null)
                    {
                        var styleType = vsImGui.DefaultStyle.GetType();
                        var fontNameProp = styleType.GetProperty("FontName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (fontNameProp != null)
                        {
                            fontNameProp.SetValue(vsImGui.DefaultStyle, VSImGuiFontPatcher.SuccessfullyLoadedFontName);
                            api.Logger.Event($"[xSkillGilded] Стиль xSkillsGilded переключён на шрифт: '{VSImGuiFontPatcher.SuccessfullyLoadedFontName}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error("[xSkillGilded] Error while switching FontName in AssetsLoaded: " + ex);
                }
            }
        }
    }
}