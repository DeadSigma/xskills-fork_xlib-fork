using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace XSkills
{
    internal static class AlchemyPotionDurationPatch
    {
        // Качество текущего обрабатываемого зелья
        [ThreadStatic]
        private static float currentPotionQuality;

        private static ICoreAPI api;

        private static FieldInfo sourceStackField;

        private static FieldInfo durationField;
        private static PropertyInfo durationProperty;


        // Каждая единица качества:
        //
        // quality 1  = +10%
        // quality 5  = +50%
        // quality 10 = +100%
        private const float DurationBonusPerQuality = 0.10f;


        public static void Apply(
            Harmony harmony,
            Assembly alchemyAssembly,
            ICoreAPI coreApi
        )
        {
            api = coreApi;

            if (harmony == null || alchemyAssembly == null)
            {
                return;
            }


            Type consumableType =
                alchemyAssembly.GetType(
                    "Alchemy.PotionConsumableLogic",
                    false
                );

            Type registryType =
                alchemyAssembly.GetType(
                    "Alchemy.EffectRegistry",
                    false
                );

            Type potionDataType =
                alchemyAssembly.GetType(
                    "Alchemy.PotionData",
                    false
                );

            Type effectContextType =
                alchemyAssembly.GetType(
                    "Alchemy.EffectContext",
                    false
                );


            if (consumableType == null
                || registryType == null
                || potionDataType == null
                || effectContextType == null)
            {
                return;
            }


            MethodInfo processPotion =
                consumableType.GetMethod(
                    "TryProcessPotionEffects",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            MethodInfo buildEffect =
                registryType.GetMethod(
                    "Build",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            sourceStackField =
                potionDataType.GetField(
                    "SourceStack",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            durationField =
                effectContextType.GetField(
                    "Duration",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            durationProperty =
                effectContextType.GetProperty(
                    "Duration",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            if (processPotion == null
                || buildEffect == null
                || sourceStackField == null
                || (durationField == null
                    && durationProperty == null))
            {
                return;
            }


            Type patchType =
                typeof(AlchemyPotionDurationPatch);


            MethodInfo processPrefix =
                patchType.GetMethod(
                    nameof(TryProcessPotionEffects_Prefix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic
                );

            MethodInfo processFinalizer =
                patchType.GetMethod(
                    nameof(TryProcessPotionEffects_Finalizer),
                    BindingFlags.Static |
                    BindingFlags.NonPublic
                );

            MethodInfo buildPostfix =
                patchType.GetMethod(
                    nameof(Build_Postfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic
                );


            harmony.Patch(
                processPotion,
                prefix: new HarmonyMethod(processPrefix),
                finalizer: new HarmonyMethod(processFinalizer)
            );


            harmony.Patch(
                buildEffect,
                postfix: new HarmonyMethod(buildPostfix)
            );
        }



        private static void TryProcessPotionEffects_Prefix(
            object[] __args
        )
        {
            currentPotionQuality = 0f;


            // Аргументы оригинального метода:
            //
            // 0 = EntityAgent byEntity
            // 1 = PotionData data
            // 2 = ICoreAPI api

            if (__args == null
                || __args.Length < 2)
            {
                return;
            }


            object potionData =
                __args[1];

            if (potionData == null)
                return;


            ItemStack sourceStack =
                sourceStackField?.GetValue(
                    potionData
                ) as ItemStack;


            if (sourceStack == null)
                return;


            float quality =
                QualityUtil.GetQuality(
                    sourceStack
                );


            if (quality <= 0f)
                return;


            currentPotionQuality =
                quality;

        }


        private static void Build_Postfix(
            object __result
        )
        {
            if (__result == null)
                return;

            if (currentPotionQuality <= 0f)
                return;


            int baseDuration =
                GetDuration(
                    __result
                );


            // Главный фильтр
            // Duration <= 0 = мгновенное зелье
            //
            // Recall, Nutrition, Temporal, Reshape, Grow, Shrink и т.д сюда не попадут
            if (baseDuration <= 0)
                return;


            float multiplier =
                1f
                + currentPotionQuality
                * DurationBonusPerQuality;


            int finalDuration =
                Math.Max(
                    1,
                    (int)Math.Round(
                        baseDuration
                        * multiplier
                    )
                );


            SetDuration(
                __result,
                finalDuration
            );

        }


        // CLEANUP
        private static Exception TryProcessPotionEffects_Finalizer(
            Exception __exception
        )
        {
            currentPotionQuality = 0f;

            return __exception;
        }


        private static int GetDuration(
            object context
        )
        {
            if (context == null)
                return 0;


            if (durationField != null)
            {
                object value =
                    durationField.GetValue(
                        context
                    );

                if (value is int duration)
                    return duration;
            }


            if (durationProperty != null)
            {
                object value =
                    durationProperty.GetValue(
                        context
                    );

                if (value is int duration)
                    return duration;
            }


            return 0;
        }


        private static void SetDuration(
            object context,
            int duration
        )
        {
            if (context == null)
                return;


            if (durationField != null)
            {
                durationField.SetValue(
                    context,
                    duration
                );

                return;
            }


            if (durationProperty?.CanWrite == true)
            {
                durationProperty.SetValue(
                    context,
                    duration
                );
            }
        }
    }
}