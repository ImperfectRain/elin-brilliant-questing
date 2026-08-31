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
            ProblemSolving = new ProblemSolvingProfile();
            Sensitivities = new SensitivityProfile();
            Contradiction = new ContradictionProfile();
            Goals = new List<NpcGoal>();
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

        /// <summary>What they do for a living. Description, not permission.</summary>
        public string Occupation { get; set; }

        /// <summary>
        /// Standing this character holds - who may take a crime report, who speaks for a guild.
        ///
        /// Separate from <see cref="Occupation"/> on purpose. Authority was briefly stored there,
        /// which conflated two dimensions that are not the same: a brewer can be a guild officer,
        /// and a guard who stops being one is still a person with a job. Overloading the field
        /// also made the answer sticky, because there was nowhere to record that somebody no
        /// longer holds a role without erasing what they do.
        ///
        /// Strings rather than an enum because the adapter, situations and eventually
        /// organizations all name roles, and Core should not have to enumerate every source.
        /// </summary>
        public HashSet<string> Roles { get; } = new HashSet<string>();

        public EntityId HomeSiteId { get; set; }

        public NarrativeImportance Importance { get; set; } = NarrativeImportance.Background;

        public PersonalityWeights Personality { get; }

        public ProblemSolvingProfile ProblemSolving { get; }

        public SensitivityProfile Sensitivities { get; }

        public ContradictionProfile Contradiction { get; }

        public List<NpcGoal> Goals { get; }

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
    ///
    /// Named NpcGoal rather than Goal because Elin defines a global Goal, and the shipped plugin
    /// compiles this source into an assembly that references it.
    /// </summary>
    public sealed class NpcGoal
    {
        public NpcGoal(string kind, EntityId subject, int weight)
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
