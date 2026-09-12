using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-015. Several people reaching for one thing, and what decides which of them gets it.
    ///
    /// Every test here is written against the thing the step exists to remove: an outcome decided
    /// by the order a collection happened to be walked in. So the assertions are mostly of the
    /// form "change the motive, the hour, the place or the roll and the winner changes; change
    /// nothing but the input order and it does not".
    /// </summary>
    public class ActionArbitrationTests
    {
        private static readonly EntityId Lysa = EntityId.Parse("npc_lysa");
        private static readonly EntityId Maren = EntityId.Parse("npc_maren");
        private static readonly EntityId Orrin = EntityId.Parse("npc_orrin");
        private static readonly EntityId Victim = EntityId.Parse("npc_jorin");
        private static readonly EntityId Reeve = EntityId.Parse("npc_herrick");
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Valley = EntityId.Parse("zone_valley");
        private static readonly EntityId Purse = EntityId.Parse("item_purse");

        // -- what is contested, and whether anybody can lose it -------------------------------

        [Fact]
        public void AVerbThatMovesAnObjectContestsThatObjectExclusively()
        {
            Bench bench = Bench.Create();
            ActionContest contest = ActionContest.ForIntent(bench.Actions.Get("pickpocket"), bench.Lift(Lysa));

            Assert.Equal(ContestedThing.Object, contest.Over);
            Assert.Equal(Purse, contest.Subject);
            Assert.True(contest.IsExclusive);
            Assert.Equal("object|" + Purse.Value, contest.Key);
        }

        [Fact]
        public void AVerbThatOnlyTellsSomebodyContestsNothingExclusively()
        {
            Bench bench = Bench.Create();
            ActionIntent telling = new ActionIntent(Lysa, "report", Reeve, "somebody should know")
            {
                SubjectFact = bench.TheftFact
            };

            ActionContest contest = ActionContest.ForIntent(bench.Actions.Get("report"), telling);

            Assert.True(contest.IsSomething);
            Assert.False(contest.IsExclusive);
            Assert.DoesNotContain(SemanticEffects.InformationDisclosed, ActionContest.IndivisibleEffects);
        }

        [Fact]
        public void AVerbThatHasDeclaredNothingIsNotTreatedAsScarce()
        {
            // BQa-010 reports an undeclared verb as a coverage gap. Reading scarcity out of that
            // gap would turn a reported hole into silent refusals nobody asked for.
            Bench bench = Bench.Create();
            ActionContest contest = ActionContest.ForIntent(new SilentAction(), bench.Lift(Lysa));

            Assert.True(contest.IsSomething);
            Assert.False(contest.IsExclusive);
        }

        [Fact]
        public void TwoActorsAfterOneShareableThingBothGetTheirTurn()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.Define(Reeve, zone: Town);
            bench.World.Registry.Add(Guard(Reeve));
            bench.Knows(Lysa);
            bench.Knows(Maren);

            ArbitrationResult result = bench.Resolve(
                "day-7",
                bench.Telling(Lysa, 0.6),
                bench.Telling(Maren, 0.4));

            Assert.All(
                result.Decisions,
                decision => Assert.Equal(ArbitrationVerdict.Attempted, decision.Verdict));
            Assert.Empty(result.Claims);
        }

        // -- one object, two actors ------------------------------------------------------------

        [Fact]
        public void OneObjectAndTwoActorsLeavesExactlyOneOfThemHoldingIt()
        {
            Bench bench = Bench.Create();

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.7), bench.Candidate(Maren, 0.7));

            Assert.Single(result.Committed);
            ArbitrationDecision winner = result.Committed[0];
            ArbitrationDecision loser = result.For(winner.Actor == Lysa ? Maren : Lysa);

            Assert.Equal(ArbitrationVerdict.Yielded, loser.Verdict);
            Assert.Equal("somebody else finished it first", loser.Why);
            Assert.Null(loser.Attempt);
            Assert.Equal(bench.Vanilla.GetInventory(winner.Actor)[0].Id, Purse);
            Assert.Empty(bench.Vanilla.GetInventory(loser.Actor));
        }

        [Fact]
        public void TheClaimIsGivenBackTheMomentTheAttemptIsOverAndNeverOutlivesTheBatch()
        {
            Bench bench = Bench.Create();

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.7), bench.Candidate(Maren, 0.7));

            Assert.Single(result.Claims);
            Assert.False(result.Claims[0].IsHeld);
            Assert.Equal("the attempt committed", result.Claims[0].ReleasedBecause);
            Assert.Equal(0, result.OutstandingClaims);
        }

        // -- the outcome follows motive, timing, opportunity and the roll ----------------------

        [Fact]
        public void WantingItMoreBeatsBeingEarlierInTheList()
        {
            Bench bench = Bench.Create();

            // The one who cares least is handed over first, which is precisely how a walked list
            // used to decide it.
            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.2), bench.Candidate(Maren, 0.9));

            Assert.Equal(Maren, result.Committed[0].Actor);
        }

        [Fact]
        public void BeingReadierSettlesAContestTwoPeopleWantEqually()
        {
            Bench bench = Bench.Create();
            GameTime now = bench.Vanilla.Now;

            ArbitrationResult result = bench.Resolve(
                "day-7",
                bench.Candidate(Lysa, 0.5, now.PlusHours(4)),
                bench.Candidate(Maren, 0.5, now.PlusHours(1)));

            Assert.Equal(Maren, result.Committed[0].Actor);
        }

        [Fact]
        public void WhatThePlaceAndTheHourAllowChangesWhoTakesIt()
        {
            Bench bench = Bench.Create();
            bench.Asleep(Maren);

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.5), bench.Candidate(Maren, 0.5));

            Assert.Equal(Lysa, result.Committed[0].Actor);
            Assert.True(result.For(Lysa).Standing > result.For(Maren).Standing);
        }

        [Fact]
        public void AContenderTheWorldWillNotHaveIsRankedLastHoweverMuchTheyWantIt()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Maren, Valley);

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Maren, 0.9), bench.Candidate(Lysa, 0.1));

            ArbitrationDecision outOfTown = result.For(Maren);
            Assert.Equal(0.0, outOfTown.Standing);
            Assert.Equal(1, outOfTown.Rank);
            Assert.Equal(Lysa, result.Committed[0].Actor);
        }

        [Fact]
        public void TheLastContenderIsStillAskedAgainAndRefusedInTheWorldsOwnWords()
        {
            // Ranked last is not dropped: a contest nobody else is left in still asks the world
            // about the one contender it has, and reports what the world said.
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Maren, Valley);

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Maren, 0.9));

            ArbitrationDecision refused = result.For(Maren);
            Assert.Equal(ArbitrationVerdict.Refused, refused.Verdict);
            Assert.Contains("not in the same place", refused.Why);
            Assert.Null(refused.Attempt);
            Assert.Empty(result.Claims);
        }

        [Fact]
        public void TheRollDecidesTheRestAndAFailedAttemptDoesNotCloseTheContest()
        {
            // The loser retry. The first contender genuinely tried and changed nothing, so the
            // purse is exactly where it was and the second contender is owed their turn.
            Bench bench = Bench.Create().WithChecks(new SequencedCheckResolver(CheckOutcome.Fail, CheckOutcome.Pass));

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.9), bench.Candidate(Maren, 0.5));

            Assert.Equal(ArbitrationVerdict.Attempted, result.For(Lysa).Verdict);
            Assert.False(result.For(Lysa).Committed);
            Assert.Equal(ArbitrationVerdict.Attempted, result.For(Maren).Verdict);
            Assert.True(result.For(Maren).Committed);

            Assert.Equal(2, result.Claims.Count);
            Assert.Equal("the attempt changed nothing", result.Claims[0].ReleasedBecause);
            Assert.Equal("the attempt committed", result.Claims[1].ReleasedBecause);
        }

        [Fact]
        public void ARefusedWinnerHandsTheContestOnInsteadOfHoldingIt()
        {
            Bench bench = Bench.Create();

            // Ranked first, and then the world moves under them before it is their turn: the
            // reading arbitration ranked on is not the reading it executes on.
            ArbitrationResult result = bench.Resolve(
                "day-7",
                new IAttemptEnvironment[] { new MovesThemOut(bench.Environment(), Lysa, bench.Vanilla) },
                bench.Candidate(Lysa, 0.9),
                bench.Candidate(Maren, 0.4));

            Assert.Equal(ArbitrationVerdict.Refused, result.For(Lysa).Verdict);
            Assert.True(result.For(Lysa).Standing > result.For(Maren).Standing);
            Assert.Equal(Maren, result.Committed[0].Actor);

            // The refused first choice never took a claim, so there was nothing to strand.
            Assert.Single(result.Claims);
            Assert.Equal(Maren, result.Claims[0].Holder);
        }

        // -- the player is a contender, not a parallel resolver --------------------------------

        [Fact]
        public void ThePlayerLosesToAnNpcWhoWantsItMoreAndWinsWhenTheyWantItMore()
        {
            Bench outbid = Bench.Create();
            ArbitrationResult npcWins = outbid.Resolve("day-7", outbid.Candidate(Player, 0.2), outbid.Candidate(Lysa, 0.9));
            Assert.Equal(Lysa, npcWins.Committed[0].Actor);
            Assert.Equal(ArbitrationVerdict.Yielded, npcWins.For(Player).Verdict);

            Bench keener = Bench.Create();
            ArbitrationResult playerWins = keener.Resolve("day-7", keener.Candidate(Player, 0.9), keener.Candidate(Lysa, 0.2));
            Assert.Equal(Player, playerWins.Committed[0].Actor);
            Assert.Equal(ArbitrationVerdict.Yielded, playerWins.For(Lysa).Verdict);
        }

        // -- the input order decides nothing ---------------------------------------------------

        [Fact]
        public void EveryOrderingOfTheSameThreeContendersDecidesTheSameWay()
        {
            ActionCandidate[][] orderings = new ActionCandidate[6][];
            EntityId[] winners = new EntityId[6];
            int[][] permutations =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
            };

            for (int p = 0; p < permutations.Length; p++)
            {
                Bench bench = Bench.Create();
                EntityId[] actors = { Lysa, Maren, Orrin };
                List<ActionCandidate> candidates = new List<ActionCandidate>();
                for (int i = 0; i < permutations[p].Length; i++)
                {
                    // Identical motive and identical readiness, so only the keyed draw is left.
                    candidates.Add(bench.Candidate(actors[permutations[p][i]], 0.5));
                }

                orderings[p] = candidates.ToArray();
                winners[p] = bench.Resolve("day-7", orderings[p]).Committed[0].Actor;
            }

            for (int p = 1; p < winners.Length; p++)
            {
                Assert.Equal(winners[0], winners[p]);
            }
        }

        [Fact]
        public void ADeadLevelContestIsSettledByTheSeedAndTheBatchRatherThanAlwaysTheSameWay()
        {
            HashSet<EntityId> bySeed = new HashSet<EntityId>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                Bench bench = Bench.Create(seed);
                bySeed.Add(bench.Resolve("day-7", bench.Candidate(Lysa, 0.5), bench.Candidate(Maren, 0.5)).Committed[0].Actor);
            }

            HashSet<EntityId> byBatch = new HashSet<EntityId>();
            for (int day = 1; day <= 12; day++)
            {
                Bench bench = Bench.Create();
                byBatch.Add(bench.Resolve("day-" + day, bench.Candidate(Lysa, 0.5), bench.Candidate(Maren, 0.5)).Committed[0].Actor);
            }

            Assert.Equal(2, bySeed.Count);
            Assert.Equal(2, byBatch.Count);
        }

        [Fact]
        public void TheBatchIsTakenWhenItIsGatheredAndNotReadAgainAfterwards()
        {
            Bench bench = Bench.Create();
            List<ActionCandidate> mutable = new List<ActionCandidate> { bench.Candidate(Lysa, 0.5) };
            ArbitrationBatch batch = ArbitrationBatch.Gather("day-7", mutable);

            mutable.Add(bench.Candidate(Maren, 0.9));

            Assert.Single(batch.Candidates);
            ArbitrationResult result = batch.Resolve(bench.Actions, bench.Environment(), bench.World.Rng);
            Assert.Single(result.Decisions);
            Assert.Equal(Lysa, result.Committed[0].Actor);
        }

        // -- ranking is a read ------------------------------------------------------------------

        [Fact]
        public void RankingSeveralContendersDoesNotMoveTheStreamTheWinnerRollsOn()
        {
            Bench alone = Bench.Create();
            ulong before = alone.World.Rng.State;
            alone.Resolve("day-7", alone.Candidate(Lysa, 0.9));
            ulong oneContender = alone.World.Rng.State;

            Bench crowded = Bench.Create();
            Assert.Equal(before, crowded.World.Rng.State);

            // Three more people weighing the same purse, all of whom lose. If ranking drew from
            // the world stream, the winner's own roll would land somewhere else entirely.
            crowded.Resolve(
                "day-7",
                crowded.Candidate(Lysa, 0.9),
                crowded.Candidate(Maren, 0.1),
                crowded.Candidate(Orrin, 0.1),
                crowded.Candidate(Player, 0.1));

            Assert.Equal(oneContender, crowded.World.Rng.State);
        }

        // -- claims expire on every ending ------------------------------------------------------

        [Fact]
        public void AVerbThatThrowsReleasesTheContestInsteadOfStrandingIt()
        {
            Bench bench = Bench.Create();
            bench.Actions.Register(new ThrowingLiftAction());

            ArbitrationResult result = bench.Resolve(
                "day-7",
                bench.Candidate(Lysa, 0.9, actionId: "lift_violently"),
                bench.Candidate(Maren, 0.4));

            Assert.Equal(ArbitrationVerdict.Faulted, result.For(Lysa).Verdict);
            Assert.Contains("the pocket came apart", result.For(Lysa).Why);
            Assert.Equal("the attempt threw", result.Claims[0].ReleasedBecause);
            Assert.Equal(0, result.OutstandingClaims);

            // And the contest stayed open, because nothing was taken.
            Assert.Equal(Maren, result.Committed[0].Actor);
        }

        [Fact]
        public void StoppingTheBatchReleasesWhatIsHeldAndRunsNobodyElse()
        {
            // Nobody takes it, so the contest stays open and the stop is what ends the batch.
            Bench bench = Bench.Create().WithChecks(new SequencedCheckResolver(CheckOutcome.Fail));
            StopAfter stop = new StopAfter(1);

            ArbitrationResult result = bench.Resolve(
                "day-7",
                new IBatchCancellation[] { stop },
                bench.Candidate(Lysa, 0.9),
                bench.Candidate(Maren, 0.5),
                bench.Candidate(Orrin, 0.1));

            Assert.Equal(0, result.OutstandingClaims);
            Assert.Equal(ArbitrationVerdict.Cancelled, result.For(Maren).Verdict);
            Assert.Equal(ArbitrationVerdict.Cancelled, result.For(Orrin).Verdict);
            Assert.Null(result.For(Orrin).Attempt);
        }

        // -- nothing transient reaches the save --------------------------------------------------

        [Fact]
        public void ABatchThatCommittedNothingLeavesTheSaveExactlyAsItWas()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Lysa, Valley);
            bench.Vanilla.SetZone(Maren, Valley);
            string before = WorldStateSerializer.Save(bench.World);

            ArbitrationResult result = bench.Resolve("day-7", bench.Candidate(Lysa, 0.9), bench.Candidate(Maren, 0.5));

            Assert.Empty(result.Committed);
            Assert.Equal(before, WorldStateSerializer.Save(bench.World));
        }

        [Fact]
        public void OnlyTheCommittedOutcomeSurvivesAReloadAtTheBatchBoundary()
        {
            Bench bench = Bench.Create();
            ArbitrationResult first = bench.Resolve("day-7", bench.Candidate(Lysa, 0.7), bench.Candidate(Maren, 0.7));
            EntityId winner = first.Committed[0].Actor;
            EntityId loser = winner == Lysa ? Maren : Lysa;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(bench.World));
            Bench resumed = bench.Resume(reloaded);

            // The same batch again, exactly as a replayed interval would present it. The theft
            // that happened is history; the claim that carried it left nothing behind to honour.
            ArbitrationResult second = resumed.Resolve("day-7", resumed.Candidate(Lysa, 0.7), resumed.Candidate(Maren, 0.7));

            Assert.Empty(second.Committed);
            Assert.Equal(ArbitrationVerdict.Refused, second.For(loser).Verdict);
            Assert.Equal(0, second.OutstandingClaims);
            Assert.Single(resumed.Vanilla.GetInventory(winner));
        }

        // -- fixtures ----------------------------------------------------------------------------

        private static NarrativeNpc Guard(EntityId who)
        {
            NarrativeNpc guard = new NarrativeNpc(who, "Herrick");
            guard.Roles.Add(AuthorityPolicy.GuardRole);
            return guard;
        }

        /// <summary>A verb nobody has declared the effects of. BQa-010 reports it as a gap.</summary>
        private sealed class SilentAction : NarrativeAction
        {
            public SilentAction() : base("undeclared_lift", ActionFamily.Crime, "Take it")
            {
            }

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context)
            {
                return new ActionOutcome(Id, null, "nothing in particular");
            }
        }

        /// <summary>A verb that means to take the purse and throws halfway through.</summary>
        private sealed class ThrowingLiftAction : NarrativeAction
        {
            public ThrowingLiftAction() : base("lift_violently", ActionFamily.Crime, "Tear it free")
            {
            }

            public override ActionEffects Effects => ActionEffects
                .Declaring(ActionEffect.Recorded(SemanticEffects.PossessionTransferred));

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context)
            {
                throw new InvalidOperationException("the pocket came apart in their hand");
            }
        }

        /// <summary>Stops the batch once <paramref name="after"/> executions have been allowed.</summary>
        private sealed class StopAfter : IBatchCancellation
        {
            private readonly int _after;
            private int _seen;

            public StopAfter(int after)
            {
                _after = after;
            }

            public bool IsCancelled => _seen++ >= _after;
        }

        /// <summary>
        /// Moves one actor out of town the first time somebody asks to execute for them - the
        /// world changing between the ranking read and the execution read, which is the only
        /// reason revalidation exists.
        /// </summary>
        private sealed class MovesThemOut : IAttemptEnvironment
        {
            private readonly IAttemptEnvironment _inner;
            private readonly EntityId _who;
            private readonly SandboxVanillaState _vanilla;
            private int _opened;

            public MovesThemOut(IAttemptEnvironment inner, EntityId who, SandboxVanillaState vanilla)
            {
                _inner = inner;
                _who = who;
                _vanilla = vanilla;
            }

            public bool TryOpen(ActionIntent intent, out ActionContext context, out string refusal)
            {
                // The first open is the ranking read; the second is the execution read. Moving
                // them between the two is the world changing under a decision already made.
                if (intent != null && intent.Actor == _who && ++_opened == 2)
                {
                    _vanilla.SetZone(_who, Valley);
                }

                return _inner.TryOpen(intent, out context, out refusal);
            }
        }

        /// <summary>Controlled adapter behaviour: the declared outcomes, in order, then the last.</summary>
        private sealed class SequencedCheckResolver : ICheckResolver
        {
            private readonly CheckOutcome[] _outcomes;
            private int _next;

            public SequencedCheckResolver(params CheckOutcome[] outcomes)
            {
                _outcomes = outcomes;
            }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                CheckOutcome outcome = _outcomes[_next < _outcomes.Length ? _next : _outcomes.Length - 1];
                _next++;
                return new FixedCheckResolver(outcome).Resolve(request, rng);
            }
        }

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId theft)
            {
                World = world;
                Vanilla = vanilla;
                TheftFact = theft;
                Checks = new FixedCheckResolver(CheckOutcome.Pass);
                Actions = StandardActions.CreateRegistry();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; private set; }

            public ActionRegistry Actions { get; }

            public EntityId TheftFact { get; }

            public static Bench Create(ulong seed = 4114)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeNpc(Lysa, "Lysa"));
                world.Registry.Add(new NarrativeNpc(Maren, "Maren"));
                world.Registry.Add(new NarrativeNpc(Orrin, "Orrin"));
                world.Registry.Add(new NarrativeNpc(Victim, "Jorin"));
                world.Registry.Add(new NarrativeNpc(Player, "You"));
                world.Registry.Add(new NarrativeSite(Town, "Cordwall", "town"));
                world.Registry.Add(new NarrativeSite(Valley, "the valley road", "road"));

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Lysa, Maren, Orrin, Victim, Player })
                {
                    vanilla.Define(who, zone: Town, money: 100);
                }

                vanilla.GiveItem(Victim, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));

                Fact theft = new Fact(world.NewId("fact"), Victim, FactPredicates.Stole, Lysa, "the purse");
                world.Knowledge.AddFact(theft);

                vanilla.AdvanceDays(40);
                return new Bench(world, vanilla, theft.Id);
            }

            /// <summary>The same bench continued against a reloaded save.</summary>
            public Bench Resume(NarrativeWorldState reloaded)
            {
                return new Bench(reloaded, Vanilla, TheftFact) { Checks = Checks };
            }

            public Bench WithChecks(ICheckResolver checks)
            {
                Checks = checks;
                return this;
            }

            public void Knows(EntityId who)
            {
                World.Knowledge.Teach(who, TheftFact, KnowledgeSource.Participant, 1.0, Vanilla.Now, false);
            }

            public void Asleep(EntityId who)
            {
                Vanilla.SetActorActivity(who, new ActorActivityBuilder(who)
                    .WithPresence(PhysicalPresence.InActiveZone)
                    .WithSpan(ActivitySpan.Sleep)
                    .WithActivity(ActivityFamily.Sleep)
                    .Build());
            }

            public ActionIntent Lift(EntityId actor)
            {
                return new ActionIntent(actor, "pickpocket", Victim, "the purse is right there")
                {
                    SubjectItem = Purse
                };
            }

            public ActionIntent Tell(EntityId actor)
            {
                return new ActionIntent(actor, "report", Reeve, "somebody should know")
                {
                    SubjectFact = TheftFact
                };
            }

            public ActionCandidate Candidate(
                EntityId actor,
                double motive,
                GameTime readyAt = default,
                string actionId = "pickpocket")
            {
                ActionIntent intent = actionId == "pickpocket"
                    ? Lift(actor)
                    : new ActionIntent(actor, actionId, Victim, "the purse is right there") { SubjectItem = Purse };

                return new ActionCandidate(
                    intent,
                    ActionContest.ForIntent(Actions.Get(actionId), intent),
                    motive,
                    readyAt == default ? Vanilla.Now : readyAt);
            }

            public ActionCandidate Telling(EntityId actor, double motive)
            {
                ActionIntent intent = Tell(actor);
                return new ActionCandidate(intent, ActionContest.ForIntent(Actions.Get("report"), intent), motive, Vanilla.Now);
            }

            public IAttemptEnvironment Environment()
            {
                return new ActorContextEnvironment(World, Vanilla, Checks, World.Rng, ContextObservation.Observed);
            }

            public ArbitrationResult Resolve(string batchKey, params ActionCandidate[] candidates)
            {
                return ArbitrationBatch.Gather(batchKey, candidates).Resolve(Actions, Environment(), World.Rng);
            }

            public ArbitrationResult Resolve(string batchKey, IAttemptEnvironment[] environment, params ActionCandidate[] candidates)
            {
                return ArbitrationBatch.Gather(batchKey, candidates).Resolve(Actions, environment[0], World.Rng);
            }

            public ArbitrationResult Resolve(string batchKey, IBatchCancellation[] cancellation, params ActionCandidate[] candidates)
            {
                return ArbitrationBatch.Gather(batchKey, candidates).Resolve(Actions, Environment(), World.Rng, cancellation[0]);
            }
        }
    }
}
