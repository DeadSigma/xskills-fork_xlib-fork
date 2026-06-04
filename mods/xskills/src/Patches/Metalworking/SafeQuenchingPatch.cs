using HarmonyLib;
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
        [HarmonyPrefix]
        public static void Prefix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, float temperature, out object[] __state)
        {
            __state = null;
            if (world.Side == EnumAppSide.Client || slot.Itemstack == null) return;

            string currentState = __instance.GetState(slot.Itemstack);

            // Если предмет вот-вот сломается
            if ((currentState == "quench" || currentState == "overheat") && temperature < slot.Itemstack.Collectible.GetTemperature(world, slot.Itemstack) - 2f)
            {
                ItemStack clonedStack = slot.Itemstack.Clone();
                IPlayer player = null;
                Vec3d pos = null;

                // щем, где находится предмет ДО того, как он пропадет
                if (slot.Inventory is InventoryBasePlayer invPlayer)
                {
                    player = invPlayer.Player;
                    pos = player.Entity.Pos.XYZ;
                }
                else
                {
                    // Безопасно приводим world к IServerWorldAccessor
                    if (world is IServerWorldAccessor serverWorld)
                    {
                        // Ищем брошенную сущность предмета (в воде)
                        foreach (var entity in serverWorld.LoadedEntities.Values)
                        {
                            if (entity is EntityItem entityItem && (entityItem.Slot == slot || entityItem.Itemstack == slot.Itemstack))
                            {
                                pos = entity.Pos.XYZ;
                                break;
                            }
                        }
                    }

                    // Если это блок (например, корыто/бочка) и сущность не найдена
                    if (pos == null && slot.Inventory != null)
                    {
                        var posProp = slot.Inventory.GetType().GetProperty("Pos");
                        if (posProp != null && posProp.GetValue(slot.Inventory) is BlockPos blockPos)
                        {
                            pos = new Vec3d(blockPos.X + 0.5, blockPos.Y + 0.5, blockPos.Z + 0.5);
                        }
                    }

                    // Если нашли позицию - ищем игрока в радиусе 15 блоков
                    if (pos != null)
                    {
                        IPlayer nearest = world.NearestPlayer(pos.X, pos.Y, pos.Z);
                        if (nearest != null && nearest.Entity.Pos.DistanceTo(pos) <= 15.0)
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

                        // Если читерский перк включен в конфигах и изучен игроком
                        if (perfectQuench != null && perfectQuench.Tier > 0)
                        {
                            slot.Itemstack.TempAttributes.SetBool("willbreak", false);
                            return; // Прерываем Prefix. В Postfix предмет не уйдет, так как останется целым.
                        }
                    }
                }

                // Сохраняем состояние для Postfix
                __state = new object[] { clonedStack, player, pos };
            }
        }

        [HarmonyPostfix]
        public static void Postfix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, float temperature, object[] __state)
        {
            // Если оригинальный код сломал предмет, и у нас есть сохраненные данные
            if (__state != null && slot.Itemstack == null)
            {
                ItemStack originalStack = __state[0] as ItemStack;
                IPlayer player = __state[1] as IPlayer;
                Vec3d pos = __state[2] as Vec3d;

                // Если игрок с подходящей дистанцией найден
                if (player != null && originalStack != null)
                {
                    Metalworking metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;

                    if (metalworking != null)
                    {
                        PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.SafeQuenchingId];

                        if (playerAbility != null && playerAbility.Tier > 0)
                        {
                            string metalGroupCode = AccessTools.Field(typeof(CollectibleBehaviorQuenchable), "metalGroupCode").GetValue(__instance) as string ?? "metal";
                            string metalCode = originalStack.Collectible.Variant[metalGroupCode] ?? "iron";

                            Item workItem = world.GetItem(new AssetLocation("game", "workitem-" + metalCode));

                            if (workItem != null)
                            {
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
                                        }
                                    }
                                }

                                workItemStack.Collectible.SetTemperature(world, workItemStack, temperature);

                                // 3. Определяем, как вернуть заготовку
                                if (slot.Inventory == null || slot.Inventory.ClassName == "dummy" || slot.Inventory.ClassName == "entity")
                                {
                                    // Предмет брошен в воду. Создаем НОВУЮ сущность предмета.
                                    // Старая сущность (slot) останется пустой и игра ее безопасно удалит.
                                    Vec3d spawnPos = pos ?? player.Entity.Pos.XYZ;
                                    world.SpawnItemEntity(workItemStack, spawnPos);
                                }
                                else
                                {
                                    // Предмет в инвентаре или бочке - кладем обратно в слот
                                    slot.Itemstack = workItemStack;
                                    slot.MarkDirty();
                                }

                                // Возврат излишков, если игрок бросил сразу стак (2+ штуки)
                                if (originalStack.StackSize > 1)
                                {
                                    ItemStack extraItems = originalStack.Clone();
                                    extraItems.StackSize = originalStack.StackSize - 1;

                                    Vec3d spawnPos = pos ?? player.Entity.Pos.XYZ;

                                    if (!player.InventoryManager.TryGiveItemstack(extraItems))
                                    {
                                        world.SpawnItemEntity(extraItems, spawnPos);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}