using HarmonyLib;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    /// <summary>
    /// Патч для BlockLiquidContainerBase.
    /// Показывает качество жидкости в тултипе и применяет его эффекты при питье
    ///
    /// BlockSaucepan (кастрюля/котёл ACA) не переопределяет ни GetContentInfo,
    /// ни tryEatStop, поэтому патч базового класса покрывает их автоматически,
    /// как и вёдра с ванильными бутылками
    ///
    /// ACulinaryArtillery.BlockBottle переопределяет tryEatStop
    /// целиком, без вызова base - для неё нужен отдельный ManualPatch
    /// </summary>
    [HarmonyPatch(typeof(BlockLiquidContainerBase))]
    public class BlockLiquidContainerBasePatch
    {
        /// <summary>
        /// Prepares the Harmony patch
        /// Only patches the methods if necessary
        /// </summary>
        /// <param name="original">The method to be patched</param>
        /// <returns>whether the method should be patched</returns>
        public static bool Prepare(MethodBase original)
        {
            XSkills xSkills = XSkills.Instance;
            if (xSkills == null) return false;
            Skill skill;
            xSkills.Skills.TryGetValue("cooking", out skill);
            Cooking cooking = skill as Cooking;

            if (!(cooking?.Enabled ?? false)) return false;
            if (original == null) return true;

            return cooking[cooking.GourmetId].Enabled;
        }

        /// <summary>
        /// Postfix for the GetContentInfo method
        /// Качество жидкости лежит на стеке содержимого, а не на самой таре.
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="inSlot">The in slot</param>
        /// <param name="dsc">The string builder</param>
        [HarmonyPostfix]
        [HarmonyPatch("GetContentInfo")]
        public static void GetContentInfoPostfix(BlockLiquidContainerBase __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            if (inSlot?.Itemstack == null) return;
            ItemStack content = __instance.GetContent(inSlot.Itemstack);
            float quality = content?.Attributes?.GetFloat("quality") ?? 0.0f;
            if (quality <= 0.0f) return;

            QualityUtil.AddQualityString(quality, dsc);
        }

        /// <summary>
        /// Prefix for the tryEatStop method
        /// Собирает состояние до глотка: сам глоток произойдёт внутри метода
        /// </summary>
        /// <param name="__instance">The instance</param>
        /// <param name="__state">The state</param>
        /// <param name="slot">The slot</param>
        /// <param name="byEntity">The entity</param>
        [HarmonyPrefix]
        [HarmonyPatch("tryEatStop")]
        public static void tryEatStopPrefix(BlockLiquidContainerBase __instance, out DrinkQualityState __state, ItemSlot slot, EntityAgent byEntity)
        {
            __state = new DrinkQualityState();
            ItemStack stack = slot?.Itemstack;
            if (stack == null || byEntity?.World == null) return;

            ItemStack content = __instance.GetContent(stack);
            if (content == null) return;

            __state.quality = content.Attributes?.GetFloat("quality") ?? 0.0f;
            if (__state.quality <= 0.0f) return;

            FoodNutritionProperties props = __instance.GetNutritionPropertiesPerLitre(byEntity.World, stack, byEntity);
            if (props == null) return;

            __state.litresBefore = __instance.GetCurrentLitres(stack);
            __state.temperature = content.Collectible?.GetTemperature(byEntity.World, content) ?? 0.0f;
            __state.food0 = props.FoodCategory;
            __state.valid = true;
        }

        /// <summary>
        /// Postfix for the tryEatStop method
        /// Считает, сколько реально выпито, и вешает эффекты качества.
        /// </summary>
        /// <param name="__instance">The instance.</param>
        /// <param name="__state">The state</param>
        /// <param name="slot">The slot</param>
        /// <param name="byEntity">The entity</param>
        [HarmonyPostfix]
        [HarmonyPatch("tryEatStop")]
        public static void tryEatStopPostfix(BlockLiquidContainerBase __instance, DrinkQualityState __state, ItemSlot slot, EntityAgent byEntity)
        {
            if (!(__state?.valid ?? false)) return;

            float litresAfter = slot?.Itemstack != null ? __instance.GetCurrentLitres(slot.Itemstack) : 0.0f;
            float drunk = __state.litresBefore - litresAfter;
            if (drunk <= 0.0f) return;

            // eaten измеряется в "порциях": у BlockMeal это порции блюда, здесь - литры
            // 1 литр ≈ 1 порция → 600 секунд эффекта на литр. Крутить здесь, если нужен баланс
            Cooking.ApplyQuality(__state.quality, drunk, __state.temperature, __state.food0, EnumFoodCategory.Unknown, byEntity);
        }
    }//!class BlockLiquidContainerBasePatch

    /// <summary>
    /// State for the tryEatStop method.
    /// </summary>
    public class DrinkQualityState
    {
        public bool valid;
        public float quality;
        public float litresBefore;
        public float temperature;
        public EnumFoodCategory food0;
    }//!class DrinkQualityState
}//!namespace XSkills