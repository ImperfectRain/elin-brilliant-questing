using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Plugin;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class CapabilityDegradationTests
    {
        public static IEnumerable<object[]> Capabilities => Enum.GetValues<VanillaCapability>()
            .Select(capability => new object[] { capability });

        [Theory]
        [MemberData(nameof(Capabilities))]
        public void EachDisableSkipsOnlyItsProbeAndReportsTheReason(VanillaCapability disabled)
        {
            var logs = new List<string>();
            var report = new VanillaCapabilityReport(disabled.ToString(), logs.Add);
            // Detection repeats on a new save attach; it must retain the drill selection.
            for (int attach = 0; attach < 2; attach++)
            {
                report.Clear();
                var called = new HashSet<VanillaCapability>();
                foreach (var capability in Enum.GetValues<VanillaCapability>())
                    report.Probe(capability, () => { called.Add(capability); return "successful readback"; });
                Assert.DoesNotContain(disabled, called);
                Assert.Equal(Enum.GetValues<VanillaCapability>().Length - 1, report.Count);
                foreach (var capability in Enum.GetValues<VanillaCapability>())
                    Assert.Equal(capability != disabled, report.Supports(capability));
                report.Report(logs.Add);
                Assert.Contains(logs, line => line.Contains("capability " + disabled + ": unavailable - disabled by BQ-109 drill"));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("0")]
        [InlineData("ReadFaith, ReadSkills")]
        [InlineData("misspelled")]
        public void EmptyOrInvalidSettingCannotDisableOrEnableCapabilities(string setting)
        {
            var logs = new List<string>();
            var report = new VanillaCapabilityReport(setting, logs.Add);
            report.Probe(VanillaCapability.ReadFaith, () => "read");
            report.MarkUnsupported(VanillaCapability.BuildPlaceStructure, "not implemented");
            Assert.True(report.Supports(VanillaCapability.ReadFaith));
            Assert.False(report.Supports(VanillaCapability.BuildPlaceStructure));
            Assert.All(Enum.GetValues<VanillaCapability>(), c => Assert.False(report.IsDisabled(c)));
            Assert.Equal(!string.IsNullOrWhiteSpace(setting), logs.Any(line => line.Contains("INVALID")));
        }

        [Fact]
        public void FailedReprobeRevokesSupportWithoutAffectingOtherCapabilities()
        {
            var logs = new List<string>();
            var report = new VanillaCapabilityReport(null, logs.Add);
            report.Probe(VanillaCapability.ReadFaith, () => "read");
            report.Probe(VanillaCapability.ReadSkills, () => "read");
            report.Probe(VanillaCapability.ReadFaith, () => throw new InvalidOperationException("unreadable"));
            Assert.False(report.Supports(VanillaCapability.ReadFaith));
            Assert.True(report.Supports(VanillaCapability.ReadSkills));
            report.Report(logs.Add);
            Assert.Contains(logs, line => line.Contains("InvalidOperationException: unreadable"));
        }

        [Fact]
        public void AlreadyUnsupportedCapabilityKeepsItsBaselineReasonAndCannotBeEnabled()
        {
            var logs = new List<string>();
            var report = new VanillaCapabilityReport(" buildplacestructure ", logs.Add);
            report.MarkUnsupported(VanillaCapability.BuildPlaceStructure, "not implemented");
            report.Report(logs.Add);
            Assert.False(report.Supports(VanillaCapability.BuildPlaceStructure));
            Assert.Contains(logs, line => line.Contains("disabled by BQ-109 drill; not implemented"));
            var restored = new VanillaCapabilityReport("", logs.Add);
            restored.Probe(VanillaCapability.BuildPlaceStructure, () => null, "not implemented");
            Assert.False(restored.Supports(VanillaCapability.BuildPlaceStructure));
        }

        [Theory]
        [MemberData(nameof(Capabilities))]
        public void OneMissingCapabilityDoesNotBreakTheftDiscoveryAdvanceOrRoundTrip(VanillaCapability disabled)
        {
            // Exercises Core consumers through the existing sandbox seam; not native proof.
            var lab = TheftLaboratory.Create();
            lab.Vanilla.SetCapability(disabled, false);
            foreach (var target in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
                Assert.NotNull(lab.Actions.AvailableFamilies(lab.Context(target)));
            Assert.True(lab.Vanilla.IsAlive(lab.Player));
            Assert.Equal(lab.Zone, lab.Vanilla.GetZoneOf(lab.Player));
            lab.AdvanceDays(10);
            var reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            Assert.Equal(lab.World.Ledger.Count, reloaded.Ledger.Count);
            Assert.Equal(lab.World.Threads.Count, reloaded.Threads.Count);
        }
    }
}
