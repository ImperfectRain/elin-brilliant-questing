using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-124. Worst outcomes are priced setbacks with routes back, not deleted content.
    /// </summary>
    public class RecoveryRouteTests
    {
        [Fact]
        public void ImplementedCanonicalArchetypesDocumentRecoveryFromTheirWorstOutcome()
        {
            foreach (NarrativeThread thread in ImplementedCanonicalArchetypes())
            {
                RecoveryRoute route = Assert.Single(thread.RecoveryRoutes);
                Assert.False(string.IsNullOrWhiteSpace(route.WorstOutcome), thread.ArchetypeId);
                Assert.False(string.IsNullOrWhiteSpace(route.ActionId), thread.ArchetypeId);
                Assert.False(string.IsNullOrWhiteSpace(route.Price), thread.ArchetypeId);
                Assert.False(string.IsNullOrWhiteSpace(route.Uncertainty), thread.ArchetypeId);
                Assert.False(string.IsNullOrWhiteSpace(route.Restores), thread.ArchetypeId);
            }
        }

        [Fact]
        public void RecoveryDocumentationSurvivesSaveLoad()
        {
            NarrativeThread thread = CreateDistressedBusiness().Thread;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(threadWorld));

            RecoveryRoute route = Assert.Single(reloaded.GetThread(thread.Id).RecoveryRoutes);
            Assert.Equal("business_failed", route.WorstOutcome);
            Assert.Equal("reopen_business", route.ActionId);
        }

        [Fact]
        public void InspectorShowsTheRecoveryRoute()
        {
            NarrativeThread thread = CreateDistressedBusiness().Thread;

            string report = NarrativeInspector.DescribeThread(threadWorld, thread);

            Assert.Contains("recovery routes:", report);
            Assert.Contains("business_failed -> reopen_business", report);
            Assert.Contains("three times the original debt", report);
        }

        [Fact]
        public void AFailedBusinessCanBeReopenedAtAHighUncertainPrice()
        {
            DistressedBusinessSituation situation = CreateDistressedBusiness(playerMoney: 5000);
            ThreadEngine engine = new ThreadEngine();
            engine.Register(DistressedBusinessSituation.ArchetypeId, new DistressedBusinessEscalation());
            engine.Advance(threadWorld, GameTime.FromDays(8));
            Assert.Equal(BusinessContinuityState.Failed, threadWorld.Businesses.Of(situation.BusinessId).State);
            Assert.Equal("business_failed", situation.Thread.Resolution);

            ActionContext context = new ActionContext(
                threadWorld,
                vanilla,
                new FixedCheckResolver(CheckOutcome.Pass),
                threadWorld.Rng,
                player,
                situation.OwnerId)
            {
                Thread = situation.Thread,
                ThirdParty = situation.CreditorId
            };

            ActionOutcome outcome = StandardActions.CreateRegistry().Get("reopen_business").Perform(context);

            Assert.True(outcome.Succeeded);
            Assert.Equal(1400, vanilla.GetMoney(player));
            Assert.Equal(BusinessContinuityState.Recovered, threadWorld.Businesses.Of(situation.BusinessId).State);
            Assert.Equal(ThreadState.Resolved, situation.Thread.State);
            Assert.Equal("business_failed", situation.Thread.Resolution);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped);
            Assert.Contains(threadWorld.Ledger.Events, e => e.Type == WorldEventType.BusinessStateChanged && e.Tags.Contains("Recovered"));
        }

        [Fact]
        public void FailedRecoveryStillCostsMoneyAndLeavesTheFailureInPlace()
        {
            DistressedBusinessSituation situation = CreateDistressedBusiness(playerMoney: 5000);
            ThreadEngine engine = new ThreadEngine();
            engine.Register(DistressedBusinessSituation.ArchetypeId, new DistressedBusinessEscalation());
            engine.Advance(threadWorld, GameTime.FromDays(8));

            ActionContext context = new ActionContext(
                threadWorld,
                vanilla,
                new FixedCheckResolver(CheckOutcome.Fail),
                threadWorld.Rng,
                player,
                situation.OwnerId)
            {
                Thread = situation.Thread,
                ThirdParty = situation.CreditorId
            };

            ActionOutcome outcome = StandardActions.CreateRegistry().Get("reopen_business").Perform(context);

            Assert.False(outcome.Succeeded);
            Assert.Equal(1400, vanilla.GetMoney(player));
            Assert.Equal(BusinessContinuityState.Failed, threadWorld.Businesses.Of(situation.BusinessId).State);
            Assert.DoesNotContain(threadWorld.Ledger.Events, e => e.Type == WorldEventType.BusinessStateChanged && e.Tags.Contains("Recovered"));
        }

        private NarrativeWorldState threadWorld;
        private SandboxVanillaState vanilla;
        private EntityId player;

        private IEnumerable<NarrativeThread> ImplementedCanonicalArchetypes()
        {
            yield return CreatePettyTheft().Thread;
            yield return CreateShortage().Thread;
            yield return CreateHuntedWitness().Thread;
            yield return CreateFalseAccusation().Thread;
            yield return CreateDistressedBusiness().Thread;
            yield return CreateMaraudingBeast().Thread;
            yield return CreateFestival().Thread;
        }

        private PettyTheftSituation CreatePettyTheft()
        {
            Reset(101);
            return PettyTheftSituation.Create(threadWorld, new SandboxStager(vanilla), zone, vanilla.Now, 11);
        }

        private ShortageSituation CreateShortage()
        {
            Reset(102);
            return ShortageSituation.Create(threadWorld, new SandboxStager(vanilla), player, zone, vanilla.Now);
        }

        private HuntedWitnessSituation CreateHuntedWitness()
        {
            Reset(103);
            return HuntedWitnessSituation.Create(threadWorld, new SandboxStager(vanilla), player, zone, vanilla.Now);
        }

        private FalseAccusationSituation CreateFalseAccusation()
        {
            Reset(104);
            return FalseAccusationSituation.Create(threadWorld, new SandboxStager(vanilla), player, zone, vanilla.Now);
        }

        private DistressedBusinessSituation CreateDistressedBusiness(int playerMoney = 5000)
        {
            Reset(105, playerMoney);
            return DistressedBusinessSituation.Create(threadWorld, new SandboxStager(vanilla), zone, vanilla.Now);
        }

        private MaraudingBeastSituation CreateMaraudingBeast()
        {
            Reset(106);
            return MaraudingBeastSituation.Create(threadWorld, new SandboxStager(vanilla), player, zone, vanilla.Now);
        }

        private FestivalCompetitionSituation CreateFestival()
        {
            Reset(107);
            return FestivalCompetitionSituation.Create(threadWorld, new SandboxStager(vanilla), player, zone, vanilla.Now);
        }

        private EntityId zone;

        private void Reset(ulong seed, int playerMoney = 1000)
        {
            threadWorld = new NarrativeWorldState(seed);
            player = threadWorld.NewId("npc");
            zone = threadWorld.NewId("zone");
            threadWorld.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });
            vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, level: 8, money: playerMoney, zone: zone)
                .SetSkill(player, VanillaSkill.Investing, 12)
                .SetSkill(player, VanillaSkill.Negotiation, 8);
        }
    }
}
