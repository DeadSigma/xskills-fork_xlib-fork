using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills.Patches
{
    // ОСНОВНОЙ ПАТЧ: ускоряет сведение прицела копья.
    // Перехватываем единственную точку, где ваниль пишет aimingAccuracy,
    // и домножаем накопленную за тик точность -> прицел сходится быстрее.
    [HarmonyPatch(typeof(EntityBehaviorAimingAccuracy))]
    public class AimingAccuracy_SwiftSpear_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnGameTick")]
        public static void OnGameTickPostfix(EntityBehaviorAimingAccuracy __instance)
        {
            // entity лежит в базовом EntityBehavior как protected-поле -> берём через Traverse.
            // Если эффекта не будет вообще — проверь имя поля в декомпиле EntityBehavior.
            Entity entity = Traverse.Create(__instance).Field("entity").GetValue<Entity>();
            if (entity is not EntityPlayer player) return;

            // Только когда в руке копьё (перк специфичен для копья)
            if (player.RightHandItemSlot?.Itemstack?.Collectible is not ItemSpear) return;

            PlayerAbility swiftSpear = SwiftSpearHelper.Get(player);
            if (swiftSpear == null || swiftSpear.Tier <= 0) return;

            float acc = entity.Attributes.GetFloat("aimingAccuracy", 0f);
            if (acc <= 0f) return;

            // Множитель скорости сведения управляется значениями ability в Combat.cs.
            float mult = SwiftSpearHelper.Multiplier(swiftSpear);

            // Не превышаем ванильный потолок точности
            float rangedAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float maxAcc = Math.Min(1f - 0.075f / rangedAcc, 1f);

            float boosted = GameMath.Clamp(acc * mult, 0f, maxAcc);
            entity.Attributes.SetFloat("aimingAccuracy", boosted);

            // ОТЛАДКА: раскомментируй, чтобы убедиться, что патч работает и видеть рост точности.
            // entity.Api.Logger.Notification("[SwiftSpear] tier={0} acc {1:0.000} -> {2:0.000}",
            //     swiftSpear.Tier, acc, boosted);
        }
    }

    // Косметика (по желанию): подгоняет визуальную анимацию замаха под перк.
    [HarmonyPatch(typeof(ItemSpear))]
    public class ItemSpear_Anim_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnHeldInteractStart")]
        public static void StartPostfix(EntityAgent byEntity)
        {
            if (byEntity.World.Side != EnumAppSide.Client) return;
            if (byEntity is not EntityPlayer player) return;

            PlayerAbility swiftSpear = SwiftSpearHelper.Get(player);
            if (swiftSpear == null || swiftSpear.Tier <= 0) return;

            if (byEntity.AnimManager.ActiveAnimationsByAnimCode.TryGetValue("aim", out var anim))
            {
                anim.AnimationSpeed = SwiftSpearHelper.Multiplier(swiftSpear);
            }
        }
    }

    internal static class SwiftSpearHelper
    {
        // Множитель скорости сведения прицела из значений ability в Combat.cs.
        // ВАЖНО: FValue(0) уже возвращает ДОЛЮ (10 в конструкторе -> 0.10),
        // поэтому делить на 100 НЕ нужно. mult = 1 + доля.
        //   {10, 20, 30}   -> +10/20/30%  (мягко: сведение ~0.50/0.46/0.42 сек)
        //   {100, 300, 700}-> x2 / x4 / x8 (как было захардкожено: ~0.27/0.14/0.07 сек)
        public static float Multiplier(PlayerAbility ability)
        {
            if (ability == null || ability.Tier <= 0) return 1f;
            return 1f + ability.FValue(0);
        }

        public static PlayerAbility Get(EntityPlayer player)
        {
            XLeveling xLeveling = XLeveling.Instance(player.Api);
            Skill combatSkill = xLeveling?.GetSkill("combat");
            if (combatSkill is Combat combatInstance)
            {
                PlayerSkillSet skillSet = player.GetBehavior<PlayerSkillSet>();
                return skillSet?[combatInstance.Id]?[combatInstance.SwiftSpearId];
            }
            return null;
        }
    }
}