using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using System.Text.RegularExpressions;

namespace XLib.XLeveling
{
    /// <summary>
    /// Represents a specific trait that a player must have to learn an ability.
    /// </summary>
    /// <seealso cref="Requirement" />
    public class TraitRequirement : Requirement
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public override string Name => "trait";

        /// <summary>
        /// Gets or sets the traits. If the player has at least one of them, the requirement is fulfilled.
        /// </summary>
        /// <value>
        /// The traits.
        /// </value>
        public List<string> Traits { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraitRequirement"/> class.
        /// </summary>
        public TraitRequirement() : base()
        {
            this.Traits = new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraitRequirement" /> class.
        /// </summary>
        /// <param name="traits">The traits.</param>
        /// <param name="minimumTier">The minimum tier this requirement is required for.</param>
        /// <param name="hideAbilityUntilFulfilled">if set to <c>true</c> the ability is hidden until this requirement is fulfilled.</param>
        /// <exception cref="ArgumentNullException">Is thrown if traits is <c>null</c>.</exception>
        public TraitRequirement(string[] traits, int minimumTier = 1, bool hideAbilityUntilFulfilled = false) : base()
        {
            if (traits == null) throw new ArgumentNullException("The traits of a trait requirement must not be null.");
            this.Traits = new List<string>();

            foreach (string str in traits)
            {
                if (str != null) this.Traits.Add(str);
            }

            this.HideAbilityUntilFulfilled = hideAbilityUntilFulfilled;
            this.MinimumTier = minimumTier;
        }

        /// <summary>
        /// Creates a requirement from a tree.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="toResolve">XLeveling object for resolving.</param>
        /// <returns>
        ///   <c>true</c> if the resolving was successful, the requirement is only added to an ability if this method was successful; otherwise, <c>false</c>.
        /// </returns>
        public override bool FromTree(TreeAttribute tree, XLeveling toResolve)
        {
            base.FromTree(tree, toResolve);
            string[] traits = tree.GetStringArray("traits");
            if (traits != null)
            {
                foreach (string str in traits)
                {
                    if (str != null) this.Traits.Add(str);
                }
            }
            else
            {
                string singleTrait = tree.GetString("trait");
                if (singleTrait == null) return false;
                this.Traits.Add(singleTrait);
            }
            return true;
        }

        /// <summary>
        /// Determines whether the specified player ability fulfills the requirement.
        /// </summary>
        /// <param name="playerAbility">The player ability.</param>
        /// <param name="tier">The tier this requirement is checked for.</param>
        /// <returns>
        ///   <c>true</c> if the specified player ability fulfills the requirement; otherwise, <c>false</c>.
        /// </returns>
        public override bool IsFulfilled(PlayerAbility playerAbility, int tier)
        {
            if (tier < this.MinimumTier) return true;

            var player = playerAbility?.PlayerSkill?.PlayerSkillSet?.Player?.Entity;
            if (player == null) return false;

            string[] vanillaTraits = player.WatchedAttributes.GetStringArray("traits");
            string[] extraTraits = player.WatchedAttributes.GetStringArray("extraTraits");

            foreach (string requiredTrait in this.Traits)
            {
                if (vanillaTraits != null && vanillaTraits.Contains(requiredTrait)) return true;
                if (extraTraits != null && extraTraits.Contains(requiredTrait)) return true;
            }

            return false;
        }

        /// <summary>
        /// This function is called when the requirement is not fulfilled after all skills are loaded and should resolve this conflict.
        /// </summary>
        /// <param name="playerAbility">The player ability.</param>
        /// <returns>
        ///   false, if this conflict has been ignored; true, if the conflict has been resolved.
        /// </returns>
        public override bool ResolveConflict(PlayerAbility playerAbility)
        {
            if (playerAbility != null)
            {
                playerAbility.Tier = this.MinimumTier - 1;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Describes the requirement for the given player ability.
        /// </summary>
        /// <param name="playerAbility">The player ability.</param>
        /// <returns>
        /// a Description that describes the requirement for the given player ability.
        /// </returns>
        public override string ShortDescription(PlayerAbility playerAbility)
        {
            PlayerSkillSet playerSkillSet = playerAbility?.PlayerSkill?.PlayerSkillSet;
            if (playerSkillSet == null) return "";

            List<string> localizedTraits = new List<string>();
            foreach (string t in this.Traits)
            {
                string rawText = Lang.Get("trait-" + t);

                string cleanText = System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", string.Empty);

                cleanText = cleanText.Replace("•", "").Trim();

                localizedTraits.Add(cleanText);
            }

            return Lang.Get("xleveling:requirement-trait", string.Join(", ", localizedTraits));
        }

        /// <summary>
        /// The Type of the requirement.
        /// </summary>
        /// <returns>
        ///   the Type of the requirement.
        /// </returns>
        public override EnumRequirementType RequirementType()
        {
            return EnumRequirementType.MediumRequirement;
        }
    }
}