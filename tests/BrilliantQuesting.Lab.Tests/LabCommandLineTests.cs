using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Cli;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// Covers the laboratory's dispatch surface: discovery, the historic command forms, seeding,
    /// option handling and exit status. Resolution is asserted without running a simulation, which
    /// is the point of separating <see cref="LabCommandLine.Resolve"/> from execution.
    /// </summary>
    public class LabCommandLineTests
    {
        [Fact]
        public void NoArgumentsRunsTheTheftLaboratoryOnItsDefaultSeed()
        {
            LabInvocation invocation = Resolve();

            Assert.True(invocation.IsValid);
            Assert.Equal(LabCommand.Run, invocation.Command);
            Assert.Equal("theft", invocation.Scenario.Id);
            Assert.Equal(LabDefaults.Seed, invocation.Seed);
        }

        [Fact]
        public void BareSeedStillRunsTheTheftLaboratory()
        {
            LabInvocation invocation = Resolve("7");

            Assert.True(invocation.IsValid);
            Assert.Equal("theft", invocation.Scenario.Id);
            Assert.Equal(7UL, invocation.Seed);
        }

        [Theory]
        [InlineData("--questline", "questline")]
        [InlineData("--questline-sweep", "questline-sweep")]
        [InlineData("--ambient", "ambient")]
        [InlineData("--news", "news")]
        [InlineData("--guilds", "guilds")]
        [InlineData("--authority", "authority")]
        [InlineData("--integration", "integration")]
        [InlineData("--find-seed", "find-seed")]
        public void HistoricFlagsStillSelectTheirScenario(string flag, string expectedId)
        {
            LabInvocation invocation = Resolve(flag);

            Assert.True(invocation.IsValid, invocation.Error);
            Assert.Equal(LabCommand.Run, invocation.Command);
            Assert.Equal(expectedId, invocation.Scenario.Id);
        }

        [Fact]
        public void HistoricFlagsStillTakeTheirSeedAsABareArgument()
        {
            Assert.Equal(7UL, Resolve("--ambient", "7").Seed);
            Assert.Equal(7UL, Resolve("--questline", "7").Seed);
        }

        [Fact]
        public void AScenarioMayKeepItsOwnDefaultSeed()
        {
            Assert.Equal(LabDefaults.Seed, Resolve("--ambient").Seed);
            Assert.Equal(3UL, Resolve("--authority").Seed);
        }

        [Fact]
        public void RunVerbTakesTheSeedAsAnOption()
        {
            LabInvocation invocation = Resolve("run", "ambient", "--seed", "9");

            Assert.True(invocation.IsValid, invocation.Error);
            Assert.Equal("ambient", invocation.Scenario.Id);
            Assert.Equal(9UL, invocation.Seed);
        }

        [Fact]
        public void SeedResolutionIsTheSameThroughEveryForm()
        {
            Assert.Equal(Resolve("--ambient", "42").Seed, Resolve("run", "ambient", "--seed", "42").Seed);
            Assert.Equal(Resolve("run", "ambient", "42").Seed, Resolve("run", "ambient", "--seed=42").Seed);
        }

        [Fact]
        public void TheIntegrationHarnessKeepsItsOwnArguments()
        {
            LabInvocation invocation = Resolve("--integration", "--mode", "compare", "--snapshot", "world.json");

            Assert.True(invocation.IsValid, invocation.Error);
            Assert.True(invocation.Scenario.ForwardsRawArguments);
            Assert.Equal(new[] { "--mode", "compare", "--snapshot", "world.json" }, invocation.RawArguments);
        }

        [Fact]
        public void ListNamesEveryRegisteredScenario()
        {
            LabCatalog catalog = LabCatalog.Default();
            StringWriter output = new StringWriter();

            int status = LabCommandLine.Execute(new[] { "list" }, catalog, output, new StringWriter());

            Assert.Equal(LabExit.Success, status);
            foreach (LabScenario scenario in catalog.Scenarios)
            {
                Assert.Contains(scenario.Id, output.ToString());
            }
        }

        [Fact]
        public void DescribeReportsOptionsDefaultsAndAliases()
        {
            StringWriter output = new StringWriter();

            int status = LabCommandLine.Execute(new[] { "describe", "ambient" }, LabCatalog.Default(), output, new StringWriter());

            string described = output.ToString();
            Assert.Equal(LabExit.Success, status);
            Assert.Contains("ambient", described);
            Assert.Contains("--ambient", described);
            Assert.Contains("--days", described);
            Assert.Contains("default 14", described);
            Assert.Contains("default 15", described);
        }

        [Fact]
        public void HelpIsAvailableUnderItsUsualNames()
        {
            foreach (string form in new[] { "help", "--help", "-h" })
            {
                StringWriter output = new StringWriter();
                int status = LabCommandLine.Execute(new[] { form }, LabCatalog.Default(), output, new StringWriter());

                Assert.Equal(LabExit.Success, status);
                Assert.Contains("run <scenario>", output.ToString());
            }
        }

        [Theory]
        [InlineData(new[] { "nonsense" }, "Unknown command")]
        [InlineData(new[] { "run", "nonsense" }, "Unknown scenario")]
        [InlineData(new[] { "describe", "nonsense" }, "Unknown scenario")]
        [InlineData(new[] { "run" }, "run needs a scenario")]
        [InlineData(new[] { "describe" }, "describe needs a scenario")]
        [InlineData(new[] { "run", "ambient", "--nope" }, "no option --nope")]
        [InlineData(new[] { "run", "ambient", "--seed", "later" }, "--seed")]
        [InlineData(new[] { "run", "ambient", "1", "2" }, "at most 1")]
        public void AnUnreadableCommandLineFailsWithAMessageAndNoRun(string[] args, string expected)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int status = LabCommandLine.Execute(args, LabCatalog.Default(), output, error);

            Assert.Equal(LabExit.UsageError, status);
            Assert.Contains(expected, error.ToString());
            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public void ScenarioNamesAndAliasesAreUnique()
        {
            LabCatalog catalog = LabCatalog.Default();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LabScenario scenario in catalog.Scenarios)
            {
                Assert.True(names.Add(scenario.Id), "Duplicate scenario id " + scenario.Id);
                Assert.Same(scenario, catalog.Find(scenario.Id));
                foreach (string alias in scenario.Aliases)
                {
                    Assert.True(names.Add(alias), "Duplicate scenario alias " + alias);
                    Assert.Same(scenario, catalog.Find(alias));
                }
            }
        }

        [Fact]
        public void EveryScenarioDescribesItself()
        {
            foreach (LabScenario scenario in LabCatalog.Default().Scenarios)
            {
                Assert.False(string.IsNullOrWhiteSpace(scenario.Id));
                Assert.False(string.IsNullOrWhiteSpace(scenario.Summary));
                Assert.False(string.IsNullOrWhiteSpace(scenario.Description));
                Assert.Equal(scenario.Id.ToLowerInvariant(), scenario.Id);
            }
        }

        [Fact]
        public void RunningAScenarioPassesItTheSeedAndOptionsAndReturnsItsStatus()
        {
            RecordingScenario recording = new RecordingScenario();
            LabCatalog catalog = new LabCatalog(new LabScenario[] { recording });
            StringWriter output = new StringWriter();

            int status = LabCommandLine.Execute(
                new[] { "run", "recording", "--seed", "11", "--days", "4" },
                catalog,
                output,
                new StringWriter());

            Assert.Equal(3, status);
            Assert.Equal(11UL, recording.Seed);
            Assert.Equal(4, recording.Days);
            Assert.Contains("recording ran", output.ToString());
        }

        [Fact]
        public void ARegisteredScenarioActuallyRunsTheProductionSimulation()
        {
            int status = LabCommandLine.Execute(
                new[] { "run", "guilds", "--seed", "15", "--days", "1" },
                LabCatalog.Default(),
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(LabExit.Success, status);
        }

        private static LabInvocation Resolve(params string[] args)
        {
            return LabCommandLine.Resolve(LabCatalog.Default(), args);
        }

        private sealed class RecordingScenario : LabScenario
        {
            public ulong Seed { get; private set; }

            public int Days { get; private set; }

            public override string Id => "recording";

            public override string Summary => "records what the runner handed it";

            public override IReadOnlyList<LabOption> Options => new[]
            {
                new LabOption("days", "n", "days", "1")
            };

            public override int Run(LabRunContext context)
            {
                Seed = context.Seed;
                Days = context.Arguments.Int("days", 1);
                context.WriteLine("recording ran");
                return 3;
            }
        }
    }
}
