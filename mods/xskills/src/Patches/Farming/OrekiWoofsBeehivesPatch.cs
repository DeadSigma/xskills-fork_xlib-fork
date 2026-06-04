using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(GridRecipe), "ConsumeInput")]
    public class OrekiWoofsBeehivesPatch
    {
        // Проверяем слоты ДО того, как игра их очистит или заменит рамку
        [HarmonyPrefix]
        public static void Prefix(GridRecipe __instance, ItemSlot[] inputSlots, out bool __state)
        {
            __state = false; // По умолчанию считаем, что рамки нет

            // Быстрая проверка, что крафтят именно соты
            if (__instance.Output?.Code?.Path != "honeycomb" || inputSlots == null) return;

            // Ищем заполненную рамку
            foreach (ItemSlot slot in inputSlots)
            {
                if (slot?.Itemstack?.Block != null)
                {
                    if (slot.Itemstack.Block.Code.Domain == "orekiwoofsbeehives" &&
                        slot.Itemstack.Block.Code.Path == "beehiveframe-filled")
                    {
                        __state = true; 
                        break;
                    }
                }
            }
        }

        // Выдаем лут ПОСЛЕ успешного крафта, используя память из Prefix
        [HarmonyPostfix]
        public static void Postfix(GridRecipe __instance, IPlayer byPlayer, bool __state)
        {
            // Если в Prefix мы не нашли нужную рамку, прерываем выполнение
            if (!__state) return;

            // Проверка на серверную сторону
            if (byPlayer?.Entity?.World == null || byPlayer.Entity.World.Side == EnumAppSide.Client) return;

            Farming farming = XLeveling.Instance(byPlayer.Entity.World.Api)?.GetSkill("farming") as Farming;
            if (farming == null) return;

            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[farming.Id];
            if (playerSkill == null) return;

            PlayerAbility playerAbility = playerSkill[farming.BeekeeperId];

            if (playerAbility != null && playerAbility.Tier > 0)
            {
                int bonusCount = playerAbility.Value(0);

                if (bonusCount > 0)
                {
                    Item honeycombItem = byPlayer.Entity.World.GetItem(new AssetLocation("game", "honeycomb"));
                    if (honeycombItem == null) return;

                    ItemStack bonusStack = new ItemStack(honeycombItem, bonusCount);

                    if (!byPlayer.InventoryManager.TryGiveItemstack(bonusStack))
                    {
                        byPlayer.Entity.World.SpawnItemEntity(bonusStack, byPlayer.Entity.Pos.XYZ);
                    }
                }
            }
        }
    }
}