using System;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class Brewing : XSkill
    {
        public int HopWhispererId { get; private set; }
        public int VintnerId { get; private set; }
        public int MeadMasterId { get; private set; }
        public int HangoverResistanceId { get; private set; }
        public int TipplersToleranceId { get; private set; }
        public int SteadyHandId { get; private set; } = -1;
        public int PureYeastId { get; private set; } = -1;
        public int BigBatchId { get; private set; } = -1;
        public int ThriftyBrewerId { get; private set; } = -1;
        public int ReputableTavernkeepId { get; private set; } = -1;
        public int MaltMasterId { get; private set; } = -1;

        public Brewing(ICoreAPI api) : base("brewing", "xskills:skill-brewing", "xskills:group-processing", 200, 1.33f, 25)
        {
            XLeveling xleveling = XLeveling.Instance(api);
            if (xleveling != null)
            {
                xleveling.RegisterSkill(this);
            }

            bool hasBrewingMod = api.ModLoader.IsModEnabled("brewing");

            // (5) минимальный уровень
            // (3) макс тир
            // { 20, 35, 50 } значения
            this.HopWhispererId = base.AddAbility(new Ability(
                "hopwhisperer",
                "xskills:ability-hopwhisperer",
                "xskills:abilitydesc-hopwhisperer",
                5, 3, new int[] { 20, 35, 50 }, false));

            // (5) минимальный уровень
            // (3) макс тир
            // { 20, 35, 50 } значения
            this.VintnerId = base.AddAbility(new Ability(
                "vintner",
                "xskills:ability-vintner",
                "xskills:abilitydesc-vintner",
                5, 3, new int[] { 20, 35, 50 }, false));

            // (5) минимальный уровень
            // (3) макс тир
            // { 20, 35, 50 } значения
            this.MeadMasterId = base.AddAbility(new Ability(
                "meadmaster",
                "xskills:ability-meadmaster",
                "xskills:abilitydesc-meadmaster",
                5, 3, new int[] { 20, 35, 50 }, false));

            // (3) минимальный уровень
            // (3) макс тир
            // { 30, 60, 90 } значения
            this.HangoverResistanceId = base.AddAbility(new Ability(
                "hangoverresistance",
                "xskills:ability-hangoverresistance",
                "xskills:abilitydesc-hangoverresistance",
                3, 3, new int[] { 30, 60, 90 }, false));

            // (7) минимальный уровень
            // (1) макс тир
            // 0 - базовое значение
            this.TipplersToleranceId = base.AddAbility(new Ability(
                "tipplerstolerance",
                "xskills:ability-tipplerstolerance",
                "xskills:abilitydesc-tipplerstolerance",
                7, 1, 0, false));

            // (5) минимальный уровень
            // (1) макс тир
            // { 40 } значение
            base.SpecialisationID = base.AddAbility(new Ability(
                "masterbrewmaster",
                "xskills:ability-masterbrewmaster",
                "xskills:abilitydesc-masterbrewmaster",
                5, 1, new int[] { 40 }, false));

            if (hasBrewingMod)
            {
                // (1) минимальный уровень
                // (3) макс тир
                // { 10, 20, 30 } значения
                this.SteadyHandId = base.AddAbility(new Ability(
                    "steadyhand",
                    "xskills:ability-steadyhand",
                    "xskills:abilitydesc-steadyhand",
                    1, 3, new int[] { 10, 20, 30 }, false));

                // чистые дрожжи (Pure Yeast)
                // (1) минимальный уровень
                // (3) макс тир
                // { 10, 20, 30 } значения
                this.PureYeastId = base.AddAbility(new Ability(
                    "pureyeast",
                    "xskills:ability-pureyeast",
                    "xskills:abilitydesc-pureyeast",
                    1, 3, new int[] { 10, 20, 30 }, false));

                // (3) минимальный уровень
                // (3) макс тир
                // { 10, 20, 30 } значения
                this.BigBatchId = base.AddAbility(new Ability(
                    "bigbatch",
                    "xskills:ability-bigbatch",
                    "xskills:abilitydesc-bigbatch",
                    3, 3, new int[] { 10, 20, 30 }, false));

                // (1) минимальный уровень
                // (3) макс тир
                // { 5, 10, 15 } значения
                this.ThriftyBrewerId = base.AddAbility(new Ability(
                    "thriftybrewer",
                    "xskills:ability-thriftybrewer",
                    "xskills:abilitydesc-thriftybrewer",
                    1, 3, new int[] { 5, 10, 15 }, false));

                // (6) минимальный уровень
                // (2) макс тир
                // { 15, 30 } значения
                this.ReputableTavernkeepId = base.AddAbility(new Ability(
                    "reputabletavernkeep",
                    "xskills:ability-reputabletavernkeep",
                    "xskills:abilitydesc-reputabletavernkeep",
                    6, 2, new int[] { 15, 30 }, false));

                // (3) минимальный уровень
                // (3) макс тир
                // { 10, 20, 30 } значения
                this.MaltMasterId = base.AddAbility(new Ability(
                    "maltmaster",
                    "xskills:ability-maltmaster",
                    "xskills:abilitydesc-maltmaster",
                    3, 3, new int[] { 10, 20, 30 }, false));
            }

            new ExclusiveAbilityRequirement(base[this.HopWhispererId], base[this.VintnerId], 1, false);
            new ExclusiveAbilityRequirement(base[this.HopWhispererId], base[this.MeadMasterId], 1, false);
            new ExclusiveAbilityRequirement(base[this.VintnerId], base[this.MeadMasterId], 1, false);

            base.ExperienceEquation = new ExperienceEquationDelegate(base.QuadraticEquation);
            base.ExpBase = 40f;
            base.ExpMult = 10f;
            base.ExpEquationValue = 0.8f;
        }
    }
}