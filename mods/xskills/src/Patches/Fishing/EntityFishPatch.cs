// Основано на моде pandaxskills от Pandarific, кроме FreshFish
using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(EntityFish))]
    public class EntityFishPatch
    {
        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("fishing", out skill);
            Fishing fishing = skill as Fishing;
            return fishing != null && fishing.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("get_BaitBobberSeekChance")]
        public static void GetBaitBobberSeekChancePostfix(EntityFish __instance, ref double __result)
        {
            __result *= (double)EntityFishPatch.BiteRateMultiplier(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("get_NoBaitBobberSeekChance")]
        public static void GetNoBaitBobberSeekChancePostfix(EntityFish __instance, ref double __result)
        {
            __result *= (double)EntityFishPatch.BiteRateMultiplier(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityBobber), "TryCatchFish")]
        public static void TryCatchFishPrefix(EntityBobber __instance, out bool __state)
        {
            __state = (__instance.caughtFish != null && __instance.caughtFish.Alive);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EntityBobber), "TryCatchFish")]
        public static void TryCatchFishPostfix(EntityAgent entityCatcher, bool __state)
        {
            if (!__state) return;

            EntityPlayer entityPlayer = entityCatcher as EntityPlayer;
            IPlayer player = entityPlayer?.Player;
            if (player == null) return;

            Fishing fishing = XSkills.Instance.Skills["fishing"] as Fishing;
            if (fishing == null || !fishing.Enabled) return;

            PlayerSkillSet behavior = entityPlayer.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[fishing.Id] : null;
            if (playerSkill == null) return;

            // Базовый опыт за пойманную рыбу
            playerSkill.AddExperience(1f, true);

            // Перк "Свежее филе": метим только что пойманную рыбу
            PlayerAbility freshFish = playerSkill[fishing.FreshFishId];
            if (freshFish == null || freshFish.Tier <= 0) return;

            // Value(0,0) = процент из конфига перка (25 / 50 / 75)
            float bonus = freshFish.Value(0, 0) / 100f;
            if (bonus <= 0f) return;

            // Множитель скорости гниения
            // 1/(1+bonus) даёт срок годности РОВНО +25/50/75%
            float perishMultiplier = 1f / (1f + bonus);

            if (player.InventoryManager == null) return;

            // Проходимся по инвентарям и метим свежую рыбу
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                if (inventory == null) continue;

                foreach (ItemSlot itemSlot in inventory)
                {
                    ItemStack stack = itemSlot?.Itemstack;
                    if (stack?.Collectible?.Code?.Path == null) continue;
                    if (!stack.Collectible.Code.Path.Contains("fish")) continue;

                    // Если у рыбы ещё нет нашей метки свежести - вешаем её
                    if (!stack.Attributes.HasAttribute("freshFishMul"))
                    {
                        stack.Attributes.SetFloat("freshFishMul", perishMultiplier);
                        itemSlot.MarkDirty();
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CollectibleObject), "GetTransitionRateMul")]
        public static void GetTransitionRateMulPostfix(CollectibleObject __instance, ItemSlot inSlot, EnumTransitionType transType, ref float __result)
        {
            // Только процесс гниения (Perish)
            if (transType != EnumTransitionType.Perish) return;

            if (__instance?.Code?.Path == null || !__instance.Code.Path.Contains("fish")) return;

            ItemStack stack = inSlot?.Itemstack;
            if (stack?.Attributes == null) return;

            // Если на рыбе есть метка перка "Свежая Рыба"
            if (!stack.Attributes.HasAttribute("freshFishMul")) return;

            float multiplier = stack.Attributes.GetFloat("freshFishMul", 1f);
            __result *= multiplier;
        }

        private static float BiteRateMultiplier(EntityFish fish)
        {
            IPlayer player = fish.World.NearestPlayer(fish.Pos.X, fish.Pos.Y, fish.Pos.Z);
            if (player?.Entity == null)
            {
                return 1f;
            }
            double num = player.Entity.Pos.X - fish.Pos.X;
            double num2 = player.Entity.Pos.Y - fish.Pos.Y;
            double num3 = player.Entity.Pos.Z - fish.Pos.Z;
            if (num * num + num2 * num2 + num3 * num3 > 100.0)
            {
                return 1f;
            }
            Fishing fishing = XSkills.Instance.Skills["fishing"] as Fishing;
            if (fishing == null)
            {
                return 1f;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[fishing.Id] : null;
            if (playerSkill == null)
            {
                return 1f;
            }

            float num4 = 1f;

            // MoonlitWatersId логика
            PlayerAbility playerAbility2 = playerSkill[fishing.MoonlitWatersId];
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                IGameCalendar calendar = fish.World.Calendar;
                double num5 = (double)calendar.HoursPerDay;
                double num6 = (double)calendar.HourOfDay;
                if (num6 < num5 * 0.25 || num6 >= num5 * 0.75)
                {
                    num4 *= 1f + playerAbility2.FValue(0, 0f);
                }
            }

            PlayerAbility playerAbility3 = null;
            PlayerAbility playerAbility4 = null;

            try
            {
                // Исправлено ServerPos на Pos (CS0618)
                BlockPos asBlockPos = fish.Pos.AsBlockPos;
                Block block = fish.World.BlockAccessor.GetBlock(asBlockPos);
                string text2 = block?.Code?.Path ?? "";
                if (text2.Contains("saltwater"))
                {
                    playerAbility3 = playerSkill[fishing.CoastalMasterId];
                    if (playerAbility3 != null && playerAbility3.Tier > 0)
                    {
                        num4 *= 1f + playerAbility3.FValue(0, 0f);
                    }
                }
                else if (text2.Contains("water"))
                {
                    playerAbility4 = playerSkill[fishing.RiverSpecialistId];
                    if (playerAbility4 != null && playerAbility4.Tier > 0)
                    {
                        num4 *= 1f + playerAbility4.FValue(0, 0f);
                    }
                }
            }
            catch
            {
            }

            return num4;
        }
    }
}