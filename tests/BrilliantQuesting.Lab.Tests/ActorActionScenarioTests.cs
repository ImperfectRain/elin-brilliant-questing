using System;
using System.IO;
using BrilliantQuesting.Lab.Cli;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// BQ-093's inspector proof, asserted as a proof rather than as a smoke test.
    ///
    /// The scenario exists so a reader can follow one NPC from a need to a recorded consequence,
    /// so what is checked here is that every joint in that chain is actually printed - a run that
    /// silently stopped printing which verb an approach bound to would still exit zero.
    /// </summary>
    public class ActorActionScenarioTests
    {
        [Fact]
        public void TheRunPrintsEveryJointFromGoalToRecordedEvent()
        {
            string report = Run();

            // Selection, and that it is selection: needs, values and scored approaches.
            Assert.Contains("goal formation for Nessa", report);
            Assert.Contains("need: Protection", report);
            Assert.Contains("candidate actions:", report);

            // The join, and that it names a registered verb rather than a style label.
            Assert.Contains("= verb question", report);
            Assert.Contains("attempted as: question", report);
            Assert.Contains("is attempted as the registered verb 'question'", report);

            // Context construction, availability, the shared check, the shared ledger.
            Assert.Contains("within earshot:", report);
            Assert.Contains("who may take it: any actor", report);
            Assert.Contains("availability: available", report);
            Assert.Contains("check proc_interrogation", report);
            Assert.Contains("recorded Conversed: Nessa -> Haron", report);
        }

        [Fact]
        public void TheRunShowsTheSameVerbObjectServingBothActors()
        {
            string report = Run();

            Assert.Contains("attempt by Nessa", report);
            Assert.Contains("attempt by the player", report);
            Assert.Contains("same registered verb object: yes, the same instance", report);
            Assert.DoesNotContain("NO - two objects", report);
        }

        [Fact]
        public void TheRunShowsAnNpcActCostingThePlayerNothing()
        {
            string report = Run();

            Assert.Contains("the player believes it:     False", report);
            Assert.Contains("player karma / fame:        0 / 0", report);
            Assert.Contains("Haron's affinity to player: 40", report);
        }

        [Fact]
        public void TheRunNamesTheVerbsSheMayNotTakeAndWhy()
        {
            string report = Run();

            Assert.Contains("the Home is the player's settlement", report);
            Assert.Contains("a guild card, rank and contribution are the player's", report);
            Assert.Contains("BQ keeps no underworld standing for anybody else", report);
        }

        [Fact]
        public void TheRunClaimsNothingPhysicalItDidNotDo()
        {
            string report = Run();

            Assert.Contains("embodiment resolved coarsely", report);
            Assert.Contains("embodiment delegated to vanilla", report);
            Assert.Contains("vanilla travel: Unknown", report);
            Assert.DoesNotContain("tile", report);
        }

        [Fact]
        public void TheScenarioIsRegisteredUnderItsIdAndItsFlag()
        {
            Assert.Equal("actor-action", Resolve("run", "actor-action").Scenario.Id);
            Assert.Equal("actor-action", Resolve("--actor-action").Scenario.Id);
        }

        private static string Run()
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int status = LabCommandLine.Execute(
                new[] { "run", "actor-action" }, LabCatalog.Default(), output, error);

            Assert.Equal(0, status);
            Assert.Equal(string.Empty, error.ToString());
            return output.ToString();
        }

        private static LabInvocation Resolve(params string[] args)
        {
            LabInvocation invocation = LabCommandLine.Resolve(LabCatalog.Default(), args);
            Assert.True(invocation.IsValid, invocation.Error);
            return invocation;
        }
    }
}
