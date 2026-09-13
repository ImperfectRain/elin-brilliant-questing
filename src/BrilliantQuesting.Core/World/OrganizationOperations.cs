using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// How a body gets an operation done (BQa-019).
    ///
    /// Two, and the line between them is whether the change needs hands. Nothing else decides it:
    /// not the goal's name, not how important the body thinks it is.
    /// </summary>
    public enum OrganizationMeans
    {
        /// <summary>
        /// A change to the body's own records - its roll, its reserves, the watch on its own yard.
        /// No NPC performs it, no NPC is anywhere, and the event names the body rather than a
        /// person, because a ledger entry has no body and pretending otherwise is the invented
        /// physical presence this step is told not to produce.
        /// </summary>
        Bookkeeping,

        /// <summary>
        /// A deed in the world - somebody else's property, a person's safety, a shortage in the
        /// town. It goes through one eligible real member's shared <see cref="ActionAttempt"/>,
        /// routed by BQa-011 and settled in BQa-015's batch, exactly as that member's own want
        /// would be.
        /// </summary>
        Delegated
    }

    /// <summary>
    /// What a body would do about one of its ends, with what it would take and what it would
    /// cost, before anything is done about it (BQa-019).
    ///
    /// A proposal. Reading one changes nothing; <see cref="OrganizationActivity"/> is what
    /// performs a bookkeeping operation and <see cref="ArbitrationBatch"/> what performs a
    /// delegated one.
    /// </summary>
    public sealed class OrganizationOperation
    {
        internal OrganizationOperation(
            EntityId organizationId,
            OrganizationGoal goal,
            string kind,
            OrganizationMeans means,
            IReadOnlyList<string> effects,
            EntityId agent,
            EntityId subject,
            int reserveCost,
            GoalRoute route,
            string because)
        {
            OrganizationId = organizationId;
            Goal = goal;
            Kind = kind ?? string.Empty;
            Means = means;
            Effects = effects ?? NoEffects;
            Agent = agent;
            Subject = subject;
            ReserveCost = reserveCost < 0 ? 0 : reserveCost;
            Route = route;
            Because = because ?? string.Empty;
        }

        private static readonly string[] NoEffects = new string[0];

        public EntityId OrganizationId { get; }

        /// <summary>The end this serves. Never null.</summary>
        public OrganizationGoal Goal { get; }

        /// <summary>
        /// Which operation this is: one of <see cref="OrganizationActivity"/>'s institutional
        /// operations, or the registered verb id a delegated deed would be taken for.
        /// </summary>
        public string Kind { get; }

        public OrganizationMeans Means { get; }

        /// <summary>
        /// The kinds of state change this could advance, in <see cref="SemanticEffects"/>' own
        /// vocabulary. Declared rather than inferred, and in the shared vocabulary rather than a
        /// second one, so a consumer that already reads what a verb could do reads this the same
        /// way. Potential capability, never a prediction: the check and the record still decide.
        /// </summary>
        public IReadOnlyList<string> Effects { get; }

        /// <summary>
        /// The member who carries this, or <see cref="EntityId.None"/> for pure bookkeeping.
        /// Always somebody the registry already holds; nothing here mints a body.
        /// </summary>
        public EntityId Agent { get; }

        /// <summary>What it is about: the recruit, the holding, the rival, the route's target.</summary>
        public EntityId Subject { get; }

        /// <summary>Reserves it spends. Zero for a delegated deed, whose cost is the member.</summary>
        public int ReserveCost { get; }

        /// <summary>The BQa-011 route a delegated deed would take, or null for bookkeeping.</summary>
        public GoalRoute Route { get; }

        /// <summary>A code, not a sentence. Nothing branches on it.</summary>
        public string Because { get; }

        public override string ToString()
        {
            return Kind + " for " + Goal.Identity + " (" + Means.ToString().ToLowerInvariant()
                   + (Agent.IsNone ? string.Empty : ", " + Agent.Value)
                   + (ReserveCost > 0 ? ", costs " + ReserveCost : string.Empty) + ")";
        }
    }

    /// <summary>
    /// What a body could do about one end, or why it can do nothing about it (BQa-019).
    ///
    /// The second half is the point. Before this step every end a body could not act on fell
    /// through to building wealth, so a crew that could not find the cart, could not reach a
    /// recruit, or wanted something nothing had ever heard of all got richer for the trouble.
    /// An operation nothing supports is a body with nothing to do, and a body with nothing to do
    /// waits.
    /// </summary>
    public sealed class OrganizationOperationPlan
    {
        private static readonly OrganizationOperation[] None = new OrganizationOperation[0];

        internal OrganizationOperationPlan(params OrganizationOperation[] operations)
        {
            Operations = operations ?? None;
            Refusal = string.Empty;
        }

        internal OrganizationOperationPlan(IReadOnlyList<OrganizationOperation> operations)
        {
            Operations = operations ?? None;
            Refusal = string.Empty;
        }

        internal OrganizationOperationPlan(string refusal)
        {
            Operations = None;
            Refusal = refusal ?? string.Empty;
        }

        /// <summary>
        /// What the body could do about this end. One for an institutional operation; for a deed
        /// it is every way the member it would send could go about it, bounded, because which of
        /// them is worth anything here is BQa-015's question rather than this one's - they share
        /// one contest, so the batch ranks them, revalidates the best against the world as it is,
        /// and the rest yield the moment one of them finishes the thing.
        /// </summary>
        public IReadOnlyList<OrganizationOperation> Operations { get; }

        /// <summary>The first of them, or null when there is none.</summary>
        public OrganizationOperation Operation => Operations.Count > 0 ? Operations[0] : null;

        /// <summary>Why there is none, or empty when there is one.</summary>
        public string Refusal { get; }

        public bool IsSupported => Operations.Count > 0;

        public override string ToString() =>
            IsSupported ? Operation.ToString() : "waits: " + Refusal;
    }

    /// <summary>
    /// Which operation a body's end admits, and what that operation needs (BQa-019).
    ///
    /// <b>Two questions, and only the first one is here.</b> BQa-018 decided what a body wants and
    /// why. This decides how - if at all - that want can be pursued with what the body actually
    /// has. Nothing here forms, reweights or retires a goal, and nothing here writes anything: a
    /// plan is a reading, and reading twice gives the same answer.
    ///
    /// <b>No central switch on ends.</b> Four operation kinds are named here, and they are named
    /// because they already existed as institutional operations that write the body's own records
    /// - a roll, a reserve band, the watch on a holding, a blow at a rival - and BQa-019 keeps
    /// their owner rather than reinventing them as verbs. Every other end is routed generically:
    /// the end's BQa-008 condition says what would satisfy it, BQa-010's vocabulary says which
    /// changes would move it, BQa-011 says which registered verbs could make those changes, and a
    /// member takes one. So a sixth institutional end registered tomorrow with today's condition
    /// vocabulary is executable here without a line being edited, and an end this build has never
    /// heard of is refused rather than converted into money.
    ///
    /// <b>What a body may reach for.</b> A holding is a site the body keeps whose own record does
    /// not call somebody else its controller - the site's record is the authority on that, the
    /// body's list is only its claim. A member is somebody the registry already holds, alive,
    /// canonical, present and not already spent this pass. A recruit must additionally be reachable
    /// where the body actually is and must not stand against it. Nothing here invents a member, a
    /// holding, a rival or a coin.
    /// </summary>
    public static class OrganizationOperations
    {
        /// <summary>
        /// How many ways of going about one end the body may put in front of the batch.
        ///
        /// More than one because the first registered verb that could make the right kind of
        /// change is not the same thing as the one this person could take here, and fewer than
        /// all because the pass has to cost the same in a town that has been played for a year.
        /// </summary>
        public const int MostRoutesPerEnd = 3;

        /// <summary>What one pass of collecting from its own holdings adds to a body's band.</summary>
        public const int ReserveYield = 4;

        /// <summary>What taking somebody onto the roll costs the body.</summary>
        public const int RecruitCost = 5;

        /// <summary>How far a recruit's standing toward the body may sink before consent fails.</summary>
        public const int LeastRecruitSentiment = -20;

        /// <summary>
        /// The operation this end admits, or why it admits none.
        ///
        /// <paramref name="spentMembers"/> is who this pass has already committed, across every
        /// body: one person does one thing per pass, whether the two bodies asking for them are
        /// rivals or the same crew twice. <paramref name="purse"/> is what the body has left to
        /// spend now, which is not the same as what its record said when the pass began.
        /// </summary>
        public static OrganizationOperationPlan Plan(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ActionRegistry registry,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spentMembers,
            int purse)
        {
            if (world == null || body == null || goal == null)
            {
                return new OrganizationOperationPlan("there is nothing to plan for");
            }

            ICollection<EntityId> spent = spentMembers ?? new List<EntityId>();

            switch (goal.Kind)
            {
                case OrganizationActivity.ExpandMembership:
                    return Recruit(world, vanilla, body, goal, spent, purse);
                case OrganizationActivity.BuildReserves:
                    return Collect(world, vanilla, body, goal, spent);
                case OrganizationActivity.ProtectHolding:
                    return Watch(world, vanilla, body, goal, spent);
                case OrganizationActivity.RaidOrganization:
                    return Raid(world, vanilla, body, goal, spent, purse);
                default:
                    return Delegate(world, vanilla, registry, body, goal, spent);
            }
        }

        // -- institutional operations, which write the body's own records -------------------------

        /// <summary>
        /// Taking somebody onto the roll: reachable where the body is, willing, and paid for.
        ///
        /// All three prerequisites were missing. The old search accepted anybody in the world when
        /// the body held no site, asked nothing about whether they would come, and spent reserves
        /// the body might not have - and when it found nobody, the body built wealth instead. A
        /// recruit nobody can reach is not a recruit, and failing to find one is not an income.
        /// </summary>
        private static OrganizationOperationPlan Recruit(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent,
            int purse)
        {
            if (body.SiteIds.Count == 0)
            {
                return new OrganizationOperationPlan("it keeps nowhere anybody could be reached at");
            }

            if (purse < RecruitCost)
            {
                return new OrganizationOperationPlan("it cannot afford to take anybody on");
            }

            EntityId recruit = Reachable(world, vanilla, body, spent);
            if (recruit.IsNone)
            {
                return new OrganizationOperationPlan("nobody it can reach would come");
            }

            return new OrganizationOperationPlan(new OrganizationOperation(
                body.Id,
                goal,
                OrganizationActivity.ExpandMembership,
                OrganizationMeans.Bookkeeping,
                new[] { SemanticEffects.StandingAltered },
                EntityId.None,
                recruit,
                RecruitCost,
                null,
                "reachable_and_willing"));
        }

        /// <summary>
        /// Reserves, from a named holding of its own.
        ///
        /// The inflow is the thing this operation was missing. It used to add three coins plus one
        /// per member every time it ran, from nowhere, which is both an income that grows with
        /// the roll and an economy nobody owns. What a body may collect now is what its own
        /// holdings bring in, the holding is named on the record, and a body that keeps nothing
        /// collects nothing. Somebody has to do the collecting, so a body with no member free this
        /// pass waits.
        /// </summary>
        private static OrganizationOperationPlan Collect(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent)
        {
            EntityId holding = Holding(world, body, goal.Subject);
            if (holding.IsNone)
            {
                return new OrganizationOperationPlan("it holds nothing that brings anything in");
            }

            EntityId hand = Member(world, vanilla, body, spent);
            if (hand.IsNone)
            {
                return new OrganizationOperationPlan("nobody of its own is free to collect it");
            }

            return new OrganizationOperationPlan(new OrganizationOperation(
                body.Id,
                goal,
                OrganizationActivity.BuildReserves,
                OrganizationMeans.Bookkeeping,
                new[] { SemanticEffects.PossessionTransferred },
                hand,
                holding,
                0,
                null,
                "collected_from_its_own_holding"));
        }

        /// <summary>
        /// The watch on a holding of its own. Somebody stands it, so somebody has to be free.
        /// </summary>
        private static OrganizationOperationPlan Watch(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent)
        {
            EntityId holding = Holding(world, body, goal.Subject);
            if (holding.IsNone)
            {
                return new OrganizationOperationPlan("the place it would keep is not its to keep");
            }

            EntityId hand = Member(world, vanilla, body, spent);
            if (hand.IsNone)
            {
                return new OrganizationOperationPlan("nobody of its own is free to stand it");
            }

            return new OrganizationOperationPlan(new OrganizationOperation(
                body.Id,
                goal,
                OrganizationActivity.ProtectHolding,
                OrganizationMeans.Bookkeeping,
                new[] { SemanticEffects.AccessAltered },
                hand,
                holding,
                0,
                null,
                "its_own_holding"));
        }

        /// <summary>
        /// A blow at another body. It needs a rival that exists, somebody to throw it, and the
        /// reserves to cover it - and when the rival is not there, the body does nothing rather
        /// than getting richer for having wanted to.
        /// </summary>
        private static OrganizationOperationPlan Raid(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent,
            int purse)
        {
            Organization target = world.Registry.GetOrganization(goal.Subject);
            if (target == null || target.Id == body.Id)
            {
                return new OrganizationOperationPlan("there is no such rival to move against");
            }

            int cost = RaidCost(body);
            if (purse < cost)
            {
                return new OrganizationOperationPlan("it cannot afford to move against anybody");
            }

            EntityId hand = Member(world, vanilla, body, spent);
            if (hand.IsNone)
            {
                return new OrganizationOperationPlan("nobody of its own is free to go");
            }

            return new OrganizationOperationPlan(new OrganizationOperation(
                body.Id,
                goal,
                OrganizationActivity.RaidOrganization,
                OrganizationMeans.Bookkeeping,
                new[] { SemanticEffects.StandingAltered },
                hand,
                target.Id,
                cost,
                null,
                "against_a_rival"));
        }

        /// <summary>What one blow costs the body that throws it. Unchanged; openly legitimate bodies pay less.</summary>
        public static int RaidCost(Organization body) => 1 + (100 - body.Legitimacy) / 50;

        // -- everything else, routed through the shared semantics ---------------------------------

        /// <summary>
        /// An end with a machine-readable condition, pursued by somebody who could actually do
        /// something about it.
        ///
        /// This is the whole of BQa-019's "shared semantics": the body does not get verbs of its
        /// own, it gets a member, and the member is offered exactly the routes BQa-011 would offer
        /// them for a want of their own - read through what that member knows rather than what the
        /// body was told, because being directed is not the same as being informed. An end nothing
        /// can be read from, or nothing on this build could move, waits.
        /// </summary>
        private static OrganizationOperationPlan Delegate(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ActionRegistry registry,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent)
        {
            if (!goal.HasCondition)
            {
                return new OrganizationOperationPlan(
                    "'" + goal.Kind + "' is no operation this build has, and the end names no condition to route");
            }

            if (registry == null)
            {
                return new OrganizationOperationPlan("no action library is attached to carry a deed out");
            }

            EntityId handId = Sender(world, vanilla, body, goal, spent);
            if (handId.IsNone)
            {
                return new OrganizationOperationPlan("nobody of its own is free to go");
            }

            NarrativeNpc hand = world.Registry.GetNpc(handId);
            GoalRouteSearch search = GoalRoutes.DiscoverFor(world, vanilla, registry, hand, goal.Condition);
            if (!search.IsSupported)
            {
                return new OrganizationOperationPlan(search.Unsupported);
            }

            if (!search.HasRoutes)
            {
                return new OrganizationOperationPlan(
                    "nothing " + handId.Value + " could do would move '" + goal.Condition.Kind + "'");
            }

            List<OrganizationOperation> ways = new List<OrganizationOperation>();
            for (int i = 0; i < search.Routes.Count && ways.Count < MostRoutesPerEnd; i++)
            {
                GoalRoute route = search.Routes[i];
                ways.Add(new OrganizationOperation(
                    body.Id,
                    goal,
                    route.Action.Id,
                    OrganizationMeans.Delegated,
                    new[] { route.EffectKind },
                    handId,
                    route.Target,
                    0,
                    route,
                    "directed_a_member"));
            }

            return new OrganizationOperationPlan(ways);
        }

        // -- what a body may reach for ------------------------------------------------------------

        /// <summary>
        /// A site the body keeps whose own record does not call somebody else its controller, or
        /// none. <paramref name="preferred"/> is the end's own subject where it names one.
        ///
        /// The site's record is the authority on who holds it and the body's list is only its
        /// claim, which is the same discipline BQa-018 applies to a holding it says was taken: a
        /// body that collects from a yard another body's record calls its own is helping itself.
        /// </summary>
        public static EntityId Holding(NarrativeWorldState world, Organization body, EntityId preferred)
        {
            if (!preferred.IsNone && Keeps(world, body, preferred))
            {
                return preferred;
            }

            EntityId best = EntityId.None;
            for (int i = 0; i < body.SiteIds.Count; i++)
            {
                EntityId site = body.SiteIds[i];
                if (!Keeps(world, body, site))
                {
                    continue;
                }

                // The least id rather than the first listed, so the same body collects from the
                // same holding after a reload however the list came back.
                if (best.IsNone || string.CompareOrdinal(site.Value, best.Value) < 0)
                {
                    best = site;
                }
            }

            return best;
        }

        private static bool Keeps(NarrativeWorldState world, Organization body, EntityId siteId)
        {
            if (siteId.IsNone || !body.SiteIds.Contains(siteId))
            {
                return false;
            }

            NarrativeSite site = world.Registry.GetSite(siteId);
            return site != null
                   && (site.ControllingOrganizationId.IsNone || site.ControllingOrganizationId == body.Id);
        }

        /// <summary>
        /// Who the body sends about this end: whoever brought it the matter, if they are free, and
        /// otherwise whoever else it has.
        ///
        /// Not an optimisation. What the body holds is a filing, and the person on the filing is
        /// the one who has actually seen the thing - so sending them is both what an institution
        /// does and the only way the deed gets a member who knows enough to be pointed at anybody.
        /// Sending the leader instead would have a boss who has never laid eyes on the cart
        /// looking for whoever took it, which BQa-011 correctly offers no route to.
        ///
        /// It reads the body's own receipt ledger and nothing else: an end nobody filed, or one
        /// whose filer is gone, falls back to the ordinary choice rather than searching the world
        /// for somebody who happens to know.
        /// </summary>
        public static EntityId Sender(
            NarrativeWorldState world,
            IVanillaState vanilla,
            Organization body,
            OrganizationGoal goal,
            ICollection<EntityId> spent)
        {
            if (goal.Origin.Kind == GoalSourceKind.InstitutionalReading && !goal.Origin.RecordId.IsNone)
            {
                InstitutionalReceipt filing = body.Receipts.Standing(goal.Origin.RecordId);
                if (filing != null
                    && body.MemberIds.Contains(filing.FiledBy)
                    && Eligible(world, vanilla, filing.FiledBy)
                    && !spent.Contains(filing.FiledBy))
                {
                    return filing.FiledBy;
                }
            }

            return Member(world, vanilla, body, spent);
        }

        /// <summary>
        /// One of the body's own people who could do something today, preferring the leader, or
        /// none.
        ///
        /// The leader is only a preference. A body whose leader has died, gone away or turned out
        /// to be an alias is not a body that has stopped existing - its other people are still
        /// there - and a body with nobody left does nothing at all rather than acting through
        /// somebody who is not there.
        /// </summary>
        public static EntityId Member(
            NarrativeWorldState world, IVanillaState vanilla, Organization body, ICollection<EntityId> spent)
        {
            if (Eligible(world, vanilla, body.LeaderId) && !spent.Contains(body.LeaderId))
            {
                return body.LeaderId;
            }

            EntityId best = EntityId.None;
            for (int i = 0; i < body.MemberIds.Count; i++)
            {
                EntityId member = body.MemberIds[i];
                if (!Eligible(world, vanilla, member) || spent.Contains(member))
                {
                    continue;
                }

                if (best.IsNone || string.CompareOrdinal(member.Value, best.Value) < 0)
                {
                    best = member;
                }
            }

            return best;
        }

        /// <summary>
        /// Somebody the body could actually take on: reachable where it keeps something, not
        /// already anybody's, and not set against it.
        /// </summary>
        private static EntityId Reachable(
            NarrativeWorldState world, IVanillaState vanilla, Organization body, ICollection<EntityId> spent)
        {
            EntityId best = EntityId.None;
            foreach (KeyValuePair<EntityId, NarrativeNpc> pair in world.Registry.Npcs)
            {
                NarrativeNpc npc = pair.Value;
                if (!Eligible(world, vanilla, npc.Id)
                    || spent.Contains(npc.Id)
                    || body.MemberIds.Contains(npc.Id)
                    || npc.OrganizationIds.Count > 0
                    || !body.SiteIds.Contains(npc.HomeSiteId)
                    || !Consents(world, body, npc.Id))
                {
                    continue;
                }

                // Ordinal rather than whatever the registry enumerates, so which of two willing
                // strangers is asked does not depend on a dictionary.
                if (best.IsNone || string.CompareOrdinal(npc.Id.Value, best.Value) < 0)
                {
                    best = npc.Id;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether somebody would come if asked, read off the standing they already hold toward
        /// the body and its leader. An enemy does not join, and neither does somebody who thinks
        /// that badly of them; anybody with no opinion recorded is free to be asked.
        /// </summary>
        private static bool Consents(NarrativeWorldState world, Organization body, EntityId who)
        {
            return Willing(world, who, body.Id) && Willing(world, who, body.LeaderId);
        }

        private static bool Willing(NarrativeWorldState world, EntityId who, EntityId toward)
        {
            if (toward.IsNone || who == toward)
            {
                return true;
            }

            RelationshipEdge edge = world.Relationships.Find(who, toward);
            if (edge == null)
            {
                return true;
            }

            return edge.Kind != RelationKind.Enemy && edge.Sentiment >= LeastRecruitSentiment;
        }

        /// <summary>
        /// Whether this is somebody the simulation may act through: a real, living, canonical
        /// person who is here. The player is never one of them - a body does not get to spend the
        /// player's day - and where a build is attached it gets the last word on whether they are
        /// still alive.
        /// </summary>
        public static bool Eligible(NarrativeWorldState world, IVanillaState vanilla, EntityId who)
        {
            if (who.IsNone)
            {
                return false;
            }

            NarrativeNpc npc = world.Registry.GetNpc(who);
            if (npc == null || !npc.Alive || !npc.IsCanonical || world.Absences.IsAbsent(who))
            {
                return false;
            }

            return vanilla == null || (who != vanilla.PlayerId && vanilla.IsAlive(who));
        }
    }
}
