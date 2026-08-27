using BrilliantQuesting.Foundation;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class FoundationTests
    {
        [Fact]
        public void MintedIdsCarryTheirKindAndAreStable()
        {
            IdMinter minter = new IdMinter();
            EntityId first = minter.Next("npc");
            EntityId second = minter.Next("npc");

            Assert.Equal("npc", first.Kind);
            Assert.NotEqual(first, second);
            Assert.Equal(first, EntityId.Parse(first.Value));
        }

        [Fact]
        public void RestoredCountersDoNotReissueIds()
        {
            IdMinter original = new IdMinter();
            EntityId a = original.Next("npc");
            EntityId b = original.Next("npc");

            IdMinter reloaded = new IdMinter();
            reloaded.Restore("npc", 2);

            EntityId next = reloaded.Next("npc");
            Assert.NotEqual(a, next);
            Assert.NotEqual(b, next);
        }

        [Fact]
        public void SameSeedReplaysTheSameSequence()
        {
            DeterministicRng first = new DeterministicRng(99);
            DeterministicRng second = new DeterministicRng(99);

            for (int i = 0; i < 50; i++)
            {
                Assert.Equal(first.Roll(20), second.Roll(20));
            }
        }

        [Fact]
        public void ForkedStreamsAreIndependentButReproducible()
        {
            DeterministicRng parent = new DeterministicRng(7);
            DeterministicRng a = parent.Fork("situations");
            DeterministicRng b = parent.Fork("situations");
            DeterministicRng c = parent.Fork("dialogue");

            Assert.Equal(a.NextUInt64(), b.NextUInt64());
            Assert.NotEqual(a.Seed, c.Seed);
        }

        [Fact]
        public void TimeCountsDaysNotTicks()
        {
            GameTime start = GameTime.Zero;
            GameTime later = start.PlusDays(3).PlusHours(5);

            Assert.Equal(3, later.TotalDays);
            Assert.Equal(5, later.Hour);
            Assert.Equal(3, later.DaysSince(start));
        }
    }
}
