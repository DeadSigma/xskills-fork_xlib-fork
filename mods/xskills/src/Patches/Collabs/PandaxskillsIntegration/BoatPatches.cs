using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills
{
    [HarmonyPatch(typeof(EntityBoat))]
    public class BoatPatches
    {
        private static readonly Dictionary<EntityBoat, EntityAgent> captains = new Dictionary<EntityBoat, EntityAgent>();

        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("sailing", out skill);
            Sailing sailing = skill as Sailing;
            return sailing != null && sailing.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("DidMount")]
        public static void DidMountPostfix(EntityBoat __instance, EntityAgent entityAgent)
        {
            if (entityAgent == null)
            {
                return;
            }
            if (!BoatPatches.captains.ContainsKey(__instance))
            {
                BoatPatches.captains[__instance] = entityAgent;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("DidUnmount")]
        public static void DidUnmountPostfix(EntityBoat __instance, EntityAgent entityAgent)
        {
            EntityAgent entityAgent2;
            if (BoatPatches.captains.TryGetValue(__instance, out entityAgent2) && entityAgent2 == entityAgent)
            {
                BoatPatches.captains.Remove(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("get_SpeedMultiplier")]
        public static void GetSpeedMultiplierPostfix(EntityBoat __instance, ref float __result)
        {
            EntityAgent entityAgent;
            if (!BoatPatches.captains.TryGetValue(__instance, out entityAgent))
            {
                return;
            }
            EntityPlayer entityPlayer = entityAgent as EntityPlayer;
            if (entityPlayer == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Sailing sailing = ((instance != null) ? instance.Skills["sailing"] : null) as Sailing;
            if (sailing == null)
            {
                return;
            }
            PlayerSkillSet behavior = entityPlayer.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[sailing.Id] : null;
            if (playerSkill == null)
            {
                return;
            }
            float num = __result;
            PlayerAbility playerAbility = playerSkill[sailing.SteadyHelmId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                __result *= 1f + playerAbility.SkillDependentFValue(0);
            }
            AssetLocation code = __instance.Code;
            string text = ((code != null) ? code.Path : null) ?? "";
            if (text.Contains("rowboat"))
            {
                PlayerAbility playerAbility2 = playerSkill[sailing.StrongOarsmanId];
                if (playerAbility2 != null && playerAbility2.Tier > 0)
                {
                    __result *= 1f + playerAbility2.FValue(0, 0f);
                }
            }
            int num2 = BoatPatches.CountExtraCrew(__instance, entityAgent);
            if (num2 > 0)
            {
                PlayerAbility playerAbility3 = playerSkill[sailing.CrewCaptainId];
                if (playerAbility3 != null && playerAbility3.Tier > 0)
                {
                    __result *= 1f + playerAbility3.FValue(0, 0f) * (float)num2;
                }
            }
            BoatPatches.WaterType waterType = BoatPatches.ClassifyWater(__instance);
            BoatPatches.ApplySpec(playerSkill, sailing.CoastalPilotId, waterType == BoatPatches.WaterType.Coastal, ref __result);
            BoatPatches.ApplySpec(playerSkill, sailing.OpenSeaId, waterType == BoatPatches.WaterType.OpenSea, ref __result);
            BoatPatches.ApplySpec(playerSkill, sailing.RiverRunnerId, waterType == BoatPatches.WaterType.River, ref __result);
            if (sailing.AirshipCaptainId >= 0)
            {
                if (!text.Contains("airship"))
                {
                    AssetLocation code2 = __instance.Code;
                    if (!(((code2 != null) ? code2.Domain : null) ?? "").Contains("airship"))
                    {
                        return;
                    }
                }
                PlayerAbility playerAbility4 = playerSkill[sailing.AirshipCaptainId];
                if (playerAbility4 != null && playerAbility4.Tier > 0)
                {
                    __result *= 1f + playerAbility4.FValue(0, 0f);
                }
            }
        }

        private static void ApplySpec(PlayerSkill skill, int abilityId, bool matches, ref float result)
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

        private static int CountExtraCrew(EntityBoat boat, EntityAgent captain)
        {
            int result;
            try
            {
                IMountable mountable = boat as IMountable;
                if (mountable == null || mountable.Seats == null)
                {
                    result = 0;
                }
                else
                {
                    int num = 0;
                    foreach (IMountableSeat mountableSeat in mountable.Seats)
                    {
                        if (((mountableSeat != null) ? mountableSeat.Passenger : null) != null && mountableSeat.Passenger != captain)
                        {
                            num++;
                        }
                    }
                    result = num;
                }
            }
            catch
            {
                result = 0;
            }
            return result;
        }

        private static BoatPatches.WaterType ClassifyWater(EntityBoat boat)
        {
            if (((boat != null) ? boat.Pos : null) == null)
            {
                return BoatPatches.WaterType.Unknown;
            }
            try
            {
                BlockPos blockPos = boat.Pos.AsBlockPos.DownCopy(1);
                Block block = boat.World.BlockAccessor.GetBlock(blockPos);
                string text;
                if (block == null)
                {
                    text = null;
                }
                else
                {
                    AssetLocation code = block.Code;
                    text = ((code != null) ? code.Path : null);
                }
                string text2 = text ?? "";
                if (text2.Contains("saltwater"))
                {
                    int i = 0;
                    BlockPos blockPos2 = blockPos.Copy();
                    while (i < 16)
                    {
                        AssetLocation code2 = boat.World.BlockAccessor.GetBlock(blockPos2).Code;
                        bool flag;
                        if (code2 == null)
                        {
                            flag = false;
                        }
                        else
                        {
                            string path = code2.Path;
                            flag = ((path != null) ? new bool?(path.Contains("water")) : null).GetValueOrDefault();
                        }
                        if (!flag)
                        {
                            break;
                        }
                        i++;
                        blockPos2.Down(1);
                    }
                    return (i >= 8) ? BoatPatches.WaterType.OpenSea : BoatPatches.WaterType.Coastal;
                }
                if (text2.Contains("water"))
                {
                    return BoatPatches.WaterType.River;
                }
            }
            catch
            {
            }
            return BoatPatches.WaterType.Unknown;
        }

        public static PlayerSkill GetCaptainSailingSkill(EntityBoat boat, out Sailing sailing)
        {
            XSkills instance = XSkills.Instance;
            sailing = (((instance != null) ? instance.Skills["sailing"] : null) as Sailing);
            if (sailing == null)
            {
                return null;
            }
            EntityAgent entityAgent;
            if (!BoatPatches.captains.TryGetValue(boat, out entityAgent))
            {
                return null;
            }
            EntityPlayer entityPlayer = entityAgent as EntityPlayer;
            if (entityPlayer == null)
            {
                return null;
            }
            PlayerSkillSet behavior = entityPlayer.GetBehavior<PlayerSkillSet>();
            if (behavior == null)
            {
                return null;
            }
            return behavior[sailing.Id];
        }

        public enum WaterType
        {
            Unknown,
            Coastal,
            OpenSea,
            River
        }
    }
}