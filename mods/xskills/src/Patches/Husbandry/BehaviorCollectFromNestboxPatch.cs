using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    [HarmonyPatch(typeof(BlockEntityHenBox), nameof(BlockEntityHenBox.OnInteract))]
    public class BlockEntityNestBoxPatch
    {
        /// <summary>Отслеживаемый атрибут игрока (watched attribute), хранящий дробный прогресс бонусных яиц.</summary>
        public const string BonusProgressKey = "xskillsEggBonusProgress";

        /// <summary>
        /// Малая эпсилон-величина, чтобы чистые дроби вроде 1/3 накапливались до целого яйца, 
        /// несмотря на округление float (иначе 3 * 0.3333f = 0.9999f округлилось бы до 0).
        /// </summary>
        private const float FloorEpsilon = 0.0001f;

        /// <summary>
        /// Устанавливается в true первым postfix'ом взаимодействия и проверяется любым 
        /// последующим postfix'ом того же вызова, поэтому дважды примененный патч выдает награду 
        /// только один раз. Сбрасывается в начале каждого взаимодействия с помощью prefix. 
        /// Является локальным для потока (Thread-local), так как все пары одного вызова 
        /// выполняются синхронно в одном и том же потоке сервера.
        /// </summary>
        [ThreadStatic] private static bool grantedThisInteraction;

        private static readonly bool DebugLogging = false;

        /// <summary>
        /// Устаревшая пустышка (no-op), сохранена только для того, чтобы старое место ручного 
        /// вызова по-прежнему компилировалось. Теперь патч применяется через аннотации класса и PatchAll().
        /// </summary>
        /// <param name="harmony">не используется</param>
        /// <param name="nestType">не используется</param>
        /// <param name="xskills">не используется</param>
        public static void Apply(Harmony harmony, Type nestType, XSkills xskills)
        { }

        /// <summary>Переносит состояние инвентаря до сбора из prefix в postfix.</summary>
        public class CollectState
        {
            /// <summary>Общее количество яиц в гнезде до взаимодействия.</summary>
            public int EggsBefore;

            /// <summary>Клон одного из яиц, используемый как шаблон для бонусных яиц.</summary>
            public ItemStack Template;
        }

        /// <summary>Подсчитывает общий размер стаков во всех непустых слотах.</summary>
        /// <param name="inventory">инвентарь гнезда</param>
        /// <returns>количество яиц в инвентаре</returns>
        private static int CountEggs(InventoryBase inventory)
        {
            int count = 0;
            foreach (ItemSlot slot in inventory)
            {
                if (!slot.Empty) count += slot.Itemstack.StackSize;
            }
            return count;
        }

        /// <summary>
        /// Сбрасывает защиту на каждое взаимодействие и делает снимок содержимого гнезда 
        /// до взаимодействия. Только на стороне сервера - клиент выполняет неизмененный ванильный метод.
        /// </summary>
        /// <param name="__instance">блок-сущность (block entity) гнезда</param>
        /// <param name="world">доступ к миру (world accessor)</param>
        /// <param name="byPlayer">взаимодействующий игрок</param>
        /// <param name="__state">состояние, передаваемое в postfix</param>
        [HarmonyPrefix]
        public static void OnInteractPrefix(BlockEntityHenBox __instance, IWorldAccessor world, IPlayer byPlayer, out CollectState __state)
        {
            // запускается перед (единственным) оригиналом, поэтому каждая пара дважды 
            // примененного патча очищает защиту до того, как любой postfix ее прочитает
            grantedThisInteraction = false;
            __state = null;
            if (world.Side != EnumAppSide.Server || byPlayer?.Entity == null) return;

            int count = 0;
            ItemStack template = null;
            foreach (ItemSlot slot in __instance.Inventory)
            {
                if (slot.Empty) continue;
                count += slot.Itemstack.StackSize;
                if (template == null) template = slot.Itemstack;
            }
            if (count == 0) return;

            __state = new CollectState
            {
                EggsBefore = count,
                // клонируем сейчас, оригинальный метод может опустошить слот
                Template = template.Clone()
            };
        }

        /// <summary>
        /// Дает опыт животноводства (husbandry) за фактически собранные яйца и применяет 
        /// накопленный бонус Фермера (Rancher). Защищено для выполнения ровно один раз за взаимодействие.
        /// </summary>
        /// <param name="__instance">блок-сущность (block entity) гнезда</param>
        /// <param name="__result">было ли успешным ванильное взаимодействие</param>
        /// <param name="world">доступ к миру (world accessor)</param>
        /// <param name="byPlayer">взаимодействующий игрок</param>
        /// <param name="__state">состояние, захваченное с помощью prefix</param>
        [HarmonyPostfix]
        public static void OnInteractPostfix(BlockEntityHenBox __instance, bool __result, IWorldAccessor world, IPlayer byPlayer, CollectState __state)
        {
            if (__state == null || !__result) return;
            // второй (и последующие) postfix дважды примененного патча выходит здесь
            if (grantedThisInteraction) return;
            grantedThisInteraction = true;

            int collected = __state.EggsBefore - CountEggs(__instance.Inventory);
            // ничего не было взято (например, вместо этого предмет был помещен в гнездо)
            if (collected <= 0) return;

            Husbandry husbandry = XLeveling.Instance(world.Api)?.GetSkill("husbandry") as Husbandry;
            if (husbandry == null) return;
            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[husbandry.Id];
            if (playerSkill == null) return;

            playerSkill.AddExperience(collected * 0.1f);

            PlayerAbility playerAbility = playerSkill[husbandry.RancherId];
            if (playerAbility == null || playerAbility.Tier <= 0) return;

            // fraction - это бонус за яйцо: 0.33 на 1 уровне, 0.5 на 2 уровне.
            float fraction = playerAbility.FValue(0);
            if (fraction <= 0.0f) return;

            ITreeAttribute attributes = byPlayer.Entity.WatchedAttributes;
            float progress = attributes.GetFloat(BonusProgressKey, 0.0f) + collected * fraction;
            int bonusEggs = (int)(progress + FloorEpsilon);
            float carry = progress - bonusEggs;
            if (carry < 0.0f) carry = 0.0f;
            attributes.SetFloat(BonusProgressKey, carry);

            if (DebugLogging)
            {
                world.Api.Logger.Notification(
                    "[xskills-nest] tier: {0}, fraction: {1}, collected: {2}, bonus: {3}, carry: {4}",
                    playerAbility.Tier, fraction, collected, bonusEggs, carry);
            }

            if (bonusEggs <= 0) return;

            ItemStack bonusStack = __state.Template;
            bonusStack.StackSize = bonusEggs;
            // бонусные яйца никогда не бывают оплодотворенными, иначе перк дублировал бы цыплят
            bonusStack.Attributes.RemoveAttribute("chick");

            byPlayer.InventoryManager.TryGiveItemstack(bonusStack, true);
            if (bonusStack.StackSize > 0)
            {
                world.SpawnItemEntity(bonusStack, __instance.Position);
            }
        }
    }//!class BlockEntityNestBoxPatch
}