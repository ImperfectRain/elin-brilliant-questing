using System;
using System.IO;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    internal static class TravelingGroupsRun
    {
        public const ulong DefaultSeed = 97097UL;

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);

            Banner(output, "BQ-097: TRAVELING GROUPS");
            output.WriteLine("  Player zone: " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Player)));

            TravelingGroup semantic = bench.Plan("travel_semantic_caravan", TravelPhysicalPlan.SemanticOnly, bench.Merchant);
            TravelingGroup relocated = bench.Plan("travel_relocation_patrol", TravelPhysicalPlan.BqRelocation, bench.Patrol);
            TravelingGroup vanilla = bench.Plan("travel_vanilla_courier", TravelPhysicalPlan.BqRelocation, bench.Courier);
            TravelingGroup failed = bench.Plan("travel_failed_caravan", TravelPhysicalPlan.SemanticOnly, bench.LostMerchant);

            bench.SetNotMoving(bench.Patrol, bench.Origin);
            bench.SetVanillaMoving(bench.Courier, bench.Road);

            bench.Vanilla.AdvanceDays(1);
            TravelingGroupRound departure = bench.Travel.Advance(bench.Now);
            output.WriteLine(NarrativeInspector.DescribeTravelRound(departure));

            WorldEvent attack = bench.World.Record(
                WorldEventType.Attacked,
                bench.Bandits,
                failed.Id,
                bench.Now,
                0.8,
                bench.Ambush,
                related: new[] { bench.Wine });
            bench.Travel.TryInterrupt(failed.Id, attack.Id, bench.Ambush, bench.Now);
            bench.Travel.TryFail(failed.Id, attack.Id, bench.Ambush, bench.Now);

            bench.Vanilla.AdvanceDays(4);
            TravelingGroupRound arrival = bench.Travel.Advance(bench.Now);
            output.WriteLine(NarrativeInspector.DescribeTravelRound(arrival));

            bench.Vanilla.SetZone(bench.Courier, bench.Destination);
            bench.SetVanillaSettled(bench.Courier, bench.Destination);
            TravelingGroupRound reconciled = bench.Travel.Advance(bench.Now);
            output.WriteLine(NarrativeInspector.DescribeTravelRound(reconciled));

            Banner(output, "GROUP INSPECTOR");
            output.WriteLine(NarrativeInspector.DescribeTravelingGroup(bench.World, semantic.Id));
            output.WriteLine(NarrativeInspector.DescribeTravelingGroup(bench.World, relocated.Id));
            output.WriteLine(NarrativeInspector.DescribeTravelingGroup(bench.World, vanilla.Id));
            output.WriteLine(NarrativeInspector.DescribeTravelingGroup(bench.World, failed.Id));

            Banner(output, "EVENTS PRODUCED");
            foreach (WorldEvent recorded in bench.World.Ledger.Events.Where(e => IsTravel(e.Type) || e.Type == WorldEventType.Attacked))
            {
                output.WriteLine("  " + recorded.Type
                                 + " target " + recorded.Target
                                 + " zone " + bench.World.Registry.NameOf(recorded.Zone)
                                 + " related " + string.Join(", ", recorded.Related.Select(id => id.Value).ToArray()));
            }

            Banner(output, "IDENTITY AND IDEMPOTENCY");
            string saved = WorldStateSerializer.Save(bench.World);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(saved);
            int before = reloaded.Ledger.Events.Count(e => e.Type == WorldEventType.TravelArrived);
            new TravelingGroupLifecycle(reloaded, bench.Vanilla).Advance(bench.Now);
            int after = reloaded.Ledger.Events.Count(e => e.Type == WorldEventType.TravelArrived);
            output.WriteLine("  group identity after load: " + reloaded.TravelingGroups.Of(semantic.Id).Id);
            output.WriteLine("  member identity after load: " + reloaded.TravelingGroups.Of(semantic.Id).Member(bench.Merchant).ActorId);
            output.WriteLine("  cargo identity after load: " + reloaded.TravelingGroups.Of(semantic.Id).CargoIds[0]);
            output.WriteLine("  arrival events before/after duplicate pass: " + before + " -> " + after);
            output.WriteLine("  patrol physical zone: " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Patrol)));
            output.WriteLine("  courier physical zone: " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Courier)));
        }

        private static bool IsTravel(WorldEventType type)
        {
            return type == WorldEventType.TravelPlanned
                   || type == WorldEventType.TravelDeparted
                   || type == WorldEventType.TravelArrived
                   || type == WorldEventType.TravelInterrupted
                   || type == WorldEventType.TravelFailed;
        }

        private static void Banner(TextWriter output, string title)
        {
            output.WriteLine();
            output.WriteLine("== " + title + " " + new string('=', Math.Max(3, 68 - title.Length)));
        }

        private sealed class Bench
        {
            private Bench()
            {
            }

            public readonly EntityId Player = EntityId.Parse("npc_player");
            public readonly EntityId Merchant = EntityId.Parse("npc_wine_merchant");
            public readonly EntityId Patrol = EntityId.Parse("npc_patrol_leader");
            public readonly EntityId Courier = EntityId.Parse("npc_vanilla_courier");
            public readonly EntityId LostMerchant = EntityId.Parse("npc_lost_merchant");
            public readonly EntityId Origin = EntityId.Parse("zone_origin");
            public readonly EntityId Destination = EntityId.Parse("zone_destination");
            public readonly EntityId Road = EntityId.Parse("zone_road");
            public readonly EntityId Ambush = EntityId.Parse("site_ambush");
            public readonly EntityId Wine = EntityId.Parse("item_wine");
            public readonly EntityId Bandits = EntityId.Parse("org_bandits");

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public TravelingGroupLifecycle Travel { get; private set; }

            public GameTime Now => Vanilla.Now;

            public static Bench Create(ulong seed)
            {
                Bench bench = new Bench
                {
                    World = new NarrativeWorldState(seed),
                    Vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"))
                };

                bench.Travel = new TravelingGroupLifecycle(bench.World, bench.Vanilla);
                bench.World.Registry.Add(new NarrativeSite(bench.Origin, "origin", "town"));
                bench.World.Registry.Add(new NarrativeSite(bench.Destination, "destination", "town"));
                bench.World.Registry.Add(new NarrativeSite(bench.Road, "trade road", "road"));
                bench.World.Registry.Add(new NarrativeSite(bench.Ambush, "ambush camp", "camp"));
                bench.World.Registry.Add(new Organization(bench.Bandits, "Black Ford Crew", "bandits"));
                bench.AddActor(bench.Player, "You", bench.Origin);
                bench.AddActor(bench.Merchant, "Mara", bench.Origin);
                bench.AddActor(bench.Patrol, "Tellis", bench.Origin);
                bench.AddActor(bench.Courier, "Ordel", bench.Road);
                bench.AddActor(bench.LostMerchant, "Ivet", bench.Origin);
                bench.Vanilla.GiveItem(bench.Merchant, new ItemDescriptor(bench.Wine, "wine casks", "alcohol", 500));
                return bench;
            }

            public TravelingGroup Plan(string id, TravelPhysicalPlan physicalPlan, EntityId member)
            {
                TravelingGroup group = new TravelingGroup(
                    EntityId.Parse(id),
                    "caravan",
                    Origin,
                    Destination,
                    "carry_alcohol",
                    Now,
                    Now.PlusDays(1),
                    Now.PlusDays(5),
                    physicalPlan)
                {
                    RouteRisk = id.Contains("failed") ? 0.85 : 0.30
                };

                group.AddMember(member);
                group.AddCargo(Wine);
                Travel.TryPlan(group);
                return group;
            }

            public void SetNotMoving(EntityId actor, EntityId zone)
            {
                Vanilla.SetActorActivity(actor, new ActorActivityBuilder(actor)
                    .WithZone(zone)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.NotEligible)
                    .WithGlobalActivity(GlobalActivityKind.None)
                    .WithZoneTransition(ZoneTransitionState.None)
                    .Build());
            }

            public void SetVanillaMoving(EntityId actor, EntityId zone)
            {
                Vanilla.SetActorActivity(actor, new ActorActivityBuilder(actor)
                    .WithZone(zone)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                    .WithGlobalActivity(GlobalActivityKind.Travelling)
                    .WithZoneTransition(ZoneTransitionState.Pending)
                    .Build());
            }

            public void SetVanillaSettled(EntityId actor, EntityId zone)
            {
                Vanilla.SetActorActivity(actor, new ActorActivityBuilder(actor)
                    .WithZone(zone)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
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
