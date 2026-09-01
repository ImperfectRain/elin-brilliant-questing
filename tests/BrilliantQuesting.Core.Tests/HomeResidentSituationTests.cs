using System.Linq;
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
    public class HomeResidentSituationTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Home = EntityId.Parse("zone_home");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Resident = EntityId.Parse("npc_resident");

        [Fact]
        public void HomeResidentCanOriginateSituationWithoutLocalPresence()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            world.Registry.Add(new NarrativeNpc(Player, "You") { Importance = NarrativeImportance.Major });
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Town);
            vanilla.SetHome(LowFoodHome());

            HomeResidentSituation situation = HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now);

            Assert.NotNull(situation);
            Assert.Single(world.Threads);
            Assert.Equal(HomeResidentSituation.ArchetypeId, situation.Thread.ArchetypeId);
            Assert.Equal(ThreadState.Active, situation.Thread.State);
            Assert.Contains(Resident, situation.Thread.ParticipantIds);
            Assert.Contains(Home, situation.Thread.SiteIds);
            Assert.Contains(situation.Thread.GenerationCauses, c => c.Contains("Home resident roll"));

            NarrativeNpc npc = world.Registry.GetNpc(Resident);
            Assert.NotNull(npc);
            Assert.Equal(Home, npc.HomeSiteId);
            Assert.Equal("cook", npc.Occupation);
            Assert.Contains(npc.Goals, g => g.Kind == "keep_home_fed" && g.Subject == Home);

            Fact need = world.Knowledge.GetFact(situation.NeedFactId);
            Assert.NotNull(need);
            Assert.Equal(FactPredicates.Needs, need.Predicate);
            Assert.Equal(Resident, need.Subject);
            Assert.Equal("food quality 20", need.Value);
            Assert.True(world.Knowledge.TryGetBelief(Player, need.Id, out KnowledgeRecord heard));
            Assert.Equal(KnowledgeSource.Hearsay, heard.Source);
            Assert.Equal(Resident, heard.ToldBy);

            Assert.NotNull(world.Demands.Get(Home, LocalDemandCategory.Food, need.Id));
            Assert.Contains(NarrativeContentProjection.BoardEntries(world, Player), entry =>
                entry.ContentClass == NarrativeContentClass.Request && entry.FactId == need.Id);
        }

        [Fact]
        public void HomeBelowFoodSupportedCapacityDoesNotInventResidentProblem()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.SetHome(new HomeStateBuilder(Home, "Willow Hall")
                .WithCapacity(4)
                .AddResident(Resident, "Mara", "cook")
                .WithMetric(HomeMetric.Food, 12)
                .Build());

            Assert.Null(HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now));
            Assert.Empty(world.Threads);
            Assert.Empty(world.Knowledge.Facts);
        }

        [Fact]
        public void UnreadFoodMetricDoesNotInventResidentProblem()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.SetHome(new HomeStateBuilder(Home, "Willow Hall")
                .WithCapacity(1)
                .AddResident(Resident, "Mara", "cook")
                .Build());

            Assert.Null(HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now));
            Assert.Empty(world.Threads);
            Assert.Empty(world.Knowledge.Facts);
        }

        [Fact]
        public void LiveResidentProblemSuppressesDuplicate()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.SetHome(LowFoodHome());

            HomeResidentSituation first = HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now);
            HomeResidentSituation second = HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now);

            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Single(world.Threads);
            Assert.Single(world.Knowledge.Facts.Values, f => f.Predicate == FactPredicates.Needs);
        }

        [Fact]
        public void DormantResidentProblemIsReactivatedRatherThanDuplicated()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.SetHome(LowFoodHome());
            HomeResidentSituation first = HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now);
            ThreadEngine threads = new ThreadEngine();
            threads.Register(HomeResidentSituation.ArchetypeId, new HomeResidentEscalation());
            threads.Advance(world, vanilla.Now.PlusDays(5));

            Assert.Equal(ThreadState.Dormant, first.Thread.State);

            HomeResidentSituation reactivated = HomeResidentSituation.TryGenerate(
                world,
                vanilla,
                vanilla.Now.PlusDays(7));

            Assert.NotNull(reactivated);
            Assert.Equal(first.Thread.Id, reactivated.Thread.Id);
            Assert.Equal(ThreadState.Active, first.Thread.State);
            Assert.Contains("Home pressure still exists", first.Thread.LifecycleReason);
            Assert.Single(world.Threads);
            Assert.Single(world.Knowledge.Facts.Values, f => f.Predicate == FactPredicates.Needs);
            Assert.Single(world.Ledger.OfType(WorldEventType.ThreadReactivated), e => e.ThreadId == first.Thread.Id);
        }

        [Fact]
        public void ResidentFoodProblemEscalatesIntoHistory()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.SetHome(LowFoodHome());
            HomeResidentSituation situation = HomeResidentSituation.TryGenerate(world, vanilla, vanilla.Now);
            ThreadEngine threads = new ThreadEngine();
            threads.Register(HomeResidentSituation.ArchetypeId, new HomeResidentEscalation());

            int applied = threads.Advance(world, vanilla.Now.PlusDays(5));

            Assert.Equal(1, applied);
            Assert.Contains("home_resident_problem/household_pressure_mounts", threads.LastApplied);
            Assert.Contains(world.Ledger.Events, e =>
                e.Type == WorldEventType.Harmed
                && e.Actor == Resident
                && e.Zone == Home
                && e.ThreadId == situation.Thread.Id
                && e.Related.Contains(situation.NeedFactId));
        }

        private static HomeState LowFoodHome()
        {
            return new HomeStateBuilder(Home, "Willow Hall")
                .WithCapacity(1)
                .AddResident(Resident, "Mara", "cook")
                .WithMetric(HomeMetric.Food, 12)
                .Build();
        }
    }
}
