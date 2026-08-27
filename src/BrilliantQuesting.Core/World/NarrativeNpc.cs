using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// How much simulation budget a character earns. Promotion is emergent: a shopkeeper becomes
    /// Important because the player kept dealing with her, not because a generator decided in
    /// advance that she mattered.
    /// </summary>
    public enum NarrativeImportance
    {
        Background = 0,
        Known = 1,
        Recurring = 2,
        Important = 3,
        Major = 4
    }

    /// <summary>
    /// The procedural half of a character. The vanilla Chara remains the mechanical truth - stats,
    /// affinity, inventory, whether they are alive - and this holds the causal half: what they
    /// want, who they are tied to, what they have seen.
    /// </summary>
    public sealed class NarrativeNpc
    {
        public NarrativeNpc(EntityId id, string name)
        {
            Id = id;
            Name = name;
            Personality = new PersonalityWeights();
            Goals = new List<Goal>();
            OrganizationIds = new List<EntityId>();
            Alive = true;
        }

        public EntityId Id { get; }

        /// <summary>Display name only. Never an identity - see EntityId.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Handle for the live Elin Chara, filled in by the adapter when the character is spawned.
        /// Empty while the NPC exists only in the database, which is a normal state: history
        /// outlives instances.
        /// </summary>
        public string VanillaCharaRef { get; set; }

        public string Occupation { get; set; }

        public EntityId HomeSiteId { get; set; }

        public NarrativeImportance Importance { get; set; } = NarrativeImportance.Background;

        public PersonalityWeights Personality { get; }

        public List<Goal> Goals { get; }

        public List<EntityId> OrganizationIds { get; }

        public bool Alive { get; set; }

        public GameTime LastSimulatedAt { get; set; }

        /// <summary>
        /// Bumps importance when the player keeps touching this character. One-way: a character
        /// who mattered once stays cheap to remember and worth reusing.
        /// </summary>
        public void Promote(NarrativeImportance to)
        {
            if (to > Importance)
            {
                Importance = to;
            }
        }

        public override string ToString() => Name + " [" + Id + ", " + Importance + "]";
    }

    /// <summary>
    /// Something a character is trying to achieve. Weighted so that conflicting goals produce
    /// actual dilemmas - a merchant who wants his cargo back but wants his daughter safe more.
    /// </summary>
    public sealed class Goal
    {
        public Goal(string kind, EntityId subject, int weight)
        {
            Kind = kind;
            Subject = subject;
            Weight = weight;
        }

        /// <summary>Ontology term: "recover_property", "protect", "repay_debt", "avoid_exposure".</summary>
        public string Kind { get; }

        public EntityId Subject { get; }

        /// <summary>0..100. Higher wins when goals collide.</summary>
        public int Weight { get; set; }

        public bool Satisfied { get; set; }

        public override string ToString() => Kind + "(" + Subject + ") w" + Weight + (Satisfied ? " [done]" : string.Empty);
    }
}
