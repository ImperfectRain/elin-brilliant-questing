using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-033: the journal is a projection of what the player knows, not what the simulation knows.
    /// </summary>
    public class NarrativeJournalTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");

        [Fact]
        public void DirectProvableKnowledgeIsListedAsKnown()
        {
            NarrativeWorldState world = World();
            Fact theft = AddFact(world, FactPredicates.Stole, Thief, Ring);
            world.Knowledge.Teach(Player, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, true);

            JournalEntry entry = Assert.Single(NarrativeJournal.Entries(world, Player));

            Assert.Equal(JournalTag.Known, entry.Tag);
            Assert.Equal("Haron stole item_ring", entry.Text);
            Assert.True(entry.CanProve);
        }

        [Fact]
        public void ReportedClaimsAreTaggedAfterThePlayerTakesThemToAuthority()
        {
            NarrativeWorldState world = World();
            Fact theft = AddFact(world, FactPredicates.Stole, Thief, Ring);
            world.Knowledge.Teach(Player, theft.Id, KnowledgeSource.Document, 0.9, GameTime.Zero, true);
            world.Record(WorldEventType.CrimeReported, Player, Guard, GameTime.Zero, related: new[] { theft.Id });

            JournalEntry entry = Assert.Single(NarrativeJournal.Entries(world, Player));

            Assert.Equal(JournalTag.Reported, entry.Tag);
        }

        [Fact]
        public void HearsayIsRumourAndInferenceIsSuspicion()
        {
            NarrativeWorldState world = World();
            Fact rumour = AddFact(world, FactPredicates.Owes, Victim, EntityId.None, "400 orens");
            Fact suspicion = AddFact(world, FactPredicates.Funds, Thief, Victim);
            world.Knowledge.Teach(Player, rumour.Id, KnowledgeSource.Hearsay, 0.6, GameTime.Zero, false, Victim);
            world.Knowledge.Teach(Player, suspicion.Id, KnowledgeSource.Inference, 0.8, GameTime.Zero.PlusMinutes(1), false);

            Dictionary<EntityId, JournalEntry> entries = NarrativeJournal.Entries(world, Player).ToDictionary(e => e.FactId);

            Assert.Equal(JournalTag.Rumour, entries[rumour.Id].Tag);
            Assert.Equal(JournalTag.Suspected, entries[suspicion.Id].Tag);
        }

        [Fact]
        public void ConflictingPlayerBeliefsAreDisputedWithoutNamingWhichOneIsTrue()
        {
            NarrativeWorldState world = World();
            Fact trueTheft = AddFact(world, FactPredicates.Stole, Thief, Ring, TruthState.True);
            Fact falseTheft = AddFact(world, FactPredicates.Stole, Victim, Ring, TruthState.False);
            falseTheft.DistortionOf = trueTheft.Id;

            world.Knowledge.Teach(Player, trueTheft.Id, KnowledgeSource.Witnessed, 0.8, GameTime.Zero, true);
            world.Knowledge.Teach(Player, falseTheft.Id, KnowledgeSource.Hearsay, 0.7, GameTime.Zero.PlusMinutes(1), false, Guard);

            string journal = NarrativeJournal.Describe(world, Player);
            IReadOnlyList<JournalEntry> entries = NarrativeJournal.Entries(world, Player);

            Assert.All(entries, entry => Assert.Equal(JournalTag.Disputed, entry.Tag));
            Assert.DoesNotContain("False", journal);
            Assert.DoesNotContain("True", journal);
            Assert.DoesNotContain("!", journal);
        }

        [Fact]
        public void HiddenFactsThePlayerHasNotLearnedAreNotReadableFromTheJournal()
        {
            NarrativeWorldState world = World();
            Fact publicTheft = AddFact(world, FactPredicates.Stole, Thief, Ring);
            AddFact(world, FactPredicates.Killed, Thief, Victim, TruthState.True);
            world.Knowledge.Teach(Player, publicTheft.Id, KnowledgeSource.Hearsay, 0.5, GameTime.Zero, false, Victim);

            string journal = NarrativeJournal.Describe(world, Player);

            Assert.Contains("Haron stole item_ring", journal);
            Assert.DoesNotContain("killed", journal);
            Assert.DoesNotContain("Mira", journal);
        }

        private static NarrativeWorldState World()
        {
            NarrativeWorldState world = new NarrativeWorldState(1);
            world.Registry.Add(new NarrativeNpc(Player, "You"));
            world.Registry.Add(new NarrativeNpc(Thief, "Haron"));
            world.Registry.Add(new NarrativeNpc(Victim, "Mira"));
            world.Registry.Add(new NarrativeNpc(Guard, "Rook"));
            return world;
        }

        private static Fact AddFact(
            NarrativeWorldState world,
            string predicate,
            EntityId subject,
            EntityId objectId,
            TruthState truth = TruthState.True)
        {
            return AddFact(world, predicate, subject, objectId, null, truth);
        }

        private static Fact AddFact(
            NarrativeWorldState world,
            string predicate,
            EntityId subject,
            EntityId objectId,
            string value,
            TruthState truth = TruthState.True)
        {
            Fact fact = new Fact(world.NewId("fact"), subject, predicate, objectId, value, truth);
            world.Knowledge.AddFact(fact);
            return fact;
        }
    }
}
