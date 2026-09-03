using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// One conversation, from the world that caused it to the words that came out of it, through
    /// the production path and nothing else.
    ///
    /// BQ-068 through BQ-083 were each proved on their own, and each proof is the right shape for
    /// its step. What none of them could show is that the steps *compose*: that an authoritative
    /// fact reaches a cast actor, that the actor's own reading of it reaches a semantic act, that
    /// the act survives a disclosure decision, a voice, a lived vocabulary, a personal line, a
    /// repetition history and a weirdness ceiling without changing what it means, that the exchange
    /// is remembered well enough for the next one to react to it, and that old business can be
    /// raised without leaking anything its owner would have kept.
    ///
    /// This file is that single vertical path. Everything in it is production Core over shipped
    /// content: <see cref="TheftLaboratory"/> stages the situation, <c>Package/content.bqc</c>
    /// supplies both the storylets and the wordings, and there is no second conversation engine
    /// here - the test only calls the same entry points a caller would.
    ///
    /// <b>What it is not.</b> It is not a substitute for the per-step tests, which say far more
    /// about each layer than any end-to-end run can. It does not require every optional system to
    /// take part in every line - a voice with nothing to say about a fragment is a real answer, and
    /// so is a speaker with no old business. And it stops where Core stops: no step in this tranche
    /// projects any of this into a running game, so the last thing proved here is a realized line
    /// and its meaning, not a line the player read.
    /// </summary>
    public class SemanticConversationIntegrationTests
    {
        // -- the path, end to end ------------------------------------------------------------------

        /// <summary>
        /// The whole vertical, in the order the systems actually run.
        ///
        /// A theft the world holds; the scenes it makes available and who they cast; the witness's
        /// own reading of what happened; the question the player puts to them; what they decide to
        /// do with it; the tone, vocabulary and prohibitions their character brings; the words; and
        /// the exchange, remembered.
        /// </summary>
        [Fact]
        public void OneTheftReachesOneSpokenLineThroughEverySystemThatApplies()
        {
            Conversation scene = Conversation.Begin();

            // 1. An authoritative situation, and the scenes it supports. Casting is BQ-067's
            //    eligibility and BQ-068's chemistry over it, and the report accounts for both.
            StoryletOpportunity accusation = scene.Opportunity("storylet.public_accusation");
            Assert.True(accusation.IsAvailable, accusation.RefusalReason);
            Assert.Equal(scene.Thief, accusation.RoleBindings["accused"]);
            Assert.Equal(scene.Witness, accusation.RoleBindings["accuser"]);
            Assert.False(accusation.SearchTruncated);

            // 2. The cast actor's own reading of the fact, which is where their state enters.
            ActorReaction reaction = ReactionDerivation.React(
                scene.World, scene.Witness, scene.TheftFactId, WeirdnessLevel.Mundane, scene.Now, scene.Vanilla);
            Assert.NotNull(reaction.Interpretation);
            Assert.Equal(scene.Witness, reaction.Interpretation.ActorId);

            // 3. A semantic act, with no text anywhere on it.
            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            Assert.Equal(SpeechActType.Ask, ask.Type);
            Assert.Equal(scene.TheftFactId, ask.About);

            // 4. What the person asked decides to do about it.
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            Assert.True(decision.WillDisclose);
            SpeechAct answer = Disclosure.Compose(decision, ask);
            Assert.Equal(SpeechActType.Answer, answer.Type);
            Assert.Same(ask, answer.InReplyTo);

            // 5. Who they are, as constraints on wording and never as a source of anything said.
            RealizationRequest request = scene.Speaking(answer, decision);
            Assert.NotEmpty(request.Tone);

            // 6. The words, from the shipped library.
            RealizedLine line = scene.Realizer.Realize(request);
            Assert.True(line.Rendered, line.Refusal);
            Assert.NotEqual(string.Empty, line.Text);

            // 7. And the meaning is the same object it was before anybody said anything.
            Assert.Equal(answer.Signature, line.Meaning);

            // 8. The exchange is remembered, as two acts and not as two sentences.
            scene.State.Note(ask);
            scene.State.Note(answer);
            Assert.Equal(new[] { ask, answer }, scene.State.Acts);
            Assert.Empty(scene.State.UnansweredQuestions);
        }

        // -- meaning survives wording ----------------------------------------------------------------

        /// <summary>
        /// Every identity layer that touches the line is a filter on how it is said, and none of
        /// them is allowed to change what was said. Run the same act under voices that disagree,
        /// under a lived vocabulary and none, under a personal line and none, under every weirdness
        /// ceiling, and against a repetition history that has already heard it: the wording may
        /// differ or be refused outright, and the meaning is one value throughout.
        ///
        /// Refusal counts as passing. A voice that narrows the pool to nothing has said "not like
        /// that", which is the mechanism working; what would be a failure is a line that came out
        /// meaning something else.
        /// </summary>
        [Fact]
        public void NoVoiceVocabularyProhibitionOrBudgetChangesWhatWasSaid()
        {
            Conversation scene = Conversation.Begin();
            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);

            List<RealizationRequest> variants = new List<RealizationRequest>();
            foreach (VoiceProfile voice in Voices())
            {
                foreach (IReadOnlyList<string> vocabulary in new[] { Nothing, OneOfEachDomain() })
                {
                    foreach (IReadOnlyList<string> forbidden in new[] { Nothing, AllManners() })
                    {
                        foreach (WeirdnessLevel ceiling in Enum.GetValues(typeof(WeirdnessLevel)).Cast<WeirdnessLevel>())
                        {
                            variants.Add(new RealizationRequest(answer)
                            {
                                Decision = decision,
                                Claim = scene.Theft,
                                Cast = scene.Cast,
                                Tone = voice.RequestedTone(),
                                Vocabulary = vocabulary,
                                Forbidden = forbidden,
                                WeirdnessBudget = new WeirdnessBudget(ceiling),
                                Rng = new DeterministicRng(4242)
                            });
                        }
                    }
                }
            }

            int rendered = 0;
            foreach (RealizationRequest variant in variants)
            {
                RealizedLine line = scene.Realizer.Realize(variant);

                // The one invariant, whichever way the line went.
                Assert.Equal(answer.Signature, line.Meaning);
                Assert.Equal(SpeechActType.Answer, answer.Type);
                Assert.Equal(scene.TheftFactId, answer.Content.PropositionFact);

                if (line.Rendered)
                {
                    rendered++;
                }
                else
                {
                    Assert.Equal(string.Empty, line.Text);
                    Assert.NotEqual(string.Empty, line.Refusal);
                }
            }

            // The sweep has to actually reach the realizer rather than being refused throughout,
            // or "meaning never changed" would be a statement about nothing.
            Assert.True(
                rendered > variants.Count / 4,
                "only " + rendered + " of " + variants.Count + " variants reached the realizer at all");
        }

        /// <summary>
        /// A repetition history narrows what may be said again and never what was meant. The same
        /// act, said twice into a history that remembers the first, is the same meaning both times.
        /// </summary>
        [Fact]
        public void RepetitionHistoryNarrowsWordingAndNotMeaning()
        {
            Conversation scene = Conversation.Begin();
            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);

            DialogueExpressionHistory history = new DialogueExpressionHistory();

            RealizedLine first = scene.Realizer.Realize(scene.Speaking(answer, decision, history));
            Assert.True(first.Rendered, first.Refusal);

            RealizedLine second = scene.Realizer.Realize(scene.Speaking(answer, decision, history));

            Assert.Equal(answer.Signature, first.Meaning);
            Assert.Equal(answer.Signature, second.Meaning);
        }

        // -- nothing downstream invents knowledge -----------------------------------------------------

        /// <summary>
        /// A speaker who holds no belief about the claim says nothing, and no amount of voice,
        /// vocabulary or staging turns that into a sentence.
        ///
        /// The victim of the theft never saw it happen. Asked about it they have no decision to
        /// take, so there is no act to word - which is the boundary in its plainest form: wording
        /// cannot be the place a belief appears, because it is never reached.
        /// </summary>
        [Fact]
        public void SomebodyWithNoBeliefProducesNoActAndSoNoLine()
        {
            Conversation scene = Conversation.Begin();
            Assert.False(scene.World.Knowledge.TryGetBelief(scene.Victim, scene.TheftFactId, out KnowledgeRecord _));

            SpeechAct ask = scene.Ask(scene.Player, scene.Victim);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Victim, scene.Now);

            Assert.Equal(DisclosureStrategy.NothingToDisclose, decision.Strategy);
            Assert.Null(Disclosure.Compose(decision, ask));
        }

        /// <summary>
        /// The one thing wording is never told. The thief will deny the theft and their denial is a
        /// lie - <c>Deception</c> says so from the belief graph - and the words are drawn from the
        /// pool an honest denial draws from, at the same seed, identically.
        ///
        /// Stated as the comparison rather than as an inspection of the request, because the claim
        /// is about what a listener could hear: passing the falsifying decision and passing no
        /// decision at all have to produce the same sentence, or the lie is catchable by ear.
        /// </summary>
        [Fact]
        public void ALyingDenialIsWordedExactlyAsAnHonestOneIs()
        {
            Conversation scene = Conversation.Begin();
            SpeechAct ask = scene.Ask(scene.Player, scene.Thief);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Thief, scene.Now);

            Assert.Equal(DisclosureTactic.Falsify, decision.Tactic);
            SpeechAct denial = Disclosure.Compose(decision, ask);
            Assert.Equal(SpeechActType.Deny, denial.Type);

            // The simulation still knows perfectly well that it is a lie.
            Assert.Equal(Sincerity.Insincere, Deception.Assess(scene.World, denial).Sincerity);

            RealizedLine toldTheTactic = scene.Realizer.Realize(new RealizationRequest(denial)
            {
                Decision = decision, Claim = scene.Theft, Cast = scene.Cast, Rng = new DeterministicRng(31)
            });
            RealizedLine toldNothing = scene.Realizer.Realize(new RealizationRequest(denial)
            {
                Claim = scene.Theft, Cast = scene.Cast, Rng = new DeterministicRng(31)
            });

            Assert.True(toldTheTactic.Rendered, toldTheTactic.Refusal);
            Assert.Equal(toldNothing.Text, toldTheTactic.Text);
            Assert.Equal(toldNothing.Core, toldTheTactic.Core);
        }

        // -- the second exchange reads the first --------------------------------------------------------

        /// <summary>
        /// A conversation that has happened is a conversation the next turn can stand on: the same
        /// question asked twice is recognised as the same question, an unanswered one is still
        /// open, and a speaker who reverses themselves is caught on what they said here rather than
        /// on what the world knows.
        /// </summary>
        [Fact]
        public void ASecondExchangeReactsToWhatTheFirstOneEstablished()
        {
            Conversation scene = Conversation.Begin();
            ConversationState state = scene.State;

            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            state.Note(ask);
            Assert.Single(state.UnansweredQuestions);

            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);
            state.Note(answer);
            Assert.Empty(state.UnansweredQuestions);
            Assert.Single(state.Claims);

            // Asked again, about the same matter, in different words that Core never sees.
            SpeechAct again = scene.Ask(scene.Player, scene.Witness);
            Assert.True(state.WasAlreadyAsked(again));

            // And the witness now says the opposite of what they just said.
            SpeechAct reversal = SpeechAct.Compose(
                SpeechActType.Deny,
                scene.Witness,
                scene.Player,
                new ActionBinding { PropositionFact = scene.TheftFactId },
                scene.Thief);
            DiscourseContradiction? caught = state.Contradicts(scene.World, reversal);

            Assert.True(caught.HasValue);
            Assert.Same(answer, caught.Value.Earlier);
            Assert.Same(reversal, caught.Value.Later);
        }

        // -- callbacks reach wording without leaking ------------------------------------------------------

        /// <summary>
        /// Old business the speaker is entitled to raise, raised - and the same clearance refused
        /// the moment it is carried into an act it was not granted for.
        ///
        /// A permit is cleared for one speaker talking to one listener. Handing it to a line
        /// somebody else is saying, or to a line said to somebody else, is the shape a leak would
        /// take, and both are refused at the request rather than silently honoured.
        /// </summary>
        [Fact]
        public void CallbackMaterialIsSurfacedOnlyForTheSpeakerAndListenerItWasClearedFor()
        {
            Conversation scene = Conversation.WithHistory();

            CallbackPermit permit = CallbackDisclosure.Best(
                scene.World, scene.Vanilla, scene.Witness, scene.Player, scene.Now);
            Assert.NotNull(permit);
            Assert.True(permit.Allowed);

            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);

            RealizationRequest clearedRequest = new RealizationRequest(answer)
            {
                Decision = decision, Claim = scene.Theft, Cast = scene.Cast,
                Callback = permit, Rng = new DeterministicRng(5)
            };
            Assert.Equal(string.Empty, clearedRequest.WhyNot());

            RealizedLine cleared = scene.Realizer.Realize(clearedRequest);
            Assert.True(cleared.Rendered, cleared.Refusal);
            Assert.Equal(answer.Signature, cleared.Meaning);

            // The victim's line, carrying the witness's clearance.
            SpeechAct victimAsked = scene.Ask(scene.Player, scene.Victim);
            SpeechAct borrowed = SpeechAct.Compose(
                SpeechActType.Refuse, scene.Victim, scene.Player, ActionBinding.Empty, EntityId.None, victimAsked);
            RealizationRequest borrowedRequest = new RealizationRequest(borrowed)
            {
                Cast = scene.Cast, Callback = permit, Rng = new DeterministicRng(5)
            };
            Assert.Equal(
                "the callback belongs to somebody other than the speaker",
                borrowedRequest.WhyNot());
            Assert.False(scene.Realizer.Realize(borrowedRequest).Rendered);

            // The witness's line, said to somebody the clearance never covered.
            SpeechAct askedByThief = scene.Ask(scene.Thief, scene.Witness);
            DisclosureDecision toThief = Disclosure.Decide(scene.World, askedByThief, scene.Witness, scene.Now);
            SpeechAct toTheThief = Disclosure.Compose(toThief, askedByThief);
            RealizationRequest wrongListener = new RealizationRequest(toTheThief)
            {
                Decision = toThief, Claim = scene.Theft, Cast = scene.Cast,
                Callback = permit, Rng = new DeterministicRng(5)
            };
            Assert.Equal(
                "the callback was cleared for somebody other than the person being addressed",
                wrongListener.WhyNot());
            Assert.False(scene.Realizer.Realize(wrongListener).Rendered);
        }

        // -- consequences, determinism, durability ----------------------------------------------------------

        /// <summary>
        /// A promise made in conversation becomes durable exactly once, and only because a caller
        /// said it should. Committing the same promise twice mints one obligation and writes one
        /// event; the ledger is otherwise untouched by everything else this conversation did.
        /// </summary>
        [Fact]
        public void ConversationWritesOneConsequenceAndOnlyWhenAskedTo()
        {
            Conversation scene = Conversation.Begin();
            int eventsBefore = scene.World.Ledger.Events.Count;
            int obligationsBefore = scene.World.Obligations.Records.Count;

            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);
            scene.State.Note(ask);
            scene.State.Note(answer);
            scene.Realizer.Realize(scene.Speaking(answer, decision));

            // Deciding, composing and wording an exchange writes nothing.
            Assert.Equal(eventsBefore, scene.World.Ledger.Events.Count);
            Assert.Equal(obligationsBefore, scene.World.Obligations.Records.Count);

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Witness, scene.Player,
                new ActionBinding { Purpose = "speak to the guard" });
            scene.State.Note(promise);

            WorldEvent committed = scene.State.Commit(scene.World, promise, scene.Now, scene.Zone);
            Assert.NotNull(committed);
            Assert.Equal(WorldEventType.PromiseMade, committed.Type);
            Assert.Equal(eventsBefore + 1, scene.World.Ledger.Events.Count);
            Assert.Equal(obligationsBefore + 1, scene.World.Obligations.Records.Count);

            Assert.Null(scene.State.Commit(scene.World, promise, scene.Now, scene.Zone));
            Assert.Equal(eventsBefore + 1, scene.World.Ledger.Events.Count);
            Assert.Equal(obligationsBefore + 1, scene.World.Obligations.Records.Count);
        }

        /// <summary>
        /// The durable half survives a save, and the transient half is not there to survive.
        /// A committed promise comes back as the same obligation over the same event; nothing is
        /// redispatched, and the conversation itself is gone, which is what BQ-083 said it would be.
        /// </summary>
        [Fact]
        public void TheCommittedPromiseSurvivesAReloadAndTheConversationDoesNot()
        {
            Conversation scene = Conversation.Begin();
            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Witness, scene.Player,
                new ActionBinding { Purpose = "speak to the guard" });
            scene.State.Note(promise);
            WorldEvent committed = scene.State.Commit(scene.World, promise, scene.Now, scene.Zone);

            int events = scene.World.Ledger.Events.Count;
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(scene.World));

            Assert.Equal(events, reloaded.Ledger.Events.Count);
            SocialObligation carried = Assert.Single(
                reloaded.Obligations.Records, o => o.SourceEventId == committed.Id);
            Assert.Equal(scene.Witness, carried.Debtor);
            Assert.Equal(scene.Player, carried.Creditor);

            // And a second save of the reloaded world is the same world, so nothing was re-applied.
            Assert.Equal(
                WorldStateSerializer.Save(reloaded),
                WorldStateSerializer.Save(WorldStateSerializer.Load(WorldStateSerializer.Save(reloaded))));
        }

        /// <summary>
        /// The same seed says the same thing. Two independent runs of the identical path - separate
        /// worlds, separate laboratories - agree on who was cast, what was decided, and word for
        /// word what came out, which is what makes a scene that went strangely replayable.
        /// </summary>
        [Fact]
        public void TheWholePathReplaysDeterministically()
        {
            string first = Transcript();
            string second = Transcript();

            Assert.Equal(first, second);
            Assert.NotEqual(string.Empty, first);
        }

        private static string Transcript()
        {
            Conversation scene = Conversation.Begin();
            StoryletOpportunity accusation = scene.Opportunity("storylet.public_accusation");
            SpeechAct ask = scene.Ask(scene.Player, scene.Witness);
            DisclosureDecision decision = Disclosure.Decide(scene.World, ask, scene.Witness, scene.Now);
            SpeechAct answer = Disclosure.Compose(decision, ask);
            RealizedLine line = scene.Realizer.Realize(scene.Speaking(answer, decision));

            return string.Join(
                "\n",
                Diagnostics.NarrativeInspector.DescribeCasting(accusation),
                decision.Strategy + "/" + decision.Depth + "/" + decision.Tactic,
                answer.Signature,
                line.Core + ": " + line.Text);
        }

        // -- scaffolding --------------------------------------------------------------------------------

        private static readonly string[] Nothing = new string[0];

        private static IEnumerable<VoiceProfile> Voices()
        {
            yield return VoiceProfile.Neutral;
            yield return new VoiceProfile { Formality = 1.0, Directness = 0.0, Warmth = 1.0, Sarcasm = 0.0 };
            yield return new VoiceProfile { Formality = 0.0, Directness = 1.0, Warmth = 0.0, Sarcasm = 1.0 };
        }

        private static IReadOnlyList<string> OneOfEachDomain()
        {
            return DialogueVocabulary.Vocabulary;
        }

        private static IReadOnlyList<string> AllManners()
        {
            return DialogueManners.Vocabulary;
        }

        /// <summary>
        /// The theft, the people, the shipped content, and the one conversation being held over it.
        ///
        /// Nothing here is a stand-in: the world comes from <see cref="TheftLaboratory"/>, the
        /// storylets and fragments come out of the compiled bundle the game ships, and every step
        /// below is the call a caller makes.
        /// </summary>
        private sealed class Conversation
        {
            private readonly TheftLaboratory _lab;

            private Conversation(TheftLaboratory lab, StoryletEngine engine, DialogueRealizer realizer)
            {
                _lab = lab;
                Engine = engine;
                Realizer = realizer;
                Cast = DialogueCast.From(lab.World, lab.Player, Witness, Thief, Victim);
                State = new ConversationState();
            }

            public StoryletEngine Engine { get; }

            public DialogueRealizer Realizer { get; }

            public DialogueCast Cast { get; }

            /// <summary>The one exchange this scene is holding. Transient, and never saved.</summary>
            public ConversationState State { get; }

            public NarrativeWorldState World => _lab.World;

            public SandboxVanillaState Vanilla => _lab.Vanilla;

            public EntityId Player => _lab.Player;

            public EntityId Zone => _lab.Zone;

            public EntityId Thief => _lab.Situation.ThiefId;

            public EntityId Victim => _lab.Situation.VictimId;

            public EntityId Witness => _lab.Situation.WitnessId;

            public EntityId TheftFactId => _lab.Situation.TheftFactId;

            public Fact Theft => World.Knowledge.GetFact(TheftFactId);

            public GameTime Now => _lab.Vanilla.Now;

            public static Conversation Begin()
            {
                return Build(TheftLaboratory.Create());
            }

            /// <summary>The same situation, played out and left to settle, so there is old business.</summary>
            public static Conversation WithHistory()
            {
                TheftLaboratory lab = TheftLaboratory.Create();
                lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
                lab.Perform("pickpocket", lab.Situation.ThiefId);
                lab.Perform("return_item", lab.Situation.VictimId);
                lab.AdvanceDays(20);
                return Build(lab);
            }

            public StoryletOpportunity Opportunity(string id)
            {
                foreach (StoryletOpportunity opportunity in Engine.Find(
                    new StoryletCastingContext(World, _lab.Vanilla, _lab.Situation.Thread, TheftFactId)))
                {
                    if (string.Equals(opportunity.Definition.Id, id, StringComparison.Ordinal))
                    {
                        return opportunity;
                    }
                }

                throw new InvalidOperationException("no opportunity " + id);
            }

            public SpeechAct Ask(EntityId asker, EntityId asked)
            {
                SpeechAct ask = SpeechAct.Compose(
                    SpeechActType.Ask, asker, asked, new ActionBinding { PropositionFact = TheftFactId });
                Assert.NotNull(ask);
                return ask;
            }

            /// <summary>
            /// The speaker's whole request: their voice, whatever their observed life lets them
            /// reach for, and whatever their own lines take off the table. Every one of them is
            /// derived where it belongs and carried here unchanged.
            /// </summary>
            public RealizationRequest Speaking(
                SpeechAct act,
                DisclosureDecision decision,
                DialogueExpressionHistory history = null)
            {
                NarrativeNpc speaker = World.Registry.GetNpc(act.Speaker);
                IdentityAffordances identity = IdentityAffordances.Of(speaker, _lab.Vanilla);

                return new RealizationRequest(act)
                {
                    Decision = decision,
                    Claim = Theft,
                    Cast = Cast,
                    Tone = new VoiceProfile { Formality = 0.9, Directness = 0.1, Warmth = 0.9 }.RequestedTone(),
                    Vocabulary = OccupationalVocabulary.RequestedVocabulary(identity),
                    Forbidden = NegativeSpaceVoice.ForbiddenManners(
                        decision == null ? null : decision.Prohibitions),
                    History = history,
                    WeirdnessBudget = new WeirdnessBudget(WeirdnessLevel.DistinctlyElin),
                    Rng = new DeterministicRng(20260903UL)
                };
            }

            private static Conversation Build(TheftLaboratory lab)
            {
                ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                    Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
                Assert.Empty(bundle.Diagnostics);

                IReadOnlyList<ContentDiagnostic> diagnostics;
                StoryletEngine engine = StoryletContent.CreateEngine(bundle.Bundle, out diagnostics);
                Assert.Empty(diagnostics);

                DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(bundle.Bundle, out diagnostics);
                Assert.Empty(diagnostics);

                return new Conversation(lab, engine, new DialogueRealizer(library));
            }

            private static string RepositoryRoot()
            {
                DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                {
                    directory = directory.Parent;
                }

                if (directory == null)
                {
                    throw new InvalidOperationException("Could not locate repository root.");
                }

                return directory.FullName;
            }
        }

    }
}
