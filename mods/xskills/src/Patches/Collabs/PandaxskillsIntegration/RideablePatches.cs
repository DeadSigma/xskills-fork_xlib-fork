using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;


namespace XSkills
{
    [HarmonyPatch(typeof(EntityBehaviorRideable))]
    public class RideablePatches
    {
        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("riding", out skill);
            Riding riding = skill as Riding;
            return riding != null && riding.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("DidMount")]
        public static void DidMountPostfix(EntityBehaviorRideable __instance, EntityAgent entityAgent)
        {
            if (entityAgent == null)
            {
                return;
            }
            if (!RideablePatches.drivers.ContainsKey(__instance))
            {
                RideablePatches.drivers[__instance] = entityAgent;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("DidUnmount")]
        public static void DidUnmountPostfix(EntityBehaviorRideable __instance, EntityAgent entityAgent)
        {
            EntityAgent entityAgent2;
            if (RideablePatches.drivers.TryGetValue(__instance, out entityAgent2) && entityAgent2 == entityAgent)
            {
                RideablePatches.drivers.Remove(__instance);
            }
            EntityPlayer entityPlayer = entityAgent as EntityPlayer;
            if (entityPlayer != null)
            {
                IPlayer player = entityPlayer.Player;
                if (((player != null) ? player.PlayerUID : null) != null)
                {
                    RideablePatches.lastDismountMsByPlayerUid[entityPlayer.Player.PlayerUID] = entityPlayer.World.ElapsedMilliseconds;
                }
            }
        }

        public static bool RecentlyDismounted(Entity entity, long windowMs)
        {
            EntityPlayer entityPlayer = entity as EntityPlayer;
            if (entityPlayer != null)
            {
                IPlayer player = entityPlayer.Player;
                if (((player != null) ? player.PlayerUID : null) != null)
                {
                    long num;
                    return RideablePatches.lastDismountMsByPlayerUid.TryGetValue(entityPlayer.Player.PlayerUID, out num) && entityPlayer.World.ElapsedMilliseconds - num <= windowMs;
                }
            }
            return false;
        }

        internal static PlayerSkill GetDriverRidingSkill(EntityBehaviorRideable behavior, out Riding ridingSkill)
        {
            ridingSkill = null;
            if (behavior == null)
            {
                return null;
            }
            EntityAgent entityAgent;
            if (!RideablePatches.drivers.TryGetValue(behavior, out entityAgent))
            {
                return null;
            }
            EntityPlayer entityPlayer = entityAgent as EntityPlayer;
            if (entityPlayer == null)
            {
                return null;
            }
            XSkills instance = XSkills.Instance;
            Skill skill;
            ridingSkill = ((instance != null && instance.Skills.TryGetValue("riding", out skill)) ? (skill as Riding) : null);
            if (ridingSkill == null)
            {
                return null;
            }
            PlayerSkillSet behavior2 = entityPlayer.GetBehavior<PlayerSkillSet>();
            if (behavior2 == null)
            {
                return null;
            }
            return behavior2[ridingSkill.Id];
        }

        public static RideablePatches.MountType ClassifyMount(Entity mount)
        {
            if (((mount != null) ? mount.Code : null) == null)
            {
                return RideablePatches.MountType.Unknown;
            }
            string a = mount.Code.Domain ?? "";
            string text = mount.Code.Path ?? "";
            if (a == "draconis" || a == "draconisrhinocroma" || text.Contains("dragon"))
            {
                return RideablePatches.MountType.Dragon;
            }
            if (a == "pegasus" || text.Contains("pegasus"))
            {
                return RideablePatches.MountType.Pegasus;
            }
            if (a == "equus")
            {
                return RideablePatches.MountType.Equus;
            }
            if (text.Contains("bullsiver") || text.Contains("bull"))
            {
                return RideablePatches.MountType.Heavy;
            }
            if (text.Contains("horse") || text.Contains("donkey") || text.Contains("mule") || text.Contains("elk"))
            {
                return RideablePatches.MountType.Light;
            }
            return RideablePatches.MountType.Unknown;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityBehaviorGait), "Move")]
        public static void GaitMovePrefix(EntityBehaviorGait __instance)
        {
            Entity entity = __instance.entity;
            if (entity == null) return;

            EntityBehaviorRideable rideable = entity.GetBehavior<EntityBehaviorRideable>();
            if (rideable == null) return;

            Riding riding;
            PlayerSkill driverRidingSkill = RideablePatches.GetDriverRidingSkill(rideable, out riding);

            if (driverRidingSkill == null)
            {
                __instance.MoveSpeedModifier = 1.0;
                return;
            }

            // Расчет бонусов скорости
            float speedMultiplier = 1f;

            // Перк Steady Reins
            PlayerAbility steadyReins = driverRidingSkill[riding.SteadyReinsId];
            if (steadyReins != null && steadyReins.Tier > 0)
            {
                speedMultiplier += steadyReins.SkillDependentFValue(0);
            }

            // Перк Multi Seat Master (пассажиры)
            PlayerAbility multiSeat = driverRidingSkill[riding.MultiSeatMasterId];
            if (multiSeat != null && multiSeat.Tier > 0)
            {
                int passengers = RideablePatches.CountExtraPassengers(rideable);
                if (passengers > 0)
                {
                    speedMultiplier += multiSeat.FValue(0, 0f) * (float)passengers;
                }
            }

            // 5. Применяем бонусы от типа животного (используем ваши оригинальные методы)
            RideablePatches.MountType mountType = RideablePatches.ClassifyMount(rideable.Mount);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.LightCavalryId, mountType == RideablePatches.MountType.Light, ref speedMultiplier);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.HeavyCavalryId, mountType == RideablePatches.MountType.Heavy, ref speedMultiplier);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.SkyRiderId, mountType == RideablePatches.MountType.Sky || mountType == RideablePatches.MountType.Dragon || mountType == RideablePatches.MountType.Pegasus, ref speedMultiplier);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.EquusAffinityId, mountType == RideablePatches.MountType.Equus, ref speedMultiplier);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.DragonBondId, mountType == RideablePatches.MountType.Dragon, ref speedMultiplier);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.PegasusRiderId, mountType == RideablePatches.MountType.Pegasus, ref speedMultiplier);

            // 6. Записываем итоговый результат в переменную ванильного класса
            __instance.MoveSpeedModifier = speedMultiplier;
        }

        private static int CountExtraPassengers(EntityBehaviorRideable behavior)
        {
            int result;
            try
            {
                if (behavior == null)
                {
                    result = 0;
                }
                else
                {
                    int num = 0;
                    if (((IMountable)behavior).Seats == null)
                    {
                        result = 0;
                    }
                    else
                    {
                        foreach (IMountableSeat mountableSeat in ((IMountable)behavior).Seats)
                        {
                            EntityAgent entityAgent;
                            if (((mountableSeat != null) ? mountableSeat.Passenger : null) != null && (!RideablePatches.drivers.TryGetValue(behavior, out entityAgent) || mountableSeat.Passenger != entityAgent))
                            {
                                num++;
                            }
                        }
                        result = num;
                    }
                }
            }
            catch
            {
                result = 0;
            }
            return result;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EntityBehaviorRideableAccessories), "EntityBehaviorDressable_CanRide")]
        public static void CanRidePostfix(EntityBehaviorRideableAccessories __instance, IMountableSeat seat, ref string errorMessage, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            EntityPlayer entityPlayer = ((seat != null) ? seat.Passenger : null) as EntityPlayer;
            if (entityPlayer == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Skill skill;
            Riding riding = (instance != null && instance.Skills.TryGetValue("riding", out skill)) ? (skill as Riding) : null;
            if (riding == null)
            {
                return;
            }
            PlayerSkillSet behavior = entityPlayer.GetBehavior<PlayerSkillSet>();
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[riding.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[riding.LightTackId] : null);
            }
            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                __result = true;
                errorMessage = null;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("DoSaddleBreak")]
        public static bool DoSaddleBreakPrefix(EntityBehaviorRideable __instance)
        {
            Riding riding;
            PlayerSkill driverRidingSkill = RideablePatches.GetDriverRidingSkill(__instance, out riding);
            if (driverRidingSkill == null)
            {
                return true;
            }
            PlayerAbility playerAbility = driverRidingSkill[riding.TirelessTrotId];
            if (playerAbility == null || playerAbility.Tier <= 0)
            {
                return true;
            }

            Entity entity = __instance.entity;
            Random random = (entity != null && entity.World != null) ? entity.World.Rand : new Random();

            double num = random.NextDouble();
            float num2 = playerAbility.FValue(0, 0f);

            // Если перк сработал (игрок удерживается в седле)
            if (num < (double)num2)
            {
                // 1. СБРОС ТАЙМЕРА! Даем еще 4 секунды спокойной езды.
                AccessTools.Field(typeof(EntityBehaviorRideable), "mountedTotalMs").SetValue(__instance, entity.Api.World.ElapsedMilliseconds);

                // 2. Успокаиваем животное (гасим флаг прыжка и переводим в Idle)
                AccessTools.Field(typeof(EntityBehaviorRideable), "jumpNow").SetValue(__instance, false);
                var ebg = AccessTools.Field(typeof(EntityBehaviorRideable), "ebg").GetValue(__instance);
                if (ebg != null)
                {
                    AccessTools.Method(ebg.GetType(), "SetIdle")?.Invoke(ebg, null);
                }

                // 3. Продвигаем прогресс приручения
                float interval = (float)AccessTools.Field(typeof(EntityBehaviorRideable), "saddleBreakDayInterval").GetValue(__instance);
                double currentDays = __instance.entity.Api.World.Calendar.TotalDays;

                if (currentDays - __instance.LastSaddleBreakTotalDays > (double)interval)
                {
                    __instance.RemainingSaddleBreaks--;
                    __instance.LastSaddleBreakTotalDays = currentDays;

                    if (__instance.RemainingSaddleBreaks <= 0)
                    {
                        // Вызываем скрытый метод превращения животного в прирученное
                        AccessTools.Method(typeof(EntityBehaviorRideable), "ConvertToTamedAnimal").Invoke(__instance, null);
                    }
                }

                // Отменяем ванильный метод
                return false;
            }

            // Иначе запускаем ванильный метод (животное сбрасывает игрока)
            return true;
        }

        private static void ApplyTypeBonus(PlayerSkill skill, int abilityId, bool matches, ref float result)
        {
            if (!matches || abilityId < 0)
            {
                return;
            }
            PlayerAbility playerAbility = skill[abilityId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                result *= 1f + playerAbility.FValue(0, 0f);
            }
        }

        private static readonly Dictionary<EntityBehaviorRideable, EntityAgent> drivers = new Dictionary<EntityBehaviorRideable, EntityAgent>();
        private static readonly Dictionary<string, long> lastDismountMsByPlayerUid = new Dictionary<string, long>();

        public enum MountType
        {
            Unknown,
            Light,
            Heavy,
            Sky,
            Equus,
            Dragon,
            Pegasus
        }
    }
}