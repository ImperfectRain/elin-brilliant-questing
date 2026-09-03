using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Lab.Playground;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// The conversation playground, held to what it claims.
    ///
    /// Two kinds of assertion live here and they are kept apart on purpose. The first is about the
    /// laboratory: that a scenario is discoverable, that a command line resolves, that a run is
    /// reproducible, that a report writes nothing. The second is about what the playground is for -
    /// that two states really do produce different answers, that wording never changes meaning,
    /// that a withheld callback never reaches words, that a promise becomes durable exactly once.
    /// Those are Core's guarantees, and the tests here prove the playground <em>exercises</em> them
    /// rather than restating <c>SemanticConversationIntegrationTests</c>: what would fail is a
    /// playground that had quietly stopped calling production, not a Core regression.
    /// </summary>
    public class PlaygroundScenarioTests
    {
        private const ulong Seed = 15UL;

        // -- discovery and dispatch ------------------------------------------------------------

        [Theory]
        [InlineData("playground")]
        [InlineData("playground-contrast")]
        [InlineData("playground-systems")]
        public void EveryPlaygroundScenarioIsRegisteredAndDescribable(string id)
        {
            LabCatalog catalog = LabCatalog.Default();
            LabScenario scenario = catalog.Find(id);

            Assert.NotNull(scenario);
            Assert.Equal(id, scenario.Id);
            Assert.NotEqual(string.Empty, scenario.Summary);

            StringWriter output = new StringWriter();
            Assert.Equal(LabExit.Success, LabCommandLine.Execute(new[] { "describe", id }, output, new StringWriter()));
            Assert.Contains(id, output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheRunVerbResolvesThePlaygroundWithItsSeedAndOptions()
        {
            LabInvocation invocation = LabCommandLine.Resolve(
                LabCatalog.Default(), new[] { "run", "playground", "--preset", "hostile-witness", "--seed", "9" });

            Assert.True(invocation.IsValid, invocation.Error);
            Assert.Equal(LabCommand.Run, invocation.Command);
            Assert.Equal("playground", invocation.Scenario.Id);
            Assert.Equal(9UL, invocation.Seed);
        }

        [Fact]
        public void AMistypedPresetIsAUsageErrorRatherThanAQuieterRun()
        {
            StringWriter error = new StringWriter();
            int status = LabCommandLine.Execute(
                new[] { "run", "playground", "--preset", "no-such-preset" }, new StringWriter(), error);

            Assert.Equal(LabExit.UsageError, status);
            Assert.Contains("no-such-preset", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnUndeclaredOptionIsRejectedBeforeAnythingRuns()
        {
            StringWriter error = new StringWriter();
            int status = LabCommandLine.Execute(
                new[] { "run", "playground", "--strategy", "refuse" }, new StringWriter(), error);

            Assert.Equal(LabExit.UsageError, status);
            Assert.Contains("--strategy", error.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// There is no option that names an outcome. The playground's whole claim is that the
        /// answer is derived, and a command line that could ask for a refusal would make every run
        /// a tautology - so the absence is asserted rather than left to reviewer discipline.
        /// </summary>
        [Fact]
        public void NoOptionNamesAnOutcome()
        {
            string[] outcomes = { "strategy", "depth", "tactic", "line", "text", "refuse", "lie", "disclose" };
            foreach (LabScenario scenario in LabCatalog.Default().Scenarios)
            {
                if (!scenario.Id.StartsWith("playground", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (LabOption option in scenario.Options)
                {
                    Assert.DoesNotContain(option.Name, outcomes);
                }
            }
        }

        [Fact]
        public void EveryPresetIsUniquelyNamedAndRunnableAsDeclared()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlaygroundPreset preset in PlaygroundPresets.Default().All)
            {
                Assert.True(ids.Add(preset.Id), "duplicate preset id " + preset.Id);
                Assert.NotEqual(string.Empty, preset.Summary);
                Assert.Contains(preset.Speaker, PlaygroundRoles.All);
                Assert.Contains(preset.Listener, PlaygroundRoles.All);
                Assert.NotNull(PlaygroundVoices.Find(preset.Voice));
                Assert.InRange(preset.Turns, 1, 3);

                PlaygroundRun run = Run(preset.Id);
                Assert.NotEmpty(run.Exchange.Turns);
                Assert.Equal(preset.Turns, run.Exchange.Turns.Count);
            }
        }

        // -- determinism -----------------------------------------------------------------------

        [Fact]
        public void TheSameSeedAndPresetPrintTheSameReport()
        {
            string first = Report("run", "playground", "--preset", "settled-history", "--seed", "15");
            string second = Report("run", "playground", "--preset", "settled-history", "--seed", "15");

            Assert.Equal(first, second);
            Assert.NotEqual(string.Empty, first);
        }

        [Fact]
        public void TwoSeparateWorldsAgreeOnTheWholeSemanticResult()
        {
            PlaygroundRun first = Run("neutral-witness");
            PlaygroundRun second = Run("neutral-witness");

            Assert.Equal(first.Exchange.Turns.Count, second.Exchange.Turns.Count);
            for (int i = 0; i < first.Exchange.Turns.Count; i++)
            {
                PlaygroundTurn a = first.Exchange.Turns[i];
                PlaygroundTurn b = second.Exchange.Turns[i];

                Assert.Equal(a.Decision.Strategy, b.Decision.Strategy);
                Assert.Equal(a.Decision.Depth, b.Decision.Depth);
                Assert.Equal(a.Decision.Tactic, b.Decision.Tactic);
                Assert.Equal(a.Reply.Signature, b.Reply.Signature);
                Assert.Equal(a.Line.Text, b.Line.Text);
            }
        }

        // -- two states, two answers -----------------------------------------------------------

        /// <summary>
        /// The pair the contrast scenario defaults to. Identical pressures over a speaker who lies
        /// and one who does not: the strategy is the same refusal, and the tactic, the act and what
        /// the world records are not.
        /// </summary>
        [Fact]
        public void IdenticalPressuresOverDifferentCharactersProduceDifferentTactics()
        {
            PlaygroundTurn liar = Run("loyal-liar").Exchange.Turns[0];
            PlaygroundTurn refuser = Run("principled-refuser").Exchange.Turns[0];

            Assert.Equal(DisclosureStrategy.Refuse, liar.Decision.Strategy);
            Assert.Equal(DisclosureStrategy.Refuse, refuser.Decision.Strategy);

            Assert.Equal(DisclosureTactic.Falsify, liar.Decision.Tactic);
            Assert.Equal(DisclosureTactic.Decline, refuser.Decision.Tactic);

            Assert.Equal(SpeechActType.Deny, liar.Reply.Type);
            Assert.Equal(SpeechActType.Refuse, refuser.Reply.Type);

            // The falsehood is durable and the refusal is not - which is the difference the two
            // states are supposed to make to the world rather than only to the wording.
            Assert.NotNull(liar.RecordedDeception);
            Assert.Null(refuser.RecordedDeception);
        }

        /// <summary>The tie buys depth, and depth is the axis rather than willingness.</summary>
        [Fact]
        public void ADeeperTieBuysMoreOfTheSameClaim()
        {
            PlaygroundTurn stranger = Run("neutral-witness").Exchange.Turns[0];
            PlaygroundTurn confidant = Run("trusted-confidant").Exchange.Turns[0];

            Assert.True(confidant.Decision.Depth > stranger.Decision.Depth);
            Assert.Equal(stranger.Decision.KnownDepth, confidant.Decision.KnownDepth);
            Assert.Equal(SpeechActType.Answer, stranger.Reply.Type);
            Assert.Equal(SpeechActType.Answer, confidant.Reply.Type);
        }

        /// <summary>
        /// BQ-077's second line, which is the case a pressure model alone cannot produce: the
        /// weighing came out in favour of speaking and a ruling changed the answer afterwards.
        /// </summary>
        [Fact]
        public void APersonalLineCanRefuseWhatTheWeighingWasWillingToSay()
        {
            PlaygroundTurn turn = Run("kin-line").Exchange.Turns[0];

            Assert.Equal(DisclosureStrategy.Refuse, turn.Decision.Strategy);
            Assert.True(turn.Decision.Balance > Disclosure.HedgeAt,
                "the weighing itself was in favour of speaking, so the line is what refused");

            bool kin = false;
            foreach (ProhibitionRuling ruling in turn.Decision.Prohibitions)
            {
                kin |= ruling.Kind == PersonalProhibition.NeverSpeaksBadlyOfFamily && ruling.Forbids;
            }

            Assert.True(kin, "the kin line should be the ruling that holds");
        }

        /// <summary>
        /// A different route into the claim, over an ordinary relationship. The point is that the
        /// answer moved because the belief did, so the belief is asserted alongside it.
        /// </summary>
        [Fact]
        public void AThinlyHeldSecondHandBeliefIsNotAnsweredLikeAWitnessed()
        {
            PlaygroundRun run = Run("hearsay-victim");
            PlaygroundTurn turn = run.Exchange.Turns[0];

            Assert.True(run.Stage.World.Knowledge.TryGetBelief(
                run.Speaker, run.Stage.SubjectFactId, out KnowledgeRecord belief));
            Assert.Equal(KnowledgeSource.Hearsay, belief.Source);
            Assert.True(belief.Confidence < Disclosure.ConvictionToStandBehind);
            Assert.NotEqual(DisclosureStrategy.Disclose, turn.Decision.Strategy);
        }

        // -- multi-turn conversation state -------------------------------------------------------

        [Fact]
        public void ASecondExchangeSeesTheFirst()
        {
            PlaygroundRun run = Run("neutral-witness", turns: 2);
            PlaygroundTurn first = run.Exchange.Turns[0];
            PlaygroundTurn second = run.Exchange.Turns[1];

            Assert.False(first.AlreadyAsked);
            Assert.True(second.AlreadyAsked, "conversation state should recognise the repeated question");

            Assert.Equal(2, first.ActsNoted);
            Assert.Equal(4, second.ActsNoted);
            Assert.Equal(0, second.Unanswered);
            Assert.Equal(4, run.Exchange.Conversation.Acts.Count);
        }

        /// <summary>
        /// Repetition control had the earlier exchange's material and used it. The history object
        /// being the same one is the structural half; the wording differing is the observable half,
        /// pinned at a fixed seed over the shipped fragment library.
        /// </summary>
        [Fact]
        public void RepetitionHistoryCarriesAcrossTheTurnsAndNarrowsTheChoice()
        {
            PlaygroundRun run = Run("neutral-witness", turns: 2);
            PlaygroundTurn first = run.Exchange.Turns[0];
            PlaygroundTurn second = run.Exchange.Turns[1];

            Assert.Same(first.Request.History, second.Request.History);
            Assert.Same(run.Exchange.History, first.Request.History);

            Assert.Equal(first.Reply.Signature, second.Reply.Signature);
            Assert.NotEqual(first.Line.Core, second.Line.Core);
            Assert.NotEqual(first.Line.Text, second.Line.Text);
        }

        // -- disclosure and callback boundaries ---------------------------------------------------

        /// <summary>
        /// The gate this playground must never open. Across every preset, anything that reached
        /// wording was cleared, for this listener, and belongs to this speaker.
        /// </summary>
        [Fact]
        public void NothingUnclearedEverReachesWording()
        {
            foreach (PlaygroundPreset preset in PlaygroundPresets.Default().All)
            {
                PlaygroundRun run = Run(preset.Id);
                foreach (PlaygroundTurn turn in run.Exchange.Turns)
                {
                    if (turn.Request == null)
                    {
                        continue;
                    }

                    Assert.Equal(string.Empty, turn.Request.WhyNot());

                    CallbackPermit permit = turn.Request.Callback;
                    if (permit == null)
                    {
                        continue;
                    }

                    Assert.True(permit.Allowed, preset.Id + " worded a permit that was withheld");
                    Assert.Equal(run.Listener, permit.Listener);
                    Assert.Equal(run.Speaker, permit.Hook.Recaller);
                }
            }
        }

        /// <summary>
        /// The same history, twice, with only the state around it changed: once raised and worded,
        /// once withheld with the claim that withheld it named and nothing of it in the line.
        /// </summary>
        [Fact]
        public void AWithheldCallbackIsNamedAndNeverSpoken()
        {
            PlaygroundTurn open = Run("settled-history").Exchange.Turns[0];
            PlaygroundTurn shut = Run("guarded-history").Exchange.Turns[0];

            Assert.NotNull(open.Callback);
            Assert.True(open.Callback.Allowed);
            Assert.Contains("call.history", string.Join(",", open.Line.Fragments), StringComparison.Ordinal);

            Assert.Null(shut.Callback);
            Assert.NotNull(shut.WithheldCallback);
            Assert.False(shut.WithheldCallback.Allowed);
            Assert.Equal(shut.Decision.FactId, shut.WithheldCallback.Withheld);
            Assert.Null(shut.Request.Callback);
            Assert.DoesNotContain("call.history", string.Join(",", shut.Line.Fragments), StringComparison.Ordinal);

            // Both speakers may recall it; only one would say it. Recall permission is not telling
            // permission, and the withheld run proves the two gates are separate here too.
            Assert.NotNull(shut.WithheldCallback.Hook);
            Assert.Equal(CallbackRoute.Involved, shut.WithheldCallback.Hook.Route);
        }

        /// <summary>
        /// Nothing unbelieved is disclosed, and "would not" is kept apart from "could not": the
        /// victim holds no belief about the theft, so no act is composed and no line exists to
        /// mistake for a refusal.
        /// </summary>
        [Fact]
        public void ASpeakerWithNoBeliefProducesNoActAndNoLine()
        {
            PlaygroundRun run = Run("neutral-witness", speaker: PlaygroundRoles.Victim, turns: 1);
            PlaygroundTurn turn = run.Exchange.Turns[0];

            Assert.False(run.Stage.World.Knowledge.Knows(run.Speaker, run.Stage.SubjectFactId));
            Assert.Equal(DisclosureStrategy.NothingToDisclose, turn.Decision.Strategy);
            Assert.Null(turn.Reply);
            Assert.Null(turn.Line);
            Assert.NotEmpty(turn.Notes);
        }

        // -- wording changes nothing --------------------------------------------------------------

        [Fact]
        public void EveryLineMeansExactlyWhatTheActMeant()
        {
            foreach (PlaygroundPreset preset in PlaygroundPresets.Default().All)
            {
                PlaygroundRun run = Run(preset.Id);
                foreach (PlaygroundTurn turn in run.Exchange.Turns)
                {
                    if (turn.Line == null)
                    {
                        continue;
                    }

                    Assert.Equal(turn.Reply.Signature, turn.Line.Meaning);
                }
            }
        }

        /// <summary>
        /// Four voices over one state. The words may differ or the pool may narrow to nothing;
        /// what may never differ is the decision or what the act meant.
        /// </summary>
        [Fact]
        public void AVoiceChangesTheWordingAndNeverTheMeaning()
        {
            PlaygroundTurn baseline = Run("neutral-witness", turns: 1).Exchange.Turns[0];

            foreach (string voice in PlaygroundVoices.All)
            {
                PlaygroundTurn turn = Run("neutral-witness", voice: voice, turns: 1).Exchange.Turns[0];

                Assert.Equal(baseline.Decision.Strategy, turn.Decision.Strategy);
                Assert.Equal(baseline.Decision.Depth, turn.Decision.Depth);
                Assert.Equal(baseline.Decision.Tactic, turn.Decision.Tactic);
                Assert.Equal(baseline.Reply.Signature, turn.Reply.Signature);

                if (turn.Line != null && turn.Line.Rendered)
                {
                    Assert.Equal(baseline.Reply.Signature, turn.Line.Meaning);
                }
            }
        }

        // -- promises ------------------------------------------------------------------------------

        [Fact]
        public void APromiseBecomesDurableOnceAndOnlyWhenAsked()
        {
            PlaygroundRun committed = Run("promise-exchange", turns: 3);
            PlaygroundTurn turn = committed.Exchange.Turns[2];

            Assert.Equal(SpeechActType.Promise, turn.Reply.Type);
            Assert.NotNull(turn.Committed);
            Assert.Equal(WorldEventType.PromiseMade, turn.Committed.Type);
            Assert.Equal(committed.Speaker, turn.Committed.Actor);
            Assert.Equal(committed.Listener, turn.Committed.Target);

            Assert.Equal(1, Count(committed, SocialObligationKind.Promise));
            Assert.Equal(1, Promises(committed));

            // The two refusals the turn exercised: the same promise again, and one this
            // conversation never heard. Both are writes that did not happen.
            Assert.Contains(turn.Notes, note => note.Contains("minted nothing", StringComparison.Ordinal));
            Assert.Contains(turn.Notes, note => note.Contains("never heard", StringComparison.Ordinal));
            Assert.DoesNotContain(turn.Notes, note => note.StartsWith("WARNING", StringComparison.Ordinal));
        }

        [Fact]
        public void APromiseNobodyPromotedLeavesTheLedgerAlone()
        {
            PlaygroundRun run = Run("promise-exchange", turns: 3, commit: false);
            PlaygroundTurn turn = run.Exchange.Turns[2];

            Assert.Equal(SpeechActType.Promise, turn.Reply.Type);
            Assert.Contains(turn.Reply, run.Exchange.Conversation.Acts);
            Assert.Null(turn.Committed);
            Assert.Equal(0, Count(run, SocialObligationKind.Promise));
            Assert.Equal(0, Promises(run));
        }

        /// <summary>
        /// The shipped library has no words for a promise, and the playground says so rather than
        /// assembling a line out of openers and closers. An honest gap, reported as one.
        /// </summary>
        [Fact]
        public void APromiseIsReportedAsUnwordedRatherThanWordedVaguely()
        {
            PlaygroundTurn turn = Run("promise-exchange", turns: 3).Exchange.Turns[2];

            Assert.NotNull(turn.Line);
            Assert.False(turn.Line.Rendered);
            Assert.Equal(string.Empty, turn.Line.Text);
            Assert.NotEqual(string.Empty, turn.Line.Refusal);
            Assert.Equal(turn.Reply.Signature, turn.Line.Meaning);
        }

        // -- inspection writes nothing ---------------------------------------------------------------

        /// <summary>
        /// Reporting is a read. Running every reporter twice over one finished run produces the
        /// identical text and moves neither the ledger nor the obligations, which is what makes an
        /// inspection safe to repeat and a report safe to diff.
        /// </summary>
        [Fact]
        public void ReplayingTheReportChangesNothingDurable()
        {
            PlaygroundRun run = Run("promise-exchange", turns: 3);

            int events = run.Stage.World.Ledger.Count;
            int obligations = run.Stage.World.Obligations.Records.Count;

            StringWriter first = new StringWriter();
            StringWriter second = new StringWriter();
            PlaygroundReporters.Default().Write(first, run);
            PlaygroundReporters.Default().Write(second, run);

            Assert.Equal(first.ToString(), second.ToString());
            Assert.NotEqual(string.Empty, first.ToString());
            Assert.Equal(events, run.Stage.World.Ledger.Count);
            Assert.Equal(obligations, run.Stage.World.Obligations.Records.Count);
        }

        // -- headless honesty --------------------------------------------------------------------------

        [Fact]
        public void SystemsThatNeedALiveGameAreNamedRatherThanSimulated()
        {
            IReadOnlyList<PlaygroundSystem> runtime =
                PlaygroundAvailability.WithSupport(PlaygroundSupport.RuntimeRequired);

            Assert.NotEmpty(runtime);

            string report = Report("run", "playground-systems");
            Assert.Contains("PLUGIN ONLY", report, StringComparison.Ordinal);

            foreach (PlaygroundSystem system in runtime)
            {
                Assert.Contains(system.Name, report, StringComparison.Ordinal);
                Assert.NotEqual(string.Empty, system.Note);
            }
        }

        [Fact]
        public void EverySystemIsClaimedExactlyOnceAndExplained()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlaygroundSystem system in PlaygroundAvailability.All)
            {
                Assert.True(names.Add(system.Name + "|" + system.Step),
                    "duplicate system entry " + system.Name);
                Assert.NotEqual(string.Empty, system.Note);
                Assert.NotEqual(string.Empty, system.Step);
            }

            // The two the laboratory supplies for want of a production authority are named, because
            // an unlabelled laboratory choice is exactly the dishonesty this table exists against.
            IReadOnlyList<PlaygroundSystem> authored =
                PlaygroundAvailability.WithSupport(PlaygroundSupport.LaboratoryAuthored);
            Assert.NotEmpty(authored);
            Assert.Contains(authored, system => system.Name.Contains("voice", StringComparison.Ordinal));
        }

        /// <summary>
        /// The voice is the laboratory's, and the run says so in its own output rather than only in
        /// the systems table - a reader of one transcript should not have to know to go and look.
        /// </summary>
        [Fact]
        public void TheRunItselfLabelsTheVoiceAsLaboratoryAuthored()
        {
            Assert.Contains("laboratory-authored", Report("run", "playground", "--turns", "1"),
                StringComparison.Ordinal);
        }

        // -- overrides ------------------------------------------------------------------------------------

        [Fact]
        public void AnOverriddenTieChangesTheStateAndIsReportedAsApplied()
        {
            PlaygroundRun run = Run("neutral-witness", turns: 1, configure: options =>
            {
                options.Tie = RelationKind.Enemy;
                options.Sentiment = -90;
            });

            RelationshipEdge edge = run.Stage.World.Relationships.Find(run.Speaker, run.Listener);
            Assert.NotNull(edge);
            Assert.Equal(RelationKind.Enemy, edge.Kind);
            Assert.Equal(-90, edge.Sentiment);
            Assert.Contains(run.Overrides, line => line.Contains("Enemy", StringComparison.Ordinal));
        }

        /// <summary>
        /// The graph strengthens a belief somebody already holds rather than re-sourcing it, and
        /// the playground reports what the graph did instead of what was asked for.
        /// </summary>
        [Fact]
        public void AKnowledgeRouteAskedOfSomebodyWhoAlreadyBelievesIsReportedAsDeclined()
        {
            PlaygroundRun run = Run("neutral-witness", turns: 1, configure: options =>
            {
                options.Knowledge = KnowledgeSource.Hearsay;
                options.Confidence = 0.3;
            });

            Assert.True(run.Stage.World.Knowledge.TryGetBelief(
                run.Speaker, run.Stage.SubjectFactId, out KnowledgeRecord belief));
            Assert.Equal(KnowledgeSource.Witnessed, belief.Source);
            Assert.Contains(run.Overrides, line => line.Contains("already believed it", StringComparison.Ordinal));
        }

        // -- scaffolding ------------------------------------------------------------------------------------

        private static PlaygroundRun Run(
            string preset,
            string speaker = null,
            string voice = null,
            int? turns = null,
            bool commit = true,
            Action<PlaygroundOptions> configure = null)
        {
            PlaygroundOptions options = new PlaygroundOptions
            {
                Seed = Seed,
                Preset = preset,
                Speaker = speaker,
                Voice = voice,
                Turns = turns,
                Commit = commit
            };

            configure?.Invoke(options);
            return PlaygroundRun.Begin(options, PlaygroundPresets.Default());
        }

        private static string Report(params string[] args)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            Assert.Equal(LabExit.Success, LabCommandLine.Execute(args, output, error));
            return output.ToString();
        }

        private static int Count(PlaygroundRun run, SocialObligationKind kind)
        {
            int found = 0;
            IReadOnlyList<SocialObligation> records = run.Stage.World.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Kind == kind)
                {
                    found++;
                }
            }

            return found;
        }

        private static int Promises(PlaygroundRun run)
        {
            int found = 0;
            IReadOnlyList<WorldEvent> events = run.Stage.World.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == WorldEventType.PromiseMade)
                {
                    found++;
                }
            }

            return found;
        }
    }
}
