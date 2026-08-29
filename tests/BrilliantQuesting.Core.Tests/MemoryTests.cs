using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Memory;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class MemoryTests
    {
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shop");
        private static readonly EntityId Player = EntityId.Parse("npc_player");

        [Fact]
        public void RoutineRepetitionConsolidatesIntoOneMemory()
        {
            MemoryLedger ledger = new MemoryLedger();
            for (int i = 0; i < 20; i++)
            {
                ledger.Add(new MemoryRecord(EntityId.Parse("mem_" + i), Shopkeeper, Player, WorldEventType.Conversed, MemoryWeight.Routine, GameTime.FromDays(i), 1, "spoke_with"));
            }

            Assert.Single(ledger.MemoriesOf(Shopkeeper));
            Assert.Equal(20, ledger.MemoriesOf(Shopkeeper)[0].Occurrences);
            Assert.Equal(20, ledger.AccountedAffinity(Shopkeeper, Player));
        }

        [Fact]
        public void DefiningMemoriesAreNeverFoldedTogether()
        {
            MemoryLedger ledger = new MemoryLedger();
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_a"), Shopkeeper, Player, WorldEventType.Killed, MemoryWeight.Defining, GameTime.Zero, -80, "killed_someone"));
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_b"), Shopkeeper, Player, WorldEventType.Killed, MemoryWeight.Defining, GameTime.FromDays(9), -80, "killed_someone"));

            Assert.Equal(2, ledger.MemoriesOf(Shopkeeper).Count);
        }

        [Fact]
        public void ForgettingTouchesTriviaOnly()
        {
            MemoryLedger ledger = new MemoryLedger();
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_a"), Shopkeeper, Player, WorldEventType.Met, MemoryWeight.Trivial, GameTime.Zero, 0, "met"));
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_b"), Shopkeeper, Player, WorldEventType.Rescued, MemoryWeight.Defining, GameTime.Zero, 35, "was_rescued"));

            int removed = ledger.Forget(GameTime.FromDays(400), olderThanDays: 90);

            Assert.Equal(1, removed);
            Assert.Single(ledger.MemoriesOf(Shopkeeper));
            Assert.Equal(MemoryWeight.Defining, ledger.MemoriesOf(Shopkeeper)[0].Weight);
        }

        [Fact]
        public void StrongestReturnsTheMemoriesDialogueShouldLeadWith()
        {
            MemoryLedger ledger = new MemoryLedger();
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_a"), Shopkeeper, Player, WorldEventType.Conversed, MemoryWeight.Routine, GameTime.Zero, 1, "spoke_with"));
            ledger.Add(new MemoryRecord(EntityId.Parse("mem_b"), Shopkeeper, Player, WorldEventType.Rescued, MemoryWeight.Defining, GameTime.FromDays(1), 35, "was_rescued"));

            MemoryRecord top = Assert.Single(ledger.Strongest(Shopkeeper, Player, 1));
            Assert.Equal("was_rescued", top.SummaryTag);
        }

        [Fact]
        public void SyntheticTwoHundredHourLedgerStaysWithinMemoryBudget()
        {
            MemoryLedger ledger = new MemoryLedger();
            EntityId[] townspeople = new EntityId[40];
            for (int i = 0; i < townspeople.Length; i++)
            {
                townspeople[i] = EntityId.Parse("npc_town_" + i);
            }

            for (int hour = 0; hour < 200; hour++)
            {
                for (int i = 0; i < townspeople.Length; i++)
                {
                    EntityId owner = townspeople[i];
                    ledger.Add(new MemoryRecord(
                        EntityId.Parse("mem_routine_" + hour + "_" + i),
                        owner,
                        Player,
                        WorldEventType.Conversed,
                        MemoryWeight.Routine,
                        GameTime.FromDays(hour / 24),
                        1,
                        "spoke_with"));

                    ledger.Add(new MemoryRecord(
                        EntityId.Parse("mem_trivia_" + hour + "_" + i),
                        owner,
                        Player,
                        WorldEventType.Met,
                        MemoryWeight.Trivial,
                        GameTime.FromDays(hour / 24),
                        0,
                        "saw_player_" + hour));
                }
            }

            MemoryCompactionReport report = ledger.Compact(
                GameTime.FromDays(200),
                new MemoryCompactionPolicy
                {
                    MaxMemoriesPerOwner = 16,
                    TrivialRetentionDays = 7,
                    RoutineRetentionDays = 90,
                    ReinforcedRoutineOccurrenceFloor = 3
                });

            Assert.True(ledger.Count <= 640, "kept " + ledger.Count + " memories after compaction");
            Assert.True(report.Removed > 7000);
            Assert.Equal(0, report.OwnersOverBudget);
            foreach (EntityId owner in townspeople)
            {
                Assert.Contains(ledger.MemoriesOf(owner), m => m.SummaryTag == "spoke_with" && m.Occurrences == 200);
            }
        }

        [Fact]
        public void CompactionNeverDeletesDefiningMemories()
        {
            MemoryLedger ledger = new MemoryLedger();
            for (int i = 0; i < 20; i++)
            {
                ledger.Add(new MemoryRecord(
                    EntityId.Parse("mem_defining_" + i),
                    Shopkeeper,
                    Player,
                    WorldEventType.Killed,
                    MemoryWeight.Defining,
                    GameTime.FromDays(i),
                    -80,
                    "killed_someone_" + i));
            }

            MemoryCompactionReport report = ledger.Compact(
                GameTime.FromDays(200),
                new MemoryCompactionPolicy { MaxMemoriesPerOwner = 5 });

            Assert.Equal(20, ledger.MemoriesOf(Shopkeeper).Count);
            Assert.Equal(1, report.OwnersOverBudget);
        }
    }
}
