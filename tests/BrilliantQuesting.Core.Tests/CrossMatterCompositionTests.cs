using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class CrossMatterCompositionTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Owner = EntityId.Parse("npc_owner");
        private static readonly EntityId Outsider = EntityId.Parse("npc_outsider");
        private static readonly EntityId Place = EntityId.Parse("zone_workshop");
        private static readonly EntityId Tool = EntityId.Parse("item_tool");
        private static readonly EntityId Shop = EntityId.Parse("business_workshop");

        [Fact]
        public void OneRepairChangesTwoIndependentMattersWithoutDuplicatingTheDeed()
        {
            var single = new Fixture(false);
            var shared = new Fixture(true);
            int serviceTension = shared.Service.Tension;
            int repairTension = shared.Repair.Tension;
            int events = shared.World.Ledger.Count;
            Assert.True(shared.CanRepair(shared.Repair));
            Assert.True(shared.CanRepair(shared.Service));
            Assert.NotEqual(shared.Repair.OriginEventId, shared.Service.OriginEventId);
            Assert.NotEqual(shared.Repair.Establishment.Id, shared.Service.Establishment.Id);
            Assert.Equal(shared.Repair.FactIds, shared.Service.FactIds);

            single.Mend();
            shared.Mend();

            Assert.Equal(TruthState.Superseded, shared.Damage.Truth);
            Assert.False(shared.CanRepair(shared.Service)); // Shared gameplay state, not just a cast ID.
            Assert.Equal(ThreadState.Resolved, shared.Repair.State);
            Assert.Equal(ThreadState.Active, shared.Service.State);
            Assert.Null(shared.Service.Resolution);
            Assert.Equal(serviceTension + 7, shared.Service.Tension);
            Assert.Equal(repairTension + 7, shared.Repair.Tension); // Named directly AND by fact, once.
            Assert.Equal(events + 2, shared.World.Ledger.Count); // Helped, then only its own ending.
            Assert.Single(shared.World.Ledger.Events, e => e.Type == WorldEventType.Helped);
            Assert.Single(shared.World.Ledger.Events, e => e.Type == WorldEventType.ThreadResolved);
            Assert.Single(shared.Native.GetInventory(Player)); // One of two parts spent.
            Assert.DoesNotContain(DevelopmentDetector.Detect(shared.World),
                d => d.HasPressure(DevelopmentPressures.DamagedProperty));
            Assert.Contains(DevelopmentDetector.Detect(shared.World),
                d => d.HasPressure(DevelopmentPressures.ServiceInterruption) && d.ThreadId == shared.Service.Id);
            Assert.False(shared.World.Knowledge.Knows(Outsider, shared.Damage.Id));

            // The same deed with a second matter has exactly the same global effects as with one.
            foreach (string field in new[] { "facts", "beliefs", "memories", "relationships", "obligations" })
            {
                var expected = WorldStateSerializer.ToJson(single.World)[field];
                Assert.NotNull(expected);
                Assert.Equal(WithActionReferences(single.World, expected.ToJson()),
                    WithActionReferences(shared.World, WorldStateSerializer.ToJson(shared.World)[field].ToJson()));
            }
            Assert.Equal(single.Native.Karma, shared.Native.Karma);
            Assert.Equal(single.Native.Fame, shared.Native.Fame);
            Assert.Equal(single.Native.GetAffinity(Owner), shared.Native.GetAffinity(Owner));
            Assert.Equal(single.Native.GetMoney(Player), shared.Native.GetMoney(Player));
            Assert.All(shared.World.Registry.Npcs.Values, npc => Assert.Empty(npc.Goals));
            Assert.Equal(EntityId.None, shared.Service.ParentThreadId);
            Assert.Equal(EntityId.None, shared.Repair.SuccessorThreadId);
        }

        [Fact]
        public void ANewMatterAfterResolutionRetainsTheCauseAndTheSharedActorAcrossReload()
        {
            var fixture = new Fixture(true);
            fixture.Mend();
            string saved = WorldStateSerializer.Save(fixture.World);
            var world = WorldStateSerializer.Load(saved);
            new ConsequenceEngine(world, fixture.Native).Attach();
            Assert.Equal(saved, WorldStateSerializer.Save(world)); // No redispatch or rewards on load.
            var ended = world.GetThread(fixture.Repair.Id);
            var continuing = world.GetThread(fixture.Service.Id);
            string endedBefore = NarrativeInspector.DescribeThread(world, ended);
            int oldHistoryCount = world.Ledger.Count;
            var oldHistory = world.Ledger.Events.Select(e => e.Id).ToArray();
            fixture.Native.AdvanceDays(1);
            var continuity = new BusinessContinuity(world);
            var laterShop = EntityId.Parse("business_later");
            Assert.True(continuity.TryRegister(laterShop, Place, Owner, fixture.Native.Now));
            // A second business reports continuing disruption from the already recorded damage.
            Assert.True(continuity.TryChangeState(laterShop, BusinessContinuityState.TemporarilyClosed,
                fixture.Native.Now, fixture.Damage.Id));
            NarrativeThread later = Establish(world, new ServiceContinuityProducer(), fixture.Native.Now);
            Assert.Contains(Owner, later.ParticipantIds); // Closing the repair did not reserve its actor.
            Assert.Equal(ThreadState.Resolved, ended.State);
            Assert.True(continuing.IsLive);
            Assert.True(later.IsLive);
            Assert.Equal(3, world.Threads.Count);
            Assert.Equal(oldHistory, world.Ledger.Events.Take(oldHistoryCount).Select(e => e.Id));
            Assert.All(world.Threads, t =>
            {
                Assert.Equal(EntityId.None, t.ParentThreadId);
                Assert.Equal(EntityId.None, t.SuccessorThreadId);
                Assert.Contains(CausalHistory.Read(world, world.Ledger.Find(t.OriginEventId)).About,
                    c => c.Occurrence?.Id == fixture.Damage.OriginEvent);
                string description = NarrativeInspector.DescribeThread(world, t);
                Assert.Contains(t.Id.Value, description);
                Assert.Contains(fixture.Damage.Id.Value, description);
                Assert.Contains("shared with", description);
            });
            Assert.Contains("resolution: cause_removed", endedBefore);
            Assert.Null(new SituationProposalEcology(new[] { new ServiceContinuityProducer() }).Read(world).Select());
            string committed = WorldStateSerializer.Save(world);
            Assert.Equal(committed, WorldStateSerializer.Save(WorldStateSerializer.Load(committed)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IndependentSelectionsShareFactsInEitherOrderAndStaleDuplicatesAllocateNothing(bool reverse)
        {
            var fixture = new Fixture(null);
            var damage = new SituationProposalEcology(new[] { new DamagedPropertyProducer() }).Read(fixture.World).Select();
            var service = new SituationProposalEcology(new[] { new ServiceContinuityProducer() }).Read(fixture.World).Select();
            Assert.NotNull(damage);
            Assert.NotNull(service);
            var first = reverse ? service : damage;
            var second = reverse ? damage : service;
            Assert.True(first.Fulfill(GameTime.Zero).Established);
            // The second proposal was selected before the other matter established its shared fact.
            var result = second.Fulfill(GameTime.Zero);
            Assert.True(result.Established, result.Refusal);
            Assert.Equal(2, fixture.World.Threads.Count);
            foreach (var condition in DevelopmentDetector.Detect(fixture.World))
            {
                var owner = fixture.World.Threads.SingleOrDefault(t => t.Establishment.CauseId == condition.Id);
                if (owner != null) Assert.Equal(owner.Id, condition.ThreadId);
            }
            string committed = WorldStateSerializer.Save(fixture.World);
            Assert.Same(result.Thread, second.Fulfill(GameTime.Zero).Thread);
            Assert.Equal(committed, WorldStateSerializer.Save(fixture.World));
        }

        [Fact]
        public void ServiceWithoutHistoricalCauseIsRefusedWithoutAllocation()
        {
            var fixture = new Fixture(null);
            new BusinessContinuity(fixture.World).TryChangeState(Shop, BusinessContinuityState.Failed, GameTime.Zero);
            var selected = new SituationProposalEcology(new[] { new ServiceContinuityProducer() }).Read(fixture.World).Select();
            string before = WorldStateSerializer.Save(fixture.World);
            Assert.Contains("no recorded cause", selected.Fulfill(GameTime.Zero).Refusal);
            Assert.Equal(before, WorldStateSerializer.Save(fixture.World));
        }

        [Fact]
        public void AnOldSharedCauseDoesNotPermitBackdatingANewBusinessCondition()
        {
            var fixture = new Fixture(null);
            new BusinessContinuity(fixture.World).TryChangeState(Shop, BusinessContinuityState.Failed,
                GameTime.FromDays(2), fixture.Damage.Id);
            var selected = new SituationProposalEcology(new[] { new ServiceContinuityProducer() }).Read(fixture.World).Select();
            string before = WorldStateSerializer.Save(fixture.World);
            Assert.Contains("did not exist", selected.Fulfill(GameTime.Zero).Refusal);
            Assert.Equal(before, WorldStateSerializer.Save(fixture.World));
            Assert.True(selected.Fulfill(GameTime.FromDays(2)).Established);
        }

        [Fact]
        public void SettlingABusinessCannotSettleItsSharedCrimeThroughTheFirstCarrier()
        {
            var fixture = new Fixture(null);
            var incident = fixture.World.Record(WorldEventType.Theft, Owner, Player, GameTime.Zero);
            var crime = new Fact(fixture.World.NewId("fact"), Owner, FactPredicates.Stole, Player, originEvent: incident.Id);
            fixture.World.Knowledge.AddFact(crime);
            new BusinessContinuity(fixture.World).TryChangeState(Shop, BusinessContinuityState.Failed, GameTime.Zero, crime.Id);
            var service = Establish(fixture.World, new ServiceContinuityProducer(), GameTime.Zero);
            var settlement = new Fact(fixture.World.NewId("fact"), Owner, FactPredicates.Settled, EntityId.None);
            fixture.World.Knowledge.AddFact(settlement);
            service.FactIds.Add(settlement.Id);
            ThreadResolution.Resolve(fixture.World, service, "business_settled", Player, GameTime.Zero);
            Assert.Contains(DevelopmentDetector.Detect(fixture.World),
                d => d.FocusFactId == crime.Id && d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            var recovery = Establish(fixture.World, new UnresolvedCrimeProducer(), GameTime.Zero);
            Assert.True(recovery.IsLive);
            Assert.Equal(ThreadState.Resolved, service.State);
        }

        [Theory]
        [InlineData(ThreadState.Resolved)]
        [InlineData(ThreadState.Quarantined)]
        [InlineData(ThreadState.Inherited)]
        [InlineData(ThreadState.Dormant)]
        public void SharedFactsDoNotReactivateClosedOrDormantMatters(ThreadState state)
        {
            var fixture = new Fixture(true);
            fixture.Service.State = state;
            int tension = fixture.Service.Tension;
            fixture.World.Record(WorldEventType.Helped, Player, Owner, GameTime.Zero,
                related: new[] { fixture.Damage.Id }, threadId: fixture.Repair.Id);
            Assert.Equal(state, fixture.Service.State);
            Assert.Equal(tension, fixture.Service.Tension);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AdditionalOwnersUseTheSameAtomicRollback(bool business)
        {
            var fixture = new Fixture(null);
            ISituationProposalProducer owner = business ? (ISituationProposalProducer)new ServiceContinuityProducer() : new DamagedPropertyProducer();
            var selected = new SituationProposalEcology(new[] { owner }).Read(fixture.World).Select();
            string before = WorldStateSerializer.ToJson(fixture.World).Set("idCounters", JsonValue.Object()).ToJson();
            var result = CoreSituationEstablishment.Fulfill(owner, fixture.World, selected, GameTime.Zero,
                EstablishmentFault.AfterThreadStaged);
            Assert.False(result.Established);
            Assert.Equal(before, WorldStateSerializer.ToJson(fixture.World).Set("idCounters", JsonValue.Object()).ToJson());
            Assert.True(selected.Fulfill(GameTime.Zero).Established);
        }

        private static NarrativeThread Establish(NarrativeWorldState world, ISituationProposalProducer producer, GameTime now)
        {
            var selected = new SituationProposalEcology(new[] { producer }).Read(world).Select();
            Assert.NotNull(selected);
            var result = selected.Fulfill(now);
            Assert.True(result.Established, result.Refusal);
            return result.Thread;
        }

        // A second recognition legitimately consumes an event ID; compare the deed's effects
        // while retaining (and checking) their references to the actual committed deed/ending.
        private static string WithActionReferences(NarrativeWorldState world, string json)
        {
            foreach (WorldEvent occurrence in world.Ledger.Events)
                if (occurrence.Type == WorldEventType.Helped || occurrence.Type == WorldEventType.ThreadResolved)
                    json = json.Replace("\"" + occurrence.Id.Value + "\"", "\"" + occurrence.Type + "\"");
            return json;
        }

        private sealed class Fixture
        {
            internal readonly NarrativeWorldState World = new NarrativeWorldState(23);
            internal readonly SandboxVanillaState Native = new SandboxVanillaState(Player);
            internal readonly Fact Damage;
            internal readonly NarrativeThread Repair;
            internal readonly NarrativeThread Service;

            internal Fixture(bool? shared)
            {
                foreach (var who in new[] { Player, Owner, Outsider })
                    World.Registry.Add(new NarrativeNpc(who, who.Value));
                World.Registry.Add(new NarrativeSite(Place, "Workshop", "workshop"));
                Native.Define(Player, zone: Place).Define(Owner, zone: Place);
                Native.GiveItem(Place, new ItemDescriptor(Tool, "workshop tool", "tool", 100));
                Native.GiveItem(Player, new ItemDescriptor(EntityId.Parse("item_part1"), "plank", "wood", 1));
                Native.GiveItem(Player, new ItemDescriptor(EntityId.Parse("item_part2"), "plank", "wood", 1));
                var incident = World.Record(WorldEventType.Harmed, EntityId.None, Owner, GameTime.Zero);
                Damage = new Fact(World.NewId("fact"), Tool, FactPredicates.Damaged, Owner, originEvent: incident.Id);
                World.Knowledge.AddFact(Damage);
                var continuity = new BusinessContinuity(World);
                continuity.TryRegister(Shop, Place, Owner, GameTime.Zero);
                continuity.TryChangeState(Shop, BusinessContinuityState.ShortOnStock, GameTime.Zero, Damage.Id);
                // Normalize legacy optional-field defaults before comparing later save round trips.
                World = WorldStateSerializer.Load(WorldStateSerializer.Save(World));
                Damage = World.Knowledge.GetFact(Damage.Id);
                new ConsequenceEngine(World, Native).Attach();
                if (shared.HasValue)
                {
                    Repair = Establish(World, new DamagedPropertyProducer(), GameTime.Zero);
                    if (shared.Value) Service = Establish(World, new ServiceContinuityProducer(), GameTime.Zero);
                }
            }

            private ActionContext Context(NarrativeThread thread)
            {
                var context = new ActionContext(World, Native, new FixedCheckResolver(CheckOutcome.Pass),
                    World.Rng, Player, Owner) { Thread = thread, SubjectFact = Damage.Id };
                context.Witnesses.Add(Owner);
                return context;
            }

            internal bool CanRepair(NarrativeThread thread) => new RepairAction().GetAvailability(Context(thread)).IsAvailable;
            internal void Mend() => new RepairAction().Perform(Context(Repair));
        }
    }
}
