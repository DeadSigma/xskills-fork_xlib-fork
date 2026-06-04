using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Патч для применения перков кулинарии к варочным панелям из мода stonebakeoven.
    /// </summary>
    public class BlockEntityOvenCookingTopPatch : ManualPatch
    {
        public static void Apply(Harmony harmony, Type ovenType, XSkills xSkills)
        {
            if (xSkills == null) return;
            Skill skill;
            xSkills.Skills.TryGetValue("cooking", out skill);
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return;
            Type patch = typeof(BlockEntityOvenCookingTopPatch);

            var maxStackGetter = typeof(ItemSlot).GetProperty("MaxSlotStackSize").GetGetMethod();
            var maxStackPostfix = typeof(BlockEntityOvenCookingTopPatch).GetMethod("MaxSlotStackSizePostfix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            harmony.Patch(maxStackGetter, null, new HarmonyMethod(maxStackPostfix));

            var activateSlot = typeof(ItemSlot).GetMethod("ActivateSlot");
            var activateSlotPostfix = typeof(BlockEntityOvenCookingTopPatch).GetMethod("ActivateSlotPostfix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            harmony.Patch(activateSlot, null, new HarmonyMethod(activateSlotPostfix));
            
            // Патчим метод обновления GUI, чтобы ползунок не отставал
            PatchMethod(harmony, ovenType, patch, "SetDialogValues");
            // Патчим время готовки
            PatchMethod(harmony, ovenType, patch, "maxCookingTime");
            // Патчим правый клик, чтобы XskillsOwnable запоминал владельца печи
            PatchMethod(harmony, ovenType, patch, "OnPlayerRightClick");
            // Патчим сам процесс окончания готовки
            PatchMethod(harmony, ovenType, patch, "smeltItems");
        }

        public static void OnPlayerRightClickPostfix(BlockEntity __instance, IPlayer byPlayer)
        {
            BlockEntityBehaviorOwnable ownable = __instance.GetBehavior<BlockEntityBehaviorOwnable>();
            if (ownable == null) return;
            ownable.Owner = byPlayer;
        }

        public static void smeltItemsPrefix(InventoryBase ___inventory, ref CookingState __state)
        {
            __state = new CookingState();
            __state.quality = 0.0f;

            // Запоминаем исходный предмет из слота для сырья (индекс 1) ДО того, как он сгорит/превратится в еду
            // Clone() нужен, чтобы XSkills точно знал, из чего готовилась еда, даже если слот очистится
            __state.stacks = new ItemStack[] { ___inventory[1]?.Itemstack?.Clone() };
        }

        public static void smeltItemsPostfix(BlockEntity __instance, ref CookingState __state, InventoryBase ___inventory)
        {
            IPlayer byPlayer = __instance.GetBehavior<BlockEntityBehaviorOwnable>()?.Owner;
            if (byPlayer == null) return;

            // Берем слот с готовой едой (индекс 2)
            ItemSlot outputSlot = ___inventory[2];
            if (outputSlot?.Itemstack == null) return;

            Cooking cooking = byPlayer.Entity?.Api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            //  Вызываем метод XSkills, который применяет перки к стаку
            cooking.ApplyAbilities(outputSlot, byPlayer, __state.quality, 1.0f, __state.stacks);

            // Сохраняем изменения слота и блока
            outputSlot.MarkDirty();
            __instance.MarkDirty(true);
        }
        public static void MaxSlotStackSizePostfix(ItemSlot __instance, ref int __result)
        {
            // Применяем только для инвентаря печи
            if (__instance.Inventory?.ClassName != "stove") return;

            // сключаем слоты, которые не предназначены для ингредиентов
            // В InventoryCookingTop слоты 3, 4, 5, 6 это слоты ингредиентов
            int slotId = __instance.Inventory.GetSlotId(__instance);
            if (slotId < 3) return;

            ICoreAPI api = __instance.Inventory.Api;
            if (api?.World == null) return;

            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(__instance.Inventory.Pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();
            EntityPlayer player = ownable?.Owner?.Entity;
            if (player == null) return;

            // Достаем скилл и перк CanteenCook
            Cooking cooking = api.ModLoader.GetModSystem<XLeveling>()?.GetSkill("cooking") as Cooking;
            if (cooking == null) return;

            PlayerAbility ability = player.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.CanteenCookId];
            if (ability != null)
            {
                // Увеличиваем лимит слота 
                __result = (int)(__result * (1.0f + ability.FValue(0)));
            }
        }
        public static void ActivateSlotPostfix(ItemSlot __instance, ref ItemStackMoveOperation op)
        {
            // Этот метод нужен, чтобы при перекладывании предметов в GUI 
            // печка точно запоминала, кто именно сейчас в ней копается.
            if (__instance.Inventory?.ClassName != "stove") return;

            BlockEntity blockEntity = __instance.Inventory.Api?.World?.BlockAccessor?.GetBlockEntity(__instance.Inventory.Pos);
            BlockEntityBehaviorOwnable ownable = blockEntity?.GetBehavior<BlockEntityBehaviorOwnable>();

            if (op.ActingPlayer != null && ownable != null)
            {
                ownable.Owner = op.ActingPlayer;
            }
        }
        public static void maxCookingTimePostfix(BlockEntity __instance, ref float __result)
        {
            // На клиенте визуал таймера тоже должен ускоряться, поэтому здесь мы НЕ прерываем выполнение для клиента
            // Получаем готовый множитель времени из утилит XSkills
            float multiplier = CookingUtil.GetCookingTimeMultiplier(__instance);

            // Умножаем ванильное время мода на основной множитель
            __result *= multiplier;
        }
        public static void SetDialogValuesPostfix(BlockEntity __instance, ITreeAttribute dialogTree)
        {
            // Проверка - добавлен ли код оригинального атрибута максимального времени
            if (dialogTree.HasAttribute("maxOreCookingTime"))
            {
                // Достаем ванильное время, которое мод отправил в GUI
                float maxTime = dialogTree.GetFloat("maxOreCookingTime");

                // Получаем основной множитель 
                float multiplier = CookingUtil.GetCookingTimeMultiplier(__instance);

                // Перезаписываем время для ползунка с учетом нашего ускорения
                dialogTree.SetFloat("maxOreCookingTime", maxTime * multiplier);
            }
        }
    }
}