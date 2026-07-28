using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills
{
    [HarmonyPatch(typeof(EntityBoatConstruction))]
    public class BoatConstructionPatch
    {
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
        [HarmonyPatch("OnInteract")]
        public static void OnInteractPostfix(EntityBoatConstruction __instance, EntityAgent byEntity, ItemSlot handslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
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
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[sailing.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[sailing.ShipwrightId] : null);
            }
            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 == null || playerAbility2.Tier <= 0)
            {
                return;
            }
            double num = entityPlayer.World.Rand.NextDouble();
            double num2 = (double)playerAbility2.FValue(0, 0f);
            if (num < num2)
            {
                try
                {
                    PropertyInfo property = typeof(EntityBoatConstruction).GetProperty("CurrentStage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null)
                    {
                        int num3 = (int)property.GetValue(__instance);
                        int num4 = Math.Max(0, num3 + 1);
                        property.SetValue(__instance, num4);
                    }
                }
                catch
                {
                }
            }
        }
    }
}