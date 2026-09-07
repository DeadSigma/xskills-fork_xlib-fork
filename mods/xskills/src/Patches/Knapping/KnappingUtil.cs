using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public static class KnappingUtil
    {
        private static readonly MethodInfo TryBfsRemoveMethod = AccessTools.Method(typeof(BlockEntityKnappingSurface), "tryBfsRemove");

        public static int CountVoxels(BlockEntityKnappingSurface surface)
        {
            if (surface?.Voxels == null) return 0;

            int count = 0;
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (surface.Voxels[x, z]) count++;
                }
            }

            return count;
        }

        public static bool MatchesRecipe(BlockEntityKnappingSurface surface)
        {
            KnappingRecipe recipe = surface?.SelectedRecipe;
            if (recipe == null || surface.Voxels == null) return false;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (surface.Voxels[x, z] != recipe.Voxels[x, 0, z]) return false;
                }
            }

            return true;
        }

        public static float FinishedProportion(BlockEntityKnappingSurface surface)
        {
            KnappingRecipe recipe = surface?.SelectedRecipe;
            if (recipe == null || surface.Voxels == null) return 0.0f;

            int totalExcess = 0;
            int remainingExcess = 0;

            for (int x = 3; x <= 12; x++)
            {
                for (int z = 3; z <= 12; z++)
                {
                    if (!recipe.Voxels[x, 0, z]) totalExcess++;
                }
            }

            if (totalExcess <= 0) return 1.0f;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (surface.Voxels[x, z] && !recipe.Voxels[x, 0, z]) remainingExcess++;
                }
            }

            return Math.Max(0.0f, Math.Min(1.0f, 1.0f - (float)remainingExcess / totalExcess));
        }

        public static int RemoveNearby(BlockEntityKnappingSurface surface, Vec3i origin, int amount)
        {
            KnappingRecipe recipe = surface?.SelectedRecipe;
            if (recipe == null || surface.Voxels == null || origin == null || amount <= 0) return 0;

            int removed = 0;

            for (int i = 0; i < amount; i++)
            {
                Vec3i pos = FindClosestOutlineVoxel(surface, origin);
                if (pos == null) break;

                surface.Voxels[pos.X, pos.Z] = false;
                CleanupLoosePieces(surface, pos);
                removed++;
            }

            return removed;
        }

        private static Vec3i FindClosestOutlineVoxel(BlockEntityKnappingSurface surface, Vec3i origin)
        {
            KnappingRecipe recipe = surface?.SelectedRecipe;
            if (recipe == null || surface.Voxels == null) return null;

            Vec3i best = null;
            int bestDistance = int.MaxValue;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (!surface.Voxels[x, z]) continue;
                    if (recipe.Voxels[x, 0, z]) continue;
                    if (!IsOutlineVoxel(recipe, x, z)) continue;

                    int dx = x - origin.X;
                    int dz = z - origin.Z;
                    int distance = dx * dx + dz * dz;

                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    best = new Vec3i(x, 0, z);
                }
            }

            return best;
        }

        private static bool IsOutlineVoxel(KnappingRecipe recipe, int x, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    int nx = x + dx;
                    int nz = z + dz;

                    if (nx < 0 || nx >= 16 || nz < 0 || nz >= 16) continue;
                    if (recipe.Voxels[nx, 0, nz]) return true;
                }
            }

            return false;
        }

        public static bool TryFastFinish(BlockEntityKnappingSurface surface, IPlayer byPlayer, PlayerAbility ability)
        {
            if (surface?.SelectedRecipe == null || byPlayer?.Entity == null || ability?.Tier <= 0) return false;
            if (!CanFinishByRemoving(surface)) return false;

            float progress = FinishedProportion(surface);
            float chance = ability.Value(0) * progress * progress * 0.01f;
            if (chance < byPlayer.Entity.World.Rand.NextDouble()) return false;

            KnappingRecipe recipe = surface.SelectedRecipe;
            bool changed = false;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (!surface.Voxels[x, z] || recipe.Voxels[x, 0, z]) continue;
                    surface.Voxels[x, z] = false;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool CanFinishByRemoving(BlockEntityKnappingSurface surface)
        {
            KnappingRecipe recipe = surface?.SelectedRecipe;
            if (recipe == null || surface.Voxels == null) return false;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (recipe.Voxels[x, 0, z] && !surface.Voxels[x, z]) return false;
                }
            }

            return true;
        }

        private static void CleanupLoosePieces(BlockEntityKnappingSurface surface, Vec3i removedPos)
        {
            if (TryBfsRemoveMethod == null || surface?.SelectedRecipe == null) return;

            for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
            {
                BlockFacing face = BlockFacing.HORIZONTALS[i];
                int x = removedPos.X + face.Normali.X;
                int z = removedPos.Z + face.Normali.Z;

                if (x < 0 || x >= 16 || z < 0 || z >= 16) continue;
                if (!surface.Voxels[x, z]) continue;
                if (surface.SelectedRecipe.Voxels[x, 0, z]) continue;

                TryBfsRemoveMethod.Invoke(surface, new object[] { x, z });
            }
        }
    }
}
