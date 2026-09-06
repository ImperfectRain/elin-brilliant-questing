using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// The eight things the game can be asked about what somebody is doing now.
    ///
    /// Named so a diagnostic can say which of them this build failed to answer without spelling
    /// the list out again at every call site, exactly as <see cref="IdentityFacetKind"/> does for
    /// who somebody is. The two lists never overlap, and neither read answers the other's
    /// questions.
    /// </summary>
    public enum ActivityFacetKind
    {
        Presence,
        Zone,
        TimeTable,
        Span,
        Activity,
        GlobalGoalEligibility,
        GlobalActivity,
        ZoneTransition
    }

    /// <summary>
    /// Whether this actor is somewhere the game is simulating moment to moment.
    ///
    /// Three states rather than a bool, because "this build did not say" and "they are elsewhere"
    /// are different answers and only one of them is a reason to conclude somebody is out of
    /// reach (`D017`).
    /// </summary>
    public enum PhysicalPresence
    {
        /// <summary>The build was not asked or could not answer. Never "elsewhere".</summary>
        Unknown,

        /// <summary>Physically in the zone the game is currently running.</summary>
        InActiveZone,

        /// <summary>Alive in the save and somewhere the game is not running moment to moment.</summary>
        OutsideActiveZone
    }

    /// <summary>
    /// The stretch of the day the actor's routine puts them in, as vanilla's own timetable divides
    /// it.
    ///
    /// A span is where the routine says they are in their day; <see cref="ActivityFamily"/> is
    /// what they are actually doing. The two disagree often and legitimately - somebody in a Work
    /// span who is being attacked is in combat - which is why both are read and neither is derived
    /// from the other.
    /// </summary>
    public enum ActivitySpan
    {
        Unknown,
        Sleep,
        Eat,
        Work,
        Free
    }

    /// <summary>
    /// What vanilla is having this actor do right now, projected onto the smallest set of families
    /// the S8 systems actually distinguish between.
    ///
    /// Semantic only. No Elin goal class reaches Core: the adapter maps whatever the live build
    /// calls its goals onto these, and a goal it does not recognise is <see cref="Other"/> - which
    /// is the game answering with something unfamiliar, and is emphatically not
    /// <see cref="Unknown"/>, which is the build answering nothing at all (`D001`, `D017`).
    /// </summary>
    public enum ActivityFamily
    {
        /// <summary>Nothing was read. Never idle, never available, never "not working".</summary>
        Unknown,

        Sleep,

        /// <summary>Vanilla's own work AI. A vanilla work goal is not a BQ motive.</summary>
        Work,

        Hobby,

        /// <summary>Bodily need - eating, drinking, sleeping it off. Vanilla's mechanism (`D021`).</summary>
        Needs,

        Combat,

        /// <summary>A specific local job in hand, as distinct from routine work.</summary>
        Task,

        /// <summary>The game answered that this actor is doing nothing in particular.</summary>
        Idle,

        /// <summary>The game answered with a goal this build has no family for. An answer, not a gap.</summary>
        Other
    }

    /// <summary>
    /// Whether vanilla's hourly off-screen mechanism runs this actor at all.
    ///
    /// Read on its own rather than folded into <see cref="GlobalActivityKind"/> because the two
    /// come from different places and a build can lose either alone: eligibility is a trait flag,
    /// the goal is a record on the actor. Which real traits report eligible is the single most
    /// load-bearing open runtime question behind this whole read (`ELIN-Q-0014`).
    /// </summary>
    public enum GlobalGoalEligibility
    {
        Unknown,
        Eligible,
        NotEligible
    }

    /// <summary>
    /// What vanilla's off-screen mechanism currently has this actor doing.
    ///
    /// <see cref="Travelling"/> is the one that matters: it is the seam's answer to "is Elin
    /// already moving this actor", which is what stops BQ scheduling a second journey on top of
    /// vanilla's (`VS 2.4`, `D021`). <see cref="Other"/> is a global goal this build does not
    /// recognise, and is deliberately not evidence that nobody is being moved - an unfamiliar
    /// goal may move somebody perfectly well.
    /// </summary>
    public enum GlobalActivityKind
    {
        /// <summary>Not read.</summary>
        Unknown,

        /// <summary>The game answered: this actor carries no global goal.</summary>
        None,

        /// <summary>The game answered with a goal that can move this actor between zones.</summary>
        Travelling,

        /// <summary>A global goal this build has no family for. An answer, and not a denial.</summary>
        Other
    }

    /// <summary>
    /// Whether the game is holding a zone move for this actor.
    ///
    /// Separate from <see cref="GlobalActivityKind"/> because a transition is state on the actor
    /// that outlives the goal that set it, and because a build can read one without the other.
    /// </summary>
    public enum ZoneTransitionState
    {
        Unknown,
        None,
        Pending
    }

    /// <summary>
    /// The seam's answer to "is vanilla itself moving this actor right now".
    ///
    /// Deliberately not a bool and deliberately fail-closed. <see cref="Unknown"/> is what a
    /// caller gets whenever the reads do not add up to certainty, and a caller that would schedule
    /// movement on the strength of a negative must treat unknown as a refusal: scheduling a
    /// journey for somebody Elin is already carrying is the failure this read exists to prevent.
    /// </summary>
    public enum VanillaMovement
    {
        Unknown,
        NotMoving,
        Moving
    }

    /// <summary>
    /// Elin's own answer to <em>what is this actor doing right now</em>, read through the seam and
    /// nothing more.
    ///
    /// The activity counterpart of <see cref="CharacterIdentity"/>, and the roadmap's actor
    /// activity snapshot (BQ-135). It exists so that BQ-093, BQ-095, BQ-097 and the rest of S8
    /// share one observation instead of each growing a private reflective probe into a `Chara`.
    ///
    /// Four rules hold it together, and each of them is an existing project rule rather than a new
    /// invention:
    ///
    /// <b>It is transient and it is never persisted.</b> Nothing here goes into the save chunk
    /// (`D004`, `D005`). A consumer may hold it for one pass - one opportunity check, one scheme
    /// tick, one scene - and asks again after a zone change, a save or a load. What survives is
    /// what BQ concluded, never a mirror of a `Chara`.
    ///
    /// <b>A facet the build did not answer is unknown</b>, never false, never zero and never idle
    /// (`D017`). A citizen whose span could not be read must not therefore read as awake, at work
    /// and available.
    ///
    /// <b>No Elin type crosses it.</b> Goal subclasses, timetable types, `Chara` and reflection
    /// handles stay inside the adapter; Core sees these enums (`D001`).
    ///
    /// <b>It says nothing about who anybody is.</b> Character archetype, race, work, hobby,
    /// service and institutional standing are <see cref="CharacterIdentity"/>'s single answer
    /// (BQ-144), what those imply is <see cref="World.IdentityAffordances"/>'s (BQ-145), and what
    /// the simulation wants of somebody is <see cref="World.NpcGoal"/>'s. An actor at a work goal
    /// is activity; the job they hold is identity; and the job is still theirs while they sleep.
    /// A vanilla work goal is not a BQ motive, and a BQ motive is not permission to overwrite the
    /// vanilla goal (`D021`).
    /// </summary>
    public sealed class ActorActivity
    {
        internal ActorActivity(
            EntityId actor,
            PhysicalPresence presence,
            EntityId currentZone,
            string timeTableId,
            bool timeTableKnown,
            ActivitySpan span,
            ActivityFamily activity,
            GlobalGoalEligibility usesGlobalGoal,
            GlobalActivityKind globalActivity,
            ZoneTransitionState pendingZoneTransition)
        {
            Actor = actor;
            Presence = presence;
            CurrentZone = currentZone;
            TimeTableId = timeTableId ?? string.Empty;
            TimeTableKnown = timeTableKnown && TimeTableId.Length > 0;
            CurrentSpan = span;
            CurrentActivity = activity;
            UsesGlobalGoal = usesGlobalGoal;
            GlobalActivity = globalActivity;
            PendingZoneTransition = pendingZoneTransition;
        }

        /// <summary>An observation about nobody in particular: every facet unread.</summary>
        public static ActorActivity UnknownFor(EntityId actor)
        {
            return new ActorActivityBuilder(actor).Build();
        }

        public EntityId Actor { get; }

        /// <summary>Whether the game is running this actor moment to moment.</summary>
        public PhysicalPresence Presence { get; }

        /// <summary>
        /// Where the game currently keeps this actor. <see cref="EntityId.None"/> means unknown,
        /// never "here" - the same rule <see cref="IVanillaState.GetZoneOf"/> follows, and read
        /// from the same place so the two cannot disagree.
        /// </summary>
        public EntityId CurrentZone { get; }

        /// <summary>
        /// The game's own timetable id, carried verbatim. Empty when unread, and meaningless then.
        ///
        /// An id and not a schedule: BQ does not own a timetable, cannot set one, and does not
        /// know what an unfamiliar id means. It is a stable discriminator - two actors on the same
        /// timetable keep the same hours - and nothing more.
        /// </summary>
        public string TimeTableId { get; }

        /// <summary>Whether the timetable facet was answered at all.</summary>
        public bool TimeTableKnown { get; }

        public ActivitySpan CurrentSpan { get; }

        /// <summary>What vanilla has this actor doing, as a family. Never a goal object.</summary>
        public ActivityFamily CurrentActivity { get; }

        public GlobalGoalEligibility UsesGlobalGoal { get; }

        public GlobalActivityKind GlobalActivity { get; }

        public ZoneTransitionState PendingZoneTransition { get; }

        /// <summary>
        /// Whether Elin is already moving this actor, as far as this build can tell.
        ///
        /// The one derived answer on the observation, and the reason the three global facets are
        /// read at all. It fails closed in both directions that matter: an unrecognised global
        /// goal never reports <see cref="VanillaMovement.NotMoving"/>, because an unfamiliar goal
        /// may move somebody perfectly well, and nothing short of the game actually saying so
        /// reports <see cref="VanillaMovement.Moving"/>.
        ///
        /// This is an observation about <em>vanilla's</em> travel and says nothing whatever about
        /// BQ's own. Whether BQ has sent somebody anywhere is
        /// <see cref="World.ActorAbsence"/>'s record and only ever will be; reading this
        /// registers no absence, and an actor Elin is carrying is not thereby a BQ absentee
        /// (`D020`, `D021`).
        /// </summary>
        public VanillaMovement VanillaMovementState()
        {
            if (GlobalActivity == GlobalActivityKind.Travelling
                || PendingZoneTransition == ZoneTransitionState.Pending)
            {
                return VanillaMovement.Moving;
            }

            // A transition nobody read leaves the question open however clear the goal is: the
            // pending move is the state that outlives the goal that set it.
            if (PendingZoneTransition != ZoneTransitionState.None)
            {
                return VanillaMovement.Unknown;
            }

            // Two independent routes to a settled no: the actor carries no global goal, or the
            // build says the hourly mechanism does not run them at all. An unrecognised goal is
            // neither, and stays unknown.
            if (GlobalActivity == GlobalActivityKind.None
                || UsesGlobalGoal == GlobalGoalEligibility.NotEligible)
            {
                return VanillaMovement.NotMoving;
            }

            return VanillaMovement.Unknown;
        }

        /// <summary>Whether this facet carries an answer. Unknown never grants anything.</summary>
        public bool IsKnown(ActivityFacetKind facet)
        {
            switch (facet)
            {
                case ActivityFacetKind.Presence:
                    return Presence != PhysicalPresence.Unknown;
                case ActivityFacetKind.Zone:
                    return !CurrentZone.IsNone;
                case ActivityFacetKind.TimeTable:
                    return TimeTableKnown;
                case ActivityFacetKind.Span:
                    return CurrentSpan != ActivitySpan.Unknown;
                case ActivityFacetKind.Activity:
                    return CurrentActivity != ActivityFamily.Unknown;
                case ActivityFacetKind.GlobalGoalEligibility:
                    return UsesGlobalGoal != GlobalGoalEligibility.Unknown;
                case ActivityFacetKind.GlobalActivity:
                    return GlobalActivity != GlobalActivityKind.Unknown;
                case ActivityFacetKind.ZoneTransition:
                    return PendingZoneTransition != ZoneTransitionState.Unknown;
                default:
                    return false;
            }
        }

        /// <summary>True when the build answered nothing at all about what this actor is doing.</summary>
        public bool IsFullyUnknown => UnreadFacets.Count == FacetKinds.Length;

        /// <summary>
        /// Which facets this build did not answer, in facet order. Naming them is the difference
        /// between "nobody in this town is travelling" and "this build cannot see travel".
        /// </summary>
        public IReadOnlyList<ActivityFacetKind> UnreadFacets
        {
            get
            {
                List<ActivityFacetKind> unread = new List<ActivityFacetKind>();
                for (int i = 0; i < FacetKinds.Length; i++)
                {
                    if (!IsKnown(FacetKinds[i]))
                    {
                        unread.Add(FacetKinds[i]);
                    }
                }

                return unread;
            }
        }

        /// <summary>How many facets there are. The diagnostic and its tests count against it.</summary>
        public static int FacetKindCount => FacetKinds.Length;

        internal static readonly ActivityFacetKind[] FacetKinds =
            (ActivityFacetKind[])Enum.GetValues(typeof(ActivityFacetKind));

        /// <summary>
        /// One line per actor, written so a live log distinguishes an answer from a silence.
        /// Formatted here rather than in the plugin so that the honesty of the line the adapter
        /// prints can be tested with no game attached.
        /// </summary>
        public string Describe()
        {
            List<string> parts = new List<string>
            {
                "presence " + Word(Presence != PhysicalPresence.Unknown, Presence.ToString()),
                "zone " + (CurrentZone.IsNone ? "?" : CurrentZone.ToString()),
                "timetable " + (TimeTableKnown ? TimeTableId : "?"),
                "span " + Word(CurrentSpan != ActivitySpan.Unknown, CurrentSpan.ToString()),
                "activity " + Word(CurrentActivity != ActivityFamily.Unknown, CurrentActivity.ToString()),
                "global-goal " + Word(UsesGlobalGoal != GlobalGoalEligibility.Unknown, UsesGlobalGoal.ToString()),
                "global-activity " + Word(GlobalActivity != GlobalActivityKind.Unknown, GlobalActivity.ToString()),
                "transition " + Word(PendingZoneTransition != ZoneTransitionState.Unknown, PendingZoneTransition.ToString()),
                "vanilla-moving " + VanillaMovementState()
            };

            return string.Join(", ", parts.ToArray());
        }

        private static string Word(bool known, string value) => known ? value : "?";

        public override string ToString() => Actor + ": " + Describe();
    }

    /// <summary>
    /// The one way an <see cref="ActorActivity"/> is put together.
    ///
    /// The live adapter and the headless reference implementation build observations through this,
    /// so "unread" means the same thing on both sides of the seam. A facet nobody set stays
    /// unknown, and there is no constructor that lets a caller assert a facet it did not read.
    /// </summary>
    public sealed class ActorActivityBuilder
    {
        private readonly EntityId _actor;
        private PhysicalPresence _presence = PhysicalPresence.Unknown;
        private EntityId _zone = EntityId.None;
        private string _timeTableId = string.Empty;
        private bool _timeTableKnown;
        private ActivitySpan _span = ActivitySpan.Unknown;
        private ActivityFamily _activity = ActivityFamily.Unknown;
        private GlobalGoalEligibility _usesGlobalGoal = GlobalGoalEligibility.Unknown;
        private GlobalActivityKind _globalActivity = GlobalActivityKind.Unknown;
        private ZoneTransitionState _transition = ZoneTransitionState.Unknown;

        public ActorActivityBuilder(EntityId actor)
        {
            _actor = actor;
        }

        public ActorActivityBuilder WithPresence(PhysicalPresence presence)
        {
            _presence = presence;
            return this;
        }

        /// <summary>
        /// Where the game says they are. <see cref="EntityId.None"/> leaves the facet unread
        /// rather than asserting nowhere, which is the same refusal
        /// <see cref="IVanillaState.GetZoneOf"/> makes.
        /// </summary>
        public ActorActivityBuilder WithZone(EntityId zone)
        {
            _zone = zone;
            return this;
        }

        /// <summary>
        /// Records the game's timetable id verbatim. An empty id is the build not answering, never
        /// a timetable called "".
        /// </summary>
        public ActorActivityBuilder WithTimeTable(string vanillaId)
        {
            if (string.IsNullOrEmpty(vanillaId))
            {
                return this;
            }

            _timeTableId = vanillaId;
            _timeTableKnown = true;
            return this;
        }

        public ActorActivityBuilder WithSpan(ActivitySpan span)
        {
            _span = span;
            return this;
        }

        public ActorActivityBuilder WithActivity(ActivityFamily activity)
        {
            _activity = activity;
            return this;
        }

        public ActorActivityBuilder WithGlobalGoalEligibility(GlobalGoalEligibility eligibility)
        {
            _usesGlobalGoal = eligibility;
            return this;
        }

        public ActorActivityBuilder WithGlobalActivity(GlobalActivityKind kind)
        {
            _globalActivity = kind;
            return this;
        }

        public ActorActivityBuilder WithZoneTransition(ZoneTransitionState state)
        {
            _transition = state;
            return this;
        }

        public ActorActivity Build()
        {
            return new ActorActivity(
                _actor,
                _presence,
                _zone,
                _timeTableId,
                _timeTableKnown,
                _span,
                _activity,
                _usesGlobalGoal,
                _globalActivity,
                _transition);
        }
    }
}
