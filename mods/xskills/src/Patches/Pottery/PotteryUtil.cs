using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Provides helper methods for pottery-related logic and progression.
    /// </summary>
    public class PotteryUtil
    {
        /// <summary>
        /// Converts clay from a player's inventory into usable voxels for a clay form entity.
        /// </summary>
        public static bool AddClay(BlockEntityClayForm cfe, ItemSlot slot, EntityAgent entity)
        {
            if (cfe == null || slot == null) return false;

            if (cfe.AvailableVoxels <= 0)
            {
                if ((slot.Itemstack?.StackSize ?? 0) <= 0)
                {
                    return false;
                }

                slot.TakeOut(1);
                slot.MarkDirty();
                cfe.AvailableVoxels += 25;

                if (entity?.Api == null) return true;

                Pottery pottery = XLeveling.Instance(entity.Api).GetSkill("pottery") as Pottery;
                if (pottery == null) return true;

                PlayerSkill playerSkill = entity.GetBehavior<PlayerSkillSet>()?[pottery.Id];
                if (playerSkill == null) return true;

                // Thrift bonus.
                PlayerAbility playerAbility = playerSkill[pottery.ThriftId];
                if (playerAbility == null) return true;

                cfe.AvailableVoxels += playerAbility.Value(0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies the selected recipe to the clay form.
        /// </summary>
        public static void FinishRecipe(BlockEntityClayForm clayForm, ItemSlot slot, EntityAgent entity)
        {
            if (clayForm == null) return;

            int ymax = Math.Min(16, clayForm.SelectedRecipe.QuantityLayers);

            for (int y = 0; y < ymax; ++y)
            {
                for (int x = 0; x < 16; ++x)
                {
                    for (int z = 0; z < 16; ++z)
                    {
                        if (clayForm.SelectedRecipe.Voxels[x, y, z] != clayForm.Voxels[x, y, z])
                        {
                            if (clayForm.SelectedRecipe.Voxels[x, y, z])
                            {
                                if (clayForm.AvailableVoxels < 0)
                                {
                                    if (!AddClay(clayForm, slot, entity)) return;
                                }

                                clayForm.AvailableVoxels--;
                            }
                            else
                            {
                                clayForm.AvailableVoxels++;
                            }
                        }

                        clayForm.Voxels[x, y, z] = clayForm.SelectedRecipe.Voxels[x, y, z];
                    }
                }
            }
        }

        /// <summary>
        /// Determines how much of the selected recipe has been completed.
        /// </summary>
        public static float FinishedProportion(BlockEntityClayForm clayForm)
        {
            if (clayForm?.SelectedRecipe == null) return 0.0f;

            int finishedVoxel = 0;
            int neededVoxels = 0;
            int voxelCount = 0;

            int ymax = Math.Min(16, clayForm.SelectedRecipe.QuantityLayers);

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < ymax; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (clayForm.SelectedRecipe.Voxels[x, y, z])
                        {
                            neededVoxels++;

                            if (clayForm.Voxels[x, y, z])
                            {
                                voxelCount++;
                                finishedVoxel++;
                            }
                        }
                        else if (clayForm.Voxels[x, y, z])
                        {
                            voxelCount++;
                        }
                    }
                }
            }

            float temp1 = voxelCount < neededVoxels
                ? (float)voxelCount / neededVoxels
                : (float)neededVoxels / voxelCount;

            float temp2 = (float)finishedVoxel / voxelCount;
            return temp1 * temp2;
        }

        /// <summary>
        /// Counts the voxels in a clay-forming recipe.
        /// </summary>
        public static int CountVoxels(ClayFormingRecipe recipe)
        {
            int voxelCount = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < recipe.QuantityLayers; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (recipe.Voxels[x, y, z])
                        {
                            voxelCount++;
                        }
                    }
                }
            }

            return voxelCount;
        }

        /// <summary>
        /// Applies pottery-related bonuses and inspirations to the given output stack.
        /// </summary>
        public static bool ApplyOnStack(IPlayer byPlayer, IWorldAccessor world, ItemSlot outputSlot)
        {
            Pottery pottery = byPlayer?.Entity?.Api.ModLoader
                .GetModSystem<XLeveling>()?.GetSkill("pottery") as Pottery;

            if (pottery == null || world == null) return false;
            if (outputSlot?.Itemstack?.Collectible?.Code == null) return false;

            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[pottery.Id];

            // Give the player 1 XP for a successful firing, regardless of perk level.
            playerSkill?.AddExperience(1);

            PlayerAbility playerAbility = playerSkill?[pottery.PotteryTimerId];
            if (playerAbility?.Tier > 0)
            {
                NotifyPlayer(
                    world,
                    outputSlot,
                    byPlayer,
                    "xskills:pottery-finished",
                    "xskillsPotteryMsg"
                );
            }

            // Inspiration bonus.
            playerAbility = playerSkill?[pottery.InspirationId];
            if (playerAbility == null || playerAbility.Tier <= 0) return false;

            string name = outputSlot.Itemstack.Collectible.Code.FirstCodePart();

            // Build and cache matching collectibles when this code stem is first encountered.
            if (!pottery.InspirationCollectibles.TryGetValue(name, out List<CollectibleObject> dest)
                || dest == null)
            {
                dest = new List<CollectibleObject>();

                foreach (CollectibleObject collectible in world.Collectibles)
                {
                    if (collectible?.Code == null) continue;
                    if (collectible == outputSlot.Itemstack.Collectible) continue;

                    // Related variants must share the same code stem, such as "clayplanter-...".
                    if (!collectible.Code.Path.StartsWith(name + "-", StringComparison.Ordinal)) continue;

                    // Exclude unfired variants.
                    if (IsUnfired(collectible)) continue;
                    if (collectible.Code.Path.EndsWith("fired", StringComparison.Ordinal)) continue;

                    dest.Add(collectible);
                }

                pottery.InspirationCollectibles[name] = dest;
            }

            if (dest.Count <= 0) return true;

            if (world.Rand.NextDouble()
                < playerAbility.FValue(0) / outputSlot.Itemstack.StackSize)
            {
                CollectibleObject obj = dest[world.Rand.Next(dest.Count)];
                ItemStack stack = new ItemStack(obj, outputSlot.Itemstack.StackSize);

                string type = obj.Attributes?["defaultType"]?.AsString();
                if (type != null)
                {
                    stack.Attributes.SetString("type", type);
                }

                ITreeAttribute pairs =
                    outputSlot.Itemstack.Attributes.GetTreeAttribute("temperature");

                if (pairs != null)
                {
                    float temperature = pairs.GetFloat("temperature", 0.0f);
                    double temperatureLastUpdate =
                        pairs.GetDouble("temperatureLastUpdate", 0.0f);

                    if (temperature > 50.0f)
                    {
                        ITreeAttribute tempTree =
                            stack.Attributes.GetOrAddTreeAttribute("temperature");

                        tempTree.SetFloat("temperature", temperature);
                        tempTree.SetDouble("temperatureLastUpdate", temperatureLastUpdate);
                    }
                }

                outputSlot.Itemstack = stack;
                outputSlot.MarkDirty();
            }

            return true;
        }

        /// <summary>
        /// Checks whether a collectible is an unfired raw product.
        /// </summary>
        private static bool IsUnfired(CollectibleObject collectible)
        {
            // Raw products have a defined firing result.
            CombustibleProperties props = collectible.CombustibleProps;
            if (props != null
                && props.SmeltingType == EnumSmeltType.Fire
                && props.SmeltedStack != null)
            {
                return true;
            }

            // Fallback check for mods that use their own firing process.
            string[] parts = collectible.Code.Path.Split('-');

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "raw"
                    || parts[i] == "unfired"
                    || parts[i] == "unburned")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Notifies the player when a process has completed.
        /// </summary>
        public static void NotifyPlayer(
            IWorldAccessor world,
            ItemSlot slot,
            IPlayer player,
            string msg,
            string attribute)
        {
            BlockPos pos = slot.Inventory.Pos;
            Block block = pos != null
                ? world.BulkBlockAccessor.GetBlock(pos)
                : null;

            bool emptyInput =
                (slot.Inventory as InventorySmelting)?[1].Empty ?? true;

            if (block != null && emptyInput)
            {
                double now = world.Calendar.TotalHours;
                double lastMsg = player.Entity.Attributes.GetDouble(attribute);

                if (now > lastMsg + 0.333)
                {
                    player.Entity.Attributes.SetDouble(attribute, now);
                    world.PlaySoundFor(
                        new AssetLocation("sounds/tutorialstepsuccess.ogg"),
                        player
                    );

                    (player as IServerPlayer)?.SendMessage(
                        0,
                        Lang.Get(
                            msg,
                            block.GetPlacedBlockName(world, pos)
                                + " (" + pos.X + ", " + pos.X + pos.Y + ", " + pos.Z + ")"
                        ),
                        EnumChatType.Notification
                    );
                }
            }
        }
    }
}