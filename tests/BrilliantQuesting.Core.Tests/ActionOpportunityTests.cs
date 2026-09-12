using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-014. The two evidence modes an attempt can be read in, and the line between them.
    ///
    /// Every test here is written about what was *observed*, not about what is plausible. A coarse
    /// reading is allowed to make an attempt more or less likely; the thing it is never allowed to
    /// do is turn a shared zone id in a save file into two people standing together, or an unread
    /// room into an empty one.
    /// </summary>
    public class ActionOpportunityTests
    {
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Bystander = EntityId.Parse("npc_bystander");
        private static readonly EntityId Reeve = EntityId.Parse("npc_reeve");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Valley = EntityId.Parse("zone_valley");
        private static readonly EntityId Purse = EntityId.Parse("item_purse");

        // -- the same actor and the same goal, read against changed Elin state ----------------

        [Fact]
        public void TheSameTheftReadsDifferentlyWhenVanillaMovesTheVictimOutOfReach()
        {
            Bench bench = Bench.Create();

            ActionOpportunity together = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);
            Assert.True(together.IsPossible);

            bench.Vanilla.SetZone(Victim, Valley);
            ActionOpportunity apart = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.False(apart.IsPossible);
            Assert.Contains("the save keeps them apart", apart.Refusal);
            Assert.Contains(Valley.ToString(), apart.Refusal);
        }

        [Fact]
        public void TheSameTheftReadsLessPlausiblyWhenVanillaPutsTheThiefToSleep()
        {
            Bench bench = Bench.Create();
            double awake = bench.Read("pickpocket", Victim, ContextObservation.OffScreen).Plausibility;

            bench.Asleep(Thief);
            ActionOpportunity asleep = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.True(asleep.IsPossible);
            Assert.True(asleep.Plausibility < awake);
            Assert.Contains(
                asleep.Terms,
                term => term.Facet == OpportunityFacet.WorkService && term.Observation.Contains("asleep"));
            Assert.Contains(
                asleep.Terms,
                term => term.Facet == OpportunityFacet.Routine && term.Observation.Contains("asleep about now"));
        }

        [Fact]
        public void TakingTheObjectOutOfEverybodysHandsIsReadAndNamedRatherThanIgnored()
        {
            Bench bench = Bench.Create();

            OpportunityTerm held = bench
                .Read("pickpocket", Victim, ContextObservation.OffScreen)
                .Term(OpportunityFacet.ObjectAccessibility);

            Assert.True(held.Known);
            Assert.Equal(1.0, held.Weight);
            Assert.Contains(Victim.ToString(), held.Observation);

            bench.Vanilla.DestroyItem(Purse);
            OpportunityTerm gone = bench
                .Read("pickpocket", Victim, ContextObservation.OffScreen)
                .Term(OpportunityFacet.ObjectAccessibility);

            Assert.True(gone.Known);
            Assert.True(gone.Weight < 1.0);
            Assert.Contains("is holding " + Purse + " any more", gone.Observation);
        }

        [Fact]
        public void AnActorVanillaIsAlreadyCarryingIsRefusedInBothEvidenceModes()
        {
            Bench bench = Bench.Create();
            bench.Travelling(Thief);

            foreach (ContextObservation mode in new[] { ContextObservation.Observed, ContextObservation.OffScreen })
            {
                ActionOpportunity reading = bench.Read("pickpocket", Victim, mode);

                Assert.False(reading.IsPossible);
                Assert.Contains("carrying them between zones", reading.Refusal);
                Assert.Equal(0.0, reading.Plausibility);
            }
        }

        [Fact]
        public void ATargetVanillaIsAlreadyCarryingIsRefusedTheSameWay()
        {
            Bench bench = Bench.Create();
            bench.Travelling(Victim);

            ActionOpportunity reading = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.False(reading.IsPossible);
            Assert.Contains("carrying the other party between zones", reading.Refusal);
        }

        [Fact]
        public void AnUnreadTravelFacetRefusesNothingAndIsCountedForNothing()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetCapability(VanillaCapability.ReadActorActivity, false);

            ActionOpportunity reading = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.True(reading.IsPossible);
            OpportunityTerm travel = reading.Term(OpportunityFacet.Travel);
            Assert.False(travel.Known);
            Assert.Equal(1.0, travel.Weight);
            Assert.Contains("unread, counted for nothing", travel.Observation);
        }

        /// <summary>
        /// Active, Warm and Cold are a work budget and have never been a claim about where
        /// anybody is (`BQ-107`). Leaving the active zone changes the reading; being demoted from
        /// one background queue to another must not, or the simulation would be quietly cheaper
        /// to rob in.
        /// </summary>
        [Fact]
        public void LeavingTheActiveZoneChangesTheReadingAndChangingBackgroundTierDoesNot()
        {
            Bench bench = Bench.Create();

            ActionOpportunity inTheRoom = bench.Read("pickpocket", Victim, ContextObservation.Observed);
            Assert.True(inTheRoom.VerifiedCoLocation);
            Assert.True(inTheRoom.IsKnown(OpportunityFacet.Witnesses));

            ActionOpportunity offScreen = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);
            Assert.False(offScreen.VerifiedCoLocation);
            Assert.False(offScreen.IsKnown(OpportunityFacet.Witnesses));
            Assert.True(offScreen.Plausibility < inTheRoom.Plausibility);

            NarrativeNpc thief = bench.World.Registry.GetNpc(Thief);
            thief.Importance = NarrativeImportance.Known;
            Assert.Equal(SimulationTier.Warm, thief.BackgroundTier);
            ActionOpportunity warm = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            thief.Importance = NarrativeImportance.Background;
            Assert.Equal(SimulationTier.Cold, thief.BackgroundTier);
            ActionOpportunity cold = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.Equal(warm.Plausibility, cold.Plausibility);
            Assert.Equal(warm.Describe(), cold.Describe());
        }

        // -- every refusal and every bonus names what was read --------------------------------

        [Fact]
        public void EveryTermCarriesTheObservationBehindItEvenWhenItMovedNothing()
        {
            Bench bench = Bench.Create();

            foreach (ContextObservation mode in new[] { ContextObservation.Observed, ContextObservation.OffScreen })
            {
                ActionOpportunity reading = bench.Read("pickpocket", Victim, mode);

                Assert.NotEmpty(reading.Terms);
                Assert.All(reading.Terms, term => Assert.NotEqual(string.Empty, term.Observation));

                // An unread facet is worth exactly what a harmless one is, and only the flag
                // tells them apart - which is the whole reason the flag exists.
                Assert.All(reading.Terms.Where(term => !term.Known), term => Assert.Equal(1.0, term.Weight));
            }
        }

        [Fact]
        public void EveryRefusalQuotesTheTermThatRefusedIt()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Victim, Valley);

            ActionOpportunity reading = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            OpportunityTerm barring = Assert.Single(reading.Terms, term => term.Barring);
            Assert.Equal(reading.Refusal, barring.Observation);
            Assert.Equal(OpportunityFacet.CoLocation, barring.Facet);
            Assert.Contains("refused: " + reading.Refusal, reading.Describe());
        }

        [Fact]
        public void AnObservedBonusNamesTheRoomItWasReadIn()
        {
            Bench bench = Bench.Create();

            OpportunityTerm alone = bench
                .Read("pickpocket", Victim, ContextObservation.Observed, crowd: false)
                .Term(OpportunityFacet.Privacy);
            Assert.True(alone.Known);
            Assert.Equal(1.0, alone.Weight);
            Assert.Contains("the room was read and nobody else was in it", alone.Observation);

            OpportunityTerm crowded = bench
                .Read("pickpocket", Victim, ContextObservation.Observed, crowd: true)
                .Term(OpportunityFacet.Privacy);
            Assert.True(crowded.Known);
            Assert.True(crowded.Weight < 1.0);
            Assert.Contains("read in the room", crowded.Observation);
        }

        // -- a coarse run never becomes physical evidence -------------------------------------

        [Fact]
        public void TwoPeopleTheSaveKeepsInOneTownAreAnOpportunityAndNeverAMeeting()
        {
            Bench bench = Bench.Create();

            ActionOpportunity coarse = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.True(coarse.IsPossible);
            Assert.False(coarse.VerifiedCoLocation);
            Assert.Contains("never proof that they did", coarse.Term(OpportunityFacet.CoLocation).Observation);
            Assert.Contains("nothing here is physical evidence", coarse.Describe());
        }

        [Fact]
        public void AnUnreadRoomIsNeverAnEmptyOneAndNeverBecomesAWitnessOrAnAlibi()
        {
            Bench bench = Bench.Create();

            OpportunityTerm observed = bench
                .Read("pickpocket", Victim, ContextObservation.Observed, crowd: true)
                .Term(OpportunityFacet.Witnesses);
            Assert.True(observed.Known);
            Assert.Contains("the room was read and held", observed.Observation);

            OpportunityTerm coarse = bench
                .Read("pickpocket", Victim, ContextObservation.OffScreen, crowd: true)
                .Term(OpportunityFacet.Witnesses);
            Assert.False(coarse.Known);
            Assert.Contains("never become a witness or an alibi", coarse.Observation);
        }

        [Fact]
        public void AnEquivalentOffScreenRunRecordsNoWitnessAndNoPlaceClaim()
        {
            Bench observedBench = Bench.Create().WithCheckOutcome(CheckOutcome.CriticalFail);
            ActionContext seen = observedBench.Context(Victim, ContextObservation.Observed, crowd: true);
            observedBench.Actions.Get("pickpocket").Perform(seen);

            Bench coarseBench = Bench.Create().WithCheckOutcome(CheckOutcome.CriticalFail);
            ActionContext unseen = coarseBench.Context(Victim, ContextObservation.OffScreen, crowd: true);
            coarseBench.Actions.Get("pickpocket").Perform(unseen);

            // The observed run is the control: being caught in a read room does produce witnesses.
            Assert.Contains(observedBench.World.Ledger.Events, e => e.Witnesses.Count > 0);

            Assert.All(coarseBench.World.Ledger.Events, e => Assert.Empty(e.Witnesses));
            Assert.DoesNotContain(
                coarseBench.World.Knowledge.Facts.Values,
                fact => fact.Predicate == FactPredicates.LocatedAt);
        }

        // -- a coarse communication travels, and the rest does not ----------------------------

        [Fact]
        public void AReportReachesAnAuthorityThroughItsChannelWithoutClaimingAMeeting()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Reeve, Valley);

            ActionOpportunity reading = bench.Read("report", Reeve, ContextObservation.OffScreen);

            Assert.True(reading.IsPossible);
            Assert.False(reading.VerifiedCoLocation);

            OpportunityTerm reach = reading.Term(OpportunityFacet.CoLocation);
            Assert.Contains("no meeting is needed or claimed", reach.Observation);
            Assert.Contains("a standing report to whoever holds authority here", reach.Observation);
        }

        [Fact]
        public void AVerbThatHasToBeDoneWithSomebodyIsRefusedWhenNothingAnswersWhereTheyAre()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Victim, EntityId.None);

            ActionOpportunity reading = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);

            Assert.False(reading.IsPossible);
            Assert.Contains("nothing answered where they both are", reading.Refusal);
        }

        [Fact]
        public void TheChannelBuysEligibilityAndNothingElseAboutWhereAnybodyStood()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Reeve, Valley);

            ActionContext context = bench.Context(Reeve, ContextObservation.OffScreen, crowd: true);
            AttemptFeasibility feasibility = AttemptFeasibility.Classify(bench.Actions.Get("report"), context);
            Assert.True(feasibility.IsPossible);

            bench.Actions.Get("report").Perform(context);

            Assert.NotEmpty(bench.World.Ledger.Events);
            Assert.All(bench.World.Ledger.Events, e => Assert.Empty(e.Witnesses));
        }

        // -- the ordered gate, and no second answer about the same facet ----------------------

        [Fact]
        public void TheWorldIsAskedBeforeTheVerbAndARefusedAttemptNeverReachesTheVerbsOwnReasons()
        {
            Bench bench = Bench.Create();
            bench.Vanilla.SetZone(Victim, Valley);

            AttemptFeasibility feasibility = AttemptFeasibility.Classify(
                bench.Actions.Get("pickpocket"), bench.Context(Victim, ContextObservation.OffScreen));

            Assert.False(feasibility.IsPossible);
            Assert.NotNull(feasibility.Opportunity);
            Assert.False(feasibility.Opportunity.IsPossible);
            Assert.Equal(feasibility.Opportunity.Refusal, feasibility.Reason);

            // Nothing was classified and no profile was reached, exactly as for a verb's own
            // refusal (BQa-005).
            Assert.Null(feasibility.Uncertainty);
            Assert.Null(feasibility.Profile);
        }

        [Fact]
        public void AnAllowedAttemptCarriesItsOpportunityForWhoeverIsRankingOptions()
        {
            Bench bench = Bench.Create();
            bench.Asleep(Thief);

            AttemptFeasibility feasibility = AttemptFeasibility.Classify(
                bench.Actions.Get("pickpocket"), bench.Context(Victim, ContextObservation.OffScreen));

            Assert.True(feasibility.IsPossible);
            Assert.NotNull(feasibility.Opportunity);
            Assert.True(feasibility.Opportunity.Plausibility < 1.0);
            Assert.Contains("opportunity", feasibility.ToString());
        }

        [Fact]
        public void BeingAsleepIsWorthOneNumberToTheAttemptAndToTheAutonomyPassThatChoseIt()
        {
            Bench bench = Bench.Create();
            bench.Asleep(Thief);

            ActorActivity activity = bench.Vanilla.GetActorActivity(Thief);
            InterventionOpportunity matterSized = InterventionOpportunity.Read(bench.Vanilla, Thief, Town);

            // The matter-sized weight is the same place term it always was, times exactly the two
            // facet weights the attempt-sized reading uses. One answer, read twice.
            double shared = ActionOpportunity.ReadActivity(activity).Weight
                            * ActionOpportunity.ReadRoutine(activity).Weight;
            Assert.Equal(shared, matterSized.Plausibility, 10);

            ActionOpportunity attemptSized = bench.Read("pickpocket", Victim, ContextObservation.OffScreen);
            Assert.Contains(
                ActionOpportunity.ReadActivity(activity).Observation,
                attemptSized.Terms.Select(term => term.Observation));
            Assert.Contains(ActionOpportunity.ReadActivity(activity).Observation, matterSized.Terms);
        }

        [Fact]
        public void AMatterThatMovedTodayIsLessOfAnOpeningThanOneNobodyHasTouchedForAWeek()
        {
            Bench bench = Bench.Create();

            OpportunityTerm today = bench
                .Read("pickpocket", Victim, ContextObservation.OffScreen, daysSinceTheMatterMoved: 0)
                .Term(OpportunityFacet.ElapsedTime);
            OpportunityTerm aWeek = bench
                .Read("pickpocket", Victim, ContextObservation.OffScreen, daysSinceTheMatterMoved: 9)
                .Term(OpportunityFacet.ElapsedTime);

            Assert.True(today.Weight < aWeek.Weight);
            Assert.Contains("hardly been time", today.Observation);
            Assert.Contains("time enough", aWeek.Observation);
        }

        [Fact]
        public void EveryRegisteredVerbAnswersTheReachQuestionAndMostOfThemHaveToBeThere()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            Assert.All(registry.Actions, action => Assert.NotNull(action.Reach));

            string[] travelling = registry.Actions
                .Where(action => action.Reach.Mode == OpportunityReach.Channel)
                .Select(action => action.Id)
                .OrderBy(id => id)
                .ToArray();

            // Declared, not assumed: a verb that reaches across the map is the exception, and the
            // day another one becomes one it should be a deliberate edit here.
            Assert.Equal(new[] { "report" }, travelling);
            Assert.All(
                registry.Actions.Where(action => action.Reach.Mode == OpportunityReach.Channel),
                action => Assert.NotEqual(string.Empty, action.Reach.Channel));
        }

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId theft)
            {
                World = world;
                Vanilla = vanilla;
                TheftFact = theft;
                Checks = new VanillaStyleCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; private set; }

            public ActionRegistry Actions { get; }

            public EntityId TheftFact { get; }

            public static Bench Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(4114);
                world.Registry.Add(new NarrativeNpc(Thief, "Lysa"));
                world.Registry.Add(new NarrativeNpc(Victim, "Jorin"));
                world.Registry.Add(new NarrativeNpc(Bystander, "Merren"));
                NarrativeNpc reeve = new NarrativeNpc(Reeve, "Herrick");
                reeve.Roles.Add(AuthorityPolicy.GuardRole);
                world.Registry.Add(reeve);
                world.Registry.Add(new NarrativeSite(Town, "Cordwall", "town"));
                world.Registry.Add(new NarrativeSite(Valley, "the valley road", "road"));

                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                foreach (EntityId who in new[] { Thief, Victim, Bystander, Reeve })
                {
                    vanilla.Define(who, zone: Town, money: 100);
                }

                vanilla.GiveItem(Victim, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));

                Fact theft = new Fact(world.NewId("fact"), Victim, FactPredicates.Stole, Thief, "the purse");
                world.Knowledge.AddFact(theft);
                world.Knowledge.Teach(Thief, theft.Id, KnowledgeSource.Participant, 1.0, vanilla.Now, false);

                vanilla.AdvanceDays(40);
                return new Bench(world, vanilla, theft.Id);
            }

            public Bench WithCheckOutcome(CheckOutcome outcome)
            {
                Checks = new FixedCheckResolver(outcome);
                return this;
            }

            /// <summary>Vanilla says they are asleep, and their own routine agrees.</summary>
            public void Asleep(EntityId who)
            {
                Vanilla.SetActorActivity(who, new ActorActivityBuilder(who)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithSpan(ActivitySpan.Sleep)
                    .WithActivity(ActivityFamily.Sleep)
                    .Build());
            }

            public void Travelling(EntityId who)
            {
                Vanilla.SetActorActivity(who, new ActorActivityBuilder(who)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                    .WithGlobalActivity(GlobalActivityKind.Travelling)
                    .WithZoneTransition(ZoneTransitionState.Pending)
                    .Build());
            }

            public ActionContext Context(
                EntityId target,
                ContextObservation mode,
                bool crowd = false,
                long daysSinceTheMatterMoved = 3)
            {
                NarrativeThread thread = new NarrativeThread(World.NewId("thread"), "theft", Vanilla.Now)
                {
                    LastAdvancedAt = Vanilla.Now.PlusDays(-daysSinceTheMatterMoved)
                };

                ActionContext context = new ActionContext(World, Vanilla, Checks, World.Rng, Thief, target)
                {
                    Observation = mode,
                    SubjectFact = TheftFact,
                    SubjectItem = Purse,
                    Thread = thread
                };

                // Filled by hand rather than from the zone, so that the crowd is the same in both
                // evidence modes and only the mode can be responsible for the difference.
                if (crowd)
                {
                    context.Witnesses.Add(Bystander);
                }

                return context;
            }

            public ActionOpportunity Read(
                string actionId,
                EntityId target,
                ContextObservation mode,
                bool crowd = false,
                long daysSinceTheMatterMoved = 3)
            {
                return ActionOpportunity.Read(
                    Actions.Get(actionId),
                    Context(target, mode, crowd, daysSinceTheMatterMoved));
            }
        }
    }
}
