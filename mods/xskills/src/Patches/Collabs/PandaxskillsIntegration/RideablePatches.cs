using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace xskills.src.Patches.Collabs.PandaxskillsIntegration
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

        [HarmonyPostfix]
        [HarmonyPatch("get_SpeedMultiplier")]
        public static void GetSpeedMultiplierPostfix(EntityBehaviorRideable __instance, ref float __result)
        {
            Riding riding;
            PlayerSkill driverRidingSkill = RideablePatches.GetDriverRidingSkill(__instance, out riding);
            if (driverRidingSkill == null)
            {
                return;
            }
            float num = __result;
            PlayerAbility playerAbility = driverRidingSkill[riding.SteadyReinsId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                __result *= 1f + playerAbility.SkillDependentFValue(0);
            }
            RideablePatches.MountType mountType = RideablePatches.ClassifyMount(__instance.Mount);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.LightCavalryId, mountType == RideablePatches.MountType.Light, ref __result);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.HeavyCavalryId, mountType == RideablePatches.MountType.Heavy, ref __result);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.SkyRiderId, mountType == RideablePatches.MountType.Sky || mountType == RideablePatches.MountType.Dragon || mountType == RideablePatches.MountType.Pegasus, ref __result);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.EquusAffinityId, mountType == RideablePatches.MountType.Equus, ref __result);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.DragonBondId, mountType == RideablePatches.MountType.Dragon, ref __result);
            RideablePatches.ApplyTypeBonus(driverRidingSkill, riding.PegasusRiderId, mountType == RideablePatches.MountType.Pegasus, ref __result);
            PlayerAbility playerAbility2 = driverRidingSkill[riding.MultiSeatMasterId];
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                int num2 = RideablePatches.CountExtraPassengers(__instance);
                if (num2 > 0)
                {
                    __result *= 1f + playerAbility2.FValue(0, 0f) * (float)num2;
                }
            }
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
            Random random;
            if (entity == null)
            {
                random = null;
            }
            else
            {
                IWorldAccessor world = entity.World;
                random = ((world != null) ? world.Rand : null);
            }
            double num = (random ?? new Random()).NextDouble();
            float num2 = playerAbility.FValue(0, 0f);
            if (num < (double)num2)
            {
                return false;
            }
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