using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.Server;

namespace XlibFork.ModIdSpoofing;

/// <summary>
/// Заставляет форк выдавать себя за оригинал (xlib / xskills): чужие проверки
/// </summary>
public class ModIdSpoofSystem : ModSystem
{
    private string canonicalId;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "loadedMods")]
    private static extern ref Dictionary<string, ModContainer> GetLoadedMods(ModLoader instance);

    /// <summary>
    /// Определяет, за кого себя выдавать (по типу форка в этой сборке),
    /// и вешает Harmony-патчи с уникальным на каждый форк id, чтобы два
    /// форка не гасили патчи друг друга через HasAnyPatches.
    /// </summary>
    public ModIdSpoofSystem()
    {
        var self = typeof(ModIdSpoofSystem).Assembly;

        if (AccessTools.TypeByName("XSkills.XSkills")?.Assembly == self)
            canonicalId = "xskills";
        else if (AccessTools.TypeByName("XLib.XLeveling.XLeveling")?.Assembly == self)
            canonicalId = "xlib";

        if (canonicalId is null) return;

        ModIdSpoofPatches.Spoofed.Add(canonicalId);

        string harmonyId = "modidspoof:" + canonicalId;
        if (!Harmony.HasAnyPatches(harmonyId))
            new Harmony(harmonyId).PatchAll(typeof(ModIdSpoofPatches).Assembly);
    }

    /// <summary>
    /// Переименовывает собственный контейнер мода в canonicalId и подменяет
    /// запись в loadedMods. Настоящий modid запоминается для отдачи клиенту
    /// </summary>
    /// <param name="api">API ядра игры.</param>
    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        if (canonicalId is null || Mod?.Info is null) return;

        string ownId = Mod.Info.ModID;
        if (ownId == canonicalId) return;

        var loaded = GetLoadedMods((ModLoader)api.ModLoader);
        if (loaded.TryGetValue(ownId, out var container) && !loaded.ContainsKey(canonicalId))
        {
            ModIdSpoofPatches.Originals[canonicalId] = ownId;
            loaded[canonicalId] = container;
            loaded.Remove(ownId);
            Mod.Info.ModID = canonicalId;
        }
    }

    /// <summary>
    /// Снимает Harmony-патчи этого форка и чистит записи спуфинга
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        if (canonicalId is null) return;

        string harmonyId = "modidspoof:" + canonicalId;
        new Harmony(harmonyId).UnpatchAll(harmonyId);
        ModIdSpoofPatches.Spoofed.Remove(canonicalId);
        ModIdSpoofPatches.Originals.Remove(canonicalId);
    }
}

/// <summary>
/// Harmony-патчи, обеспечивающие спуфинг modid: подмена ответа IsModEnabled
/// и возврат клиенту настоящего форкового id в пакете идентификации
/// </summary>
[HarmonyPatch]
public static class ModIdSpoofPatches
{
    internal static readonly HashSet<string> Spoofed = [];
    internal static readonly Dictionary<string, string> Originals = [];

    /// <summary>
    /// Возвращает true для спуфнутых id - на случай, если кто-то спросит
    /// modid ещё до StartPre.
    /// </summary>
    /// <param name="modID">Запрашиваемый идентификатор мода.</param>
    /// <param name="__result">Результат оригинального метода.</param>
    /// <returns>false, если результат подменён; иначе true.</returns>
    [HarmonyPatch(typeof(ModLoader), nameof(ModLoader.IsModEnabled))]
    [HarmonyPrefix]
    public static bool IsModEnabledPrefix(string modID, ref bool __result)
    {
        if (Spoofed.Contains(modID) || Originals.ContainsValue(modID))
        {
            __result = true;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Подменяет canonicalId обратно на настоящий форковый modid в пакете,
    /// иначе клиент попытается скачать оригинальный xlib / xskills
    /// </summary>
    /// <param name="__result">Пакет идентификации сервера.</param>
    [HarmonyPatch(typeof(ServerMain), "CreatePacketIdentification")]
    [HarmonyPostfix]
    public static void CreatePacketIdentificationPostfix(Packet_Server __result)
    {
        if (__result?.Identification?.Mods is null) return;

        foreach (var mod in __result.Identification.Mods)
        {
            if (Originals.TryGetValue(mod.Modid, out string original))
                mod.Modid = original;
        }
    }
}