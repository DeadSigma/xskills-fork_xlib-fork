using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Интеграция XSkills с модом Knapster для лепки из глины (EasyClayForming).
    /// </summary>
    public static class KnapsterClayIntegration
    {
        public static void ApplyPatches(Harmony harmony, ICoreAPI api)
        {
            // Проверяем наличие мода Knapster
            if (!api.ModLoader.IsModEnabled("knapster")) return;

            var patchType = AccessTools.TypeByName("Knapster.Features.EasyClayForming.Patches.EasyClayFormingUniversalPatches");
            if (patchType == null) return;

            // Патчим метод OnUseOver_Prefix от Knapster, чтобы отслеживать трату глины
            var onUseOverPrefixMethod = AccessTools.Method(patchType, "UniversalPatch_BlockEntityClayForm_OnUseOver_Prefix");

            if (onUseOverPrefixMethod != null)
            {
                harmony.Patch(onUseOverPrefixMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(KnapsterClayIntegration), nameof(KnapsterUseOver_Prefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(KnapsterClayIntegration), nameof(KnapsterUseOver_Postfix)))
                );
            }
        }

        // Используем ThreadStatic для безопасного хранения состояния до выполнения метода Knapster
        [ThreadStatic]
        private static int _clayStackSizeBefore;

        public static void KnapsterUseOver_Prefix(BlockEntityClayForm __instance, IPlayer byPlayer)
        {
            if (byPlayer == null) return;
            var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            _clayStackSizeBefore = slot?.Itemstack?.StackSize ?? 0;
        }

        public static void KnapsterUseOver_Postfix(BlockEntityClayForm __instance, IPlayer byPlayer)
        {
            if (__instance == null || byPlayer == null) return;

            var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            int currentStackSize = slot?.Itemstack?.StackSize ?? 0;

            int consumedClay = _clayStackSizeBefore - currentStackSize;

            // Если Knapster потратил глину во время своей автоматической лепки
            if (consumedClay > 0)
            {
                Pottery pottery = XLeveling.Instance(__instance.Api)?.GetSkill("pottery") as Pottery;
                PlayerSkill playerSkill = pottery != null ? byPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[pottery.Id] : null;

                if (playerSkill != null)
                {
                    // --- ЛОГИКА ПЕРКА "ЭКОНОМИЯ" (Thrift) ---
                    PlayerAbility thrift = playerSkill.PlayerAbilities[pottery.ThriftId];
                    if (thrift != null && thrift.Tier > 0)
                    {
                        // Добавляем бесплатные воксели за каждый потраченный кусок глины
                        __instance.AvailableVoxels += consumedClay * thrift.Value(0);
                    }
                }
            }
        }
    }
}