//Код от пользователя Taper4ik
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using XLib.XLeveling;

namespace XSkills
{
    internal static class FarmingBushHelper
    {
        public static PlayerAbility GetAbility(IPlayer player, string abilityName)
        {
            if (player?.Entity == null) return null;
            PlayerSkillSet skillSet = player.Entity.GetBehavior<PlayerSkillSet>();
            PlayerSkill farmingSkill = skillSet?.FindSkill("farming");
            return farmingSkill?.FindAbility(abilityName);
        }
    }

    [HarmonyPatch]
    internal static class BushCuttingCooldownPatch
    {
        public static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Vintagestory.GameContent.BEBehaviorFruitingBush");
            return type == null ? null : AccessTools.Method(type, "SetHarvested");
        }

        public static bool Prepare() => TargetMethod() != null;

        public static void Postfix(object __instance, object[] __args)
        {
            IPlayer player = __args.OfType<IPlayer>().FirstOrDefault();
            if (player?.Entity?.World?.Side != EnumAppSide.Server) return;

            PlayerAbility ability = FarmingBushHelper.GetAbility(player, "cuttingrhythm");
            if (ability == null || ability.Tier <= 0) return;

            int percent = ability.Value(0);
            if (percent <= 0) return;

            double reduction = percent / 100.0;
            BushReflectionUtil.ReduceCuttingCooldown(__instance, player, reduction);
        }
    }

    [HarmonyPatch(typeof(Block), nameof(Block.DoPlaceBlock))]
    internal static class BushNurseryPlacementPatch
    {
        private const int BushNurseryDeferredDelayMs = 50;
        private static readonly HashSet<string> PendingBushNurseryCallbacks = new HashSet<string>();

        public static void Postfix(Block __instance, bool __result, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            if (!__result || world?.Side != EnumAppSide.Server || byPlayer == null || blockSel == null || byItemStack == null) return;
            if (!BushReflectionUtil.IsBushCuttingPlacement(__instance, byItemStack)) return;

            PlayerAbility ability = FarmingBushHelper.GetAbility(byPlayer, "bushnursery");
            if (ability == null || ability.Tier <= 0) return;

            int percent = ability.Value(0);
            if (percent <= 0) return;

            BlockPos pos = blockSel.Position.Copy();
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (BushReflectionUtil.HasBushNurseryMarker(be)) return;

            string key = PendingKey(byPlayer, pos);
            if (!PendingBushNurseryCallbacks.Add(key)) return;

            System.Action<IWorldAccessor, BlockPos, float> action = (worldAccessor, callbackPos, _) =>
            {
                try
                {
                    TryApplyBushNurseryGrowth(byPlayer, worldAccessor, callbackPos.Copy(), percent);
                }
                finally
                {
                    PendingBushNurseryCallbacks.Remove(key);
                }
            };

            world.Api.Event.RegisterCallback(action, pos, BushNurseryDeferredDelayMs);
        }

        private static bool TryApplyBushNurseryGrowth(IPlayer player, IWorldAccessor world, BlockPos blockPos, int percent)
        {
            if (world.Side != EnumAppSide.Server || player == null) return false;

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockPos);
            if (be == null || BushReflectionUtil.HasBushNurseryMarker(be)) return false;

            double normalTimeFactor = Math.Min(Math.Max(percent / 100.0, 0.05), 1.0);
            double reduction = 1.0 - normalTimeFactor;

            return BushReflectionUtil.ReducePlacedCuttingGrowth(be, world, player, reduction, out _, out _, out _);
        }

        private static string PendingKey(IPlayer player, BlockPos pos)
        {
            return player.PlayerUID + ":" + pos.X + ":" + pos.Y + ":" + pos.Z;
        }
    }

    internal static class BushReflectionUtil
    {
        private static readonly string[] BushNurseryGrowthHints = { "grow", "growth", "matur", "stage", "ready", "transition", "fruit" };
        private static readonly Dictionary<string, double> CuttingCooldownReductionMarks = new Dictionary<string, double>();

        private const double CuttingCooldownYears = 1.0;
        private const double CuttingCooldownDuplicateWindowDays = 0.0001;

        public static bool ReduceCuttingCooldown(object fruitingBushBehavior, IPlayer player, double reduction)
        {
            if (reduction <= 0.0 || player.Entity?.World == null) return false;

            object state = GetBState(fruitingBushBehavior);
            if (state == null)
            {
                player.Entity.Api.Logger.Warning("[XSkills] Could not reduce bush cutting cooldown: BState was not found.");
                return false;
            }

            IWorldAccessor world = player.Entity.World;
            double nowDays = world.Calendar.TotalDays;
            double currentLast = GetDoubleField(state, "LastCuttingTakenTotalDays", double.NaN);
            if (double.IsNaN(currentLast))
            {
                player.Entity.Api.Logger.Warning("[XSkills] Could not reduce bush cutting cooldown: LastCuttingTakenTotalDays was not found.");
                return false;
            }

            double clampedReduction = Math.Min(Math.Max(reduction, 0.0), 0.95);
            double cooldownDays = Math.Max(world.Calendar.DaysPerYear * CuttingCooldownYears, 1.0);

            double targetLast = nowDays - cooldownDays * clampedReduction;
            double adjustedLast = Math.Min(currentLast, targetLast);
            if (adjustedLast >= currentLast - 0.0001) return false;

            if (WasCuttingCooldownRecentlyReduced(fruitingBushBehavior, player, nowDays)) return false;

            if (!SetDoubleField(state, "LastCuttingTakenTotalDays", adjustedLast))
            {
                player.Entity.Api.Logger.Warning("[XSkills] Could not reduce bush cutting cooldown: LastCuttingTakenTotalDays could not be written.");
                return false;
            }

            MarkCuttingCooldownReduced(fruitingBushBehavior, player, nowDays);
            MarkBlockEntityDirty(fruitingBushBehavior);
            return true;
        }

        public static bool ReducePlacedCuttingGrowth(BlockEntity be, IWorldAccessor world, IPlayer player, double reduction, out double oldDays, out double newDays, out string fieldPath)
        {
            oldDays = 0.0;
            newDays = 0.0;
            fieldPath = string.Empty;

            if (be == null || world.Side != EnumAppSide.Server || player == null || reduction <= 0.0) return false;

            TreeAttribute tree = new TreeAttribute();
            be.ToTreeAttributes(tree);
            if (tree.GetBool("xskillsrandskills:bushnurseryApplied", false)) return false;

            double clampedReduction = Math.Min(Math.Max(reduction, 0.0), 0.95);

            if (TryReduceGrowthFutureTimeInTree(tree, world, clampedReduction, out oldDays, out newDays, out fieldPath))
            {
                WriteBushNurseryMarker(tree, world, player, fieldPath, clampedReduction);
                be.FromTreeAttributes(tree, world);
            }
            else if (TryReduceGrowthFutureTimeOnBehavior(be, world, clampedReduction, out oldDays, out newDays, out fieldPath))
            {
                TreeAttribute after = new TreeAttribute();
                be.ToTreeAttributes(after);
                WriteBushNurseryMarker(after, world, player, fieldPath, clampedReduction);
                be.FromTreeAttributes(after, world);
            }
            else
            {
                return false;
            }

            be.MarkDirty(true);
            return true;
        }

        public static bool HasBushNurseryMarker(BlockEntity be)
        {
            if (be == null) return false;
            TreeAttribute tree = new TreeAttribute();
            be.ToTreeAttributes(tree);
            return tree.GetBool("xskillsrandskills:bushnurseryApplied", false);
        }

        private static void WriteBushNurseryMarker(TreeAttribute tree, IWorldAccessor world, IPlayer player, string fieldPath, double reduction)
        {
            tree.SetBool("xskillsrandskills:bushnurseryApplied", true);
            tree.SetString("xskillsrandskills:bushnurseryPlayer", player.PlayerUID);
            tree.SetDouble("xskillsrandskills:bushnurseryAppliedAtTotalHours", world.Calendar.TotalHours);
            tree.SetString("xskillsrandskills:bushnurseryField", fieldPath);
            tree.SetDouble("xskillsrandskills:bushnurseryReduction", reduction);
        }

        public static bool IsBushCuttingPlacement(Block block, ItemStack byItemStack)
        {
            string blockCode = block.Code?.ToString().ToLowerInvariant() ?? string.Empty;
            string stackCode = byItemStack?.Collectible?.Code?.ToString().ToLowerInvariant() ?? string.Empty;
            string combined = blockCode + " " + stackCode;

            if (!combined.Contains("bush") && !combined.Contains("berry") && !combined.Contains("fruiting")) return false;
            if (combined.Contains("legacy")) return false;

            string traits = null;
            try { traits = byItemStack?.Attributes?.GetString("traits", null); }
            catch { traits = null; }

            return combined.Contains("cutting")
                || !string.IsNullOrEmpty(traits)
                || blockCode.Contains("planted")
                || blockCode.Contains("young");
        }

        private static bool TryReduceGrowthFutureTimeOnBehavior(BlockEntity be, IWorldAccessor world, double reduction, out double oldDays, out double newDays, out string fieldPath)
        {
            oldDays = 0.0;
            newDays = 0.0;
            fieldPath = string.Empty;

            object behavior = FindFruitingBushBehavior(be);
            if (behavior == null) return false;

            object state = GetBState(behavior);
            if (state == null) return false;

            return TryReduceFutureTimeMember(state, world, reduction, "BState", out oldDays, out newDays, out fieldPath);
        }

        private static bool TryReduceFutureTimeMember(object obj, IWorldAccessor world, double reduction, string pathPrefix, out double oldDays, out double newDays, out string fieldPath)
        {
            oldDays = 0.0;
            newDays = 0.0;
            fieldPath = string.Empty;

            List<(int Priority, MemberInfo Member, string Name, double Value, double Now, GrowthTimeUnit Unit)> candidates = new List<(int Priority, MemberInfo Member, string Name, double Value, double Now, GrowthTimeUnit Unit)>();

            foreach (MemberInfo member in obj.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!LooksLikeBushNurseryGrowthKey(member.Name)) continue;

                bool writable = member is FieldInfo || (member is PropertyInfo prop && prop.CanWrite);
                if (!writable) continue;

                object raw = null;
                if (member is FieldInfo field) raw = field.GetValue(obj);
                else if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) raw = SafeGetProperty(property, obj);

                if (!TryConvertToDouble(raw, out double value)) continue;
                if (!TryReadFutureGrowthValue(member.Name, value, world, out double now, out GrowthTimeUnit unit)) continue;

                candidates.Add((GrowthKeyPriority(member.Name), member, member.Name, value, now, unit));
            }

            foreach (var candidate in candidates.OrderBy(c => c.Priority).ThenBy(c => c.Name))
            {
                if (!TryCalculateReducedFutureTime(candidate.Value, candidate.Now, candidate.Unit, world, reduction, out double adjusted, out oldDays, out newDays)) continue;
                if (!TrySetNumericMember(candidate.Member, obj, adjusted)) continue;

                fieldPath = pathPrefix + "." + candidate.Name;
                return true;
            }

            return false;
        }

        private static bool TryReduceGrowthFutureTimeInTree(TreeAttribute tree, IWorldAccessor world, double reduction, out double oldDays, out double newDays, out string fieldPath)
        {
            oldDays = 0.0;
            newDays = 0.0;
            fieldPath = string.Empty;

            List<(int Priority, TreeAttribute Parent, string Key, string Path, double Value, double Now, GrowthTimeUnit Unit)> candidates = new List<(int Priority, TreeAttribute Parent, string Key, string Path, double Value, double Now, GrowthTimeUnit Unit)>();
            CollectGrowthFutureTimeAttributes(tree, string.Empty, world, candidates);

            foreach (var candidate in candidates.OrderBy(c => c.Priority).ThenBy(c => c.Path))
            {
                if (!TryCalculateReducedFutureTime(candidate.Value, candidate.Now, candidate.Unit, world, reduction, out double adjusted, out oldDays, out newDays)) continue;

                candidate.Parent.SetDouble(candidate.Key, adjusted);
                fieldPath = candidate.Path;
                return true;
            }

            return false;
        }

        private static void CollectGrowthFutureTimeAttributes(TreeAttribute parent, string pathPrefix, IWorldAccessor world, List<(int Priority, TreeAttribute Parent, string Key, string Path, double Value, double Now, GrowthTimeUnit Unit)> candidates)
        {
            foreach (string key in parent.Keys.ToArray())
            {
                IAttribute attr = parent.GetAttribute(key);
                string path = string.IsNullOrEmpty(pathPrefix) ? key : pathPrefix + "." + key;

                if (attr is TreeAttribute child)
                {
                    CollectGrowthFutureTimeAttributes(child, path, world, candidates);
                    continue;
                }

                if (!LooksLikeBushNurseryGrowthKey(key)) continue;

                double value = parent.GetDouble(key, double.NaN);
                if (!TryReadFutureGrowthValue(key, value, world, out double now, out GrowthTimeUnit unit)) continue;

                candidates.Add((GrowthKeyPriority(key), parent, key, path, value, now, unit));
            }
        }

        private static bool LooksLikeBushNurseryGrowthKey(string key)
        {
            string lower = key.ToLowerInvariant();
            if (lower.Contains("lastcutting") || lower.Contains("cuttingtaken") || lower.Contains("cooldown")) return false;
            return BushNurseryGrowthHints.Any(lower.Contains);
        }

        private static int GrowthKeyPriority(string key)
        {
            string lower = key.ToLowerInvariant();
            if (lower.Equals("maturetotaldays", StringComparison.OrdinalIgnoreCase)) return 0;
            if (lower.Contains("mature") && lower.Contains("total") && lower.Contains("day")) return 1;
            if (lower.Contains("matur")) return 2;
            if (lower.Contains("grow") || lower.Contains("growth")) return 3;
            if (lower.Contains("ready")) return 4;
            if (lower.Contains("stage")) return 5;
            if (lower.Contains("transition")) return 6;
            if (lower.Contains("fruit")) return 7;
            return 100;
        }

        private static bool TryReadFutureGrowthValue(string key, double value, IWorldAccessor world, out double now, out GrowthTimeUnit unit)
        {
            now = 0.0;
            unit = GrowthTimeUnit.Hours;

            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            string lower = key.ToLowerInvariant();
            double nowHours = world.Calendar.TotalHours;
            double nowDays = world.Calendar.TotalDays;
            double maxDays = Math.Max(2.0 * world.Calendar.DaysPerYear, 730.0);
            double maxHours = maxDays * world.Calendar.HoursPerDay;

            if (lower.Contains("day"))
            {
                now = nowDays;
                unit = GrowthTimeUnit.Days;
                return value > nowDays && value - nowDays <= maxDays;
            }

            if (lower.Contains("hour"))
            {
                now = nowHours;
                unit = GrowthTimeUnit.Hours;
                return value > nowHours && value - nowHours <= maxHours;
            }

            if (value > nowHours && value - nowHours <= maxHours)
            {
                now = nowHours;
                unit = GrowthTimeUnit.Hours;
                return true;
            }

            if (value > nowDays && value - nowDays <= maxDays)
            {
                now = nowDays;
                unit = GrowthTimeUnit.Days;
                return true;
            }

            return false;
        }

        private static bool TryCalculateReducedFutureTime(double value, double now, GrowthTimeUnit unit, IWorldAccessor world, double reduction, out double adjusted, out double oldDays, out double newDays)
        {
            adjusted = 0.0;
            oldDays = 0.0;
            newDays = 0.0;

            double remaining = value - now;
            if (remaining <= 0.0001) return false;

            adjusted = now + remaining * Math.Max(0.0, 1.0 - reduction);
            if (adjusted >= value - 0.0001) return false;

            oldDays = ToDays(remaining, unit, world);
            newDays = ToDays(adjusted - now, unit, world);
            return true;
        }

        private static bool TryConvertToDouble(object raw, out double value)
        {
            try
            {
                if (raw == null)
                {
                    value = double.NaN;
                    return false;
                }
                value = Convert.ToDouble(raw);
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch
            {
                value = double.NaN;
                return false;
            }
        }

        private static bool TrySetNumericMember(MemberInfo member, object obj, double value)
        {
            try
            {
                if (member is FieldInfo field)
                {
                    field.SetValue(obj, Convert.ChangeType(value, field.FieldType));
                    return true;
                }
                else if (member is PropertyInfo property && property.CanWrite)
                {
                    property.SetValue(obj, Convert.ChangeType(value, property.PropertyType));
                    return true;
                }
            }
            catch { return false; }
            return false;
        }

        private static double ToDays(double value, GrowthTimeUnit unit, IWorldAccessor world)
        {
            return unit == GrowthTimeUnit.Days ? value : value / world.Calendar.HoursPerDay;
        }

        private enum GrowthTimeUnit { Hours, Days }

        private static object FindFruitingBushBehavior(BlockEntity be)
        {
            Type behaviorType = AccessTools.TypeByName("Vintagestory.GameContent.BEBehaviorFruitingBush");
            if (behaviorType != null)
            {
                MethodInfo genericGetBehavior = typeof(BlockEntity).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "GetBehavior" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);

                if (genericGetBehavior != null)
                {
                    try
                    {
                        object value = genericGetBehavior.MakeGenericMethod(behaviorType).Invoke(be, null);
                        if (value != null) return value;
                    }
                    catch { }
                }
            }

            foreach (MemberInfo member in be.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object value = null;
                if (member is FieldInfo field) value = field.GetValue(be);
                else if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) value = SafeGetProperty(property, be);

                object found = FindFruitingBushBehaviorInValue(value);
                if (found != null) return found;
            }

            return null;
        }

        private static object FindFruitingBushBehaviorInValue(object value)
        {
            if (value == null) return null;

            string fullName = value.GetType().FullName ?? value.GetType().Name;
            if (fullName.Contains("BEBehaviorFruitingBush", StringComparison.OrdinalIgnoreCase)) return value;

            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (object item in enumerable)
                {
                    string itemName = item?.GetType().FullName;
                    if (itemName != null && itemName.Contains("BEBehaviorFruitingBush", StringComparison.OrdinalIgnoreCase)) return item;
                }
            }

            return null;
        }

        private static object SafeGetProperty(PropertyInfo property, object obj)
        {
            try { return property.GetValue(obj); }
            catch { return null; }
        }

        private static bool WasCuttingCooldownRecentlyReduced(object fruitingBushBehavior, IPlayer player, double nowDays)
        {
            string key = CuttingCooldownReductionKey(fruitingBushBehavior, player);
            if (key == null) return false;

            return CuttingCooldownReductionMarks.TryGetValue(key, out double lastAppliedDays)
                && Math.Abs(nowDays - lastAppliedDays) <= CuttingCooldownDuplicateWindowDays;
        }

        private static void MarkCuttingCooldownReduced(object fruitingBushBehavior, IPlayer player, double nowDays)
        {
            string key = CuttingCooldownReductionKey(fruitingBushBehavior, player);
            if (key == null) return;

            if (CuttingCooldownReductionMarks.Count > 2048)
            {
                foreach (string oldKey in CuttingCooldownReductionMarks
                    .Where(pair => Math.Abs(nowDays - pair.Value) > 1.0)
                    .Select(pair => pair.Key)
                    .ToArray())
                {
                    CuttingCooldownReductionMarks.Remove(oldKey);
                }
            }

            CuttingCooldownReductionMarks[key] = nowDays;
        }

        private static string CuttingCooldownReductionKey(object fruitingBushBehavior, IPlayer player)
        {
            BlockEntity be = GetBlockEntity(fruitingBushBehavior);
            BlockPos pos = be?.Pos;
            if (pos == null) return null;

            return player.PlayerUID + ":" + pos.X + ":" + pos.Y + ":" + pos.Z;
        }

        private static object GetBState(object fruitingBushBehavior)
        {
            return GetMemberValue(fruitingBushBehavior, "BState");
        }

        private static object GetMemberValue(object obj, string name)
        {
            MemberInfo member = FindInstanceMember(obj.GetType(), name, requireWritable: false);
            if (member == null) return null;

            if (member is FieldInfo field) return field.GetValue(obj);
            if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) return SafeGetProperty(property, obj);

            return null;
        }

        private static double GetDoubleField(object obj, string name, double fallback)
        {
            object value = GetMemberValue(obj, name);
            if (value == null) return fallback;

            try { return Convert.ToDouble(value); }
            catch { return fallback; }
        }

        private static bool SetDoubleField(object obj, string name, double value)
        {
            MemberInfo member = FindInstanceMember(obj.GetType(), name, requireWritable: true);
            return member != null && TrySetNumericMember(member, obj, value);
        }

        private static MemberInfo FindInstanceMember(Type type, string name, bool requireWritable)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field != null) return field;

            PropertyInfo property = AccessTools.Property(type, name);
            if (property != null && property.GetIndexParameters().Length == 0 && (!requireWritable || property.CanWrite)) return property;

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (MemberInfo member in type.GetMembers(flags))
            {
                if (!member.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

                if (member is FieldInfo) return member;
                if (member is PropertyInfo candidate && candidate.GetIndexParameters().Length == 0 && (!requireWritable || candidate.CanWrite)) return member;
            }

            return null;
        }

        private static BlockEntity GetBlockEntity(object fruitingBushBehavior)
        {
            return GetMemberValue(fruitingBushBehavior, "BlockEntity") as BlockEntity;
        }

        private static void MarkBlockEntityDirty(object fruitingBushBehavior)
        {
            GetBlockEntity(fruitingBushBehavior)?.MarkDirty(true);
        }
    }
}