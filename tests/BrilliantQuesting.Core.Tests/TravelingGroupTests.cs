using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class TravelingGroupTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Merchant = EntityId.Parse("npc_merchant");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Absentee = EntityId.Parse("npc_absentee");
        private static readonly EntityId Origin = EntityId.Parse("zone_origin");
        private static readonly EntityId Destination = EntityId.Parse("zone_destination");
        private static readonly EntityId Road = EntityId.Parse("zone_road");
        private static readonly EntityId Ambush = EntityId.Parse("site_ambush");
        private static readonly EntityId Cargo = EntityId.Parse("item_wine");
        private static readonly EntityId Bandits = EntityId.Parse("org_bandits");

        [Fact]
        public void SemanticOnlyTravelArrivesWithoutInventingPhysicalWhereabouts()
        {
            Lab lab = Lab.Create();
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.SemanticOnly);

            lab.Vanilla.AdvanceDays(5);
            TravelingGroupRound round = lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Arrived, group.State);
            Assert.Equal(1, round.Arrived);
            Assert.Equal(Origin, lab.Vanilla.GetZoneOf(Merchant));
            Assert.Equal(TravelMovementOwnership.SemanticOnly, group.Member(Merchant).Movement);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelDeparted);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelArrived);
        }

        [Fact]
        public void BqRelocationUsesTheGatedRelocationSeamAtArrival()
        {
            Lab lab = Lab.Create();
            lab.SetNotMoving(Merchant, Origin);
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.BqRelocation);

            lab.Vanilla.AdvanceDays(5);
            lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Arrived, group.State);
            Assert.Equal(Destination, lab.Vanilla.GetZoneOf(Merchant));
            Assert.Equal(TravelMovementOwnership.BqRelocated, group.Member(Merchant).Movement);
            Assert.Empty(lab.World.Absences.Active);
        }

        [Fact]
        public void VanillaOwnedTravelerIsObservedAndMustReconcileToDestination()
        {
            Lab lab = Lab.Create();
            lab.Vanilla.SetActorActivity(Merchant, new ActorActivityBuilder(Merchant)
                .WithZone(Road)
                .WithPresence(PhysicalPresence.OutsideActiveZone)
                .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                .WithGlobalActivity(GlobalActivityKind.Travelling)
                .WithZoneTransition(ZoneTransitionState.Pending)
                .Build());
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.BqRelocation);

            lab.Vanilla.AdvanceDays(5);
            TravelingGroupRound delayed = lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Underway, group.State);
            Assert.Equal(1, delayed.Delayed);
            Assert.Equal(TravelMovementOwnership.VanillaGlobalGoal, group.Member(Merchant).Movement);
            Assert.False(lab.World.Absences.IsAbsent(Merchant));
            Assert.NotEqual(Destination, lab.Vanilla.GetZoneOf(Merchant));

            lab.Vanilla.SetZone(Merchant, Destination);
            lab.Vanilla.SetActorActivity(Merchant, new ActorActivityBuilder(Merchant)
                .WithZone(Destination)
                .WithPresence(PhysicalPresence.OutsideActiveZone)
                .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                .WithGlobalActivity(GlobalActivityKind.None)
                .WithZoneTransition(ZoneTransitionState.None)
                .Build());
            TravelingGroupRound arrived = lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Arrived, group.State);
            Assert.Equal(1, arrived.Arrived);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelArrived);
        }

        [Fact]
        public void SaveLoadMidJourneyDoesNotReplayDepartureAndArrivesOnce()
        {
            Lab lab = Lab.Create();
            lab.SetNotMoving(Merchant, Origin);
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.BqRelocation);

            lab.Vanilla.AdvanceDays(1);
            lab.Travel.Advance(lab.Now);
            Assert.Equal(TravelingGroupState.Underway, group.State);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            TravelingGroup reopened = reloaded.TravelingGroups.Of(group.Id);
            TravelingGroupLifecycle travel = new TravelingGroupLifecycle(reloaded, lab.Vanilla);

            lab.Vanilla.AdvanceDays(4);
            travel.Advance(lab.Now);
            travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Arrived, reopened.State);
            Assert.Equal(group.Id, reopened.Id);
            Assert.Equal(Merchant, reopened.Member(Merchant).ActorId);
            Assert.Single(reloaded.Ledger.Events, e => e.Type == WorldEventType.TravelDeparted);
            Assert.Single(reloaded.Ledger.Events, e => e.Type == WorldEventType.TravelArrived);
        }

        [Fact]
        public void InterruptedJourneyDoesNotLaterArrive()
        {
            Lab lab = Lab.Create();
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.SemanticOnly);
            lab.Vanilla.AdvanceDays(1);
            lab.Travel.Advance(lab.Now);
            WorldEvent attack = lab.World.Record(
                WorldEventType.Attacked,
                Bandits,
                group.Id,
                lab.Now,
                0.8,
                Ambush,
                related: new[] { Cargo });

            Assert.True(lab.Travel.TryInterrupt(group.Id, attack.Id, Ambush, lab.Now));

            lab.Vanilla.AdvanceDays(9);
            lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Interrupted, group.State);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelInterrupted);
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelArrived);

            Assert.True(lab.Travel.TryFail(group.Id, attack.Id, Ambush, lab.Now));
            Assert.Equal(TravelingGroupState.Failed, group.State);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelFailed);
            Assert.Equal(attack.Id, group.InterruptionCauseId);
        }

        [Fact]
        public void DuplicateProcessingDoesNotDuplicateArrivalOrCargo()
        {
            Lab lab = Lab.Create();
            lab.SetNotMoving(Merchant, Origin);
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.BqRelocation);

            lab.Vanilla.AdvanceDays(5);
            lab.Travel.Advance(lab.Now);
            lab.Travel.Advance(lab.Now);

            WorldEvent arrived = Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.TravelArrived);
            Assert.Contains(Cargo, arrived.Related);
            Assert.Single(lab.Vanilla.GetInventory(Merchant), item => item.Id == Cargo);
            Assert.Equal(Merchant, group.Member(Merchant).ActorId);
        }

        [Fact]
        public void UnknownWhereaboutsRefusePhysicalRelocationWithoutInventingLocation()
        {
            Lab lab = Lab.Create();
            lab.Vanilla.Define(Stranger, zone: EntityId.None);
            lab.World.Registry.Add(new NarrativeNpc(Stranger, "Unknown"));
            lab.SetNotMoving(Stranger, EntityId.None);
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.BqRelocation, Stranger);

            lab.Vanilla.AdvanceDays(5);
            TravelingGroupRound round = lab.Travel.Advance(lab.Now);

            Assert.Equal(TravelingGroupState.Underway, group.State);
            Assert.Equal(1, round.Delayed);
            Assert.Equal(EntityId.None, lab.Vanilla.GetZoneOf(Stranger));
            Assert.Equal(TravelMovementOwnership.Refused, group.Member(Stranger).Movement);
            Assert.DoesNotContain(lab.World.Knowledge.Facts.Values, fact => fact.Predicate == FactPredicates.LocatedAt);
        }

        [Fact]
        public void UnrelatedAbsenceRecordsAreUntouched()
        {
            Lab lab = Lab.Create();
            Assert.True(lab.Absences.TrySendAway(Absentee, Road, "separate_absence", lab.Now.PlusDays(20)));
            lab.SetNotMoving(Merchant, Origin);
            lab.Plan(TravelPhysicalPlan.BqRelocation);

            lab.Vanilla.AdvanceDays(5);
            lab.Travel.Advance(lab.Now);

            ActorAbsence absence = lab.World.Absences.Of(Absentee);
            Assert.NotNull(absence);
            Assert.Equal(Road, absence.AwayZoneId);
            Assert.Equal(Origin, absence.HomeZoneId);
            Assert.Equal("separate_absence", absence.Reason);
            Assert.False(lab.World.Absences.IsAbsent(Merchant));
        }

        [Fact]
        public void InspectorShowsTravelOwnershipAndMilestones()
        {
            Lab lab = Lab.Create();
            TravelingGroup group = lab.Plan(TravelPhysicalPlan.SemanticOnly);
            lab.Vanilla.AdvanceDays(5);
            lab.Travel.Advance(lab.Now);

            string described = NarrativeInspector.DescribeTravelingGroup(lab.World, group.Id);

            Assert.Contains("origin -> destination", described);
            Assert.Contains("purpose:     carry_alcohol", described);
            Assert.Contains("physical:    SemanticOnly", described);
            Assert.Contains("milestone:   Arrived", described);
            Assert.Contains("movement SemanticOnly", described);
            Assert.Contains("item_wine", described);
            Assert.Contains("TravelArrived", described);
        }

        private sealed class Lab
        {
            private Lab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public TravelingGroupLifecycle Travel { get; private set; }

            public AbsenceLifecycle Absences { get; private set; }

            public GameTime Now => Vanilla.Now;

            public static Lab Create()
            {
                Lab lab = new Lab
                {
                    World = new NarrativeWorldState(97097),
                    Vanilla = new SandboxVanillaState(Player)
                };

                lab.Travel = new TravelingGroupLifecycle(lab.World, lab.Vanilla);
                lab.Absences = new AbsenceLifecycle(lab.World, lab.Vanilla);
                lab.World.Registry.Add(new NarrativeSite(Origin, "origin", "town"));
                lab.World.Registry.Add(new NarrativeSite(Destination, "destination", "town"));
                lab.World.Registry.Add(new NarrativeSite(Road, "road", "road"));
                lab.World.Registry.Add(new NarrativeSite(Ambush, "ambush site", "camp"));
                lab.World.Registry.Add(new Organization(Bandits, "Black Ford Crew", "bandits"));
                lab.AddActor(Player, "You", Origin);
                lab.AddActor(Merchant, "Mara", Origin);
                lab.AddActor(Guard, "Tellis", Origin);
                lab.AddActor(Absentee, "Pavel", Origin);
                lab.Vanilla.GiveItem(Merchant, new ItemDescriptor(Cargo, "casks of wine", "alcohol", 500));
                return lab;
            }

            public TravelingGroup Plan(TravelPhysicalPlan physicalPlan, EntityId member = default)
            {
                TravelingGroup group = new TravelingGroup(
                    EntityId.Parse("travel_caravan"),
                    "caravan",
                    Origin,
                    Destination,
                    "carry_alcohol",
                    Now,
                    Now.PlusDays(1),
                    Now.PlusDays(5),
                    physicalPlan)
                {
                    RouteRisk = 0.35
                };

                group.AddMember(member.IsNone ? Merchant : member);
                group.AddCargo(Cargo);
                Assert.True(Travel.TryPlan(group));
                return group;
            }

            public void SetNotMoving(EntityId actor, EntityId zone)
            {
                Vanilla.SetActorActivity(actor, new ActorActivityBuilder(actor)
                    .WithZone(zone)
                    .WithPresence(zone.IsNone ? PhysicalPresence.Unknown : PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.NotEligible)
                    .WithGlobalActivity(GlobalActivityKind.None)
                    .WithZoneTransition(ZoneTransitionState.None)
                    .Build());
            }

            private void AddActor(EntityId id, string name, EntityId zone)
            {
                World.Registry.Add(new NarrativeNpc(id, name));
                Vanilla.Define(id, zone: zone);
            }
        }
    }
}
