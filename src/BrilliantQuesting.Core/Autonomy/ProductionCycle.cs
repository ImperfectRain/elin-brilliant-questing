using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Autonomy
{
    /// <summary>
    /// How much one pass may do (BQa-016).
    ///
    /// Every bound is a count of work, never a wall-clock budget, because the same pass has to
    /// cost the same on a phone and in a test. Nothing here is a quality knob: raising a bound
    /// lets the cycle reach more of the world per day, it never makes an actor want something
    /// harder. Work past a bound is late rather than lost - the records rotate, and an actor who
    /// was not reached keeps the older <see cref="NarrativeNpc.LastSimulatedAt"/> that puts them
    /// at the front of the next pass.
    /// </summary>
    public sealed class ProductionCycleBudget
    {
        /// <summary>Records the catch-all rotation reads per pass, for changes nobody announced.</summary>
        public int MostRecordsInspected { get; set; } = 24;

        /// <summary>People whose own reading of the world is derived per pass.</summary>
        public int MostActorsPerPass { get; set; } = 12;

        /// <summary>People the cold rotation contributes to that slice.</summary>
        public int MostColdActorsPerPass { get; set; } = 2;

        /// <summary>
        /// How many times the actor slice the pass is chosen from over-samples the slice itself.
        ///
        /// The simulation index hands out a window rather than the whole town, which is what keeps
        /// a pass from costing what the save's population costs. Sampling a few windows' worth and
        /// then choosing the least recently considered inside it is what makes the choice rest on
        /// the persisted turn marker rather than on where the rotation happened to be - so a
        /// reload, which rebuilds the rotation from the front, converges on the same people
        /// instead of starting the round again.
        /// </summary>
        public int TurnSampling { get; set; } = 4;

        /// <summary>Wants of one actor that are searched for routes.</summary>
        public int MostGoalsPerActor { get; set; } = 3;

        /// <summary>Intentions one actor may put into the batch.</summary>
        public int MostIntentionsPerActor { get; set; } = 2;

        /// <summary>
        /// Intentions the whole batch may hold.
        ///
        /// This is the execution bound, and it is on the batch's input rather than on its
        /// executions because the input is the thing the cycle owns: arbitration runs each
        /// gathered intention at most once, so a bounded batch is a bounded pass without the
        /// cycle reaching into how arbitration walks it.
        /// </summary>
        public int MostIntentionsPerPass { get; set; } = 8;
    }

    /// <summary>What one pass read, decided and did. Derived, transient, for people and tests.</summary>
    public sealed class ProductionCyclePass
    {
        private static readonly IReadOnlyList<EntityId> Nobody = new EntityId[0];
        private static readonly IReadOnlyList<GoalChange> NoChanges = new GoalChange[0];
        private static readonly IReadOnlyList<string> NoKeys = new string[0];

        internal ProductionCyclePass(GameTime at)
        {
            At = at;
            Day = at.TotalDays;
            Evaluated = Nobody;
            GoalChanges = NoChanges;
            OpeningsSpent = NoKeys;
            OpeningsSkipped = NoKeys;
            Refusal = string.Empty;
        }

        public GameTime At { get; }

        /// <summary>The interval this pass is for. The batch boundary is one day.</summary>
        public long Day { get; }

        /// <summary>Whether the pass ran. False is an answer, and <see cref="Refusal"/> says which.</summary>
        public bool Ran { get; internal set; }

        /// <summary>Why nothing ran, or empty when something did.</summary>
        public string Refusal { get; internal set; }

        /// <summary>Supplied native outcomes that became history. Never rolled again.</summary>
        public int ObservationsRecorded { get; internal set; }

        /// <summary>Records the catch-all rotation actually read.</summary>
        public int RecordsInspected { get; internal set; }

        /// <summary>The bounded work set this pass was read against.</summary>
        public DevelopmentScope Scope { get; internal set; }

        /// <summary>Objective conditions the work set produced.</summary>
        public int DevelopmentsRead { get; internal set; }

        /// <summary>People whose own reading was derived, in the order they were taken.</summary>
        public IReadOnlyList<EntityId> Evaluated { get; internal set; }

        /// <summary>Actor-local readings across every evaluated person.</summary>
        public int PressuresRead { get; internal set; }

        /// <summary>Everything goal evolution did this pass.</summary>
        public IReadOnlyList<GoalChange> GoalChanges { get; internal set; }

        /// <summary>Intentions put into the batch.</summary>
        public int IntentionsGathered { get; internal set; }

        /// <summary>The batch's own result, or null when nothing was gathered.</summary>
        public ArbitrationResult Arbitration { get; internal set; }

        /// <summary>Indivisible openings this pass closed, in the order it closed them.</summary>
        public IReadOnlyList<string> OpeningsSpent { get; internal set; }

        /// <summary>Intentions refused because their opening had already been spent.</summary>
        public IReadOnlyList<string> OpeningsSkipped { get; internal set; }

        /// <summary>Attempts that changed the world.</summary>
        public int Committed { get; internal set; }

        public override string ToString()
        {
            if (!Ran)
            {
                return "day " + Day + ": no pass (" + Refusal + ")";
            }

            return "day " + Day + ": " + DevelopmentsRead + " condition(s), " + Evaluated.Count
                + " actor(s), " + GoalChanges.Count + " goal change(s), " + IntentionsGathered
                + " intention(s), " + Committed + " committed";
        }
    }

    /// <summary>
    /// One bounded causal pass over the world, coordinating the owners that already exist
    /// (BQa-016).
    ///
    /// <code>
    /// supplied authoritative observations
    ///   -> bounded affected work set        (PressureFeedback)
    ///   -> objective conditions             (DevelopmentDetector)
    ///   -> actor-local interpretation       (ActorPressureView)
    ///   -> goal evolution                   (ActorGoalEvolution)
    ///   -> feasible intentions              (GoalRoutes)
    ///   -> opportunity and arbitration      (ArbitrationBatch)
    ///   -> attempts and consequences        (ActionAttempt, the attached ConsequenceEngine)
    ///   -> changed state for the next pass  (back into PressureFeedback)
    /// </code>
    ///
    /// <b>It decides nothing of its own.</b> Every judgement in that list belongs to the owner
    /// named beside it, and this type holds none of them: it does not score a pressure, form a
    /// want, rank a contender or resolve a check. What it owns is the four things none of those
    /// owners could own alone - where a pass starts and ends, how much work it may do, whose turn
    /// it is, and which openings are already gone.
    ///
    /// <b>The batch boundary is one day.</b> A cycle consumes an interval, and
    /// <see cref="ProductionCycleLedger"/> remembers which, so a host that fires the same day
    /// twice gets one pass. That is what makes replay harmless rather than merely unlikely, and it
    /// is why a reload onto the same morning does not re-run the morning.
    ///
    /// <b>Execution ownership.</b> Attempts run inside <see cref="ArbitrationBatch"/> and nowhere
    /// else here, so a contested opening is settled once against the world as it is at that
    /// moment. Consequences stay with whoever already owns them; this pass writes no fact, no
    /// event and no thread of its own.
    ///
    /// <b>No avalanche on one stack.</b> The consequence engine and the feedback collector are
    /// immediate listeners, and an attempt made inside a pass appends events they both see.
    /// Re-entering <see cref="Run"/> from inside that reaction would let one theft start a second
    /// pressure pass on the same stack, so a re-entrant call is refused outright and its changes
    /// wait for the next interval - which is what "queued reaction" has to mean if the phrase is
    /// to mean anything.
    ///
    /// <b>Headless.</b> Nothing here touches Elin. <see cref="IVanillaState"/> is asked what the
    /// build can carry, exactly as the existing off-screen owners ask it, and supplied
    /// observations are written down through <see cref="VanillaActionRecorder"/> rather than
    /// simulated - a native outcome that already happened is history, not a candidate. BQa-017
    /// supplies the live host; a green pass here is not evidence that any Elin hook advances it.
    /// </summary>
    public sealed class ProductionCycle
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;
        private readonly ICheckResolver _checks;
        private readonly ActionRegistry _registry;
        private readonly VanillaActionRecorder _recorder;
        private bool _running;

        public ProductionCycle(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            ActionRegistry registry)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _vanilla = vanilla ?? throw new ArgumentNullException(nameof(vanilla));
            _checks = checks ?? throw new ArgumentNullException(nameof(checks));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _recorder = new VanillaActionRecorder(world, vanilla);
            Feedback = new PressureFeedback(world);
        }

        /// <summary>The work-set collector this cycle reads from. Attach it after loading.</summary>
        public PressureFeedback Feedback { get; }

        public ProductionCycleBudget Budget { get; set; } = new ProductionCycleBudget();

        /// <summary>
        /// A host's own stop, asked before each execution in the batch.
        /// 
        /// Null by default and deliberately not the work budget: the budget is a count this cycle
        /// applies to its own input, while this is somewhere a live host can say "not this frame".
        /// Stopping releases whatever the batch held rather than stranding it (BQa-015).
        /// </summary>
        public IBatchCancellation Cancellation { get; set; }

        /// <summary>How the batch reads the world. Off-screen unless a host says otherwise.</summary>
        public ContextObservation Observation { get; set; } = ContextObservation.OffScreen;

        /// <summary>The last pass, for an inspector. Transient.</summary>
        public ProductionCyclePass LastPass { get; private set; }

        /// <summary>
        /// Start collecting changes. Idempotent, and to be called after a load for the reason
        /// <see cref="PressureFeedback.Attach"/> gives: restored events are not dispatched, so a
        /// listener added afterwards sees new changes only and a reload is not a replay.
        /// </summary>
        public void Attach()
        {
            Feedback.Attach();
        }

        /// <summary>
        /// Runs the interval <paramref name="now"/> falls in, once.
        ///
        /// <paramref name="observations"/> are native outcomes the host already saw. They are
        /// written down and their indivisible openings are closed, so nothing this pass gathers
        /// can try to perform them a second time. Supplying none is ordinary: most days the game
        /// did nothing this simulation needed to hear about.
        /// </summary>
        public ProductionCyclePass Run(GameTime now, IReadOnlyList<ObservedVanillaAction> observations = null)
        {
            ProductionCyclePass pass = new ProductionCyclePass(now);
            ProductionCycleLedger ledger = _world.ProductionCycle;

            if (_running)
            {
                pass.Refusal = "a pass is already running; a reaction to it may not start another";
                LastPass = pass;
                return pass;
            }

            if (ledger.HasConsumed(pass.Day))
            {
                pass.Refusal = "day " + pass.Day + " has already been consumed";
                LastPass = pass;
                return pass;
            }

            _running = true;
            try
            {
                Perform(pass, ledger, now, observations);
            }
            finally
            {
                _running = false;
            }

            ledger.LastConsumedDay = pass.Day;
            pass.Ran = true;
            LastPass = pass;
            return pass;
        }

        private void Perform(
            ProductionCyclePass pass,
            ProductionCycleLedger ledger,
            GameTime now,
            IReadOnlyList<ObservedVanillaAction> observations)
        {
            ProductionCycleBudget budget = Budget ?? new ProductionCycleBudget();
            List<string> spent = new List<string>();
            List<string> skipped = new List<string>();

            TakeObservations(pass, ledger, now, observations, spent);

            // The catch-all first, so a condition that changed with time rather than with an
            // event is in the same work set as the ones history announced.
            pass.RecordsInspected = Feedback.Inspect(budget.MostRecordsInspected);
            PressurePass work = Feedback.Take();
            pass.Scope = work.Scope;

            IReadOnlyList<Development> objective = DevelopmentDetector.Detect(_world, work.Scope);
            pass.DevelopmentsRead = objective.Count;

            List<NarrativeNpc> actors = Whose(work, budget, now);
            List<EntityId> evaluated = new List<EntityId>();
            List<GoalChange> changes = new List<GoalChange>();
            List<ActionCandidate> candidates = new List<ActionCandidate>();
            int pressures = 0;

            for (int i = 0; i < actors.Count; i++)
            {
                NarrativeNpc actor = actors[i];
                evaluated.Add(actor.Id);

                // Taken as considered whether or not anything came of it. An actor who was read
                // and had nothing to say has had their turn; leaving them at the front of the
                // queue would starve everybody behind them.
                actor.LastSimulatedAt = now;

                IReadOnlyList<ActorLocalPressure> readings =
                    ActorPressureView.Of(_world, actor.Id, objective, _vanilla);
                pressures += readings.Count;

                changes.AddRange(ActorGoalEvolution.Advance(_world, actor.Id, readings, now));

                if (candidates.Count < budget.MostIntentionsPerPass)
                {
                    Gather(actor, ledger, budget, now, candidates, skipped);
                }
            }

            pass.Evaluated = evaluated;
            pass.PressuresRead = pressures;
            pass.GoalChanges = changes;
            pass.IntentionsGathered = candidates.Count;

            if (candidates.Count > 0)
            {
                Settle(pass, ledger, now, candidates, spent);
            }

            pass.OpeningsSpent = spent;
            pass.OpeningsSkipped = skipped;
        }

        /// <summary>
        /// Writes down what the game already did, and closes what it already took.
        ///
        /// Recording is the whole of it: the recorder mints the same ledger entries a procedural
        /// verb would, no check is rolled and no verb is performed, because the outcome is not in
        /// question - it happened. Closing the opening is what stops this pass from offering the
        /// purse Elin has already moved.
        /// </summary>
        private void TakeObservations(
            ProductionCyclePass pass,
            ProductionCycleLedger ledger,
            GameTime now,
            IReadOnlyList<ObservedVanillaAction> observations,
            List<string> spent)
        {
            if (observations == null)
            {
                return;
            }

            for (int i = 0; i < observations.Count; i++)
            {
                ObservedVanillaAction observed = observations[i];
                if (observed == null || _recorder.Record(observed) == null)
                {
                    continue;
                }

                pass.ObservationsRecorded++;

                if (observed.Item.IsNone)
                {
                    continue;
                }

                ActionContest taken = ActionContest.Exclusive(ContestedThing.Object, observed.Item);
                if (ledger.Spend(taken.Key, observed.Actor, now, ConsumedOpening.Observed) != null)
                {
                    spent.Add(taken.Key);
                }
            }
        }

        /// <summary>
        /// Whose turn it is: everybody a change named, then the least recently considered.
        ///
        /// Woken first, because somebody whose shop burned down this morning should not wait for
        /// the rotation to come round - and being woken is not being informed, since what any of
        /// them can legitimately make of it stays <see cref="ActorPressureView"/>'s answer. The
        /// rest fill the slice in <see cref="NarrativeNpc.LastSimulatedAt"/> order, which is the
        /// persisted marker rather than the rotation's position - the index hands out a window, and
        /// the window is where the round happens to be, which a load rebuilds from the front.
        /// Sampling several windows and choosing the oldest inside them is what makes resume rest
        /// on the save instead of on the rotation; the rotation's job is to keep the sample
        /// bounded, not to decide whose turn it is. Newly eligible people with no goals at all are
        /// in both tiers on the same terms as everybody else.
        /// </summary>
        private List<NarrativeNpc> Whose(PressurePass work, ProductionCycleBudget budget, GameTime now)
        {
            List<NarrativeNpc> taken = new List<NarrativeNpc>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            List<NarrativeNpc> woken = new List<NarrativeNpc>();
            for (int i = 0; i < work.Actors.Count; i++)
            {
                NarrativeNpc actor = _world.Registry.GetNpc(work.Actors[i]);
                if (Eligible(actor) && seen.Add(actor.Id.Value))
                {
                    woken.Add(actor);
                }
            }

            woken.Sort(ById);
            Fill(taken, woken, budget.MostActorsPerPass);

            if (taken.Count >= budget.MostActorsPerPass)
            {
                return taken;
            }

            int sampling = budget.TurnSampling < 1 ? 1 : budget.TurnSampling;
            List<NarrativeNpc> rotation = new List<NarrativeNpc>();
            foreach (NarrativeNpc actor in _world.Registry.TakeSimulationActors(
                budget.MostActorsPerPass * sampling, budget.MostColdActorsPerPass * sampling))
            {
                if (Eligible(actor) && seen.Add(actor.Id.Value))
                {
                    rotation.Add(actor);
                }
            }

            rotation.Sort(ByTurn);
            Fill(taken, rotation, budget.MostActorsPerPass);
            return taken;
        }

        private static void Fill(List<NarrativeNpc> taken, List<NarrativeNpc> from, int most)
        {
            for (int i = 0; i < from.Count && taken.Count < most; i++)
            {
                taken.Add(from[i]);
            }
        }

        private static int ById(NarrativeNpc a, NarrativeNpc b)
        {
            return string.CompareOrdinal(a.Id.Value, b.Id.Value);
        }

        private static int ByTurn(NarrativeNpc a, NarrativeNpc b)
        {
            int turn = a.LastSimulatedAt.CompareTo(b.LastSimulatedAt);
            return turn != 0 ? turn : ById(a, b);
        }

        private bool Eligible(NarrativeNpc actor)
        {
            return actor != null
                && actor.IsCanonical
                && actor.Alive
                && actor.Id != _vanilla.PlayerId
                && _vanilla.IsAlive(actor.Id)
                && !_world.Absences.IsAbsent(actor.Id);
        }

        /// <summary>
        /// This actor's wants, turned into intentions the batch can rank.
        ///
        /// Routes come from <see cref="GoalRoutes"/>, which already asks the build what it can
        /// carry, so nothing unattemptable reaches the batch. Two rules beyond that, and both are
        /// fairness rather than judgement.
        ///
        /// Wants are taken in weight order but a round at a time, so a want that happens to have
        /// twelve registered routes cannot spend an actor's whole allowance and leave their other
        /// wants unrepresented. Which of the offered intentions is worth anything is arbitration's
        /// question; this only has to make sure it is asked about more than one want.
        ///
        /// And an intention whose indivisible opening has already been spent is dropped here
        /// rather than refused later, because a contest nobody can win is not a contest - with
        /// <see cref="ProductionCyclePass.OpeningsSkipped"/> keeping the drop visible.
        /// </summary>
        private void Gather(
            NarrativeNpc actor,
            ProductionCycleLedger ledger,
            ProductionCycleBudget budget,
            GameTime now,
            List<ActionCandidate> candidates,
            List<string> skipped)
        {
            List<NpcGoal> goals = new List<NpcGoal>();
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                if (actor.Goals[i] != null && actor.Goals[i].IsActive)
                {
                    goals.Add(actor.Goals[i]);
                }
            }

            goals.Sort(ByWeight);

            List<GoalRouteSearch> searches = new List<GoalRouteSearch>();
            List<NpcGoal> searched = new List<NpcGoal>();
            int deepest = 0;
            for (int g = 0; g < goals.Count && g < budget.MostGoalsPerActor; g++)
            {
                GoalRouteSearch search = GoalRoutes.Discover(_world, _vanilla, _registry, actor, goals[g]);
                if (!search.HasRoutes)
                {
                    continue;
                }

                searches.Add(search);
                searched.Add(goals[g]);
                deepest = Math.Max(deepest, search.Routes.Count);
            }

            int mine = 0;
            for (int round = 0; round < deepest; round++)
            {
                for (int g = 0; g < searches.Count; g++)
                {
                    if (mine >= budget.MostIntentionsPerActor
                        || candidates.Count >= budget.MostIntentionsPerPass)
                    {
                        return;
                    }

                    if (round >= searches[g].Routes.Count)
                    {
                        continue;
                    }

                    GoalRoute route = searches[g].Routes[round];
                    ActionIntent intent = new ActionIntent(actor.Id, route.Action.Id, route.Target, route.Because)
                    {
                        SubjectFact = route.SubjectFact,
                        SubjectItem = route.SubjectItem
                    };

                    ActionCandidate candidate = ActionCandidate.For(
                        _registry, intent, searched[g].Weight / 100.0, now);
                    if (candidate.Contest.IsExclusive && ledger.IsSpent(candidate.Contest.Key))
                    {
                        skipped.Add(candidate.Contest.Key);
                        continue;
                    }

                    candidates.Add(candidate);
                    mine++;
                }
            }
        }

        private static int ByWeight(NpcGoal a, NpcGoal b)
        {
            if (a.Weight != b.Weight)
            {
                return b.Weight - a.Weight;
            }

            return string.CompareOrdinal(a.Identity, b.Identity);
        }

        /// <summary>
        /// One batch, keyed on the interval it belongs to.
        ///
        /// The key is part of every tie-break, so a contest that comes level again tomorrow is
        /// not settled the same way it was today - ties move with time rather than with whatever
        /// order the candidates were gathered in.
        /// </summary>
        private void Settle(
            ProductionCyclePass pass,
            ProductionCycleLedger ledger,
            GameTime now,
            List<ActionCandidate> candidates,
            List<string> spent)
        {
            ActorContextEnvironment environment = new ActorContextEnvironment(
                _world, _vanilla, _checks, _world.Rng, Observation);

            ArbitrationResult result = ArbitrationBatch
                .Gather("bqa016|day|" + pass.Day, candidates)
                .Resolve(_registry, environment, _world.Rng, Cancellation);

            pass.Arbitration = result;

            IReadOnlyList<ArbitrationDecision> committed = result.Committed;
            pass.Committed = committed.Count;

            for (int i = 0; i < committed.Count; i++)
            {
                ArbitrationDecision decision = committed[i];
                if (!decision.Contest.IsExclusive)
                {
                    continue;
                }

                if (ledger.Spend(decision.Contest.Key, decision.Actor, now, ConsumedOpening.Committed) != null)
                {
                    spent.Add(decision.Contest.Key);
                }
            }
        }
    }
}
