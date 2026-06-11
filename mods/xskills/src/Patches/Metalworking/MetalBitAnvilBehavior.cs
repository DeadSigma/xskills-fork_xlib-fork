using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Делает ванильные куски металла (metalbit-*) обрабатываемыми на наковальне,
    /// в точности как слитки. Навешивается на предметы программно в Metalworking.UpdateBits().
    /// Наковальня находит это поведение через CollectibleObject.GetCollectibleInterface&lt;IAnvilWorkable&gt;(),
    /// поэтому никакой подмены класса предмета не требуется.
    ///
    /// Доступ к ковке кусками ограничивается перком (см. BlockEntityAnvilPatch.TryPutPrefix):
    /// это поведение лишь предоставляет механику, проверка перка делается в патче TryPut,
    /// потому что только там доступен игрок.
    /// </summary>
    public class MetalBitAnvilBehavior : CollectibleBehavior, IAnvilWorkable
    {
        private ICoreAPI api;

        public MetalBitAnvilBehavior(CollectibleObject collObj) : base(collObj)
        { }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
        }

        /// <summary>Является ли предмет ванильным куском металла.</summary>
        public static bool IsMetalBit(CollectibleObject obj)
        {
            return obj?.Code != null && obj.Code.Path.StartsWith("metalbit");
        }

        /// <summary>
        /// Навешивает это поведение на предмет, если его там ещё нет. Идемпотентно,
        /// безопасно вызывать многократно (на клиенте и сервере).
        /// </summary>
        public static void TryAddTo(CollectibleObject obj, ICoreAPI api)
        {
            if (obj == null || api == null) return;

            CollectibleBehavior[] old = obj.CollectibleBehaviors ?? new CollectibleBehavior[0];
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] is MetalBitAnvilBehavior) return; // уже добавлено
            }

            MetalBitAnvilBehavior beh = new MetalBitAnvilBehavior(obj);
            beh.OnLoaded(api);

            CollectibleBehavior[] arr = new CollectibleBehavior[old.Length + 1];
            Array.Copy(old, arr, old.Length);
            arr[old.Length] = beh;
            obj.CollectibleBehaviors = arr;
        }

        /// <summary>
        /// Металл, как которым кусок реально куётся.
        ///
        /// XSkills MetalRecovery превращает осколки стали в metalbit-blistersteel
        /// (Metalworking.cs: baseMaterial "steel" -> "blistersteel" без smithingplus).
        /// Blistersteel — промежуточный металл: у него один рецепт (складывание в сталь),
        /// а его workitem помечен isBlisterSteel и не принимает докидывание вокселей.
        /// Поэтому такие куски куём как сталь — это совпадает и с ванильной металлургией,
        /// и с ожиданием игрока (осколки от стальной заготовки -> снова сталь).
        /// Для обычных металлов это тождество.
        /// </summary>
        private static string ResolveForgeMetal(ItemStack stack)
        {
            string metal = null;

            // Пытаемся безопасно получить металл из словаря вариантов
            if (stack?.Collectible?.Variant != null && stack.Collectible.Variant.ContainsKey("metal"))
            {
                metal = stack.Collectible.Variant["metal"];
            }

            // Резервный вариант: извлекаем название металла из пути кода предмета
            if (string.IsNullOrEmpty(metal) && stack?.Collectible?.Code != null)
            {
                string path = stack.Collectible.Code.Path;
                if (path.StartsWith("metalbit-"))
                {
                    metal = path.Substring(9); 
                }
                else
                {
                    metal = stack.Collectible.Code.EndVariant(); // Последний запасной вариант
                }
            }

            if (metal == "blistersteel") return "steel";

            //Возвращаем пустую строку вместо null, чтобы предотвратить ArgumentNullException ниже по коду
            return metal ?? string.Empty;
        }

        /// <summary>
        /// Сколько вокселей добавляет один кусок. По умолчанию выводится из bitsForIngot
        /// (21 -> round(42/21) = 2 вокселя на кусок), что соответствует ванильной экономике.
        /// Чтобы сделать «1 кусок = целый слиток», вернуть ItemIngot.VoxelCount.
        /// </summary>
        private int VoxelsPerBit
        {
            get
            {
                int bitsForIngot = 21;
                Metalworking mw = XLeveling.Instance(api)?.GetSkill("metalworking") as Metalworking;
                if (mw?.Config is MetalworkingConfig cfg && cfg.bitsForIngot > 0)
                {
                    bitsForIngot = cfg.bitsForIngot;
                }
                return Math.Max(1, (int)Math.Round((float)ItemIngot.VoxelCount / bitsForIngot));
            }
        }

        // IAnvilWorkable

        public int GetRequiredAnvilTier(ItemStack stack)
        {
            string metalcode = ResolveForgeMetal(stack);
            int tier = 0;
            MetalPropertyVariant var;
            if (api.ModLoader.GetModSystem<SurvivalCoreSystem>(true).metalsByCode.TryGetValue(metalcode, out var))
            {
                tier = var.Tier - 1;
            }
            JsonObject attributes = stack.Collectible.Attributes;
            if (attributes != null && attributes["requiresAnvilTier"].Exists)
            {
                tier = stack.Collectible.Attributes["requiresAnvilTier"].AsInt(tier);
            }
            return tier;
        }

        public List<SmithingRecipe> GetMatchingRecipes(ItemStack stack)
        {
            // Рецепты ковки сопоставляются с базовым слитком, а не с самим куском.
            ItemStack ingotStack = GetBaseMaterial(stack);
            if (ingotStack == null) return new List<SmithingRecipe>();

            return (from r in api.GetSmithingRecipes()
                    where r.Ingredient.SatisfiesAsIngredient(ingotStack, true)
                    orderby r.Output.ResolvedItemstack.Collectible.Code
                    select r).ToList();
        }

        public bool CanWork(ItemStack stack)
        {
            float temperature = stack.Collectible.GetTemperature(api.World, stack);
            float meltingpoint = stack.Collectible.GetMeltingPoint(api.World, null, new DummySlot(stack));
            JsonObject attributes = stack.Collectible.Attributes;
            if (attributes != null && attributes["workableTemperature"].Exists)
            {
                return attributes["workableTemperature"].AsFloat(meltingpoint / 2f) <= temperature;
            }
            return temperature >= meltingpoint / 2f;
        }

        public ItemStack TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            if (!CanWork(stack)) return null;

            string metal = ResolveForgeMetal(stack);
            Item wiItem = api.World.GetItem(new AssetLocation("workitem-" + metal));
            if (wiItem == null) return null; // у этого металла нет заготовки (например припои) -> не куётся

            ItemStack workItemStack = new ItemStack(wiItem, 1);
            workItemStack.Collectible.SetTemperature(
                api.World, workItemStack,
                stack.Collectible.GetTemperature(api.World, stack), true);

            int count = VoxelsPerBit;

            if (beAnvil.WorkItemStack == null)
            {
                // первый кусок: засеваем воксели
                beAnvil.Voxels = new byte[16, 6, 16];
                AddBitVoxels(ref beAnvil.Voxels, count);
            }
            else
            {
                // добавляем к уже лежащей заготовке — логика как у слитков
                if (beAnvil.WorkItemStack.Collectible is ItemWorkItem wi && wi.isBlisterSteel)
                {
                    return null;
                }
                if (!string.Equals(beAnvil.WorkItemStack.Collectible.Variant["metal"], metal))
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "notequal",
                            Lang.Get("Must be the same metal to add voxels"));
                    }
                    return null;
                }
                if (AddBitVoxels(ref beAnvil.Voxels, count) == 0)
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "requireshammering",
                            Lang.Get("Try hammering down before adding additional voxels"));
                    }
                    return null;
                }
            }
            return workItemStack;
        }

        public ItemStack GetBaseMaterial(ItemStack stack)
        {
            string metal = ResolveForgeMetal(stack);
            Item ingot = api.World.GetItem(new AssetLocation("ingot-" + metal));
            if (ingot == null) return null;
            return new ItemStack(ingot, 1);
        }

        public EnumHelveWorkableMode GetHelveWorkableMode(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            return EnumHelveWorkableMode.NotWorkable;
        }

        public int VoxelCountForHandbook(ItemStack stack)
        {
            return VoxelsPerBit;
        }

        /// <summary>
        /// Заполняет до count металлических вокселей в той же области, что и слиток
        /// (x: 4..10, z: 6..8, y: 0..5), снизу вверх. Возвращает реально добавленное число.
        /// Работает и для засева пустой наковальни, и для добавления к существующей заготовке.
        /// </summary>
        private static int AddBitVoxels(ref byte[,,] voxels, int count)
        {
            int added = 0;
            for (int y = 0; y < 6 && added < count; y++)
            {
                for (int x = 0; x < 7 && added < count; x++)
                {
                    for (int z = 0; z < 3 && added < count; z++)
                    {
                        if (voxels[4 + x, y, 6 + z] == 0)
                        {
                            voxels[4 + x, y, 6 + z] = 1; // EnumVoxelMaterial.Metal
                            added++;
                        }
                    }
                }
            }
            return added;
        }
        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling handled)
        {
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe, ref handled);

            ItemSlot workItemSlot = allInputslots.FirstOrDefault(s => s.Itemstack?.Collectible is ItemWorkItem);

            if (workItemSlot != null && workItemSlot.Itemstack != null)
            {
                // Просим саму игру десериализовать сжатые воксели в 3D-массив
                byte[,,] voxels = ItemWorkItem.GetVoxels(workItemSlot.Itemstack);

                if (voxels != null)
                {
                    int metalVoxelCount = 0;

                    // Он пройдется по всем координатам x, y, z
                    foreach (byte voxel in voxels)
                    {
                        if (voxel == 1) // 1 = EnumVoxelMaterial.Metal
                        {
                            metalVoxelCount++;
                        }
                    }

                    // Считаем куски. Math.Floor отбросит нечетный воксель, если игрок сковал, например, 41 единицу
                    // (1 потерянный воксель спишем на стружку от зубила)
                    int expectedBits = (int)Math.Floor((float)metalVoxelCount / VoxelsPerBit);

                    expectedBits = Math.Max(1, Math.Min(expectedBits, outputSlot.Itemstack.Collectible.MaxStackSize));

                    // Выдаем честно заработанное
                    outputSlot.Itemstack.StackSize = expectedBits;
                    handled = EnumHandling.PassThrough;
                }
            }
        }
    }//!class MetalBitAnvilBehavior
}//!namespace XSkills