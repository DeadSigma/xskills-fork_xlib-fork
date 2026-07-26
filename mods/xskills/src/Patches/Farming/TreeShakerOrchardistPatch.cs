using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Installs the optional Tree Shaker compatibility after all mod assemblies
    /// have been loaded. This avoids a compile-time dependency on Tree Shaker.
    /// </summary>
    public sealed class TreeShakerOrchardistCompatibilitySystem : ModSystem
    {
        private const string HarmonyId =
            "xskills.treeshaker.orchardist";

        private Harmony harmony;

        public override double ExecuteOrder()
        {
            return 0.9;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            if (!api.ModLoader.IsModEnabled("treeshaker"))
            {
                return;
            }

            harmony = new Harmony(HarmonyId);
            TreeShakerOrchardistPatch.Apply(harmony, api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    /// <summary>
    /// Applies Orchardist yield and Farming experience to fruit trees harvested
    /// through Tree Shaker 1.1.1.
    /// </summary>
    internal static class TreeShakerOrchardistPatch
    {
        private const string BehaviorTypeName =
            "treeshaker.TreeHarvestCollectibleBehavior";

        private const string HarvestMethodName =
            "HarvestAllFruitTreeParts";

        private const string InteractionMethodName =
            "OnHeldInteractStop";

        private static ICoreAPI api;
        private static FieldInfo radiusField;
        private static FieldInfo heightField;

        // Tree Shaker 1.1.1 resolves the server IPlayer in OnHeldInteractStop,
        // but calls HarvestAllFruitTreeParts with "byEntity as IPlayer". The
        // cast returns null, so keep the correctly resolved player for the
        // nested harvest call without changing Tree Shaker's public API.
        [ThreadStatic]
        private static IPlayer interactionPlayer;

        private sealed class HarvestState
        {
            public PlayerSkill PlayerSkill;
            public Farming Farming;
            public List<BlockEntityFruitTreePart> RipeParts;
        }

        /// <summary>
        /// Resolves and patches Tree Shaker explicitly so patch installation does
        /// not depend on Harmony PatchAll discovering the external type in time.
        /// </summary>
        public static void Apply(Harmony harmony, ICoreAPI coreApi)
        {
            api = coreApi;

            Type behaviorType = FindLoadedType(BehaviorTypeName);
            if (behaviorType == null)
            {
                api.Logger.Error(
                    "[XSkills] Tree Shaker compatibility failed: type {0} was not found.",
                    BehaviorTypeName
                );
                return;
            }

            radiusField = AccessTools.Field(behaviorType, "radius");
            heightField = AccessTools.Field(behaviorType, "height");

            MethodInfo interactionMethod = AccessTools.Method(
                behaviorType,
                InteractionMethodName,
                new Type[]
                {
                    typeof(float),
                    typeof(ItemSlot),
                    typeof(EntityAgent),
                    typeof(BlockSelection),
                    typeof(EntitySelection),
                    typeof(EnumHandHandling).MakeByRefType()
                }
            );

            MethodInfo harvestMethod = AccessTools.Method(
                behaviorType,
                HarvestMethodName,
                new Type[]
                {
                    typeof(BlockPos),
                    typeof(IWorldAccessor),
                    typeof(IPlayer)
                }
            );

            if (interactionMethod == null || harvestMethod == null)
            {
                api.Logger.Error(
                    "[XSkills] Tree Shaker compatibility failed: required methods were not found on {0}.",
                    BehaviorTypeName
                );
                return;
            }

            try
            {
                harmony.Patch(
                    interactionMethod,
                    prefix: new HarmonyMethod(
                        AccessTools.Method(
                            typeof(TreeShakerOrchardistPatch),
                            nameof(InteractionPrefix)
                        )
                    ),
                    postfix: new HarmonyMethod(
                        AccessTools.Method(
                            typeof(TreeShakerOrchardistPatch),
                            nameof(InteractionPostfix)
                        )
                    )
                );

                harmony.Patch(
                    harvestMethod,
                    prefix: radiusField != null && heightField != null
                        ? new HarmonyMethod(
                            AccessTools.Method(
                                typeof(TreeShakerOrchardistPatch),
                                nameof(HarvestPrefix)
                            )
                        )
                        : null,
                    postfix: radiusField != null && heightField != null
                        ? new HarmonyMethod(
                            AccessTools.Method(
                                typeof(TreeShakerOrchardistPatch),
                                nameof(HarvestPostfix)
                            )
                        )
                        : null,
                    transpiler: new HarmonyMethod(
                        AccessTools.Method(
                            typeof(TreeShakerOrchardistPatch),
                            nameof(HarvestTranspiler)
                        )
                    )
                );

                api.Logger.Notification(
                    "[XSkills] Tree Shaker Orchardist compatibility was installed on {0}.",
                    BehaviorTypeName
                );
            }
            catch (Exception exception)
            {
                api.Logger.Error(
                    "[XSkills] Tree Shaker Orchardist compatibility could not be installed: {0}",
                    exception
                );
            }
        }

        /// <summary>
        /// Captures the actual server player before Tree Shaker enters its fruit
        /// harvesting method with a null IPlayer argument.
        /// </summary>
        private static void InteractionPrefix(
            EntityAgent byEntity,
            out IPlayer __state)
        {
            __state = interactionPlayer;
            interactionPlayer = ResolveServerPlayer(byEntity);
        }

        private static void InteractionPostfix(IPlayer __state)
        {
            interactionPlayer = __state;
        }

        /// <summary>
        /// Replaces Tree Shaker's direct GetNextItemStack call with a helper that
        /// applies Orchardist to the original fruit quantity multiplier.
        /// </summary>
        private static IEnumerable<CodeInstruction> HarvestTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getNextItemStack = AccessTools.Method(
                typeof(BlockDropItemStack),
                nameof(BlockDropItemStack.GetNextItemStack),
                new Type[] { typeof(float) }
            );

            MethodInfo getAdjustedItemStack = AccessTools.Method(
                typeof(TreeShakerOrchardistPatch),
                nameof(GetOrchardistAdjustedItemStack)
            );

            int replacements = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (getNextItemStack == null ||
                    getAdjustedItemStack == null ||
                    !instruction.Calls(getNextItemStack))
                {
                    yield return instruction;
                    continue;
                }

                // The original evaluation stack already contains the drop
                // definition and its multiplier. Add Tree Shaker's player
                // argument, then replace the instance call with our helper.
                CodeInstruction loadPlayer =
                    new CodeInstruction(OpCodes.Ldarg_3);

                loadPlayer.labels.AddRange(instruction.labels);
                loadPlayer.blocks.AddRange(instruction.blocks);

                yield return loadPlayer;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    getAdjustedItemStack
                );

                replacements++;
            }

            if (replacements == 0)
            {
                api?.Logger.Error(
                    "[XSkills] Tree Shaker compatibility found no GetNextItemStack(float) call to replace."
                );
            }
            else
            {
                api?.Logger.Notification(
                    "[XSkills] Tree Shaker Orchardist patched {0} fruit-drop call(s).",
                    replacements
                );
            }
        }

        /// <summary>
        /// Applies Orchardist to the same quantity roll used by Tree Shaker.
        /// This preserves the fruit definition's NatFloat distribution and
        /// GameMath.RoundRandom handling instead of creating a separate roll.
        /// </summary>
        private static ItemStack GetOrchardistAdjustedItemStack(
            BlockDropItemStack dropDefinition,
            float baseMultiplier,
            IPlayer player)
        {
            if (dropDefinition == null)
            {
                return null;
            }

            IPlayer effectivePlayer = player ?? interactionPlayer;
            float bonusMultiplier =
                GetOrchardistBonusMultiplier(effectivePlayer);

            float adjustedMultiplier =
                Math.Max(0.0f, baseMultiplier) *
                (1.0f + bonusMultiplier);

            ItemStack result =
                dropDefinition.GetNextItemStack(adjustedMultiplier);

            api?.Logger.Debug(
                "[XSkills] Tree Shaker fruit roll for {0}: base={1:0.###}, orchardist={2:0.###}, adjusted={3:0.###}, result={4}.",
                effectivePlayer?.PlayerName ?? "<unresolved>",
                baseMultiplier,
                bonusMultiplier,
                adjustedMultiplier,
                result?.StackSize ?? 0
            );

            return result;
        }

        /// <summary>
        /// Reads Orchardist as a normalized percentage and tolerates either the
        /// fractional or raw integer representation used by XLeveling versions.
        /// </summary>
        private static float GetOrchardistBonusMultiplier(IPlayer player)
        {
            if (player?.Entity == null)
            {
                return 0.0f;
            }

            Farming farming =
                XLeveling.Instance(player.Entity.Api)?
                    .GetSkill("farming") as Farming;

            if (farming == null)
            {
                return 0.0f;
            }

            PlayerSkill playerSkill =
                player.Entity.GetBehavior<PlayerSkillSet>()?[farming.Id];

            PlayerAbility orchardist =
                playerSkill?[farming.OrchardistId];

            if (orchardist?.Tier <= 0)
            {
                return 0.0f;
            }

            float bonus = orchardist.SkillDependentFValue();

            if (bonus <= 0.0f)
            {
                bonus = 0.01f * orchardist.SkillDependentValue();
            }
            else if (bonus >= 1.0f)
            {
                bonus *= 0.01f;
            }

            if (float.IsNaN(bonus) ||
                float.IsInfinity(bonus) ||
                bonus <= 0.0f)
            {
                return 0.0f;
            }

            return bonus;
        }

        /// <summary>
        /// Captures ripe parts before Tree Shaker marks them as harvested.
        /// </summary>
        private static void HarvestPrefix(
            object __instance,
            BlockPos stemPos,
            IWorldAccessor world,
            IPlayer player,
            out HarvestState __state)
        {
            __state = null;

            IPlayer effectivePlayer = player ?? interactionPlayer;

            if (__instance == null ||
                stemPos == null ||
                world?.Side != EnumAppSide.Server ||
                effectivePlayer?.Entity == null ||
                radiusField == null ||
                heightField == null)
            {
                return;
            }

            Farming farming =
                XLeveling.Instance(world.Api)?
                    .GetSkill("farming") as Farming;

            if (farming == null)
            {
                return;
            }

            PlayerSkill playerSkill =
                effectivePlayer.Entity
                    .GetBehavior<PlayerSkillSet>()?[farming.Id];

            if (playerSkill == null ||
                !(radiusField.GetValue(__instance) is int radius) ||
                !(heightField.GetValue(__instance) is int height))
            {
                return;
            }

            List<BlockEntityFruitTreePart> ripeParts =
                CollectRipeParts(stemPos, world, radius, height);

            if (ripeParts.Count == 0)
            {
                return;
            }

            __state = new HarvestState
            {
                PlayerSkill = playerSkill,
                Farming = farming,
                RipeParts = ripeParts
            };
        }

        /// <summary>
        /// Awards the configured fruit-tree harvest experience only for parts
        /// that Tree Shaker successfully changed out of the ripe state.
        /// </summary>
        private static void HarvestPostfix(HarvestState __state)
        {
            if (__state?.PlayerSkill == null ||
                __state.Farming == null ||
                __state.RipeParts == null)
            {
                return;
            }

            int harvestedPartCount = 0;

            foreach (BlockEntityFruitTreePart part in __state.RipeParts)
            {
                if (part != null &&
                    part.FoliageState != EnumFoliageState.Ripe)
                {
                    harvestedPartCount++;
                }
            }

            if (harvestedPartCount <= 0)
            {
                return;
            }

            float experiencePerPart =
                (__state.Farming.Config as FarmingConfig)?
                    .treeHarvestExp ?? 0.0f;

            if (experiencePerPart > 0.0f)
            {
                __state.PlayerSkill.AddExperience(
                    experiencePerPart * harvestedPartCount
                );
            }
        }

        private static IPlayer ResolveServerPlayer(EntityAgent byEntity)
        {
            EntityPlayer entityPlayer = byEntity as EntityPlayer;

            if (entityPlayer == null)
            {
                return null;
            }

            return byEntity.World?
                .PlayerByUid(entityPlayer.PlayerUID);
        }

        private static Type FindLoadedType(string fullTypeName)
        {
            Type type = AccessTools.TypeByName(fullTypeName);
            if (type != null)
            {
                return type;
            }

            foreach (Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(
                        fullTypeName,
                        false,
                        false
                    );

                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore assemblies whose optional dependencies cannot be resolved.
                }
            }

            return null;
        }

        private static List<BlockEntityFruitTreePart> CollectRipeParts(
            BlockPos stemPos,
            IWorldAccessor world,
            int radius,
            int height)
        {
            List<BlockEntityFruitTreePart> ripeParts =
                new List<BlockEntityFruitTreePart>();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = 0; y <= height; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos partPos =
                            stemPos.AddCopy(x, y, z);

                        BlockEntityFruitTreePart part =
                            world.BlockAccessor.GetBlockEntity(partPos)
                                as BlockEntityFruitTreePart;

                        if (part?.FoliageState == EnumFoliageState.Ripe)
                        {
                            ripeParts.Add(part);
                        }
                    }
                }
            }

            return ripeParts;
        }
    }
}
