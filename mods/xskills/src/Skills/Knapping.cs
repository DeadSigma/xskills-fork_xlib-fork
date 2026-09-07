using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class Knapping : XSkill
    {
        public int WideChippingId { get; private set; }
        public int FastKnapperId { get; private set; }
        public int JackPotId { get; private set; }

        public Knapping(ICoreAPI api) : base("knapping", "xskills:skill-knapping", "xskills:group-processing")
        {
            XLeveling.Instance(api)?.RegisterSkill(this);

            // extra nearby voxels per strike
            WideChippingId = AddAbility(new Ability(
                "widechipping",
                "xskills:ability-widechipping",
                "xskills:abilitydesc-widechipping",
                1, 5, new int[] { 1, 2, 3, 4, 5 }));

            // chance to remove all remaining excess material
            FastKnapperId = AddAbility(new Ability(
                "fastknapper",
                "xskills:ability-fastknapper",
                "xskills:abilitydesc-fastknapper",
                1, 3, new int[] { 5, 10, 15 }));

            // profession
            SpecialisationID = AddAbility(new Ability(
                "knapper",
                "xskills:ability-knapper",
                "xskills:abilitydesc-knapper",
                3, 1, new int[] { 40 }));

            // duplicate result
            JackPotId = AddAbility(new Ability(
                "knappingjackpot",
                "xskills:ability-knappingjackpot",
                "xskills:abilitydesc-knappingjackpot",
                3, 3, new int[] { 5, 0, 5, 5, 1, 15, 5, 1, 25 }));

            ExperienceEquation = QuadraticEquation;
            ExpBase = 40;
            ExpMult = 10.0f;
            ExpEquationValue = 0.8f;
        }
    }
}
