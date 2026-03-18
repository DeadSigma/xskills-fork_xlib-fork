using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace XSkills
{
    /// <summary>
    /// The patch for the BlockCrock class.
    /// </summary>
    public class BlockCrockPatch
    {
        /// <summary>
        /// Postfix for the OnPickBlock method.
        /// </summary>
        /// <param name="__result">The result.</param>
        /// <param name="world">The world.</param>
        /// <param name="pos">The position.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Block), "OnPickBlock")] // Явно указываем, что ищем в классе Block
        public static void OnPickBlockPostfix(ItemStack __result, IWorldAccessor world, BlockPos pos)
        {
            QualityUtil.PickQuality(__result, world, pos);
        }

        /// <summary>
        /// Postfix for the GetPlacedBlockInfo method.
        /// </summary>
        /// <param name="__result">The result.</param>
        /// <param name="world">The world.</param>
        /// <param name="pos">The position.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Block), "GetPlacedBlockInfo")] // Явно указываем, что ищем в классе Block
        public static void GetPlacedBlockInfoPostfix(ref string __result, IWorldAccessor world, BlockPos pos)
        {
            float quality = QualityUtil.GetQuality(world, pos);
            if (quality <= 0.0f) return;
            __result += QualityUtil.QualityString(quality);
        }

        /// <summary>
        /// Postfix for the OnCreatedByCrafting method.
        /// Sealing crocks reduces quality by 20%.
        /// </summary>
        /// <param name="allInputSlots">All inputslots.</param>
        /// <param name="outputSlot">The output slot.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CollectibleObject), "OnCreatedByCrafting")] // Ищем в CollectibleObject
        public static void OnCreatedByCraftingPostfix(ItemSlot[] allInputSlots, ItemSlot outputSlot)
        {
            // Так как мы теперь патчим CollectibleObject, проверяем, является ли предмет горшком
            if (!(outputSlot.Itemstack?.Collectible is BlockCrock)) return;

            for (int i = 0; i < allInputSlots.Length; i++)
            {
                ItemSlot slot = allInputSlots[i];
                if (slot.Itemstack?.Collectible is BlockCrock)
                {
                    float quality = QualityUtil.GetQuality(slot);
                    if (quality > 0.0f) outputSlot.Itemstack.Attributes.SetFloat("quality", quality * 0.8f);
                    return;
                }
            }
        }

        /// <summary>
        /// Prefix for the OnContainedInteractStart method.
        /// Sealing crocks reduces quality by 20%.
        /// </summary>
        /// <param name="be">The block entity container.</param>
        /// <param name="slot">The slot.</param>
        /// <param name="byPlayer">The player.</param>
        /// <param name="blockSel">The block selection.</param>
        /// <param name="__state">if set to <c>true</c> the crock was sealed.</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockCrock), "OnContainedInteractStart")] // Это специфично для горшка
        public static void OnContainedInteractStartPrefix(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, out bool __state)
        {
            __state = slot.Itemstack?.Attributes.GetBool("sealed", false) ?? false;
        }

        /// <summary>
        /// Postfix for the OnContainedInteractStart method.
        /// Sealing crocks reduces quality by 20%.
        /// </summary>
        /// <param name="be">The block entity container.</param>
        /// <param name="slot">The slot.</param>
        /// <param name="byPlayer">The player.</param>
        /// <param name="blockSel">The block selection.</param>
        /// <param name="__state">if set to <c>true</c> the crock was sealed.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockCrock), "OnContainedInteractStart")] // Это специфично для горшка
        public static void OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, bool __state)
        {
            if (__state) return;
            if (slot.Itemstack == null || !slot.Itemstack.Attributes.GetBool("sealed", false)) return;

            float quality = QualityUtil.GetQuality(slot);
            if (quality > 0.0f) slot.Itemstack.Attributes.SetFloat("quality", quality * 0.8f);
        }
    }//!class BlockCrockPatch
}//!namespace XSkills