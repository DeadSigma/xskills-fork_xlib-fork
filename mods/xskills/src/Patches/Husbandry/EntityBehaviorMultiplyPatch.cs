using HarmonyLib;
using System;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(EntityBehaviorMultiply))]
    public static class EntityBehaviorMultiplyPatch
    {
        public static float GetPregnancyDays(this EntityBehaviorMultiply multiply)
        {
            // Добавлена проверка на null для дерева атрибутов "multiply"
            ITreeAttribute multiplyTree = multiply.entity.WatchedAttributes.GetTreeAttribute("multiply");
            return multiplyTree != null ? multiplyTree.GetFloat("pregnancyDays", 3.0f) : 3.0f;
        }

        public static void SetPregnancyDays(this EntityBehaviorMultiply multiply, float days)
        {
            // Добавлена проверка на null перед записью
            ITreeAttribute multiplyTree = multiply.entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (multiplyTree != null) multiplyTree.SetFloat("pregnancyDays", days);
        }


        [HarmonyPatch("SpawnQuantityMin", MethodType.Getter)]
        public static void Postfix1(EntityBehaviorMultiply __instance, ref float __result)
        {
            IPlayer player = __instance.entity?.GetBehavior<XSkillsAnimalBehavior>()?.Feeder;
            if (player == null) return;

            Husbandry husbandry = XLeveling.Instance(__instance.entity.World.Api).GetSkill("husbandry") as Husbandry;
            if (husbandry == null) return;
            PlayerSkill playerSkill = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
            if (playerSkill == null) return;
            PlayerAbility playerAbility = playerSkill[husbandry.BreederId];
            if (playerAbility == null) return;
            __result += playerAbility.Value(playerAbility.Tier);
        }

        [HarmonyPatch("SpawnQuantityMax", MethodType.Getter)]
        public static void Postfix2(EntityBehaviorMultiply __instance, ref float __result)
        {
            IPlayer player = __instance.entity?.GetBehavior<XSkillsAnimalBehavior>()?.Feeder;
            if (player == null) return;

            Husbandry husbandry = XLeveling.Instance(__instance.entity.World.Api).GetSkill("husbandry") as Husbandry;
            if (husbandry == null) return;
            PlayerSkill playerSkill = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
            if (playerSkill == null) return;
            PlayerAbility playerAbility = playerSkill[husbandry.BreederId];
            if (playerAbility == null) return;
            __result += playerAbility.Value(playerAbility.Tier);
        }

        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(EntityBehaviorMultiply __instance)
        {
            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ ДЛЯ 1.22:
            // Проверяем существование дерева атрибутов "multiply", чтобы избежать NullReferenceException
            ITreeAttribute multiplyTree = __instance.entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (multiplyTree == null) return;

            float pregnancyDays = multiplyTree.GetFloat("pregnancyDays", 0.0f);
            if (pregnancyDays <= 0.0f)
            {
                // Попытка получить значение через рефлексию, если в атрибутах пусто
                FieldInfo field = typeof(EntityBehaviorMultiply).GetField("pregnancyDays", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    pregnancyDays = (float)field.GetValue(__instance);
                    multiplyTree.SetFloat("pregnancyDays", pregnancyDays);
                }
            }
        }

        /*[HarmonyPatch("GetInteractionHelp")]
        [HarmonyPrefix]
        public static bool GetInteractionHelpPrefix(EntityBehaviorMultiply __instance, IWorldAccessor world, ref WorldInteraction[] interactions, ref EnumHandling handling)
        {
            return true;
        }
        */

        [HarmonyPatch("GetInfoText")]
        [HarmonyPrefix]
        public static bool GetInfoTextPrefix(EntityBehaviorMultiply __instance, StringBuilder infotext)
        {
            IPlayer player = (XLeveling.Instance(__instance.entity.World.Api).Api as ICoreClientAPI)?.World.Player;
            if (player == null) return true;
            Husbandry husbandry = XLeveling.Instance(__instance.entity.World.Api).GetSkill("husbandry") as Husbandry;
            if (husbandry == null) return true;
            PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id][husbandry.BreederId];
            if (!(playerAbility?.Tier > 0)) return true;

            if (__instance.IsPregnant)
            {
                float pregnancyDays = __instance.GetPregnancyDays();
                double pregnantDays = __instance.entity.World.Calendar.TotalDays - __instance.TotalDaysPregnancyStart;
                infotext.AppendLine(Lang.Get("Is pregnant") + string.Format(" ({0:N1}/{1:N1})", pregnantDays, pregnancyDays));
            }
            else if (__instance.entity.Alive)
            {
                ITreeAttribute tree = __instance.entity.WatchedAttributes.GetTreeAttribute("hunger");
                if (tree != null)
                {
                    float saturation = tree.GetFloat("saturation", 0);
                    infotext.AppendLine(Lang.Get("Portions eaten: {0}", saturation));
                }

                double daysLeft = __instance.TotalDaysCooldownUntil - __instance.entity.World.Calendar.TotalDays;
                if (daysLeft <= 0) infotext.AppendLine(Lang.Get("Ready to mate"));
                else infotext.AppendLine(Lang.Get("xskills:ready-to-mate", daysLeft));
            }
            return false;
        }
    }//!class EntityBehaviorMultiplyPatch
}//!namespace XSkills