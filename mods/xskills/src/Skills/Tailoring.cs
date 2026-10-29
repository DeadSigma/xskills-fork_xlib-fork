using System;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class Tailoring : XSkill
    {
        public int PatternMemoryId { get; private set; }
        public int ReinforcedSeamsId { get; private set; }
        public int FiberSorterId { get; private set; }
        public int WinterWeaverId { get; private set; }
        public int WanderingTailorId { get; private set; }
        public int LeatherWorkerId { get; private set; } = -1; // Disabled, not working
        public int ClothWeaverId { get; private set; } = -1;// Disabled, not working
        public int ThriftyCutsId { get; private set; } = -1;
        public int DurableWeaveId { get; private set; } = -1;
        public int PatchworkId { get; private set; } = -1;
        public int SummerWeaverId { get; private set; } = -1;

        public Tailoring(ICoreAPI api) : base("tailoring", "xskills:skill-tailoring", "xskills:group-processing", 200, 1.33f, 25)
        {
            XLeveling xleveling = XLeveling.Instance(api);
            if (xleveling != null)
            {
                xleveling.RegisterSkill(this);
            }

            // Both mods are required
            bool hasAdvancedTailoringMods = api.ModLoader.IsModEnabled("tailorsdelight") && api.ModLoader.IsModEnabled("clothierheirloomsmod");
            bool hasHydrationMod = api.ModLoader.IsModEnabled("hydrateordeitrate");

            // (3) minimum level
            // (3) maximum tier
            // { 5, 10, 15 } values
            this.PatternMemoryId = base.AddAbility(new Ability(
                "patternmemory",
                "xskills:ability-patternmemory",
                "xskills:abilitydesc-patternmemory",
                3, 3, new int[] { 5, 10, 15 }, false));

            // (3) minimum level
            // (3) maximum tier
            // { 5, 10, 15 } values
            this.ReinforcedSeamsId = base.AddAbility(new Ability(
                "reinforcedseams",
                "xskills:ability-reinforcedseams",
                "xskills:abilitydesc-reinforcedseams",
                3, 3, new int[] { 5, 10, 15 }, false));

            // (3) minimum level
            // (3) maximum tier
            // { 15, 30, 45 } values
            this.FiberSorterId = base.AddAbility(new Ability(
                "fibersorter",
                "xskills:ability-fibersorter",
                "xskills:abilitydesc-fibersorter",
                3, 3, new int[] { 15, 30, 45 }, false));

            // (5) minimum level
            // (3) maximum tier
            // { 20, 35, 50 } values
            this.WinterWeaverId = base.AddAbility(new Ability(
                "winterweaver",
                "xskills:ability-winterweaver",
                "xskills:abilitydesc-winterweaver",
                5, 3, new int[] { 20, 35, 50 }, false));

            // (7) minimum level
            // (2) maximum tier
            // { 10, 20 } values
            this.WanderingTailorId = base.AddAbility(new Ability(
                "wanderingtailor",
                "xskills:ability-wanderingtailor",
                "xskills:abilitydesc-wanderingtailor",
                7, 2, new int[] { 10, 20 }, false));

            // (5) minimum level
            // (1) maximum tier
            // { 40 } value
            base.SpecialisationID = base.AddAbility(new Ability(
                "couturier",
                "xskills:ability-couturier",
                "xskills:abilitydesc-couturier",
                5, 1, new int[] { 40 }, false));


            if (hasAdvancedTailoringMods)
            {
                // (1) minimum level
                // (3) maximum tier
                // { 5, 10, 15 } values
                this.ThriftyCutsId = base.AddAbility(new Ability(
                    "thriftycuts",
                    "xskills:ability-thriftycuts",
                    "xskills:abilitydesc-thriftycuts",
                    1, 3, new int[] { 5, 10, 15 }, false));

                // (1) minimum level
                // (3) maximum tier
                // { 20, 40, 60 } values
                this.DurableWeaveId = base.AddAbility(new Ability(
                    "durableweave",
                    "xskills:ability-durableweave",
                    "xskills:abilitydesc-durableweave",
                    1, 3, new int[] { 20, 40, 60 }, false));

                // (3) minimum level
                // (3) maximum tier
                // { 25, 50, 75 } values
                this.PatchworkId = base.AddAbility(new Ability(
                    "patchwork",
                    "xskills:ability-patchwork",
                    "xskills:abilitydesc-patchwork",
                    3, 3, new int[] { 25, 50, 75 }, false));
            }

            if (hasHydrationMod)
            {
                // (5) minimum level
                // (3) maximum tier
                // { 20, 35, 50 } values
                this.SummerWeaverId = base.AddAbility(new Ability(
                    "summerweaver",
                    "xskills:ability-summerweaver",
                    "xskills:abilitydesc-summerweaver",
                    5, 3, new int[] { 20, 35, 50 }, false));
            }

            if (hasHydrationMod)
            {
                new ExclusiveAbilityRequirement(base[this.WinterWeaverId], base[this.SummerWeaverId], 1, false);
            }

            base.ExperienceEquation = new ExperienceEquationDelegate(base.QuadraticEquation);
            base.ExpBase = 40f;
            base.ExpMult = 10f;
            base.ExpEquationValue = 0.8f;
        }
    }
}