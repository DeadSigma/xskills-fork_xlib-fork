using System;
using System.Collections.Generic;

namespace XLib.XLeveling
{
    /// <summary>
    /// Предоставляет механизмы декларативной миграции конфигураций между версиями.
    /// </summary>
    public static class ConfigMigrations
    {
        /// <summary>
        /// Текущая версия конфигурации. 
        /// Значение необходимо увеличивать в релизах, требующих применения новых миграций
        /// </summary>
        public const int CurrentVersion = 10;

        /// <summary>
        /// Определяет параметры сброса навыка или способности при обновлении версии
        /// </summary>
        public struct SkillReset
        {
            /// <summary>
            /// Версия, начиная с которой применяется данный сброс.
            /// </summary>
            public int Version;

            /// <summary>
            /// Идентификатор навыка (соответствует имени файла, например, skill.json)
            /// </summary>
            public string Skill;

            /// <summary>
            /// Идентификатор способности (перка). Если значение null или пусто, будет сброшен весь файл навыка
            /// </summary>
            public string Ability;

            /// <summary>
            /// Предыдущие стандартные значения способности. 
            /// Если заданы, сброс применяется только у тех пользователей, чьи значения совпадают с указанными
            /// </summary>
            public int[] OldValues;

            /// <summary>
            /// Инициализирует новый экземпляр структуры <see cref="SkillReset"/>.
            /// </summary>
            /// <param name="version">Версия, в которой применяется сброс</param>
            /// <param name="skill">Имя навыка</param>
            /// <param name="ability">Имя способности (null для сброса всего навыка)</param>
            /// <param name="oldValues">Массив старых значений для проверки (null для безусловного сброса)</param>
            public SkillReset(int version, string skill, string ability = null, int[] oldValues = null)
            {
                Version = version;
                Skill = skill;
                Ability = ability;
                OldValues = oldValues;
            }
        }

        /// <summary>
        /// Таблица миграций. Дополняется новыми записями при каждом релизе.
        /// </summary>
        private static readonly SkillReset[] Resets = new SkillReset[]
        {
            // Версия 10: treenursery - обновление стандартных значений
            // Сброс применяется только для старых значений (пользовательские изменения сохраняются)
            new SkillReset(10, "forestry", "treenursery", new int[] { 87, 74, 60 })
        };

        /// <summary>
        /// Определяет правила сброса для отдельной способности
        /// </summary>
        public class AbilityReset
        {
            /// <summary>
            /// Указывает на необходимость безусловного сброса без проверки предыдущих значений
            /// </summary>
            public bool Unconditional;

            /// <summary>
            /// Коллекция наборов значений. Если текущие значения совпадают с любым из наборов, выполняется сброс
            /// </summary>
            public List<int[]> MatchOldValues = new List<int[]>();
        }

        /// <summary>
        /// Представляет план сброса навыков и способностей для заданного диапазона версий
        /// </summary>
        public class SkillResetPlan
        {
            /// <summary>
            /// Набор навыков, подлежащих полному сбросу
            /// </summary>
            public HashSet<string> WholeSkills =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Словарь способностей, подлежащих частичному или условному сбросу, сгруппированных по навыкам
            /// </summary>
            public Dictionary<string, Dictionary<string, AbilityReset>> Abilities =
                new Dictionary<string, Dictionary<string, AbilityReset>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Собирает и возвращает план сбросов для версий, находящихся в диапазоне от <paramref name="fromVersion"/> до <paramref name="toVersion"/>
        /// </summary>
        /// <param name="fromVersion">Начальная версия (исключительно)</param>
        /// <param name="toVersion">Конечная версия (включительно)</param>
        /// <returns>План сбросов <see cref="SkillResetPlan"/></returns>
        public static SkillResetPlan CollectSkillResets(int fromVersion, int toVersion)
        {
            var plan = new SkillResetPlan();
            foreach (var r in Resets)
            {
                if (r.Version <= fromVersion || r.Version > toVersion) continue;

                if (string.IsNullOrEmpty(r.Ability))
                {
                    plan.WholeSkills.Add(r.Skill);
                    continue;
                }

                if (!plan.Abilities.TryGetValue(r.Skill, out var byAbility))
                {
                    byAbility = new Dictionary<string, AbilityReset>(StringComparer.OrdinalIgnoreCase);
                    plan.Abilities[r.Skill] = byAbility;
                }
                if (!byAbility.TryGetValue(r.Ability, out var ab))
                {
                    ab = new AbilityReset();
                    byAbility[r.Ability] = ab;
                }

                if (r.OldValues == null) ab.Unconditional = true;
                else ab.MatchOldValues.Add(r.OldValues);
            }
            return plan;
        }
    }
}