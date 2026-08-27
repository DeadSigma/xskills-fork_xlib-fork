using HarmonyLib;
using Vintagestory.API.Common;

namespace XSkills
{
    // strongback-слоты (подмешаны в рюкзак через InventoryPlayerBackpacksPatch) не должны быть
    // целью авто-размещения — ни shift-клика, ни авто-подбора, ни раскладки — НЕЗАВИСИМО от
    // фиксации. Класть в них можно только вручную (мышью) и сортировкой StorageTweaks.
    //
    // Патчим базовый InventoryBase.GetSuitability, потому что движок при shift-переносе спрашивает
    // suitability у РЮКЗАКА (он использует базовый метод), а не у самого strongback-инвентаря.
    //
    // Флаг InOwnSuitability: XSkillsPlayerInventory.GetSuitability внутри вызывает base.GetSuitability.
    // Если бы мы глушили и этот вызов, сломалась бы раскладка StorageTweaks (xInv.GetBestSuitedSlot
    // перестал бы находить слоты). Поэтому во время собственного вызова strongback-инвентаря
    // постфикс ничего не трогает — глушим только внешние обращения (рюкзак и т.п.).
    [HarmonyPatch(typeof(InventoryBase), "GetSuitability")]
    public class StrongBackFixedSuitabilityPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref float __result, ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            // Вызвано из собственного GetSuitability strongback-инвентаря (через base) — не трогаем.
            if (XSkillsPlayerInventory.InOwnSuitability) return;

            // Иначе suitability спросили извне (рюкзак через подмешивание, авто-подбор, shift):
            // strongback-слоты целью авто-размещения быть не должны.
            if (targetSlot?.Inventory is XSkillsPlayerInventory)
            {
                __result = -1.0f;
            }
        }
    }
}