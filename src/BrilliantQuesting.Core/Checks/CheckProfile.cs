using System;
using System.Collections.Generic;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Maps a semantic action ("lie to this guard") onto real Elin values.
    ///
    /// A profile is data, not code: it names the kind of uncertainty it represents, which of the
    /// actor's skills and attributes reduce the difficulty, and which of the target's resist it.
    ///
    /// A vanilla `SourceCheck` row is single-element - one actor element, one target element, a
    /// level modifier - and most of these are deliberately composite, so a profile is not a row
    /// waiting to become one. Shipped rows earn their keep through `Check.GetText`, giving the
    /// player vanilla's own difficulty wording over our arithmetic. Composition and the roll stay
    /// here; see <see cref="ICheckResolver"/> for why the roll in particular does.
    /// </summary>
    public sealed class CheckProfile
    {
        public CheckProfile(string id, CheckFamily family, int baseDifficulty)
        {
            if (family == CheckFamily.Certain)
            {
                throw new ArgumentException(
                    "Check profile '" + id + "' cannot be Certain: a profile is a roll, and an attempt with no "
                    + "uncertainty in it has no profile at all.",
                    nameof(family));
            }

            Id = id;
            Family = family;
            BaseDifficulty = baseDifficulty;
            ActorSkills = new List<WeightedSkill>();
            ActorAttributes = new List<WeightedAttribute>();
            TargetAttributes = new List<WeightedAttribute>();
            Dice = 20;
            CritRange = 1;
            FumbleRange = 1;
        }

        public string Id { get; }

        /// <summary>
        /// The kind of uncertainty this check is, declared rather than inferred (BQa-003).
        /// </summary>
        public CheckFamily Family { get; }

        /// <summary>Difficulty before anybody's stats are considered. Roughly a d20 target.</summary>
        public int BaseDifficulty { get; }

        /// <summary>Skills that make the attempt easier, each scaled by its weight.</summary>
        public List<WeightedSkill> ActorSkills { get; }

        /// <summary>Attributes that make the attempt easier. Usually the skill's parent.</summary>
        public List<WeightedAttribute> ActorAttributes { get; }

        /// <summary>Target attributes that resist. Perception spots the lie, Will resists coercion.</summary>
        public List<WeightedAttribute> TargetAttributes { get; }

        /// <summary>Whether a higher-level target is inherently harder, as vanilla GetDC does.</summary>
        public double TargetLevelWeight { get; private set; }

        /// <summary>
        /// Whether this profile actually names somebody on the other side.
        ///
        /// An <see cref="CheckFamily.Opposed"/> profile that declares no opposition is a row that
        /// has lost the thing that made it opposed; the classification test rejects it rather than
        /// letting it resolve as a fixed challenge under an opposed label.
        /// </summary>
        public bool DeclaresOpposition => TargetAttributes.Count > 0 || TargetLevelWeight != 0.0;

        /// <summary>
        /// Whether the target's level is part of the opposition on this profile (BQa-003).
        ///
        /// Only an opposed profile can carry a level term at all, so this is that declaration read
        /// back rather than a second switch. A fixed challenge does not get harder because the
        /// actor levelled, and nothing may infer a universal level term from its absence here.
        /// </summary>
        public bool TargetLevelIsOpposition => Family == CheckFamily.Opposed && TargetLevelWeight != 0.0;

        /// <summary>
        /// Faces on the die. Vanilla's SourceCheck row carries this per row rather than assuming
        /// d20, so a profile has to as well or the two resolvers drift apart.
        /// </summary>
        public int Dice { get; private set; }

        /// <summary>How many of the top faces are a critical pass. 1 means only a natural 20.</summary>
        public int CritRange { get; private set; }

        /// <summary>How many of the bottom faces are a critical fail. 1 means only a natural 1.</summary>
        public int FumbleRange { get; private set; }

        public CheckProfile WithActorSkill(VanillaSkill skill, double weight = 1.0)
        {
            ActorSkills.Add(new WeightedSkill(skill, weight));
            return this;
        }

        public CheckProfile WithActorAttribute(VanillaAttribute attribute, double weight = 0.5)
        {
            ActorAttributes.Add(new WeightedAttribute(attribute, weight));
            return this;
        }

        public CheckProfile WithTargetAttribute(VanillaAttribute attribute, double weight = 0.5)
        {
            RequireOpposed("a resisting target attribute");
            TargetAttributes.Add(new WeightedAttribute(attribute, weight));
            return this;
        }

        /// <summary>Overrides the die and its critical windows, mirroring a SourceCheck row.</summary>
        public CheckProfile WithDice(int dice, int critRange = 1, int fumbleRange = 1)
        {
            Dice = dice < 2 ? 2 : dice;
            CritRange = critRange < 0 ? 0 : critRange;
            FumbleRange = fumbleRange < 0 ? 0 : fumbleRange;
            return this;
        }

        public CheckProfile WithTargetLevel(double weight = 0.5)
        {
            RequireOpposed("a target level term");
            TargetLevelWeight = weight;
            return this;
        }

        /// <summary>
        /// Refuses opposition on a profile that declared itself a fixed challenge.
        ///
        /// The point of declaring the family is lost if a row can then quietly acquire the other
        /// family's terms: a lock that resists by Will is either miscategorised or not a lock, and
        /// both of those are worth a build failure rather than a silent difficulty.
        /// </summary>
        private void RequireOpposed(string term)
        {
            if (Family != CheckFamily.Opposed)
            {
                throw new InvalidOperationException(
                    "Check profile '" + Id + "' is classified " + Family + " and cannot declare " + term
                    + "; classify it as Opposed or drop the term.");
            }
        }

        public readonly struct WeightedSkill
        {
            public WeightedSkill(VanillaSkill skill, double weight)
            {
                Skill = skill;
                Weight = weight;
            }

            public VanillaSkill Skill { get; }

            public double Weight { get; }
        }

        public readonly struct WeightedAttribute
        {
            public WeightedAttribute(VanillaAttribute attribute, double weight)
            {
                Attribute = attribute;
                Weight = weight;
            }

            public VanillaAttribute Attribute { get; }

            public double Weight { get; }
        }
    }
}
