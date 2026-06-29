using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace XSkills
{
    public class SimplePotteryWheelIntegration
    {
        [ThreadStatic] private static int _availableVoxelsBefore;

        public static void ApplyPatches(Harmony harmony, ICoreAPI api)
        {
            Type clayWheelType = api.ClassRegistry.GetBlockEntity("ClayWheelEntity");

            if (clayWheelType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("SimplePotteryWheel.ClayWheelEntity", false);
                    if (t != null) { clayWheelType = t; break; }
                }
            }

            if (clayWheelType != null)
            {
                var putClayMethod = AccessTools.Method(clayWheelType, "PutClay", new Type[] { typeof(ItemSlot), typeof(IPlayer) });
                if (putClayMethod != null)
                {
                    harmony.Patch(putClayMethod,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SimplePotteryWheelIntegration), nameof(PutClay_Prefix))),
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(SimplePotteryWheelIntegration), nameof(PutClay_Postfix)))
                    );
                }

                // FastPotter - один бросок на сессию. CreateInitialWorkItem вызывается ровно 1 раз при выборе рецепта.
                var createWorkItemMethod = AccessTools.Method(clayWheelType, "CreateInitialWorkItem", Type.EmptyTypes);
                if (createWorkItemMethod != null)
                {
                    harmony.Patch(createWorkItemMethod,
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(SimplePotteryWheelIntegration), nameof(CreateInitialWorkItem_Postfix)))
                    );
                }

                var checkFinishedMethod = AccessTools.Method(clayWheelType, "CheckIfFinished", new Type[] { typeof(IPlayer) });
                if (checkFinishedMethod != null)
                {
                    harmony.Patch(checkFinishedMethod,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SimplePotteryWheelIntegration), nameof(CheckIfFinished_Prefix)))
                    );
                }

                var clayAddedGetter = AccessTools.PropertyGetter(clayWheelType, "ClayAddedPerUse");
                if (clayAddedGetter != null)
                {
                    harmony.Patch(clayAddedGetter,
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(SimplePotteryWheelIntegration), nameof(ClayAddedPerUse_Postfix)))
                    );
                }

                api.Logger.Notification("[XSkills] Применены патчи (XP, Thrift, FastPotter, LayerLayer, JackPot) для Simple Pottery Wheel!");
            }
        }

        public static void PutClay_Prefix(BlockEntity __instance)
        {
            _availableVoxelsBefore = Traverse.Create(__instance).Field("AvailableVoxels").GetValue<int>();
        }

        // THRIFT +X вокселей бюджета за каждый расходуемый кусок глины
        public static void PutClay_Postfix(BlockEntity __instance, IPlayer byPlayer)
        {
            if (byPlayer == null || __instance.Api.Side == EnumAppSide.Client) return;
            if (byPlayer is not IServerPlayer) return;

            var traverse = Traverse.Create(__instance);
            int currentVoxels = traverse.Field("AvailableVoxels").GetValue<int>();
            int addedVoxels = currentVoxels - _availableVoxelsBefore;

            // Бюджет растёт только в момент реального расхода глины
            if (addedVoxels <= 0) return;

            Pottery pottery = XLeveling.Instance(byPlayer.Entity.Api)?.GetSkill("pottery") as Pottery;
            PlayerSkill playerSkill = pottery != null ? byPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[pottery.Id] : null;
            if (playerSkill == null) return;

            PlayerAbility thrift = playerSkill.PlayerAbilities[pottery.ThriftId];
            if (thrift != null && thrift.Tier > 0)
            {
                traverse.Field("AvailableVoxels").SetValue(currentVoxels + thrift.Value(0));
                __instance.MarkDirty(true);
            }
        }

        // FASTPOTTER шанс мгновенно завершить лепку. Катится 1 раз на каждый клик добавления глины (per-action), а не per-tick.
        // FASTPOTTER один шанс мгновенно завершить лепку, брошенный в момент старта.
        public static void CreateInitialWorkItem_Postfix(BlockEntity __instance)
        {
            if (__instance?.Api == null || __instance.Api.Side == EnumAppSide.Client) return;

            IPlayer player = __instance.Api.World.NearestPlayer(__instance.Pos.X + 0.5, __instance.Pos.Y + 0.5, __instance.Pos.Z + 0.5);
            if (player == null || player.Entity.Pos.DistanceTo(__instance.Pos.ToVec3d()) > 5.0) return;
            if (player is not IServerPlayer serverPlayer) return;

            Pottery pottery = XLeveling.Instance(__instance.Api)?.GetSkill("pottery") as Pottery;
            PlayerSkill playerSkill = pottery != null ? player.Entity?.GetBehavior<PlayerSkillSet>()?[pottery.Id] : null;
            if (playerSkill == null) return;

            PlayerAbility fastPotter = playerSkill.PlayerAbilities[pottery.FastPotterId];
            if (fastPotter == null || fastPotter.Tier <= 0) return;

            float chance = fastPotter.Value(0) / 100f;
            if (serverPlayer.Entity.World.Rand.NextDouble() >= chance) return;

            var traverse = Traverse.Create(__instance);
            var recipe = traverse.Property("SelectedRecipe").GetValue<ClayFormingRecipe>();
            if (recipe == null) return;

            traverse.Field("Voxels").SetValue(recipe.Voxels.Clone() as bool[,,]);
            traverse.Method("CheckIfFinished", player).GetValue();
            __instance.MarkDirty(true);
        }

        // LAYERLAYER: +X вокселей за клик
        public static void ClayAddedPerUse_Postfix(BlockEntity __instance, ref int __result)
        {
            if (__instance?.Api == null) return;

            IPlayer player = null;
            if (__instance.Api.Side == EnumAppSide.Client)
            {
                player = (__instance.Api as ICoreClientAPI)?.World.Player;
            }
            else
            {
                player = __instance.Api.World.NearestPlayer(__instance.Pos.X + 0.5, __instance.Pos.Y + 0.5, __instance.Pos.Z + 0.5);
                if (player != null && player.Entity.Pos.DistanceTo(__instance.Pos.ToVec3d()) > 5.0) player = null;
            }

            if (player == null) return;

            Pottery pottery = XLeveling.Instance(__instance.Api)?.GetSkill("pottery") as Pottery;
            PlayerSkill playerSkill = pottery != null ? player.Entity?.GetBehavior<PlayerSkillSet>()?[pottery.Id] : null;

            if (playerSkill != null)
            {
                PlayerAbility layerLayer = playerSkill.PlayerAbilities[pottery.LayerLayerId];
                if (layerLayer != null && layerLayer.Tier > 0)
                {
                    __result += layerLayer.Value(0);
                }
            }
        }

        // XP + JACKPOT точный паритет со штатным BlockEntityClayForm
        public static void CheckIfFinished_Prefix(BlockEntity __instance, IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            var traverse = Traverse.Create(__instance);
            var selectedRecipe = traverse.Property("SelectedRecipe").GetValue<ClayFormingRecipe>();
            var voxels = traverse.Field("Voxels").GetValue<bool[,,]>();

            if (selectedRecipe == null || voxels == null) return;

            bool done = true;
            for (int y = 0; y < 16 && done; y++)
                for (int x = 0; x < 16 && done; x++)
                    for (int z = 0; z < 16; z++)
                        if (!voxels[x, y, z] && selectedRecipe.Voxels[x, y, z]) { done = false; break; }

            if (!done || byPlayer is not IServerPlayer serverPlayer) return;

            Pottery pottery = XLeveling.Instance(serverPlayer.Entity.Api)?.GetSkill("pottery") as Pottery;
            PlayerSkill playerSkill = pottery != null ? byPlayer.Entity?.GetBehavior<PlayerSkillSet>()?[pottery.Id] : null;
            if (playerSkill == null) return;

            // Опыт: база 1.0 + 0.002 за каждый воксель рецепта (как на верстаке).
            int voxelCount = PotteryUtil.CountVoxels(selectedRecipe);
            playerSkill.AddExperience(1.0f + voxelCount * 0.002f);

            // JackPot: шанс по SkillDependentValue() (растёт с уровнем), а не плоский Value(0).
            PlayerAbility jackpot = playerSkill.PlayerAbilities[pottery.JackPotId];
            if (jackpot != null && jackpot.SkillDependentValue() * 0.01f >= serverPlayer.Entity.World.Rand.NextDouble())
            {
                ItemStack extraLoot = selectedRecipe.Output.ResolvedItemstack.Clone();
                if (!serverPlayer.InventoryManager.TryGiveItemstack(extraLoot))
                {
                    serverPlayer.Entity.World.SpawnItemEntity(extraLoot, __instance.Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                }
            }
        }
    }
}