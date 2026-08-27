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
