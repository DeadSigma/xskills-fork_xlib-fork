using System;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
	public class Sailing : XSkill
	{
		public int SteadyHelmId { get; private set; }
		public int StrongOarsmanId { get; private set; }
		public int CrewCaptainId { get; private set; }
		public int IronForearmsId { get; private set; }
		public int RiggerId { get; private set; }
		public int ShipwrightId { get; private set; }
		public int ReinforcedDeckingId { get; private set; }
		public int CargoCaptainId { get; private set; }
		public int CoastalPilotId { get; private set; }
		public int OpenSeaId { get; private set; }
		public int RiverRunnerId { get; private set; }
		public int TrawlerId { get; private set; }
		public int WaveReaderId { get; private set; }
		public int AirshipCaptainId { get; private set; } = -1;

		public Sailing(ICoreAPI api) : base("sailing", "xskills:skill-sailing", "xskills:group-survival", 200, 1.33f, 25)
		{
			XLeveling xleveling = XLeveling.Instance(api);
			if (xleveling != null)
			{
				xleveling.RegisterSkill(this);
			}
			bool flag = api.ModLoader.IsModEnabled("vsairshipmod") || api.ModLoader.IsModEnabled("airship");
			this.SteadyHelmId = base.AddAbility(new Ability("steadyhelm", "xskills:ability-steadyhelm", "xskills:abilitydesc-steadyhelm", 1, 3, new int[]
			{
				10, 1, 20, 20, 2, 40, 20, 2, 60
			}, false));
			this.StrongOarsmanId = base.AddAbility(new Ability("strongoarsman", "xskills:ability-strongoarsman", "xskills:abilitydesc-strongoarsman", 1, 3, new int[]
			{
				15, 30, 45
			}, false));
			this.CrewCaptainId = base.AddAbility(new Ability("crewcaptain", "xskills:ability-crewcaptain", "xskills:abilitydesc-crewcaptain", 5, 3, new int[]
			{
				5, 10, 15
			}, false));
			this.IronForearmsId = base.AddAbility(new Ability("ironforearms", "xskills:ability-ironforearms", "xskills:abilitydesc-ironforearms", 3, 3, new int[]
			{
				25, 50, 75
			}, false));
			this.RiggerId = base.AddAbility(new Ability("rigger", "xskills:ability-rigger", "xskills:abilitydesc-rigger", 5, 3, new int[]
			{
				20, 40, 60
			}, false));
			this.ShipwrightId = base.AddAbility(new Ability("shipwright", "xskills:ability-shipwright", "xskills:abilitydesc-shipwright", 3, 3, new int[]
			{
				20, 40, 60
			}, false));
			this.ReinforcedDeckingId = base.AddAbility(new Ability("reinforceddecking", "xskills:ability-reinforceddecking", "xskills:abilitydesc-reinforceddecking", 1, 3, new int[]
			{
				25, 50, 75
			}, false));
			this.CargoCaptainId = base.AddAbility(new Ability("cargocaptain", "xskills:ability-cargocaptain", "xskills:abilitydesc-cargocaptain", 3, 3, new int[]
			{
				15, 30, 45
			}, false));
			this.CoastalPilotId = base.AddAbility(new Ability("coastalpilot", "xskills:ability-coastalpilot", "xskills:abilitydesc-coastalpilot", 5, 3, new int[]
			{
				20, 35, 50
			}, false));
			this.OpenSeaId = base.AddAbility(new Ability("opensea", "xskills:ability-opensea", "xskills:abilitydesc-opensea", 5, 3, new int[]
			{
				20, 35, 50
			}, false));
			this.RiverRunnerId = base.AddAbility(new Ability("riverrunner", "xskills:ability-riverrunner", "xskills:abilitydesc-riverrunner", 5, 3, new int[]
			{
				20, 35, 50
			}, false));
			this.TrawlerId = base.AddAbility(new Ability("trawler", "xskills:ability-trawler", "xskills:abilitydesc-trawler", 6, 3, new int[]
			{
				5, 10, 15
			}, false));
			this.WaveReaderId = base.AddAbility(new Ability("wavereader", "xskills:ability-wavereader", "xskills:abilitydesc-wavereader", 6, 1, 0, false));
			base.SpecialisationID = base.AddAbility(new Ability("seadog", "xskills:ability-seadog", "xskills:abilitydesc-seadog", 5, 1, new int[]
			{
				40
			}, false));
			if (flag)
			{
				this.AirshipCaptainId = base.AddAbility(new Ability("airshipcaptain", "xskills:ability-airshipcaptain", "xskills:abilitydesc-airshipcaptain", 6, 3, new int[]
				{
					20, 40, 60
				}, false));
			}
			new ExclusiveAbilityRequirement(base[this.CoastalPilotId], base[this.OpenSeaId], 1, false);
			new ExclusiveAbilityRequirement(base[this.CoastalPilotId], base[this.RiverRunnerId], 1, false);
			new ExclusiveAbilityRequirement(base[this.OpenSeaId], base[this.RiverRunnerId], 1, false);
			base.ExperienceEquation = new ExperienceEquationDelegate(base.QuadraticEquation);
			base.ExpBase = 40f;
			base.ExpMult = 10f;
			base.ExpEquationValue = 0.8f;
		}
	}
}