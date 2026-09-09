using System;
using System.IO;
using System.Linq;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    public class SpatialRangeHarnessTests
    {
        private static SiteGrammar Sketch(string noun, bool reverse = false, bool cycle = false, string verb = "trespass")
        {
            var nodes = new[] { new SiteNodeSpec(noun + "hall", true, Array.Empty<SiteAffordance>(), ""),
                new SiteNodeSpec(noun + "vault", true, new[] { SiteAffordance.EvidenceCache }, "") };
            var routes = new[] { new SiteRouteSpec(SiteGrammar.Outside, noun + "hall", verb, false, Array.Empty<SiteAffordance>()),
                new SiteRouteSpec(noun + "hall", noun + "vault", "trespass", false, Array.Empty<SiteAffordance>()) }.ToList();
            if (cycle) routes.Add(new SiteRouteSpec(noun + "vault", noun + "hall", "trespass", false, Array.Empty<SiteAffordance>()));
            return new SiteGrammar(noun, "hideout", true, reverse ? nodes.Reverse() : nodes, reverse ? routes.AsEnumerable().Reverse() : routes);
        }

        [Fact]
        public void RenamedAndReorderedPlansCountAsExperientialRepetitionWithoutMutatingState()
        {
            var fixture = SpatialRangeHarness.BuildFixture(15);
            string saved = WorldStateSerializer.Save(fixture.World);
            var first = SpatialRangeHarness.Plan(fixture, Sketch("camp"), 15);
            var second = SpatialRangeHarness.Plan(fixture, Sketch("mine", reverse: true), 15);
            Assert.True(first.Valid, string.Join("; ", first.Refusals));
            Assert.True(second.Valid, string.Join("; ", second.Refusals));
            Assert.NotEqual(first.PlanId, second.PlanId);
            var metric = new RepetitionMetric();
            metric.Observe(SpatialRangeHarness.Measure(first)["experientialTopology"]);
            metric.Observe(SpatialRangeHarness.Measure(second)["experientialTopology"]);
            Assert.Equal(1, metric.Repeats);
            Assert.Equal(SpatialRangeHarness.Measure(first), SpatialRangeHarness.Measure(second));
            Assert.Equal(WorldStateSerializer.Save(fixture.World), saved);
        }

        [Fact]
        public void SymmetricBranchesAreCanonicalAndSearchExhaustionIsMissing()
        {
            SiteGrammar Branches(string noun, int count, bool reverse)
            {
                var nodes = Enumerable.Range(0, count).Select(i => new SiteNodeSpec(noun + i, true, Array.Empty<SiteAffordance>(), "")).ToList();
                nodes.Add(new SiteNodeSpec(noun + "goal", true, new[] { SiteAffordance.EvidenceCache }, ""));
                var routes = Enumerable.Range(0, count).SelectMany(i => new[] {
                    new SiteRouteSpec(SiteGrammar.Outside, noun + i, "trespass", false, Array.Empty<SiteAffordance>()),
                    new SiteRouteSpec(noun + i, noun + "goal", "trespass", false, Array.Empty<SiteAffordance>()) }).ToList();
                routes.Add(new SiteRouteSpec(SiteGrammar.Outside, noun + "goal", "persuade", false, new[] { SiteAffordance.GuardedThreshold }));
                return new SiteGrammar(noun, "hideout", true, reverse ? nodes.AsEnumerable().Reverse() : nodes,
                    reverse ? routes.AsEnumerable().Reverse() : routes);
            }
            var fixture = SpatialRangeHarness.BuildFixture(15);
            var first = SpatialRangeHarness.Plan(fixture, Branches("a", 3, false), 15);
            var renamed = SpatialRangeHarness.Plan(fixture, Branches("b", 3, true), 15);
            Assert.True(first.Valid, string.Join("; ", first.Refusals.Concat(first.Validation.Findings.Where(f => !f.Held).Select(f => f.ToString()))));
            Assert.True(renamed.Valid);
            Assert.NotNull(SpatialRangeHarness.Topology(first, true));
            Assert.Equal(SpatialRangeHarness.Topology(first, true), SpatialRangeHarness.Topology(renamed, true));
            var large = SpatialRangeHarness.Plan(fixture, Branches("large", 9, false), 15);
            Assert.True(large.Valid);
            var metric = new RepetitionMetric();
            metric.Observe(SpatialRangeHarness.Topology(large, true));
            Assert.Equal(1, metric.Missing);
            Assert.Equal(0, metric.Repeats);
        }

        [Fact]
        public void ActualCyclesAndDifferentMechanicsAreNotNounChanges()
        {
            var fixture = SpatialRangeHarness.BuildFixture(15);
            var plain = SpatialRangeHarness.Plan(fixture, Sketch("a"), 15);
            var cycle = SpatialRangeHarness.Plan(fixture, Sketch("b", cycle: true), 15);
            var social = SpatialRangeHarness.Plan(fixture, Sketch("c", verb: "persuade"), 15);
            Assert.True(cycle.Valid);
            Assert.True(social.Valid, string.Join("; ", social.Refusals));
            Assert.NotEqual(SpatialRangeHarness.Topology(plain, false), SpatialRangeHarness.Topology(cycle, false));
            Assert.NotEqual(SpatialRangeHarness.Measure(plain)["cycleCount"], SpatialRangeHarness.Measure(cycle)["cycleCount"]);
            Assert.Equal(SpatialRangeHarness.Topology(plain, false), SpatialRangeHarness.Topology(social, false));
            Assert.NotEqual(SpatialRangeHarness.Topology(plain, true), SpatialRangeHarness.Topology(social, true));
        }

        [Fact]
        public void BatchReplaysReportsAllAxesAndSeparatesRefusals()
        {
            var bundle = AntiTemplateHarness.LoadBundle();
            var report = SpatialRangeHarness.Run(bundle, 15, 3);
            Assert.Equal(report.ToJson(), SpatialRangeHarness.Run(bundle, 15, 3).ToJson());
            Assert.True(report.Accepted > 3);
            Assert.NotEmpty(report.Rejections);
            Assert.Equal(report.Runs, report.Accepted + report.Rejections.Count);
            Assert.Equal(8, report.Metrics.Count);
            Assert.All(report.Metrics.Values, m => Assert.Equal(report.Accepted, m.Observations + m.Missing));
            Assert.True(report.Metrics["experientialTopology"].Distinct > 1);
            Assert.True(report.Metrics["experientialTopology"].Repeats > 0);
            Assert.True(report.Metrics["evidenceDistribution"].Observations > 0);
            Assert.True(report.Metrics["historyReadability"].Observations > 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1001)]
        public void InvalidBatchIsRejected(int count) => Assert.Throws<ArgumentOutOfRangeException>(() => SpatialRangeHarness.Run(null, 0, count));

        [Fact]
        public void OverflowIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => SpatialRangeHarness.Run(null, ulong.MaxValue, 2));

        [Fact]
        public void CatalogEmitsJson()
        {
            var scenario = LabCatalog.Default().Find("spatial-range");
            using var output = new StringWriter();
            Assert.Equal(LabExit.Success, scenario.Run(new LabRunContext(scenario, 15,
                LabArguments.Parse(new[] { "--runs", "2" }), output, TextWriter.Null)));
            using var json = System.Text.Json.JsonDocument.Parse(output.ToString());
            Assert.Equal(8, json.RootElement.GetProperty("Metrics").EnumerateObject().Count());
        }
    }
}
