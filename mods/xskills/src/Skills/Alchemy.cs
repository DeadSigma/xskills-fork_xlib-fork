using System.Collections.Generic;
using Vintagestory.API.Common;
using XLib.XLeveling;

namespace XSkills
{
    public class Alchemy : XSkill
    {
        public int IngredientEfficiencyId { get; private set; }
        public int BrewingSpeedId { get; private set; }
        public int PotentPotionId { get; private set; }
        public int AlchemistId { get; private set; }
        public int TenuationId { get; private set; }
        public int PotionQualityId { get; private set; }

        public Alchemy(ICoreAPI api) : base("alchemy", "xskills:skill-alchemy", "xskills:group-processing")
        {
            (XLeveling.Instance(api))?.RegisterSkill(this);
            // Экономия ингредиентов
            // 0: шанс срабатывания в процентах
            // 1: процент возвращаемого количества
            IngredientEfficiencyId = this.AddAbility(new Ability(
                "ingredientefficiency",
                "xskills:ability-ingredientefficiency",
                "xskills:abilitydesc-ingredientefficiency",
                1, 3,
                new int[]
                { 25, 10, 30, 20, 40, 30 }
            ));

            // ускорение варки
            // 0: процент ускорения
            BrewingSpeedId = this.AddAbility(new Ability(
                "brewingspeed",
                "xskills:ability-brewingspeed",
                "xskills:abilitydesc-brewingspeed",
                1, 3, new int[] { 15, 25, 40 }));

            // специализация - профессия алхимика
            // 0: бонус опыта
            AlchemistId = this.AddAbility(new Ability(
                "alchemist",
                "xskills:ability-alchemist",
                "xskills:abilitydesc-alchemist",
                5, 1, new int[] { 40 }));


            // Тенуация
            // увеличивает количество порций при приготовлении
            // 0: базовое значение
            // 1: прирост за уровень
            // 2: максимальное значение
            TenuationId = this.AddAbility(new Ability(
                "tenuation",
                "xskills:ability-tenuation",
                "xskills:abilitydesc-tenuation",
                3, 3,
                new int[]
                { 10, 1, 20, 20, 1, 30, 20, 1, 40 }));

            // Качество зелий
            // 0: множитель качества
            // 1: внутренний предел качества
            PotionQualityId = this.AddAbility(new Ability(
                "potionquality",
                "xskills:ability-potionquality",
                "xskills:abilitydesc-potionquality",
                3, 2,
                new int[]
                { 1, 7, 2, 12 }));

            this.ExperienceEquation = QuadraticEquation;
            this.ExpBase = 40;
            this.ExpMult = 10.0f;
            this.ExpEquationValue = 0.8f;
        }
    }
}