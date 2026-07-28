using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace xskills.src.Patches.Collabs.PandaxskillsIntegration
{
    
    // Patch 1: BlockBarrelInspectorPatch
    // Выводит в интерфейсе бочки информацию о времени брожения (перк BarrelInspector)
    
    [HarmonyPatch(typeof(BlockBarrel))]
    public class BlockBarrelInspectorPatch
    {
        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("brewing", out skill);
            Brewing brewing = skill as Brewing;
            return brewing != null && brewing.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetPlacedBlockInfo")]
        public static void GetPlacedBlockInfoPostfix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, ref string __result)
        {
            if (((forPlayer != null) ? forPlayer.Entity : null) == null)
            {
                return;
            }
            XSkills instance = XSkills.Instance;
            Brewing brewing = ((instance != null) ? instance.Skills["brewing"] : null) as Brewing;
            if (brewing == null)
            {
                return;
            }
            PlayerSkillSet behavior = forPlayer.Entity.GetBehavior<PlayerSkillSet>();
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[brewing.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[brewing.BarrelInspectorId] : null);
            }
            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 == null || playerAbility2.Tier <= 0)
            {
                return;
            }
            BlockEntityBarrel blockEntityBarrel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBarrel;
            if (blockEntityBarrel == null || blockEntityBarrel.CurrentRecipe == null || !blockEntityBarrel.Sealed)
            {
                return;
            }
            double sealHours = blockEntityBarrel.CurrentRecipe.SealHours;
            double num = world.Calendar.TotalHours - blockEntityBarrel.SealedSinceTotalHours;
            double num2 = Math.Max(0.0, sealHours - num);
            StringBuilder stringBuilder = new StringBuilder(__result ?? "");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.Format("[XSkills] Fermentation: {0:0.0}h remaining of {1:0.0}h", num2, sealHours));
            __result = stringBuilder.ToString();
        }
    }

    
    // Patch 2: BlockEntityBarrelBrewingPatch
    // Отслеживает процесс запечатывания, начисляет опыт и возвращает ингредиенты
    
    [HarmonyPatch(typeof(BlockEntityBarrel))]
    public class BlockEntityBarrelBrewingPatch
    {
        internal static readonly Dictionary<BlockPos, string> brewerUidByBarrel = new Dictionary<BlockPos, string>();

        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("brewing", out skill);
            Brewing brewing = skill as Brewing;
            return brewing != null && brewing.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnReceivedClientPacket")]
        public static void OnReceivedClientPacketPostfix(BlockEntityBarrel __instance, IPlayer player, int packetid)
        {
            if (packetid != 1337)
            {
                return;
            }
            if (player == null)
            {
                return;
            }
            BarrelRecipe currentRecipe = __instance.CurrentRecipe;
            string text;
            if (currentRecipe == null)
            {
                text = null;
            }
            else
            {
                string code = currentRecipe.Code;
                text = ((code != null) ? code.ToString() : null);
            }
            string text2 = text;
            if (string.IsNullOrEmpty(text2))
            {
                return;
            }
            if (!BlockEntityBarrelBrewingPatch.IsBrewingRecipe(text2))
            {
                return;
            }
            Brewing brewing = XSkills.Instance.Skills["brewing"] as Brewing;
            if (brewing == null)
            {
                return;
            }
            PlayerSkillSet behavior = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[brewing.Id] : null;
            if (playerSkill == null)
            {
                return;
            }

            if (__instance.Pos != null && player.PlayerUID != null)
            {
                BlockEntityBarrelBrewingPatch.brewerUidByBarrel[__instance.Pos.Copy()] = player.PlayerUID;
            }

            playerSkill.AddExperience(3f, true);

            PlayerAbility playerAbility = playerSkill[brewing.ThriftyBrewerId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                double num = (double)playerAbility.FValue(0, 0f);
                foreach (ItemSlot itemSlot in __instance.Inventory)
                {
                    if (((itemSlot != null) ? itemSlot.Itemstack : null) != null && player.Entity.World.Rand.NextDouble() < num)
                    {
                        itemSlot.Itemstack.StackSize++;
                        itemSlot.MarkDirty();
                    }
                }
            }
        }

        internal static bool IsBrewingRecipe(string code)
        {
            return code.Contains("cider") || code.Contains("mead") || code.Contains("beer") || code.Contains("wine") || code.Contains("ale") || code.Contains("perry");
        }

        internal static BlockEntityBarrelBrewingPatch.BrewType ClassifyBrew(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return BlockEntityBarrelBrewingPatch.BrewType.None;
            }
            if (code.Contains("mead"))
            {
                return BlockEntityBarrelBrewingPatch.BrewType.Mead;
            }
            if (code.Contains("beer") || code.Contains("ale"))
            {
                return BlockEntityBarrelBrewingPatch.BrewType.Beer;
            }
            if (code.Contains("wine") || code.Contains("cider") || code.Contains("perry"))
            {
                return BlockEntityBarrelBrewingPatch.BrewType.Wine;
            }
            return BlockEntityBarrelBrewingPatch.BrewType.None;
        }

        internal static bool IsGrainInput(ItemSlot slot)
        {
            string text;
            if (slot == null)
            {
                text = null;
            }
            else
            {
                ItemStack itemstack = slot.Itemstack;
                if (itemstack == null)
                {
                    text = null;
                }
                else
                {
                    CollectibleObject collectible = itemstack.Collectible;
                    if (collectible == null)
                    {
                        text = null;
                    }
                    else
                    {
                        AssetLocation code = collectible.Code;
                        text = ((code != null) ? code.Path : null);
                    }
                }
            }
            string text2 = text;
            return !string.IsNullOrEmpty(text2) && (text2.Contains("grain") || text2.Contains("barley") || text2.Contains("rye") || text2.Contains("oat") || text2.Contains("malt"));
        }

        internal enum BrewType
        {
            None,
            Beer,
            Wine,
            Mead
        }
    }

    
    // Patch 3: BarrelCompletionPatch
    // Обрабатывает завершение работы бочки и увеличивает количество продукта
    
    [HarmonyPatch(typeof(BlockEntityBarrel))]
    public class BarrelCompletionPatch
    {
        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("brewing", out skill);
            Brewing brewing = skill as Brewing;
            return brewing != null && brewing.Enabled;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnEvery3Second")]
        public static void OnEvery3SecondPrefix(BlockEntityBarrel __instance, out BarrelCompletionPatch.CompletionContext __state)
        {
            BarrelCompletionPatch.CompletionContext completionContext = default(BarrelCompletionPatch.CompletionContext);
            completionContext.preOutSize = ((__instance != null) ? __instance.CurrentOutSize : 0);
            ItemStack preFirstStack;
            if (__instance == null)
            {
                preFirstStack = null;
            }
            else
            {
                InventoryBase inventory = __instance.Inventory;
                if (inventory == null)
                {
                    preFirstStack = null;
                }
                else
                {
                    ItemSlot itemSlot = inventory[0];
                    if (itemSlot == null)
                    {
                        preFirstStack = null;
                    }
                    else
                    {
                        ItemStack itemstack = itemSlot.Itemstack;
                        preFirstStack = ((itemstack != null) ? itemstack.Clone() : null);
                    }
                }
            }
            completionContext.preFirstStack = preFirstStack;
            bool wasBrewing;
            if (((__instance != null) ? __instance.CurrentRecipe : null) != null)
            {
                string code = __instance.CurrentRecipe.Code;
                wasBrewing = BlockEntityBarrelBrewingPatch.IsBrewingRecipe(((code != null) ? code.ToString() : null) ?? "");
            }
            else
            {
                wasBrewing = false;
            }
            completionContext.wasBrewing = wasBrewing;
            __state = completionContext;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnEvery3Second")]
        public static void OnEvery3SecondPostfix(BlockEntityBarrel __instance, BarrelCompletionPatch.CompletionContext __state)
        {
            if (((__instance != null) ? __instance.Pos : null) == null)
            {
                return;
            }
            if (!__state.wasBrewing)
            {
                return;
            }
            if (__instance.CurrentRecipe != null || __state.preFirstStack == null)
            {
                return;
            }
            string playerUid;
            if (!BlockEntityBarrelBrewingPatch.brewerUidByBarrel.TryGetValue(__instance.Pos, out playerUid))
            {
                return;
            }

            BlockEntityBarrelBrewingPatch.brewerUidByBarrel.Remove(__instance.Pos);
            ICoreServerAPI coreServerAPI = __instance.Api as ICoreServerAPI;
            IServerPlayer serverPlayer = ((coreServerAPI != null) ? coreServerAPI.World.PlayerByUid(playerUid) : null) as IServerPlayer;
            if (((serverPlayer != null) ? serverPlayer.Entity : null) == null)
            {
                return;
            }

            XSkills instance = XSkills.Instance;
            Brewing brewing = ((instance != null) ? instance.Skills["brewing"] : null) as Brewing;
            if (brewing == null)
            {
                return;
            }
            PlayerSkillSet behavior = serverPlayer.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill playerSkill = (behavior != null) ? behavior[brewing.Id] : null;
            if (playerSkill == null)
            {
                return;
            }

            float num = 1f;

            PlayerAbility playerAbility = playerSkill[brewing.PureYeastId];
            if (playerAbility != null && playerAbility.Tier > 0)
            {
                num += playerAbility.FValue(0, 0f);
            }

            PlayerAbility playerAbility2 = playerSkill[brewing.MaltMasterId];
            if (playerAbility2 != null && playerAbility2.Tier > 0 && __state.preFirstStack != null)
            {
                CollectibleObject collectible = __state.preFirstStack.Collectible;
                string text;
                if (collectible == null)
                {
                    text = null;
                }
                else
                {
                    AssetLocation code = collectible.Code;
                    text = ((code != null) ? code.Path : null);
                }
                string text2 = text ?? "";
                if (text2.Contains("grain") || text2.Contains("barley") || text2.Contains("rye") || text2.Contains("oat") || text2.Contains("malt"))
                {
                    num += playerAbility2.FValue(0, 0f);
                }
            }

            InventoryBase inventory = __instance.Inventory;
            string text3;
            if (inventory == null)
            {
                text3 = null;
            }
            else
            {
                ItemSlot itemSlot = inventory[0];
                if (itemSlot == null)
                {
                    text3 = null;
                }
                else
                {
                    ItemStack itemstack = itemSlot.Itemstack;
                    if (itemstack == null)
                    {
                        text3 = null;
                    }
                    else
                    {
                        CollectibleObject collectible2 = itemstack.Collectible;
                        if (collectible2 == null)
                        {
                            text3 = null;
                        }
                        else
                        {
                            AssetLocation code2 = collectible2.Code;
                            text3 = ((code2 != null) ? code2.Path : null);
                        }
                    }
                }
            }
            string str = text3 ?? "";
            ItemStack preFirstStack = __state.preFirstStack;
            string text4;
            if (preFirstStack == null)
            {
                text4 = null;
            }
            else
            {
                CollectibleObject collectible3 = preFirstStack.Collectible;
                if (collectible3 == null)
                {
                    text4 = null;
                }
                else
                {
                    AssetLocation code3 = collectible3.Code;
                    text4 = ((code3 != null) ? code3.Path : null);
                }
            }
            string str2 = text4 ?? "";
            BlockEntityBarrelBrewingPatch.BrewType brewType = BlockEntityBarrelBrewingPatch.ClassifyBrew(str + " " + str2);
            int num2;
            switch (brewType)
            {
                case BlockEntityBarrelBrewingPatch.BrewType.Beer:
                    num2 = brewing.HopWhispererId;
                    break;
                case BlockEntityBarrelBrewingPatch.BrewType.Wine:
                    num2 = brewing.VintnerId;
                    break;
                case BlockEntityBarrelBrewingPatch.BrewType.Mead:
                    num2 = brewing.MeadMasterId;
                    break;
                default:
                    num2 = -1;
                    break;
            }

            int num3 = num2;
            if (num3 >= 0)
            {
                PlayerAbility playerAbility3 = playerSkill[num3];
                if (playerAbility3 != null && playerAbility3.Tier > 0)
                {
                    num += playerAbility3.FValue(0, 0f);
                }
            }

            if (num > 1.001f)
            {
                InventoryBase inventory2 = __instance.Inventory;
                bool flag;
                if (inventory2 == null)
                {
                    flag = (null != null);
                }
                else
                {
                    ItemSlot itemSlot2 = inventory2[0];
                    flag = (((itemSlot2 != null) ? itemSlot2.Itemstack : null) != null);
                }
                if (flag)
                {
                    int stackSize = __instance.Inventory[0].Itemstack.StackSize;
                    int num4 = (int)Math.Round((double)((float)stackSize * num));
                    if (num4 > stackSize)
                    {
                        __instance.Inventory[0].Itemstack.StackSize = num4;
                        __instance.Inventory[0].MarkDirty();
                    }
                }
            }
        }

        public struct CompletionContext
        {
            public int preOutSize;
            public ItemStack preFirstStack;
            public bool wasBrewing;
        }
    }

    
    // Patch 4: BarrelTransitionSpeedPatch
    // Ускоряет процесс брожения с помощью перка SteadyHand
    
    [HarmonyPatch(typeof(BlockEntityBarrel))]
    public class BarrelTransitionSpeedPatch
    {
        public static bool Prepare(MethodBase original)
        {
            XSkills instance = XSkills.Instance;
            if (instance == null)
            {
                return false;
            }
            Skill skill;
            instance.Skills.TryGetValue("brewing", out skill);
            Brewing brewing = skill as Brewing;
            return brewing != null && brewing.Enabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Inventory_OnAcquireTransitionSpeed1")]
        public static void TransitionSpeedPostfix(BlockEntityBarrel __instance, ref float __result)
        {
            if (((__instance != null) ? __instance.Pos : null) == null)
            {
                return;
            }
            if (__instance.CurrentRecipe == null)
            {
                return;
            }
            string code = __instance.CurrentRecipe.Code;
            if (!BlockEntityBarrelBrewingPatch.IsBrewingRecipe(((code != null) ? code.ToString() : null) ?? ""))
            {
                return;
            }

            string playerUid;
            if (!BlockEntityBarrelBrewingPatch.brewerUidByBarrel.TryGetValue(__instance.Pos, out playerUid))
            {
                return;
            }
            ICoreServerAPI coreServerAPI = __instance.Api as ICoreServerAPI;
            IServerPlayer serverPlayer = ((coreServerAPI != null) ? coreServerAPI.World.PlayerByUid(playerUid) : null) as IServerPlayer;
            if (((serverPlayer != null) ? serverPlayer.Entity : null) == null)
            {
                return;
            }

            XSkills instance = XSkills.Instance;
            Brewing brewing = ((instance != null) ? instance.Skills["brewing"] : null) as Brewing;
            if (brewing == null)
            {
                return;
            }
            PlayerSkillSet behavior = serverPlayer.Entity.GetBehavior<PlayerSkillSet>();
            PlayerAbility playerAbility;
            if (behavior == null)
            {
                playerAbility = null;
            }
            else
            {
                PlayerSkill playerSkill = behavior[brewing.Id];
                playerAbility = ((playerSkill != null) ? playerSkill[brewing.SteadyHandId] : null);
            }

            PlayerAbility playerAbility2 = playerAbility;
            if (playerAbility2 != null && playerAbility2.Tier > 0)
            {
                float num = playerAbility2.FValue(0, 0f);
                if (num >= 0.99f)
                {
                    num = 0.99f;
                }

                float num2 = 1f / (1f - num);

                if (num2 > 4f)
                {
                    num2 = 4f;
                }

                __result *= num2;
            }
        }
    }
}