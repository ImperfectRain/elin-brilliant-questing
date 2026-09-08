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
    public class DevelopmentScoringTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Speaker = EntityId.Parse("npc_speaker");
        private static readonly EntityId Subject = EntityId.Parse("npc_subject");
        private static readonly EntityId Zone = EntityId.Parse("zone_market");

        [Fact]
        public void DirectorRanksEveryEligibleFactBeforeTakingASpeakersBestLine()
        {
            var (world, vanilla) = Town();
            NarrativeThread first = Matter(world);
            NarrativeThread urgent = Matter(world);
            urgent.Tension = 100;
            AmbientTalk talk = Ambient(world);
            Assert.Equal(urgent.FactIds[0], talk.Next(world, vanilla, vanilla.Now).FactId);
            string report = NarrativeInspector.DescribeAmbientTalk(world, vanilla);
            Assert.Contains(first.FactIds[0].Value, report);
            Assert.Contains(urgent.FactIds[0].Value, report);
            Assert.Contains("selected for delivery", report);
            Assert.Contains("not selected: lower score", report);
            Assert.Contains("tension=1.000", report);
            Assert.False(world.Knowledge.Knows(Player, urgent.FactIds[0]));
        }

        [Fact]
        public void AttentionRefusalOutranksEvenTheHighestDirectorScore()
        {
            var (world, vanilla) = Town();
            world.AttentionBudget.MaximumExposedThreads = 1;
            NarrativeThread known = Matter(world);
            Learn(world, known);
            EntityId update = Claim(world);
            known.FactIds.Add(update);
            NarrativeThread hidden = Matter(world);
            hidden.Tension = 100;
            Assert.Equal(update, Ambient(world).Next(world, vanilla, vanilla.Now).FactId);
            string report = NarrativeInspector.DescribeAmbientTalk(world, vanilla);
            Assert.Contains("exposed-thread budget", report);
            Assert.Contains(hidden.FactIds[0].Value, report);
        }

        [Fact]
        public void InspectionSelectionAndReloadPreserveStateAndStableTies()
        {
            var (world, vanilla) = Town();
            NarrativeThread first = Matter(world);
            Matter(world);
            string before = WorldStateSerializer.Save(world);
            string report = NarrativeInspector.DescribeAmbientTalk(world, vanilla);
            Assert.Equal(first.FactIds[0], Ambient(world).Next(world, vanilla, vanilla.Now).FactId);
            Assert.Equal(before, WorldStateSerializer.Save(world));
            world = WorldStateSerializer.Load(before);
            world.Threads.Reverse();
            Assert.Equal(report, NarrativeInspector.DescribeAmbientTalk(world, vanilla));
            Assert.Equal(first.FactIds[0], Ambient(world).Next(world, vanilla, vanilla.Now).FactId);
        }

        [Fact]
        public void UnknownLocationIsExplicitAndOnlyObservedProximityContributes()
        {
            var (world, vanilla) = Town();
            NarrativeThread matter = Matter(world);
            vanilla.Define(Subject, zone: EntityId.None);
            DevelopmentScore unknown = Read(world, vanilla, matter);
            Assert.Null(unknown.Proximity);
            Assert.Contains("proximity=unknown", unknown.Explain());
            vanilla.Define(Subject, zone: Zone);
            Assert.True(Read(world, vanilla, matter).Total > unknown.Total);
            vanilla.Define(Player, zone: EntityId.None);
            Assert.Null(Read(world, vanilla, matter).Proximity);
        }

        [Fact]
        public void KnownRecurringPeopleAndUnresolvedHistoryEarnBoundedBonuses()
        {
            var (world, vanilla) = Town();
            NarrativeThread old = Matter(world, "old");
            old.ParticipantIds.Add(Subject);
            NarrativeThread current = Matter(world, "new");
            DevelopmentScore hiddenHistory = Read(world, vanilla, current);
            Assert.Equal(0, hiddenHistory.Recurrence);
            Learn(world, old);
            Assert.True(Read(world, vanilla, current).Recurrence > 0);
            Learn(world, current);
            vanilla.Now = new GameTime(10080);
            Assert.True(Read(world, vanilla, current).UnresolvedHistory > 0);
            current.State = ThreadState.Resolved;
            Assert.Equal(0, Read(world, vanilla, current).UnresolvedHistory);
        }

        [Fact]
        public void DeclaredMechanicExposureAndArchetypeRepetitionReducePriority()
        {
            var (world, vanilla) = Town();
            NarrativeThread current = Matter(world);
            Assert.Null(Read(world, vanilla, current).UnderusedMechanics);
            current.RecoveryRoutes.Add(new RecoveryRoute("loss", "craft_supplies", "", "", ""));
            double fresh = Read(world, vanilla, current).UnderusedMechanics.Value;
            NarrativeThread previous = Matter(world);
            previous.RecoveryRoutes.Add(new RecoveryRoute("loss", "craft_supplies", "", "", ""));
            Assert.Equal(fresh, Read(world, vanilla, current).UnderusedMechanics);
            Learn(world, previous);
            DevelopmentScore repeated = Read(world, vanilla, current);
            Assert.True(repeated.UnderusedMechanics < fresh);
            Assert.True(repeated.Repetition > 0);
            Assert.Equal(0, repeated.RecentExposure);
        }

        [Fact]
        public void ObservedConsequencesEarnVisibilityWithoutTeachingHiddenFacts()
        {
            var (world, vanilla) = Town();
            NarrativeThread matter = Matter(world);
            world.Ledger.Append(new WorldEvent(world.NewId("event"), WorldEventType.Harmed,
                Subject, Speaker, GameTime.Zero, threadId: matter.Id));
            Assert.Equal(0, Read(world, vanilla, matter).ConsequenceVisibility);
            world.Ledger.Append(new WorldEvent(world.NewId("event"), WorldEventType.Helped,
                Subject, Speaker, GameTime.Zero, witnesses: new[] { Player }, threadId: matter.Id));
            Assert.True(Read(world, vanilla, matter).ConsequenceVisibility > 0);
            Assert.False(world.Knowledge.Knows(Player, matter.FactIds[0]));
        }

        [Fact]
        public void ActualDeliveryPenalizesUpdatesAndPenaltySurvivesReloadAndBackwardsTime()
        {
            var (world, vanilla) = Town();
            NarrativeThread matter = Matter(world);
            EntityId update = Claim(world);
            matter.FactIds.Add(update);
            vanilla.Now = new GameTime(100);
            AmbientTalk talk = Ambient(world);
            Assert.True(talk.Deliver(world, vanilla, talk.Next(world, vanilla, vanilla.Now), vanilla.Now));
            double penalty = Read(world, vanilla, matter).RecentExposure;
            Assert.True(penalty > 0);
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            matter = world.Threads.Single();
            vanilla.Now = new GameTime(50);
            Assert.Equal(penalty, Read(world, vanilla, matter).RecentExposure);
            vanilla.Now = new GameTime(1540);
            Assert.Equal(0, Read(world, vanilla, matter).RecentExposure);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OnlyPlayerEncounteredArrivalsCountAsExposure(bool present)
        {
            var (world, vanilla) = Town();
            NarrativeThread matter = Matter(world);
            string[] tags = present
                ? new[] { ConsequenceArrivals.ArrivalTag, ConsequenceArrivals.PlayerPresentTag }
                : new[] { ConsequenceArrivals.ArrivalTag };
            world.Ledger.Append(new WorldEvent(world.NewId("event"), WorldEventType.Conversed,
                Subject, Speaker, GameTime.Zero, tags: tags, threadId: matter.Id));
            Assert.Equal(present ? 1.0 : 0.0, Read(world, vanilla, matter).RecentExposure);
        }

        private static DevelopmentScore Read(NarrativeWorldState world, SandboxVanillaState vanilla, NarrativeThread matter) =>
            DevelopmentScoring.Read(world, vanilla, Speaker, matter.FactIds[0], 1.9, vanilla.Now);

        private static void Learn(NarrativeWorldState world, NarrativeThread matter) =>
            world.Knowledge.Teach(Player, matter.FactIds[0], KnowledgeSource.Hearsay, 0.7, GameTime.Zero, false, Speaker);

        private static (NarrativeWorldState, SandboxVanillaState) Town()
        {
            var world = new NarrativeWorldState(99);
            var vanilla = new SandboxVanillaState(Player);
            foreach (EntityId actor in new[] { Player, Speaker, Subject })
            {
                world.Registry.Add(new NarrativeNpc(actor, actor.Value));
                vanilla.Define(actor, zone: Zone);
            }
            return (world, vanilla);
        }

        private static NarrativeThread Matter(NarrativeWorldState world, string archetype = "debt")
        {
            var matter = new NarrativeThread(world.NewId("thread"), archetype, GameTime.Zero)
            { State = ThreadState.Active };
            matter.FactIds.Add(Claim(world));
            world.Threads.Add(matter);
            return matter;
        }

        private static EntityId Claim(NarrativeWorldState world)
        {
            EntityId id = world.NewId("fact");
            world.Knowledge.AddFact(new Fact(id, Subject, FactPredicates.Owes, Player, "80 orens"));
            world.Knowledge.Teach(Speaker, id, KnowledgeSource.Hearsay, 0.9, GameTime.Zero, false, Subject);
            return id;
        }

        private static AmbientTalk Ambient(NarrativeWorldState world) =>
            new AmbientTalk(new RumorSystem(world.Knowledge, world.Ledger, world.Ids));
    }
}
