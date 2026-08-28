using System.Collections.Generic;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Maps a semantic action ("lie to this guard") onto real Elin values.
    ///
    /// A profile is data, not code: it names which of the actor's skills and attributes reduce
    /// the difficulty and which of the target's resist it. This is the piece intended to migrate
    /// into a native Check source sheet row once the runtime spike confirms the format - the
    /// resolver below deliberately mirrors vanilla's arithmetic so that migration is a swap, not
    /// a rewrite.
    /// </summary>
    public sealed class CheckProfile
    {
        public CheckProfile(string id, int baseDifficulty)
        {
            Id = id;
            BaseDifficulty = baseDifficulty;
            ActorSkills = new List<WeightedSkill>();
            ActorAttributes = new List<WeightedAttribute>();
            TargetAttributes = new List<WeightedAttribute>();
            Dice = 20;
            CritRange = 1;
            FumbleRange = 1;
        }

        public string Id { get; }

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
            TargetLevelWeight = weight;
            return this;
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
