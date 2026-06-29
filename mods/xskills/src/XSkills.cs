using CombatOverhaul.Implementations;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.Server;
using XLib.XLeveling;

namespace XSkills
{
    public class XSkills : ModSystem
    {
        private static Harmony harmony;

        /// <summary>
        /// Gets an instance of this class.
        /// This is only used to get an instance for harmony prepare methods.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static XSkills Instance { get; private set; }

        public Dictionary<string, Skill> Skills { get; set; }

        public ICoreAPI Api { get; private set; }
        public XLeveling XLeveling { get; private set; }

        internal static void DoHarmonyPatch(ICoreAPI api)
        {
            if (harmony == null)
            {
                XSkills xskills = api.ModLoader.GetModSystem<XSkills>();

                harmony = new Harmony("XSkillsPatch");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                // Фикс наковальни
                KnapsterIntegration.ApplyPatches(harmony, api);
                // Фикс лепки из глины
                KnapsterClayIntegration.ApplyPatches(harmony, api);

                Type type;

                BlockEntityAnvilPatch.Apply(harmony, api.ClassRegistry.GetBlockEntity("Anvil"));
                BlockEntityOvenPatch.Apply(harmony, api.ClassRegistry.GetBlockEntity("Oven"), xskills);

                //type = api.ClassRegistry.GetBlockEntity("ExpandedOven");
                //if (type != null) BlockEntityOvenPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("OvenBakingTop");
                if (type != null) BlockEntityOvenPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("OvenCookingTop");
                if (type != null) BlockEntityOvenCookingTopPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("MixingBowl");
                if (type != null) BlockEntityMixingBowlPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetItemClass("ExpandedRawFood");
                if (type != null) ItemExpandedRawFoodPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockClass("BlockSaucepan");
                if (type != null) BlockSaucepanPatch.Apply(harmony, type, xskills);

                type = type?.Assembly.GetType("ACulinaryArtillery.InventoryMixingBowl");
                if (type != null) InventoryMixingBowlPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("ButcherTable");
                if (type != null) BlockEntityButcherWorkstationPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("beframerack");
                if (type != null) BEFrameRackPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockClass("hivetop");
                if (type != null) ClayHiveTopPatch.Apply(harmony, type, xskills);

                type = api.ClassRegistry.GetBlockEntity("BlockNestbox");
                if (type != null) BlockEntityNestBoxPatch.Apply(harmony, type, xskills);

                if (api.ModLoader.IsModSystemEnabled("overhaullib"))
                {
                    ApplyCOPatch(xskills);
                }

                if (api.ModLoader.IsModEnabled("electricalprogressiveqol"))
                {
                    BlockEntityEFruitPressPatch.Apply(harmony);
                }

                if (api.ModLoader.IsModEnabled("ithaniacannedgoods"))
                {
                    // Пытаемся получить класс стола из реестра игры
                    Type benchType = api.ClassRegistry.GetBlockEntity("CanningBench");

                    // Если мод зарегистрировал его под другим именем, ищем жестко через рефлексию
                    if (benchType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type t = asm.GetType("IthaniaCannedGoods.BlockEntities.BlockEntityCanningBench", false);
                            if (t != null)
                            {
                                benchType = t;
                                break;
                            }

                        }
                    }

                    if (benchType != null)


                    {
                        BlockEntityCanningBenchPatch.Apply(harmony, benchType);
                    }
                    Type pressType = api.ClassRegistry.GetBlockEntity("CanPress");
                    if (pressType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type t = asm.GetType("IthaniaCannedGoods.BlockEntities.BlockEntityCanPress", false);
                            if (t != null)
                            {
                                pressType = t;
                                break;
                            }
                        }
                    }

                    if (pressType != null)
                    {
                        BlockEntityCanPressPatch.Apply(harmony, pressType);
                    }
                    Type cookerType = api.ClassRegistry.GetBlockEntity("PressureCooker");
                    if (cookerType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type t = asm.GetType("IthaniaCannedGoods.BlockEntities.BlockEntityPressureCooker", false);
                            if (t != null)
                            {
                                cookerType = t;
                                break;
                            }
                        }
                    }

