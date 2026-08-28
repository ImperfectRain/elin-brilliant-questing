using System.Linq;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class VanillaActionRecorderTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Bystander = EntityId.Parse("npc_bystander");
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
            Assert.Empty(recorded.Witnesses);
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

        [Fact]
        public void ObservedTheftWithNobodyPresentTeachesNobodyElse()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            new ConsequenceEngine(world, vanilla).Attach();
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Thief,
                Victim,
                Ring,
                "silver ring",
                Zone,
                "AI_Steal"));

            Fact fact = recorded.Related.Select(id => world.Knowledge.GetFact(id)).Single();
            Assert.True(world.Knowledge.Knows(Thief, fact.Id));
            Assert.False(world.Knowledge.Knows(Victim, fact.Id));
            Assert.False(world.Knowledge.Knows(Witness, fact.Id));
            Assert.Empty(recorded.Witnesses);
        }

        [Fact]
        public void ObservedTheftInACrowdTeachesExactlyTheWitnessesPresent()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            new ConsequenceEngine(world, vanilla).Attach();
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Thief,
                Victim,
                Ring,
                "silver ring",
                Zone,
                "AI_Steal",
                new[] { Witness }));

            Fact fact = recorded.Related.Select(id => world.Knowledge.GetFact(id)).Single();
            Assert.Contains(Witness, recorded.Witnesses);
            Assert.True(world.Knowledge.Knows(Witness, fact.Id));
            Assert.True(world.Knowledge.CanProve(Witness, fact.Id));
            Assert.False(world.Knowledge.Knows(Victim, fact.Id));
            Assert.False(world.Knowledge.Knows(Bystander, fact.Id));
        }

        [Fact]
        public void ObservedKillingIsRememberedByEveryoneWhoSawItButNotJudged()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            world.Registry.Add(new NarrativeNpc(Victim, "Victim"));
            world.Registry.Add(new NarrativeNpc(Witness, "Witness"));
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Victim, zone: Zone);
            vanilla.Define(Witness, zone: Zone);
            int victimAffinityBefore = vanilla.GetAffinity(Victim);
            int witnessAffinityBefore = vanilla.GetAffinity(Witness);
            int karmaBefore = vanilla.Karma;
            int fameBefore = vanilla.Fame;
            new ConsequenceEngine(world, vanilla).Attach();
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            WorldEvent recorded = recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Killed,
                Player,
                Victim,
                EntityId.None,
                "",
                Zone,
                "ActMelee",
                new[] { Witness }));

            Assert.NotNull(recorded);
            Assert.Equal(WorldEventType.Killed, recorded.Type);
            Assert.True(vanilla.GetAffinity(Victim) < victimAffinityBefore);
            Assert.True(vanilla.GetAffinity(Witness) < witnessAffinityBefore);
            // The people involved react - they watched somebody die. The world does not pass a
            // verdict, because the observer cannot tell murder from self-defence, a lawful bounty
            // from an assault, or clearing a dungeon from killing a shopkeeper. Karma and fame
            // wait for BQ-046 to say which it was.
            Assert.Equal(karmaBefore, vanilla.Karma);
            Assert.Equal(fameBefore, vanilla.Fame);

            MemoryRecord victimMemory = Assert.Single(world.Memories.MemoriesAbout(Victim, Player));
            Assert.Equal("killed_someone", victimMemory.SummaryTag);
            Assert.Equal(MemoryWeight.Defining, victimMemory.Weight);

            MemoryRecord witnessMemory = Assert.Single(world.Memories.MemoriesAbout(Witness, Player));
            Assert.Equal("saw_killed_someone", witnessMemory.SummaryTag);
            Assert.Equal(MemoryWeight.Important, witnessMemory.Weight);

            Fact fact = recorded.Related.Select(id => world.Knowledge.GetFact(id)).Single();
            Assert.Equal(FactPredicates.Killed, fact.Predicate);
            Assert.True(world.Knowledge.Knows(Witness, fact.Id));
        }

        /// <summary>
        /// The same act, done deliberately through a Brilliant Questing verb rather than merely
        /// seen happening, is judged. The distinction is whether anything classified it, not who
        /// swung - so the tag has to be what gates the verdict.
        /// </summary>
        [Fact]
        public void AnActTheSimulationItselfRecordsIsStillJudged()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            world.Registry.Add(new NarrativeNpc(Victim, "Victim"));
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Victim, zone: Zone);
            int karmaBefore = vanilla.Karma;
            new ConsequenceEngine(world, vanilla).Attach();

            world.Record(WorldEventType.Killed, Player, Victim, vanilla.Now, 1.0, Zone);

            Assert.True(vanilla.Karma < karmaBefore);
        }

        /// <summary>Observed theft is not double-punished either: Elin charges its own karma.</summary>
        [Fact]
        public void ObservedTheftDoesNotChargeKarmaOnTopOfElinsOwn()
        {
            NarrativeWorldState world = new NarrativeWorldState(123);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            world.Registry.Add(new NarrativeNpc(Victim, "Victim"));
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Victim, zone: Zone);
            int karmaBefore = vanilla.Karma;
            new ConsequenceEngine(world, vanilla).Attach();
            VanillaActionRecorder recorder = new VanillaActionRecorder(world, vanilla);

            recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Player,
                Victim,
                EntityId.Parse("item_1"),
                "silver ring",
                Zone,
                "ActStealing",
                new EntityId[0]));

            Assert.Equal(karmaBefore, vanilla.Karma);
        }
    }
}
