using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-012: the twelve questions in `living-world-priorities.md` §12 must all be answerable
    /// without reading source. These assert the report answers them, and that the four whose
    /// systems do not exist yet say so rather than going quiet — a missing section reads as
    /// "nothing to report", which is the one answer that would be a lie.
    /// </summary>
    public class NarrativeInspectorTests
    {
        private static string Report()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = lab.Context(lab.Situation.ThiefId);
            context.SubjectFact = lab.Situation.TheftFactId;
            context.SubjectItem = lab.Situation.ItemId;

            return NarrativeInspector.Explain(lab.World, lab.Vanilla, lab.Actions, context, lab.Situation.Thread);
        }

        [Theory]
        [InlineData("why does this situation exist")]
        [InlineData("origin event")]
        [InlineData("why is this person involved")]
        [InlineData("believes")]
        [InlineData("why is each action available or unavailable")]
        [InlineData("who witnessed what")]
        [InlineData("why a claim spread")]
        [InlineData("what somebody standing here would say out loud")]
        [InlineData("what this person would say if the player asked")]
        public void TheReportAnswersTheQuestionsItsSystemsCanAnswer(string heading)
        {
            Assert.Contains(heading, Report());
        }

        [Theory]
        [InlineData("BQ-093")]
        [InlineData("BQ-051")]
        [InlineData("BQ-087")]
        [InlineData("BQ-071")]
        public void UnbuiltSystemsSaySoAndNameTheStepTheyArriveAt(string step)
        {
            Assert.Contains(step, Report());
        }

        /// <summary>"What check runs?" — every offered verb names its check or says it rolls none.</summary>
        [Fact]
        public void EveryOptionNamesTheCheckItWouldRoll()
        {
            string report = Report();

            Assert.Contains("proc_interrogation", report);
            Assert.Contains("proc_pickpocket", report);
            Assert.Contains("no check", report);
        }

        /// <summary>"Why is an action unavailable?" — the reason, not just the absence.</summary>
        [Fact]
        public void RejectedOptionsCarryTheirReason()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = lab.Context(lab.Situation.ThiefId);

            string report = NarrativeInspector.DescribeOptions(lab.Actions, context);

            Assert.Contains("[ ]", report);
            Assert.Contains("-", report);
        }

        [Fact]
        public void TheReportSurvivesHavingNoSituationInFrontOfThePlayer()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = lab.Context(lab.Situation.ThiefId);

            string report = NarrativeInspector.Explain(lab.World, lab.Vanilla, lab.Actions, context, null);

            Assert.Contains("no thread in front of the player", report);
        }

        /// <summary>
        /// "Which event caused it?" answered `none recorded` in the first live run, because the
        /// situation created the theft as a fact and never as something that happened.
        /// </summary>
        [Fact]
        public void TheThreadNamesTheEventItGrewOutOf()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.False(lab.Situation.Thread.OriginEventId.IsNone);

            string report = Report();
            Assert.Contains("origin event: ", report);
            Assert.DoesNotContain("origin event: none recorded", report);
            Assert.Contains("Theft", report);
        }

        /// <summary>The founding act is in the ledger, with the witness who saw it.</summary>
        [Fact]
        public void TheTheftItselfIsInTheHistory()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            string history = NarrativeInspector.DescribeHistory(lab.World);

            Assert.Contains("Theft", history);
            Assert.Contains("seen by " + lab.World.Registry.NameOf(lab.Situation.WitnessId), history);
        }

        /// <summary>
        /// "Who witnessed it?" is not answered by a number. The history used to print the count,
        /// which tells a reader that somebody saw it and nothing they can act on.
        /// </summary>
        [Fact]
        public void WitnessesAreNamedRatherThanCounted()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Record(
                Events.WorldEventType.Theft,
                lab.Situation.ThiefId,
                lab.Situation.VictimId,
                lab.Vanilla.Now,
                witnesses: new[] { lab.Situation.WitnessId });

            string history = NarrativeInspector.DescribeHistory(lab.World);
            string witnessName = lab.World.Registry.NameOf(lab.Situation.WitnessId);

            Assert.Contains("seen by " + witnessName, history);
            Assert.DoesNotContain("seen by 1)", history);
        }
    }
}
