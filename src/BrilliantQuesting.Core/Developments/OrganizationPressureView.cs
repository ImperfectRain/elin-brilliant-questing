using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// How a body reached a pressure. The institutional counterpart of a person's route, and the
    /// same rule holds: no route means no reading, and a stake is never one.
    /// </summary>
    public enum OrganizationRouteKind
    {
        /// <summary>It is on file. A receipt names the channel and whoever filed it.</summary>
        Receipt,

        /// <summary>An open condition of a site the body holds, with nothing secret about it.</summary>
        OwnHolding,

        /// <summary>An open undertaking the body is itself party to, read off the ledger.</summary>
        OwnUndertaking
    }

    /// <summary>
    /// Why the matter is this body's business rather than merely near it. As with a person, a stake
    /// changes what a pressure costs the body and never how it came to hear of it.
    /// </summary>
    public enum OrganizationStakeKind
    {
        /// <summary>A site or holding of the body's is in it.</summary>
        Holding,

        /// <summary>One of its people is in it.</summary>
        Member,

        /// <summary>Its reserves are in it.</summary>
        Reserves,

        /// <summary>Its standing with another body is in it.</summary>
        Standing,

        /// <summary>The body itself is what the claim names.</summary>
        Charge
    }

    /// <summary>One reason the matter is this body's, with the record that says so.</summary>
    public sealed class OrganizationStake
    {
        internal OrganizationStake(OrganizationStakeKind kind, EntityId subjectId, string explanation, int weight)
        {
            Kind = kind;
            SubjectId = subjectId;
            Explanation = explanation ?? string.Empty;
            Weight = weight;
        }

        public OrganizationStakeKind Kind { get; }

        /// <summary>Whom or what the stake is in, where the stake names one. None otherwise.</summary>
        public EntityId SubjectId { get; }

        /// <summary>Plain words for an inspector. Never the hidden content of the pressure.</summary>
        public string Explanation { get; }

        /// <summary>How much this stake adds to the body's urgency, 0..100 before clamping.</summary>
        public int Weight { get; }

        public override string ToString() => Kind + "(" + (SubjectId.IsNone ? "-" : SubjectId.Value) + ") +" + Weight;
    }

    /// <summary>
    /// One pressure as a single body can legitimately have it in front of it.
    ///
    /// Derived and never saved, like the development and the actor-local reading it sits beside.
    /// What is saved is the receipt it came off, which is the point: the record of having been told
    /// is durable, and the reading of it is recomputed.
    /// </summary>
    public sealed class OrganizationPressure
    {
        internal OrganizationPressure(
            EntityId organizationId,
            string id,
            string developmentId,
            IReadOnlyList<string> pressureTags,
            EntityId focusFactId,
            IReadOnlyList<EntityId> subjectIds,
            IReadOnlyList<EntityId> siteIds,
            IReadOnlyList<OrganizationStake> stakes,
            OrganizationRouteKind route,
            string receiptId,
            InstitutionalChannel channel,
            double confidence,
            bool provable,
            bool misinformed,
            IReadOnlyList<string> unknown,
            int urgency,
            IReadOnlyList<string> terms)
        {
            OrganizationId = organizationId;
            Id = id ?? string.Empty;
            DevelopmentId = developmentId ?? string.Empty;
            PressureTags = pressureTags ?? Empty<string>.List;
            FocusFactId = focusFactId;
            SubjectIds = subjectIds ?? Empty<EntityId>.List;
            SiteIds = siteIds ?? Empty<EntityId>.List;
            Stakes = stakes ?? Empty<OrganizationStake>.List;
            Route = route;
            ReceiptId = receiptId ?? string.Empty;
            Channel = channel;
            Confidence = confidence;
            Provable = provable;
            Misinformed = misinformed;
            Unknown = unknown ?? Empty<string>.List;
            Urgency = urgency < 0 ? 0 : urgency > 100 ? 100 : urgency;
            Terms = terms ?? Empty<string>.List;
        }

        public EntityId OrganizationId { get; }

        /// <summary>Stable key for this body's reading, derived from what it is reading.</summary>
        public string Id { get; }

        /// <summary>The objective pressure behind it, or empty where a standing report is all of it.</summary>
        public string DevelopmentId { get; }

        public bool HasObjectiveCause => DevelopmentId.Length > 0;

        public IReadOnlyList<string> PressureTags { get; }

        /// <summary>The claim the body is acting on - the one on file, which may not be the true one.</summary>
        public EntityId FocusFactId { get; }

        /// <summary>Who the body can legitimately place in the matter, in stable id order.</summary>
        public IReadOnlyList<EntityId> SubjectIds { get; }

        public IReadOnlyList<EntityId> SiteIds { get; }

        public IReadOnlyList<OrganizationStake> Stakes { get; }

        public OrganizationRouteKind Route { get; }

        /// <summary>The receipt this was read off, or empty where the route was not a receipt.</summary>
        public string ReceiptId { get; }

        /// <summary>Meaningful only for <see cref="OrganizationRouteKind.Receipt"/>.</summary>
        public InstitutionalChannel Channel { get; }

        /// <summary>0..1: the filing's confidence, or whole for the body's own records.</summary>
        public double Confidence { get; }

        /// <summary>The filing came with something that would demonstrate the claim.</summary>
        public bool Provable { get; }

        /// <summary>
        /// The claim the body is acting on is untrue. Here so a test, an inspector and a later
        /// consequence can tell a misinformed institution from a dishonest one - never consulted
        /// by anything deciding what the body does, because a body that can see this is omniscient.
        /// </summary>
        public bool Misinformed { get; }

        /// <summary>Which parts the body has no route to, as generic labels rather than content.</summary>
        public IReadOnlyList<string> Unknown { get; }

        /// <summary>0..100, from the body's point of view rather than the world's.</summary>
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

        public bool HasStake(OrganizationStakeKind kind)
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
            return Id + " u" + Urgency + " via " + Route
                   + (Route == OrganizationRouteKind.Receipt ? "/" + Channel : string.Empty);
        }
    }

    /// <summary>
    /// What one body can legitimately have in front of it (BQa-018).
    ///
    /// The institutional twin of <see cref="ActorPressureView"/>, and it is a twin rather than a
    /// copy because the gate is a different one. A person's route is what they hold, what they are
    /// party to, and what is plainly there where they live. A body holds nothing and lives nowhere:
    /// what it has is what somebody put in front of it. So the first route is a standing receipt,
    /// and the other two are the records the body itself maintains - a site it keeps, an
    /// undertaking it is party to, an aim it has stated.
    ///
    /// <b>What is deliberately not a route.</b> The body's own stated aim is not: the detector reads
    /// an organization stake off the goals the body already holds, and admitting that back as a
    /// reading would make every institutional goal its own cause and nothing would ever retire.
    /// Membership is not: a guild of forty does not know
    /// what forty people privately believe, and unioning their beliefs is the collective mind this
    /// step exists to refuse. Ownership is not: a body that owns the warehouse does not thereby
    /// know who emptied it, which is why an owned loss needs an accounting, member or holding
    /// observation channel before the body has it at all. Office is not: being answerable for a
    /// kind of matter is a reason to act on one and never a way of hearing about it.
    ///
    /// <b>Reports may be false, and this never checks.</b> A receipt that still stands is read as
    /// the body's own position whether or not the world agrees, exactly as a sincere belief is for
    /// a person. <see cref="OrganizationPressure.Misinformed"/> records the disagreement for
    /// whoever is entitled to see it; the body is never told.
    ///
    /// A pure reading. Nothing here teaches, files, writes or appends: running it twice returns the
    /// same answer and running it never leaves the world different.
    /// </summary>
    public static class OrganizationPressureView
    {
        private static readonly IReadOnlyList<OrganizationPressure> Nothing = new OrganizationPressure[0];

        private static readonly IReadOnlyList<Development> NoDevelopments = new Development[0];

        /// <summary>Label for a matter the body has no filing about. Generic on purpose.</summary>
        private const string UnfiledFocus = "focus claim";

        /// <summary>Label for who is in it, when nothing on file demonstrates that.</summary>
        private const string UnnamedParty = "implicated party";

        /// <summary>
        /// Everything this body can legitimately be under pressure about, in stable id order.
        ///
        /// <paramref name="objective"/> is normally
        /// <see cref="DevelopmentDetector.Detect(NarrativeWorldState, DevelopmentScope)"/>'s output
        /// for whatever work set the caller bounded. Passing nothing is meaningful and is not the
        /// same as passing an empty world: the receipt path still runs, because what a body has been
        /// told does not depend on what the detector was asked to look at.
        /// </summary>
        public static IReadOnlyList<OrganizationPressure> Of(
            NarrativeWorldState world,
            EntityId organizationId,
            IReadOnlyList<Development> objective)
        {
            if (world == null)
            {
                return Nothing;
            }

            Organization body = world.Registry.GetOrganization(organizationId);
            if (body == null)
            {
                return Nothing;
            }

            List<OrganizationPressure> readings = new List<OrganizationPressure>();
            List<EntityId> covered = new List<EntityId>();

            IReadOnlyList<Development> pressures = objective ?? NoDevelopments;
            for (int i = 0; i < pressures.Count; i++)
            {
                Development development = pressures[i];
                if (development == null)
                {
                    continue;
                }

                OrganizationPressure reading = Project(world, body, development);
                if (reading == null)
                {
                    continue;
                }

                readings.Add(reading);
                if (!development.FocusFactId.IsNone && !covered.Contains(development.FocusFactId))
                {
                    covered.Add(development.FocusFactId);
                }
            }

            IReadOnlyList<InstitutionalReceipt> filings = body.Receipts.StandingReceipts();
            for (int i = 0; i < filings.Count; i++)
            {
                InstitutionalReceipt receipt = filings[i];
                if (covered.Contains(receipt.ClaimId))
                {
                    // The world is holding it and the body reached it above. Reading the filing
                    // again would weigh one matter twice under a second key.
                    continue;
                }

                OrganizationPressure reading = ProjectFiling(world, body, receipt);
                if (reading != null)
                {
                    readings.Add(reading);
                }
            }

            readings.Sort(ById);
            return readings;
        }

        private static int ById(OrganizationPressure left, OrganizationPressure right) =>
            string.CompareOrdinal(left.Id, right.Id);

        // -- objective pressure, seen from where this body stands --------------------------------

        /// <summary>
        /// This body's reading of one development, or null where it has no legitimate route to it.
        ///
        /// Null rather than a faint reading, for the reason the actor view gives: "this is not ours"
        /// and "this is ours and we do not care" are different answers, and a consumer handed the
        /// second for the first eventually has a guild act on a matter nobody ever reported to it.
        /// </summary>
        private static OrganizationPressure Project(NarrativeWorldState world, Organization body, Development development)
        {
            Terms terms = new Terms();
            Fact focus = development.FocusFactId.IsNone ? null : world.Knowledge.GetFact(development.FocusFactId);
            InstitutionalReceipt receipt =
                development.FocusFactId.IsNone ? null : body.Receipts.Standing(development.FocusFactId);

            OrganizationRouteKind route;
            if (receipt != null)
            {
                route = OrganizationRouteKind.Receipt;
                terms.Route("on file by " + Lower(receipt.Channel));
            }
            else if (PartyToUndertakingIn(world, body, development))
            {
                route = OrganizationRouteKind.OwnUndertaking;
                terms.Route("its own undertaking");
            }
            else if (OpenConditionOfItsHolding(body, development, focus))
            {
                route = OrganizationRouteKind.OwnHolding;
                terms.Route("open condition of a holding it keeps");
            }
            else
            {
                return null;
            }

            double confidence = receipt == null ? 1.0 : receipt.Confidence;
            bool provable = receipt == null || receipt.Provable;

            List<string> unknown = new List<string>();
            if (!development.FocusFactId.IsNone && receipt == null)
            {
                // It is party to the record and has been told nothing about the claim behind it.
                unknown.Add(UnfiledFocus);
            }

            List<EntityId> subjects = Placeable(body, development.SubjectIds, provable);
            if (NamedOthers(subjects, body) < NamedOthers(development.SubjectIds, body))
            {
                unknown.Add(UnnamedParty);
            }

            List<OrganizationStake> stakes = StakesIn(world, body, development.PressureTags, subjects, development.SiteIds, focus);
            int urgency = Weigh(terms, development.Urgency, confidence, stakes);

            return new OrganizationPressure(
                body.Id,
                "institutional:" + development.Id,
                development.Id,
                development.PressureTags,
                development.FocusFactId,
                subjects,
                development.SiteIds,
                stakes,
                route,
                receipt == null ? string.Empty : receipt.Id,
                receipt == null ? InstitutionalChannel.Accounting : receipt.Channel,
                confidence,
                provable,
                focus != null && focus.IsUntrue,
                unknown,
                urgency,
                terms.All);
        }

        // -- a filing the world is not holding, or has not been asked about ----------------------

        /// <summary>
        /// A claim on file that names a trouble, where no development covered it.
        ///
        /// The required wrong-report path, and like the actor view's false-belief path it has to
        /// enumerate rather than filter: the detector tests claims for truth and returns, so a body
        /// misinformed by one of its own people is invisible to it by construction.
        ///
        /// Not only for errors. A true matter the detector was not asked about - outside a bounded
        /// work set, or settled while the body's file still says otherwise - is a real thing for the
        /// body to be answering, and saying so costs less than truth-checking it would.
        /// </summary>
        private static OrganizationPressure ProjectFiling(
            NarrativeWorldState world, Organization body, InstitutionalReceipt receipt)
        {
            Fact claim = world.Knowledge.GetFact(receipt.ClaimId);
            if (claim == null)
            {
                return null;
            }

            ClaimConcern concern = ClaimConcern.Of(claim.Predicate);
            if (concern == null)
            {
                return null;
            }

            Terms terms = new Terms();
            terms.Route("on file by " + Lower(receipt.Channel));

            List<string> tags = new List<string> { concern.Tag };
            List<EntityId> named = new List<EntityId>();
            Include(named, claim.Subject, world);
            Include(named, claim.Object, world);
            named.Sort();

            List<EntityId> subjects = Placeable(body, named, receipt.Provable);
            List<string> unknown = new List<string>();
            if (NamedOthers(subjects, body) < NamedOthers(named, body))
            {
                unknown.Add(UnnamedParty);
            }

            List<EntityId> sites = new List<EntityId>();
            if (world.Registry.GetSite(claim.Subject) != null)
            {
                sites.Add(claim.Subject);
            }

            List<OrganizationStake> stakes = StakesIn(world, body, tags, subjects, sites, claim);
            int urgency = Weigh(terms, concern.Weight, receipt.Confidence, stakes);

            return new OrganizationPressure(
                body.Id,
                "institutional.filed:" + claim.Id.Value,
                string.Empty,
                tags,
                claim.Id,
                subjects,
                sites,
                stakes,
                OrganizationRouteKind.Receipt,
                receipt.Id,
                receipt.Channel,
                receipt.Confidence,
                receipt.Provable,
                claim.IsUntrue,
                unknown,
                urgency,
                terms.All);
        }

        // -- routes ------------------------------------------------------------------------------

        /// <summary>
        /// An undertaking the body is itself party to. Read off the obligation ledger, which names
        /// both sides, rather than inferred from the body being mentioned somewhere near a debt.
        /// </summary>
        private static bool PartyToUndertakingIn(NarrativeWorldState world, Organization body, Development development)
        {
            if (!development.HasPressure(DevelopmentPressures.UnmetObligation))
            {
                return false;
            }

            IReadOnlyList<SocialObligation> debts = world.Obligations.Records;
            for (int i = 0; i < debts.Count; i++)
            {
                SocialObligation debt = debts[i];
                if (debt.IsOpen
                    && (debt.Debtor == body.Id || debt.Creditor == body.Id)
                    && string.Equals(
                        development.Id, "dev.unmet_obligation:" + debt.Id.Value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A condition of a place the body keeps that is plainly there - a shuttered counter, a
        /// town short of grain, a broken wheel on its own yard.
        ///
        /// Narrow in the same two directions the actor view's street route is. Only conditions of a
        /// place rather than of a person, and only where the claim behind it is not secret. A theft,
        /// a killing, a secret nobody can prove and a contradiction in the record are all invisible
        /// from a yard, and holding the deed to the yard is not a way of learning them: those reach
        /// the body through an accounting, member or holding observation channel or not at all.
        /// </summary>
        private static bool OpenConditionOfItsHolding(Organization body, Development development, Fact focus)
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
                if (body.SiteIds.Contains(sites[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // -- stakes, computed strictly after a route has admitted the reading --------------------

        private static List<OrganizationStake> StakesIn(
            NarrativeWorldState world,
            Organization body,
            IReadOnlyList<string> tags,
            IReadOnlyList<EntityId> subjects,
            IReadOnlyList<EntityId> sites,
            Fact focus)
        {
            List<OrganizationStake> stakes = new List<OrganizationStake>();

            for (int i = 0; i < sites.Count; i++)
            {
                if (body.SiteIds.Contains(sites[i]))
                {
                    stakes.Add(new OrganizationStake(
                        OrganizationStakeKind.Holding, sites[i], "a site it keeps", 20));
                    break;
                }
            }

            for (int i = 0; i < subjects.Count; i++)
            {
                EntityId subject = subjects[i];
                if (subject == body.Id)
                {
                    stakes.Add(new OrganizationStake(
                        OrganizationStakeKind.Charge, subject, "the body itself is named", 25));
                    continue;
                }

                if (subject == body.LeaderId || body.MemberIds.Contains(subject))
                {
                    stakes.Add(new OrganizationStake(
                        OrganizationStakeKind.Member,
                        subject,
                        subject == body.LeaderId ? "the one who leads it" : "one of its people",
                        subject == body.LeaderId ? 25 : 15));
                    continue;
                }

                if (world.Registry.GetOrganization(subject) != null)
                {
                    stakes.Add(new OrganizationStake(
                        OrganizationStakeKind.Standing, subject, "another body it stands against", 10));
                }
            }

            if (Has(tags, DevelopmentPressures.Shortage)
                || Has(tags, DevelopmentPressures.ServiceInterruption)
                || (focus != null && !focus.Object.IsNone && Owns(world, body, focus.Object)))
            {
                stakes.Add(new OrganizationStake(
                    OrganizationStakeKind.Reserves, EntityId.None, "what it trades on", 10));
            }

            return stakes;
        }

        private static bool Owns(NarrativeWorldState world, Organization body, EntityId itemId) =>
            Ownership.OwnerOf(world, itemId) == body.Id;

        // -- urgency -----------------------------------------------------------------------------

        /// <summary>
        /// How hard this presses on the body.
        ///
        /// The objective reading is the ceiling, scaled by how sure the filing is and raised by what
        /// the body stands to lose. Stakes move it only after a route has already admitted the
        /// reading, so owning the warehouse never becomes a way of hearing that it was emptied.
        /// </summary>
        private static int Weigh(Terms terms, int objectiveUrgency, double confidence, List<OrganizationStake> stakes)
        {
            double sure = confidence < 0.0 ? 0.0 : confidence > 1.0 ? 1.0 : confidence;
            double urgency = objectiveUrgency * (0.5 + (0.5 * sure));
            terms.Add("condition", objectiveUrgency);
            terms.Add("certainty", sure);

            for (int i = 0; i < stakes.Count; i++)
            {
                urgency += stakes[i].Weight;
                terms.Add(Lower(stakes[i].Kind), stakes[i].Weight);
            }

            int rounded = (int)Math.Round(urgency);
            return rounded < 0 ? 0 : rounded > 100 ? 100 : rounded;
        }

        // -- what the body can put a name to -----------------------------------------------------

        /// <summary>
        /// Which of the people a matter names this body can actually place in it.
        ///
        /// Everyone, once the filing came with something that would demonstrate the claim. Short of
        /// that the body can place itself and its own people - who are answerable to it and whose
        /// part in a matter it can establish by asking them - and the rest stays
        /// <see cref="UnnamedParty"/>, because handing over the detector's list of names is handing
        /// over the answer to a body that was only told that something happened.
        /// </summary>
        private static List<EntityId> Placeable(Organization body, IReadOnlyList<EntityId> named, bool provable)
        {
            List<EntityId> placeable = new List<EntityId>();
            for (int i = 0; i < named.Count; i++)
            {
                EntityId id = named[i];
                if (id.IsNone || placeable.Contains(id))
                {
                    continue;
                }

                if (provable || id == body.Id || id == body.LeaderId || body.MemberIds.Contains(id))
                {
                    placeable.Add(id);
                }
            }

            placeable.Sort();
            return placeable;
        }

        /// <summary>
        /// How many parties other than the body itself a list names. Counted on both sides of the
        /// placement question, because the body places itself for free and a raw length would hide
        /// the one name it cannot actually put to the matter.
        /// </summary>
        private static int NamedOthers(IReadOnlyList<EntityId> ids, Organization body)
        {
            int count = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!ids[i].IsNone && ids[i] != body.Id)
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

        private static bool Has(IReadOnlyList<string> tags, string tag)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Lower(object value) => value.ToString().ToLowerInvariant();

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
    }
}
