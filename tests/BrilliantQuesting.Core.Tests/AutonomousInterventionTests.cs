using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-094. The world takes one of its own problems up.
    ///
    /// The done-when has two halves and both are tested as behaviour rather than as shape: a
    /// matter the player ignored is ended by somebody else, and the player can find out how
    /// without being told for free. Around them sit the constraints that make the first half
    /// honest - who is entitled to act, what an off-screen act may claim, and what is allowed
    /// into history.
    /// </summary>
    public class AutonomousInterventionTests
    {
        // -- the done-when ------------------------------------------------------------------

        [Fact]
        public void AMatterNobodyHasTakenUpIsEndedBySomebodyElse()
        {
            Village village = Village.Create();

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            Assert.Equal(1, acted);
            Assert.Equal(ThreadState.Resolved, village.Thread.State);

            WorldEvent ending = village.World.Ledger.Events.Last(e => e.Type == WorldEventType.ThreadResolved);
            Assert.Equal(village.Bram, ending.Actor);
            Assert.NotEqual(village.Player, ending.Actor);
        }

        [Fact]
        public void ThePlayerIsToldNothingByTheWorldSolvingItsOwnProblem()
        {
            Village village = Village.Create();

            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            EntityId claim = village.SettledClaim();
            Assert.True(village.World.Knowledge.Knows(village.Bram, claim));
            Assert.False(village.World.Knowledge.Knows(village.Player, claim));
            Assert.Empty(Chronicle.Entries(village.World, village.Player));
        }

        [Fact]
        public void OnceSomebodyTellsThemThePlayerCanReadHowItEnded()
        {
            Village village = Village.Create();
            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            RumorSystem rumours = new RumorSystem(village.World.Knowledge, village.World.Ledger, village.World.Ids);
            Assert.True(rumours.Tell(village.Bram, village.Player, village.SettledClaim(), village.Now));

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(village.World, village.Player));
            Assert.Equal(village.Thread.Id, entry.ThreadId);
            Assert.Equal(village.Bram, entry.ResolvedBy);
            Assert.Equal("supplies_bought", entry.Outcome);
            Assert.Empty(entry.WhatThePlayerDid);
        }

        [Fact]
        public void WhatTheyReadIsWhatTheyWereToldRatherThanWhatHappened()
        {
            Village village = Village.Create();
            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            // A version of the claim that got the name wrong on the way. The chronicle has to
            // report the version the player holds, not quietly correct it from the ledger.
            Fact truth = village.World.Knowledge.GetFact(village.SettledClaim());
            Fact garbled = new Fact(
                village.World.NewId("fact"),
                village.Marla,
                FactPredicates.Settled,
                truth.Object,
                truth.Value)
            {
                DistortionOf = truth.Id
            };
            village.World.Knowledge.AddFact(garbled);
            village.World.Knowledge.Teach(village.Player, garbled.Id, KnowledgeSource.Hearsay, 0.5, village.Now, false);

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(village.World, village.Player));
            Assert.Equal(village.Marla, entry.ResolvedBy);
        }

        [Fact]
        public void AMatterSomebodyElseEndedIsNotReportedAsSomethingThePlayerFinished()
        {
            Village village = Village.Create();
            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);
            new RumorSystem(village.World.Knowledge, village.World.Ledger, village.World.Ids)
                .Tell(village.Bram, village.Player, village.SettledClaim(), village.Now);

            string life = ChronicleNarrative.Export(village.World, village.Player, village.Now);

            Assert.Contains("What happened without you", life);
            Assert.DoesNotContain("What you finished", life);
        }

        // -- who is entitled to act ---------------------------------------------------------

        [Fact]
        public void NobodyTakesUpAMatterThePlayerIsAlreadyActingIn()
        {
            Village village = Village.Create();
            village.World.Record(
                WorldEventType.Conversed, village.Player, village.Marla, village.Now, 0.2, village.Town,
                threadId: village.Thread.Id);

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            Assert.Equal(0, acted);
            Assert.NotEqual(ThreadState.Resolved, village.Thread.State);
        }

        /// <summary>
        /// BQa-016. The deferral is a window, not a claim. An act the player made weeks ago used
        /// to take the matter off the world's table permanently, which froze every matter the
        /// player had ever touched; now it defers a conflicting attempt only while they are
        /// actually in it.
        /// </summary>
        [Fact]
        public void AMatterThePlayerTouchedLongAgoBecomesTheWorldsAgain()
        {
            Village village = Village.Create();
            GameTime touched = village.Now;
            village.World.Record(
                WorldEventType.Conversed, village.Player, village.Marla, touched, 0.2, village.Town,
                threadId: village.Thread.Id);

            Assert.Equal(0, village.Autonomy.Advance(
                village.World, village.Vanilla, village.Checks, village.Actions, touched));

            // Still inside the window: their visit is not interrupted.
            GameTime during = touched.PlusDays(village.Autonomy.PlayerInteractionDays - 1);
            village.Vanilla.Now = during;
            Assert.Equal(0, village.Autonomy.Advance(
                village.World, village.Vanilla, village.Checks, village.Actions, during));

            // Past it, the neighbour is entitled to take it up.
            GameTime after = touched.PlusDays(village.Autonomy.PlayerInteractionDays);
            village.Vanilla.Now = after;
            Assert.Equal(1, village.Autonomy.Advance(
                village.World, village.Vanilla, village.Checks, village.Actions, after));
        }

        /// <summary>
        /// And what renews the window is the latest act, not the first: somebody still working on
        /// a matter keeps deferring the world rather than spending one window and losing it.
        /// </summary>
        [Fact]
        public void ThePlayerStillWorkingOnAMatterKeepsDeferringIt()
        {
            Village village = Village.Create();
            GameTime first = village.Now;
            village.World.Record(
                WorldEventType.Conversed, village.Player, village.Marla, first, 0.2, village.Town,
                threadId: village.Thread.Id);

            GameTime again = first.PlusDays(village.Autonomy.PlayerInteractionDays);
            village.World.Record(
                WorldEventType.Conversed, village.Player, village.Marla, again, 0.2, village.Town,
                threadId: village.Thread.Id);

            village.Vanilla.Now = again;
            Assert.Equal(0, village.Autonomy.Advance(
                village.World, village.Vanilla, village.Checks, village.Actions, again));
        }

        [Fact]
        public void NobodyActsOnAMatterTheyHaveNeverHeardOf()
        {
            Village village = Village.Create(teachTheNeighbour: false);

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            // He has the money, the pantry and the reason. What he has not got is any idea that
            // Marla is short of anything, so he is never weighed at all.
            InterventionTrace trace = Assert.Single(village.Autonomy.LastPass);
            Assert.Equal(0, acted);
            Assert.DoesNotContain(trace.Opportunities, read => read.Actor == village.Bram);
            Assert.DoesNotContain(trace.Options, option => option.Actor == village.Bram);
        }

        [Fact]
        public void NobodyActsOnAMatterThatIsNoneOfTheirs()
        {
            Village village = Village.Create();
            village.World.Registry.GetNpc(village.Bram).Goals.Clear();

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            // He still knows about it, still has the money and still has the pantry. What he has
            // not got is any reason of his own, and nobody acts on somebody else's matter for
            // nothing - so he is not weighed against it at all.
            InterventionTrace trace = Assert.Single(village.Autonomy.LastPass);
            Assert.Equal(0, acted);
            Assert.DoesNotContain(trace.Opportunities, read => read.Actor == village.Bram);
            Assert.DoesNotContain(trace.Options, option => option.Actor == village.Bram);
            Assert.NotEqual(ThreadState.Resolved, village.Thread.State);
        }

        [Fact]
        public void AMatterThatHasOnlyJustArisenIsLeftAlone()
        {
            Village village = Village.Create();

            int acted = village.Autonomy.Advance(
                village.World, village.Vanilla, village.Checks, village.Actions, village.Thread.CreatedAt);

            Assert.Equal(0, acted);
            Assert.Empty(village.Autonomy.LastPass);
        }

        [Fact]
        public void SomebodyVanillaIsAlreadyCarryingBetweenZonesIsNotAvailable()
        {
            Village village = Village.Create();
            village.Vanilla.SetCapability(VanillaCapability.ReadActorActivity, true);
            village.Vanilla.SetActorActivity(village.Bram, new ActorActivityBuilder(village.Bram)
                .WithZone(village.Town)
                .WithGlobalActivity(GlobalActivityKind.Travelling)
                .Build());

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            Assert.Equal(0, acted);
            Assert.NotEqual(ThreadState.Resolved, village.Thread.State);
        }

        [Fact]
        public void AnUnreadActivityIsNeverAReasonNobodyCouldHaveActed()
        {
            Village village = Village.Create();
            village.Vanilla.SetCapability(VanillaCapability.ReadActorActivity, false);

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            Assert.Equal(1, acted);
            Assert.All(
                village.Autonomy.LastPass.Single().Opportunities,
                read => Assert.Contains(read.Terms, term => term.Contains("unread")));
        }

        // -- what an off-screen act may claim ------------------------------------------------

        [Fact]
        public void AnActNobodyWatchedRecordsNoWitnessesAndSaysTheyWereUnread()
        {
            Village village = Village.Create();

            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            ActionOutcome outcome = village.Autonomy.LastPass.Single().Attempt.Outcome;
            Assert.Equal(ContextObservation.OffScreen, outcome.Observation);

            // The town is full and nobody was recorded as having seen it, because nobody's
            // presence was read - which the outcome says rather than leaving to be inferred.
            Assert.True(village.Vanilla.GetCharactersInZone(village.Town).Count > 2);
            Assert.All(outcome.Events, recorded => Assert.Empty(recorded.Witnesses));
            Assert.Contains("witnesses unread", outcome.Explain());
        }

        [Fact]
        public void AnAbsentActorIsNotAskedForAPhysicalActNobodyHasWatchedVanillaPerform()
        {
            Blockage blockage = Blockage.Create();

            blockage.Autonomy.Advance(blockage.World, blockage.Vanilla, blockage.Checks, blockage.Actions, blockage.Now);

            InterventionTrace trace = Assert.Single(blockage.Autonomy.LastPass);
            List<InterventionOption> delegated = trace.Options
                .Where(o => blockage.Actions.Get(o.ActionId).Embodiment.Mode == EmbodimentMode.Delegated)
                .ToList();

            // The delegated routes were found, were available, and were still not asked for.
            Assert.NotEmpty(delegated);
            Assert.All(delegated, option =>
            {
                Assert.NotEqual(string.Empty, option.Barred);
                Assert.False(option.Eligible);
            });

            // And the matter was still answered, by the route that claims nothing physical.
            Assert.Equal("mine_bypass", trace.Attempt.Intent.ActionId);
            Assert.Equal(EmbodimentMode.Coarse, trace.Attempt.Action.Embodiment.Mode);
        }

        [Fact]
        public void NothingAboutTheActorsDayReachesHistory()
        {
            Village village = Village.Create();

            village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            List<WorldEvent> theirs = village.World.Ledger.Events.Where(e => e.Actor == village.Bram).ToList();

            // Exactly the deed and the ending. No arrival, no routine, no whereabouts.
            Assert.Equal(2, theirs.Count);
            Assert.Contains(theirs, e => e.Type == WorldEventType.Helped);
            Assert.Contains(theirs, e => e.Type == WorldEventType.ThreadResolved);
            Assert.DoesNotContain(
                village.World.Knowledge.Facts.Values,
                fact => fact.Predicate == FactPredicates.LocatedAt);
        }

        // -- succeed, fail, or make it worse -------------------------------------------------

        [Fact]
        public void HowSomebodySolvesThingsDecidesWhichOfTwoOpenRoutesTheyTake()
        {
            Village buyer = Village.Create();
            buyer.Neighbour.ProblemSolving.PaySomeone = 0.95;
            buyer.Neighbour.ProblemSolving.DoItSelf = 0.05;

            Village maker = Village.Create();
            maker.Neighbour.ProblemSolving.PaySomeone = 0.05;
            maker.Neighbour.ProblemSolving.DoItSelf = 0.95;

            buyer.Autonomy.Advance(buyer.World, buyer.Vanilla, buyer.Checks, buyer.Actions, buyer.Now);
            maker.Autonomy.Advance(maker.World, maker.Vanilla, maker.Checks, maker.Actions, maker.Now);

            Assert.Equal("buy_supplies", buyer.Autonomy.LastPass.Single().Attempt.Intent.ActionId);
            Assert.Equal("cook", maker.Autonomy.LastPass.Single().Attempt.Intent.ActionId);
        }

        [Fact]
        public void AnAttemptThatFailsLeavesTheMatterOpenAndCostsTheActorSomething()
        {
            Village village = Village.Create(new FixedCheckResolver(CheckOutcome.CriticalFail));
            village.Neighbour.ProblemSolving.PaySomeone = 0.05;
            village.Neighbour.ProblemSolving.DoItSelf = 0.95;
            int stockBefore = village.Vanilla.GetInventory(village.Bram).Count;

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            InterventionTrace trace = Assert.Single(village.Autonomy.LastPass);
            Assert.Equal(1, acted);
            Assert.False(trace.Attempt.Outcome.Succeeded);
            Assert.False(trace.Resolved);
            Assert.NotEqual(ThreadState.Resolved, village.Thread.State);
            Assert.True(village.Vanilla.GetInventory(village.Bram).Count < stockBefore);

            // Nothing was put right, so nothing may be said about it having been.
            Assert.DoesNotContain(
                village.World.Knowledge.Facts.Values,
                fact => fact.Predicate == FactPredicates.Settled);
        }

        [Fact]
        public void OnlyOneMatterIsTakenUpInOnePass()
        {
            Village village = Village.Create();
            village.SecondMatter();

            int acted = village.Autonomy.Advance(village.World, village.Vanilla, village.Checks, village.Actions, village.Now);

            Assert.Equal(1, acted);
            Assert.Equal(1, village.World.Threads.Count(t => t.State == ThreadState.Resolved));
        }

        [Fact]
        public void TheSameWorldAnsweredTwiceMakesTheSameChoice()
        {
            Village first = Village.Create();
            Village second = Village.Create();

            first.Autonomy.Advance(first.World, first.Vanilla, first.Checks, first.Actions, first.Now);
            second.Autonomy.Advance(second.World, second.Vanilla, second.Checks, second.Actions, second.Now);

            Assert.Equal(
                first.Autonomy.LastPass.Single().Attempt.Intent.ActionId,
                second.Autonomy.LastPass.Single().Attempt.Intent.ActionId);
            Assert.Equal(
                first.Autonomy.LastPass.Single().Attempt.Intent.Actor,
                second.Autonomy.LastPass.Single().Attempt.Intent.Actor);
        }

        // -- fixtures -------------------------------------------------------------------------

        /// <summary>
        /// Marla is short of food, and Bram next door has both money and a full pantry and a
        /// stated reason to care. The player has heard of it and has done nothing about it.
        /// </summary>
        private sealed class Village
        {
            private Village(NarrativeWorldState world, SandboxVanillaState vanilla, ICheckResolver checks, NarrativeThread thread)
            {
                World = world;
                Vanilla = vanilla;
                Checks = checks;
                Thread = thread;
                Actions = StandardActions.CreateRegistry();
                Autonomy = new AutonomousInterventions();
            }

            private static readonly EntityId PlayerId = EntityId.Parse("npc_player");
            private static readonly EntityId MarlaId = EntityId.Parse("npc_marla");
            private static readonly EntityId BramId = EntityId.Parse("npc_bram");
            private static readonly EntityId OnlookerId = EntityId.Parse("npc_onlooker");
            private static readonly EntityId TownId = EntityId.Parse("zone_rillford");

            public EntityId Player => PlayerId;

            public EntityId Marla => MarlaId;

            public EntityId Bram => BramId;

            public EntityId Onlooker => OnlookerId;

            public EntityId Town => TownId;

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public AutonomousInterventions Autonomy { get; }

            public NarrativeThread Thread { get; }

            public NarrativeNpc Neighbour => World.Registry.GetNpc(BramId);

            public GameTime Now => Vanilla.Now;

            public static Village Create(bool teachTheNeighbour = true)
            {
                return Create(null, teachTheNeighbour);
            }

            public static Village Create(ICheckResolver checks, bool teachTheNeighbour = true)
            {
                NarrativeWorldState world = new NarrativeWorldState(94);
                world.Registry.Add(new NarrativeNpc(PlayerId, "You"));
                world.Registry.Add(new NarrativeNpc(MarlaId, "Marla") { Occupation = "weaver" });
                NarrativeNpc bram = world.Registry.Add(new NarrativeNpc(BramId, "Bram") { Occupation = "farmer" });
                world.Registry.Add(new NarrativeNpc(OnlookerId, "Onlooker"));

                SandboxVanillaState vanilla = new SandboxVanillaState(PlayerId);
                foreach (EntityId who in new[] { PlayerId, MarlaId, BramId, OnlookerId })
                {
                    vanilla.Define(who, zone: TownId);
                }

                vanilla.Define(BramId, zone: TownId, money: 500);
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);
                for (int i = 0; i < 4; i++)
                {
                    vanilla.GiveItem(BramId, new ItemDescriptor(
                        EntityId.Parse("item_grain_" + i), "a sack of grain", "grain", 30, "grain"));
                }

                vanilla.SetSkill(BramId, VanillaSkill.Cooking, 12);
                vanilla.SetAttribute(BramId, VanillaAttribute.Dexterity, 12);

                GameTime start = vanilla.Now;
                Fact need = new Fact(world.NewId("fact"), MarlaId, FactPredicates.Needs, EntityId.None, "food");
                world.Knowledge.AddFact(need);
                world.Knowledge.Teach(MarlaId, need.Id, KnowledgeSource.Participant, 1.0, start, false);
                world.Knowledge.Teach(PlayerId, need.Id, KnowledgeSource.Hearsay, 0.8, start, false);
                if (teachTheNeighbour)
                {
                    world.Knowledge.Teach(BramId, need.Id, KnowledgeSource.Hearsay, 0.9, start, false);
                }

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "shortage", start)
                {
                    State = ThreadState.Active,
                    Tension = 30
                };
                thread.ParticipantIds.Add(MarlaId);
                thread.ParticipantIds.Add(BramId);
                thread.FactIds.Add(need.Id);
                world.Threads.Add(thread);

                bram.Goals.Add(new NpcGoal("feed_his_neighbour", MarlaId, 80));
                bram.ProblemSolving.PaySomeone = 0.9;

                vanilla.AdvanceDays(5);
                return new Village(world, vanilla, checks ?? new VanillaStyleCheckResolver(vanilla), thread);
            }

            /// <summary>A second matter, identical in shape, so a pass has two things it could do.</summary>
            public void SecondMatter()
            {
                Fact need = new Fact(World.NewId("fact"), OnlookerId, FactPredicates.Needs, EntityId.None, "food");
                World.Knowledge.AddFact(need);
                World.Knowledge.Teach(BramId, need.Id, KnowledgeSource.Hearsay, 0.9, Now, false);

                NarrativeThread thread = new NarrativeThread(World.NewId("thread"), "shortage", Thread.CreatedAt)
                {
                    State = ThreadState.Active
                };
                thread.ParticipantIds.Add(OnlookerId);
                thread.ParticipantIds.Add(BramId);
                thread.FactIds.Add(need.Id);
                World.Threads.Add(thread);
                Neighbour.Goals.Add(new NpcGoal("feed_the_other_one", OnlookerId, 80));
            }

            public EntityId SettledClaim()
            {
                return World.Knowledge.Facts.Values.Single(f => f.Predicate == FactPredicates.Settled).Id;
            }
        }

        /// <summary>
        /// A rockfall on the mine road, which the foreman can answer three ways: two that ask
        /// vanilla to take the stone out of the world, and one that claims nothing physical.
        /// </summary>
        private sealed class Blockage
        {
            private Blockage(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Actions = StandardActions.CreateRegistry();
                Autonomy = new AutonomousInterventions();
                Checks = new FixedCheckResolver(CheckOutcome.Pass);
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ActionRegistry Actions { get; }

            public ICheckResolver Checks { get; }

            public AutonomousInterventions Autonomy { get; }

            public GameTime Now => Vanilla.Now;

            public static Blockage Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(940);
                EntityId player = world.NewId("npc");
                EntityId trail = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, zone: trail);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);
                vanilla.SetCapability(VanillaCapability.ReadPlaceContents, true);
                world.Registry.Add(new NarrativeNpc(player, "You"));

                BlockedPassageSituation.Create(world, new SandboxStager(vanilla), player, trail, vanilla.Now);
                vanilla.AdvanceDays(4);
                return new Blockage(world, vanilla);
            }
        }
    }
}
