using System.Linq;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class VanillaActionRecorderTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Ring = EntityId.Parse("item_42");
        private static readonly EntityId Zone = EntityId.Parse("zone_7");

        [Fact]
        public void ObservedTheftAppendsTheftEventWithTheRealItem()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Thief, zone: Zone);
            vanilla.Define(Victim, zone: Zone);
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Thief,
                Victim,
                Ring,
                "silver ring",
                Zone,
                "AI_Steal"));

            Assert.NotNull(recorded);
            Assert.Equal(WorldEventType.Theft, recorded.Type);
            Assert.Equal(Thief, recorded.Actor);
            Assert.Equal(Victim, recorded.Target);
            Assert.Equal(Zone, recorded.Zone);
            Assert.Contains(Ring, recorded.Evidence);
            Assert.Contains("observed_vanilla", recorded.Tags);
            Assert.Contains("AI_Steal", recorded.Tags);
        }

        [Fact]
        public void ObservedTheftCreatesAProvableTheftFactKnownByTheActor()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Thief,
                Victim,
                Ring,
                "silver ring",
                Zone,
                "ActSteal"));

            Fact fact = recorded.Related.Select(id => world.Knowledge.GetFact(id)).Single();
            Assert.Equal(Thief, fact.Subject);
            Assert.Equal(FactPredicates.Stole, fact.Predicate);
            Assert.Equal(Ring, fact.Object);
            Assert.Equal("silver ring", fact.Value);
            Assert.Contains(Ring, fact.EvidenceIds);
            Assert.True(world.Knowledge.Knows(Thief, fact.Id));
            Assert.True(world.Knowledge.CanProve(Thief, fact.Id));
        }

        [Fact]
        public void ObservedTheftWithoutAnItemIsIgnored()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Thief,
                Victim,
                EntityId.None,
                "",
                Zone,
                "AI_Steal"));

            Assert.Null(recorded);
            Assert.Empty(world.Ledger.Events);
            Assert.Empty(world.Knowledge.Facts);
        }
    }
}
