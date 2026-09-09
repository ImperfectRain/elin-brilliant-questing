using System;
using System.Diagnostics;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;
using Xunit.Abstractions;

namespace BrilliantQuesting.Tests
{
    public sealed class SimulationTierTests
    {
        private readonly ITestOutputHelper _output;
        public SimulationTierTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void ExistingStateDrivesTierChangesAndRegistryReplacement()
        {
            var registry = new EntityRegistry();
            var npc = registry.Add(new NarrativeNpc(EntityId.Parse("actor"), "Actor"));
            Assert.Equal(SimulationTier.Cold, npc.BackgroundTier);
            var goal = new NpcGoal("court", EntityId.None, 50);
            npc.Goals.Add(goal);
            Assert.Same(npc, Assert.Single(registry.TakeSimulationActors(1, 0)));
            goal.Satisfied = true;
            Assert.Empty(registry.TakeSimulationActors(1, 0));
            goal.Satisfied = false;
            goal.Weight = 0;
            Assert.Equal(SimulationTier.Cold, npc.BackgroundTier);
            goal.Weight = 50;
            npc.Goals.Clear();
            Assert.Empty(registry.TakeSimulationActors(1, 0));
            npc.Promote(NarrativeImportance.Known);
            Assert.Single(registry.TakeSimulationActors(1, 0));
            npc.Alive = false;
            Assert.Equal(SimulationTier.Archived, npc.BackgroundTier);
            Assert.Empty(registry.TakeSimulationActors(10, 10));
            npc.Alive = true;
            var replacement = registry.Add(new NarrativeNpc(npc.Id, "Replacement"));
            npc.Alive = false; // detached objects cannot corrupt the index
            Assert.Same(replacement, Assert.Single(registry.TakeSimulationActors(10, 10)));
            var canonical = registry.Add(new NarrativeNpc(EntityId.Parse("canonical"), "Canonical"));
            Assert.True(registry.Retire(replacement.Id, canonical.Id));
            Assert.Same(canonical, Assert.Single(registry.TakeSimulationActors(10, 10)));
        }

        [Fact]
        public void ThousandsOfHistoricalActorsHaveBoundedTickWork()
        {
            var world = new NarrativeWorldState(107);
            var player = EntityId.Parse("player");
            var vanilla = new SandboxVanillaState(player);
            for (int i = 0; i < 10000; i++)
            {
                world.Registry.Add(new NarrativeNpc(EntityId.Parse("history_" + i), "History") { Alive = false });
                var cold = world.Registry.Add(new NarrativeNpc(EntityId.Parse("cold_" + i), "Cold"));
                vanilla.Define(cold.Id);
            }
            for (int i = 0; i < 12; i++)
            {
                var warm = world.Registry.Add(new NarrativeNpc(EntityId.Parse("warm_" + i), "Warm") { Importance = NarrativeImportance.Known });
                vanilla.Define(warm.Id);
            }
            var schemes = new OffScreenSchemes();
            var actions = new ActionRegistry();
            var checks = new FixedCheckResolver(CheckOutcome.Pass);
            schemes.Advance(world, vanilla, checks, actions, GameTime.FromDays(30)); // warm up
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                schemes.Advance(world, vanilla, checks, actions, GameTime.FromDays(31 + i));
                Assert.Equal(14, schemes.LastActorsInspected);
            }
            watch.Stop();
            _output.WriteLine("20,012 actors; 1,000 ticks: {0} ms; budget 2,000 ms; 14 inspections/tick", watch.ElapsedMilliseconds);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "1,000 bounded scheduler ticks must fit 2 seconds (setup excluded).");
            Assert.Equal(0, world.Ledger.Count);
            Assert.Equal(GameTime.Zero, world.Registry.GetNpc(EntityId.Parse("history_0")).LastSimulatedAt);
        }

        [Fact]
        public void ColdQueueRotatesAndRebuildsFromExistingSaveFields()
        {
            var world = new NarrativeWorldState(107);
            for (int i = 0; i < 5; i++) world.Registry.Add(new NarrativeNpc(EntityId.Parse("cold_" + i), "Cold"));
            var first = world.Registry.TakeSimulationActors(0, 2);
            var second = world.Registry.TakeSimulationActors(0, 2);
            Assert.Empty(first.Intersect(second));
            var restored = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Assert.Equal(5, restored.Registry.TakeSimulationActors(0, 10).Count);
            Assert.All(restored.Registry.Npcs.Values, n => Assert.Equal(SimulationTier.Cold, n.BackgroundTier));
        }

        [Fact]
        public void HomeRevisitReadsVanillaCatchUpWithoutReplayingItAcrossReload()
        {
            var world = new NarrativeWorldState(107);
            var player = EntityId.Parse("player");
            var homeId = EntityId.Parse("home");
            var npc = world.Registry.Add(new NarrativeNpc(EntityId.Parse("resident"), "Resident"));
            npc.Goals.Add(new NpcGoal("court", player, 50));
            var vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, zone: homeId);
            vanilla.Define(npc.Id, zone: homeId);
            vanilla.SetActorActivity(npc.Id, new ActorActivityBuilder(npc.Id).WithPresence(PhysicalPresence.InActiveZone).Build());
            var home = new HomeStateBuilder(homeId, "Home").AddResident(npc.Id, npc.Name, "farmer")
                .WithMetric(HomeMetric.Food, 73).Build();
            vanilla.SetHome(home); // models readback AFTER vanilla's revisit processing
            var schemes = new OffScreenSchemes();
            var now = GameTime.FromDays(30);
            schemes.ReconcileZone(world, vanilla, now);
            Assert.Same(home, schemes.LastHomeObservation);
            Assert.Equal(SimulationTier.Active, OffScreenSchemes.TierOf(npc, vanilla.GetActorActivity(npc.Id)));
            Assert.Equal(now, npc.LastSimulatedAt);
            var restored = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            schemes.ReconcileZone(restored, vanilla, now);
            schemes.Advance(restored, vanilla, new FixedCheckResolver(CheckOutcome.Pass), new ActionRegistry(), now);
            Assert.Equal(0, restored.Ledger.Count);
            Assert.Empty(schemes.LastPass);
            Assert.Same(home, vanilla.GetHomeState());
            Assert.True(vanilla.GetHomeState().TryGetMetric(HomeMetric.Food, out int food));
            Assert.Equal(73, food);
            Assert.Equal(now, restored.Registry.GetNpc(npc.Id).LastSimulatedAt);
            vanilla.SetHome(null);
            schemes.ReconcileZone(restored, vanilla, now);
            Assert.Null(schemes.LastHomeObservation);
        }
    }
}
