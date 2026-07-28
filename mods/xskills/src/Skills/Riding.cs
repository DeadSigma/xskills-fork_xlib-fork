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
			bool flag = api.ModLoader.IsModEnabled("equus");
			bool flag2 = api.ModLoader.IsModEnabled("draconis") || api.ModLoader.IsModEnabled("draconisrhinocroma");
			bool flag3 = api.ModLoader.IsModEnabled("pegasus");
            this.SteadyReinsId = base.AddAbility(new Ability("steadyreins", "xskills:ability-steadyreins", "xskills:abilitydesc-steadyreins", 3, 3, new int[]
{
    10, 1, 20, 20, 2, 40, 20, 2, 60
}, false));

            this.LightTackId = base.AddAbility(new Ability("lighttack", "xskills:ability-lighttack", "xskills:abilitydesc-lighttack", 1, 1, 0, false));

            this.TirelessTrotId = base.AddAbility(new Ability("tirelesstrot", "xskills:ability-tirelesstrot", "xskills:abilitydesc-tirelesstrot", 4, 3, new int[]
            {
    15, 30, 45
            }, false));

            this.TrailProvisionsId = base.AddAbility(new Ability("trailprovisions", "xskills:ability-trailprovisions", "xskills:abilitydesc-trailprovisions", 3, 3, new int[]
            {
    20, 40, 60
            }, false));

            this.SaddleSmithId = base.AddAbility(new Ability("saddlesmith", "xskills:ability-saddlesmith", "xskills:abilitydesc-saddlesmith", 4, 3, new int[]
            {
    25, 50, 75
            }, false));

            this.VetId = base.AddAbility(new Ability("vet", "xskills:ability-vet", "xskills:abilitydesc-vet", 4, 2, new int[]
            {
    5, 10
            }, false));

            this.LightCavalryId = base.AddAbility(new Ability("lightcavalry", "xskills:ability-lightcavalry", "xskills:abilitydesc-lightcavalry", 7, 3, new int[]
            {
    20, 35, 50
            }, false));

            this.HeavyCavalryId = base.AddAbility(new Ability("heavycavalry", "xskills:ability-heavycavalry", "xskills:abilitydesc-heavycavalry", 7, 3, new int[]
            {
    20, 35, 50
            }, false));

            this.SkyRiderId = base.AddAbility(new Ability("skyrider", "xskills:ability-skyrider", "xskills:abilitydesc-skyrider", 7, 3, new int[]
            {
    20, 35, 50
            }, false));

            this.MountedCombatantId = base.AddAbility(new Ability("mountedcombatant", "xskills:ability-mountedcombatant", "xskills:abilitydesc-mountedcombatant", 5, 3, new int[]
            {
    15, 30, 45
            }, false));

            this.LongRiderId = base.AddAbility(new Ability("longrider", "xskills:ability-longrider", "xskills:abilitydesc-longrider", 5, 3, new int[]
            {
    25, 50, 75
            }, false));

            this.PackAnimalId = base.AddAbility(new Ability("packanimal", "xskills:ability-packanimal", "xskills:abilitydesc-packanimal", 5, 3, new int[]
            {
    1, 2, 3
            }, false));

            this.MultiSeatMasterId = base.AddAbility(new Ability("multiseatmaster", "xskills:ability-multiseatmaster", "xskills:abilitydesc-multiseatmaster", 8, 2, new int[]
            {
    10, 20
            }, false));

            base.SpecialisationID = base.AddAbility(new Ability("equestrian", "xskills:ability-equestrian", "xskills:abilitydesc-equestrian", 5, 1, new int[]
            {
    40
            }, false));

            if (flag)
            {
                this.EquusAffinityId = base.AddAbility(new Ability("equusaffinity", "xskills:ability-equusaffinity", "xskills:abilitydesc-equusaffinity", 8, 3, new int[]
                {
        15, 30, 45
                }, false));
            }

            if (flag2)
            {
                this.DragonBondId = base.AddAbility(new Ability("dragonbond", "xskills:ability-dragonbond", "xskills:abilitydesc-dragonbond", 8, 3, new int[]
                {
        15, 30, 45
                }, false));
            }

            if (flag3)
            {
                this.PegasusRiderId = base.AddAbility(new Ability("pegasusrider", "xskills:ability-pegasusrider", "xskills:abilitydesc-pegasusrider", 8, 3, new int[]
                {
        15, 30, 45
                }, false));
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