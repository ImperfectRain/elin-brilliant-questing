using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            Goals = new NpcGoalCollection(() => SimulationChanged?.Invoke(this));
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

        /// <summary>
        /// The actor this record turned out to be a second name for, or <see cref="EntityId.None"/>
        /// for everybody else - which is nearly everybody.
        ///
        /// One live Elin character has exactly one participating BQ identity. Where two records
        /// were nonetheless registered for one physical character - the intake minting a
        /// uid-derived id for somebody BQ had already staged under an authored one - the later
        /// record is retired onto the earlier rather than deleted: history already refers to it by
        /// id, and rewriting old events to say somebody else did them would be a worse lie than
        /// the duplicate was.
        ///
        /// A retired record keeps everything it had and stays resolvable, so a fact, belief,
        /// relationship, callback or thread that names it still reads. What it loses is
        /// participation: <see cref="EntityRegistry.Npcs"/> no longer lists it, so it cannot be
        /// cast, simulated or counted as a second person.
        /// </summary>
        private EntityId _aliasOf;
        public EntityId AliasOf { get => _aliasOf; set { _aliasOf = value; SimulationChanged?.Invoke(this); } }

        /// <summary>Whether this record participates as an actor in its own right.</summary>
        public bool IsCanonical => AliasOf.IsNone;

        public EntityId HomeSiteId { get; set; }

        private NarrativeImportance _importance;
        public NarrativeImportance Importance { get => _importance; set { _importance = value; SimulationChanged?.Invoke(this); } }

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

        public NpcGoalCollection Goals { get; }

        public List<EntityId> OrganizationIds { get; }

        private bool _alive;
        public bool Alive { get => _alive; set { _alive = value; SimulationChanged?.Invoke(this); } }

        internal event Action<NarrativeNpc> SimulationChanged;

        /// <summary>Derived budget, never a claim about vanilla simulation or physical presence.</summary>
        public SimulationTier BackgroundTier
        {
            get
            {
                if (!Alive || !IsCanonical) return SimulationTier.Archived;
                if (Importance >= NarrativeImportance.Known) return SimulationTier.Warm;
                foreach (NpcGoal goal in Goals)
                    if (goal != null && goal.IsActive && goal.Weight > 0) return SimulationTier.Warm;
                return SimulationTier.Cold;
            }
        }

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
        public NpcGoal(
            string kind,
            EntityId subject,
            int weight,
            string reason = "",
            GoalCondition condition = null,
            GoalOrigin origin = null)
        {
            Kind = kind;
            Subject = subject;
            Weight = weight;
            Reason = reason ?? string.Empty;
            Condition = condition;
            Origin = origin ?? UnknownOrigin;
            Identity = Kind + "|" + Subject.Value + "|" + (condition == null ? string.Empty : condition.Key);
        }

        private static readonly GoalOrigin UnknownOrigin = new GoalOrigin(GoalSourceKind.Unknown);

        /// <summary>Ontology term: "recover_property", "protect", "repay_debt", "avoid_exposure".</summary>
        public string Kind { get; }

        public EntityId Subject { get; }

        /// <summary>
        /// The world-state condition that would satisfy this, or null for a desire nothing can yet
        /// evaluate. Null is honest rather than empty: an old save's goals and hand-established
        /// fixture goals genuinely have no machine-readable condition, and inventing one for them
        /// would be guessing what the author meant.
        /// </summary>
        public GoalCondition Condition { get; }

        public bool HasCondition => Condition != null;

        /// <summary>Why this goal exists. Never <c>null</c>; unknown provenance says so explicitly.</summary>
        public GoalOrigin Origin { get; }

        /// <summary>
        /// What makes two goals the same want: the kind, what it is about, and the exact condition.
        ///
        /// Derived rather than minted, because the point is that the pass that reads the same
        /// pressure again lands on the goal it formed last time instead of stacking another one,
        /// and a freshly minted id would land nowhere. Stable across a reload for the same reason.
        /// </summary>
        public string Identity { get; }

        /// <summary>0..100. Higher wins when goals collide.</summary>
        private int _weight;
        public int Weight { get => _weight; set { _weight = value; Changed?.Invoke(); } }

        /// <summary>
        /// Inspector-facing trace for why this goal exists or last changed.
        ///
        /// For people. Nothing in the simulation reads it, and nothing may start: the machine-readable
        /// answers live in <see cref="Condition"/>, <see cref="Origin"/> and <see cref="Lifecycle"/>.
        /// </summary>
        public string Reason { get; set; }

        public GoalLifecycle Lifecycle { get; private set; } = GoalLifecycle.Active;

        public bool IsActive => Lifecycle == GoalLifecycle.Active;

        /// <summary>When it was retired, for a goal that has been. Meaningless while active.</summary>
        public GameTime RetiredAt { get; private set; }

        /// <summary>Why it was retired, as a code: "recovered", "gave_up", "crowded_out".</summary>
        public string RetirementCode { get; private set; } = string.Empty;

        /// <summary>The <see cref="Identity"/> of the goal that took this one's place, when superseded.</summary>
        public string SupersededBy { get; private set; } = string.Empty;

        /// <summary>
        /// What the owner believes about their own goal, which the world's answer never sets. An
        /// actor whose cargo was quietly recovered by somebody else still believes it is missing
        /// until they legitimately learn otherwise.
        /// </summary>
        private GoalAssessment _assessment;
        public GoalAssessment ActorAssessment { get => _assessment; set { _assessment = value; Changed?.Invoke(); } }

        /// <summary>
        /// The boolean view of the lifecycle that the rest of the simulation has always used.
        /// Lifecycle is the authority; this is a projection of it, and setting it is the short way
        /// to say <see cref="Satisfy"/> or to reopen a goal that turned out not to be done.
        /// </summary>
        public bool Satisfied
        {
            get => Lifecycle == GoalLifecycle.Satisfied;
            set
            {
                if (value) Satisfy(RetiredAt);
                else if (Lifecycle == GoalLifecycle.Satisfied) Reopen();
            }
        }

        internal event Action Changed;

        /// <summary>
        /// Reads the desired condition against authoritative state. A pure read: it reports what
        /// the world holds and never moves the goal's own lifecycle, because objective truth does
        /// not get to decide on the owner's behalf.
        /// </summary>
        public GoalConditionState Evaluate(NarrativeWorldState world) =>
            GoalConditionRegistry.Evaluate(world, Condition);

        /// <summary>The condition holds. Retires the goal without removing it from history.</summary>
        public void Satisfy(GameTime when, string code = "")
        {
            Retire(GoalLifecycle.Satisfied, when, code, string.Empty);
        }

        /// <summary>Given up on. The condition did not hold, and the want stops anyway.</summary>
        public void Abandon(GameTime when, string code = "")
        {
            Retire(GoalLifecycle.Abandoned, when, code, string.Empty);
        }

        /// <summary>
        /// Handed over to another goal. The replacement is named by identity rather than by
        /// reference so the link survives a save, and so a retired goal never keeps a live object
        /// alive behind it.
        /// </summary>
        public void SupersedeWith(NpcGoal replacement, GameTime when, string code = "")
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (ReferenceEquals(replacement, this))
            {
                throw new ArgumentException("A goal cannot supersede itself.", nameof(replacement));
            }

            Retire(GoalLifecycle.Superseded, when, code, replacement.Identity);
        }

        /// <summary>
        /// Back to wanted, clearing the retirement that no longer applies. The history a retirement
        /// belongs to is the event ledger's; this field is the goal's current state, not a log.
        /// </summary>
        public void Reopen()
        {
            if (Lifecycle == GoalLifecycle.Active)
            {
                return;
            }

            Lifecycle = GoalLifecycle.Active;
            RetiredAt = default;
            RetirementCode = string.Empty;
            SupersededBy = string.Empty;
            Changed?.Invoke();
        }

        /// <summary>Puts back exactly what a save recorded, with no policy of its own.</summary>
        public void Restore(
            GoalLifecycle lifecycle,
            GameTime retiredAt,
            string retirementCode,
            string supersededBy,
            GoalAssessment assessment)
        {
            Lifecycle = lifecycle;
            RetiredAt = lifecycle == GoalLifecycle.Active ? default : retiredAt;
            RetirementCode = lifecycle == GoalLifecycle.Active ? string.Empty : GoalCodes.Normalize(retirementCode);
            SupersededBy = lifecycle == GoalLifecycle.Superseded ? supersededBy ?? string.Empty : string.Empty;
            _assessment = assessment;
            Changed?.Invoke();
        }

        private void Retire(GoalLifecycle lifecycle, GameTime when, string code, string supersededBy)
        {
            Lifecycle = lifecycle;
            RetiredAt = when;
            RetirementCode = GoalCodes.Normalize(code);
            SupersededBy = supersededBy;
            Changed?.Invoke();
        }

        public override string ToString()
        {
            string text = Kind + "(" + Subject + ") w" + Weight;
            if (Condition != null)
            {
                text += " wanting " + Condition.Key;
            }

            if (Origin.Kind != GoalSourceKind.Unknown)
            {
                text += " from " + Origin;
            }

            if (!string.IsNullOrEmpty(Reason))
            {
                text += " because " + Reason;
            }

            return text + (Lifecycle == GoalLifecycle.Active ? string.Empty : " [" + Lower(Lifecycle) + "]");
        }

        private static string Lower(GoalLifecycle lifecycle) => lifecycle.ToString().ToLowerInvariant();
    }

    /// <summary>Notifies the derived scheduling index when the existing goal authority changes.</summary>
    public sealed class NpcGoalCollection : Collection<NpcGoal>
    {
        /// <summary>
        /// How many wants one person may be pursuing at once.
        ///
        /// Automatic goal formation runs every pass forever, so the bound is the difference between
        /// a character and a list that grows with playtime. Chosen to be larger than any hand-built
        /// situation gives anybody, so establishing a scenario never trips it.
        /// </summary>
        public const int MaxActive = 16;

        /// <summary>
        /// How much retired history one person keeps. Retirement is not deletion, but "not deletion"
        /// cannot mean "forever" in a save a player carries for fifty hours.
        /// </summary>
        public const int MaxRetained = 16;

        private readonly Action _changed;
        internal NpcGoalCollection(Action changed) { _changed = changed; }

        /// <summary>The active goal that is the same want as this identity, or null.</summary>
        public NpcGoal FindActive(string identity)
        {
            if (string.IsNullOrEmpty(identity))
            {
                return null;
            }

            for (int i = 0; i < Count; i++)
            {
                NpcGoal goal = this[i];
                if (goal != null && goal.IsActive && string.Equals(goal.Identity, identity, StringComparison.Ordinal))
                {
                    return goal;
                }
            }

            return null;
        }

        /// <summary>
        /// Takes on a want, or finds it already taken on.
        ///
        /// The entry point automatic goal formation uses, and the reason a pressure read twice does
        /// not become two goals: the existing active goal for the same identity is returned
        /// untouched, so the caller decides what to update on it rather than this deciding for them.
        /// A hand-established scenario still uses <see cref="Collection{T}.Add"/>, which is the raw
        /// path and stays raw - fixtures author exactly what they mean.
        /// </summary>
        public NpcGoal Adopt(NpcGoal goal)
        {
            if (goal == null)
            {
                throw new ArgumentNullException(nameof(goal));
            }

            NpcGoal existing = FindActive(goal.Identity);
            if (existing != null)
            {
                return existing;
            }

            Add(goal);
            CrowdOutWeakestActive(goal);
            PruneRetired();
            return goal;
        }

        /// <summary>
        /// Retires the least-wanted active goal once somebody holds more than they can pursue.
        /// The newcomer is not exempt: an actor already full of heavier concerns does not drop one
        /// of them for a trivial new one.
        /// </summary>
        private void CrowdOutWeakestActive(NpcGoal added)
        {
            while (true)
            {
                int active = 0;
                NpcGoal weakest = null;
                for (int i = 0; i < Count; i++)
                {
                    NpcGoal goal = this[i];
                    if (goal == null || !goal.IsActive) continue;
                    active++;
                    if (weakest == null || goal.Weight < weakest.Weight) weakest = goal;
                }

                if (active <= MaxActive || weakest == null)
                {
                    return;
                }

                weakest.Abandon(added.Origin.FormedAt, "crowded_out");
            }
        }

        /// <summary>
        /// Drops the oldest retired goals past the cap. Oldest by when they ended, so a goal that
        /// was given up years ago goes before one settled last week.
        /// </summary>
        private void PruneRetired()
        {
            while (true)
            {
                int retired = 0;
                int oldest = -1;
                for (int i = 0; i < Count; i++)
                {
                    NpcGoal goal = this[i];
                    if (goal == null || goal.IsActive) continue;
                    retired++;
                    if (oldest < 0 || goal.RetiredAt < this[oldest].RetiredAt) oldest = i;
                }

                if (retired <= MaxRetained || oldest < 0)
                {
                    return;
                }

                RemoveAt(oldest);
            }
        }
        protected override void InsertItem(int index, NpcGoal item)
        {
            base.InsertItem(index, item);
            if (item != null) item.Changed += _changed;
            _changed();
        }
        protected override void RemoveItem(int index)
        {
            if (this[index] != null) this[index].Changed -= _changed;
            base.RemoveItem(index);
            _changed();
        }
        protected override void SetItem(int index, NpcGoal item)
        {
            if (this[index] != null) this[index].Changed -= _changed;
            base.SetItem(index, item);
            if (item != null) item.Changed += _changed;
            _changed();
        }
        protected override void ClearItems()
        {
            foreach (NpcGoal item in this) if (item != null) item.Changed -= _changed;
            base.ClearItems();
            _changed();
        }
    }
}
