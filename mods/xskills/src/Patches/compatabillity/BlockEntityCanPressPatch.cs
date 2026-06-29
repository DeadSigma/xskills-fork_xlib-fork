using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public class BlockEntityCanPressPatch
    {
        public static void Apply(Harmony harmony, Type pressType)
        {
            if (pressType == null) return;

            // Патчим старт пресса, чтобы поймать игрока и записать его кулинарный бонус
            var interactStart = pressType.GetMethod("OnHandleInteractStart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var interactPostfix = typeof(BlockEntityCanPressPatch).GetMethod(nameof(OnHandleInteractStartPostfix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (interactStart != null && interactPostfix != null)
            {
                harmony.Patch(interactStart, postfix: new HarmonyMethod(interactPostfix));
            }

            // Патчим завершение пресса, чтобы применить сохраненный бонус к новой запечатанной банке
            var sealCan = pressType.GetMethod("TrySealCan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sealCanPostfix = typeof(BlockEntityCanPressPatch).GetMethod(nameof(TrySealCanPostfix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (sealCan != null && sealCanPostfix != null)
            {
                harmony.Patch(sealCan, postfix: new HarmonyMethod(sealCanPostfix));
            }
        }

        public static void OnHandleInteractStartPostfix(BlockEntityContainer __instance, IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;

            InventoryBase inv = __instance.Inventory;
            // Слот 1 - это заполненная банка до закрытия
            if (inv == null || inv.Count < 3 || inv[1].Empty) return;

            XSkills xskills = XSkills.Instance;
            Cooking cooking = xskills?.Skills["cooking"] as Cooking;
            if (cooking == null) return;

            PlayerSkill skill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id];
            if (skill != null)
            {
                PlayerAbility wellDone = skill[cooking.WellDoneId];
                if (wellDone != null && wellDone.Tier > 0)
                {
                    // Сохраняем множитель бонуса прямо в атрибуты текущей открытой банки
                    float multiplier = 1.0f + wellDone.SkillDependentFValue();
                    inv[1].Itemstack.Attributes.SetFloat("xskillsWelldoneBonus", multiplier);
                }
            }
        }

        public static void TrySealCanPostfix(BlockEntityContainer __instance, ref bool __result)
        {
            // Если банка не запечаталась (не хватило крышки и т.д.), ничего не делаем
            if (!__result) return;

            InventoryBase inv = __instance.Inventory;
            // Слот 2 - это готовая запечатанная банка
            if (inv == null || inv.Count < 3 || inv[2].Empty) return;

            ItemStack sealedCan = inv[2].Itemstack;

            // Скопировался из открытой банки во время TrySealCan
            float bonus = sealedCan.Attributes.GetFloat("xskillsWelldoneBonus", 0f);

            if (bonus > 0f)
            {
                ICoreAPI api = __instance.Api;

                sealedCan.Collectible.UpdateAndGetTransitionStates(api.World, inv[2]);

                ITreeAttribute transAttr = sealedCan.Attributes.GetTreeAttribute("transitionstate");
                if (transAttr != null)
                {
                    FloatArrayAttribute freshHours = transAttr["freshHours"] as FloatArrayAttribute;
                    FloatArrayAttribute transHours = transAttr["transitionHours"] as FloatArrayAttribute;

                    if (freshHours != null && freshHours.value.Length > 0) freshHours.value[0] *= bonus;
                    if (transHours != null && transHours.value.Length > 0) transHours.value[0] *= bonus;
                }

                // Удаляем технический атрибут, чтобы не засорять сохранение игры
                sealedCan.Attributes.RemoveAttribute("xskillsWelldoneBonus");

                inv[2].MarkDirty();
            }
        }
    }
}