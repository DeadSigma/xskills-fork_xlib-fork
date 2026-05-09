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

            // Проверяем, остывает ли предмет в критической зоне
            if ((currentState == "quench" || currentState == "overheat") && temperature < slot.Itemstack.Collectible.GetTemperature(world, slot.Itemstack) - 2f)
            {
                // Ищем игрока до того, как предмет потенциально сломается
                IPlayer player = null;
                if (slot.Inventory is InventoryBasePlayer invPlayer)
                {
                    player = invPlayer.Player;
                }
                else
                {
                    string uid = slot.Itemstack.Attributes.GetString("forgedByUid");
                    if (uid != null)
                    {
                        player = world.PlayerByUid(uid);
                    }
                }

                // Проверяем уровень перка
                if (player != null)
                {
                    Metalworking metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;
                    if (metalworking != null)
                    {
                        PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[metalworking.Id]?[metalworking.SafeQuenchingId];

                        // ЕСЛИ УРОВЕНЬ ПЕРКА 2: Полностью отменяем шанс поломки
                        if (playerAbility != null && playerAbility.Tier >= 2)
                        {
                            slot.Itemstack.TempAttributes.SetBool("willbreak", false);
                        }
                    }
                }

                // Сохраняем копию предмета на случай, если перк 1-го уровня и предмет сломается
                __state = slot.Itemstack.Clone();
            }
        }

        [HarmonyPostfix]
        public static void Postfix(CollectibleBehaviorQuenchable __instance, IWorldAccessor world, ItemSlot slot, float temperature, ItemStack __state)
        {
            // Если предмет исчез (сломался), значит сработал код разрушения (перк не 2 уровня)
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

                        // ЕСЛИ УРОВЕНЬ ПЕРКА 1: Выдаем деформированную заготовку
                        if (playerAbility != null && playerAbility.Tier >= 1)
                        {
                            string metalGroupCode = AccessTools.Field(typeof(CollectibleBehaviorQuenchable), "metalGroupCode").GetValue(__instance) as string ?? "metal";
                            string metalCode = __state.Collectible.Variant[metalGroupCode] ?? "iron";

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
                                            r.Output.ResolvedItemstack.Collectible.Code.Equals(__state.Collectible.Code))
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
                                                    if (rand < 0.15)
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Empty;
                                                    else if (rand < 0.25)
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Slag;
                                                    else
                                                        voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
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
                                slot.Itemstack = workItemStack;
                                slot.MarkDirty();
                            }
                        }
                    }
                }
            }
        }
    }
}