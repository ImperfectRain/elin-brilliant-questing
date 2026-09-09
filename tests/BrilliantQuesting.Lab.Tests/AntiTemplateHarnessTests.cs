using System;
using System.IO;
using System.Linq;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Lab.Scenes;
using BrilliantQuesting.Persistence;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    public class AntiTemplateHarnessTests
    {
        [Fact]
        public void MissingEvidenceDoesNotMasqueradeAsRepetition()
        {
            var metric = new RepetitionMetric();
            metric.Observe(null);
            metric.Observe("");
            Assert.Null(metric.RepeatRate);
            metric.Observe("social");
            metric.Observe("economic");
            metric.Observe("social");
            Assert.Equal(2, metric.Missing);
            Assert.Equal(3, metric.Observations);
            Assert.Equal(2, metric.Distinct);
            Assert.Equal(1, metric.Repeats);
            Assert.Equal(1.0 / 3, metric.RepeatRate);
        }

        [Fact]
        public void RenamingActorsAndSitesDoesNotHideTheSameShape()
        {
            var fixture = SceneSituations.Find("theft").Build(15);
            string Before() => AntiTemplateHarness.ReadCausalSkeleton(fixture);
            string original = Before();
            foreach (var npc in fixture.World.Registry.Npcs.Values) npc.Name = "renamed " + npc.Id;
            foreach (var site in fixture.World.Registry.Sites.Values) site.Name = "different place";
            var metric = new RepetitionMetric();
            metric.Observe(original);
            metric.Observe(Before());
            Assert.Equal(1, metric.Repeats);
        }

        [Fact]
        public void ReadingCausalSeedsIsReadOnlyAndDistinguishesEconomicPredicates()
        {
            var debt = SceneSituations.Find("debt").Build(15);
            var shortage = SceneSituations.Find("shortage").Build(15);
            string before = WorldStateSerializer.Save(debt.World);
            string debtShape = AntiTemplateHarness.ReadCausalSkeleton(debt);
            Assert.NotNull(debtShape);
            Assert.NotEqual(debtShape, AntiTemplateHarness.ReadCausalSkeleton(shortage));
            Assert.Equal(before, WorldStateSerializer.Save(debt.World));
        }

        [Fact]
        public void SyntheticBatchReplaysAndReportsEveryAxisWithoutInventingCoverage()
        {
            var bundle = AntiTemplateHarness.LoadBundle();
            var result = AntiTemplateHarness.Run(bundle, 15, 3);
            Assert.Equal(result.ToJson(), AntiTemplateHarness.Run(bundle, 15, 3).ToJson());
            Assert.Empty(result.Failures);
            Assert.Equal(3 * SceneSituations.All.Count, result.Runs);
            Assert.Equal(7, result.Metrics.Count);
            Assert.True(result.Presentations > 0);
            Assert.Equal(result.Presentations, result.Metrics["storylets"].Observations);
            Assert.Equal(result.Runs, result.Metrics["causalSkeleton"].Observations + result.Metrics["causalSkeleton"].Missing);
            Assert.Equal(result.Runs, result.Metrics["rewards"].Observations + result.Metrics["rewards"].Missing);
            Assert.True(result.Metrics["openers"].Observations > 0);
            Assert.True(result.Metrics["rewards"].Observations > 0);
            Assert.True(result.Metrics["sites"].Missing > 0);
            Assert.All(result.Metrics.Values, m => Assert.Equal(m.Observations - m.Distinct, m.Repeats));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1001)]
        public void RejectsInvalidBatchSizes(int seeds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AntiTemplateHarness.Run(null, 0, seeds));
        }

        [Fact]
        public void RejectsSeedOverflowBeforeGeneratingAnything()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AntiTemplateHarness.Run(null, ulong.MaxValue, 2));
        }

        [Fact]
        public void CatalogCommandEmitsAHeadlessJsonProfile()
        {
            var scenario = LabCatalog.Default().Find("anti-template");
            using var output = new StringWriter();
            int exit = scenario.Run(new LabRunContext(scenario, 15, LabArguments.Parse(new[] { "--runs", "2" }), output, TextWriter.Null));
            Assert.Equal(LabExit.Success, exit);
            using var json = System.Text.Json.JsonDocument.Parse(output.ToString());
            Assert.Equal(2 * SceneSituations.All.Count, json.RootElement.GetProperty("Runs").GetInt32());
            Assert.Equal(7, json.RootElement.GetProperty("Metrics").EnumerateObject().Count());
        }
    }
}
