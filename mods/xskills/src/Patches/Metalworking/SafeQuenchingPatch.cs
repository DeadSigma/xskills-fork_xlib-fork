using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "IsGettingCooled")]
    public class CollectibleBehaviorQuenchable_IsGettingCooled_Patch
    {
        private static bool IsSmithingPlusPlusPresent(IWorldAccessor world)
        {
            if (world?.Api?.ModLoader?.Mods == null) return false;

            foreach (var mod in world.Api.ModLoader.Mods)
            {
                if (string.Equals(mod?.Info?.ModID, "smithingplusplus", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryRecoverSafeQuench(
            CollectibleBehaviorQuenchable __instance,
            IWorldAccessor world,
            ItemSlot slot,
            float temperature,
            ItemStack originalStack,
            IPlayer player,
            Vec3d recoveryPos,
            Vec3d pos)
        {
            if (player == null || originalStack == null) return false;

            Metalworking metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;
            if (metalworking == null) return false;

            PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.SafeQuenchingId];
            if (playerAbility == null || playerAbility.Tier <= 0) return false;

            string metalGroupCode = AccessTools.Field(typeof(CollectibleBehaviorQuenchable), "metalGroupCode").GetValue(__instance) as string ?? "metal";
            string metalCode = originalStack.Collectible.Variant[metalGroupCode] ?? "iron";

            Item workItem = world.GetItem(new AssetLocation("game", "workitem-" + metalCode));

            if (workItem == null) return false;

            ItemStack workItemStack = new ItemStack(workItem, 1);
            SmithingRecipe shatteredRecipe = null;
            var recipes = world.Api.GetSmithingRecipes();

            if (recipes != null)
            {
                foreach (var r in recipes)
                {
                    if (r.Output?.ResolvedItemstack?.Collectible?.Code != null &&
                        r.Output.ResolvedItemstack.Collectible.Code.Equals(originalStack.Collectible.Code))
                    {
                        shatteredRecipe = r;
                        break;
                    }
                }
            }

            if (shatteredRecipe != null)
            {
                byte[,,] voxels = new byte[16, 6, 16];
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 6; y++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            if (y < shatteredRecipe.QuantityLayers && shatteredRecipe.Voxels[x, y, z])
                            {
                                double rand = world.Rand.NextDouble();
                                if (rand < 0.15) voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty;
                                else if (rand < 0.25) voxels[x, y, z] = (byte)EnumVoxelMaterial.Slag;
                                else voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                            }
                        }
                    }
                }

                MethodInfo serializeMethod = typeof(BlockEntityAnvil).GetMethod("serializeVoxels", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (serializeMethod != null)
                {
                    byte[] voxelData = serializeMethod.Invoke(null, new object[] { voxels }) as byte[];
                    if (voxelData != null)
                    {
                        workItemStack.Attributes.SetBytes("voxels", voxelData);
                        workItemStack.Attributes.SetInt("selectedRecipeId", shatteredRecipe.RecipeId);
                        workItemStack.Attributes.SetBool("fromSafeQuenching", true);
                    }
                }
            }

            workItemStack.Collectible.SetTemperature(world, workItemStack, temperature);

            bool suppressSmithingPlusRecovery = IsSmithingPlusPlusPresent(world);

            if (suppressSmithingPlusRecovery)
            {
                slot.Itemstack = workItemStack;
                slot.MarkDirty();
            }
            else if (slot.Inventory == null)
            {
                Vec3d spawnPos = recoveryPos ?? pos ?? player.Entity.Pos.XYZ;
                world.SpawnItemEntity(workItemStack, spawnPos);
            }
            else
            {
                slot.Itemstack = workItemStack;
                slot.MarkDirty();
            }

            if (originalStack.StackSize > 1)
            {
                ItemStack extraItems = originalStack.Clone();
                extraItems.StackSize = originalStack.StackSize - 1;

                Vec3d spawnPos = recoveryPos ?? pos ?? player.Entity.Pos.XYZ;

                if (!player.InventoryManager.TryGiveItemstack(extraItems))
                {
                    world.SpawnItemEntity(extraItems, spawnPos);
                }
            }

            return true;
        }

        [HarmonyPrefix]
        public static bool Prefix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, Vec3d pos, float dt, float temperature, out object[] __state)
        {
            __state = null;
            if (world.Side == EnumAppSide.Client || slot.Itemstack == null) return true;

            string currentState = __instance.GetState(slot.Itemstack);

            // If the item is about to break
            if ((currentState == "quench" || currentState == "overheat") && temperature < slot.Itemstack.Collectible.GetTemperature(world, slot.Itemstack) - 2f)
            {
                ItemStack clonedStack = slot.Itemstack.Clone();
                IPlayer player = null;
                Vec3d quenchPos = pos;

                // Determine where the item is BEFORE it disappears
                if (slot.Inventory is InventoryBasePlayer invPlayer)
                {
                    player = invPlayer.Player;
                    quenchPos ??= player.Entity.Pos.XYZ;
                }
                else
                {
                    // If this is a block inventory (e.g., trough/barrel) and no entity was found
                    if (quenchPos == null && slot.Inventory != null)
                    {
                        var posProp = slot.Inventory.GetType().GetProperty("Pos");
                        if (posProp != null && posProp.GetValue(slot.Inventory) is BlockPos blockPos)
                        {
                            quenchPos = new Vec3d(blockPos.X + 0.5, blockPos.Y + 0.5, blockPos.Z + 0.5);
                        }
                    }

                    // If we found a position, find a player within a 15-block radius
                    if (quenchPos != null)
                    {
                        IPlayer nearest = world.NearestPlayer(quenchPos.X, quenchPos.Y, quenchPos.Z);
                        if (nearest != null && nearest.Entity.Pos.DistanceTo(quenchPos) <= 15.0)
                        {
                            player = nearest;
                        }
                    }
                }

                if (player != null)
                {
                    Metalworking metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;
                    if (metalworking != null)
                    {
                        PlayerAbility perfectQuench = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.PerfectQuenchingId];

                        // If the perk is enabled in config and learned by the player
                        if (perfectQuench != null && perfectQuench.Tier > 0)
                        {
                            slot.Itemstack.TempAttributes.SetBool("willbreak", false);
                            return true;
                        }

                        PlayerAbility safeQuenching = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.SafeQuenchingId];
                        bool suppressSmithingPlusRecovery = safeQuenching != null && safeQuenching.Tier > 0 && IsSmithingPlusPlusPresent(world);
                        if (suppressSmithingPlusRecovery)
                        {
                            if (TryRecoverSafeQuench(__instance, world, slot, temperature, clonedStack, player, quenchPos, pos))
                            {
                                return false;
                            }
                        }
                    }
                }

                // Save state for Postfix
                __state = new object[] { clonedStack, player, quenchPos };
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyAfter("smithingplusplus")]
        public static void Postfix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, Vec3d pos, float dt, float temperature, object[] __state)
        {
            // If the original code broke the item and we have saved data
            if (__state != null && slot.Itemstack == null)
            {
                ItemStack originalStack = __state[0] as ItemStack;
                IPlayer player = __state[1] as IPlayer;
                Vec3d recoveryPos = __state[2] as Vec3d;

                // If a player within valid distance was found
                if (player != null && originalStack != null)
                {
                    if (TryRecoverSafeQuench(__instance, world, slot, temperature, originalStack, player, recoveryPos, pos))
                    {
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "SetTemperature")]
    public class CollectibleBehaviorQuenchable_SetTemperature_QualityPatch
    {
        private const string BaseQualityAttr = "xskillsBaseQuality";

        public static int[] CaptureIterations(ItemStack itemstack)
        {
            int quench = itemstack?.Attributes?.GetInt("quenchIteration") ?? 0;
            int temper = itemstack?.Attributes?.GetInt("temperIteration") ?? 0;
            return new int[] { quench, temper };
        }

        [HarmonyPrefix]
        public static void Prefix(ItemStack itemstack, out int[] __state)
        {
            __state = CaptureIterations(itemstack);
        }

        [HarmonyPostfix]
        public static void Postfix(IWorldAccessor world, ItemStack itemstack, int[] __state)
        {
            ApplyQualityIfHeatTreatmentChanged(world, itemstack, __state);
        }

        public static void ApplyQualityIfHeatTreatmentChanged(IWorldAccessor world, ItemStack itemstack, int[] __state)
        {
            if (world?.Side != EnumAppSide.Server || itemstack?.Attributes == null || __state == null) return;

            int oldQuench = __state[0];
            int oldTemper = __state[1];
            int newQuench = itemstack.Attributes.GetInt("quenchIteration");
            int newTemper = itemstack.Attributes.GetInt("temperIteration");

            if (newQuench == oldQuench && newTemper == oldTemper) return;

            float quality = itemstack.Attributes.GetFloat("quality", 0.0f);
            if (quality <= 0.0f) return;

            float baseQuality = itemstack.Attributes.GetFloat(BaseQualityAttr, 0.0f);
            if (baseQuality <= 0.0f)
            {
                float previousMultiplier = QualityMultiplier(oldQuench, oldTemper);
                if (previousMultiplier <= 0.001f) return;
                baseQuality = quality / previousMultiplier;
                itemstack.Attributes.SetFloat(BaseQualityAttr, baseQuality);
            }

            float oldMultiplier = QualityMultiplier(oldQuench, oldTemper);
            float newMultiplier = QualityMultiplier(newQuench, newTemper);
            float targetQuality = baseQuality * newMultiplier;
            float maxQuality = baseQuality * 1.5f;
            float finalQuality = Math.Max(0.01f, Math.Min(targetQuality, maxQuality));
            itemstack.Attributes.SetFloat("quality", finalQuality);
        }

        private static float QualityMultiplier(int quenchIterations, int temperIterations)
        {
            float multiplier = 0.7f;

            if (quenchIterations >= 1) multiplier += 0.15f;
            if (temperIterations >= 1) multiplier += 0.05f;
            if (quenchIterations >= 2) multiplier += 0.10f;
            if (temperIterations >= 2) multiplier += 0.03f;
            if (quenchIterations >= 3) multiplier += 0.05f;
            if (temperIterations >= 3) multiplier += 0.01f;

            int extraTreatments = Math.Max(0, quenchIterations - 3) + Math.Max(0, temperIterations - 3);
            if (extraTreatments > 0)
            {
                multiplier += (1.0f / 3.0f) * (1.0f - (float)Math.Pow(0.85f, extraTreatments));
            }

            return Math.Min(multiplier, 1.5f);
        }
    }

    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "AfterGetTemperature")]
    public class CollectibleBehaviorQuenchable_AfterGetTemperature_QualityPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemStack itemstack, out int[] __state)
        {
            __state = CollectibleBehaviorQuenchable_SetTemperature_QualityPatch.CaptureIterations(itemstack);
        }

        [HarmonyPostfix]
        public static void Postfix(IWorldAccessor world, ItemStack itemstack, int[] __state)
        {
            CollectibleBehaviorQuenchable_SetTemperature_QualityPatch.ApplyQualityIfHeatTreatmentChanged(world, itemstack, __state);
        }
    }
}
