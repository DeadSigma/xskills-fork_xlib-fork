using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using XLib.XLeveling;

namespace XSkills
{
    public class Fishing : XSkill
    {
        public int StrongLineId { get; private set; }
        public int FishFilleterId { get; private set; }
        public int GoodBaitId { get; private set; }
        public int AutoBaiterId { get; private set; }
        public int BaitMasterId { get; private set; }
        public int DoubleHookId { get; private set; }
        public int MagneticHookId { get; private set; }

        public Fishing(ICoreAPI api) : base("fishing", "xskills:skill-fishing", "xskills:group-collecting")
        {
            XLeveling.Instance(api)?.RegisterSkill(this);

            this.ExperienceEquation = QuadraticEquation;
            this.ExpBase = 200;
            this.ExpMult = 100.0f;
            this.ExpEquationValue = 8.0f;

            // --- ПРОФЕССИЯ: Рыбак ---
            // 5 - уровень навыка для открытия
            // 1 - максимальный тир
            // 40 - бонусный опыт в процентах
            SpecialisationID = this.AddAbility(new Ability(
                "fisher",
                "xskills:ability-fisher",
                "xskills:abilitydesc-fisher",
                5, 1, new int[] { 40 }));

            // Крепкая леска
            // 2 - минимальный уровень навыка для открытия
            // 3 - максимальный тир прокачки
            // 15, 30, 45 - шанс сохранения прочности в %
            StrongLineId = this.AddAbility(new Ability(
                "strongline",
                "xskills:ability-strongline",
                "xskills:abilitydesc-strongline",
                2, 3, new int[] { 15, 30, 45 }));


            // Опытный разделщик
            // 2 - минимальный уровень навыка для открытия
            // 3 - максимальный тир прокачки
            // 10, 20, 30 - шанс получить дополнительное филе в %
            // 3, 4, 5 - максимальное количество филе
            FishFilleterId = this.AddAbility(new Ability(
                 "fishfilleter",
                 "xskills:ability-fishfilleter",
                 "xskills:abilitydesc-fishfilleter",
                 3, 3, new int[] { 10, 3, 20, 4, 30, 5 }));

            // Хорошая наживка: ускорение поклевки
            // 5 - уровень
            // 3 - тира
            // 10, 20, 30 - проценты ускорения
            GoodBaitId = this.AddAbility(new Ability(
                "goodbait",
                "xskills:ability-goodbait",
                "xskills:abilitydesc-goodbait",
                5, 3, new int[] { 10, 20, 30 }));

            // Ловкие руки: автоматическое насаживание наживки при забросе
            // 4 уровень, 1 тир. Без процентов, она либо есть, либо её нет (значение 1)
            AutoBaiterId = this.AddAbility(new Ability(
                "autobaiter",
                "xskills:ability-autobaiter",
                "xskills:abilitydesc-autobaiter",
                4, 1, new int[] { 1 }));

            // Мастер наживки: шанс не потратить червяка при улове
            // 2 - минимальный уровень, 3 - максимальный тир
            // 10, 20, 30 - шанс в процентах
            BaitMasterId = this.AddAbility(new Ability(
                "baitmaster",
                "xskills:ability-baitmaster",
                "xskills:abilitydesc-baitmaster",
                2, 3, new int[] { 10, 20, 30 }));

            // Двойной крючок: шанс выловить вторую случайную рыбу
            // 7 - мин уровень, 3 - макс тир
            // 10, 20, 30 - шанс срабатывания
            DoubleHookId = this.AddAbility(new Ability(
                "doublehook",
                "xskills:ability-doublehook",
                "xskills:abilitydesc-doublehook",
                7, 3, new int[] { 10, 20, 30 }));

            // Инициализация конфига рыбалки (со списком сокровищ)
            this.Config = new FishingSkillConfig();
            // Магнитный крючок: шанс выловить предмет вместо рыбы
            // 1 - уровень
            // 3 - тира
            // 5, 10, 15 - шанс срабатывания в процентах
            MagneticHookId = this.AddAbility(new Ability(
                "magnetichook",
                "xskills:ability-magnetichook",
                "xskills:abilitydesc-magnetichook",
                7, 3, new int[] { 5, 10, 15 }));

        }
    }

