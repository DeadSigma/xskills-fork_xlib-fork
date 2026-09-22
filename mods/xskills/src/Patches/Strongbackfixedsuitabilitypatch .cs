using HarmonyLib;
using Vintagestory.API.Common;

namespace XSkills
{
    // Авто-размещение в strongback-слоты блокируется
    [HarmonyPatch(typeof(InventoryBase), "GetSuitability")]
    public class StrongBackFixedSuitabilityPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref float __result, ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            // Собственная проверка strongback-инвентаря пропускается
            if (XSkillsPlayerInventory.InOwnSuitability) return;

            // Strongback-слоты исключаются из внешнего авто-размещения
            if (targetSlot?.Inventory is XSkillsPlayerInventory)
            {
                __result = -1.0f;
            }
        }
    }
}