using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
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
    /// BQ-027. A woman with nowhere safe to be, and a player who either has a settlement to put
    /// her in or does not. The routes are decided by the Home - beds, people, Public Safety - and
    /// not by the player's stats, which is the whole reason the family exists.
    /// </summary>
    public class HomeActionTests
    {
        [Fact]
        public void HomeAndCommunityVerbsAreRegisteredWithTheirFamily()
        {
            string[] verbs =
            {
                "shelter", "host", "recruit_specialist", "assign_protection", "provide_supplies", "store_evidence"
            };

            ActionRegistry registry = StandardActions.CreateRegistry();
            foreach (string verb in verbs)
            {
                Assert.NotNull(registry.Get(verb));
                Assert.Equal(ActionFamily.HomeCommunity, registry.Get(verb).Family);
            }

            // Everything that rolls has a profile; putting a thing in your own house does not roll.
            foreach (string verb in new[] { "shelter", "host", "recruit_specialist", "assign_protection", "provide_supplies" })
            {
                Assert.NotNull(ProceduralCheckProfiles.ForAction(verb));
            }

            Assert.Null(ProceduralCheckProfiles.ForAction("store_evidence"));
        }

        // -- the done-when -------------------------------------------------------------------

        /// <summary>
        /// The step's actual condition: sheltering somebody moves the world's opinion of the
        /// player *and* the settlement itself. Both halves are asserted against real state - the
        /// vanilla affinity and fame the game keeps, and the Home snapshot read back afterwards.
        /// </summary>
        [Fact]
        public void ShelteringSomeoneMovesBothTheWorldsDispositionAndTheHomeItself()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            int affinityBefore = lab.Vanilla.GetAffinity(lab.Witness);
            int fameBefore = lab.Vanilla.Fame;
            HomeState before = lab.Vanilla.GetHomeState();

            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.True(outcome.Succeeded);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.TakenIn);

            // The world's disposition: she thinks a great deal more of the player, the neighbour
            // who watched it thinks a little more, and the player's name has travelled.
            Assert.True(lab.Vanilla.GetAffinity(lab.Witness) > affinityBefore);
            Assert.True(lab.Vanilla.GetAffinity(lab.Neighbour) > 0);
            Assert.True(lab.Vanilla.Fame > fameBefore);

            // The Home itself: one more resident, one fewer bed, read back through the seam.
            HomeState after = lab.Vanilla.GetHomeState();
            Assert.Equal(before.ResidentCount + 1, after.ResidentCount);
            Assert.Equal(before.FreeCapacity - 1, after.FreeCapacity);
            Assert.True(after.IsResident(lab.Witness));

            // And the danger is over, which is what closes the thread.
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("sheltered", lab.Situation.Thread.Resolution);
        }

        /// <summary>
        /// The undertaking is on the record as a fact about her, not as a flag on the verb, and it
        /// is the kind of thing a town repeats - which is what makes taking somebody in a decision
        /// with a downside rather than a free kindness.
        /// </summary>
        [Fact]
        public void TakingSomebodyInIsRecordedAsAPublicUndertaking()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);

            lab.Run("shelter", lab.Witness);

            Fact undertaking = lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy);
            Assert.NotNull(undertaking);
            Assert.Equal(lab.Player, undertaking.Object);
            Assert.Equal(Undertakings.Resident, undertaking.Value);
            Assert.True(lab.World.Knowledge.Knows(lab.Witness, undertaking.Id));
            Assert.True(FactPredicates.IsNewsworthy(FactPredicates.ShelteredBy));
        }

        // -- what makes it impossible rather than unlikely ----------------------------------

        [Fact]
        public void APlayerWithNoHomeHasNoHomeRoutes()
        {
            SanctuaryLab lab = SanctuaryLab.WithNoHome(CheckOutcome.Pass);

            foreach (string verb in new[] { "shelter", "host", "assign_protection", "provide_supplies", "store_evidence" })
            {
                Availability availability = lab.Can(verb, lab.Witness);
                Assert.False(availability.IsAvailable);
                Assert.Contains("no home", availability.Reason);
            }
        }

        [Fact]
        public void AFullHomeCannotTakeAnotherResidentHoweverPersuasiveThePlayerIs()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.CriticalPass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA, SanctuaryLab.ResidentB)
                    .WithCapacity(2)
                    .WithMetric(HomeMetric.Safety, 40));

            Availability availability = lab.Can("shelter", lab.Witness);
            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.False(availability.IsAvailable);
            Assert.Contains("full", availability.Reason);
            Assert.Empty(outcome.Events);
            Assert.Equal(2, lab.Vanilla.GetHomeState().ResidentCount);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
        }

        /// <summary>
        /// D017 applied to an offer: a capacity nobody could read is not room. Promising a bed the
        /// settlement may not have is the failure worth avoiding, so the unread number refuses.
        /// </summary>
        [Fact]
        public void ACapacityThisBuildCouldNotReadRefusesTheBedRatherThanAssumingOne()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithMetric(HomeMetric.Safety, 40));

            Availability availability = lab.Can("shelter", lab.Witness);

            Assert.False(availability.IsAvailable);
            Assert.Contains("will not say whether your home has room", availability.Reason);
        }

        [Fact]
        public void ABuildThatCannotWriteResidencyLosesTheBedRoutesAndKeepsTheRest()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            lab.Vanilla.SetCapability(VanillaCapability.WriteHomeResidents, false);

            Availability shelter = lab.Can("shelter", lab.Witness);
            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.False(shelter.IsAvailable);
            Assert.Contains("moved into a home on this build", shelter.Reason);
            Assert.Empty(outcome.Events);
            Assert.False(lab.Vanilla.GetHomeState().IsResident(lab.Witness));

            // The routes that spend no bed are untouched by a missing residency write.
            Assert.True(lab.Can("host", lab.Witness).IsAvailable);
            Assert.True(lab.Can("assign_protection", lab.Witness).IsAvailable);
        }

        /// <summary>
        /// BQ-031. Moving somebody onto the settlement roll is a permanent relocation, and the
        /// mutation policy decides who may be relocated at all. Somebody the game's own story
        /// depends on is not offered a bed - impossible, not unlikely - and the routes that move
        /// nobody are untouched, which is the point of separating the four verbs in the first
        /// place: she can still be hosted for the night and still be given a watch.
        /// </summary>
        [Fact]
        public void SomebodyTheWorldWillNotLetYouMoveIsOfferedEverythingExceptABed()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            lab.Vanilla.SetActorClass(lab.Witness, NarrativeActorClass.StoryCritical);

            Availability shelter = lab.Can("shelter", lab.Witness);
            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.False(shelter.IsAvailable);
            Assert.Contains("move house", shelter.Reason);
            Assert.Empty(outcome.Events);
            Assert.False(lab.Vanilla.GetHomeState().IsResident(lab.Witness));

            Assert.True(lab.Can("host", lab.Witness).IsAvailable);
            Assert.True(lab.Can("assign_protection", lab.Witness).IsAvailable);
        }

        /// <summary>
        /// The same refusal when the build simply could not say who she is. An unclassified actor
        /// is not a licence, so the bed closes and nothing else does.
        /// </summary>
        [Fact]
        public void ABuildThatCannotSayWhoSomebodyIsWillNotMoveThemIn()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            lab.Vanilla.SetActorClass(lab.Witness, NarrativeActorClass.Unknown);

            Assert.False(lab.Can("shelter", lab.Witness).IsAvailable);
            Assert.False(lab.Vanilla.TryAdmitResident(lab.Witness));
            Assert.True(lab.Can("host", lab.Witness).IsAvailable);
        }

        /// <summary>
        /// Background simulation must not hand the player omniscience. A plight nobody has told
        /// them about is not an offer, even though the fact is sitting in the graph.
        /// </summary>
        [Fact]
        public void APlightThePlayerHasNotHeardOfIsNotAnOffer()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);

            // Hobb is in trouble too, and has told nobody. The claim is true and sitting in the
            // graph; the player has no way of knowing it, so there is no offer.
            Fact quiet = new Fact(
                lab.World.NewId("fact"), lab.Neighbour, FactPredicates.AtRisk, lab.Hunter, "debtor", TruthState.True);
            lab.World.Knowledge.AddFact(quiet);
            lab.Situation.Thread.FactIds.Add(quiet.Id);

            Availability unheard = lab.Can("shelter", lab.Neighbour);

            Assert.False(unheard.IsAvailable);
            Assert.Contains("nowhere to go", unheard.Reason);

            // Told, it becomes one - the fact did not change, only who has heard it.
            lab.World.Knowledge.Teach(lab.Player, quiet.Id, KnowledgeSource.Hearsay, 0.8, lab.Vanilla.Now, false);
            Assert.True(lab.Can("shelter", lab.Neighbour).IsAvailable);
        }

        // -- the four outcomes ---------------------------------------------------------------

        [Fact]
        public void ARefusedOfferUndertakesNothingAndLeavesHerExposed()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.False(outcome.Succeeded);
            Assert.Empty(outcome.Events);
            Assert.Null(lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy));
            Assert.False(lab.Vanilla.GetHomeState().IsResident(lab.Witness));
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
        }

        /// <summary>
        /// The cost that makes it a decision. A badly made offer is made in public, and what the
        /// street takes away from it is that this woman has something to be afraid of.
        /// </summary>
        [Fact]
        public void ABotchedOfferTellsTheStreetWhatSheHasToFear()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.CriticalFail);
            Assert.False(lab.World.Knowledge.Knows(lab.Neighbour, lab.Situation.ExposureFactId));

            ActionOutcome outcome = lab.Run("shelter", lab.Witness);

            Assert.False(outcome.Succeeded);
            Assert.False(lab.Vanilla.GetHomeState().IsResident(lab.Witness));
            Assert.True(lab.World.Knowledge.Knows(lab.Neighbour, lab.Situation.ExposureFactId));
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
        }

        /// <summary>
        /// The two successes are not the same success. A door opened gladly is worth more to the
        /// person coming through it, and travels further, than one opened after an argument.
        /// </summary>
        [Fact]
        public void AGenerousWelcomeIsWorthMoreThanAGrudgingOne()
        {
            SanctuaryLab grudging = SanctuaryLab.Create(CheckOutcome.Pass);
            SanctuaryLab generous = SanctuaryLab.Create(CheckOutcome.CriticalPass);

            grudging.Run("shelter", grudging.Witness);
            generous.Run("shelter", generous.Witness);

            Assert.True(generous.Vanilla.GetAffinity(generous.Witness) > grudging.Vanilla.GetAffinity(grudging.Witness));
            Assert.True(generous.Vanilla.Fame > grudging.Vanilla.Fame);

            // Both put her in a bed, though: the settlement does not care how it went.
            Assert.True(grudging.Vanilla.GetHomeState().IsResident(grudging.Witness));
            Assert.True(generous.Vanilla.GetHomeState().IsResident(generous.Witness));
        }

        /// <summary>
        /// BQ-048. A bed in a rough settlement answers the immediate exposure, but it does not
        /// make the undertaking quiet. Public Safety is the difference between a closed shelter
        /// story and one that can bring the hunter to the player's Home later.
        /// </summary>
        [Fact]
        public void LowPublicSafetyTurnsShelteringIntoLaterDiscoveryRisk()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 3));

            ActionOutcome shelter = lab.Run("shelter", lab.Witness);

            Assert.True(shelter.Succeeded);
            Assert.True(lab.Vanilla.GetHomeState().IsResident(lab.Witness));
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Null(lab.Situation.Thread.Resolution);
            Assert.Contains(lab.Situation.Thread.Escalation, step => step.Id == Undertakings.ResidentDiscoveredStep);

            lab.AdvanceDays(4);

            Fact undertaking = lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy);
            Assert.NotNull(undertaking);
            Assert.True(lab.World.Knowledge.Knows(lab.Hunter, undertaking.Id));
            Assert.Contains(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.Threatened
                && e.Actor == lab.Hunter
                && e.Target == lab.Witness
                && e.Zone == SanctuaryLab.SteadingZone
                && e.Related.Contains(undertaking.Id));
            Assert.Contains(lab.Situation.Thread.FactIds, id =>
            {
                Fact fact = lab.Fact(id);
                return fact != null
                       && fact.Predicate == FactPredicates.AtRisk
                       && fact.Value == "found_at_home"
                       && fact.Truth == TruthState.True;
            });
        }

        [Fact]
        public void SafePublicSafetyLetsShelterCloseCleanly()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);

            lab.Run("shelter", lab.Witness);
            lab.AdvanceDays(4);

            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.DoesNotContain(lab.Situation.Thread.Escalation, step => step.Id == Undertakings.ResidentDiscoveredStep);
            Assert.DoesNotContain(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.Threatened
                && e.Actor == lab.Hunter
                && e.Target == lab.Witness);
        }

        /// <summary>
        /// BQ-042. The sanctuary archetype is not complete when the fugitive merely disappears
        /// into a fact. If the Home is unsafe enough for the story to leak, the consequence can
        /// come to the player's land as an actual authority actor through the normal relocation
        /// seam.
        /// </summary>
        [Fact]
        public void LeakedSanctuaryCanBringAGuardToThePlayersLand()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 2));

            Assert.NotEqual(SanctuaryLab.SteadingZone, lab.Vanilla.GetZoneOf(lab.Guard));

            lab.Run("shelter", lab.Witness);
            lab.AdvanceDays(4);

            Assert.Equal(SanctuaryLab.SteadingZone, lab.Vanilla.GetZoneOf(lab.Guard));
            Fact undertaking = lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy);
            Assert.True(lab.World.Knowledge.Knows(lab.Guard, undertaking.Id));
            Assert.Contains(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.InquiryOpened
                && e.Actor == lab.Guard
                && e.Target == lab.Witness
                && e.Zone == SanctuaryLab.SteadingZone
                && e.Related.Contains(undertaking.Id));
        }

        [Fact]
        public void GuardArrivalIsNotRecordedWhenTheMoveIsNotVerified()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 2));
            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, false);

            lab.Run("shelter", lab.Witness);
            lab.AdvanceDays(4);

            Assert.NotEqual(SanctuaryLab.SteadingZone, lab.Vanilla.GetZoneOf(lab.Guard));
            Assert.DoesNotContain(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.InquiryOpened
                && e.Actor == lab.Guard
                && e.Zone == SanctuaryLab.SteadingZone);
        }

        // -- one primitive, four undertakings -------------------------------------------------

        /// <summary>
        /// The route that survives a full house. It spends no bed and needs no residency write, so
        /// it is there when sheltering is not - and it buys presence rather than safety, which is
        /// the reason it is not simply a cheaper `shelter`.
        /// </summary>
        [Fact]
        public void HostingIsTheRouteAFullHouseStillHasAndItDoesNotEndTheDanger()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA, SanctuaryLab.ResidentB)
                    .WithCapacity(2)
                    .WithMetric(HomeMetric.Safety, 40));

            Assert.False(lab.Can("shelter", lab.Witness).IsAvailable);
            ActionOutcome outcome = lab.Run("host", lab.Witness);

            Assert.True(outcome.Succeeded);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.TakenIn);
            Assert.Equal(Undertakings.Guest, lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy).Value);
            Assert.Equal(2, lab.Vanilla.GetHomeState().ResidentCount);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
        }

        /// <summary>
        /// A watch spends people rather than beds, and Elin's own Public Safety decides whether
        /// the settlement is in a position to keep one at all.
        /// </summary>
        [Fact]
        public void AWatchNeedsPeopleAndAPlaceThatCanKeepItself()
        {
            SanctuaryLab slum = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 3));
            SanctuaryLab unread = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone, SanctuaryLab.ResidentA)
                    .WithCapacity(4));
            SanctuaryLab empty = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40));

            Assert.Contains("cannot keep itself safe", slum.Can("assign_protection", slum.Witness).Reason);
            Assert.Contains("how safe your home is", unread.Can("assign_protection", unread.Witness).Reason);
            Assert.Contains("nobody at your home to stand a watch", empty.Can("assign_protection", empty.Witness).Reason);

            // Sheltering is still open in every one of them: a settlement too rough to guard
            // anybody can still put a person in a bed.
            Assert.True(slum.Can("shelter", slum.Witness).IsAvailable);
            Assert.True(empty.Can("shelter", empty.Witness).IsAvailable);
        }

        [Fact]
        public void AWatchEndsTheDangerWithoutSpendingABed()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            int residentsBefore = lab.Vanilla.GetHomeState().ResidentCount;

            ActionOutcome outcome = lab.Run("assign_protection", lab.Witness);

            Assert.True(outcome.Succeeded);
            Assert.Equal(Undertakings.Watched, lab.World.Knowledge.FindFact(lab.Witness, FactPredicates.ShelteredBy).Value);
            Assert.Equal(residentsBefore, lab.Vanilla.GetHomeState().ResidentCount);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
        }

        /// <summary>
        /// Recruiting reaches the same act from the other direction: not somebody who needs a
        /// place, but somebody worth having one. It is refused when the settlement already has
        /// that trade, and an unread job never counts as nobody doing the work.
        /// </summary>
        [Fact]
        public void ASpecialistIsOfferedAPlaceOnlyWhereTheSettlementLacksTheTrade()
        {
            SanctuaryLab already = SanctuaryLab.Create(
                CheckOutcome.Pass,
                new HomeStateBuilder(SanctuaryLab.SteadingZone, "Coldbeck steading")
                    .AddResident(SanctuaryLab.ResidentA, "Ivar", "weaver")
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40));
            SanctuaryLab unreadJob = SanctuaryLab.Create(
                CheckOutcome.Pass,
                new HomeStateBuilder(SanctuaryLab.SteadingZone, "Coldbeck steading")
                    .AddResident(SanctuaryLab.ResidentA, "Ivar")
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40));

            Assert.Contains("already does that", already.Can("recruit_specialist", already.Witness).Reason);
            Assert.True(unreadJob.Can("recruit_specialist", unreadJob.Witness).IsAvailable);

            ActionOutcome outcome = unreadJob.Run("recruit_specialist", unreadJob.Witness);

            Assert.True(outcome.Succeeded);
            Assert.True(unreadJob.Vanilla.GetHomeState().IsResident(unreadJob.Witness));
            Assert.Equal(Undertakings.Specialist, unreadJob.World.Knowledge.FindFact(unreadJob.Witness, FactPredicates.ShelteredBy).Value);
        }

        /// <summary>
        /// The mod moves the resident roll and nothing else. Every Home Skill element is exactly
        /// where the game left it, because they are the game's arithmetic over who lives there
        /// (decision D018).
        /// </summary>
        [Fact]
        public void AdmittingSomebodyNeverWritesTheSettlementsOwnNumbers()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            HomeState before = lab.Vanilla.GetHomeState();

            lab.Run("shelter", lab.Witness);

            HomeState after = lab.Vanilla.GetHomeState();
            foreach (HomeMetric metric in new[] { HomeMetric.Safety, HomeMetric.Food, HomeMetric.Administration })
            {
                Assert.Equal(before.KnowsMetric(metric), after.KnowsMetric(metric));
                Assert.Equal(before.GetMetric(metric), after.GetMetric(metric));
            }

            Assert.Equal(before.Capacity, after.Capacity);
        }

        [Fact]
        public void NobodyIsTakenInTwice()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);

            lab.Run("shelter", lab.Witness);
            Availability again = lab.Can("host", lab.Witness);

            Assert.False(again.IsAvailable);
            Assert.Contains("already under your roof", again.Reason);
        }

        // -- supplies ------------------------------------------------------------------------

        /// <summary>
        /// The settlement answers a shortage through its own Home capacity. It reads Elin's fFood
        /// metric as a precondition rather than inventing a larder, and a settlement without the
        /// capacity is refused outright instead of being given long odds.
        /// </summary>
        [Fact]
        public void SuppliesComeOutOfARealSurplusOrNotAtAll()
        {
            SanctuaryLab rich = SanctuaryLab.WithShortage(CheckOutcome.Pass, food: 60);
            SanctuaryLab poor = SanctuaryLab.WithShortage(CheckOutcome.Pass, food: 2);
            SanctuaryLab unread = SanctuaryLab.WithShortage(CheckOutcome.Pass, food: null);

            Assert.Contains("no food to spare", poor.Can("provide_supplies", poor.Neighbour).Reason);
            Assert.Contains("will not say what your home's food is", unread.Can("provide_supplies", unread.Neighbour).Reason);
            Assert.True(rich.Can("provide_supplies", rich.Neighbour).IsAvailable);

            ActionOutcome outcome = rich.Run("provide_supplies", rich.Neighbour);

            Assert.True(outcome.Succeeded);
            Assert.Equal(TruthState.Superseded, rich.Fact(rich.ShortageFactId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped);
            Assert.True(rich.Vanilla.GetAffinity(rich.Neighbour) > 0);
        }

        [Fact]
        public void SuppliesThatNeverArriveLeaveTheShortageAndTheDisappointment()
        {
            SanctuaryLab lab = SanctuaryLab.WithShortage(CheckOutcome.CriticalFail, food: 60);
            int affinityBefore = lab.Vanilla.GetAffinity(lab.Neighbour);

            ActionOutcome outcome = lab.Run("provide_supplies", lab.Neighbour);

            Assert.False(outcome.Succeeded);
            Assert.Equal(TruthState.True, lab.Fact(lab.ShortageFactId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.PromiseBroken);
            Assert.True(lab.Vanilla.GetAffinity(lab.Neighbour) < affinityBefore);
        }

        // -- custody -------------------------------------------------------------------------

        /// <summary>
        /// What storing evidence actually buys: the object leaves the one pack `destroy_evidence`
        /// can reach, and the case it substantiates is still provable afterwards. Moving a thing
        /// is not unmaking it.
        /// </summary>
        [Fact]
        public void EvidenceLeftAtHomeIsOutOfReachOfTheVerbThatWouldBurnIt()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.KillingFactId, KnowledgeSource.Document, 0.9, lab.Vanilla.Now, true);
            Assert.True(lab.Can("destroy_evidence", lab.Witness).IsAvailable);

            ActionOutcome outcome = lab.Run("store_evidence", lab.Witness);

            Assert.True(outcome.Succeeded);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.TakenIn);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), i => i.Id == lab.Situation.DepositionId);
            Assert.Contains(lab.Vanilla.GetInventory(SanctuaryLab.ResidentA), i => i.Id == lab.Situation.DepositionId);
            Assert.False(lab.Can("destroy_evidence", lab.Witness).IsAvailable);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.KillingFactId));
        }

        /// <summary>Leaving the proof of a killing with the killer is not safekeeping.</summary>
        [Fact]
        public void EvidenceIsNeverLeftWithThePersonItIsAbout()
        {
            SanctuaryLab lab = SanctuaryLab.Create(
                CheckOutcome.Pass,
                HuntedWitnessSituation.Smallholding(SanctuaryLab.SteadingZone)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40),
                hunterLivesAtHome: true);

            Availability store = lab.Can("store_evidence", lab.Hunter);

            Assert.False(store.IsAvailable);
            Assert.Contains("nobody at your home to leave it with", store.Reason);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), i => i.Id == lab.Situation.DepositionId);
        }

        // -- route diversity ------------------------------------------------------------------

        /// <summary>
        /// The laboratory's own review question. A hunted witness is answerable by the settlement,
        /// and the Home family is not the only way through - but it is a way through that no
        /// amount of Charisma substitutes for.
        /// </summary>
        [Fact]
        public void TheSituationOffersTheHomeFamilyBesideTheOthers()
        {
            SanctuaryLab lab = SanctuaryLab.Create(CheckOutcome.Pass);

            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (EntityId target in new[] { lab.Witness, lab.Hunter, lab.Neighbour })
            {
                families.UnionWith(lab.Actions.AvailableFamilies(lab.Context(target)));
            }

            Assert.Contains(ActionFamily.HomeCommunity, families);
            Assert.True(families.Count >= 3, "expected 3+ solution families, got " + families.Count);
        }

        private sealed class SanctuaryLab
        {
            internal static readonly EntityId SteadingZone = EntityId.Parse("zone_steading");
            internal static readonly EntityId ResidentA = EntityId.Parse("npc_resident_a");
            internal static readonly EntityId ResidentB = EntityId.Parse("npc_resident_b");

            private SanctuaryLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public ThreadEngine Threads { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public HuntedWitnessSituation Situation { get; private set; }

            /// <summary>The thread offers are drawn against. The situation's own, unless a test
            /// puts a different problem in front of the player.</summary>
            public NarrativeThread Thread { get; private set; }

            /// <summary>Set only by <see cref="WithShortage"/>.</summary>
            public EntityId ShortageFactId { get; private set; }

            public EntityId Witness => Situation.WitnessId;

            public EntityId Hunter => Situation.HunterId;

            public EntityId Neighbour => Situation.NeighbourId;

            public EntityId Guard => Situation.GuardId;

            /// <summary>The settlement this laboratory assumes unless a test says otherwise.</summary>
            private static HomeStateBuilder DefaultHome()
            {
                return HuntedWitnessSituation.Smallholding(SteadingZone, ResidentA, ResidentB)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40)
                    .WithMetric(HomeMetric.Food, 30)
                    .WithMetric(HomeMetric.Administration, 20);
            }

            public static SanctuaryLab Create(
                CheckOutcome outcome,
                HomeStateBuilder home = null,
                bool hunterLivesAtHome = false)
            {
                return Build(outcome, home ?? DefaultHome(), hunterLivesAtHome);
            }

            /// <summary>The no-Home case, which is a different world state from an empty Home.</summary>
            public static SanctuaryLab WithNoHome(CheckOutcome outcome)
            {
                return Build(outcome, null, false);
            }

            /// <summary>
            /// The same lane, plus a neighbour who is short of food. Used for the supply route,
            /// which answers the same `needs` demand the crafts answer.
            /// </summary>
            public static SanctuaryLab WithShortage(CheckOutcome outcome, int? food)
            {
                HomeStateBuilder home = HuntedWitnessSituation.Smallholding(SteadingZone, ResidentA, ResidentB)
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 40)
                    .WithMetric(HomeMetric.Administration, 20);
                if (food.HasValue)
                {
                    home.WithMetric(HomeMetric.Food, food.Value);
                }

                SanctuaryLab lab = Build(outcome, home, false);

                Fact shortage = new Fact(
                    lab.World.NewId("fact"),
                    lab.Neighbour,
                    FactPredicates.Needs,
                    EntityId.None,
                    new ProductionSpec("food").ToFactValue(),
                    TruthState.True);
                lab.World.Knowledge.AddFact(shortage);
                lab.World.Knowledge.Teach(lab.Player, shortage.Id, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, false);
                // Its own thread: a hamlet short of food is a different problem from a woman
                // being hunted, and hanging it on her thread would have a delivered cartload
                // closing her situation.
                NarrativeThread hunger = new NarrativeThread(lab.World.NewId("thread"), "local_shortage", lab.Vanilla.Now)
                {
                    State = ThreadState.Active
                };
                hunger.ParticipantIds.Add(lab.Neighbour);
                hunger.FactIds.Add(shortage.Id);
                lab.World.Threads.Add(hunger);
                lab.Thread = hunger;
                lab.ShortageFactId = shortage.Id;
                return lab;
            }

            private static SanctuaryLab Build(CheckOutcome outcome, HomeStateBuilder home, bool hunterLivesAtHome)
            {
                SanctuaryLab lab = new SanctuaryLab();
                NarrativeWorldState world = new NarrativeWorldState(27027);
                EntityId player = world.NewId("npc");
                EntityId lane = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 9, money: 400, zone: lane);
                vanilla.SetAttribute(player, VanillaAttribute.Charisma, 14);
                vanilla.SetAttribute(player, VanillaAttribute.Will, 13);
                vanilla.SetAttribute(player, VanillaAttribute.Perception, 12);
                vanilla.SetAttribute(player, VanillaAttribute.Learning, 12);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 14);
                vanilla.SetSkill(player, VanillaSkill.Travel, 10);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Threads = new ThreadEngine();
                lab.Threads.Register(HuntedWitnessSituation.ArchetypeId, new HuntedWitnessEscalation(vanilla));
                lab.Checks = new FixedCheckResolver(outcome);

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = HuntedWitnessSituation.Create(world, stager, player, lane, vanilla.Now);
                lab.Thread = lab.Situation.Thread;

                if (home != null)
                {
                    if (hunterLivesAtHome)
                    {
                        home.AddResident(lab.Situation.HunterId, "Brann");
                    }

                    vanilla.SetHome(home.Build());
                }

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            public ActionContext Context(EntityId target)
            {
                ActionContext context = new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Thread
                };

                // The lane is not empty: whoever else is standing in it can see what is offered.
                foreach (EntityId here in Vanilla.GetCharactersInZone(Situation.LaneZoneId))
                {
                    if (here != Player && here != target)
                    {
                        context.Witnesses.Add(here);
                    }
                }

                return context;
            }

            public ActionOutcome Run(string actionId, EntityId target)
            {
                return Actions.Get(actionId).Perform(Context(target));
            }

            public Availability Can(string actionId, EntityId target)
            {
                return Actions.Get(actionId).GetAvailability(Context(target));
            }

            public int AdvanceDays(long days)
            {
                Vanilla.AdvanceDays(days);
                return Threads.Advance(World, Vanilla.Now);
            }
        }
    }
}
