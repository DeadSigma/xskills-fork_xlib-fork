using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills
{
    public static class MountedXpListener
    {
        private static bool IsBoat(Entity mount)
        {
            if (mount is EntityBoat)
            {
                return true;
            }
            string text;
            if (mount == null)
            {
                text = null;
            }
            else
            {
                AssetLocation code = mount.Code;
                if (code == null)
                {
                    text = null;
                }
                else
                {
                    string text2 = code.ToString();
                    text = ((text2 != null) ? text2.ToLowerInvariant() : null);
                }
            }
            string text3 = text;
            if (text3 == null)
            {
                return false;
            }
            foreach (string value in MountedXpListener.BoatCodeHints)
            {
                if (text3.Contains(value))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Register(ICoreServerAPI api)
        {
            MountedXpListener.Stop();
            MountedXpListener.registeredApi = api;
            MountedXpListener.listenerId = api.Event.RegisterGameTickListener(delegate (float _)
            {
                MountedXpListener.Tick(api);
            }, 2000, 0);
        }

        public static void Stop()
        {
            if (MountedXpListener.listenerId >= 0L && MountedXpListener.registeredApi != null)
            {
                MountedXpListener.registeredApi.Event.UnregisterGameTickListener(MountedXpListener.listenerId);
            }
            MountedXpListener.listenerId = -1L;
            MountedXpListener.registeredApi = null;
            MountedXpListener.lastMountPos.Clear();
        }

        private static void Tick(ICoreServerAPI api)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return;
            }
            Skill skill;
            Sailing sailing = instance.Skills.TryGetValue("sailing", out skill) ? (skill as Sailing) : null;
            Skill skill2;
            Riding riding = instance.Skills.TryGetValue("riding", out skill2) ? (skill2 as Riding) : null;
            if (sailing == null && riding == null)
            {
                return;
            }
            float num = 2f;
            float num2 = 0.05f;
            foreach (IServerPlayer serverPlayer in api.World.AllOnlinePlayers.OfType<IServerPlayer>())
            {
                EntityPlayer entity = serverPlayer.Entity;
                IMountableSeat mountableSeat = (entity != null) ? entity.MountedOn : null;
                if (mountableSeat != null)
                {
                    IMountable mountSupplier = mountableSeat.MountSupplier;
                    if (mountSupplier != null)
                    {
                        Entity entity2 = null;
                        EntityBoat entityBoat = mountSupplier as EntityBoat;
                        if (entityBoat != null)
                        {
                            entity2 = entityBoat;
                        }
                        else
                        {
                            EntityBehavior entityBehavior = mountSupplier as EntityBehavior;
                            if (entityBehavior != null)
                            {
                                entity2 = entityBehavior.entity;
                            }
                        }
                        if (((entity2 != null) ? entity2.Pos : null) != null)
                        {
                            Vec3d xyz = entity2.Pos.XYZ;
                            bool flag = false;
                            Vec3d vec3d;
                            if (MountedXpListener.lastMountPos.TryGetValue(serverPlayer.PlayerUID, out vec3d))
                            {
                                double num3 = xyz.X - vec3d.X;
                                double num4 = xyz.Y - vec3d.Y;
                                double num5 = xyz.Z - vec3d.Z;
                                flag = (num3 * num3 + num4 * num4 + num5 * num5 >= 0.04);
                            }
                            MountedXpListener.lastMountPos[serverPlayer.PlayerUID] = xyz;
                            if (flag)
                            {
                                bool flag2 = MountedXpListener.IsBoat(entity2);
                                if (flag2 && sailing != null && sailing.Enabled)
                                {
                                    PlayerSkillSet behavior = entity.GetBehavior<PlayerSkillSet>();
                                    PlayerSkill playerSkill = (behavior != null) ? behavior[sailing.Id] : null;
                                    if (playerSkill != null)
                                    {
                                        playerSkill.AddExperience(num2, true);
                                    }

                                    PlayerAbility playerAbility = (playerSkill != null) ? playerSkill[sailing.TrawlerId] : null;
                                    Skill skill3;
                                    Fishing fishing = instance.Skills.TryGetValue("fishing", out skill3) ? (skill3 as Fishing) : null;
                                    if (playerAbility != null && playerAbility.Tier > 0 && fishing != null && fishing.Enabled)
                                    {
                                        double num7 = api.World.Rand.NextDouble();
                                        double num8 = (double)playerAbility.FValue(0, 0f);
                                        if (num7 < num8)
                                        {
                                            PlayerSkillSet behavior2 = entity.GetBehavior<PlayerSkillSet>();
                                            if (behavior2 != null)
                                            {
                                                PlayerSkill playerSkill2 = behavior2[fishing.Id];
                                                if (playerSkill2 != null)
                                                {
                                                    playerSkill2.AddExperience(0.25f, true);
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (!flag2 && riding != null && riding.Enabled)
                                {
                                    PlayerSkillSet behavior3 = entity.GetBehavior<PlayerSkillSet>();
                                    PlayerSkill playerSkill3 = (behavior3 != null) ? behavior3[riding.Id] : null;
                                    if (playerSkill3 != null)
                                    {
                                        playerSkill3.AddExperience(num2, true);
                                    }

                                    PlayerAbility playerAbility2 = (playerSkill3 != null) ? playerSkill3[riding.VetId] : null;
                                    if (playerAbility2 != null && playerAbility2.Tier > 0)
                                    {
                                        float damage = (float)playerAbility2.Value(0, 0) * (num / 60f);
                                        entity2.ReceiveDamage(new DamageSource
                                        {
                                            Source = EnumDamageSource.Internal,
                                            Type = EnumDamageType.Heal
                                        }, damage);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private const int IntervalMs = 2000;
        private const float XpPerTick = 0.05f;
        private const double MinTravelSq = 0.04;
        private static readonly Dictionary<string, Vec3d> lastMountPos = new Dictionary<string, Vec3d>();
        private static long listenerId = -1L;
        private static ICoreServerAPI registeredApi;
        private static readonly string[] BoatCodeHints = new string[]
        {
            "boat",
            "raft",
            "ship",
            "sail",
            "canoe",
            "skiff",
            "kayak",
            "barge"
        };
    }
}