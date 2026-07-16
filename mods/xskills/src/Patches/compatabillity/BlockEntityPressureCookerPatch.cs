using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public class BlockEntityPressureCookerPatch
    {
        // Слоты банок в котле (0 = топливо, 1 = вода, 2..5 = банки)
        private static readonly int[] CanSlots = { 2, 3, 4, 5 };

        // Доля кулинарного опыта за стерилизацию (вторичный шаг, не полная готовка).
        private const float PreserveExpMultiplier = 0.2f;

        private static bool Debug = false;
        private static void Log(ICoreAPI api, string msg)
        {
            if (Debug) api?.Logger?.Notification("[XSkills/PressureCooker] " + msg);
        }

        public static void Apply(Harmony harmony, Type cookerType)
        {
            if (cookerType == null)
            {
                Console.WriteLine("[XSkills/PressureCooker] Apply: cookerType == null - патч НЕ применён (нет вызова в XSkills.cs?)");
                return;
            }
            Console.WriteLine("[XSkills/PressureCooker] Apply для типа: " + cookerType.FullName);

            var tick = cookerType.GetMethod("OnServerTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Console.WriteLine("[XSkills/PressureCooker] OnServerTick найден: " + (tick != null));
            if (tick != null)
            {
                harmony.Patch(tick,
                    prefix: new HarmonyMethod(typeof(BlockEntityPressureCookerPatch).GetMethod(nameof(OnServerTickPrefix))),
                    postfix: new HarmonyMethod(typeof(BlockEntityPressureCookerPatch).GetMethod(nameof(OnServerTickPostfix))));
            }

            var preserve = cookerType.GetMethod("TryPreserveCanInSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            Console.WriteLine("[XSkills/PressureCooker] TryPreserveCanInSlot найден: " + (preserve != null));
            if (preserve != null)
            {
                harmony.Patch(preserve,
                    prefix: new HarmonyMethod(typeof(BlockEntityPressureCookerPatch).GetMethod(nameof(TryPreserveCanInSlotPrefix))),
                    postfix: new HarmonyMethod(typeof(BlockEntityPressureCookerPatch).GetMethod(nameof(TryPreserveCanInSlotPostfix))));
            }
        }

        // Повар: владелец котла (XskillsOwnable) ближайший игрок
        private static IPlayer ResolveCook(BlockEntityContainer be)
        {
            ICoreAPI api = be?.Api;
            if (api == null) return null;

            IPlayer owner = be.GetBehavior<BlockEntityBehaviorOwnable>()?.Owner;
            if (owner?.Entity != null) return owner;

            if (be.Pos != null)
                return api.World.NearestPlayer(be.Pos.X + 0.5, be.Pos.Y + 0.5, be.Pos.Z + 0.5);

            return null;
        }

        private static Cooking GetCooking() => XSkills.Instance?.Skills["cooking"] as Cooking;

        //  Тик:  fastfood + замок качества до нагрева

        public static void OnServerTickPrefix(BlockEntityContainer __instance, out float __state)
        {
            __state = Traverse.Create(__instance).Field("cookTime").GetValue<float>();

            // Фиксируем качество запечатанных банок ДО нагрева (тело тика с HeatCansWhileCooking выполнится уже после этого префикса). Делается один раз на банку

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;

            InventoryBase inv = __instance.Inventory;
            if (inv == null) return;

            foreach (int i in CanSlots)
            {
                if (i >= inv.Count) break;
                ItemStack st = inv[i]?.Itemstack;
                if (st == null || st.Attributes.HasAttribute("xskillsQualityLock")) continue;

                float q = ReadQuality(st);
                if (q >= 0f) st.Attributes.SetFloat("xskillsQualityLock", q);
            }
        }

        public static void OnServerTickPostfix(BlockEntityContainer __instance, float __state)
        {
            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;

            Traverse cookTimeField = Traverse.Create(__instance).Field("cookTime");
            float newCookTime = cookTimeField.GetValue<float>();

            // базовый тик добавил dt только если идёт стерилизация; готово - не трогаем
            if (newCookTime <= __state || newCookTime >= 90f) return;

            float factor = GetFastFoodFactor(__instance);
            if (factor <= 0f) return;

            float bonus = (newCookTime - __state) * factor;
            cookTimeField.SetValue(newCookTime + bonus);
        }

        private static float GetFastFoodFactor(BlockEntityContainer be)
        {
            Cooking cooking = GetCooking();
            if (cooking == null) return 0f;

            IPlayer cook = ResolveCook(be);
            if (cook?.Entity == null) return 0f;

            PlayerAbility ff = cook.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id]?[cooking.FastFoodId];
            return (ff != null && ff.Tier > 0) ? ff.SkillDependentFValue() : 0f;
        }


        //  Завершение стерилизации

        public class PreserveState
        {
            public bool HadInput;
            public float Quality; // -1 = качества не было
        }

        public static void TryPreserveCanInSlotPrefix(BlockEntityContainer __instance, int slotIdx, out PreserveState __state)
        {
            __state = new PreserveState { Quality = -1f };

            InventoryBase inv = __instance.Inventory;
            ItemStack sealedCan = (inv != null && slotIdx >= 0 && slotIdx < inv.Count) ? inv[slotIdx].Itemstack : null;
            if (sealedCan == null) return;

            __state.HadInput = true;

            // Сначала "замок" (значение бенча до нагрева), иначе текущее значение
            float locked = sealedCan.Attributes.GetFloat("xskillsQualityLock", -1f);
            __state.Quality = (locked >= 0f) ? locked : ReadQuality(sealedCan);
        }

        public static void TryPreserveCanInSlotPostfix(BlockEntityContainer __instance, int slotIdx, bool __result, PreserveState __state)
        {
            if (!__result || __state == null || !__state.HadInput) return;

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server) return;

            InventoryBase inv = __instance.Inventory;
            ItemStack preserved = (inv != null && slotIdx >= 0 && slotIdx < inv.Count) ? inv[slotIdx].Itemstack : null;
            if (preserved == null) return;

            IWorldAccessor world = api.World;
            Cooking cooking = GetCooking();

            IPlayer cook = ResolveCook(__instance);
            PlayerSkill skill = (cook?.Entity != null && cooking != null)
                ? cook.Entity.GetBehavior<PlayerSkillSet>()?[cooking.Id]
                : null;

            // КАЧЕСТВО не меняется после стерилизации (его задаёт только bench)
            RestoreQuality(preserved, __state.Quality);

            if (cooking != null && skill != null)
            {
                //  WELLDONE: продлеваем срок годности (как в XSkills - только freshHours)
                PlayerAbility wellDone = skill[cooking.WellDoneId];
                if (wellDone != null && wellDone.Tier > 0)
                    ApplyWelldoneToTransition(api, preserved, 1.0f + wellDone.SkillDependentFValue());

                //  ОПЫТ за консервацию (урезанный)
                GrantPreserveExp(api, cooking, skill, preserved, world);

                //  EGGTIMER
                TryEggTimerNotify(__instance, cook, cooking, skill);
            }

            preserved.Attributes.RemoveAttribute("xskillsQualityLock");
            inv[slotIdx].MarkDirty();
        }

        // опыт
        private static void GrantPreserveExp(ICoreAPI api, Cooking cooking, PlayerSkill skill, ItemStack preservedCan, IWorldAccessor world)
        {
            ItemStack[] contents = cooking.ContentStacks(preservedCan, world);
            if (contents == null || contents.Length == 0) return;

            float diversity = cooking.IngredientDiversity(preservedCan, contents, world, out int ingredientCount);
            if (ingredientCount <= 0) return;

            float servings = (float)preservedCan.Attributes.GetDecimal("quantityServings", 1.0);
            if (servings <= 0f) servings = 1f;

            float expBase = 0.0004f;
            if (cooking.Config is CookingSkillConfig csc) expBase = csc.expBase;

            float exp = PreserveExpMultiplier * expBase;
            if (ingredientCount == 1)
            {
                float satiety = contents[0]?.Collectible?.NutritionProps?.Satiety ?? 0f;
                exp *= satiety * servings;
            }
            else
            {
                exp *= 225f * ingredientCount * diversity * servings;
            }

            if (exp > 0f)
            {
                skill.AddExperience(exp);
                Log(api, "exp +" + exp.ToString("0.00") + " (ингр=" + ingredientCount + " порц=" + servings.ToString("0.0") + ")");
            }
        }

        // качество
        private static float ReadQuality(ItemStack can)
        {
            if (can == null) return -1f;
            float q = can.Attributes.GetFloat("quality", -1f);
            if (q >= 0f) return q;

            ITreeAttribute contents = can.Attributes.GetTreeAttribute("contents");
            if (contents != null)
            {
                foreach (var kvp in contents)
                {
                    if (kvp.Value is ItemstackAttribute attr && attr.value != null)
                    {
                        float cq = attr.value.Attributes.GetFloat("quality", -1f);
                        if (cq >= 0f) return cq;
                    }
                }
            }
            return -1f;
        }

        private static void RestoreQuality(ItemStack can, float quality)
        {
            if (can == null || quality < 0f) return;
            can.Attributes.SetFloat("quality", quality);

            ITreeAttribute contents = can.Attributes.GetTreeAttribute("contents");
            if (contents != null)
            {
                foreach (var kvp in contents)
                {
                    if (kvp.Value is ItemstackAttribute attr && attr.value != null)
                        attr.value.Attributes.SetFloat("quality", quality);
                }
            }
        }

        // welldone: только freshHours, как в Cooking.ApplyAbilities
        private static void ApplyWelldoneToTransition(ICoreAPI api, ItemStack can, float multiplier)
        {
            if (multiplier <= 1.0f) return;
            ITreeAttribute trans = can.Attributes.GetTreeAttribute("transitionstate");
            if (trans == null) return;

            FloatArrayAttribute freshHours = trans["freshHours"] as FloatArrayAttribute;
            if (freshHours != null && freshHours.value.Length > 0)
            {
                float before = freshHours.value[0];
                freshHours.value[0] *= multiplier;
                Log(api, "welldone x" + multiplier.ToString("0.00") +
                    " freshHours " + before.ToString("0.0") + " -> " + freshHours.value[0].ToString("0.0"));
            }
        }

        // eggtimer
        private static void TryEggTimerNotify(BlockEntityContainer be, IPlayer player, Cooking cooking, PlayerSkill skill)
        {
            if (player?.Entity == null) return;

            PlayerAbility eggTimer = skill[cooking.EggTimerId];
            if (eggTimer == null || eggTimer.Tier <= 0) return;

            ICoreAPI api = be.Api;
            IWorldAccessor world = api.World;

            double now = world.Calendar.TotalHours;
            double lastMsg = player.Entity.Attributes.GetDouble("xskillsCookingMsg");
            if (now <= lastMsg + 0.333) return;

            player.Entity.Attributes.SetDouble("xskillsCookingMsg", now);
            world.PlaySoundFor(new AssetLocation("sounds/tutorialstepsuccess.ogg"), player);

            BlockPos pos = be.Pos;
            Block block = pos != null ? world.BlockAccessor.GetBlock(pos) : null;
            string blockName = block?.GetPlacedBlockName(world, pos) ?? be.Block?.GetPlacedBlockName(world, pos) ?? "";
            string location = pos != null ? " (" + pos.X + ", " + pos.Y + ", " + pos.Z + ")" : "";
            string msg = Lang.Get("xskills:cooking-finished", blockName + location);
            (player as IServerPlayer)?.SendMessage(0, msg, EnumChatType.Notification);
        }
    }
}