using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Lab.Playground;
using BrilliantQuesting.Lab.Playground.Sweep;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// The sweep, held to what it claims.
    ///
    /// Three kinds of assertion, kept apart. About the laboratory: the scenario is discoverable, a
    /// command line resolves, a run is byte-reproducible, no option names an outcome, and a family
    /// that breaks an invariant fails the process. About the instrument: the readings really come
    /// off production objects and the reporters write without running anything. And about what the
    /// instrument is for - that moving one input really does move the answer where the model says
    /// it should, and really does not where it says it should not.
    ///
    /// Where a property would pass for the wrong reason, it is mutation-checked: the sweep's own
    /// invariant is run against a deliberately broken row and asserted to complain. A test that only
    /// ever sees the happy path proves the code ran, not that it would object.
    /// </summary>
    public class PlaygroundSweepTests
    {
        private const ulong Seed = 15UL;

        // -- discovery and dispatch ------------------------------------------------------------

        [Fact]
        public void TheSweepIsRegisteredAndDescribable()
        {
            LabScenario scenario = LabCatalog.Default().Find("playground-sweep");

            Assert.NotNull(scenario);
            Assert.Equal("playground-sweep", scenario.Id);

            StringWriter output = new StringWriter();
            Assert.Equal(
                LabExit.Success,
                LabCommandLine.Execute(new[] { "describe", "playground-sweep" }, output, new StringWriter()));
            Assert.Contains("--axis", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheRunVerbResolvesTheSweepWithItsSeedAndAxis()
        {
            LabInvocation invocation = LabCommandLine.Resolve(
                LabCatalog.Default(), new[] { "run", "playground-sweep", "--axis", "voice", "--seed", "9" });

            Assert.True(invocation.IsValid, invocation.Error);
            Assert.Equal(LabCommand.Run, invocation.Command);
            Assert.Equal("playground-sweep", invocation.Scenario.Id);
            Assert.Equal(9UL, invocation.Seed);
            Assert.Equal("voice", invocation.Arguments.String("axis", null));
        }

        [Fact]
        public void EveryAxisIsListedAndRunnable()
        {
            string listing = Report("run", "playground-sweep", "--list-axes");

            foreach (PlaygroundSweepAxis axis in PlaygroundSweepAxes.Default().All)
            {
                Assert.Contains(axis.Id, listing, StringComparison.Ordinal);
                Assert.NotEqual(string.Empty, axis.Summary);

                string report = Report("run", "playground-sweep", "--axis", axis.Id);
                Assert.Contains("SWEEP: " + axis.Id.ToUpperInvariant(), report, StringComparison.Ordinal);
                Assert.Contains("summary", report, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void OneFamilyCanBeRunWithoutTheRest()
        {
            string one = Report("run", "playground-sweep", "--axis", "emotion");

            Assert.Contains("SWEEP: EMOTION", one, StringComparison.Ordinal);
            Assert.DoesNotContain("SWEEP: VOICE", one, StringComparison.Ordinal);
        }

        [Fact]
        public void AMistypedAxisIsAUsageErrorRatherThanAQuieterRun()
        {
            StringWriter error = new StringWriter();
            int status = LabCommandLine.Execute(
                new[] { "run", "playground-sweep", "--axis", "no-such-axis" }, new StringWriter(), error);

            Assert.Equal(LabExit.UsageError, status);
            Assert.Contains("no-such-axis", error.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// The sweep measures outcomes, so no option may name one. Asserted rather than left to
        /// review, because a single option called <c>--tactic</c> would turn every row into a
        /// tautology and nothing else in the design would notice.
        /// </summary>
        [Fact]
        public void NoSweepOptionNamesADerivedOutcome()
        {
            string[] outcomes =
            {
                "strategy", "depth", "tactic", "act", "speech-act", "stance", "line", "text", "wording",
                "fragment", "callback", "permit", "refuse", "lie", "deceive", "disclose", "promise", "meaning"
            };

            LabScenario sweep = LabCatalog.Default().Find("playground-sweep");
            foreach (LabOption option in sweep.Options)
            {
                Assert.DoesNotContain(option.Name, outcomes);
            }
        }

        /// <summary>
        /// The same rule one level down: an input may name only state. A factory called
        /// "make them refuse" would put the answer into the input column.
        /// </summary>
        [Fact]
        public void NoInputADeclaredFamilyUsesNamesADerivedOutcome()
        {
            string[] outcomes = { "strategy", "depth", "tactic", "disclose", "refuse", "deflect", "hedge", "falsify" };

            foreach (PlaygroundSweepAxis axis in PlaygroundSweepAxes.Default().All)
            {
                foreach (PlaygroundSweepRow row in axis.Rows(Seed))
                {
                    foreach (string change in row.Changed)
                    {
                        foreach (string outcome in outcomes)
                        {
                            Assert.False(
                                change.IndexOf(outcome, StringComparison.OrdinalIgnoreCase) >= 0,
                                axis.Id + " has an input naming an outcome: " + change);
                        }
                    }
                }
            }
        }

        // -- determinism -----------------------------------------------------------------------

        [Theory]
        [InlineData("relationship")]
        [InlineData("callbacks")]
        [InlineData("repetition")]
        [InlineData("seeds")]
        public void TheSameSweepAndSeedPrintTheSameReport(string axis)
        {
            string first = Report("run", "playground-sweep", "--axis", axis, "--seed", "15");
            string second = Report("run", "playground-sweep", "--axis", axis, "--seed", "15");

            Assert.Equal(first, second);
            Assert.NotEqual(string.Empty, first);
        }

        [Fact]
        public void TheJsonFormIsTheSameRowsAndIsAlsoReproducible()
        {
            string first = Report("run", "playground-sweep", "--axis", "voice", "--json");
            string second = Report("run", "playground-sweep", "--axis", "voice", "--json");

            Assert.Equal(first, second);
            Assert.Contains("\"axis\": \"voice\"", first, StringComparison.Ordinal);
            Assert.Contains("\"meaning\":", first, StringComparison.Ordinal);
        }

        // -- what the instrument is for --------------------------------------------------------

        /// <summary>
        /// The relationship family exists to show a transition. A family in which every row reached
        /// the same answer would look like a pass and prove nothing, so the assertion is that the
        /// rungs really move - not where the thresholds are.
        /// </summary>
        [Fact]
        public void ChangingOnlyTheTieMovesTheStrategyUpTheLadder()
        {
            PlaygroundSweepResult result = Evaluate("relationship");

            DisclosureStrategy hostile = Strategy(result, "hostile");
            DisclosureStrategy distant = Strategy(result, "distant");
            DisclosureStrategy trusted = Strategy(result, "trusted");

            Assert.True(hostile < distant, "a hostile tie was not less forthcoming than a distant one");
            Assert.True(distant < trusted, "a distant tie was not less forthcoming than a trusted one");

            // The only thing that moved is the graph edge, so the claim and the belief behind it
            // have to be identical on both ends of the transition.
            Assert.Equal(
                Row(result, "hostile").Run.Stage.SubjectFactId,
                Row(result, "trusted").Run.Stage.SubjectFactId);
        }

        [Fact]
        public void DepthDeepensWithStandingAndNeverPassesWhatIsKnown()
        {
            PlaygroundSweepResult result = Evaluate("relationship");

            DisclosureDecision indifferent = Row(result, "indifferent").Turn.Decision;
            DisclosureDecision trusted = Row(result, "trusted").Turn.Decision;

            Assert.True(trusted.Depth > indifferent.Depth, "a trusted tie bought no extra depth");
            Assert.True(trusted.Depth <= trusted.KnownDepth);
            Assert.Equal(DisclosureLimit.Standing, indifferent.Limit);
        }

        [Fact]
        public void NoBeliefStaysNoActHoweverPlausibleTheSpeakerIs()
        {
            PlaygroundSweepResult result = Evaluate("knowledge");
            PlaygroundSweepRow row = Row(result, "no belief");

            Assert.False(row.Run.Stage.World.Knowledge.Knows(row.Run.Speaker, row.Run.Stage.SubjectFactId));
            Assert.Equal(DisclosureStrategy.NothingToDisclose, row.Turn.Decision.Strategy);
            Assert.Null(row.Turn.Reply);
            Assert.Null(row.Turn.Line);

            // The victim's identity is read as trade, so BQ-145 does make it plausible she would
            // know about this - which is exactly the reading that must not become knowledge.
            IdentityAffordances identity = IdentityAffordances.Of(
                row.Run.Stage.Npc(row.Run.Speaker), row.Run.Stage.Vanilla);
            Assert.NotEmpty(OccupationalVocabulary.RequestedVocabulary(identity));
        }

        [Fact]
        public void ConfidenceAndProvenanceChangeTheCommitmentTheClaimIsPutForwardWith()
        {
            PlaygroundSweepResult result = Evaluate("knowledge");

            Assert.Equal(DisclosureStrategy.Deflect, Strategy(result, "faint inference"));
            Assert.Equal(DisclosureStrategy.Hedge, Strategy(result, "confident hearsay"));
            Assert.Equal(DisclosureStrategy.Disclose, Strategy(result, "saw it, can prove it"));

            // The ladder is monotone in conviction: nothing about a stronger belief ever makes a
            // speaker less forthcoming, which is the property rather than the three rungs above.
            Assert.True(Strategy(result, "thin hearsay") <= Strategy(result, "confident hearsay"));
            Assert.True(Strategy(result, "confident hearsay") <= Strategy(result, "saw it, can prove it"));
        }

        /// <summary>
        /// The whole of BQ-077's difference from a personality weight, in one comparison: the same
        /// speaker, at the same honesty, with and without the line.
        /// </summary>
        [Fact]
        public void ALineRemovesAMoveWithoutRewritingTheScoreItWasWeighedOn()
        {
            PlaygroundSweepResult result = Evaluate("negative-space");

            DisclosureDecision without = Row(result, "lying: no line").Turn.Decision;
            DisclosureDecision with = Row(result, "lying: line holds").Turn.Decision;

            Assert.Equal(DisclosureTactic.Falsify, without.Tactic);
            Assert.NotEqual(DisclosureTactic.Falsify, with.Tactic);
            Assert.Equal(without.Balance, with.Balance);
            Assert.Contains(with.Prohibitions, ruling =>
                ruling.Kind == PersonalProhibition.NeverLiesDirectly && ruling.Forbids);
        }

        [Fact]
        public void ALineThatForbidsNoMoveHereChangesNothingAtAll()
        {
            PlaygroundSweepResult result = Evaluate("negative-space");

            Assert.Contains(result.NoEffect, row => row.Label == "a line about begging");
            Assert.Contains(result.NoEffect, row => row.Label == "a line about authority");
        }

        /// <summary>
        /// The strongest veracity-blindness proof the model permits, and the family says so itself:
        /// Disclosure composes a denial only from Falsify, so an honest denier has no path here.
        /// What can be moved is the truth of the claim, which the veracity report reads and nothing
        /// downstream of it does.
        /// </summary>
        [Fact]
        public void MovingTheTruthOfTheClaimMovesTheReportAndNotTheWords()
        {
            PlaygroundSweepResult result = Evaluate("honesty");

            PlaygroundSweepRow believed = Row(result, "honesty 0.10");
            PlaygroundSweepRow untrue = Row(result, "0.10, and the claim is untrue");

            Assert.Equal(SpeechActType.Deny, believed.Turn.Reply.Type);
            Assert.Equal(SpeechActType.Deny, untrue.Turn.Reply.Type);
            Assert.NotEqual(
                Deception.Assess(believed.Run.Stage.World, believed.Turn.Reply).Accuracy,
                Deception.Assess(untrue.Run.Stage.World, untrue.Turn.Reply).Accuracy);

            Assert.Equal(believed.EligibleBySlot, untrue.EligibleBySlot);
            Assert.Equal(believed.Line, untrue.Line);
            Assert.Equal(believed.Meaning, untrue.Meaning);
        }

        [Fact]
        public void HonestyCarriesASpeakerOffFalsifyingWithoutAnyLineAtAll()
        {
            PlaygroundSweepResult result = Evaluate("honesty");

            Assert.Equal(DisclosureTactic.Falsify, Row(result, "honesty 0.10").Turn.Decision.Tactic);
            Assert.NotEqual(DisclosureTactic.Falsify, Row(result, "honesty 0.95").Turn.Decision.Tactic);
            Assert.NotEqual(DisclosureTactic.Falsify, Row(result, "0.10, will not lie").Turn.Decision.Tactic);
        }

        [Fact]
        public void AWithheldMemoryNeverReachesTheWords()
        {
            PlaygroundSweepResult result = Evaluate("callbacks");

            PlaygroundSweepRow cleared = Row(result, "it was done to them");
            PlaygroundSweepRow guarded = Row(result, "done to them, guarded");

            // The claim is about permission, not about the coin the optional callback slot is
            // drawn against: a cleared permit reaches the request and may be spoken, and whether
            // this particular seed spoke it is the realizer's business.
            Assert.NotNull(cleared.Turn.Callback);
            Assert.True(cleared.Turn.Callback.Allowed);
            Assert.NotNull(cleared.Turn.Request.Callback);

            Assert.Null(guarded.Turn.Callback);
            Assert.NotNull(guarded.Turn.WithheldCallback);
            Assert.False(guarded.Turn.WithheldCallback.Allowed);
            Assert.Null(guarded.Turn.Request.Callback);
            if (guarded.Turn.Line != null && guarded.Turn.Line.Rendered)
            {
                Assert.DoesNotContain(
                    guarded.Turn.Line.Fragments, id => id.StartsWith("call.history.", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Remembering is not permission, and permission is not recurrence. Both speakers hold the
        /// identical material; only where it happened differs.
        /// </summary>
        [Fact]
        public void ARecurrenceIsNeverTakenFromTheThreadItHappenedIn()
        {
            PlaygroundSweepResult result = Evaluate("callbacks");

            PlaygroundSweepRow elsewhere = Row(result, "a scandal, elsewhere");
            PlaygroundSweepRow here = Row(result, "a scandal, right here");

            Assert.NotNull(elsewhere.Turn.Callback);
            Assert.NotNull(here.Turn.Callback);
            Assert.NotNull(elsewhere.Turn.Recurrence);
            Assert.Null(here.Turn.Recurrence);
        }

        [Fact]
        public void NoVoiceEverChangesWhatIsMeant()
        {
            PlaygroundSweepResult result = Evaluate("voice");

            Assert.Equal(1, result.DistinctSemantics);
            Assert.True(result.DistinctLines > 1, "every voice produced the same words, so nothing was measured");

            string meaning = Row(result, "neutral").Turn.Line.Meaning;
            foreach (PlaygroundSweepRow row in result.Rows)
            {
                Assert.Equal(meaning, row.Turn.Line.Meaning);
                Assert.Equal(row.Turn.Reply.Signature, row.Turn.Line.Meaning);
            }
        }

        [Fact]
        public void AMoreSpecifiedVoiceNeverWidensTheChoice()
        {
            PlaygroundSweepResult result = Evaluate("voice");
            PlaygroundEligibility free = Row(result, "neutral").Turn.Eligible;

            foreach (PlaygroundSweepRow row in result.Rows)
            {
                if (row.Turn.Request.Tone.Count == 0)
                {
                    continue;
                }

                foreach (FragmentPosition slot in PlaygroundEligibility.Slots)
                {
                    Assert.True(
                        row.Turn.Eligible.CountAt(slot) <= free.CountAt(slot),
                        row.Label + " widened the " + slot + " pool");
                }
            }
        }

        [Fact]
        public void NoIdentityEverChangesWhatIsMeantOrWhatIsDecided()
        {
            PlaygroundSweepResult result = Evaluate("vocabulary");

            Assert.Equal(1, result.DistinctSemantics);

            PlaygroundSweepRow unread = Row(result, "unread");
            foreach (PlaygroundSweepRow row in result.Rows)
            {
                Assert.Equal(unread.Turn.Line.Meaning, row.Turn.Line.Meaning);
                Assert.Equal(unread.Turn.Decision.Strategy, row.Turn.Decision.Strategy);
                Assert.Equal(unread.Turn.Decision.Tactic, row.Turn.Decision.Tactic);
                Assert.Equal(unread.Turn.Decision.Depth, row.Turn.Decision.Depth);
            }
        }

        [Fact]
        public void LivedContextBecomesEligibleOnlyWhereTheAffordanceExists()
        {
            PlaygroundSweepResult result = Evaluate("vocabulary");

            Assert.Empty(Row(result, "unread").Turn.Request.Vocabulary);
            Assert.Empty(Row(result, "race and archetype").Turn.Request.Vocabulary);
            Assert.Empty(Row(result, "unrecognised work").Turn.Request.Vocabulary);

            Assert.Contains(DialogueVocabulary.Trade, Row(result, "trade").Turn.Request.Vocabulary);
            Assert.Contains("mod.refuse.trade", Row(result, "trade").Turn.Eligible.At(FragmentPosition.Modifier));
            Assert.DoesNotContain(
                "mod.refuse.trade", Row(result, "unread").Turn.Eligible.At(FragmentPosition.Modifier));
        }

        /// <summary>
        /// CD §21's degrade, stated as three claims about one run: the required slot always says
        /// something, an optional slot is allowed to go quiet, and the meaning does not move while
        /// either happens.
        /// </summary>
        [Fact]
        public void RepetitionDegradesTheWordingAndNotTheMeaning()
        {
            PlaygroundSweepResult result = Evaluate("repetition");

            Assert.Equal(1, result.DistinctSemantics);
            Assert.True(result.Rows.Count >= 8, "the family did not run enough exchanges to exhaust anything");

            int optionalSlotsAtTheStart = 0;
            int optionalSlotsAtTheEnd = 0;
            Dictionary<string, int> cores = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (PlaygroundSweepRow row in result.Rows)
            {
                Assert.True(row.Turn.Line.Rendered, "exchange " + row.ReadAt + " produced no line");
                Assert.NotEqual(string.Empty, row.Turn.Line.Core);

                cores.TryGetValue(row.Turn.Line.Core, out int seen);
                cores[row.Turn.Line.Core] = seen + 1;

                int optional = row.Turn.Line.Fragments.Count - 1;
                if (row.ReadAt <= 2)
                {
                    optionalSlotsAtTheStart += optional;
                }
                else if (row.ReadAt >= result.Rows.Count - 1)
                {
                    optionalSlotsAtTheEnd += optional;
                }
            }

            Assert.True(
                optionalSlotsAtTheEnd < optionalSlotsAtTheStart,
                "no optional slot ever went quiet, so the degrade was never reached");

            // Nothing here exhausts any more: the library is deep enough that the required slot
            // never has to fall back on a stale core over a sweep this long, which is the point of
            // having authored it. That the fallback still works when a pool genuinely runs dry is
            // `RepetitionControlTests.ExhaustingEveryValidCoreStillRendersCorrectlyRatherThanRefusing`,
            // which builds a library small enough to exhaust rather than waiting for the shipped
            // one to shrink.
            foreach (KeyValuePair<string, int> use in cores)
            {
                Assert.True(
                    use.Value <= DialogueExpressionHistory.DefaultCap,
                    use.Key + " was spoken " + use.Value + " times, past the freshness cap");
            }
        }

        [Fact]
        public void AMultiTurnExchangeRecognisesTheSameQuestionAskedAgain()
        {
            PlaygroundSweepResult result = Evaluate("conversation");

            Assert.False(Row(result, "asked once").Turn.AlreadyAsked);
            Assert.True(Row(result, "asked again").Turn.AlreadyAsked);
            Assert.True(Row(result, "asked a third time").Turn.AlreadyAsked);
        }

        [Fact]
        public void ConversationLocalStateDoesNotBecomeASecondHistory()
        {
            PlaygroundSweepResult result = Evaluate("conversation");

            PlaygroundSweepRow promoted = Row(result, "a promise, promoted");
            PlaygroundSweepRow transient = Row(result, "a promise, left transient");

            Assert.NotNull(promoted.Turn.Committed);
            Assert.Equal(
                promoted.Run.Exchange.Before.Obligations + 1,
                promoted.Run.Stage.World.Obligations.Records.Count);

            Assert.Null(transient.Turn.Committed);
            Assert.Equal(
                transient.Run.Exchange.Before.Obligations,
                transient.Run.Stage.World.Obligations.Records.Count);

            // The transcript itself outlives nothing: no event carries a speech act, and the acts
            // the conversation noted are held by the exchange object alone.
            Assert.True(transient.Run.Exchange.Conversation.Acts.Count > 0);
        }

        /// <summary>An axis point current state cannot express is named, not approximated.</summary>
        [Fact]
        public void AnUnsupportedAxisPointIsReportedRatherThanFaked()
        {
            PlaygroundSweepResult result = Evaluate("conversation");
            PlaygroundSweepRow unsupported = Row(result, "a self-contradiction");

            Assert.False(unsupported.Evaluated);
            Assert.Null(unsupported.Run);
            Assert.NotNull(unsupported.Unsupported);
            Assert.Contains("SWEEP: CONVERSATION", Report("run", "playground-sweep", "--axis", "conversation"),
                StringComparison.Ordinal);
            Assert.Contains("UNSUPPORTED", Report("run", "playground-sweep", "--axis", "conversation"),
                StringComparison.Ordinal);
        }

        // -- the instrument itself -------------------------------------------------------------

        /// <summary>
        /// Reporters read. Writing a report a second time must give the same text and must leave
        /// the world exactly where the first one did - which is what makes the report replayable
        /// and stops "print it nicely" from becoming a second place the simulation happens.
        /// </summary>
        [Fact]
        public void WritingTheReportChangesNothing()
        {
            PlaygroundSweepResult result = Evaluate("callbacks");
            PlaygroundRun run = Row(result, "it was done to them").Run;

            int events = run.Stage.World.Ledger.Count;
            int facts = run.Stage.World.Knowledge.Facts.Count;
            int obligations = run.Stage.World.Obligations.Records.Count;

            StringWriter first = new StringWriter();
            PlaygroundSweepReport.Write(first, result, Seed);
            StringWriter second = new StringWriter();
            PlaygroundSweepReport.Write(second, result, Seed);

            Assert.Equal(first.ToString(), second.ToString());
            Assert.Equal(events, run.Stage.World.Ledger.Count);
            Assert.Equal(facts, run.Stage.World.Knowledge.Facts.Count);
            Assert.Equal(obligations, run.Stage.World.Obligations.Records.Count);
        }

        /// <summary>
        /// An invariant has to be able to fail, or a green sweep says nothing. Each check below is
        /// run twice: once over the rows it is meant to pass, and once over rows deliberately
        /// arranged to break it. Every broken case is built from real runs rather than from a
        /// doctored reading, because a reading nobody could produce proves nothing about the
        /// readings the sweep actually takes.
        ///
        /// Two of the universal invariants have no constructible counter-example at all -
        /// <c>RealizedLine.Meaning</c> is defined as the act's own signature, and Disclosure will
        /// not compose an act for a speaker holding no belief - so what is asserted for those is
        /// that the check was not vacuous: it really did look at rows carrying both halves.
        /// </summary>
        [Fact]
        public void TheWithheldCallbackInvariantComplainsWhenAPermitReachesWording()
        {
            PlaygroundSweepInvariant invariant = Find("a withheld memory never reaches the wording layer");
            PlaygroundSweepResult honest = Evaluate("callbacks");
            Assert.Empty(invariant.Check(honest.Rows));

            PlaygroundSweepRow guarded = Row(honest, "done to them, guarded");
            Assert.NotNull(guarded.Turn.WithheldCallback);
            Assert.Null(guarded.Turn.Request.Callback);

            guarded.Turn.Request.Callback = guarded.Turn.WithheldCallback;
            Assert.NotEmpty(invariant.Check(new[] { guarded }));

            guarded.Turn.Request.Callback = null;
            Assert.Empty(invariant.Check(honest.Rows));
        }

        /// <summary>
        /// The voice check compares every toned row against the family's own unconstrained one, so
        /// a row from a state with a wider pool standing in for a toned voice is exactly the shape
        /// of the failure - a request that appears to have added candidates.
        /// </summary>
        [Fact]
        public void TheVoiceInvariantComplainsWhenAToneAppearsToWidenAPool()
        {
            PlaygroundSweepInvariant invariant = Widening();
            PlaygroundSweepResult voices = Evaluate("voice");
            Assert.Empty(invariant.Check(voices.Rows));

            PlaygroundSweepRow unconstrained = Row(voices, PlaygroundVoices.Neutral);
            PlaygroundSweepRow wider = Row(Evaluate("vocabulary"), "trade");

            Assert.NotEmpty(wider.Turn.Request.Tone);
            Assert.True(
                wider.Turn.Eligible.CountAt(FragmentPosition.Modifier)
                > unconstrained.Turn.Eligible.CountAt(FragmentPosition.Modifier),
                "the counter-example does not actually have a wider pool");

            Assert.NotEmpty(invariant.Check(new[] { unconstrained, wider }));
        }

        /// <summary>
        /// The prohibition check compares a ruled row's arithmetic with its own control's. Handing
        /// it a control from the other situation in the family is the failure it exists to catch: a
        /// line that appears to have moved the sum it was weighed on.
        /// </summary>
        [Fact]
        public void TheProhibitionInvariantComplainsWhenALineAppearsToMoveTheBalance()
        {
            PlaygroundSweepAxis axis = PlaygroundSweepAxes.Default().Find("negative-space");
            PlaygroundSweepInvariant invariant = null;
            foreach (PlaygroundSweepInvariant candidate in axis.Invariants)
            {
                if (candidate.Name.StartsWith("a line removes a move", StringComparison.Ordinal))
                {
                    invariant = candidate;
                }
            }

            Assert.NotNull(invariant);

            PlaygroundSweepResult honest = PlaygroundSweepReport.Evaluate(axis, Seed);
            Assert.Empty(invariant.Check(honest.Rows));

            // The same rows, with the lying control renamed onto the kinship pair's control, so the
            // check reads a ruled decision against a balance it was never weighed against.
            List<PlaygroundSweepRow> crossed = new List<PlaygroundSweepRow>
            {
                Rename(Row(honest, "kin: no line"), "lying: no line"),
                Row(honest, "lying: line holds")
            };

            Assert.NotEmpty(invariant.Check(crossed));
        }

        [Fact]
        public void TheMeaningAndNoBeliefChecksLookedAtRealRows()
        {
            PlaygroundSweepResult voices = Evaluate("voice");
            PlaygroundSweepResult knowledge = Evaluate("knowledge");

            int meaningful = 0;
            foreach (PlaygroundSweepRow row in voices.Rows)
            {
                if (row.Turn?.Line != null && row.Turn.Reply != null)
                {
                    meaningful++;
                }
            }

            Assert.True(meaningful > 0, "the meaning check saw no row carrying both a line and an act");
            Assert.Empty(Find("a realized line means exactly what its act meant").Check(voices.Rows));

            PlaygroundSweepRow believes = Row(knowledge, "saw it, can prove it");
            PlaygroundSweepRow holdsNothing = Row(knowledge, "no belief");

            Assert.True(believes.Run.Stage.World.Knowledge.Knows(believes.Run.Speaker, believes.Run.Stage.SubjectFactId));
            Assert.False(
                holdsNothing.Run.Stage.World.Knowledge.Knows(
                    holdsNothing.Run.Speaker, holdsNothing.Run.Stage.SubjectFactId));
            Assert.Empty(Find("a speaker who holds no belief discloses nothing").Check(knowledge.Rows));
        }

        /// <summary>A family that breaks an invariant must fail the process, not print a warning.</summary>
        [Fact]
        public void AViolationFailsTheScenarioWithTheLaboratorysOwnFailureCode()
        {
            PlaygroundSweepResult broken = new PlaygroundSweepResult(
                PlaygroundSweepAxes.Default().Find("voice"),
                new PlaygroundSweepRow[0],
                new[] { new PlaygroundSweepViolation("voice", "an invented invariant", "for the test") });

            Assert.True(broken.Failed);

            StringWriter output = new StringWriter();
            PlaygroundSweepReport.Write(output, broken, Seed);
            Assert.Contains("VIOLATION", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("1 VIOLATED", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryFamilyPassesItsOwnInvariants()
        {
            foreach (PlaygroundSweepAxis axis in PlaygroundSweepAxes.Default().All)
            {
                PlaygroundSweepResult result = PlaygroundSweepReport.Evaluate(axis, Seed);
                Assert.True(
                    result.Violations.Count == 0,
                    axis.Id + " violated: " + (result.Violations.Count == 0 ? string.Empty : result.Violations[0].ToString()));
            }

            Assert.Equal(LabExit.Success, LabCommandLine.Execute(
                new[] { "run", "playground-sweep", "--axis", "all" }, new StringWriter(), new StringWriter()));
        }

        /// <summary>
        /// The sweep runs no Elin, and says so. Both halves matter: a laboratory that quietly
        /// mocked a runtime system would report a green row for something nobody has ever run.
        /// </summary>
        [Fact]
        public void TheSweepPerformsNoRuntimeBehaviourAndLabelsWhatItCannotReach()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                Assert.False(
                    name.StartsWith("Elin", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase),
                    "the laboratory loaded " + name);
            }

            string report = Report("run", "playground-sweep", "--axis", "ownership");
            Assert.Contains("PLUGIN ONLY", report, StringComparison.Ordinal);
            Assert.Contains("LABORATORY-AUTHORED", report, StringComparison.Ordinal);
            Assert.Contains("VoiceProfile", report, StringComparison.Ordinal);
            Assert.NotEmpty(PlaygroundAvailability.WithSupport(PlaygroundSupport.RuntimeRequired));
        }

        // -- scaffolding -------------------------------------------------------------------------

        private static PlaygroundSweepResult Evaluate(string axis)
        {
            return PlaygroundSweepReport.Evaluate(PlaygroundSweepAxes.Default().Find(axis), Seed);
        }

        private static PlaygroundSweepRow Row(PlaygroundSweepResult result, string label)
        {
            foreach (PlaygroundSweepRow row in result.Rows)
            {
                if (row.Label == label)
                {
                    return row;
                }
            }

            throw new InvalidOperationException("No row '" + label + "' in " + result.Axis.Id + ".");
        }

        private static DisclosureStrategy Strategy(PlaygroundSweepResult result, string label)
        {
            return Row(result, label).Turn.Decision.Strategy;
        }

        private static PlaygroundSweepInvariant Find(string startsWith)
        {
            foreach (PlaygroundSweepInvariant invariant in PlaygroundSweepInvariant.Universal)
            {
                if (invariant.Name.StartsWith(startsWith, StringComparison.Ordinal))
                {
                    return invariant;
                }
            }

            throw new InvalidOperationException("No universal invariant starting '" + startsWith + "'.");
        }

        /// <summary>The voice family's own "a requested tone never widens a slot's pool" check.</summary>
        private static PlaygroundSweepInvariant Widening()
        {
            foreach (PlaygroundSweepInvariant invariant in PlaygroundSweepAxes.Default().Find("voice").Invariants)
            {
                if (invariant.Name.StartsWith("a requested tone never widens", StringComparison.Ordinal))
                {
                    return invariant;
                }
            }

            throw new InvalidOperationException("The voice family no longer checks that a tone narrows.");
        }

        /// <summary>The same row under another label, so a check can be handed the wrong control.</summary>
        private static PlaygroundSweepRow Rename(PlaygroundSweepRow row, string label)
        {
            return PlaygroundSweepRow.Of(label, row.IsBaseline, row.Changed, row.Run, row.ReadAt, row.Against);
        }

        private static string Report(params string[] args)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            Assert.Equal(LabExit.Success, LabCommandLine.Execute(args, output, error));
            return output.ToString();
        }
    }
}
