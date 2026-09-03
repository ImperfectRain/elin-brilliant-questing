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
            Quirk = new CharacterQuirkProfile();
            NegativeSpace = new NegativeSpaceProfile();
            Values = new ValueProfile();
            Needs = new NarrativeNeedProfile();
            Emotions = new EmotionalStateProfile();
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

        /// <summary>
        /// What they do for a living, where BQ itself authored it. Description, not permission.
        ///
        /// Not the intake for a live character any more. What the *game* says somebody does is
        /// read at the seam as <see cref="Integration.CharacterIdentity"/>, is never persisted,
        /// and is asked for again rather than mirrored here: this field once held the literal
        /// string "local" for every townsperson in the save, which was a claim BQ invented because
        /// it had nowhere to put "we did not ask". A saved "local" is dropped on load.
        ///
        /// What remains is what a situation or an organization authored - a staged miller is a
        /// miller because this simulation made her one - and that is BQ's own state, so it stays
        /// here and stays saved.
        /// </summary>
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
        ///
        /// For a live character this is derived, once, from the institutional facet of the
        /// identity observation and re-read on every attach
        /// (<see cref="Actions.Library.AuthorityPolicy.Reconcile"/>); the saved value is not
        /// authoritative and an unread facet withdraws nothing.
        /// </summary>
        public HashSet<string> Roles { get; } = new HashSet<string>();

        public EntityId HomeSiteId { get; set; }

        public NarrativeImportance Importance { get; set; } = NarrativeImportance.Background;

        public PersonalityWeights Personality { get; }

        public ProblemSolvingProfile ProblemSolving { get; }

        public SensitivityProfile Sensitivities { get; }

        public ContradictionProfile Contradiction { get; }

        public CharacterQuirkProfile Quirk { get; }

        /// <summary>
        /// The lines this character holds against moves this simulation makes (BQ-077). Durable
        /// personality in the same sense <see cref="Contradiction"/> and <see cref="Quirk"/> are:
        /// declared onto the character, saved with them, and never derived from what they are.
        /// Empty for nearly everybody, which is the point - negative space is recognizable because
        /// it is rare.
        /// </summary>
        public NegativeSpaceProfile NegativeSpace { get; }

        public ValueProfile Values { get; }

        public NarrativeNeedProfile Needs { get; }

        public EmotionalStateProfile Emotions { get; }

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
        public NpcGoal(string kind, EntityId subject, int weight, string reason = "")
        {
            Kind = kind;
            Subject = subject;
            Weight = weight;
            Reason = reason ?? string.Empty;
        }

        /// <summary>Ontology term: "recover_property", "protect", "repay_debt", "avoid_exposure".</summary>
        public string Kind { get; }

        public EntityId Subject { get; }

        /// <summary>0..100. Higher wins when goals collide.</summary>
        public int Weight { get; set; }

        /// <summary>Inspector-facing trace for why this goal exists or last changed.</summary>
        public string Reason { get; set; }

        public bool Satisfied { get; set; }

        public override string ToString()
        {
            string text = Kind + "(" + Subject + ") w" + Weight;
            if (!string.IsNullOrEmpty(Reason))
            {
                text += " because " + Reason;
            }

            return text + (Satisfied ? " [done]" : string.Empty);
        }
    }
}
