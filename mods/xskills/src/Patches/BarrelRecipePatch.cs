using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(BarrelRecipe))]
    public class BarrelRecipePatch
    {
        [HarmonyPatch("TryCraftNow")]
        public static void Prefix(ItemSlot[] inputSlots, out ItemStack __state)
        {
            __state = inputSlots?[1]?.Itemstack?.Clone();
        }

        [HarmonyPatch("TryCraftNow")]
        public static void Postfix(ItemSlot[] inputSlots, ItemStack __state, bool __result)
        {
            if (!__result) return;
            if (inputSlots == null || inputSlots.Length < 2 || __state == null) return;
            int size = 0;

            if (inputSlots[1].Itemstack == null)
            {
                size = 0;
                inputSlots[1].Itemstack = __state;
            }
            else if (inputSlots[1].Itemstack.Collectible != __state.Collectible) return;
            else size = inputSlots[1].Itemstack.StackSize;

            size += (int)((__state.StackSize - size) * (1.0f - __state.Attributes.GetFloat("usage", 1.0f)));
            if (size > 0 && size != inputSlots[1].Itemstack.StackSize)
            {
                //round to the nearest litre
                int remainder = size % 100;
                size -= remainder;
                remainder = remainder >= 50 ? 100 : 0; 
                inputSlots[1].Itemstack.StackSize = size + remainder;
            }
            if (size <= 0) inputSlots[1].Itemstack = null;
            inputSlots[1].MarkDirty();
        }
    }//!class BarrelRecipePatch

    [HarmonyPatch(typeof(BlockEntityBarrel))]
    public class BlockEntityBarrelPatch
    {
        [HarmonyPatch("OnReceivedClientPacket")]
        public static void Postfix(BlockEntityBarrel __instance, IPlayer player, int packetid)
        {
            if (packetid == 7)
            {
                __instance.Inventory?[1]?.Itemstack?.Attributes.RemoveAttribute("usage");
            }

            //seald
            if (packetid == 1337 && (
                __instance.CurrentRecipe.Code.Contains("soakedhide") ||
                __instance.CurrentRecipe.Code.Contains("preparedhide") ||
                __instance.CurrentRecipe.Code.Contains("leather-plain")))
            {
                Husbandry husbandry = XLeveling.Instance(__instance.Api)?.GetSkill("husbandry") as Husbandry;
                if (husbandry == null) return;
                PlayerAbility playerAbility = player.Entity?.GetBehavior<PlayerSkillSet>()?[husbandry.Id]?[husbandry.TannerId];
                if (playerAbility == null) return;

                __instance.Inventory?[1]?.Itemstack?.Attributes.SetFloat("usage", 1.0f - playerAbility.SkillDependentFValue());
            }
        }
    }//!class BarrelPatch
}//!namespace XSkills
