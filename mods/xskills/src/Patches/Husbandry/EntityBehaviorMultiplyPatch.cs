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

        public static void ApplyBreederPerk(EntityBehaviorMultiply __instance)
        {
            IPlayer player = __instance.entity?.GetBehavior<XSkillsAnimalBehavior>()?.Feeder;
            if (player == null) return;

            Husbandry husbandry = XLeveling.Instance(__instance.entity.World.Api).GetSkill("husbandry") as Husbandry;
            if (husbandry == null) return;

            PlayerSkill playerSkill = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
            PlayerAbility playerAbility = playerSkill?[husbandry.BreederId];

            if (playerAbility != null && playerAbility.Tier > 0)
            {
                ITreeAttribute multiplyTree = __instance.entity.WatchedAttributes.GetTreeAttribute("multiply");

                // Берем оригинальное время, которое мы сохранили при инициализации
                float basePregnancyDays = multiplyTree?.GetFloat("basePregnancyDays", 0.0f) ?? 0.0f;

                // Если по какой-то причине его там нет, тащим рефлексией (21.0f на крайний случай)
                if (basePregnancyDays <= 0.0f)
                {
                    FieldInfo field = typeof(EntityBehaviorMultiply).GetField("pregnancyDays", BindingFlags.Instance | BindingFlags.NonPublic);
                    basePregnancyDays = field != null ? (float)field.GetValue(__instance) : 21.0f;
                }

                int baseBonus = playerAbility.Value(0);      // 10
                int perLevelBonus = playerAbility.Value(1);  // 2
                int perGenBonus = playerAbility.Value(2);    // 1
                int maxBonus = playerAbility.Value(3);       // 60

                int currentLevel = playerAbility.PlayerSkill.Level;
                int animalGen = __instance.entity.WatchedAttributes.GetInt("generation", 0);

                float calculatedBonus = baseBonus + (currentLevel * perLevelBonus) + (animalGen * perGenBonus);
                float finalBonus = Math.Min(calculatedBonus, maxBonus);

                float reductionPercent = finalBonus / 100f;

                reductionPercent = Math.Min(reductionPercent, 0.9f);
                float newPregnancyDays = basePregnancyDays * (1.0f - reductionPercent);

                FieldInfo fieldSet = typeof(EntityBehaviorMultiply).GetField("pregnancyDays", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldSet != null)
                {
                    fieldSet.SetValue(__instance, newPregnancyDays);
                }

                if (multiplyTree != null)
                {
                    multiplyTree.SetFloat("pregnancyDays", newPregnancyDays);
                }
            }
        }

        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(EntityBehaviorMultiply __instance)
        {
            ITreeAttribute multiplyTree = __instance.entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (multiplyTree == null) return;

            // КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ: Сохраняем исходное базовое значение игры в дерево
            // до того, как перки успеют его уменьшить
            if (!multiplyTree.HasAttribute("basePregnancyDays"))
            {
                FieldInfo field = typeof(EntityBehaviorMultiply).GetField("pregnancyDays", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    float origPregnancyDays = (float)field.GetValue(__instance);
                    multiplyTree.SetFloat("basePregnancyDays", origPregnancyDays);
                }
            }

            ApplyBreederPerk(__instance);
        }

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
        [HarmonyPatch(typeof(EntityBehaviorMultiply))]
        public static class EntityBehaviorMultiplyBirthPatch
        {
            [HarmonyPatch("GiveBirth")]
            [HarmonyPostfix]
            public static void BirthPostfix(EntityBehaviorMultiply __instance)
            {
                XSkillsAnimalBehavior animal = __instance.entity?.GetBehavior<XSkillsAnimalBehavior>();
                if (animal == null) return;

                IPlayer player = animal.Feeder;
                if (player == null) return;

                Husbandry husbandry = XLeveling.Instance(__instance.entity.World.Api).GetSkill("husbandry") as Husbandry;
                if (husbandry == null) return;

                PlayerSkill playerSkill = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
                if (playerSkill == null) return;

                float xpReward = animal.XP * 1.5f;
                playerSkill.AddExperience(xpReward);
            }
        }
    }//!class EntityBehaviorMultiplyPatch
}//!namespace XSkills