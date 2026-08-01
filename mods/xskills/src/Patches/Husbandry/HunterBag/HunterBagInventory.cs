using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace XSkills
{
    // Кастомный слот: принимает только сумку мясника (butcherybag)
    public class ItemSlotHunterBag : ItemSlotSurvival
    {
        public ItemSlotHunterBag(InventoryBase inventory) : base(inventory) { }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            if (!HunterBagInventory.IsButcheryBag(sourceSlot?.Itemstack)) return false;
            return base.CanHold(sourceSlot);
        }
    }

    // Инвентарь игрока: слот(ы) под сумку + порождаемые сумкой слоты содержимого
    public class HunterBagInventory : InventoryBasePlayer
    {
        private readonly ICoreAPI api;

        // Слоты под сами сумки (число задаёт перк, сейчас 1)
        private ItemSlot[] bagSlots;

        // Слоты содержимого, порождённые вложенными сумками через held-bag систему
        private readonly List<ItemSlot> contentSlots = new List<ItemSlot>();

        // Комбинированный вид: сначала слоты сумок, затем слоты содержимого.
        private ItemSlot[] slots;

        /// <summary>Срабатывает при изменении ЧИСЛА слотов (сумку положили/забрали) - HUD пересобирается</summary>
        public event Action SlotCountChanged;

        public HunterBagInventory(string className, string playerUID, ICoreAPI api) : base(className, playerUID, api)
        {
            this.api = api;
            bagSlots = GenBagSlots(1);
            RebuildCombined();
        }

        public HunterBagInventory(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            this.api = api;
            bagSlots = GenBagSlots(1);
            RebuildCombined();
        }

        private ItemSlot[] GenBagSlots(int n)
        {
            ItemSlot[] arr = new ItemSlot[n];
            for (int i = 0; i < n; i++) arr[i] = new ItemSlotHunterBag(this);
            return arr;
        }

        public override int Count => slots.Length;

        /// <summary>Число слотов под сами сумки (остальные слоты - содержимое сумок)</summary>
        public int BagSlotCount => bagSlots.Length;

        public override ItemSlot this[int slotId]
        {
            get => (slotId < 0 || slotId >= slots.Length) ? null : slots[slotId];
            set { if (slotId >= 0 && slotId < slots.Length) slots[slotId] = value; }
        }

        protected override ItemSlot NewSlot(int slotId) => new ItemSlotHunterBag(this);

        // Проверка предмета в одном месте (используется и слотом, и инвентарём)
        internal static bool IsButcheryBag(ItemStack stack)
        {
            string path = stack?.Collectible?.Code?.Path;
            return path != null && path.Contains("butcherybag");
        }

        // Определяет, является ли слот слотом ПОД СУМКУ (а не слотом содержимого)
        private bool IsBagSlot(ItemSlot slot) => Array.IndexOf(bagSlots, slot) >= 0;

        // Главный шлюз: shift-клик, автоподбор при луте и моды-сортировки ищут слот через GetBestSuitedSlot, а не через ItemSlot.CanHold. Поэтому для не-сумок исключаем слоты под сумку из кандидатов - тогда туда ничего лишнего не попадёт ни одним путём.
        // (Слоты содержимого остаются доступны - это внутренность самой сумки)
        public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op = null, List<ItemSlot> skipSlots = null)
        {
            if (!IsButcheryBag(sourceSlot?.Itemstack))
            {
                List<ItemSlot> skip = skipSlots != null ? new List<ItemSlot>(skipSlots) : new List<ItemSlot>();
                for (int i = 0; i < bagSlots.Length; i++)
                    if (!skip.Contains(bagSlots[i])) skip.Add(bagSlots[i]);
                skipSlots = skip;
            }
            return base.GetBestSuitedSlot(sourceSlot, op, skipSlots);
        }

        // Страховка для путей, идущих через инвентарный CanContain (часть модов-сортировок)
        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            if (IsBagSlot(sinkSlot) && !IsButcheryBag(sourceSlot?.Itemstack)) return false;
            return base.CanContain(sinkSlot, sourceSlot);
        }


        // Сериализуем ТОЛЬКО слоты сумок. Содержимое живёт в атрибутах самого стека сумки, поэтому сохраняется вместе с ней и не должно писаться отдельно (иначе дубли)
        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            bagSlots = SlotsFromTreeAttributes(tree, bagSlots);
            RebuildContentSlots();

            ITreeAttribute contentTree = tree.GetTreeAttribute("hunterbagcontent");
            if (contentTree != null)
            {
                int n = contentTree.GetInt("count");
                for (int i = 0; i < contentSlots.Count && i < n; i++)
                {
                    ItemStack st = contentTree.GetItemstack("s" + i);
                    if (st == null) continue;
                    if (api?.World != null) st.ResolveBlockOrItem(api.World);
                    contentSlots[i].Itemstack = st;
                }
            }

            UpdateWeightPenalty();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(bagSlots, tree);

            TreeAttribute contentTree = new TreeAttribute();
            contentTree.SetInt("count", contentSlots.Count);
            for (int i = 0; i < contentSlots.Count; i++)
            {
                ItemStack st = contentSlots[i]?.Itemstack;
                if (st != null) contentTree.SetItemstack("s" + i, st.Clone());
            }
            tree["hunterbagcontent"] = contentTree;
        }

        // Меняем число слотов под сумки (перк). Слоты содержимого пересобираются следом
        public void SetSize(int size)
        {
            if (size < 0) size = 0;
            if (bagSlots.Length == size) return;

            ItemSlot[] newBags = new ItemSlot[size];
            for (int i = 0; i < size; i++)
                newBags[i] = i < bagSlots.Length ? bagSlots[i] : new ItemSlotHunterBag(this);
            bagSlots = newBags;

            RebuildContentSlots();
            SlotCountChanged?.Invoke();
        }

        // Пересобираем слоты содержимого из всех вложенных сумок
        private void RebuildContentSlots()
        {
            List<ItemStack> preserved = new List<ItemStack>(contentSlots.Count);
            for (int i = 0; i < contentSlots.Count; i++) preserved.Add(contentSlots[i]?.Itemstack);

            contentSlots.Clear();

            for (int b = 0; b < bagSlots.Length; b++)
            {
                ItemStack stack = bagSlots[b]?.Itemstack;
                if (stack == null) continue;

                IHeldBag heldBag = GetHeldBag(stack);
                if (heldBag == null) continue;

                try
                {
                    // ФЛАГ API: сигнатура GetOrCreateSlots(bagstack, parentinv, bagIndex, world) - если на 1.22 отличается, изменить тут
                    List<ItemSlotBagContent> created = heldBag.GetOrCreateSlots(stack, this, b, api?.World);
                    if (created != null)
                    {
                        for (int i = 0; i < created.Count; i++) contentSlots.Add(created[i]);
                    }
                }
                catch (Exception) { /* сумка без held-bag поведения или иная сигнатура - пропускаем */ }
            }

            // Возвращаем сохранённое содержимое в совпадающие по индексу пустые слоты
            for (int i = 0; i < contentSlots.Count && i < preserved.Count; i++)
            {
                if (preserved[i] != null && contentSlots[i].Itemstack == null)
                    contentSlots[i].Itemstack = preserved[i];
            }

            RebuildCombined();
        }

        private void RebuildCombined()
        {
            ItemSlot[] combined = new ItemSlot[bagSlots.Length + contentSlots.Count];
            Array.Copy(bagSlots, 0, combined, 0, bagSlots.Length);
            for (int i = 0; i < contentSlots.Count; i++) combined[bagSlots.Length + i] = contentSlots[i];
            slots = combined;
        }

        // Когда меняется слот сумки - пересобрать содержимое и, если число слотов изменилось, пересобрать HUD
        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);

            // Реагируем только на изменение слота(ов) сумки, а не самого содержимого
            if (Array.IndexOf(bagSlots, slot) >= 0)
            {
            // Сумку вынули/заменили - её содержимое осиротело:  оно живёт в слотах содержимого и в стек сумки не записывается, поэтому при простой пересборке пропало бы. Роняем его дропом (на сервере), чтобы не терялось
                DropOrphanedContent();

                int before = slots.Length;
                RebuildContentSlots();
                if (slots.Length != before) SlotCountChanged?.Invoke();
            }

            // Любое изменение (сумка или содержимое) может изменить вес - пересчитываем штраф скорости
            UpdateWeightPenalty();
        }

        // Выбрасывает текущее содержимое слотов сумки предметами в мир (только на сервере).
        private void DropOrphanedContent()
        {
            if (api == null || api.Side != EnumAppSide.Server) return;
            if (contentSlots.Count == 0) return;

            Vec3d pos = Player?.Entity?.Pos?.XYZ;
            if (pos == null) return;
            pos = pos.AddCopy(0.0, 0.5, 0.0);

            foreach (ItemSlot slot in contentSlots)
            {
                if (slot?.Itemstack == null) continue;

                api.World.SpawnItemEntity(slot.Itemstack.Clone(), pos);
                slot.Itemstack = null;
                slot.MarkDirty();
            }
        }

        // Смерть игрока: роняем дропом всё, и убираем слоты содержимого
        // Вызывается на сервере из обработчика смерти в Husbandry. Очистка содержимого до синхронизации не даёт клиенту получить обновление несуществующего слота (краш "slot 1 but max is 0")
        public void DropAllAndClear(Vec3d pos)
        {
            if (api == null || api.Side != EnumAppSide.Server) return;

            Vec3d at = pos ?? Player?.Entity?.Pos?.XYZ;
            if (at == null) return;
            at = at.AddCopy(0.0, 0.5, 0.0);

            foreach (ItemSlot slot in slots) // сумки + содержимое
            {
                if (slot?.Itemstack == null) continue;

                api.World.SpawnItemEntity(slot.Itemstack.Clone(), at);
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            // Сумок больше нет - слоты содержимого убираются, размер инвентаря стабилизируется
            RebuildContentSlots();
            SlotCountChanged?.Invoke();
            UpdateWeightPenalty();
        }

        // Штраф скорости передвижения за вес содержимого сумки Мод Butchering замедляет игрока, сканируя рюкзак, слот - отдельный инвентарь, поэтому вес его содержимого учитывается через стат "walkspeed"

        private const string WalkSpeedStatCode = "hunterbagweight";

        private const float WalkSpeedPenaltyPerItem = 0.1f;

        private static float PerStackWalkSpeedPenalty(ItemStack stack)
        {
            JsonObject attrs = stack?.Collectible?.Attributes;
            if (attrs == null) return 0f;
            return attrs["mass"].AsFloat(0f);
        }

        // Пересчитывает и применяет штраф скорости (только на сервере; клиенту стат синхронизируется)
        public void UpdateWeightPenalty()
        {
            if (api == null || api.Side != EnumAppSide.Server) return;

            var entity = Player?.Entity;
            if (entity == null) return;

            int itemCount = 0;
            for (int i = 0; i < contentSlots.Count; i++)
            {
                ItemStack st = contentSlots[i]?.Itemstack;
                if (st != null) itemCount += st.StackSize;
            }

            if (itemCount <= 0)
            {
                entity.Stats.Remove("walkspeed", WalkSpeedStatCode);
                return;
            }

            float penalty = 0f;
            for (int i = 0; i < contentSlots.Count; i++)
            {
                ItemStack st = contentSlots[i]?.Itemstack;
                if (st != null) penalty += PerStackWalkSpeedPenalty(st) * st.StackSize;
            }

            string playerClass = entity.WatchedAttributes.GetString("characterClass");
            if (playerClass != null && playerClass == "innkeeper")
            {
                penalty *= 0.5f;
            }

            if (penalty <= 0f) { entity.Stats.Remove("walkspeed", WalkSpeedStatCode); return; }
            if (penalty > 0.9f) penalty = 0.9f;

            entity.Stats.Set("walkspeed", WalkSpeedStatCode, -penalty, false);
        }

        // Достаём held-bag поведение из стека без завязки на точное имя GetCollectibleInterface<>
        private static IHeldBag GetHeldBag(ItemStack stack)
        {
            CollectibleObject coll = stack?.Collectible;
            if (coll == null) return null;

            if (coll is IHeldBag direct) return direct;

            if (coll.CollectibleBehaviors != null)
            {
                foreach (CollectibleBehavior beh in coll.CollectibleBehaviors)
                    if (beh is IHeldBag b) return b;
            }
            return null;
        }
    }
}