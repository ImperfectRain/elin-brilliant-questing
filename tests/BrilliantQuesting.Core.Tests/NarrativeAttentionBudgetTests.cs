using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class NarrativeAttentionBudgetTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Speaker = EntityId.Parse("npc_speaker");
        private static readonly EntityId Debtor = EntityId.Parse("npc_debtor");
        private static readonly EntityId Zone = EntityId.Parse("zone_market");

        [Fact]
        public void FullExposureBudgetStaysQuietButAskingStillFindsTheMatter()
        {
            var (world, vanilla) = Town();
            for (int i = 0; i < world.AttentionBudget.MaximumExposedThreads; i++) Matter(world, known: true);
            NarrativeThread hidden = Matter(world);
            AmbientTalk ambient = Ambient(world);
            int events = world.Ledger.Count;
            for (int minute = 0; minute <= 60; minute++)
            {
                vanilla.Now = new GameTime(minute);
                Assert.Null(ambient.Next(world, vanilla, vanilla.Now));
            }
            Assert.Equal(events, world.Ledger.Count);
            Assert.False(world.Knowledge.Knows(Player, hidden.FactIds[0]));
            Assert.DoesNotContain(NarrativeJournal.Entries(world, Player), e => e.FactId == hidden.FactIds[0]);
            Assert.Contains("exposed-thread budget", NarrativeInspector.DescribeAmbientTalk(world, vanilla));

            TownNews news = new TownNews(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            SpokenRemark requested = Assert.Single(news.Ask(world, vanilla, Speaker));
            Assert.True(news.Deliver(world, vanilla, requested, vanilla.Now));
            Assert.Contains(NarrativeJournal.Entries(world, Player), e => e.FactId == hidden.FactIds[0] && !e.CanProve);
            Assert.Equal(NarrativeWorldState.NothingSaidYet, world.LastAmbientRemarkMinute);
        }

        [Fact]
        public void SelectionDoesNotSpendAttentionAndAnActualRemarkProtectsTheNextHourAcrossReload()
        {
            var (world, vanilla) = Town();
            Matter(world);
            Matter(world);
            AmbientTalk ambient = Ambient(world);
            SpokenRemark first = ambient.Next(world, vanilla, vanilla.Now);
            Assert.NotNull(first);
            Assert.Equal(first.FactId, ambient.Next(world, vanilla, vanilla.Now).FactId);
            Assert.Equal(0, world.AttentionBudget.Read(world, Player, vanilla.Now, 90).ExposedThreadCount);
            Assert.True(ambient.Deliver(world, vanilla, first, vanilla.Now));

            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            ambient = Ambient(world);
            int events = world.Ledger.Count;
            for (int minute = 0; minute <= 60; minute++)
                Assert.Null(ambient.Next(world, vanilla, new GameTime(minute)));
            Assert.Equal(events, world.Ledger.Count);
            Assert.NotNull(ambient.Next(world, vanilla, new GameTime(90)));
        }

        [Fact]
        public void DeliveredIntroductionsFillTheBudgetAndReloadDoesNotGrantMoreSlots()
        {
            var (world, vanilla) = Town();
            int limit = world.AttentionBudget.MaximumExposedThreads;
            for (int i = 0; i <= limit; i++) Matter(world);
            AmbientTalk ambient = Ambient(world);
            for (int i = 0; i < limit; i++)
            {
                vanilla.Now = new GameTime(i * ambient.MinutesBetweenRemarks);
                SpokenRemark remark = ambient.Next(world, vanilla, vanilla.Now);
                Assert.NotNull(remark);
                Assert.True(ambient.Deliver(world, vanilla, remark, vanilla.Now));
            }
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            vanilla.Now = vanilla.Now.PlusDays(1);
            Assert.Null(Ambient(world).Next(world, vanilla, vanilla.Now));
            Assert.Equal(limit, world.AttentionBudget.Read(world, Player, vanilla.Now, 90).ExposedThreadCount);
            Assert.Single(new TownNews(new RumorSystem(world.Knowledge, world.Ledger, world.Ids)).Ask(world, vanilla, Speaker));
        }

        [Fact]
        public void BackwardsClockDoesNotResetCooldown()
        {
            var (world, vanilla) = Town();
            Matter(world);
            world.LastAmbientRemarkMinute = 100;
            Assert.Null(Ambient(world).Next(world, vanilla, new GameTime(50)));
            Assert.NotNull(Ambient(world).Next(world, vanilla, new GameTime(190)));
            Assert.Equal(100, world.LastAmbientRemarkMinute);
        }

        [Fact]
        public void ABlockedHighScoringIntroductionDoesNotStarveAnUpdateToAKnownMatter()
        {
            var (world, vanilla) = Town();
            world.AttentionBudget.MaximumExposedThreads = 1;
            NarrativeThread known = Matter(world, known: true);
            NarrativeThread hidden = Matter(world);
            EntityId update = Claim(world, confidence: 0.6);
            known.FactIds.Add(update);
            SpokenRemark remark = Ambient(world).Next(world, vanilla, vanilla.Now);
            Assert.NotNull(remark);
            Assert.Equal(update, remark.FactId);
            Assert.False(world.Knowledge.Knows(Player, hidden.FactIds[0]));
        }

        [Theory]
        [InlineData(ThreadState.Resolved)]
        [InlineData(ThreadState.Dormant)]
        [InlineData(ThreadState.Inherited)]
        [InlineData(ThreadState.Quarantined)]
        public void ClosingALiveMatterReleasesAttentionWithoutRemovingKnowledge(ThreadState state)
        {
            var (world, vanilla) = Town();
            world.AttentionBudget.MaximumExposedThreads = 1;
            NarrativeThread known = Matter(world, known: true);
            NarrativeThread hidden = Matter(world);
            Assert.Null(Ambient(world).Next(world, vanilla, vanilla.Now));
            known.State = state;
            Assert.Equal(hidden.FactIds[0], Ambient(world).Next(world, vanilla, vanilla.Now).FactId);
            Assert.True(world.Knowledge.Knows(Player, known.FactIds[0]));
        }

        [Fact]
        public void ASharedFactMustFitAllTheThreadsItWouldIntroduce()
        {
            var (world, vanilla) = Town();
            world.AttentionBudget.MaximumExposedThreads = 1;
            NarrativeThread first = Matter(world);
            NarrativeThread second = new NarrativeThread(world.NewId("thread"), "another_cause", vanilla.Now);
            second.FactIds.Add(first.FactIds[0]);
            world.Threads.Add(second);
            Assert.Null(Ambient(world).Next(world, vanilla, vanilla.Now));
            world.AttentionBudget.MaximumExposedThreads = 2;
            Assert.NotNull(Ambient(world).Next(world, vanilla, vanilla.Now));
        }

        [Fact]
        public void LowSalienceGossipRemainsAvailableOnRequest()
        {
            var (world, vanilla) = Town();
            EntityId quiet = Claim(world, confidence: 0.4);
            Assert.Null(Ambient(world).Next(world, vanilla, vanilla.Now));
            Assert.Contains("salience threshold", NarrativeInspector.DescribeAmbientTalk(world, vanilla));
            TownNews news = new TownNews(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
            Assert.Equal(quiet, Assert.Single(news.Ask(world, vanilla, Speaker)).FactId);
        }

        [Fact]
        public void EarnedArrivalSpendsAttentionWithoutTeachingItsHiddenFacts()
        {
            var (world, vanilla) = Town();
            NarrativeThread arriving = Matter(world);
            Matter(world);
            ConsequenceArrivalResult result = new ConsequenceArrivals(world, vanilla).TryBringToPlayer(
                arriving, Debtor, Player, vanilla.Now, "debt_followup", arriving.FactIds);
            Assert.True(result.DidArrive, result.Reason);
            Assert.Null(Ambient(world).Next(world, vanilla, vanilla.Now));
            Assert.False(world.Knowledge.Knows(Player, arriving.FactIds[0]));
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Assert.True(world.AttentionBudget.Read(world, Player, new GameTime(60), 90).CoolingDown);
            Assert.Equal(1, world.AttentionBudget.Read(world, Player, new GameTime(90), 90).ExposedThreadCount);
            Assert.NotNull(Ambient(world).Next(world, vanilla, new GameTime(90)));
        }

        [Fact]
        public void OffScreenHomeArrivalDoesNotInventPlayerAwareness()
        {
            var (world, vanilla) = Town();
            NarrativeThread thread = Matter(world);
            EntityId home = EntityId.Parse("zone_home");
            vanilla.SetHome(new HomeStateBuilder(home, "Home").Build());
            Assert.True(new ConsequenceArrivals(world, vanilla).TryBringToHome(
                thread, Debtor, Player, vanilla.Now, "debt_followup").DidArrive);
            AttentionSnapshot attention = world.AttentionBudget.Read(world, Player, vanilla.Now, 90);
            Assert.False(attention.CoolingDown);
            Assert.Equal(0, attention.ExposedThreadCount);
            vanilla.Define(Player, zone: home);
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            attention = world.AttentionBudget.Read(world, Player, vanilla.Now, 90);
            Assert.False(attention.CoolingDown);
            Assert.Equal(0, attention.ExposedThreadCount);
        }

        [Fact]
        public void HomeArrivalWithPlayerPresentSpendsAttentionEvenAfterTheyLeaveAndReload()
        {
            var (world, vanilla) = Town();
            NarrativeThread thread = Matter(world);
            vanilla.SetHome(new HomeStateBuilder(Zone, "Home").Build());
            ConsequenceArrivalResult arrival = new ConsequenceArrivals(world, vanilla).TryBringToHome(
                thread, Debtor, Player, vanilla.Now, "debt_followup");
            Assert.True(arrival.DidArrive, arrival.Reason);
            Assert.Contains(ConsequenceArrivals.PlayerPresentTag, arrival.RecordedEvent.Tags);
            vanilla.Define(Player, zone: EntityId.Parse("zone_road"));
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            AttentionSnapshot attention = world.AttentionBudget.Read(world, Player, vanilla.Now, 90);
            Assert.True(attention.CoolingDown);
            Assert.Equal(1, attention.ExposedThreadCount);
            Assert.False(world.Knowledge.Knows(Player, thread.FactIds[0]));
        }

        private static (NarrativeWorldState, SandboxVanillaState) Town()
        {
            NarrativeWorldState world = new NarrativeWorldState(99);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            foreach (EntityId actor in new[] { Player, Speaker, Debtor })
            {
                world.Registry.Add(new NarrativeNpc(actor, actor.Value));
                vanilla.Define(actor, zone: Zone);
            }
            return (world, vanilla);
        }

        private static NarrativeThread Matter(NarrativeWorldState world, bool known = false)
        {
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "debt", GameTime.Zero)
            { State = ThreadState.Active, Tension = 30 };
            EntityId fact = Claim(world);
            thread.FactIds.Add(fact);
            if (known) world.Knowledge.Teach(Player, fact, KnowledgeSource.Hearsay, 0.6, GameTime.Zero, false, Speaker);
            world.Threads.Add(thread);
            return thread;
        }

        private static EntityId Claim(NarrativeWorldState world, double confidence = 0.9)
        {
            EntityId id = world.NewId("fact");
            world.Knowledge.AddFact(new Fact(id, Debtor, FactPredicates.Owes, Player, "80 orens"));
            world.Knowledge.Teach(Speaker, id, KnowledgeSource.Hearsay, confidence, GameTime.Zero, false, Debtor);
            return id;
        }

        private static AmbientTalk Ambient(NarrativeWorldState world) =>
            new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
    }
}
