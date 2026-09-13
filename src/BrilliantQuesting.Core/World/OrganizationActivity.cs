using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.World
{
    /// <summary>How much one organization pass may do (BQa-019). Counts of work, never a clock.</summary>
    public sealed class OrganizationActivityBudget
    {
        /// <summary>Bodies whose ends are read per pass.</summary>
        public int MostBodiesPerPass { get; set; } = 16;

        /// <summary>Ends of one body that are planned per pass.</summary>
        public int MostOperationsPerBody { get; set; } = 2;

        /// <summary>Delegated deeds the whole pass may put into the batch.</summary>
        public int MostDelegationsPerPass { get; set; } = 8;
    }

    /// <summary>What one organization pass planned, refused and did. Derived, transient.</summary>
    public sealed class OrganizationActivityPass
    {
        private static readonly IReadOnlyList<EntityId> Nobody = new EntityId[0];

        internal OrganizationActivityPass(GameTime at)
        {
            At = at;
            Day = at.TotalDays;
            Bodies = Nobody;
        }

        public GameTime At { get; }

        public long Day { get; }

        /// <summary>The bodies this pass gave a turn, in the order it took them.</summary>
        public IReadOnlyList<EntityId> Bodies { get; internal set; }

        /// <summary>Every operation the pass planned, bookkeeping and delegated alike.</summary>
        public List<OrganizationOperation> Planned { get; } = new List<OrganizationOperation>();

        /// <summary>
        /// Every end the pass could do nothing about, and why. The visible half of "waits":
        /// before BQa-019 each of these was an income.
        /// </summary>
        public List<string> Refusals { get; } = new List<string>();

        /// <summary>Delegated deeds dropped because their indivisible opening was already spent.</summary>
        public List<string> OpeningsSkipped { get; } = new List<string>();

        /// <summary>Indivisible openings this pass closed.</summary>
        public List<string> OpeningsSpent { get; } = new List<string>();

        /// <summary>The batch that settled the delegated deeds, or null when there were none.</summary>
        public ArbitrationResult Arbitration { get; internal set; }

        /// <summary>Operations that actually changed something.</summary>
        public int Committed { get; internal set; }

        /// <summary>Ends this pass's own committed operation brought about.</summary>
        public int Satisfied { get; internal set; }

        public override string ToString()
        {
            return "day " + Day + ": " + Bodies.Count + " body(ies), " + Planned.Count
                + " operation(s), " + Refusals.Count + " refusal(s), " + Committed + " committed";
        }
    }

    /// <summary>
    /// What a body does about what it wants (BQa-019).
    ///
    /// <b>Institutional operations keep their owner.</b> Four of them write the body's own records
    /// and are performed here, as they always were: taking somebody onto the roll, collecting from
    /// a holding, standing a watch on one, and moving against a rival. What BQa-019 changed about
    /// them is that each now says what it could change, what it needs before it can happen and
    /// what it costs - and that a body which cannot meet those prerequisites does nothing instead
    /// of falling through to money.
    ///
    /// <b>Everything else is somebody's deed.</b> An end with a machine-readable condition is
    /// pursued by an eligible real member through the shared <see cref="ActionAttempt"/>, routed
    /// by BQa-011 from the end's own condition and settled in BQa-015's ranked batch alongside
    /// everybody else's intentions. There is no organization verb library, no organization check
    /// and no invisible body: a body that has nobody free does nothing.
    ///
    /// <b>The fallbacks are gone.</b> `BuildWealth` used to catch an unknown end, a recruit search
    /// that found nobody, a holding that was not there and a rival that was not there, and paid
    /// the body for each. That is the one behaviour this step exists to remove: an end nothing
    /// supports is recorded as a refusal and the body waits.
    ///
    /// <b>Nothing depends on enumeration order.</b> Bodies are taken least-recently-acted first
    /// and then by id, one person is spent once per pass whichever body asks for them, and the
    /// contested half - deeds aimed at one object, one person, one shortage - is ranked by
    /// BQa-015 rather than by who was reached first.
    ///
    /// <b>What closes an end.</b> Only the body's own committed operation, and then only if the
    /// world agrees the condition now holds. Effort is not evidence: <see
    /// cref="OrganizationGoal.Progress"/> is what the body has put in and says nothing about
    /// whether it worked. And an end whose condition quietly became true behind the body's back
    /// stays open here, for BQa-018's evolution to retire once its cause has left the body's view
    /// - asking the world on the body's behalf would hand it the save's knowledge for free.
    ///
    /// Headless. Nothing here touches Elin; <see cref="IVanillaState"/> is asked only what this
    /// build can carry. Live enrollment and host wiring are BQa-020's.
    /// </summary>
    public sealed class OrganizationActivity
    {
        public const string ExpandMembership = "expand_membership";
        public const string BuildReserves = "build_reserves";
        public const string ProtectHolding = "protect_holding";
        public const string RaidOrganization = "raid_organization";

        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;
        private readonly ICheckResolver _checks;
        private readonly ActionRegistry _registry;

        /// <summary>
        /// Bookkeeping only. A body's institutional operations still run; an end that would need
        /// somebody to go and do something is refused by name rather than quietly skipped, because
        /// there is no library here to carry it.
        /// </summary>
        public OrganizationActivity(NarrativeWorldState world)
            : this(world, null, null, null)
        {
        }

        /// <summary>
        /// The whole seam or none of it. A build, a resolver and a library are what one delegated
        /// deed takes from end to end, and half of them is not a pass that does half the work - it
        /// is a pass that gathers deeds it has nowhere to perform. Two of three is therefore the
        /// bookkeeping-only pass, which refuses every deed by name.
        /// </summary>
        public OrganizationActivity(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));

            bool whole = vanilla != null && checks != null && registry != null;
            _vanilla = whole ? vanilla : null;
            _checks = whole ? checks : null;
            _registry = whole ? registry : null;
        }

        public OrganizationActivityBudget Budget { get; set; } = new OrganizationActivityBudget();

        /// <summary>How a delegated deed reads the world. Off-screen unless a host says otherwise.</summary>
        public ContextObservation Observation { get; set; } = ContextObservation.OffScreen;

        /// <summary>A host's own stop, asked before each delegated execution (BQa-015).</summary>
        public IBatchCancellation Cancellation { get; set; }

        /// <summary>The last pass, for an inspector and for tests. Transient.</summary>
        public OrganizationActivityPass LastPass { get; private set; }

        /// <summary>
        /// One bounded pass. Returns how many bodies changed something, which is what the existing
        /// callers count; <see cref="LastPass"/> is where what they planned and refused lives.
        /// </summary>
        public int Advance(GameTime now)
        {
            OrganizationActivityPass pass = new OrganizationActivityPass(now);
            OrganizationActivityBudget budget = Budget ?? new OrganizationActivityBudget();
            ProductionCycleLedger ledger = _world.ProductionCycle;

            List<Organization> bodies = Whose(budget, now);
            List<EntityId> turns = new List<EntityId>();
            List<EntityId> spentMembers = new List<EntityId>();
            List<Delegation> delegated = new List<Delegation>();
            HashSet<string> changed = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < bodies.Count; i++)
            {
                // Out of room. The bodies not reached keep the clock they had, so they sort to the
                // front of the next pass rather than losing a day to a batch that was already
                // full when their turn came round.
                if (delegated.Count >= budget.MostDelegationsPerPass)
                {
                    break;
                }

                Organization body = bodies[i];
                turns.Add(body.Id);
                Plan(pass, budget, ledger, body, spentMembers, delegated, changed, now);

                // Taken as having had its turn whether or not anything came of it. A body that
                // read its ends and could do nothing about them has been considered.
                body.LastActedAt = now;
            }

            pass.Bodies = turns;

            if (delegated.Count > 0)
            {
                Settle(pass, ledger, delegated, changed, now);
            }

            pass.Committed = changed.Count;
            pass.Satisfied = Satisfied(pass.Planned);
            LastPass = pass;
            return changed.Count;
        }

        /// <summary>
        /// Whose turn it is: bodies with something still wanted, least recently acted first, then
        /// by id. Ordinal rather than however the registry enumerates, because with one shared
        /// purse of people between them the order two bodies are reached in would otherwise decide
        /// which of them got anybody.
        /// </summary>
        private List<Organization> Whose(OrganizationActivityBudget budget, GameTime now)
        {
            List<Organization> due = new List<Organization>();
            foreach (KeyValuePair<EntityId, Organization> pair in _world.Registry.Organizations)
            {
                Organization body = pair.Value;
                if (body != null && now.TotalDays > body.LastActedAt.TotalDays && HasActiveGoal(body))
                {
                    due.Add(body);
                }
            }

            due.Sort(ByTurn);

            int most = budget.MostBodiesPerPass < 1 ? 1 : budget.MostBodiesPerPass;
            if (due.Count > most)
            {
                due.RemoveRange(most, due.Count - most);
            }

            return due;
        }

        private static int ByTurn(Organization a, Organization b)
        {
            int turn = a.LastActedAt.CompareTo(b.LastActedAt);
            return turn != 0 ? turn : string.CompareOrdinal(a.Id.Value, b.Id.Value);
        }

        private static bool HasActiveGoal(Organization body)
        {
            for (int i = 0; i < body.Goals.Count; i++)
            {
                if (body.Goals[i] != null && body.Goals[i].IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Plans this body's ends, performs the institutional ones as it goes, and hands the
        /// deeds to the batch.
        ///
        /// Bookkeeping happens here rather than being deferred, so the next end is planned against
        /// the reserves the last one actually left - which is the whole of "resources cannot be
        /// spent twice". The people are held the same way: a member handed one operation is not
        /// offered for another, by this body or by any other body in the pass.
        /// </summary>
        private void Plan(
            OrganizationActivityPass pass,
            OrganizationActivityBudget budget,
            ProductionCycleLedger ledger,
            Organization body,
            List<EntityId> spentMembers,
            List<Delegation> delegated,
            HashSet<string> changed,
            GameTime now)
        {
            List<OrganizationGoal> ends = new List<OrganizationGoal>();
            for (int i = 0; i < body.Goals.Count; i++)
            {
                OrganizationGoal goal = body.Goals[i];

                // Active, not merely unsatisfied. A goal can end without its condition holding -
                // given up when nothing presses any more, or handed to the end that replaced it -
                // and acting on one of those would have the body chasing what it stopped wanting.
                if (goal != null && goal.IsActive)
                {
                    ends.Add(goal);
                }
            }

            ends.Sort(ByWeight);

            int taken = 0;
            for (int i = 0; i < ends.Count && taken < budget.MostOperationsPerBody; i++)
            {
                OrganizationGoal goal = ends[i];
                OrganizationOperationPlan plan = OrganizationOperations.Plan(
                    _world, _vanilla, _registry, body, goal, spentMembers, body.Wealth);

                if (!plan.IsSupported)
                {
                    pass.Refusals.Add(body.Id.Value + " " + goal.Kind + ": " + plan.Refusal);
                    continue;
                }

                if (plan.Operation.Means == OrganizationMeans.Delegated)
                {
                    if (delegated.Count >= budget.MostDelegationsPerPass)
                    {
                        // This body has had its turn; what is left of its ends waits for the next
                        // pass, as an actor's do.
                        return;
                    }

                    if (Direct(pass, budget, ledger, body, plan, delegated, now))
                    {
                        Spend(spentMembers, plan.Operation.Agent);
                        taken++;
                    }

                    continue;
                }

                OrganizationOperation operation = plan.Operation;
                pass.Planned.Add(operation);
                if (Perform(body, operation, now))
                {
                    changed.Add(body.Id.Value);
                }

                Spend(spentMembers, operation.Agent);
                Spend(spentMembers, RecruitOf(operation));
                taken++;
            }
        }

        /// <summary>
        /// Puts every way of going about one end in front of the batch.
        ///
        /// They share one contest - the cart, the person, the shortage - so the batch ranks them
        /// against each other and against everybody else reaching for the same thing, revalidates
        /// the best against the world as it is at that moment, and stops the moment one of them
        /// finishes it. Which way a member takes is not this owner's decision to make twice.
        /// </summary>
        private bool Direct(
            OrganizationActivityPass pass,
            OrganizationActivityBudget budget,
            ProductionCycleLedger ledger,
            Organization body,
            OrganizationOperationPlan plan,
            List<Delegation> delegated,
            GameTime now)
        {
            bool any = false;
            for (int i = 0; i < plan.Operations.Count; i++)
            {
                if (delegated.Count >= budget.MostDelegationsPerPass)
                {
                    break;
                }

                OrganizationOperation operation = plan.Operations[i];
                ActionCandidate candidate = Candidate(operation, operation.Goal, now);

                // An opening a committed attempt or an observed native outcome already took is not
                // a contest anybody can win, so it is dropped where it is visible rather than
                // offered and refused. This is also what stops a reload repeating an operation the
                // save already carries the result of.
                if (candidate.Contest.IsExclusive && ledger.IsSpent(candidate.Contest.Key))
                {
                    if (!pass.OpeningsSkipped.Contains(candidate.Contest.Key))
                    {
                        pass.OpeningsSkipped.Add(candidate.Contest.Key);
                    }

                    continue;
                }

                pass.Planned.Add(operation);
                delegated.Add(new Delegation(body, operation, candidate));
                any = true;
            }

            return any;
        }

        /// <summary>The person a recruitment would take, so no second body takes them too.</summary>
        private static EntityId RecruitOf(OrganizationOperation operation)
        {
            return string.Equals(operation.Kind, ExpandMembership, StringComparison.Ordinal)
                ? operation.Subject
                : EntityId.None;
        }

        private static void Spend(List<EntityId> spentMembers, EntityId who)
        {
            if (!who.IsNone && !spentMembers.Contains(who))
            {
                spentMembers.Add(who);
            }
        }

        private static int ByWeight(OrganizationGoal a, OrganizationGoal b)
        {
            if (a.Weight != b.Weight)
            {
                return b.Weight - a.Weight;
            }

            return string.CompareOrdinal(a.Identity, b.Identity);
        }

        // -- institutional operations -------------------------------------------------------------

        private bool Perform(Organization body, OrganizationOperation operation, GameTime now)
        {
            switch (operation.Kind)
            {
                case ExpandMembership:
                    return Enrol(body, operation, now);
                case BuildReserves:
                    return Collect(body, operation, now);
                case ProtectHolding:
                    return Fortify(body, operation, now);
                case RaidOrganization:
                    return Strike(body, operation, now);
                default:
                    return false;
            }
        }

        private bool Enrol(Organization body, OrganizationOperation operation, GameTime now)
        {
            NarrativeNpc recruit = _world.Registry.GetNpc(operation.Subject);
            if (recruit == null)
            {
                return false;
            }

            body.Wealth = LocalDemandPressure.Clamp(body.Wealth - operation.ReserveCost, 0, 100);
            body.MemberIds.Add(recruit.Id);
            if (!recruit.OrganizationIds.Contains(body.Id))
            {
                recruit.OrganizationIds.Add(body.Id);
            }

            operation.Goal.Progress += 50;
            Close(body, operation, now);

            _world.Record(
                WorldEventType.OrganizationActed,
                body.Id,
                body.Id,
                now,
                0.4,
                Primary(body),
                new[] { recruit.Id },
                tags: new[] { ExpandMembership, "member_recruited" });
            return true;
        }

        private bool Collect(Organization body, OrganizationOperation operation, GameTime now)
        {
            body.Wealth = LocalDemandPressure.Clamp(
                body.Wealth + OrganizationOperations.ReserveYield, 0, 100);
            operation.Goal.Progress += OrganizationOperations.ReserveYield;
            Close(body, operation, now);

            _world.Record(
                WorldEventType.OrganizationActed,
                body.Id,
                body.Id,
                now,
                0.3,
                operation.Subject,
                new[] { operation.Subject },
                tags: new[] { BuildReserves, "wealth_changed" });
            return true;
        }

        private bool Fortify(Organization body, OrganizationOperation operation, GameTime now)
        {
            NarrativeSite site = _world.Registry.GetSite(operation.Subject);
            if (site == null)
            {
                return false;
            }

            site.DangerLevel = LocalDemandPressure.Clamp(site.DangerLevel + 1 + body.Aggression / 50, 0, 100);
            operation.Goal.Progress += 25;
            Close(body, operation, now);

            _world.Record(
                WorldEventType.OrganizationActed,
                body.Id,
                body.Id,
                now,
                0.35,
                site.Id,
                new[] { site.Id },
                tags: new[] { ProtectHolding, "site_fortified" });
            return true;
        }

        private bool Strike(Organization body, OrganizationOperation operation, GameTime now)
        {
            Organization target = _world.Registry.GetOrganization(operation.Subject);
            if (target == null)
            {
                return false;
            }

            int damage = 2 + body.Aggression / 25;
            target.Wealth = LocalDemandPressure.Clamp(target.Wealth - damage, 0, 100);
            body.Wealth = LocalDemandPressure.Clamp(body.Wealth - operation.ReserveCost, 0, 100);
            operation.Goal.Progress += 25 + body.Aggression / 10;
            Close(body, operation, now);

            RelationshipEdge targetStanding = _world.Relationships.Find(target.Id, body.Id)
                                              ?? _world.Relationships.Connect(target.Id, body.Id, RelationKind.Rival, 0);
            targetStanding.Sentiment = LocalDemandPressure.Clamp(targetStanding.Sentiment - 20, -100, 100);
            targetStanding.Kind = targetStanding.Sentiment <= -40 ? RelationKind.Enemy : RelationKind.Rival;

            RelationshipEdge raiderStanding = _world.Relationships.Find(body.Id, target.Id)
                                              ?? _world.Relationships.Connect(body.Id, target.Id, RelationKind.Rival, 0);
            raiderStanding.Sentiment = LocalDemandPressure.Clamp(raiderStanding.Sentiment - 8, -100, 100);
            raiderStanding.Kind = raiderStanding.Sentiment <= -60 ? RelationKind.Enemy : RelationKind.Rival;

            _world.Record(
                WorldEventType.OrganizationActed,
                operation.Agent,
                target.Id,
                now,
                0.5,
                Primary(target),
                new[] { body.Id, target.Id },
                tags: new[] { RaidOrganization, "standing_changed", "wealth_damaged" });
            return true;
        }

        // -- delegated deeds ----------------------------------------------------------------------

        private ActionCandidate Candidate(OrganizationOperation operation, OrganizationGoal goal, GameTime now)
        {
            GoalRoute route = operation.Route;
            ActionIntent intent = new ActionIntent(
                operation.Agent, route.Action.Id, route.Target, operation.OrganizationId.Value + ": " + route.Because)
            {
                SubjectFact = route.SubjectFact,
                SubjectItem = route.SubjectItem
            };

            // The body's own weight for the end, on the same scale an actor's want is handed over
            // on, so a body and a person reaching for the same thing are ranked against each other
            // rather than one of them being privileged.
            return ActionCandidate.For(_registry, intent, goal.Weight / 100.0, now);
        }

        /// <summary>
        /// One batch for every deed the pass gathered, keyed on the day, so two bodies reaching
        /// for the same object are settled against each other rather than in the order they were
        /// walked (BQa-015).
        /// </summary>
        private void Settle(
            OrganizationActivityPass pass,
            ProductionCycleLedger ledger,
            List<Delegation> delegated,
            HashSet<string> changed,
            GameTime now)
        {
            List<ActionCandidate> candidates = new List<ActionCandidate>();
            for (int i = 0; i < delegated.Count; i++)
            {
                candidates.Add(delegated[i].Candidate);
            }

            ActorContextEnvironment environment = new ActorContextEnvironment(
                _world, _vanilla, _checks, _world.Rng, Observation);

            ArbitrationResult result = ArbitrationBatch
                .Gather("bqa019|day|" + pass.Day, candidates)
                .Resolve(_registry, environment, _world.Rng, Cancellation);

            pass.Arbitration = result;

            for (int i = 0; i < result.Decisions.Count; i++)
            {
                ArbitrationDecision decision = result.Decisions[i];
                if (!decision.Committed)
                {
                    continue;
                }

                Delegation delegation = Find(delegated, decision.Candidate);
                if (delegation == null)
                {
                    continue;
                }

                Organization body = delegation.Body;
                OrganizationOperation operation = delegation.Operation;
                if (operation.Goal.Lifecycle == GoalLifecycle.Satisfied)
                {
                    continue;
                }

                operation.Goal.Progress += 25;
                Close(body, operation, now);
                changed.Add(body.Id.Value);

                _world.Record(
                    WorldEventType.OrganizationActed,
                    operation.Agent,
                    body.Id,
                    now,
                    0.45,
                    Primary(body),
                    new[] { body.Id, operation.Subject },
                    tags: operation.Effects.Count > 0
                        ? new[] { operation.Kind, "member_acted", operation.Effects[0] }
                        : new[] { operation.Kind, "member_acted" });

                if (decision.Contest.IsExclusive
                    && ledger.Spend(decision.Contest.Key, decision.Actor, now, ConsumedOpening.Committed) != null)
                {
                    pass.OpeningsSpent.Add(decision.Contest.Key);
                }
            }
        }

        private static Delegation Find(List<Delegation> delegated, ActionCandidate candidate)
        {
            for (int i = 0; i < delegated.Count; i++)
            {
                if (ReferenceEquals(delegated[i].Candidate, candidate))
                {
                    return delegated[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Ends this pass brought about. Counted by identity rather than by operation, because an
        /// end the pass offered three ways is still one end.
        /// </summary>
        private static int Satisfied(List<OrganizationOperation> planned)
        {
            List<string> ends = new List<string>();
            for (int i = 0; i < planned.Count; i++)
            {
                OrganizationGoal goal = planned[i].Goal;
                if (goal.Lifecycle == GoalLifecycle.Satisfied && !ends.Contains(goal.Identity))
                {
                    ends.Add(goal.Identity);
                }
            }

            return ends.Count;
        }

        // -- what closes an end -------------------------------------------------------------------

        /// <summary>
        /// Asks the world whether the end came about, now that the body's own operation has
        /// happened.
        ///
        /// The asking is legitimate exactly here and nowhere else: the body sent somebody, they
        /// came back, and what they did is the body's to know. An end with no machine-readable
        /// condition has nothing better to ask about than the effort counter, which is what it
        /// always used, and an end whose condition nothing can read stays open rather than being
        /// guessed either way.
        /// </summary>
        private void Close(Organization body, OrganizationOperation operation, GameTime now)
        {
            OrganizationGoal goal = operation.Goal;
            if (!goal.HasCondition)
            {
                if (goal.Progress >= 100)
                {
                    goal.Satisfy(now, "condition_met");
                }

                return;
            }

            if (goal.Evaluate(_world) == GoalConditionState.Met)
            {
                goal.Satisfy(now, "condition_met");
            }
        }

        private static EntityId Primary(Organization body)
        {
            return body.SiteIds.Count == 0 ? EntityId.None : body.SiteIds[0];
        }

        /// <summary>One deed the pass handed to the batch, and whose it was.</summary>
        private sealed class Delegation
        {
            internal Delegation(Organization body, OrganizationOperation operation, ActionCandidate candidate)
            {
                Body = body;
                Operation = operation;
                Candidate = candidate;
            }

            internal Organization Body { get; }

            internal OrganizationOperation Operation { get; }

            internal ActionCandidate Candidate { get; }
        }
    }
}
