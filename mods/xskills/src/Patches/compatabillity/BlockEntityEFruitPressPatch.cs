using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Патч для электрического фруктового пресса из Electrical Progressive.
    /// Переписан для мягкой зависимости (Soft Dependency), чтобы избежать краша без мода.
    /// </summary>
    public class BlockEntityEFruitPressPatch
    {
        // Вызывай этот метод вручную во время инициализации мода
        public static void Apply(Harmony harmony)
        {
            if (!Prepare()) return;

            // Ищем тип по строке. Если мода нет, вернется null и мы просто выйдем без краша.
            Type targetType = AccessTools.TypeByName("ElectricalProgressive.Content.Block.EFruitPress.BlockEntityEFruitPress");
            if (targetType == null) return;

            // Патчим OnPlayerRightClick
            MethodInfo onRightClick = AccessTools.Method(targetType, "OnPlayerRightClick");
            if (onRightClick != null)
                harmony.Patch(onRightClick, postfix: new HarmonyMethod(typeof(BlockEntityEFruitPressPatch), nameof(OnPlayerRightClickPostfix)));

            // Патчим ExtractJuice
            MethodInfo extractJuice = AccessTools.Method(targetType, "ExtractJuice");
            if (extractJuice != null)
                harmony.Patch(extractJuice,
                    prefix: new HarmonyMethod(typeof(BlockEntityEFruitPressPatch), nameof(ExtractJuicePrefix)),
                    postfix: new HarmonyMethod(typeof(BlockEntityEFruitPressPatch), nameof(ExtractJuicePostfix)));

            // Патчим GetJuiceableProperties
            MethodInfo getJuiceableProperties = AccessTools.Method(targetType, "GetJuiceableProperties");
            if (getJuiceableProperties != null)
                harmony.Patch(getJuiceableProperties, postfix: new HarmonyMethod(typeof(BlockEntityEFruitPressPatch), nameof(GetJuiceablePropertiesPostfix)));
        }

        public static bool Prepare()
        {
            XSkills xSkills = XSkills.Instance;
            if (xSkills == null) return false;

            if (!xSkills.Skills.TryGetValue("cooking", out Skill skill)) return false;
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return false;
            return cooking[cooking.JuicerId].Enabled;
        }

        // ВАЖНО: Используем базовый BlockEntity вместо BlockEntityEFruitPress
        public static void OnPlayerRightClickPostfix(BlockEntity __instance, bool __result, IPlayer byPlayer)
        {
            if (__result == false) return;

            BlockEntityBehaviorOwnable ownable = __instance?.GetBehavior<BlockEntityBehaviorOwnable>();
            if (ownable == null) return;

            ownable.Owner = byPlayer;
        }

        public static void ExtractJuicePrefix(BlockEntity __instance, ref float __state)
        {
            // Используем dynamic для доступа к свойству чужого мода
            dynamic dynInstance = __instance;
            __state = dynInstance.LiquidAmount;
        }

        public static void ExtractJuicePostfix(BlockEntity __instance, float __state)
        {
            dynamic dynInstance = __instance;
            float litres = dynInstance.LiquidAmount;
            float diff = litres - __state;

            if (diff <= 0) return;

            BlockEntityBehaviorOwnable ownable = __instance?.GetBehavior<BlockEntityBehaviorOwnable>();
            IPlayer player = ownable?.Owner;

            if (player == null) return;

            Cooking cooking = XLeveling.Instance(__instance.Api)?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerSkill skill = player.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id];
            if (skill == null) return;

            float exp = (cooking.Config as CookingSkillConfig).fruitPressExpPerLitre;
            skill.AddExperience(diff * exp);
        }

        public static void GetJuiceablePropertiesPostfix(BlockEntity __instance, ref object __result)
        {
            if (__result == null) return;

            dynamic props = __result;
            if (props.LitresPerItem == null) return;

            BlockEntityBehaviorOwnable ownable = __instance?.GetBehavior<BlockEntityBehaviorOwnable>();
            IPlayer player = ownable?.Owner;

            if (player == null) return;

            Cooking cooking = XLeveling.Instance(__instance.Api)?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerAbility ability = player.Entity?.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.JuicerId];
            if (ability == null) return;

            int value = ability.Value(0);
            float before = props.LitresPerItem;

            if (value == 33) props.LitresPerItem *= 1.0f + 1.0f / 3.0f;
            else if (value == 66) props.LitresPerItem *= 2.0f - 1.0f / 3.0f;
            else props.LitresPerItem *= 1.0f + value * 0.01f;
        }
    }
}