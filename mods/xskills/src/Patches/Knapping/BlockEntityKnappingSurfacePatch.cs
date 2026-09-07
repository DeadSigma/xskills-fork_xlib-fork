using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(BlockEntityKnappingSurface))]
    public static class BlockEntityKnappingSurfacePatch
    {
        private class UseState
        {
            public int VoxelCount;
            public int RecipeId;
            public Vec3i VoxelPos;
        }

        private class FinishState
        {
            public KnappingRecipe Recipe;
            public Knapping Knapping;
            public PlayerSkill PlayerSkill;
            public bool Matches;
        }

        public static bool Prepare(MethodBase original)
        {
            XSkills xSkills = XSkills.Instance;
            if (xSkills == null) return false;

            Skill skill;
            xSkills.Skills.TryGetValue("knapping", out skill);
            Knapping knapping = skill as Knapping;

            return knapping?.Enabled ?? false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnUseOver", new Type[] { typeof(IPlayer), typeof(Vec3i), typeof(BlockFacing), typeof(bool) })]
        private static void OnUseOverPrefix(BlockEntityKnappingSurface __instance, IPlayer byPlayer, Vec3i voxelPos, bool mouseMode, out UseState __state)
        {
            __state = null;

            if (__instance?.Api?.Side != EnumAppSide.Server) return;
            if (!mouseMode || voxelPos == null || byPlayer?.Entity == null) return;
            if (__instance.SelectedRecipe == null) return;

            __state = new UseState
            {
                VoxelCount = KnappingUtil.CountVoxels(__instance),
                RecipeId = __instance.SelectedRecipe.RecipeId,
                VoxelPos = new Vec3i(voxelPos.X, voxelPos.Y, voxelPos.Z)
            };
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnUseOver", new Type[] { typeof(IPlayer), typeof(Vec3i), typeof(BlockFacing), typeof(bool) })]
        private static void OnUseOverPostfix(BlockEntityKnappingSurface __instance, IPlayer byPlayer, UseState __state)
        {
            if (__state == null || __instance?.Api?.Side != EnumAppSide.Server) return;
            if (__instance.SelectedRecipe == null || __instance.SelectedRecipe.RecipeId != __state.RecipeId) return;
            if (KnappingUtil.CountVoxels(__instance) >= __state.VoxelCount) return;

            Knapping knapping = XLeveling.Instance(__instance.Api)?.GetSkill("knapping") as Knapping;
            if (knapping == null) return;

            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[knapping.Id];
            if (playerSkill == null) return;

            bool changed = false;

            // wide chipping
            PlayerAbility ability = playerSkill[knapping.WideChippingId];
            if (ability?.Tier > 0)
            {
                changed = KnappingUtil.RemoveNearby(__instance, __state.VoxelPos, ability.Value(0)) > 0;
            }

            // fast knapper
            if (!KnappingUtil.MatchesRecipe(__instance))
            {
                ability = playerSkill[knapping.FastKnapperId];
                changed |= KnappingUtil.TryFastFinish(__instance, byPlayer, ability);
            }

            if (!changed) return;

            __instance.RegenMeshAndSelectionBoxes();
            __instance.Api.World.BlockAccessor.MarkBlockDirty(__instance.Pos, (IPlayer)null);
            __instance.Api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);
            __instance.CheckIfFinished(byPlayer);
            __instance.MarkDirty(false, null);
        }

        [HarmonyPrefix]
        [HarmonyPatch("CheckIfFinished")]
        private static void CheckIfFinishedPrefix(BlockEntityKnappingSurface __instance, IPlayer byPlayer, out FinishState __state)
        {
            __state = new FinishState();

            if (__instance?.Api?.Side != EnumAppSide.Server || byPlayer?.Entity == null) return;

            __state.Recipe = __instance.SelectedRecipe;
            if (__state.Recipe == null) return;

            __state.Matches = KnappingUtil.MatchesRecipe(__instance);
            if (!__state.Matches) return;

            __state.Knapping = XLeveling.Instance(__instance.Api)?.GetSkill("knapping") as Knapping;
            if (__state.Knapping == null) return;

            __state.PlayerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[__state.Knapping.Id];
        }

        [HarmonyPostfix]
        [HarmonyPatch("CheckIfFinished")]
        private static void CheckIfFinishedPostfix(BlockEntityKnappingSurface __instance, IPlayer byPlayer, FinishState __state)
        {
            if (__state?.Matches != true || __state.PlayerSkill == null || __state.Knapping == null) return;
            if (byPlayer?.Entity == null || __instance?.Api?.Side != EnumAppSide.Server) return;

            // experience
            __state.PlayerSkill.AddExperience(1.0f);

            // duplicate result
            PlayerAbility ability = __state.PlayerSkill[__state.Knapping.JackPotId];
            if (ability?.Tier <= 0) return;
            if (ability.SkillDependentValue() * 0.01f < byPlayer.Entity.World.Rand.NextDouble()) return;

            ItemStack outstack = __state.Recipe.Output?.ResolvedItemstack?.Clone();
            if (outstack == null) return;

            if (!byPlayer.InventoryManager.TryGiveItemstack(outstack, false))
            {
                byPlayer.Entity.World.SpawnItemEntity(outstack, __instance.Pos, null);
            }
        }
    }
}
