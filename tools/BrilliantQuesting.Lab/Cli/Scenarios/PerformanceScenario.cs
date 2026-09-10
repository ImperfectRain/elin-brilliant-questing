using System;
using System.Diagnostics;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>CPU/allocation probe, not an Elin frame-time acceptance test.</summary>
    internal sealed class PerformanceScenario : LabScenario
    {
        public override string Id => "performance";
        public override string Summary => "measure witnessed combat norms and dispatch against a restored large history";
        public override string Description => "Restores 20,000 historical actors and 100,000 recent events, with 22 local people. "
            + "Compares full and event-scoped norm reads, then measures synchronous witnessed attack dispatch. "
            + "Reports CPU p95 and thread allocations; setup, restore and warm-up are excluded. "
            + "Sandbox queries do not reproduce native costs or measure game frame time.";

        public override int Run(LabRunContext context)
        {
            var world = new NarrativeWorldState(context.Seed);
            var player = EntityId.Parse("npc_player");
            var zone = EntityId.Parse("zone_live");
            var away = EntityId.Parse("zone_history");
            var now = GameTime.FromDays(30);
            var vanilla = new SandboxVanillaState(player) { Now = now };
            vanilla.Define(player, zone: zone);
            var locals = new EntityId[22];
            for (int i = 0; i < 20022; i++)
            {
                var id = EntityId.Parse("npc_bench_" + i);
                world.Registry.Add(new NarrativeNpc(id, "Actor " + i));
                if (i >= locals.Length) continue;
                locals[i] = id;
                vanilla.Define(id, zone: zone).SetSocialAgency(id, SocialAgency.Full)
                    .SetActorKind(id, NarrativeActorKind.Person);
            }
            // Recent unrelated events exercise the existing history readers' worst-case window.
            // No listeners are attached during fixture creation or restore.
            for (int i = 0; i < 100000; i++)
                world.Record(WorldEventType.Helped, locals[0], locals[1], now, 0.1, away);
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Console.WriteLine("Synthetic restored history: actors=20022 events=" + world.Ledger.Count + " local=22");
            var expected = SocialPractices.Read(world, vanilla, zone, now).ReadingOf(WorldEventType.Attacked);
            var actual = SocialPractices.NormFor(world, vanilla, zone, now, WorldEventType.Attacked);
            if (expected.Describe() != actual.Describe()) throw new InvalidOperationException("Norm changed.");
            Measure("Full practice read + attack norm", () =>
                SocialPractices.Read(world, vanilla, zone, now).ReadingOf(WorldEventType.Attacked));
            Measure("Event-scoped attack norm", () =>
                SocialPractices.NormFor(world, vanilla, zone, now, WorldEventType.Attacked));
            var consequences = new ConsequenceEngine(world, vanilla);
            consequences.Attach();
            Measure("Witnessed attack dispatch", () => world.Record(WorldEventType.Attacked,
                player, locals[0], now, 0.2, zone, witnesses: locals));
            Console.WriteLine("Headless timings only; native identity reads, rendering and incremental BQ frame cost remain unmeasured.");
            return LabExit.Success;
        }

        private static void Measure(string label, Action action)
        {
            for (int i = 0; i < 10; i++) action();
            var samples = new double[100];
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                action();
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(samples);
            double total = 0;
            foreach (double sample in samples) total += sample;
            Console.WriteLine(FormattableString.Invariant($"{label}: n=100 mean_ms={total / 100:F3} p95_ms={samples[94]:F3} max_ms={samples[99]:F3} bytes_per_call={allocated / 100}"));
        }
    }
}
