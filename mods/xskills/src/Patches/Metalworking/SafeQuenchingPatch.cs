using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "IsGettingCooled")]
    public class CollectibleBehaviorQuenchable_IsGettingCooled_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, float temperature, out ItemStack __state)
        {
            __state = null;
            if (world.Side == EnumAppSide.Client || slot.Itemstack == null) return;

            string currentState = __instance.GetState(slot.Itemstack);

            // Сохраняем копию предмета ДО поломки
            if ((currentState == "quench" || currentState == "overheat") && temperature < slot.Itemstack.Collectible.GetTemperature(world, slot.Itemstack) - 2f)
            {
                __state = slot.Itemstack.Clone();
            }
        }

        [HarmonyPostfix]
        public static void Postfix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, float temperature, ItemStack __state)
        {
            // Если предмет исчез (сломался)
            if (__state != null && slot.Itemstack == null)
            {
                IPlayer player = null;

                if (slot.Inventory is InventoryBasePlayer invPlayer)
                {
                    player = invPlayer.Player;
                }
                else
                {
                    string uid = __state.Attributes.GetString("forgedByUid");
                    if (uid != null)
                    {
                        player = world.PlayerByUid(uid);
                    }
                }

                if (player != null)
                {
                    Metalworking metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;

                    if (metalworking != null)
                    {
                        PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.SafeQuenchingId];

                        if (playerAbility != null && playerAbility.Tier > 0)
                        {
                            string metalGroupCode = AccessTools.Field(typeof(CollectibleBehaviorQuenchable), "metalGroupCode").GetValue(__instance) as string ?? "metal";
                            string metalCode = __state.Collectible.Variant[metalGroupCode] ?? "iron";

                            // 1. Возвращаем WorkItem (заготовку для наковальни)
                            Item workItem = world.GetItem(new AssetLocation("game", "workitem-" + metalCode));

                            if (workItem != null)
                            {
                                // ОШИБКА БЫЛА ЗДЕСЬ: Жестко задан размер стака 1
                                // ItemStack workItemStack = new ItemStack(workItem, 1); 

                                // ИСПРАВЛЕНИЕ: Создаем стак для текущего слота (1 шт.)
                                ItemStack workItemStack = new ItemStack(workItem, 1);

                                // 2. Пытаемся найти рецепт того, что игрок ковал
                                SmithingRecipe shatteredRecipe = null;
                                var recipes = world.Api.GetSmithingRecipes();
                                if (recipes != null)
                                {
                                    foreach (var r in recipes)
                                    {
                                        if (r.Output?.ResolvedItemstack?.Collectible?.Code != null &&
                                            r.Output.ResolvedItemstack.Collectible.Code.Equals(__state.Collectible.Code))
                                        {
                                            shatteredRecipe = r;
                                            break;
                                        }
                                    }
                                }

                                // 3. Если рецепт найден, "ломаем" форму
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
                                                    if (rand < 0.15)
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty; // 15% шанс откола
                                                    else if (rand < 0.25)
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Slag;  // 10% шанс превращения в шлак
                                                    else
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal; // Оставшийся металл
                                                }
                                            }
                                        }
                                    }

                                    // Сохраняем деформированные воксели в заготовку
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

                                // Сохраняем исходную температуру
                                workItemStack.Collectible.SetTemperature(world, workItemStack, temperature);

                                // Возвращаем первую заготовку в текущий слот
                                slot.Itemstack = workItemStack;
                                slot.MarkDirty();

                                // Если в исходном стаке было больше одного предмета, возвращаем остальные
                                if (__state.StackSize > 1)
                                {
                                    int remainingCount = __state.StackSize - 1;

                                    // Обрабатываем каждый оставшийся предмет отдельно, так как они нестакаемые
                                    for (int i = 0; i < remainingCount; i++)
                                    {
                                        // Клонируем деформированную заготовку (размер стака будет 1)
                                        ItemStack singleExtraItem = workItemStack.Clone();
                                        singleExtraItem.StackSize = 1;

                                        // Пытаемся выдать 1 шт. в инвентарь
                                        if (!player.InventoryManager.TryGiveItemstack(singleExtraItem))
                                        {
                                            // Если места нет (метод вернул false), выбрасываем под ноги игроку
                                            world.SpawnItemEntity(singleExtraItem, player.Entity.Pos.XYZ);
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
}