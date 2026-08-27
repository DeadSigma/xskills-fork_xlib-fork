using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace XSkills
{
    // Совместимость с XInvTweaksFork (xandu inventory tweaks).
    //
    // Проблема: XSkills подмешивает strongback-слоты в ванильный рюкзак через
    // InventoryPlayerBackpacksPatch (патчи get_Count/get_Item). Поэтому любая операция
    // xandu, которая перечисляет рюкзак (SortBackpack, FillBackpack, Pull/Push и т.д.),
    // видит и двигает эти слоты как часть рюкзака.
    //
    // Решение: когда слоты ЗАФИКСИРОВАНЫ, на время такой операции временно снимаем Linked.
    // Тогда рюкзак перечисляется без strongback-слотов, и xandu их не трогает. Linked
    // возвращается сразу в постфиксе, поэтому отображение и ручное перекладывание не страдают
    // (перекомпоновки GUI внутри синхронного вызова не происходит).
    //
    // Незафиксированные слоты остаются Linked -> xandu сортирует их вместе с рюкзаком, как и
    // задумано (то же поведение, что мы сделали для StorageTweaks).
    public static class XInvTweaksCompat
    {
        private const string HarmonyId = "com.xskills.xinvtweaks";

        public static void ApplyPatch(ICoreAPI api)
        {
            Type invUtil = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType("XInvTweaksFork.InventoryUtil", false);
                    if (t != null) { invUtil = t; break; }
                }
                catch { }
            }
            if (invUtil == null) return; // мод не загружен — тихо выходим

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                MethodInfo prefix = AccessTools.Method(typeof(XInvTweaksCompat), nameof(UnlinkPrefix));
                MethodInfo postfix = AccessTools.Method(typeof(XInvTweaksCompat), nameof(RelinkPostfix));

                // Все операции xandu, которые перечисляют/мутируют рюкзак. Сигнатура у всех
                // одна — (ICoreClientAPI capi), поэтому берём именно этот перегруз.
                string[] methods =
                {
                    "SortBackpack",       // Alt+Z — тот самый баг
                    "FillBackpack",
                    "PullInventories",
                    "SortIntoInventory",  // однопараметрический перегруз (push через кнопку)
                    "PushInventory",
                };

                foreach (string name in methods)
                    PatchOne(harmony, invUtil, name, prefix, postfix);

                api.Logger.Event("XSkills: XInvTweaks (xandu) compat-патч зарегистрирован.");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"XSkills: ошибка компат-патча XInvTweaks: {ex}");
            }
        }

        private static void PatchOne(Harmony harmony, Type type, string method, MethodInfo prefix, MethodInfo postfix)
        {
            // Тип-массив с ICoreClientAPI однозначно выбирает нужный перегруз.
            MethodInfo original = AccessTools.Method(type, method, new[] { typeof(ICoreClientAPI) });
            if (original == null) return;

            // Идемпотентность: снимаем свои прошлые патчи перед повторным навешиванием.
            harmony.Unpatch(original, HarmonyPatchType.Prefix, HarmonyId);
            harmony.Unpatch(original, HarmonyPatchType.Postfix, HarmonyId);
            harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
        }

        // Имя параметра capi ДОЛЖНО совпадать с параметром патчируемых методов — Harmony
        // прокидывает его по имени.
        public static void UnlinkPrefix(ICoreClientAPI capi, out bool __state)
        {
            __state = false;
            XSkillsPlayerInventory inv =
                capi?.World?.Player?.InventoryManager.GetOwnInventory("xskillshotbar") as XSkillsPlayerInventory;
            if (inv != null && inv.Linked && inv.IsFixed)
            {
                inv.Linked = false;
                __state = true; // запоминаем, что сняли — чтобы вернуть в постфиксе
            }
        }

        public static void RelinkPostfix(ICoreClientAPI capi, bool __state)
        {
            if (!__state) return;
            XSkillsPlayerInventory inv =
                capi?.World?.Player?.InventoryManager.GetOwnInventory("xskillshotbar") as XSkillsPlayerInventory;
            if (inv != null) inv.Linked = true;
        }
    }
}