                    if (cookerType != null)
                    {
                        BlockEntityPressureCookerPatch.Apply(harmony, cookerType);
                    }
                }
            }
        }

        /// <summary>
        /// Apply patches for Combat Overhaul.
        /// </summary>
        internal static void ApplyCOPatch(XSkills xskills)
        {
            ItemStackMeleeWeaponStatsPatch.Apply(harmony, typeof(ItemStackMeleeWeaponStats), xskills);
            ItemStackRangedStatsPatch.Apply(harmony, typeof(ItemStackRangedStats), xskills);
        }

        /// <summary>
        /// If you need mods to be executed in a certain order, adjust this methods return value.
        /// The server will call each Mods Start() method the ascending order of each mods execute order value. And thus, as long as every mod registers it's event handlers in the Start() method, all event handlers will be called in the same execution order.
        /// Default execute order of some survival mod parts
        /// Worldgen:
        /// - GenTerra: 0
        /// - RockStrata: 0.1
        /// - Deposits: 0.2
        /// - Caves: 0.3
        /// - Blocklayers: 0.4
        /// Asset Loading
        /// - Json Overrides loader: 0.05
        /// - Load hardcoded mantle block: 0.1
        /// - Block and Item Loader: 0.2
        /// - Recipes (Smithing, Knapping, Clayforming, Grid recipes, Alloys) Loader: 1
        /// </summary>
        /// <returns></returns>
        public override double ExecuteOrder() => 0.25;

        public XSkills() : base()
        {
            if (Instance == null) Instance = this;
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony?.UnpatchAll("XSkillsPatch");
            harmony = null;
            new Harmony("com.xskills.toolsmithpatch").UnpatchAll("com.xskills.toolsmithpatch");
            toolsmithPatched = false;
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            this.Api = api;
            this.XLeveling = XLeveling.Instance(this.Api);
            this.Skills = new Dictionary<string, Skill>();

            //skills
            Survival survival = new Survival(api);
            this.Skills.Add(survival.Name, survival);
            Farming farming = new Farming(api);
            this.Skills.Add(farming.Name, farming);
            Digging digging = new Digging(api);
            this.Skills.Add(digging.Name, digging);
            Forestry forestry = new Forestry(api);
            this.Skills.Add(forestry.Name, forestry);
            Mining mining = new Mining(api);
            this.Skills.Add(mining.Name, mining);
            Husbandry husbandry = new Husbandry(api);
            this.Skills.Add(husbandry.Name, husbandry);
            Combat combat = new Combat(api);
            this.Skills.Add(combat.Name, combat);
            Metalworking metalworking = new Metalworking(api);
            this.Skills.Add(metalworking.Name, metalworking);
            Pottery pottery = new Pottery(api);
            this.Skills.Add(pottery.Name, pottery);
            Cooking cooking = new Cooking(api);
            this.Skills.Add(cooking.Name, cooking);
            Fishing fishing = new Fishing(api);
            this.Skills.Add(fishing.Name, fishing);

            if (api.World.Config.GetBool("temporalStability"))
            {
                TemporalAdaptation adaptation = new TemporalAdaptation(api);
                this.Skills.Add(adaptation.Name, adaptation);
            }

            api.RegisterEntityBehaviorClass("XSkillsPlayer", typeof(XSkillsPlayerBehavior));
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            (this.Skills["metalworking"] as Metalworking).RegisterAnvil();

            //register 'quality' and 'owner' to be ignored
            string[] temp = new string[GlobalConstants.IgnoredStackAttributes.Length + 2];
            int count = 0;
            for (; count < GlobalConstants.IgnoredStackAttributes.Length; ++count)
            {
                temp[count] = GlobalConstants.IgnoredStackAttributes[count];
            }
            temp[count] = "quality";
            count++;
            temp[count] = "owner";
            GlobalConstants.IgnoredStackAttributes = temp;

            api.RegisterBlockEntityBehaviorClass("XskillsOwnable", typeof(BlockEntityBehaviorOwnable));

            ClassRegistry registry = (api as ServerCoreAPI)?.ClassRegistryNative ?? (api as ClientCoreAPI)?.ClassRegistryNative;
            if (registry != null)
            {
                registry.blockEntityClassnameToTypeMapping["Sapling"] = typeof(XSkillsBlockEntitySapling);
                registry.blockEntityTypeToClassnameMapping[typeof(XSkillsBlockEntitySapling)] = "Sapling";

                if (Api.ModLoader.IsModEnabled("primitivesurvival"))
                    HoeUtil.RegisterItemHoePrimitive(registry);
                HoeUtil.RegisterItemHoe(registry);

                registry.ItemClassToTypeMapping["ItemPlantableSeed"] = typeof(XSkillsItemPlantableSeed);

                //registry.entityBehaviorClassNameToTypeMapping["commandable"] = typeof(XSkillsEntityBehaviorCommandable);
                //registry.entityBehaviorTypeToClassNameMapping[typeof(XSkillsEntityBehaviorCommandable)] = "commandable";
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.XLeveling.CreateDescriptionFile();

            string hotkeyDescription = Lang.Get("xskills:hotkey-xskillshotbarswitch");

            api.Input.RegisterHotKey("xskillshotbarswitch", hotkeyDescription, GlKeys.R);
            api.Input.SetHotKeyHandler("xskillshotbarswitch", OnHotbarSwitch);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            DoHarmonyPatch(api);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            PatchEntities();
            TryPatchToolsmith(api);


            Survival survival = (this.Skills["survival"] as Survival);
            LimitationRequirement specialisations = this.XLeveling.Limitations["specialisations"];
            if (specialisations != null && survival != null) specialisations.ModifierAbility = survival[survival.AllRounderId];

            foreach (Skill skill in this.Skills.Values)
            {
                skill.DisplayName = Lang.GetUnformatted(skill.DisplayName);
                skill.Group = Lang.GetUnformatted(skill.Group);
                foreach (Ability ability in skill.Abilities)
                {
                    ability.DisplayName = Lang.GetUnformatted(ability.DisplayName);
                    ability.Description = Lang.GetUnformatted(ability.Description);
                }
            }
        }

        public bool OnHotbarSwitch(KeyCombination keys)
        {
            IPlayer player = (this.Api as ICoreClientAPI)?.World.Player;
            XSkillsPlayerInventory inv = player?.InventoryManager.GetOwnInventory("xskillshotbar") as XSkillsPlayerInventory;
            if (inv == null) return false;
            inv.SwitchInventories();
            return true;
        }

        /// <summary>
        /// Patches entities.
        /// Adds husbandry and combat related behaviors to entities that don't have explicit compatibility.
        /// </summary>
        public void PatchEntities()
        {
            if (Api.Side.IsClient()) return;

            foreach (EntityProperties entity in Api.World.EntityTypes)
            {
                if (entity == null) continue;

                float damage = 0.0f;
                int damageTier = -1;
                float health = 0.0f;
                bool isHostile = false;
                bool isMultipliable = false;
                bool isXskillsEntity = false;
                bool isXskillsAnimal = false;

                isHostile = entity.Server?.SpawnConditions?.Runtime?.Group == "hostile";

                JsonObject[] serverBehaviors = entity.Server?.BehaviorsAsJsonObj;
                JsonObject[] clientBehaviors = entity.Client?.BehaviorsAsJsonObj;

                if (serverBehaviors != null)
                {
                    foreach (JsonObject json in serverBehaviors)
                    {
                        if (json == null) continue;
                        string code = json["code"]?.AsString();

                        if (code == "taskai")
                        {
                            // Безопасное получение массива aitasks
                            JsonObject[] tasks = json["aitasks"]?.AsArray();
                            if (tasks != null)
                            {
                                foreach (JsonObject aitask in tasks)
                                {
                                    if (aitask == null) continue;
                                    string taskCode = aitask["code"]?.AsString();
                                    if (!(taskCode == "meleeattack" || taskCode == "melee")) continue;

                                    damage = Math.Max(aitask["damage"]?.AsFloat() ?? 0f, damage);
                                    damageTier = Math.Max(aitask["damageTier"]?.AsInt() ?? -1, damageTier);
                                }
                            }
                        }
                        else if (code == "health")
                        {
                            health = json["maxhealth"]?.AsFloat() ?? 0f;
                        }
                        else if (code == "multiply")
                        {
                            isMultipliable = true;
                        }
                        else if (code == "XSkillsEntity")
                        {
                            isXskillsEntity = true;
                            break;
                        }
                        else if (code == "XSkillsAnimal")
                        {
                            isXskillsAnimal = true;
                            break;
                        }
                    }
                }

                if (isXskillsEntity || isXskillsAnimal) continue;

                // Безопасная проверка Code и Path
                if (!isMultipliable && entity.Code != null && entity.Code.Path != null && entity.Code.Path.Contains("male"))
                {
                    AssetLocation assetLocation = new AssetLocation(entity.Code.Domain, entity.Code.Path.Replace("male", "female"));
                    EntityProperties female = Api.World.GetEntityType(assetLocation);
                    if (female != null)
                    {
                        JsonObject[] behaviors2 = female.Server?.BehaviorsAsJsonObj ?? female.Client?.BehaviorsAsJsonObj;
                        if (behaviors2 != null)
                        {
                            foreach (JsonObject json in behaviors2)
                            {
                                if (json == null) continue;
                                if (json["code"]?.AsString() == "multiply")
                                {
                                    isMultipliable = true;
                                    break; // Выходим из цикла, если нашли
                                }
                            }
                        }
                    }
                }

                if (health > 0.0f && ((damage > 1.0f && damageTier >= 0) || isMultipliable))
                {
                    string str;
                    List<JsonObject> newBehaviors = new List<JsonObject>();

                    float newXp = health * 0.025f + (damage - 1.0f) * 0.05f + damageTier * 0.25f + (isHostile ? 0.25f : 0.0f);
                    if (isMultipliable && !isXskillsAnimal)
                    {
                        newXp *= 0.5f;
                        str = "{\"code\": \"XSkillsAnimal\", \"xp\": " +
                            newXp.ToString(new CultureInfo("en-US")) +
                            ", \"catchable\": \"false\"}";
                        newBehaviors.Add(JsonObject.FromJson(str));
                    }
                    else
                    {
                        str = "{\"code\": \"XSkillsEntity\", \"xp\": " + newXp.ToString(new CultureInfo("en-US")) + "}";
                        newBehaviors.Add(JsonObject.FromJson(str));
                    }

                    JsonObject[] newBehaviorsArray = newBehaviors.ToArray();
                    JsonObject[] newServerBeh = (serverBehaviors ?? new JsonObject[0]).AddRangeToArray(newBehaviorsArray);
                    JsonObject[] newClientBeh = (clientBehaviors ?? new JsonObject[0]).AddRangeToArray(newBehaviorsArray);

                    if (entity.Server != null) entity.Server.BehaviorsAsJsonObj = newServerBeh;
                    if (entity.Client != null) entity.Client.BehaviorsAsJsonObj = newClientBeh;
                }
            }
        }

        /// <summary>
        /// Adds the tool behavior to all collectibles that are defined as a tool.
        /// </summary>
        //public void AddToolBehaviors()
        //{
        //    foreach(CollectibleObject collectible in Api.World.Collectibles)
        //    {
        //        if (collectible == null) continue;
        //        switch(collectible.Tool)
        //        {
        //            case EnumTool.Pickaxe:
        //            case EnumTool.Axe:
        //            case EnumTool.Shovel:
        //                collectible.HasBehavior(typeof(XSkillsToolBehavior), true);
        //                collectible.CollectibleBehaviors.AddItem(new XSkillsToolBehavior(collectible));
        //                break;
        //            default:
        //                continue;
        //        }
        //    }
        //}

        private static bool toolsmithPatched = false;

        private void TryPatchToolsmith(ICoreAPI api)
        {
            // В одиночной игре AssetsLoaded вызывается и для клиента, и для сервера —
            // защищаемся от повторного патча
            if (toolsmithPatched) return;

            // Ищем тип по имени, НЕ перечисляя все типы каждой сборки.
            // Иначе падаем на сборках с неразрешимыми зависимостями (OpenTK.Graphics / csogg на сервере)
            Type toolsmithNuggetType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType("Toolsmith.ToolTinkering.Items.ItemWorkableNugget", false);
                    if (t != null)
                    {
                        toolsmithNuggetType = t;
                        break;
                    }
                }
                catch
                {
                    // Сборка не смогла резолвить тип — просто пропускаем её
                }
            }

            if (toolsmithNuggetType == null) return;

            Harmony toolsmithHarmony = new Harmony("com.xskills.toolsmithpatch");

            void PatchMethod(string methodName)
            {
                MethodInfo original = toolsmithNuggetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                MethodInfo prefix = typeof(ToolsmithConflictResolver).GetMethod(methodName + "_Prefix", BindingFlags.Public | BindingFlags.Static);
                if (original != null && prefix != null)
                    toolsmithHarmony.Patch(original, prefix: new HarmonyMethod(prefix));
            }

            PatchMethod("TryPlaceOn");
            PatchMethod("GetMatchingRecipes");
            PatchMethod("GetRequiredAnvilTier");
            PatchMethod("CanWork");

            toolsmithPatched = true;
        }
    }//!class XSkills
}//!namespace XSkills