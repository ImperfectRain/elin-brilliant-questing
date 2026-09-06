using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-093. The invariant this step exists for: player and NPC narrative actions share the same
    /// semantic verb, the same availability contract, the same check path and the same consequence
    /// path.
    ///
    /// Every test here is written about *equivalence* rather than about class shape. Asserting
    /// that no `NpcBribe` type exists would pass the day before somebody wrote one; asserting that
    /// the object the player's bribe resolves through is the same object an NPC's resolves through
    /// cannot.
    /// </summary>
    public class PlayerNpcActionSymmetryTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Nessa = EntityId.Parse("npc_nessa");
        private static readonly EntityId Haron = EntityId.Parse("npc_haron");
        private static readonly EntityId Bystander = EntityId.Parse("npc_bystander");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Elsewhere = EntityId.Parse("zone_elsewhere");

        // -- one library, one contract ----------------------------------------------------

        [Fact]
        public void BothActorsDiscoverTheSameVerbObjectFromTheSameRegistry()
        {
            Bench bench = Bench.Create();

            ActionOffer asPlayer = bench.Offer(Player, Haron, "question");
            ActionOffer asNpc = bench.Offer(Nessa, Haron, "question");

            Assert.NotNull(asPlayer);
            Assert.NotNull(asNpc);

            // Reference equality, not merely the same id: there is one QuestionAction in the
            // registry and both actors went through it.
            Assert.Same(asPlayer.Action, asNpc.Action);
            Assert.Same(bench.Actions.Get("question"), asNpc.Action);
        }

        [Fact]
        public void AvailabilityIsTheSameVerdictFromTheSameVerbForBothActors()
        {
            Bench bench = Bench.Create();

            Availability asPlayer = bench.Actions.Get("question").GetAvailability(bench.Context(Player, Haron));
            Availability asNpc = bench.Actions.Get("question").GetAvailability(bench.Context(Nessa, Haron));

            Assert.True(asPlayer.IsAvailable);
            Assert.True(asNpc.IsAvailable);

            // And the same verb refuses both for the same reason when the reason is not about who
            // is asking: nobody has anything either of them does not already know.
            bench.World.Knowledge.Teach(Player, bench.Rumour, KnowledgeSource.Hearsay, 0.9, bench.Vanilla.Now, false);
            bench.World.Knowledge.Teach(Nessa, bench.Rumour, KnowledgeSource.Hearsay, 0.9, bench.Vanilla.Now, false);

            Availability playerAfter = bench.Actions.Get("question").GetAvailability(bench.Context(Player, Haron));
            Availability npcAfter = bench.Actions.Get("question").GetAvailability(bench.Context(Nessa, Haron));

            Assert.False(playerAfter.IsAvailable);
            Assert.False(npcAfter.IsAvailable);
            Assert.Equal(playerAfter.Reason, npcAfter.Reason);
        }

        [Fact]
        public void TheSameCheckProfileAndTheSameArithmeticResolveBothActors()
        {
            Bench bench = Bench.Create();
            bench.GiveIdenticalCapability(Player, Nessa);

            CheckResult asPlayer = bench.Ask(Player).Check;
            CheckResult asNpc = bench.Ask(Nessa).Check;

            Assert.Equal(asPlayer.ProfileId, asNpc.ProfileId);
            Assert.Equal(asPlayer.BaseDifficulty, asNpc.BaseDifficulty);

            // Every term that actually moved the difficulty is the same term with the same value.
            // What differs is only the standing vanilla keeps for the player and for nobody else,
            // and that difference shows up as a named zero rather than as a silent reading of
            // somebody else's number.
            Assert.NotEmpty(Contributing(asPlayer));
            Assert.Equal(Contributing(asPlayer), Contributing(asNpc));
            Assert.Equal(asPlayer.FinalDifficulty, asNpc.FinalDifficulty);
        }

        [Fact]
        public void StandingVanillaKeepsForThePlayerAloneIsNamedUnreadForAnNpcRatherThanRead()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetAffinity(Haron, 60);

            CheckResult asPlayer = bench.Ask(Player).Check;
            CheckResult asNpc = bench.Ask(Nessa).Check;

            Assert.Contains(asPlayer.Terms, t => t.Label == "rapport" && t.Delta != 0);
            Assert.DoesNotContain(asNpc.Terms, t => t.Label == "rapport");

            CheckTerm unread = Assert.Single(asNpc.Terms, t => t.Label.StartsWith("rapport unread", StringComparison.Ordinal));
            Assert.Equal(0, unread.Delta);

            // The whole point: Haron liking the player did not make Nessa's question easier.
            Assert.True(asNpc.FinalDifficulty > asPlayer.FinalDifficulty);
        }

        // -- resolution, history and consequence ------------------------------------------

        [Fact]
        public void PerformFollowsTheSamePathAndLandsInTheSameLedger()
        {
            Bench bench = Bench.Create();

            ActionOutcome asPlayer = bench.Ask(Player);
            ActionOutcome asNpc = bench.Ask(Nessa);

            Assert.Equal(asPlayer.ActionId, asNpc.ActionId);
            Assert.Equal(
                asPlayer.Events.Select(e => e.Type).ToArray(),
                asNpc.Events.Select(e => e.Type).ToArray());

            // One ledger, both acts, in order.
            List<WorldEvent> conversed = bench.World.Ledger.OfType(WorldEventType.Conversed).ToList();
            Assert.Contains(conversed, e => e.Actor == Player);
            Assert.Contains(conversed, e => e.Actor == Nessa);
        }

        [Fact]
        public void ActorAndTargetIdsProduceDifferentHistoryFromTheSameVerb()
        {
            Bench bench = Bench.Create();

            bench.Ask(Player);
            bench.Ask(Nessa);

            WorldEvent playerAct = bench.World.Ledger.OfType(WorldEventType.Conversed).First(e => e.Actor == Player);
            WorldEvent npcAct = bench.World.Ledger.OfType(WorldEventType.Conversed).First(e => e.Actor == Nessa);

            Assert.NotEqual(playerAct.Id, npcAct.Id);
            Assert.Equal(Haron, playerAct.Target);
            Assert.Equal(Haron, npcAct.Target);
            Assert.NotEqual(playerAct.Actor, npcAct.Actor);
        }

        [Fact]
        public void WhatIsLearnedStaysWithTheActorWhoAsked()
        {
            Bench bench = Bench.Create().WithCheckOutcome(CheckOutcome.Pass);

            bench.Ask(Nessa);

            Assert.True(bench.World.Knowledge.Knows(Nessa, bench.Rumour));
            Assert.False(bench.World.Knowledge.Knows(Player, bench.Rumour));
            Assert.False(bench.World.Knowledge.Knows(Bystander, bench.Rumour));
        }

        [Fact]
        public void AnNpcActRunsTheSameConsequenceEngineAndMovesNoPlayerStanding()
        {
            Bench bench = Bench.Create();
            bench.WithConsequences();
            bench.Vanilla.SetAffinity(Haron, 10);

            int karma = bench.Vanilla.Karma;
            int fame = bench.Vanilla.Fame;
            int affinity = bench.Vanilla.GetAffinity(Haron);

            ActionOutcome outcome = bench.Ask(Nessa);
            bench.Settle(outcome);

            Assert.NotEmpty(outcome.Events);
            Assert.Equal(karma, bench.Vanilla.Karma);
            Assert.Equal(fame, bench.Vanilla.Fame);
            Assert.Equal(affinity, bench.Vanilla.GetAffinity(Haron));

            // Not a silent skip: the same engine wrote the memory that records the act, keyed to
            // the actor who performed it and carrying no player-affinity slice.
            Assert.Contains(bench.World.Memories.MemoriesAbout(Haron, Nessa), m => m.AffinityContribution == 0);
        }

        [Fact]
        public void ThePlayerDoingTheSameThingDoesMoveTheStandingVanillaKeepsForThem()
        {
            Bench bench = Bench.Create();
            bench.WithConsequences();
            bench.Vanilla.SetAffinity(Haron, 10);

            ActionOutcome outcome = bench.Ask(Player);
            bench.Settle(outcome);

            // The guard above is about whose act it was, not about the engine being inert: the
            // same call with the player acting still reaches vanilla.
            Assert.Contains(bench.World.Memories.MemoriesAbout(Haron, Player), m => m.About == Player);
        }

        // -- verbs an NPC may not take ----------------------------------------------------

        [Fact]
        public void APlayerOnlyVerbRefusesAnNpcByName()
        {
            Bench bench = Bench.Create();
            NarrativeAction shelter = bench.Actions.Get("shelter");

            Availability asNpc = shelter.GetAvailability(bench.Context(Nessa, Haron));

            Assert.False(asNpc.IsAvailable);
            Assert.Contains("Home is the player's settlement", asNpc.Reason);
            Assert.Equal(ActorReach.PlayerOnly, shelter.ActorScope.Reach);
        }

        [Fact]
        public void APlayerOnlyVerbPerformedDirectlyByAnNpcFailsWithNoRollAndNoEvents()
        {
            Bench bench = Bench.Create();

            // Deliberately skipping the availability question, the way a careless caller would.
            ActionOutcome outcome = bench.Actions.Get("shelter").Perform(bench.Context(Nessa, Haron));

            Assert.Null(outcome.Check);
            Assert.Empty(outcome.Events);
            Assert.Contains(outcome.Notes, n => n.Contains("refused before any roll"));
        }

        [Fact]
        public void AVerbAwaitingACapabilityRefusesAnNpcAndSaysWhichOne()
        {
            Bench bench = Bench.Create();
            NarrativeAction fence = bench.Actions.Get("fence");

            Assert.Equal(ActorReach.AwaitingCapability, fence.ActorScope.Reach);
            Availability asNpc = fence.GetAvailability(bench.Context(Nessa, Haron));

            Assert.False(asNpc.IsAvailable);
            Assert.Contains("underworld standing", asNpc.Reason);
        }

        [Fact]
        public void EveryRegisteredVerbDeclaresWhoMayTakeItAndWhyNot()
        {
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                ActorScope scope = action.ActorScope;
                if (scope.Reach == ActorReach.AnyActor)
                {
                    Assert.Equal(string.Empty, scope.Reason);
                    continue;
                }

                Assert.False(
                    string.IsNullOrWhiteSpace(scope.Reason),
                    action.Id + " refuses non-player actors without saying why");
            }
        }

        [Fact]
        public void NoActorGenericVerbLetsThePlayersStandingDecideAnNpcsAvailability()
        {
            Bench bench = Bench.Create().WellSupplied();
            ActionContext asNpc = bench.Context(Nessa, Haron);

            Dictionary<string, string> before = Verdicts(bench, asNpc);

            // The census is only worth anything if the verbs that read standing actually reach
            // the point where they read it, so the bench hands both actors what those verbs need
            // first and the count is asserted rather than assumed.
            Assert.True(before.Count > 40, "only " + before.Count + " verbs were asked");

            // Everything vanilla keeps for the player and for nobody else, moved at once.
            bench.Vanilla.SetCapability(VanillaCapability.ReadWriteKarma, true);
            bench.Vanilla.SetCapability(VanillaCapability.ReadWriteFame, true);
            bench.Vanilla.ChangeKarma(-400);
            bench.Vanilla.ChangeFame(9000);
            bench.Vanilla.SetGuildRank(GuildId.Thieves, 5);
            bench.Vanilla.SetGuildRank(GuildId.Fighters, 5);
            bench.Vanilla.SetGuildContribution(GuildId.Fighters, 400);
            bench.Vanilla.SetAffinity(Haron, 95);

            Dictionary<string, string> after = Verdicts(bench, asNpc);

            List<string> moved = before.Keys
                .Where(id => before[id] != after[id])
                .ToList();

            Assert.True(
                moved.Count == 0,
                "the player's standing changed what an NPC may attempt: " + string.Join(", ", moved));
        }

        [Fact]
        public void ThePlayersOwnAvailabilityStillAnswersToTheirStanding()
        {
            // The guard above must not pass by the verbs having stopped reading standing at all.
            Bench bench = Bench.Create().WellSupplied();
            ActionContext asPlayer = bench.Context(Player, Haron);

            Dictionary<string, string> before = Verdicts(bench, asPlayer);
            bench.Vanilla.SetGuildRank(GuildId.Thieves, 5);
            bench.Vanilla.SetAffinity(Haron, 95);
            Dictionary<string, string> after = Verdicts(bench, asPlayer);

            Assert.Contains(before.Keys, id => before[id] != after[id]);
        }

        [Fact]
        public void EveryRegisteredVerbDeclaresWhatItNeedsFromABody()
        {
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                ActorEmbodiment embodiment = action.Embodiment;
                Assert.NotNull(embodiment);

                if (embodiment.Mode == EmbodimentMode.Narrative)
                {
                    Assert.Empty(embodiment.Needs);
                    continue;
                }

                Assert.False(
                    string.IsNullOrWhiteSpace(embodiment.LeansOn),
                    action.Id + " declares " + embodiment.Mode + " embodiment without naming what it rests on");

                if (embodiment.Mode == EmbodimentMode.Coarse)
                {
                    Assert.Empty(embodiment.Needs);
                }
            }
        }

        [Fact]
        public void ACoarselyResolvedVerbSaysSoOnTheOutcomeItProduces()
        {
            Bench bench = Bench.Create();
            bench.World.Registry.GetNpc(Haron).Roles.Add("victim");

            ActionOutcome outcome = bench.Actions.Get("rescue").Perform(bench.Context(Nessa, Haron));

            Assert.Equal(EmbodimentMode.Coarse, outcome.Embodiment.Mode);
            Assert.Contains("resolved coarsely", outcome.Explain());
            Assert.DoesNotContain("tile", outcome.Explain());
        }

        [Fact]
        public void ADelegatedVerbIsRefusedOnABuildThatCannotCarryTheWrite()
        {
            Bench bench = Bench.Create();
            ActorEmbodiment carry = bench.Actions.Get("carry").Embodiment;

            Assert.Equal(EmbodimentMode.Delegated, carry.Mode);
            Assert.True(carry.CanEmbody(bench.Vanilla, out string _));

            bench.Vanilla.SetCapability(VanillaCapability.TransferItems, false);
            Assert.False(carry.CanEmbody(bench.Vanilla, out string refusal));
            Assert.Contains("TransferItems", refusal);

            // And a build nobody has asked at all is not a build that supports everything.
            Assert.False(carry.CanEmbody(null, out string unasked));
            Assert.Contains("no build has said", unasked);
        }

        // -- intention becomes an attempt --------------------------------------------------

        [Fact]
        public void EveryGoalCandidateBoundToAVerbNamesOneTheRegistryActuallyHas()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();
            NarrativeNpc actor = Bench.Villager(Nessa, "Nessa");
            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(actor, MissingGoatProblem.OrdinaryLoss, EntityId.None);

            Assert.NotEmpty(trace.CandidateActions);
            foreach (GoalActionTrace candidate in trace.CandidateActions)
            {
                if (candidate.IsAttemptable)
                {
                    Assert.NotNull(registry.Get(candidate.RegisteredActionId));
                    Assert.Equal(string.Empty, candidate.UnboundBecause);
                }
                else
                {
                    Assert.False(
                        string.IsNullOrWhiteSpace(candidate.UnboundBecause),
                        candidate.Action + " is bound to no verb and does not say why");
                }
            }
        }

        [Fact]
        public void ACandidateNoVerbMeansProducesNoIntentRatherThanASubstitute()
        {
            NarrativeNpc waiter = Bench.Villager(Nessa, "Nessa");
            waiter.ProblemSolving.Wait = 5.0;

            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(waiter, MissingGoatProblem.OrdinaryLoss, EntityId.None);

            Assert.Equal(ProblemSolvingStyle.Wait, trace.ChosenAction.Style);
            Assert.False(trace.ChosenAction.IsAttemptable);
            Assert.Null(ActionIntent.FromGoalChoice(trace, Haron));
        }

        [Fact]
        public void AGoalThatChoseAVerbBecomesAnAttemptThroughTheSharedRegistry()
        {
            Bench bench = Bench.Create();
            NarrativeNpc nessa = bench.World.Registry.GetNpc(Nessa);
            nessa.ProblemSolving.AskFriends = 5.0;

            GoalFormationTrace trace = MissingGoatProblemSolver.Trace(nessa, MissingGoatProblem.OrdinaryLoss, EntityId.None);
            ActionIntent intent = ActionIntent.FromGoalChoice(trace, Haron);

            Assert.NotNull(intent);
            Assert.Equal("question", intent.ActionId);
            Assert.Equal(Nessa, intent.Actor);

            Assert.True(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext context, out string _));

            ActionAttempt attempt = ActionAttempt.Run(bench.Actions, intent, context);

            Assert.Same(bench.Actions.Get("question"), attempt.Action);
            Assert.True(attempt.Availability.IsAvailable);
            Assert.True(attempt.Resolved);
            Assert.NotEmpty(attempt.Outcome.Events);
            Assert.Contains(bench.World.Ledger.Events, e => e.Actor == Nessa);
        }

        [Fact]
        public void AnAttemptOnAVerbTheActorMayNotTakeResolvesNothingAndSaysWhy()
        {
            Bench bench = Bench.Create();
            Assert.True(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext context, out string _));

            int before = bench.World.Ledger.Events.Count;
            ActionAttempt attempt = ActionAttempt.Run(
                bench.Actions, new ActionIntent(Nessa, "shelter", Haron, "wants them safe"), context);

            Assert.False(attempt.Resolved);
            Assert.Contains("Home is the player's settlement", attempt.Refusal);
            Assert.Equal(before, bench.World.Ledger.Events.Count);
        }

        // -- building the context an NPC acts in -------------------------------------------

        [Fact]
        public void AContextIsBuiltTheSameWayForBothActorsAndDrawsWitnessesFromTheActorsZone()
        {
            Bench bench = Bench.Create();

            Assert.True(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Player, Haron,
                out ActionContext asPlayer, out string _));
            Assert.True(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext asNpc, out string _));

            Assert.Equal(Town, asPlayer.Zone);
            Assert.Equal(Town, asNpc.Zone);

            Assert.Contains(Nessa, asPlayer.Witnesses);
            Assert.Contains(Player, asNpc.Witnesses);
            Assert.DoesNotContain(Haron, asNpc.Witnesses);
            Assert.DoesNotContain(Nessa, asNpc.Witnesses);
        }

        [Fact]
        public void NoContextIsBuiltForTwoPeopleWhoAreNotInTheSamePlace()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Haron, Elsewhere);

            Assert.False(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext context, out string refusal));

            Assert.Null(context);
            Assert.Contains("not in the same place", refusal);
        }

        [Fact]
        public void NoContextIsBuiltForAnActorNobodyCanPlace()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Nessa, EntityId.None);

            Assert.False(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, EntityId.None,
                out ActionContext _, out string refusal));

            Assert.Contains("where they are", refusal);
        }

        [Fact]
        public void NoContextIsBuiltForAnActorVanillaIsAlreadyCarryingBetweenZones()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetCapability(VanillaCapability.ReadActorActivity, true);
            bench.Vanilla.SetActorActivity(Nessa, new ActorActivityBuilder(Nessa)
                .WithZone(Town)
                .WithGlobalActivity(GlobalActivityKind.Travelling)
                .Build());

            Assert.False(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext _, out string refusal));

            Assert.Contains("carrying them between zones", refusal);
        }

        [Fact]
        public void AnUnreadTravelFacetIsNotTakenAsProofNobodyIsMoving()
        {
            Bench bench = Bench.Create();

            // No activity capability at all: every facet unknown, which must not read as "moving"
            // and must not read as "not moving" either - the construction proceeds on the zone the
            // seam still answers, and nothing here claims to know about travel.
            Assert.Equal(VanillaMovement.Unknown, bench.Vanilla.GetActorActivity(Nessa).VanillaMovementState());
            Assert.True(ActorContexts.TryBuild(
                bench.World, bench.Vanilla, bench.Checks, bench.World.Rng, Nessa, Haron,
                out ActionContext _, out string _));
        }

        // -- no second architecture --------------------------------------------------------

        [Fact]
        public void NothingInCoreIsAnActorSpecificVerbOrASecondResolver()
        {
            Assembly core = typeof(NarrativeAction).Assembly;

            List<string> verbs = core.GetTypes()
                .Where(t => typeof(NarrativeAction).IsAssignableFrom(t) && t != typeof(NarrativeAction))
                .Select(t => t.Name)
                .Where(n => n.StartsWith("Npc", StringComparison.Ordinal) || n.StartsWith("Player", StringComparison.Ordinal))
                .ToList();

            Assert.True(verbs.Count == 0, "actor-specific verbs: " + string.Join(", ", verbs));

            List<string> resolvers = core.GetTypes()
                .Select(t => t.Name)
                .Where(n => n.EndsWith("ActionResolver", StringComparison.Ordinal))
                .ToList();

            Assert.True(resolvers.Count == 0, "second action resolvers: " + string.Join(", ", resolvers));
        }

        [Fact]
        public void TheVerbItselfOwnsAvailabilityAndPerformanceForEveryActor()
        {
            // GetAvailability and Perform are not virtual: a verb cannot replace the actor gate,
            // only what it guards. This is the structural half of the invariant - a new verb
            // written next year inherits the refusal whether or not its author remembers it.
            Assert.False(typeof(NarrativeAction).GetMethod("GetAvailability").IsVirtual);
            Assert.False(typeof(NarrativeAction).GetMethod("Perform").IsVirtual);
        }

        /// <summary>Every registered verb's availability verdict, keyed by id.</summary>
        private static Dictionary<string, string> Verdicts(Bench bench, ActionContext context)
        {
            Dictionary<string, string> verdicts = new Dictionary<string, string>();
            foreach (NarrativeAction action in bench.Actions.Actions)
            {
                if (action.ActorScope.Reach != ActorReach.AnyActor && context.Actor != Player)
                {
                    // Classified refusals are the point of the classification, not a leak.
                    continue;
                }

                verdicts[action.Id] = action.GetAvailability(context).ToString();
            }

            return verdicts;
        }

        private static string[] Contributing(CheckResult check)
        {
            return check.Terms.Where(t => t.Delta != 0).Select(t => t.Label + " " + t.Delta).ToArray();
        }

        private sealed class Bench
        {
            private ConsequenceEngine _consequences;

            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId rumour)
            {
                World = world;
                Vanilla = vanilla;
                Rumour = rumour;
                Checks = new VanillaStyleCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            /// <summary>
            /// The real arithmetic by default, because the equivalence claims are about the
            /// arithmetic. A test that is asserting on what an outcome did rather than on how it
            /// was rolled replaces it.
            /// </summary>
            public ICheckResolver Checks { get; private set; }

            public Bench WithCheckOutcome(CheckOutcome outcome)
            {
                Checks = new FixedCheckResolver(outcome);
                return this;
            }

            public ActionRegistry Actions { get; }

            /// <summary>The one thing Haron knows that nobody else does.</summary>
            public EntityId Rumour { get; }

            public static NarrativeNpc Villager(EntityId id, string name)
            {
                NarrativeNpc npc = new NarrativeNpc(id, name);
                npc.Values.Animals.Importance = 0.9;
                npc.Sensitivities.Animals = 0.8;
                return npc;
            }

            public static Bench Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(7);
                world.Registry.Add(new NarrativeNpc(Player, "Player"));
                world.Registry.Add(Villager(Nessa, "Nessa"));
                world.Registry.Add(new NarrativeNpc(Haron, "Haron"));
                world.Registry.Add(new NarrativeNpc(Bystander, "Bystander"));

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Player, Nessa, Haron, Bystander })
                {
                    vanilla.Define(who, zone: Town);
                }

                Fact rumour = new Fact(world.NewId("fact"), Haron, FactPredicates.Stole, EntityId.None, "the goat");
                world.Knowledge.AddFact(rumour);
                world.Knowledge.Teach(Haron, rumour.Id, KnowledgeSource.Witnessed, 1.0, vanilla.Now, canProve: false);

                return new Bench(world, vanilla, rumour.Id);
            }

            /// <summary>
            /// Papers, money, an object worth taking, a receiver in the room and somebody with
            /// standing to report to - so the verbs that read the player's own standing get past
            /// their earlier gates and are actually asked the question the census is about.
            /// </summary>
            public Bench WellSupplied()
            {
                World.Registry.GetNpc(Haron).Roles.Add("fence");
                World.Registry.GetNpc(Haron).Roles.Add("forger");
                World.Registry.GetNpc(Haron).Roles.Add("smuggler");
                World.Registry.GetNpc(Bystander).Roles.Add("authority");

                foreach (EntityId who in new[] { Player, Nessa })
                {
                    Vanilla.Define(who, zone: Town, money: 5000);
                    Vanilla.GiveItem(who, new ItemDescriptor(
                        EntityId.Parse("item_papers_" + who), "sealed writ", "document", 200));
                }

                Vanilla.GiveItem(Haron, new ItemDescriptor(EntityId.Parse("item_purse"), "purse", "misc", 80));
                return this;
            }

            public Bench WithConsequences()
            {
                _consequences = new ConsequenceEngine(World, Vanilla);
                _consequences.Attach();
                Vanilla.SetCapability(VanillaCapability.ReadWriteAffinity, true);
                Vanilla.SetCapability(VanillaCapability.ReadWriteKarma, true);
                Vanilla.SetCapability(VanillaCapability.ReadWriteFame, true);
                return this;
            }

            /// <summary>The same numbers on both sheets, so a difference cannot come from the build.</summary>
            public void GiveIdenticalCapability(params EntityId[] who)
            {
                foreach (EntityId actor in who)
                {
                    Vanilla.SetAttribute(actor, VanillaAttribute.Charisma, 11);
                    Vanilla.SetAttribute(actor, VanillaAttribute.Perception, 12);
                    Vanilla.SetSkill(actor, VanillaSkill.Negotiation, 6);
                }
            }

            public ActionContext Context(EntityId actor, EntityId target)
            {
                ActionContext context = new ActionContext(World, Vanilla, Checks, new DeterministicRng(1), actor, target);
                foreach (EntityId who in Vanilla.GetCharactersInZone(Town))
                {
                    if (who != actor && who != target)
                    {
                        context.Witnesses.Add(who);
                    }
                }

                return context;
            }

            public ActionOffer Offer(EntityId actor, EntityId target, string actionId)
            {
                return Actions
                    .Discover(Context(actor, target), includeUnavailable: true)
                    .FirstOrDefault(o => o.Action.Id == actionId);
            }

            public ActionOutcome Ask(EntityId actor)
            {
                return Actions.Get("question").Perform(Context(actor, Haron));
            }

            /// <summary>
            /// Nothing to do: the engine is attached to the ledger, so consequences already ran as
            /// each event was recorded. Kept as the place a reader looks for them.
            /// </summary>
            public void Settle(ActionOutcome outcome)
            {
                Assert.NotNull(_consequences);
                Assert.NotNull(outcome);
            }
        }
    }
}
