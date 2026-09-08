using System;
using System.IO;
using System.Linq;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    internal static class ConsequenceArrivalsRun
    {
        public const ulong DefaultSeed = 98098UL;

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);
            ConsequenceArrivals arrivals = new ConsequenceArrivals(bench.World, bench.Vanilla);

            Banner(output, "BQ-098: CONSEQUENCES ARRIVE");
            output.WriteLine("  Player starts in " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Player)) + ".");
            output.WriteLine("  Creditor starts in " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Creditor)) + ".");
            output.WriteLine("  Guard starts in " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Guard)) + ".");

            ConsequenceArrivalResult creditor = arrivals.TryBringToPlayer(
                bench.Thread,
                bench.Creditor,
                bench.Player,
                bench.Now,
                "creditor_arrives",
                new[] { bench.DebtFactId });
            ConsequenceArrivalResult guard = arrivals.TryBringToHome(
                bench.Thread,
                bench.Guard,
                bench.Debtor,
                bench.Now,
                "guard_follows_debt",
                new[] { bench.DebtFactId },
                WorldEventType.InquiryOpened,
                0.45);

            output.WriteLine("  Creditor arrival: " + creditor.Outcome + " (" + creditor.Reason + ").");
            output.WriteLine("  Guard arrival:    " + guard.Outcome + " (" + guard.Reason + ").");
            output.WriteLine("  Player remains in " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Player)) + ".");
            output.WriteLine("  Creditor now in   " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Creditor)) + ".");
            output.WriteLine("  Guard now in      " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Guard)) + ".");

            Banner(output, "EVENT LEDGER");
            foreach (WorldEvent recorded in bench.World.Ledger.Events.Where(e => e.Tags.Contains(ConsequenceArrivals.ArrivalTag)))
            {
                output.WriteLine("  " + recorded.Type
                                 + " by " + bench.World.Registry.NameOf(recorded.Actor)
                                 + " -> " + bench.World.Registry.NameOf(recorded.Target)
                                 + " at " + bench.World.Registry.NameOf(recorded.Zone)
                                 + " tags " + string.Join(", ", recorded.Tags.ToArray()));
            }

            Banner(output, "SAVE/RELOAD IDEMPOTENCY");
            int before = bench.World.Ledger.Events.Count(e => e.Tags.Contains(ConsequenceArrivals.ArrivalTag));
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(bench.World));
            ConsequenceArrivalResult duplicate = new ConsequenceArrivals(reloaded, bench.Vanilla).TryBringToPlayer(
                reloaded.GetThread(bench.Thread.Id),
                bench.Creditor,
                bench.Player,
                bench.Now,
                "creditor_arrives",
                new[] { bench.DebtFactId });
            int after = reloaded.Ledger.Events.Count(e => e.Tags.Contains(ConsequenceArrivals.ArrivalTag));
            output.WriteLine("  duplicate creditor arrival: " + duplicate.Outcome + ".");
            output.WriteLine("  arrival events before/after duplicate pass: " + before + " -> " + after);
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
            public readonly EntityId Debtor = EntityId.Parse("npc_debtor");
            public readonly EntityId Creditor = EntityId.Parse("npc_creditor");
            public readonly EntityId Guard = EntityId.Parse("npc_guard");
            public readonly EntityId Market = EntityId.Parse("zone_market");
            public readonly EntityId Road = EntityId.Parse("zone_road");
            public readonly EntityId Home = EntityId.Parse("zone_home");

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public NarrativeThread Thread { get; private set; }

            public EntityId DebtFactId { get; private set; }

            public GameTime Now => Vanilla.Now;

            public static Bench Create(ulong seed)
            {
                Bench bench = new Bench
                {
                    World = new NarrativeWorldState(seed),
                    Vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"))
                };

                bench.World.Registry.Add(new NarrativeNpc(bench.Player, "You") { Importance = NarrativeImportance.Major });
                bench.World.Registry.Add(new NarrativeNpc(bench.Debtor, "Mira") { Importance = NarrativeImportance.Known });
                bench.World.Registry.Add(new NarrativeNpc(bench.Creditor, "Haron") { Importance = NarrativeImportance.Known });
                bench.World.Registry.Add(new NarrativeNpc(bench.Guard, "Ovel") { Importance = NarrativeImportance.Known });
                bench.World.Registry.Add(new NarrativeSite(bench.Market, "Kell's Ford market", "market"));
                bench.World.Registry.Add(new NarrativeSite(bench.Road, "old road", "road"));
                bench.World.Registry.Add(new NarrativeSite(bench.Home, "Coldbeck steading", "home"));

                bench.Vanilla.Define(bench.Player, level: 9, zone: bench.Road);
                bench.Vanilla.Define(bench.Debtor, level: 4, zone: bench.Market);
                bench.Vanilla.Define(bench.Creditor, level: 6, zone: bench.Market);
                bench.Vanilla.Define(bench.Guard, level: 8, zone: bench.Market);
                bench.Vanilla.SetHome(new HomeStateBuilder(bench.Home, "Coldbeck steading")
                    .WithCapacity(4)
                    .WithMetric(HomeMetric.Safety, 18)
                    .Build());

                WorldEvent origin = bench.World.Record(
                    WorldEventType.DebtCreated,
                    bench.Debtor,
                    bench.Creditor,
                    bench.Now,
                    0.5,
                    bench.Market);
                Fact debt = new Fact(
                    bench.World.NewId("fact"),
                    bench.Debtor,
                    FactPredicates.Owes,
                    bench.Creditor,
                    "750 orens",
                    TruthState.True,
                    originEvent: origin.Id);
                bench.World.Knowledge.AddFact(debt);
                bench.World.Knowledge.Teach(bench.Debtor, debt.Id, KnowledgeSource.Participant, 1.0, bench.Now, true);
                bench.World.Knowledge.Teach(bench.Creditor, debt.Id, KnowledgeSource.Participant, 1.0, bench.Now, true);
                bench.DebtFactId = debt.Id;

                bench.Thread = new NarrativeThread(bench.World.NewId("thread"), "debt_default", bench.Now)
                {
                    OriginEventId = origin.Id,
                    State = ThreadState.Active,
                    Tension = 35,
                    Importance = 35
                };
                bench.Thread.ParticipantIds.Add(bench.Debtor);
                bench.Thread.ParticipantIds.Add(bench.Creditor);
                bench.Thread.ParticipantIds.Add(bench.Guard);
                bench.Thread.SiteIds.Add(bench.Market);
                bench.Thread.FactIds.Add(debt.Id);
                bench.Thread.GenerationCauses.Add("An unpaid debt was already in history before the arrival pass.");
                bench.World.Threads.Add(bench.Thread);
                return bench;
            }
        }
    }
}
