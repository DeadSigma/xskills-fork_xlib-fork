using System;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class Riding : XSkill
    {
        public int SteadyReinsId { get; private set; }
        public int LightTackId { get; private set; }
        public int TirelessTrotId { get; private set; }
        public int TrailProvisionsId { get; private set; }
        public int SaddleSmithId { get; private set; }
        public int VetId { get; private set; }
        public int LightCavalryId { get; private set; }
        public int HeavyCavalryId { get; private set; }
        public int SkyRiderId { get; private set; }
        public int MountedCombatantId { get; private set; }
        public int LongRiderId { get; private set; }
        public int PackAnimalId { get; private set; }
        public int MultiSeatMasterId { get; private set; }
        public int EquusAffinityId { get; private set; } = -1;
        public int DragonBondId { get; private set; } = -1;
        public int PegasusRiderId { get; private set; } = -1;

        public Riding(ICoreAPI api) : base("riding", "xskills:skill-riding", "xskills:group-survival", 200, 1.33f, 25)
        {
            XLeveling xleveling = XLeveling.Instance(api);
            if (xleveling != null)
            {
                xleveling.RegisterSkill(this);
            }

            // Проверки модов
            bool hasEquusMods = api.ModLoader.IsModEnabled("equus") || api.ModLoader.IsModEnabled("equusferus") || api.ModLoader.IsModEnabled("equusdestrier");
            bool hasDraconisMods = api.ModLoader.IsModEnabled("draconis") || api.ModLoader.IsModEnabled("draconisrhinocroma");
            bool hasPegasusMods = api.ModLoader.IsModEnabled("pegasus");
            bool hasSkyRiderMods = api.ModLoader.IsModEnabled("pegasus") || api.ModLoader.IsModEnabled("draconisrhinocroma");
            bool hasFeverstoneWilds = api.ModLoader.IsModEnabled("feverstonewilds");

            // (3) минимальный уровень
            // (3) макс тир
            this.SteadyReinsId = base.AddAbility(new Ability(
                "steadyreins",
                "xskills:ability-steadyreins",
                "xskills:abilitydesc-steadyreins",
                3, 3, new int[] { 10, 1, 20, 20, 2, 40, 20, 2, 60 }, false));

            // (1) минимальный уровень
            // (1) макс тир
            this.LightTackId = base.AddAbility(new Ability(
                "lighttack",
                "xskills:ability-lighttack",
                "xskills:abilitydesc-lighttack",
                1, 1, 0, false));

            // (4) минимальный уровень
            // (3) макс тир
            this.TirelessTrotId = base.AddAbility(new Ability(
                "tirelesstrot",
                "xskills:ability-tirelesstrot",
                "xskills:abilitydesc-tirelesstrot",
                4, 3, new int[] { 15, 30, 45 }, false));

            // (3) минимальный уровень
            // (3) макс тир
            this.TrailProvisionsId = base.AddAbility(new Ability(
                "trailprovisions",
                "xskills:ability-trailprovisions",
                "xskills:abilitydesc-trailprovisions",
                3, 3, new int[] { 20, 40, 60 }, false));

            // (4) минимальный уровень
            // (3) макс тир
            // Привязка к equus, equusferus или equusdestrier
            Ability saddleSmithAbility = new Ability(
                "saddlesmith",
                "xskills:ability-saddlesmith",
                "xskills:abilitydesc-saddlesmith",
                4, 3, new int[] { 25, 50, 75 }, false);
            saddleSmithAbility.Enabled = hasEquusMods;
            this.SaddleSmithId = base.AddAbility(saddleSmithAbility);

            // (4) минимальный уровень
            // (2) макс тир
            this.VetId = base.AddAbility(new Ability(
                "vet",
                "xskills:ability-vet",
                "xskills:abilitydesc-vet",
                4, 2, new int[] { 5, 10 }, false));

            // (7) минимальный уровень
            // (3) макс тир
            this.LightCavalryId = base.AddAbility(new Ability(
                "lightcavalry",
                "xskills:ability-lightcavalry",
                "xskills:abilitydesc-lightcavalry",
                7, 3, new int[] { 20, 35, 50 }, false));

            // (7) минимальный уровень
            // (3) макс тир
            // Привязка к feverstonewilds
            Ability heavyCavalryAbility = new Ability(
                "heavycavalry",
                "xskills:ability-heavycavalry",
                "xskills:abilitydesc-heavycavalry",
                7, 3, new int[] { 20, 35, 50 }, false);
            heavyCavalryAbility.Enabled = hasFeverstoneWilds;
            this.HeavyCavalryId = base.AddAbility(heavyCavalryAbility);

            // (7) минимальный уровень
            // (3) макс тир
            // Привязка к pegasus или draconisrhinocroma
            Ability skyRiderAbility = new Ability(
                "skyrider",
                "xskills:ability-skyrider",
                "xskills:abilitydesc-skyrider",
                7, 3, new int[] { 20, 35, 50 }, false);
            skyRiderAbility.Enabled = hasSkyRiderMods;
            this.SkyRiderId = base.AddAbility(skyRiderAbility);

            // (5) минимальный уровень
            // (3) макс тир
            // Отключён
            Ability mountedCombatantAbility = new Ability(
                "mountedcombatant",
                "xskills:ability-mountedcombatant",
                "xskills:abilitydesc-mountedcombatant",
                5, 3, new int[] { 15, 30, 45 }, false);
            mountedCombatantAbility.Enabled = false;
            this.MountedCombatantId = base.AddAbility(mountedCombatantAbility);

            // (5) минимальный уровень
            // (3) макс тир
            // Отключён
            Ability longRiderAbility = new Ability(
                "longrider",
                "xskills:ability-longrider",
                "xskills:abilitydesc-longrider",
                5, 3, new int[] { 25, 50, 75 });
            longRiderAbility.Enabled = false;
            this.LongRiderId = base.AddAbility(longRiderAbility);

            // (5) минимальный уровень
            // (3) макс тир
            // Отключён
            Ability packAnimalAbility = new Ability(
                "packanimal",
                "xskills:ability-packanimal",
                "xskills:abilitydesc-packanimal",
                5, 3, new int[] { 1, 2, 3 }, false);
            packAnimalAbility.Enabled = false;
            this.PackAnimalId = base.AddAbility(packAnimalAbility);

            // (8) минимальный уровень
            // (2) макс тир
            // Отключён
            Ability multiSeatMasterAbility = new Ability(
                "multiseatmaster",
                "xskills:ability-multiseatmaster",
                "xskills:abilitydesc-multiseatmaster",
                8, 2, new int[] { 10, 20 }, false);
            multiSeatMasterAbility.Enabled = false;
            this.MultiSeatMasterId = base.AddAbility(multiSeatMasterAbility);

            // (5) минимальный уровень
            // (1) макс тир
            base.SpecialisationID = base.AddAbility(new Ability(
                "equestrian",
                "xskills:ability-equestrian",
                "xskills:abilitydesc-equestrian",
                5, 1, new int[] { 40 }, false));

            if (hasEquusMods)
            {
                // (8) минимальный уровень
                // (3) макс тир
                this.EquusAffinityId = base.AddAbility(new Ability(
                    "equusaffinity",
                    "xskills:ability-equusaffinity",
                    "xskills:abilitydesc-equusaffinity",
                    8, 3, new int[] { 15, 30, 45 }, false));
            }

            if (hasDraconisMods)
            {
                // (8) минимальный уровень
                // (3) макс тир
                this.DragonBondId = base.AddAbility(new Ability(
                    "dragonbond",
                    "xskills:ability-dragonbond",
                    "xskills:abilitydesc-dragonbond",
                    8, 3, new int[] { 15, 30, 45 }, false));
            }

            if (hasPegasusMods)
            {
                // (8) минимальный уровень
                // (3) макс тир
                this.PegasusRiderId = base.AddAbility(new Ability(
                    "pegasusrider",
                    "xskills:ability-pegasusrider",
                    "xskills:abilitydesc-pegasusrider",
                    8, 3, new int[] { 15, 30, 45 }, false));
            }

            new ExclusiveAbilityRequirement(base[this.LightCavalryId], base[this.HeavyCavalryId], 1, false);
            new ExclusiveAbilityRequirement(base[this.LightCavalryId], base[this.SkyRiderId], 1, false);
            new ExclusiveAbilityRequirement(base[this.HeavyCavalryId], base[this.SkyRiderId], 1, false);

            base.ExperienceEquation = new ExperienceEquationDelegate(base.QuadraticEquation);
            base.ExpBase = 40f;
            base.ExpMult = 10f;
            base.ExpEquationValue = 0.8f;
        }
    }
}