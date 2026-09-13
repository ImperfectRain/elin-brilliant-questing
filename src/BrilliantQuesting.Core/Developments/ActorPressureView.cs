using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// How certain this actor is of the thing they are under pressure about - their certainty, not
    /// the world's. A sincerely mistaken believer can be <see cref="Believed"/> about something
    /// false, and that is the point rather than a defect.
    /// </summary>
    public enum ActorPressureCertainty
    {
        /// <summary>Held weakly. Worth wondering about; not worth acting on as settled.</summary>
        Suspected,

        /// <summary>Held firmly enough to act on, with nothing that would demonstrate it.</summary>
        Believed,

        /// <summary>Held with proof links this actor actually has.</summary>
        Proven
    }

    /// <summary>
    /// Why this actor is in a pressure rather than merely near it. A stake is what they stand to
    /// lose or answer for; it is never a route to learning what the pressure is about.
    /// </summary>
    public enum ActorStakeKind
    {
        /// <summary>The matter is about them.</summary>
        Personal,

        /// <summary>They run the service the matter is about.</summary>
        Property,

        /// <summary>They are debtor or creditor in the debt the matter is.</summary>
        Obligation,

        /// <summary>They hold a tie to somebody the matter implicates.</summary>
        Relationship,

        /// <summary>A body they belong to is carrying it.</summary>
        Organization,

        /// <summary>They hold an office that is answerable for this kind of matter.</summary>
        Office,

        /// <summary>It is happening where they live.</summary>
        Local
    }

    /// <summary>One reason this actor is in the matter, with the authority that says so.</summary>
    public sealed class ActorStake
    {
        internal ActorStake(ActorStakeKind kind, EntityId subjectId, string explanation, int weight)
        {
            Kind = kind;
            SubjectId = subjectId;
            Explanation = explanation ?? string.Empty;
            Weight = weight;
        }

        public ActorStakeKind Kind { get; }

        /// <summary>Whom or what the stake is in, where the stake names one. None otherwise.</summary>
        public EntityId SubjectId { get; }

        /// <summary>Plain words for an inspector. Never the hidden content of the pressure.</summary>
        public string Explanation { get; }

        /// <summary>How much this stake adds to the actor's own urgency, 0..100 before clamping.</summary>
        public int Weight { get; }

        public override string ToString() => Kind + "(" + (SubjectId.IsNone ? "-" : SubjectId.Value) + ") +" + Weight;
    }

    /// <summary>
    /// One pressure as a single actor can legitimately experience it.
    ///
    /// Not a filtered <see cref="Development"/>. A development is a reading of the world; this is a
    /// reading of one person's position in it, and the two come apart in both directions: a true
    /// condition nobody has a route to produces no local pressure at all, and a claim that is false
    /// produces a real one for whoever sincerely holds it.
    ///
    /// Derived and never saved, exactly like the development it may or may not correspond to.
    /// </summary>
    public sealed class ActorLocalPressure
    {
        internal ActorLocalPressure(
            EntityId actorId,
            string id,
            string developmentId,
            IReadOnlyList<string> pressureTags,
            EntityId focusFactId,
            IReadOnlyList<EntityId> subjectIds,
            IReadOnlyList<EntityId> siteIds,
            IReadOnlyList<ActorStake> stakes,
            ActorPressureCertainty certainty,
            double confidence,
            bool disputed,
            bool sincerelyMistaken,
            IReadOnlyList<string> unknown,
            int urgency,
            IReadOnlyList<string> terms)
        {
            ActorId = actorId;
            Id = id ?? string.Empty;
            DevelopmentId = developmentId ?? string.Empty;
            PressureTags = pressureTags ?? Empty<string>.List;
            FocusFactId = focusFactId;
            SubjectIds = subjectIds ?? Empty<EntityId>.List;
            SiteIds = siteIds ?? Empty<EntityId>.List;
            Stakes = stakes ?? Empty<ActorStake>.List;
            Certainty = certainty;
            Confidence = confidence;
            Disputed = disputed;
            SincerelyMistaken = sincerelyMistaken;
            Unknown = unknown ?? Empty<string>.List;
            Urgency = urgency < 0 ? 0 : urgency > 100 ? 100 : urgency;
            Terms = terms ?? Empty<string>.List;
        }

        public EntityId ActorId { get; }

        /// <summary>
        /// Stable key for this actor's reading. Derived from the condition, so the same position in
        /// the same world reads the same way every time, including after a reload.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The objective pressure this corresponds to, or empty where the actor's own belief is the
        /// whole of it. Empty is not a gap: it is the case the false-belief path exists for.
        /// </summary>
        public string DevelopmentId { get; }

        /// <summary>Whether the world is objectively holding this too.</summary>
        public bool HasObjectiveCause => DevelopmentId.Length > 0;

        public IReadOnlyList<string> PressureTags { get; }

        /// <summary>The claim this actor is acting on - theirs, which may not be the true one.</summary>
        public EntityId FocusFactId { get; }

        /// <summary>Who this actor can legitimately place in the matter, in stable id order.</summary>
        public IReadOnlyList<EntityId> SubjectIds { get; }

        public IReadOnlyList<EntityId> SiteIds { get; }

        public IReadOnlyList<ActorStake> Stakes { get; }

        public ActorPressureCertainty Certainty { get; }

        /// <summary>0..1, from the belief record where there is one.</summary>
        public double Confidence { get; }

        /// <summary>This actor holds rival versions of the same matter. Their dispute, not the world's.</summary>
        public bool Disputed { get; }

        /// <summary>
        /// The claim driving this is untrue. A reading for the actor's use must never consult it -
        /// it is here so that a test, an inspector and a later consequence can tell an honest
        /// mistake from a lie without the actor being told which one they are in.
        /// </summary>
        public bool SincerelyMistaken { get; }

        /// <summary>
        /// Which parts of the matter this actor has no route to, as generic labels. Labels only:
        /// naming the missing content here would be the one thing this class exists to prevent.
        /// </summary>
        public IReadOnlyList<string> Unknown { get; }

        /// <summary>0..100, from this actor's point of view rather than the world's.</summary>
        public int Urgency { get; }

        /// <summary>What went into <see cref="Urgency"/>, for the inspector.</summary>
        public IReadOnlyList<string> Terms { get; }

        public bool HasPressure(string tag)
        {
            for (int i = 0; i < PressureTags.Count; i++)
            {
                if (string.Equals(PressureTags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasStake(ActorStakeKind kind)
        {
            for (int i = 0; i < Stakes.Count; i++)
            {
                if (Stakes[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return Id + " [urgency " + Urgency + ", " + Certainty + (SincerelyMistaken ? ", mistaken]" : "]");
        }
    }

    internal static class Empty<T>
    {
        internal static readonly IReadOnlyList<T> List = new T[0];
    }

    /// <summary>
    /// The trouble a held claim would be, if it were true.
    ///
    /// Deliberately a table over the fact vocabulary rather than a re-reading of the detector's
    /// rules: those rules answer "what is the world holding", and asking them here would drag their
    /// truth tests in with them - and the whole reason a reader needs this is that the thing they
    /// hold may be false.
    ///
    /// Shared by every reader that projects a claim nothing objective covers: one person's sincere
    /// error (BQa-007) and one body's standing report (BQa-018) are the same question asked from
    /// two places, and two copies of this table would eventually disagree about what a killing is.
    /// </summary>
    internal sealed class ClaimConcern
    {
        private ClaimConcern(string tag, int weight, bool isWrong)
        {
            Tag = tag;
            Weight = weight;
            IsWrong = isWrong;
        }

        internal string Tag { get; }

        internal int Weight { get; }

        /// <summary>Something somebody did, which somebody else can have put right.</summary>
        internal bool IsWrong { get; }

        internal static ClaimConcern Of(string predicate)
        {
            switch (predicate)
            {
                case FactPredicates.Killed: return Wrong(DevelopmentPressures.UnresolvedCrime, 90);
                case FactPredicates.Extorted: return Wrong(DevelopmentPressures.UnresolvedCrime, 70);
                case FactPredicates.Stole: return Wrong(DevelopmentPressures.UnresolvedCrime, 60);
                case FactPredicates.Forged: return Wrong(DevelopmentPressures.UnresolvedCrime, 50);
                case FactPredicates.MayBeSabotaged: return Wrong(DevelopmentPressures.UnresolvedCrime, 35);
                case FactPredicates.AtRisk: return Condition(DevelopmentPressures.DamagedProperty, 45);
                case FactPredicates.Damaged: return Condition(DevelopmentPressures.DamagedProperty, 40);
                case FactPredicates.Needs: return Condition(DevelopmentPressures.Shortage, 40);
                case FactPredicates.IsContaminated: return Condition(DevelopmentPressures.DamagedProperty, 30);
                case FactPredicates.HasSoilTrouble: return Condition(DevelopmentPressures.DamagedProperty, 30);
                default: return null;
            }
        }

        private static ClaimConcern Wrong(string tag, int weight) => new ClaimConcern(tag, weight, true);

        private static ClaimConcern Condition(string tag, int weight) => new ClaimConcern(tag, weight, false);
    }

    /// <summary>
    /// Step 7a: what one person can legitimately be under pressure about.
    ///
    /// <see cref="DevelopmentDetector"/> reads the world as it stands, and it reads it
    /// omnisciently, because that is what an authoritative reading of authoritative state is. Goal
    /// formation cannot consume that. An actor who forms a goal from the detector's output knows
    /// every secret in the save, and a world whose people act on what the save knows is not a
    /// living world however good its pressures are.
    ///
    /// So this projects a development onto a person, and it is deliberately a *narrowing followed
    /// by a widening*:
    ///
    /// <b>Narrowing - route before stake.</b> An actor reaches a pressure only through evidence
    /// legitimately theirs: they hold the claim, or they are party to the record the pressure is
    /// read from (their shop's continuity, their debt, their body's own goal), or the condition is
    /// openly visible where they live. Nothing else is a route. In particular a stake is not: being
    /// somebody's brother, caring about the law and having everything to lose are reasons to act on
    /// what you know and never reasons to know it. That ordering is the whole guarantee, and it is
    /// why stakes are computed strictly after the route gate has already let the reading through.
    ///
    /// <b>Widening - belief without a development.</b> The detector filters claims to
    /// <see cref="TruthState.True"/>, so a sincerely mistaken person is invisible to it by
    /// construction and no amount of filtering its output can recover them. Their beliefs are
    /// therefore enumerated directly, and a claim they hold that names a trouble is a real local
    /// pressure whether or not the world agrees. <see cref="ActorLocalPressure.SincerelyMistaken"/>
    /// records the disagreement for whoever is entitled to see it; the actor is never told.
    ///
    /// A pure reading, like the detector: nothing here teaches, writes, or appends. Running it
    /// twice returns the same answer and running it never leaves the world identical.
    /// </summary>
    public static class ActorPressureView
    {
        private static readonly IReadOnlyList<ActorLocalPressure> Nothing = new ActorLocalPressure[0];

        private static readonly IReadOnlyList<Development> NoDevelopments = new Development[0];

        /// <summary>Label for a claim the actor has no route to. Generic on purpose.</summary>
        private const string UnknownFocus = "focus claim";

        /// <summary>Label for who is in it, when the actor cannot demonstrate that.</summary>
        private const string UnknownParty = "implicated party";

        /// <summary>
        /// Everything this actor can legitimately be under pressure about, in stable id order.
        ///
        /// <paramref name="objective"/> is what the world is holding - normally
        /// <see cref="DevelopmentDetector.Detect(NarrativeWorldState, DevelopmentScope)"/>'s output
        /// for whatever work set the caller bounded. Passing nothing is meaningful and is not the
        /// same as passing an empty world: the belief path still runs, because what somebody
        /// wrongly believes does not depend on what the detector was asked to look at.
        /// </summary>
        public static IReadOnlyList<ActorLocalPressure> Of(
            NarrativeWorldState world,
            EntityId actorId,
            IReadOnlyList<Development> objective,
            IVanillaState vanilla = null)
        {
            if (world == null)
            {
                return Nothing;
            }

            NarrativeNpc actor = world.Registry.GetNpc(actorId);
            if (actor == null)
            {
                return Nothing;
            }

            Standing standing = new Standing(world, actor, vanilla);
            List<ActorLocalPressure> local = new List<ActorLocalPressure>();
            List<EntityId> covered = new List<EntityId>();

            IReadOnlyList<Development> pressures = objective ?? NoDevelopments;
            for (int i = 0; i < pressures.Count; i++)
            {
                Development development = pressures[i];
                if (development == null)
                {
                    continue;
                }

                ActorLocalPressure reading = Project(world, standing, development);
                if (reading == null)
                {
                    continue;
                }

                local.Add(reading);
                if (!development.FocusFactId.IsNone && !covered.Contains(development.FocusFactId))
                {
                    covered.Add(development.FocusFactId);
                }
            }

            IReadOnlyList<KnowledgeRecord> beliefs = standing.Beliefs;
            for (int i = 0; i < beliefs.Count; i++)
            {
                KnowledgeRecord belief = beliefs[i];
                if (covered.Contains(belief.FactId))
                {
                    // The world is already holding this one and the actor got to it above. Emitting
                    // their belief again would double the same worry under a second key.
                    continue;
                }

                ActorLocalPressure reading = ProjectBelief(world, standing, belief);
                if (reading != null)
                {
                    local.Add(reading);
                }
            }

            local.Sort(ById);
            return local;
        }

        private static int ById(ActorLocalPressure left, ActorLocalPressure right)
        {
            return string.CompareOrdinal(left.Id, right.Id);
        }

        // -- objective pressure, seen from where this actor stands -----------------------------

        /// <summary>
        /// This actor's reading of one development, or null where they have no legitimate route to
        /// it at all.
        ///
        /// Null rather than a zero-urgency reading on purpose. "You are not in this" and "you are
        /// in this and do not care" are different answers, and a consumer handed the second for the
        /// first would eventually let an actor act on a matter they have never heard of.
        /// </summary>
        private static ActorLocalPressure Project(NarrativeWorldState world, Standing standing, Development development)
        {
            Terms terms = new Terms();
            KnowledgeRecord belief = null;

            Fact focus = development.FocusFactId.IsNone ? null : world.Knowledge.GetFact(development.FocusFactId);
            if (focus != null)
            {
                world.Knowledge.TryGetBelief(standing.ActorId, focus.Id, out belief);
            }

            if (belief != null)
            {
                terms.Route("holds the claim");
            }
            else if (standing.OperatesServiceIn(development))
            {
                terms.Route("runs the service");
            }
            else if (standing.PartyToDebtIn(development))
            {
                terms.Route("party to the debt");
            }
            else if (standing.BelongsToBodyCarrying(development))
            {
                terms.Route("belongs to the body carrying it");
            }
            else if (standing.SeesItWhereTheyLive(world, development, focus))
            {
                terms.Route("openly visible where they live");
            }
            else
            {
                // No legitimate route. The world may well be holding this; it is not this person's
                // to be under pressure about, and inventing a faint reading here is exactly how a
                // relationship or a sensitivity would quietly become a way of knowing things.
                return null;
            }

            ActorPressureCertainty certainty;
            double confidence;
            if (belief == null)
            {
                // Party to the record, or looking at it. They are not wrong about it and they hold
                // no claim they could be asked to demonstrate.
                certainty = ActorPressureCertainty.Believed;
                confidence = 1.0;
            }
            else
            {
                confidence = belief.Confidence;
                certainty = belief.CanProve
                    ? ActorPressureCertainty.Proven
                    : confidence >= 0.5 ? ActorPressureCertainty.Believed : ActorPressureCertainty.Suspected;
            }

            List<string> unknown = new List<string>();
            if (!development.FocusFactId.IsNone && belief == null)
            {
                unknown.Add(UnknownFocus);
            }

            List<EntityId> subjects = standing.PlaceableSubjects(development.SubjectIds, certainty);
            if (NamedOthers(subjects, standing.ActorId) < NamedOthers(development.SubjectIds, standing.ActorId))
            {
                unknown.Add(UnknownParty);
            }

            List<ActorStake> stakes = standing.StakesIn(world, development, subjects);
            int urgency = Weigh(standing, terms, development.Urgency, confidence, development.PressureTags, stakes);

            return new ActorLocalPressure(
                standing.ActorId,
                "local:" + development.Id,
                development.Id,
                development.PressureTags,
                belief == null ? development.FocusFactId : belief.FactId,
                subjects,
                development.SiteIds,
                stakes,
                certainty,
                confidence,
                standing.HoldsRivalVersionOf(world, development.FocusFactId),
                focus != null && focus.IsUntrue,
                unknown,
                urgency,
                terms.All);
        }

        // -- belief the world does not agree with, or has not been asked about ------------------

        /// <summary>
        /// A claim this actor holds that names a trouble, where no development covered it.
        ///
        /// This is the required false-belief path and it has to enumerate rather than filter. The
        /// detector's knowledge rules test <c>Truth != TruthState.True</c> and return; there is no
        /// output to narrow, so a sincere error can only be reached by reading what the actor
        /// believes and asking what it would mean if it were so.
        ///
        /// It is not only for errors. A true claim the detector was not asked about - outside a
        /// bounded work set, or settled as a matter while this actor still holds the wrong of it -
        /// is a real thing for them to be under pressure about, and saying so costs nothing that
        /// truth-checking it would not cost more.
        /// </summary>
        private static ActorLocalPressure ProjectBelief(NarrativeWorldState world, Standing standing, KnowledgeRecord belief)
        {
            Fact claim = world.Knowledge.GetFact(belief.FactId);
            if (claim == null)
            {
                return null;
            }

            ClaimConcern concern = ConcernOf(claim.Predicate);
            if (concern == null)
            {
                return null;
            }

            if (concern.IsWrong && standing.BelievesItWasPutRight(world, claim))
            {
                // They hold the wrong and they hold the settlement of it. Nothing presses.
                return null;
            }

            Terms terms = new Terms();
            terms.Route("holds the claim");

            double confidence = belief.Confidence;
            ActorPressureCertainty certainty = belief.CanProve
                ? ActorPressureCertainty.Proven
                : confidence >= 0.5 ? ActorPressureCertainty.Believed : ActorPressureCertainty.Suspected;

            List<string> tags = new List<string> { concern.Tag };
            bool disputed = standing.HoldsRivalVersionOf(world, claim.Id);
            if (disputed)
            {
                tags.Add(DevelopmentPressures.EvidenceConflict);
            }

            List<EntityId> named = new List<EntityId>();
            Include(named, claim.Subject, world);
            Include(named, claim.Object, world);
            named.Sort();

            List<EntityId> subjects = standing.PlaceableSubjects(named, certainty);
            List<string> unknown = new List<string>();
            if (NamedOthers(subjects, standing.ActorId) < NamedOthers(named, standing.ActorId))
            {
                unknown.Add(UnknownParty);
            }

            List<EntityId> sites = new List<EntityId>();
            if (world.Registry.GetSite(claim.Subject) != null)
            {
                sites.Add(claim.Subject);
            }

            List<ActorStake> stakes = standing.StakesInClaim(world, claim, tags, subjects, sites);
            int urgency = Weigh(standing, terms, concern.Weight, confidence, tags, stakes);

            return new ActorLocalPressure(
                standing.ActorId,
                "local.belief:" + claim.Id.Value,
                string.Empty,
                tags,
                claim.Id,
                subjects,
                sites,
                stakes,
                certainty,
                confidence,
                disputed,
                claim.IsUntrue,
                unknown,
                urgency,
                terms.All);
        }

        // -- urgency ---------------------------------------------------------------------------

        /// <summary>
        /// How hard this presses on this person.
        ///
        /// The objective reading is the ceiling of the condition, scaled by how sure they are of it
        /// and raised by what they stand to lose. Values and sensitivities move it because two
        /// people who know exactly the same thing do not feel it equally - but they move it only
        /// after a route has already been established, so caring about the law never becomes a way
        /// of hearing about a crime.
        /// </summary>
        private static int Weigh(
            Standing standing,
            Terms terms,
            int objectiveUrgency,
            double confidence,
            IReadOnlyList<string> tags,
            List<ActorStake> stakes)
        {
            double certainty = 0.4 + (0.6 * Clamp01(confidence));
            double urgency = objectiveUrgency * certainty;
            terms.Add("condition " + objectiveUrgency + " at certainty", urgency);

            for (int i = 0; i < stakes.Count; i++)
            {
                terms.Add("stake " + stakes[i].Kind.ToString().ToLowerInvariant(), stakes[i].Weight);
                urgency += stakes[i].Weight;
            }

            NarrativeNpc actor = standing.Actor;
            for (int i = 0; i < tags.Count; i++)
            {
                switch (tags[i])
                {
                    case DevelopmentPressures.UnresolvedCrime:
                        terms.Add("values law", actor.Values.Law.Importance * 12.0);
                        urgency += actor.Values.Law.Importance * 12.0;
                        terms.Add("sensitive to theft", actor.Sensitivities.Theft * 8.0);
                        urgency += actor.Sensitivities.Theft * 8.0;
                        break;
                    case DevelopmentPressures.UnprovenKnowledge:
                    case DevelopmentPressures.EvidenceConflict:
                        terms.Add("sensitive to dishonesty", actor.Sensitivities.Dishonesty * 10.0);
                        urgency += actor.Sensitivities.Dishonesty * 10.0;
                        break;
                    case DevelopmentPressures.Shortage:
                        terms.Add("needs material relief", actor.Needs.MaterialShortage * 12.0);
                        urgency += actor.Needs.MaterialShortage * 12.0;
                        break;
                    case DevelopmentPressures.UnmetObligation:
                        terms.Add("sensitive to unpaid debt", actor.Sensitivities.UnpaidDebt * 10.0);
                        urgency += actor.Sensitivities.UnpaidDebt * 10.0;
                        break;
                    case DevelopmentPressures.DamagedProperty:
                        terms.Add("values wealth", actor.Values.Wealth.Importance * 8.0);
                        urgency += actor.Values.Wealth.Importance * 8.0;
                        break;
                    case DevelopmentPressures.Opportunity:
                    case DevelopmentPressures.Recovering:
                        // Modifiers on a condition that is easing or is something to gain by. They
                        // do not add: a chance is not an emergency.
                        break;
                }
            }

            return urgency < 0 ? 0 : urgency > 100 ? 100 : (int)Math.Round(urgency, MidpointRounding.AwayFromZero);
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }

        // -- what a predicate would mean, if it were so ------------------------------------------

        /// <summary>The trouble a held claim would be, if it were true. See <see cref="ClaimConcern"/>.</summary>
        private static ClaimConcern ConcernOf(string predicate) => ClaimConcern.Of(predicate);

        /// <summary>
        /// How many people other than this actor a list names.
        ///
        /// Counted on both sides of the placement question rather than comparing a placed list's
        /// length against it: the actor places themselves for free, so a raw length would report a
        /// matter as fully placed the moment they are in it, and quietly hide the one name they
        /// cannot actually put to it.
        /// </summary>
        private static int NamedOthers(IReadOnlyList<EntityId> ids, EntityId actorId)
        {
            int count = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!ids[i].IsNone && ids[i] != actorId)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Include(List<EntityId> into, EntityId id, NarrativeWorldState world)
        {
            if (!id.IsNone && world.Registry.GetNpc(id) != null && !into.Contains(id))
            {
                into.Add(id);
            }
        }

        private sealed class Terms
        {
            private readonly List<string> _terms = new List<string>();

            internal IReadOnlyList<string> All => _terms;

            internal void Route(string how)
            {
                _terms.Add("route: " + how);
            }

            internal void Add(string name, double value)
            {
                if (value != 0.0)
                {
                    _terms.Add(name + " " + value.ToString("0.00"));
                }
            }
        }

        // -- where this actor stands --------------------------------------------------------------

        /// <summary>
        /// Everything about one actor's position that more than one reading needs, gathered once.
        ///
        /// Built per call and thrown away: it is a cache of reads, never a store. Gathering it up
        /// front is also what keeps the route tests honest - each one is a lookup in a record this
        /// actor is a named party to, rather than a scan of the world with the actor's name in it.
        /// </summary>
        private sealed class Standing
        {
            private readonly List<EntityId> _operatedBusinesses = new List<EntityId>();
            private readonly List<EntityId> _operatedPlaces = new List<EntityId>();
            private readonly List<string> _openDebtPressureIds = new List<string>();
            private readonly List<KnowledgeRecord> _beliefs = new List<KnowledgeRecord>();
            private readonly List<EntityId> _believedFactIds = new List<EntityId>();
            private readonly IdentityAffordances _identity;

            internal Standing(NarrativeWorldState world, NarrativeNpc actor, IVanillaState vanilla)
            {
                Actor = actor;
                ActorId = actor.Id;
                _identity = IdentityAffordances.Of(actor, vanilla);

                foreach (BusinessRecord record in world.Businesses.Records)
                {
                    if (record.OperatorId == ActorId
                        || record.ReplacementOperatorId == ActorId
                        || record.InheritedById == ActorId)
                    {
                        _operatedBusinesses.Add(record.BusinessId);
                        if (!record.PlaceId.IsNone && !_operatedPlaces.Contains(record.PlaceId))
                        {
                            _operatedPlaces.Add(record.PlaceId);
                        }
                    }
                }

                IReadOnlyList<SocialObligation> debts = world.Obligations.Records;
                for (int i = 0; i < debts.Count; i++)
                {
                    SocialObligation debt = debts[i];
                    if (debt.IsOpen && (debt.Debtor == ActorId || debt.Creditor == ActorId))
                    {
                        _openDebtPressureIds.Add("dev.unmet_obligation:" + debt.Id.Value);
                    }
                }

                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(ActorId))
                {
                    _beliefs.Add(belief);
                    _believedFactIds.Add(belief.FactId);
                }

                // The graph stores beliefs per knower in a dictionary, whose enumeration order is
                // not a contract. Sorting is what makes a second identical call identical.
                _beliefs.Sort(ByFactId);
                _believedFactIds.Sort();
            }

            internal NarrativeNpc Actor { get; }

            internal EntityId ActorId { get; }

            internal IReadOnlyList<KnowledgeRecord> Beliefs => _beliefs;

            private static int ByFactId(KnowledgeRecord left, KnowledgeRecord right)
            {
                return left.FactId.CompareTo(right.FactId);
            }

            /// <summary>
            /// They run the service the pressure is about. The business ledger records the operator,
            /// the replacement and the inheritor, so this is a party-to-the-record route rather than
            /// an inference from being nearby.
            /// </summary>
            internal bool OperatesServiceIn(Development development)
            {
                if (_operatedBusinesses.Count == 0
                    || !(development.HasPressure(DevelopmentPressures.ServiceInterruption)
                         || development.HasPressure(DevelopmentPressures.Recovering)))
                {
                    return false;
                }

                for (int i = 0; i < _operatedBusinesses.Count; i++)
                {
                    if (string.Equals(
                        development.Id,
                        "dev.business_continuity:" + _operatedBusinesses[i].Value,
                        StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>Debtor or creditor in the debt this pressure is. Nobody forgets what they owe.</summary>
            internal bool PartyToDebtIn(Development development)
            {
                for (int i = 0; i < _openDebtPressureIds.Count; i++)
                {
                    if (string.Equals(development.Id, _openDebtPressureIds[i], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// A body they belong to is carrying the goal. Membership is a legitimate route to that
            /// body's own stated aims and to nothing else - which is why this is checked against the
            /// organization stake tag rather than against every pressure a guild is named in.
            /// </summary>
            internal bool BelongsToBodyCarrying(Development development)
            {
                if (!development.HasPressure(DevelopmentPressures.OrganizationStake))
                {
                    return false;
                }

                IReadOnlyList<EntityId> subjects = development.SubjectIds;
                for (int i = 0; i < subjects.Count; i++)
                {
                    if (Actor.OrganizationIds.Contains(subjects[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// The condition is plainly there, where they live or work.
            ///
            /// Narrow on purpose, in two directions at once. Only the families that are conditions
            /// of a place rather than of a person - a town short of grain, a shuttered counter, a
            /// broken wheel - and only where the claim behind it is not secret. A crime, a secret
            /// somebody cannot prove and a contradiction in the record are all invisible from the
            /// street, and standing in the street is not a way to learn them.
            /// </summary>
            internal bool SeesItWhereTheyLive(NarrativeWorldState world, Development development, Fact focus)
            {
                if (!(development.HasPressure(DevelopmentPressures.Shortage)
                      || development.HasPressure(DevelopmentPressures.ServiceInterruption)
                      || development.HasPressure(DevelopmentPressures.Recovering)
                      || development.HasPressure(DevelopmentPressures.DamagedProperty)))
                {
                    return false;
                }

                if (focus != null && focus.Secrecy > 0)
                {
                    return false;
                }

                IReadOnlyList<EntityId> sites = development.SiteIds;
                for (int i = 0; i < sites.Count; i++)
                {
                    if (sites[i] == Actor.HomeSiteId || _operatedPlaces.Contains(sites[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Which of the people the pressure names this actor can actually place in it.
            ///
            /// Everyone, once they can demonstrate the claim: proof is what makes "who did this"
            /// answerable rather than alleged. Short of proof they can place themselves and whoever
            /// else they already hold a claim about, and the rest stays <see cref="UnknownParty"/> -
            /// because handing over the detector's list of names is handing over the answer.
            /// </summary>
            internal List<EntityId> PlaceableSubjects(IReadOnlyList<EntityId> named, ActorPressureCertainty certainty)
            {
                List<EntityId> placeable = new List<EntityId>();
                for (int i = 0; i < named.Count; i++)
                {
                    EntityId id = named[i];
                    if (id.IsNone || placeable.Contains(id))
                    {
                        continue;
                    }

                    if (certainty == ActorPressureCertainty.Proven
                        || id == ActorId
                        || Actor.OrganizationIds.Contains(id))
                    {
                        placeable.Add(id);
                    }
                }

                placeable.Sort();
                return placeable;
            }

            /// <summary>
            /// This actor holds two versions of one matter. Read within their own beliefs rather
            /// than off the graph, because a contradiction nobody has noticed is the world's problem
            /// and a contradiction they are holding both halves of is theirs.
            /// </summary>
            internal bool HoldsRivalVersionOf(NarrativeWorldState world, EntityId factId)
            {
                if (factId.IsNone)
                {
                    return false;
                }

                Fact held = world.Knowledge.GetFact(factId);
                if (held == null)
                {
                    return false;
                }

                for (int i = 0; i < _believedFactIds.Count; i++)
                {
                    EntityId otherId = _believedFactIds[i];
                    if (otherId == factId)
                    {
                        continue;
                    }

                    Fact other = world.Knowledge.GetFact(otherId);
                    if (other == null)
                    {
                        continue;
                    }

                    if (other.IsVersionOf(factId) || held.IsVersionOf(otherId))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// They hold a settlement of this wrong. Read off their own beliefs, so somebody who has
            /// not heard that restitution was made goes on being under pressure about it - which is
            /// the correct answer and the reason this is not asked of the thread.
            /// </summary>
            internal bool BelievesItWasPutRight(NarrativeWorldState world, Fact wrong)
            {
                for (int i = 0; i < _beliefs.Count; i++)
                {
                    Fact claim = world.Knowledge.GetFact(_beliefs[i].FactId);
                    if (claim != null
                        && string.Equals(claim.Predicate, FactPredicates.Settled, StringComparison.Ordinal)
                        && claim.Truth == TruthState.True
                        && claim.Object == wrong.Subject)
                    {
                        return true;
                    }
                }

                return false;
            }

            internal List<ActorStake> StakesIn(
                NarrativeWorldState world,
                Development development,
                List<EntityId> placeable)
            {
                List<ActorStake> stakes = new List<ActorStake>();
                AddPersonal(stakes, development.SubjectIds);
                AddProperty(stakes, development);
                AddDebt(stakes, world, development);
                AddOrganization(stakes, development.SubjectIds);
                AddOffice(stakes, development.PressureTags);
                AddRelationship(stakes, world, placeable);
                AddLocal(stakes, development.SiteIds);
                return stakes;
            }

            internal List<ActorStake> StakesInClaim(
                NarrativeWorldState world,
                Fact claim,
                IReadOnlyList<string> tags,
                List<EntityId> placeable,
                List<EntityId> sites)
            {
                List<ActorStake> stakes = new List<ActorStake>();
                if (claim.Subject == ActorId || claim.Object == ActorId)
                {
                    stakes.Add(new ActorStake(ActorStakeKind.Personal, ActorId, "the claim is about them", 25));
                }

                AddOffice(stakes, tags);
                AddRelationship(stakes, world, placeable);
                AddLocal(stakes, sites);
                return stakes;
            }

            private void AddPersonal(List<ActorStake> stakes, IReadOnlyList<EntityId> subjects)
            {
                for (int i = 0; i < subjects.Count; i++)
                {
                    if (subjects[i] == ActorId)
                    {
                        stakes.Add(new ActorStake(ActorStakeKind.Personal, ActorId, "the matter names them", 25));
                        return;
                    }
                }
            }

            private void AddProperty(List<ActorStake> stakes, Development development)
            {
                if (OperatesServiceIn(development))
                {
                    stakes.Add(new ActorStake(ActorStakeKind.Property, ActorId, "they run the service", 20));
                }
            }

            private void AddDebt(List<ActorStake> stakes, NarrativeWorldState world, Development development)
            {
                if (!PartyToDebtIn(development))
                {
                    return;
                }

                for (int i = 0; i < _openDebtPressureIds.Count; i++)
                {
                    if (!string.Equals(development.Id, _openDebtPressureIds[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SocialObligation debt = FindDebt(world, development.Id);
                    if (debt == null)
                    {
                        return;
                    }

                    stakes.Add(new ActorStake(
                        ActorStakeKind.Obligation,
                        debt.Debtor == ActorId ? debt.Creditor : debt.Debtor,
                        debt.Debtor == ActorId ? "they owe it" : "it is owed to them",
                        15));
                    return;
                }
            }

            private static SocialObligation FindDebt(NarrativeWorldState world, string developmentId)
            {
                IReadOnlyList<SocialObligation> debts = world.Obligations.Records;
                for (int i = 0; i < debts.Count; i++)
                {
                    if (string.Equals(
                        developmentId,
                        "dev.unmet_obligation:" + debts[i].Id.Value,
                        StringComparison.Ordinal))
                    {
                        return debts[i];
                    }
                }

                return null;
            }

            private void AddOrganization(List<ActorStake> stakes, IReadOnlyList<EntityId> subjects)
            {
                for (int i = 0; i < subjects.Count; i++)
                {
                    if (Actor.OrganizationIds.Contains(subjects[i]))
                    {
                        stakes.Add(new ActorStake(
                            ActorStakeKind.Organization,
                            subjects[i],
                            "a body they belong to is in it",
                            10));
                        return;
                    }
                }
            }

            /// <summary>
            /// Answerable for this kind of matter by office. Eligibility is
            /// <see cref="IdentityAffordances"/>' answer rather than a second reading of roles and
            /// occupation here: the question "is this person a watch officer" already has an owner.
            /// </summary>
            private void AddOffice(List<ActorStake> stakes, IReadOnlyList<string> tags)
            {
                if (!_identity.IsEligibleFor(IdentityRole.Authority))
                {
                    return;
                }

                for (int i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i], DevelopmentPressures.UnresolvedCrime, StringComparison.Ordinal))
                    {
                        stakes.Add(new ActorStake(
                            ActorStakeKind.Office,
                            ActorId,
                            _identity.ExplainEligibility(IdentityRole.Authority),
                            15));
                        return;
                    }
                }
            }

            /// <summary>
            /// A tie to somebody they can already place in the matter.
            ///
            /// Only to somebody already placed, which is the whole discipline of this class in one
            /// line: a relationship raises what a pressure costs you and never tells you who is in
            /// it. Feeding the full subject list in here would make being someone's brother a way
            /// of learning that they are suspected.
            /// </summary>
            private void AddRelationship(List<ActorStake> stakes, NarrativeWorldState world, List<EntityId> placeable)
            {
                for (int i = 0; i < placeable.Count; i++)
                {
                    if (placeable[i] == ActorId)
                    {
                        continue;
                    }

                    RelationshipEdge tie = world.Relationships.Find(ActorId, placeable[i]);
                    if (tie == null)
                    {
                        continue;
                    }

                    stakes.Add(new ActorStake(
                        ActorStakeKind.Relationship,
                        placeable[i],
                        "tied to them as " + tie.Kind.ToString().ToLowerInvariant(),
                        8));
                    return;
                }
            }

            private void AddLocal(List<ActorStake> stakes, IReadOnlyList<EntityId> sites)
            {
                for (int i = 0; i < sites.Count; i++)
                {
                    if (sites[i] == Actor.HomeSiteId || _operatedPlaces.Contains(sites[i]))
                    {
                        stakes.Add(new ActorStake(ActorStakeKind.Local, sites[i], "it is where they live or work", 10));
                        return;
                    }
                }
            }
        }
    }
}
