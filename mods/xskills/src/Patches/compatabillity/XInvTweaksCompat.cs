using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace XSkills
{
    // Совместимость с XInvTweaksFork
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

                    if (t != null)
                    {
                        invUtil = t;
                        break;
                    }
                }
                catch { }
            }

            if (invUtil == null) return;

            try
            {
                Harmony harmony = new Harmony(HarmonyId);

                MethodInfo prefix =
                    AccessTools.Method(
                        typeof(XInvTweaksCompat),
                        nameof(UnlinkPrefix)
                    );

                MethodInfo postfix =
                    AccessTools.Method(
                        typeof(XInvTweaksCompat),
                        nameof(RelinkPostfix)
                    );

                // Операции с рюкзаком перехватываются патчем
                string[] methods =
                {
                    "SortBackpack",
                    "FillBackpack",
                    "PullInventories",
                    "SortIntoInventory",
                    "PushInventory",
                };

                foreach (string name in methods)
                {
                    PatchOne(
                        harmony,
                        invUtil,
                        name,
                        prefix,
                        postfix
                    );
                }

                api.Logger.Event(
                    "XSkills: XInvTweaks (xandu) compat-патч зарегистрирован."
                );
            }
            catch (Exception ex)
            {
                api.Logger.Error(
                    $"XSkills: ошибка компат-патча XInvTweaks: {ex}"
                );
            }
        }

        private static void PatchOne(
            Harmony harmony,
            Type type,
            string method,
            MethodInfo prefix,
            MethodInfo postfix
        )
        {
            MethodInfo original =
                AccessTools.Method(
                    type,
                    method,
                    new[] { typeof(ICoreClientAPI) }
                );

            if (original == null) return;

            // Старые патчи снимаются перед регистрацией
            harmony.Unpatch(
                original,
                HarmonyPatchType.Prefix,
                HarmonyId
            );

            harmony.Unpatch(
                original,
                HarmonyPatchType.Postfix,
                HarmonyId
            );

            harmony.Patch(
                original,
                new HarmonyMethod(prefix),
                new HarmonyMethod(postfix)
            );
        }

        public static void UnlinkPrefix(
            ICoreClientAPI capi,
            out bool __state
        )
        {
            __state = false;

            XSkillsPlayerInventory inv =
                capi?.World?.Player?.InventoryManager
                    .GetOwnInventory("xskillshotbar")
                as XSkillsPlayerInventory;

            // Зафиксированные слоты временно отвязываются
            if (inv != null && inv.Linked && inv.IsFixed)
            {
                inv.Linked = false;
                __state = true;
            }
        }

        public static void RelinkPostfix(
            ICoreClientAPI capi,
            bool __state
        )
        {
            if (!__state) return;

            XSkillsPlayerInventory inv =
                capi?.World?.Player?.InventoryManager
                    .GetOwnInventory("xskillshotbar")
                as XSkillsPlayerInventory;

            // Слоты привязываются обратно после операции
            if (inv != null)
            {
                inv.Linked = true;
            }
        }
    }
}