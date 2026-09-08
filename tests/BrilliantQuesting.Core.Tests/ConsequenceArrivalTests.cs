using System.Linq;
using BrilliantQuesting.Actions.Library;
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
    public class ConsequenceArrivalTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Debtor = EntityId.Parse("npc_debtor");
        private static readonly EntityId Creditor = EntityId.Parse("npc_creditor");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Market = EntityId.Parse("zone_market");
        private static readonly EntityId Road = EntityId.Parse("zone_road");
        private static readonly EntityId Home = EntityId.Parse("zone_home");

        [Fact]
        public void ConsequenceOfAnEarlierChoiceCanReachThePlayerWithoutThePlayerTravelling()
        {
            Lab lab = Lab.Create();
            EntityId playerStarted = lab.Vanilla.GetZoneOf(Player);

            ConsequenceArrivalResult result = lab.Arrivals.TryBringToPlayer(
                lab.Thread,
                Creditor,
                Player,
                lab.Now,
                "creditor_arrives",
                new[] { lab.DebtFactId });

            Assert.True(result.DidArrive, result.Reason);
            Assert.Equal(playerStarted, lab.Vanilla.GetZoneOf(Player));
            Assert.Equal(playerStarted, lab.Vanilla.GetZoneOf(Creditor));
            Assert.Equal(WorldEventType.ThreadEscalated, result.RecordedEvent.Type);
            Assert.Equal(lab.Thread.Id, result.RecordedEvent.ThreadId);
            Assert.Equal(Creditor, result.RecordedEvent.Actor);
            Assert.Equal(Player, result.RecordedEvent.Target);
            Assert.Equal(Road, result.RecordedEvent.Zone);
            Assert.Contains(lab.DebtFactId, result.RecordedEvent.Related);
            Assert.Contains(ConsequenceArrivals.ArrivalTag, result.RecordedEvent.Tags);
            Assert.Contains(ConsequenceArrivals.PlayerSurfaceTag, result.RecordedEvent.Tags);
            Assert.Contains("arrival_reason:creditor_arrives", result.RecordedEvent.Tags);
        }

        [Fact]
        public void ConsequenceCanReachHomeAsAHomeSurface()
        {
            Lab lab = Lab.Create();

            ConsequenceArrivalResult result = lab.Arrivals.TryBringToHome(
                lab.Thread,
                Guard,
                Debtor,
                lab.Now,
                "guard_follows_debt",
                new[] { lab.DebtFactId },
                WorldEventType.InquiryOpened,
                0.45);

            Assert.True(result.DidArrive, result.Reason);
            Assert.Equal(Road, lab.Vanilla.GetZoneOf(Player));
            Assert.Equal(Home, lab.Vanilla.GetZoneOf(Guard));
            Assert.Equal(WorldEventType.InquiryOpened, result.RecordedEvent.Type);
            Assert.Equal(Home, result.RecordedEvent.Zone);
            Assert.Contains(ConsequenceArrivals.HomeSurfaceTag, result.RecordedEvent.Tags);
        }

        [Fact]
        public void ReplayingTheSameArrivalDoesNotDuplicateHistory()
        {
            Lab lab = Lab.Create();

            ConsequenceArrivalResult first = lab.Arrivals.TryBringToPlayer(
                lab.Thread,
                Creditor,
                Player,
                lab.Now,
                "creditor_arrives",
                new[] { lab.DebtFactId });
            int eventCount = lab.World.Ledger.Count;

            ConsequenceArrivalResult second = lab.Arrivals.TryBringToPlayer(
                lab.Thread,
                Creditor,
                Player,
                lab.Now,
                "creditor_arrives",
                new[] { lab.DebtFactId });

            Assert.True(first.DidArrive, first.Reason);
            Assert.Equal(ConsequenceArrivalOutcome.AlreadyArrived, second.Outcome);
            Assert.Equal(eventCount, lab.World.Ledger.Count);
            Assert.Single(lab.World.Ledger.Events, e => e.Tags.Contains(ConsequenceArrivals.ArrivalTag));
        }

        [Fact]
        public void ArrivalRefusesWhenTheMoveCannotBeVerified()
        {
            Lab lab = Lab.Create();
            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, false);

            ConsequenceArrivalResult result = lab.Arrivals.TryBringToPlayer(
                lab.Thread,
                Creditor,
                Player,
                lab.Now,
                "creditor_arrives",
                new[] { lab.DebtFactId });

            Assert.Equal(ConsequenceArrivalOutcome.Refused, result.Outcome);
            Assert.Contains("relocation", result.Reason);
            Assert.Equal(Market, lab.Vanilla.GetZoneOf(Creditor));
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Tags.Contains(ConsequenceArrivals.ArrivalTag));
        }

        [Fact]
        public void HuntedWitnessLeakUsesTheArrivalSurface()
        {
            Lab lab = Lab.CreateHuntedWitness();
            ThreadEngine threads = new ThreadEngine();
            threads.Register(HuntedWitnessSituation.ArchetypeId, new HuntedWitnessEscalation(lab.Vanilla));

            lab.Vanilla.AdvanceDays(4);
            threads.Advance(lab.World, lab.Vanilla.Now);

            WorldEvent arrival = Assert.Single(lab.World.Ledger.Events, e =>
                e.Type == WorldEventType.InquiryOpened
                && e.Tags.Contains(ConsequenceArrivals.ArrivalTag));
            Assert.Equal(Home, lab.Vanilla.GetZoneOf(lab.GuardId));
            Assert.Equal(Home, arrival.Zone);
            Assert.Contains(ConsequenceArrivals.HomeSurfaceTag, arrival.Tags);
            Assert.Contains("arrival_reason:" + Undertakings.ResidentDiscoveredStep, arrival.Tags);
        }

        private sealed class Lab
        {
            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public NarrativeThread Thread { get; private set; }

            public EntityId DebtFactId { get; private set; }

            public EntityId GuardId { get; private set; }

            public GameTime Now => Vanilla.Now;

            public ConsequenceArrivals Arrivals => new ConsequenceArrivals(World, Vanilla);

            public static Lab Create()
            {
                Lab lab = Basic();
                WorldEvent origin = lab.World.Record(WorldEventType.DebtCreated, Debtor, Creditor, lab.Now, 0.5, Market);
                Fact debt = new Fact(
                    lab.World.NewId("fact"),
                    Debtor,
                    FactPredicates.Owes,
                    Creditor,
                    "750 orens",
                    TruthState.True,
                    originEvent: origin.Id);
                lab.World.Knowledge.AddFact(debt);
                lab.DebtFactId = debt.Id;

                NarrativeThread thread = new NarrativeThread(lab.World.NewId("thread"), "debt_default", lab.Now.PlusDays(-3))
                {
                    OriginEventId = origin.Id,
                    State = ThreadState.Dormant,
                    Tension = 30,
                    Importance = 35
                };
                thread.ParticipantIds.Add(Debtor);
                thread.ParticipantIds.Add(Creditor);
                thread.FactIds.Add(debt.Id);
                thread.SiteIds.Add(Market);
                lab.World.Threads.Add(thread);
                lab.Thread = thread;
                return lab;
            }

            public static Lab CreateHuntedWitness()
            {
                Lab lab = Basic();
                HuntedWitnessSituation situation = HuntedWitnessSituation.Create(
                    lab.World,
                    new SandboxStager(lab.Vanilla),
                    Player,
                    Road,
                    lab.Now);
                lab.Vanilla.SetHome(HuntedWitnessSituation.Smallholding(Home, EntityId.Parse("npc_resident"))
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 2)
                    .Build());
                lab.Vanilla.TryAdmitResident(situation.WitnessId);

                Fact undertaking = new Fact(
                    lab.World.NewId("fact"),
                    situation.WitnessId,
                    FactPredicates.ShelteredBy,
                    Player,
                    Undertakings.Resident,
                    TruthState.True);
                lab.World.Knowledge.AddFact(undertaking);
                lab.World.Knowledge.Teach(Player, undertaking.Id, KnowledgeSource.Participant, 1.0, lab.Now, true);
                lab.World.Knowledge.Teach(situation.WitnessId, undertaking.Id, KnowledgeSource.Participant, 1.0, lab.Now, true);
                lab.World.Knowledge.GetFact(situation.ExposureFactId).Truth = TruthState.Superseded;
                situation.Thread.Escalation.Add(new EscalationStep(
                    Undertakings.ResidentDiscoveredStep,
                    4,
                    "Word reaches the person looking for them."));

                lab.Thread = situation.Thread;
                lab.GuardId = situation.GuardId;
                return lab;
            }

            private static Lab Basic()
            {
                Lab lab = new Lab
                {
                    World = new NarrativeWorldState(98098),
                    Vanilla = new SandboxVanillaState(Player)
                };
                lab.World.Registry.Add(new NarrativeNpc(Player, "You") { Importance = NarrativeImportance.Major });
                lab.World.Registry.Add(new NarrativeNpc(Debtor, "Mira") { Importance = NarrativeImportance.Known });
                lab.World.Registry.Add(new NarrativeNpc(Creditor, "Haron") { Importance = NarrativeImportance.Known });
                lab.World.Registry.Add(new NarrativeNpc(Guard, "Ovel") { Importance = NarrativeImportance.Known });
                lab.World.Registry.Add(new NarrativeSite(Market, "market", "market"));
                lab.World.Registry.Add(new NarrativeSite(Road, "road", "road"));
                lab.World.Registry.Add(new NarrativeSite(Home, "home", "home"));
                lab.Vanilla.Define(Player, level: 9, zone: Road);
                lab.Vanilla.Define(Debtor, level: 4, zone: Market);
                lab.Vanilla.Define(Creditor, level: 6, zone: Market);
                lab.Vanilla.Define(Guard, level: 8, zone: Market);
                lab.Vanilla.SetHome(new HomeStateBuilder(Home, "home")
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 20)
                    .Build());
                return lab;
            }
        }
    }
}
