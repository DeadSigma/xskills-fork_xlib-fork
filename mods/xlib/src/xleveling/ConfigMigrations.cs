using System;
using System.Collections.Generic;

namespace XLib.XLeveling
{
    /// <summary>
    /// Provides declarative configuration migration mechanisms between versions.
    /// </summary>
    public static class ConfigMigrations
    {
        /// <summary>
        /// Current configuration version.
        /// Increase this value in releases that require applying new migrations.
        /// </summary>
        public const int CurrentVersion = 11;

        /// <summary>
        /// Defines reset parameters for a skill or ability during a version update.
        /// </summary>
        public struct SkillReset
        {
            /// <summary>
            /// Version from which this reset rule is applied.
            /// </summary>
            public int Version;

            /// <summary>
            /// Skill identifier (matches the file name, for example, skill.json).
            /// </summary>
            public string Skill;

            /// <summary>
            /// Ability (perk) identifier. If null or empty, the entire skill file is reset.
            /// </summary>
            public string Ability;

            /// <summary>
            /// Previous default ability values.
            /// If specified, reset is applied only for users whose values match these values.
            /// </summary>
            public int[] OldValues;

            /// <summary>
            /// Initializes a new instance of the <see cref="SkillReset"/> struct.
            /// </summary>
            /// <param name="version">Version in which the reset is applied.</param>
            /// <param name="skill">Skill name.</param>
            /// <param name="ability">Ability name (null to reset the entire skill).</param>
            /// <param name="oldValues">Array of old values to match (null for unconditional reset).</param>
            public SkillReset(int version, string skill, string ability = null, int[] oldValues = null)
            {
                Version = version;
                Skill = skill;
                Ability = ability;
                OldValues = oldValues;
            }
        }

        /// <summary>
        /// Migration table. Extended with new entries in each release.
        /// </summary>
        private static readonly SkillReset[] Resets = new SkillReset[]
        {
            // Version 10: treenursery - default values update
            // Reset applies only to old values (user changes are preserved)
            new SkillReset(10, "forestry", "treenursery", new int[] { 87, 74, 60 })
        };

        /// <summary>
        /// Defines reset rules for a single ability.
        /// </summary>
        public class AbilityReset
        {
            /// <summary>
            /// Indicates that an unconditional reset is required without checking previous values.
            /// </summary>
            public bool Unconditional;

            /// <summary>
            /// Collection of value sets. If current values match any set, a reset is performed.
            /// </summary>
            public List<int[]> MatchOldValues = new List<int[]>();
        }

        /// <summary>
        /// Represents a skill and ability reset plan for a specified version range.
        /// </summary>
        public class SkillResetPlan
        {
            /// <summary>
            /// Set of skills that require a full reset.
            /// </summary>
            public HashSet<string> WholeSkills =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Dictionary of abilities requiring partial or conditional reset, grouped by skill.
            /// </summary>
            public Dictionary<string, Dictionary<string, AbilityReset>> Abilities =
                new Dictionary<string, Dictionary<string, AbilityReset>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds and returns a reset plan for versions in the range from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.
        /// </summary>
        /// <param name="fromVersion">Starting version (exclusive).</param>
        /// <param name="toVersion">Target version (inclusive).</param>
        /// <returns>Reset plan <see cref="SkillResetPlan"/>.</returns>
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