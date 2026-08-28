using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using System.Collections.Generic;
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
        public void RestoringAStaleCounterCannotMoveTheMinterBackward()
        {
            IdMinter minter = new IdMinter();
            EntityId first = minter.Next("npc");
            EntityId second = minter.Next("npc");

            minter.Restore("npc", 1);

            EntityId next = minter.Next("npc");
            Assert.NotEqual(first, next);
            Assert.NotEqual(second, next);
            Assert.Equal(EntityId.Mint("npc", 3), next);
        }

        [Fact]
        public void WorldRoundTripDoesNotReissueAnyExistingIds()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            HashSet<EntityId> issued = new HashSet<EntityId>();

            for (int i = 0; i < 100; i++)
            {
                issued.Add(world.NewId("npc"));
                issued.Add(world.NewId("evt"));
                issued.Add(world.NewId("fact"));
            }

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));

            for (int i = 0; i < 100; i++)
            {
                Assert.DoesNotContain(reloaded.NewId("npc"), issued);
                Assert.DoesNotContain(reloaded.NewId("evt"), issued);
                Assert.DoesNotContain(reloaded.NewId("fact"), issued);
            }
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
        public void ForkedStreamsDoNotDependOnParentDrawOrder()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                DeterministicRng parentBeforeDraws = new DeterministicRng(seed);
                DeterministicRng forkBeforeDraws = parentBeforeDraws.Fork("thread/escalation");

                DeterministicRng parentAfterDraws = new DeterministicRng(seed);
                for (int i = 0; i < 25; i++)
                {
                    parentAfterDraws.NextUInt64();
                }

                DeterministicRng forkAfterDraws = parentAfterDraws.Fork("thread/escalation");
                for (int i = 0; i < 25; i++)
                {
                    Assert.Equal(forkBeforeDraws.NextUInt64(), forkAfterDraws.NextUInt64());
                }
            }
        }

        [Fact]
        public void RngStateRoundTripContinuesTheSameSequence()
        {
            NarrativeWorldState world = new NarrativeWorldState(8675309);
            for (int i = 0; i < 37; i++)
            {
                world.Rng.NextUInt64();
            }

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(world.Rng.NextUInt64(), reloaded.Rng.NextUInt64());
            }
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

        [Fact]
        public void DayArithmeticSurvivesLongSaves()
        {
            long[] dayCounts = { 0, 1, 7, 365, 3650, 36500 };
            int[] hours = { 0, 1, 6, 12, 23 };
            int[] minutes = { 0, 1, 17, 30, 59 };

            foreach (long days in dayCounts)
            {
                foreach (int hour in hours)
                {
                    foreach (int minute in minutes)
                    {
                        GameTime time = GameTime.FromDays(days).PlusHours(hour).PlusMinutes(minute);
                        Assert.Equal(days, time.TotalDays);
                        Assert.Equal(hour, time.Hour);
                        Assert.Equal(minute, time.Minute);
                        Assert.Equal(days, time.DaysSince(GameTime.Zero));
                        Assert.Equal(time, GameTime.Zero.PlusMinutes(time.TotalMinutes));
                    }
                }
            }
        }
    }
}