    // --- ПАТЧ 1: Выдача опыта за рыбу ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.EntityBobber), "TryCatchFish")]
    public class BobberCatchPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.EntityBobber __instance, out bool __state)
        {
            __state = false;
            if (__instance == null) return;

            if (__instance.caughtFish != null && __instance.caughtFish.Alive)
            {
                __state = true;
                return;
            }

            FieldInfo stateField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "bobberState");
            if (stateField != null)
            {
                string state = stateField.GetValue(__instance)?.ToString();
                if (state == "NoEntityFishCatch" || state == "JunkCatch")
                {
                    __state = true;
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Vintagestory.GameContent.EntityBobber __instance, EntityAgent entityCatcher, bool __state)
        {
            if (!__state || !(entityCatcher is EntityPlayer entityPlayer)) return;

            IPlayer byPlayer = entityPlayer.Player;
            if (byPlayer == null) return;

            Fishing fishing = XLeveling.Instance(byPlayer.Entity.Api)?.GetSkill("fishing") as Fishing;
            if (fishing == null) return;

            PlayerSkill playerSkill = byPlayer.Entity.GetBehavior<PlayerSkillSet>()?[fishing.Id];
            if (playerSkill == null) return;

            // Выдаем 15 опыта за рыбу
            playerSkill.AddExperience(15.0f);
        }
    }

    // --- ПАТЧ 2: Крепкая леска (защита от поломки) ---
    [HarmonyPatch(typeof(Vintagestory.API.Common.CollectibleObject), "DamageItem")]
    public class FishingPoleDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Vintagestory.API.Common.CollectibleObject __instance, IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, int amount)
        {
            if (!(__instance is Vintagestory.GameContent.ItemFishingPole)) return true;
            if (world.Side != EnumAppSide.Server) return true;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return true;

            Fishing fishing = XLeveling.Instance(world.Api)?.GetSkill("fishing") as Fishing;
            if (fishing == null) return true;

            PlayerSkill playerSkill = byEntity.GetBehavior<PlayerSkillSet>()?[fishing.Id];
            if (playerSkill == null) return true;

            PlayerAbility ability = playerSkill[fishing.StrongLineId];
            if (ability != null && ability.Tier > 0)
            {
                float saveChance = ability.Value(0) / 100f;

                if (world.Rand.NextDouble() < saveChance)
                {
                    return false; // Отменяем урон
                }
            }

            return true;
        }
    }

    // --- ПАТЧ 3: Принудительная поломка удочки при замахе ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.ItemFishingPole), "OnHeldInteractStart")]
    public class FishingPoleThrowPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.ItemFishingPole __instance, ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity?.World == null || byEntity.World.Side != EnumAppSide.Server) return;

            bool isFishing = slot?.Itemstack?.Attributes.GetBool("fishing", false) ?? false;

            // Если это новый заброс (рыбалка еще не начата) — ломаем удочку на 1 ед.
            if (slot?.Itemstack != null && !isFishing)
            {
                __instance.DamageItem(byEntity.World, byEntity, slot, 1);
            }
        }
    }
    // --- ПАТЧ 4: Опытный раздельщик (Сбалансированный рандомный бонус до +5 филе) ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.CollectibleBehaviorGroundStoredProcessable), "OnContainedInteractStop")]
    public class FishFilleterPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.CollectibleBehaviorGroundStoredProcessable __instance, float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, out ItemStack __state)
        {
            __state = null;

            if (secondsUsed <= __instance.ProcessTime - 0.05f) return;
            if (be?.Api?.World?.Side != EnumAppSide.Server) return;
            if (slot?.Itemstack?.Collectible?.Code?.Path?.Contains("fishraw") != true) return;

            Fishing fishing = XLeveling.Instance(be.Api)?.GetSkill("fishing") as Fishing;
            if (fishing == null) return;

            PlayerSkill playerSkill = byPlayer?.Entity?.GetBehavior<PlayerSkillSet>()?[fishing.Id];
            if (playerSkill == null) return;

            // Начисляем опыт (базовый)
            int totalFishCount = slot.Itemstack.StackSize;
            playerSkill.AddExperience(1.5f * totalFishCount);

            PlayerAbility ability = playerSkill[fishing.FishFilleterId];
            if (ability != null && ability.Tier > 0)
            {
                float bonusChance = ability.Value(0) / 100f;
                int extraMeatCount = 0;

                // Берет минимум между лимитом из перка (3, 4 или 5) и текущим уровнем рыбака.
                int maxBonusPerFish = Math.Max(1, Math.Min(ability.Value(1), playerSkill.Level));

                if (__instance.ProcessedStacks != null && __instance.ProcessedStacks.Length > 0)
                {
                    var dropTemplate = __instance.ProcessedStacks[0];
                    if (dropTemplate != null)
                    {
                        for (int i = 0; i < totalFishCount; i++)
                        {
                            if (be.Api.World.Rand.NextDouble() < bonusChance)
                            {
                                // Рандом от 1 до текущего максимума (включительно)
                                // Метод Next(min, max) включает min, но ИСКЛЮЧАЕТ max, поэтому пишем +1
                                int randomBonus = be.Api.World.Rand.Next(1, maxBonusPerFish + 1);

                                extraMeatCount += randomBonus;

                                // Доп. опыт за мастерство
                                playerSkill.AddExperience(0.5f);
                            }
                        }

                        if (extraMeatCount > 0)
                        {
                            var resolved = dropTemplate.ResolvedItemstack;
                            if (resolved != null)
                            {
                                __state = resolved.Clone();
                                __state.StackSize = extraMeatCount;
                                // Оставил лог, чтобы тебе было удобно проверить рандом
                                be.Api.World.Logger.Notification($"[РЫБАЛКА] Мастерство (ур.{playerSkill.Level}): Игрок {byPlayer.PlayerName} получил +{extraMeatCount} бонусного филе.");
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Vintagestory.GameContent.CollectibleBehaviorGroundStoredProcessable __instance, float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, ItemStack __state)
        {
            if (__state != null)
            {
                if (!byPlayer.InventoryManager.TryGiveItemstack(__state))
                {
                    be.Api.World.SpawnItemEntity(__state, be.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }
    }
    // --- ПАТЧ 5: Хорошая наживка ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.EntityBobber), "onServertick")]
    public class GoodBaitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.EntityBobber __instance, ref float dt)
        {
            if (__instance.Api.Side != EnumAppSide.Server || !__instance.Swimming) return;

            // Проверяем состояние: ускоряем только когда рыба еще НЕ клюнула
            var stateField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "bobberState");
            if (stateField == null || (int)stateField.GetValue(__instance) > 1) return;

            IPlayer player = (__instance.FiredBy as EntityPlayer)?.Player;
            if (player == null) return;

            Fishing fishing = XLeveling.Instance(player.Entity.Api)?.GetSkill("fishing") as Fishing;
            var playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[fishing?.Id ?? -1];
            var ability = playerSkill?[fishing?.GoodBaitId ?? -1];

            if (ability != null && ability.Tier > 0)
            {
                float bonusPercent = ability.Value(0);
                float timeMultiplier = 1.0f + (bonusPercent / 100f);

                var accumField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "swimmingAccum");
                if (accumField != null)
                {
                    float currentAccum = (float)accumField.GetValue(__instance);

                    // Добавляем бонусное время к накоплению
                    accumField.SetValue(__instance, currentAccum + (dt * (timeMultiplier - 1.0f)));

                    // КРИТИЧЕСКИ ВАЖНО: Синхронизируем сервер с клиентом
                    __instance.WatchedAttributes.MarkPathDirty("swimmingAccum");
                }
            }
        }
    }
    // --- ПАТЧ 6: Ловкие руки (Авто-наживка - ИСПРАВЛЕНО) ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.ItemFishingPole), "OnHeldInteractStart")]
    public class AutoBaiterPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.ItemFishingPole __instance, ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World.Side != EnumAppSide.Server) return;

            // --- ВОТ НАШЕ СПАСЕНИЕ ---
            // Проверяем, закинута ли удочка прямо сейчас. 
            // Если да, значит этот клик - сматывание лески, и наживку вешать не нужно!
            if (slot.Itemstack?.Attributes?.GetBool("fishing", false) == true) return;
            if (slot.Itemstack?.Attributes?.GetLong("bobberEntityId", 0L) != 0L) return;
            // --------------------------

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            Fishing fishing = XLeveling.Instance(player.Entity.Api)?.GetSkill("fishing") as Fishing;
            if (fishing == null) return;

            PlayerSkill playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[fishing.Id];
            if (playerSkill == null) return;

            PlayerAbility ability = playerSkill[fishing.AutoBaiterId];
            if (ability != null && ability.Tier > 0)
            {
                ItemStack currentBait = slot.Itemstack.Attributes.GetItemstack("fishingBait", null);
                if (currentBait != null) return;

                ItemSlot foundBaitSlot = null;

                foreach (var inventory in player.InventoryManager.Inventories.Values)
                {
                    if (inventory.ClassName != "hotbar" && inventory.ClassName != "backpack") continue;

                    foreach (var invSlot in inventory)
                    {
                        if (invSlot.Empty) continue;

                        JsonObject itemAttributes = invSlot.Itemstack.ItemAttributes;
                        if (itemAttributes != null && itemAttributes.IsTrue("isFishBait"))
                        {
                            foundBaitSlot = invSlot;
                            break;
                        }
                    }
                    if (foundBaitSlot != null) break;
                }

                if (foundBaitSlot != null)
                {
                    ItemStack baitToApply = foundBaitSlot.TakeOut(1);
                    foundBaitSlot.MarkDirty();

                    slot.Itemstack.Attributes.SetItemstack("fishingBait", baitToApply);
                    slot.MarkDirty();

                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/cloth"), player.Entity, player, false, 8f, 1f);
                }
            }
        }
    }
    // --- ПАТЧ 7: Мастер наживки (Шанс сохранить наживку) ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.EntityBobber), "TryCatchFish")]
    public class BaitMasterPatch
    {
        // Вызывается ДО ванильного метода
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.EntityBobber __instance, EntityAgent entityCatcher, out ItemStack __state)
        {
            __state = null; // По умолчанию ничего не сохраняем

            if (__instance.Api.Side != EnumAppSide.Server) return;

            IPlayer player = (entityCatcher as EntityPlayer)?.Player;
            if (player == null) return;

            Fishing fishing = XLeveling.Instance(player.Entity.Api)?.GetSkill("fishing") as Fishing;
            if (fishing == null) return;

            PlayerSkill playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[fishing.Id];
            if (playerSkill == null) return;

            PlayerAbility ability = playerSkill[fishing.BaitMasterId];
            if (ability != null && ability.Tier > 0)
            {
                // Если наживки уже нет, спасать нечего
                if (__instance.BaitStack == null) return;

                float saveChance = ability.Value(0) / 100f;

                // Бросаем кубик на удачу
                if (__instance.Api.World.Rand.NextDouble() < saveChance)
                {
                    // УСПЕХ! Клонируем наживку и прячем её в карман (__state)
                    __state = __instance.BaitStack.Clone();
                }
            }
        }

        // Вызывается ПОСЛЕ ванильного метода (когда игра уже сделала BaitStack = null)
        [HarmonyPostfix]
        public static void Postfix(Vintagestory.GameContent.EntityBobber __instance, ItemStack __state, EntityAgent entityCatcher)
        {
            // Если в кармане (__state) что-то есть, значит перк сработал!
            if (__state != null)
            {
                // Возвращаем наживку в поплавок
                __instance.BaitStack = __state;
                __instance.WatchedAttributes.MarkPathDirty("baitStack");

                // Опционально: можно вывести красивое уведомление игроку
                IPlayer player = (entityCatcher as EntityPlayer)?.Player;
                if (player != null)
                {
                    // player.Entity.World.Logger.Notification("Наживка сохранена благодаря мастерству!");
                }
            }
        }
    }
    // --- ПАТЧ 8: Двойной крючок (Шанс выловить вторую рыбу) ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.EntityBobber), "TryCatchFish")]
    public class DoubleHookPatch
    {
        // В Prefix мы выясняем, успешная ли это рыбалка (а не мусор)
        [HarmonyPrefix]
        public static void Prefix(Vintagestory.GameContent.EntityBobber __instance, out bool __state)
        {
            __state = false; // По умолчанию считаем, что улова нет

            // 1. Проверяем невидимую рыбу (состояние NoEntityFishCatch)
            var stateField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "bobberState");
            if (stateField != null)
            {
                string stateName = stateField.GetValue(__instance).ToString();
                if (stateName == "NoEntityFishCatch") __state = true;
            }

            // 2. Проверяем физическую рыбу, если она подплыла
            var caughtFishField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "caughtFish");
            if (caughtFishField != null)
            {
                Entity fish = caughtFishField.GetValue(__instance) as Entity;
                if (fish != null && fish.Alive) __state = true;
            }
        }

        // В Postfix мы выдаем награду, если рыбалка была успешной
        [HarmonyPostfix]
        public static void Postfix(Vintagestory.GameContent.EntityBobber __instance, EntityAgent entityCatcher, bool __state)
        {
            if (!__state) return; // Улова не было (или поймали ржавую шестеренку), выходим
            if (__instance.Api.Side != EnumAppSide.Server) return;

            IPlayer player = (entityCatcher as EntityPlayer)?.Player;
            if (player == null) return;

            Fishing fishing = XLeveling.Instance(player.Entity.Api)?.GetSkill("fishing") as Fishing;
            var playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[fishing?.Id ?? -1];
            var ability = playerSkill?[fishing?.DoubleHookId ?? -1];

            if (ability != null && ability.Tier > 0)
            {
                float doubleChance = ability.Value(0) / 100f;

                // Бросаем кубик на вторую рыбу
                if (__instance.Api.World.Rand.NextDouble() < doubleChance)
                {
                    // Магия рефлексии: просим игру сгенерировать нам случайную рыбу для этого озера
                    var method = AccessTools.Method(typeof(Vintagestory.GameContent.EntityBobber), "getRandomFishEntityProperties");
                    if (method != null)
                    {
                        object[] args = new object[] { 0f, false }; // abundanceValue, printDebug
                        EntityProperties etype = method.Invoke(__instance, args) as EntityProperties;

                        if (etype != null && etype.Drops != null && etype.Drops.Length > 0)
                        {
                            CollectibleObject collObj = etype.Drops[0].ResolvedItemstack.Collectible;

                            // Как и в ваниле, рандомно определяем возраст: малек или взрослая
                            string age = (__instance.Api.World.Rand.NextDouble() > 0.5) ? "adult" : "juvenile";
                            CollectibleObject deadFishItem = __instance.Api.World.GetItem(collObj.CodeWithVariant("age", age)) ?? collObj;

                            // Создаем стак с рыбкой
                            ItemStack bonusFish = new ItemStack(deadFishItem, 1);

                            // Пытаемся дать в руки/инвентарь, иначе кидаем на землю
                            if (!entityCatcher.TryGiveItemStack(bonusFish))
                            {
                                __instance.Api.World.SpawnItemEntity(bonusFish, entityCatcher.Pos.XYZ);
                            }

                            // Добавляем эпичности: звук дополнительного всплеска
                            player.Entity.World.PlaySoundAt(new AssetLocation("sounds/environment/mediumsplash"), player.Entity, player, false, 16f, 1f);

                            // Сообщение в лог сервера для контроля
                            player.Entity.World.Logger.Notification($"[РЫБАЛКА] Двойной крючок: Игрок {player.PlayerName} вытащил дополнительную рыбу!");
                        }
                    }
                }
            }
        }
    }
    // --- КЛАСС 9: Магнитный крючок (Класс Конфигурации) ---
    [ProtoContract]
    public class FishingSkillConfig : CustomSkillConfig
    {
        [ProtoMember(1)]
        public Dictionary<string, int> TreasureDrops;

        public FishingSkillConfig()
        {
            TreasureDrops = new Dictionary<string, int>()
            {
                { "game:gear-temporal", 1 },  // Темпоральная шестеренка (Супер редко)
                { "game:gear-rusty", 15 },    // Ржавая шестеренка (Редко)
                { "game:bone", 30 },          // Кость (Средне)
                { "game:cattailroot", 40 },   // Корень рогоза (Часто)
                { "game:cattailtops", 40 },   // Верхушка рогоза (Часто)
                { "game:stick", 50 },         // Палка (Очень часто)
                { "game:flint", 50 }          // Кремень (Очень часто)
            };
        }

        // Этот метод говорит XSkills, что именно нужно сохранить в fishing.json
        public override Dictionary<string, string> Attributes
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string, string>();

                // Склеиваем наш словарь в строку вида "game:bone:30, game:stick:50"
                List<string> entries = new List<string>();
                foreach (var kvp in TreasureDrops)
                {
                    entries.Add(kvp.Key + ":" + kvp.Value);
                }

                result.Add("treasureDrops", string.Join(",", entries));
                return result;
            }
            set
            {
                // Этот код читает данные из fishing.json при запуске игры
                if (value.TryGetValue("treasureDrops", out string str) && !string.IsNullOrEmpty(str))
                {
                    TreasureDrops.Clear(); // Очищаем дефолтный список
                    string[] entries = str.Split(','); // Разбиваем по запятым

                    foreach (string entry in entries)
                    {
                        string[] parts = entry.Split(':'); // Разбиваем на ИМЯ и ШАНС
                        if (parts.Length == 2 && int.TryParse(parts[1], out int weight))
                        {
                            TreasureDrops[parts[0].Trim()] = weight;
                        }
                    }
                }
            }
        }
    }
    // --- ПАТЧ 9: Магнитный крючок (Шанс выловить предмет из воды) ---
    [HarmonyPatch(typeof(Vintagestory.GameContent.EntityBobber), "TryCatchFish")]
    public class MagneticHookPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Vintagestory.GameContent.EntityBobber __instance, EntityAgent entityCatcher)
        {
            if (__instance.Api.Side != EnumAppSide.Server) return true;

            IPlayer player = (entityCatcher as EntityPlayer)?.Player;
            if (player == null) return true;

            Fishing fishing = XLeveling.Instance(player.Entity.Api)?.GetSkill("fishing") as Fishing;
            PlayerSkill playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[fishing?.Id ?? -1];
            PlayerAbility ability = playerSkill?[fishing?.MagneticHookId ?? -1];

            if (ability == null || ability.Tier <= 0) return true;

            // 1. Проверяем, есть ли на крючке вообще улов (рыба или ванильный мусор)
            bool hasCatch = false;
            var stateField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "bobberState");
            if (stateField != null && stateField.GetValue(__instance).ToString() == "NoEntityFishCatch") hasCatch = true;

            Entity fish = null;
            var caughtFishField = AccessTools.Field(typeof(Vintagestory.GameContent.EntityBobber), "caughtFish");
            if (caughtFishField != null)
            {
                fish = caughtFishField.GetValue(__instance) as Entity;
                if (fish != null && fish.Alive) hasCatch = true;
            }

            if (!hasCatch) return true;

            // 2. Бросаем кубик на сокровище
            float chance = ability.Value(0) / 100f;
            if (__instance.Api.World.Rand.NextDouble() < chance)
            {
                FishingSkillConfig config = fishing.Config as FishingSkillConfig;
                if (config != null && config.TreasureDrops != null && config.TreasureDrops.Count > 0)
                {
                    // Высчитываем общий вес предметов из конфига
                    int totalWeight = 0;
                    foreach (var kvp in config.TreasureDrops) totalWeight += kvp.Value;

                    // Крутим рулетку
                    int rand = __instance.Api.World.Rand.Next(0, totalWeight);
                    string selectedItemCode = null;

                    foreach (var kvp in config.TreasureDrops)
                    {
                        rand -= kvp.Value;
                        if (rand < 0)
                        {
                            selectedItemCode = kvp.Key;
                            break;
                        }
                    }

                    if (selectedItemCode != null)
                    {
                        // Пытаемся найти предмет или блок по коду
                        AssetLocation loc = new AssetLocation(selectedItemCode);
                        Item item = __instance.Api.World.GetItem(loc);
                        Block block = item == null ? __instance.Api.World.GetBlock(loc) : null;

                        ItemStack drop = null;
                        if (item != null) drop = new ItemStack(item);
                        else if (block != null) drop = new ItemStack(block);

                        if (drop != null)
                        {
                            // Выдаем предмет
                            if (!entityCatcher.TryGiveItemStack(drop))
                            {
                                __instance.Api.World.SpawnItemEntity(drop, entityCatcher.Pos.XYZ);
                            }

                            // 3. Ручная проверка "Мастера наживки", так как мы отменяем ванильный метод
                            if (__instance.BaitStack != null)
                            {
                                bool saveBait = false;
                                PlayerAbility baitMaster = playerSkill[fishing.BaitMasterId];
                                if (baitMaster != null && baitMaster.Tier > 0)
                                {
                                    if (__instance.Api.World.Rand.NextDouble() < (baitMaster.Value(0) / 100f)) saveBait = true;
                                }

                                if (!saveBait) // Если не повезло, удаляем червяка
                                {
                                    __instance.BaitStack = null;
                                    __instance.WatchedAttributes.MarkPathDirty("baitStack");
                                }
                            }

                            // 4. Убиваем рыбу, чтобы она не осталась плавать в воде
                            if (fish != null) fish.Die(EnumDespawnReason.Death, null);

                            // Звук вытаскивания
                            player.Entity.World.PlaySoundAt(new AssetLocation("sounds/environment/splash"), player.Entity, player, false, 12f, 1f);
                            player.Entity.World.Logger.Notification($"[РЫБАЛКА] Искатель сокровищ: Игрок {player.PlayerName} выловил {selectedItemCode}!");

                            // ОТМЕНЯЕМ ВАНИЛЬНУЮ РЫБАЛКУ!
                            return false;
                        }
                    }
                }
            }

            // Если шанс не прокнул, продолжаем ловить рыбу как обычно
            return true;
        }
    }





}