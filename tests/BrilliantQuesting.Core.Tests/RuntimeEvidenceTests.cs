using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Plugin;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public sealed class RuntimeEvidenceTests
    {
        [Fact]
        public void FrameWindowReportsDistributionAndExcludesUnfocusedGap()
        {
            var lines = new List<string>();
            var probe = new RuntimeEvidence(lines.Add);
            probe.Frame(0, true, null);
            probe.Frame(10, true, null);
            probe.Frame(30, true, null);
            probe.Frame(40, false, null);
            probe.Frame(10000, true, null);
            probe.Frame(10060, true, null);
            probe.Flush("test", null);
            Assert.Contains("frames=3 frame_ms(mean/p95/p99/max)=30.000/60.000/60.000/60.000 frames_over50ms=1", Assert.Single(lines));
            probe.Flush("empty", null);
            Assert.Contains("frames=0", lines[1]);
            Assert.DoesNotContain("frame_ms", lines[1]);
        }

        [Fact]
        public void FullBufferFlushesWithoutDroppingOrRepeatingFrames()
        {
            var lines = new List<string>();
            var probe = new RuntimeEvidence(lines.Add);
            for (int i = 0; i <= 8192; i++) probe.Frame(i, true, null);
            Assert.Contains("frames=8192", Assert.Single(lines));
            probe.Frame(8193, true, null);
            probe.Flush("tail", null);
            Assert.Contains("frames=1 frame_ms(mean/p95/p99/max)=1.000/1.000/1.000/1.000", lines[1]);
        }

        [Fact]
        public void ScopeRecordsExceptionalExitAndLoggingFailureCannotEscape()
        {
            var lines = new List<string>();
            var probe = new RuntimeEvidence(lines.Add);
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using (new RuntimeEvidence.Scope(probe, RuntimeEvidence.Callback.Act))
                    throw new InvalidOperationException();
            }));
            probe.Flush("test", null);
            Assert.Contains("Act(count/total_ms/mean_ms/max_ms)=1/", Assert.Single(lines));
            var broken = new RuntimeEvidence(_ => throw new InvalidOperationException());
            broken.Frame(0, true, null);
            broken.Frame(30000, true, null);
            broken.Flush("still-safe", null);
        }

        [Fact]
        public void HomeReadbackLeavesHistoryAndResidentClocksUntouchedAndBoundsDetail()
        {
            var lines = new List<string>();
            var probe = new RuntimeEvidence(lines.Add);
            var world = new NarrativeWorldState(108);
            var player = EntityId.Parse("player");
            var homeId = EntityId.Parse("home");
            var vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, zone: homeId);
            var home = new HomeStateBuilder(homeId, "Home").WithMetric(HomeMetric.Food, 73);
            for (int i = 0; i < 70; i++)
            {
                var npc = world.Registry.Add(new NarrativeNpc(EntityId.Parse("resident_" + i), "Resident"));
                vanilla.Define(npc.Id, zone: homeId);
                home.AddResident(npc.Id, npc.Name);
            }
            vanilla.SetHome(home.Build());
            string before = WorldStateSerializer.Save(world);
            probe.Home("before", world, vanilla);
            Assert.Equal(before, WorldStateSerializer.Save(world));
            Assert.Equal(73, vanilla.GetHomeState().GetMetric(HomeMetric.Food));
            Assert.Contains("resident_sample=64/70", Assert.Single(lines));
            Assert.Contains("[resident_63 clock=0", lines[0]);
            Assert.DoesNotContain("[resident_64", lines[0]);
            vanilla.SetHome(null);
            probe.Home("unknown", world, vanilla);
            Assert.Contains("home=unreadable", lines[1]);
        }
    }
}
