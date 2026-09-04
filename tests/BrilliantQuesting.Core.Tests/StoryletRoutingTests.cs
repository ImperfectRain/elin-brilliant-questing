using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-146. A storylet played rather than listed, and the boundaries that keeps.
    ///
    /// The claim under test is not that scenes happen - <c>FirstFiveStoryletTests</c> already
    /// proves the five fire. It is that a scene now <em>develops</em>: the same definition, cast
    /// from the same world, reaches different beats, different words and different history because
    /// of who was cast, what they are like, what they were feeling and how a check came out - and
    /// that none of those layers can reach past its own job while doing it.
    ///
    /// Everything here runs over the shipped bundle through the production entry points. There is
    /// no second router, no test-only storylet where a real one would do, and no assertion about a
    /// sentence: a line is checked for being <em>a</em> line, and its meaning is checked against
    /// the act, because Core holds no wording of its own to compare against.
    /// </summary>
    public class StoryletRoutingTests
    {
        // -- the architectural boundary ------------------------------------------------------------

        /// <summary>
        /// The rule the whole pass exists to keep, stated over the shipped content: not one string
        /// anywhere in any storylet is a sentence.
        ///
        /// Checked structurally rather than by reading the files, because "no hardcoded dialogue"
        /// is only worth anything if a future author cannot add some. Every value in a storylet
        /// payload is an id, a tag or a member of a closed vocabulary, so whitespace or sentence
        /// punctuation in one is prose - and the loader refuses it, which is what this asserts by
        /// loading the shipped bundle clean and then watching a prose field be rejected.
        /// </summary>
        [Fact]
        public void NoShippedStoryletContainsAWordOfDialogue()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<StoryletDefinition> definitions = StoryletContent.LoadDefinitions(Bundle(), out diagnostics);

            Assert.Empty(diagnostics);
            Assert.True(definitions.Count >= 20, "only " + definitions.Count + " storylets are authored");

            foreach (ContentRecord record in Bundle().Records)
            {
                if (!string.Equals(record.Kind, "storylet", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string value in Strings(record.Payload))
                {
                    Assert.DoesNotContain(" ", value, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void AStoryletCarryingAuthoredWordingIsRejectedAtLoad()
        {
            AssertRefused(Storylet("\"beats\": [{\"id\": \"says_something\", \"speaker\": \"accuser\", \"listener\": \"accused\", \"text\": \"You stole my ring!\"}]"), "may not carry authored wording");

            AssertRefused(Storylet("\"situationTags\": [\"a theft happened here\"], \"beats\": [{\"id\": \"opens\"}]"), "may not contain prose");
        }

        /// <summary>
        /// Every other class of malformed routing, each failing at load with the path that caused
        /// it. A scene that cannot stop, a route into nothing, a beat nothing reaches, a check
        /// nobody built, an act nobody has, a verb the game does not offer.
        /// </summary>
        [Fact]
        public void MalformedRoutingIsRefusedWithADiagnosticRatherThanLoaded()
        {
            AssertRefused(Storylet("\"resolutions\": [\"done\"], \"beats\": [{\"id\": \"opens\", \"routes\": [{\"when\": \"always\", \"to\": \"nowhere\"}]}]"), "names a beat that does not exist");

            AssertRefused(Storylet("\"resolutions\": [\"done\"], \"beats\": [{\"id\": \"opens\", \"routes\": [{\"when\": \"always\", \"ends\": \"undeclared\"}]}]"), "does not declare");

            AssertRefused(Storylet("\"resolutions\": [\"done\"], \"beats\": [{\"id\": \"opens\", \"routes\": [{\"when\": \"always\", \"ends\": \"done\"}]}, {\"id\": \"orphan\", \"routes\": [{\"when\": \"always\", \"ends\": \"done\"}]}]"), "No route reaches this beat");

            AssertRefused(Storylet("\"resolutions\": [\"done\"], \"beats\": [{\"id\": \"opens\", \"routes\": [{\"when\": \"always\", \"to\": \"loops\"}]}, {\"id\": \"loops\", \"routes\": [{\"when\": \"always\", \"to\": \"opens\"}]}]"), "ever ends the scene");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"speaker\": \"accuser\", \"listener\": \"accused\", \"intentions\": [{\"act\": \"cajole\"}]}]"), "Semantic act is unknown");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"speaker\": \"nobody\", \"listener\": \"accused\"}]"), "References undefined role");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"check\": {\"profile\": \"proc_telepathy\", \"actor\": \"accuser\", \"question\": \"does_it_work\"}}]"), "Check profile is unknown");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"check\": {\"profile\": \"proc_persuasion\", \"actor\": \"accuser\"}}]"), "must name the uncertainty");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"playerIntersections\": [\"summon_dragon\"]}]"), "No such action");

            AssertRefused(Storylet("\"beats\": [{\"id\": \"opens\", \"consequences\": [{\"hook\": \"something\", \"event\": \"TheWorldEnded\", \"actor\": \"accuser\"}]}]"), "World event type is unknown");

            AssertRefused(Storylet("\"resolutions\": [\"done\"], \"beats\": [{\"id\": \"opens\", \"routes\": [{\"when\": \"check_pass\", \"ends\": \"done\"}]}]"), "routes on a check it does not make");
        }

        /// <summary>
        /// The three-realization rule, over the shipped library: every act the simulation can
        /// produce has at least three genuinely different cores, and at least three ways to finish
        /// a line, with no voice, no mood and no tie narrowing anything.
        ///
        /// One fragment in a cell is worse than none: none is a refusal the player never sees, and
        /// one is a catchphrase they hear every time. The compiler's coverage report
        /// (<c>--coverage</c>) is where the whole grid lives; this is the floor under it, held here
        /// so that authoring an act without wording it fails the build rather than the playtest.
        /// </summary>
        [Fact]
        public void EveryActHasAtLeastThreeWaysToBeSaidAndThreeWaysToBeFinished()
        {
            IReadOnlyList<ContentDiagnostic> problems;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(Bundle(), out problems);
            Assert.Empty(problems);

            foreach (SpeechActType act in SpeechActProfile.Vocabulary)
            {
                string slug = Slug(act.ToString());
                Assert.True(
                    Eligible(fragments, FragmentPosition.Core, slug) >= 3,
                    slug + " has fewer than three cores");
                Assert.True(
                    Eligible(fragments, FragmentPosition.Closer, slug) >= 3,
                    slug + " has fewer than three closers");
            }

            // And the library is mostly plain. A library whose lines are mostly memorable is a
            // library of catchphrases, however good each one is on its own.
            int quotable = fragments.Count(f =>
                f.Memorability == DialogueMemorability.Signature || f.Memorability == DialogueMemorability.Protected);
            Assert.True(quotable * 2 < fragments.Count, "most of the library is trying to be memorable");
        }

        private static int Eligible(IReadOnlyList<DialogueFragment> fragments, FragmentPosition position, string act)
        {
            return fragments.Count(f => f.Position == position && Answers(f, DialogueReadings.Act, act));
        }

        private static bool Answers(DialogueFragment fragment, string key, string value)
        {
            foreach (FragmentRequirement forbid in fragment.Forbids)
            {
                if (forbid.Key == key && forbid.IsMetBy(value))
                {
                    return false;
                }
            }

            foreach (FragmentRequirement require in fragment.Requires)
            {
                if (require.Key == key)
                {
                    return require.IsMetBy(value);
                }
            }

            return true;
        }

        private static string Slug(string name)
        {
            System.Text.StringBuilder slug = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    slug.Append('_');
                }

                slug.Append(char.ToLowerInvariant(name[i]));
            }

            return slug.ToString();
        }

        // -- the same scene, different people ------------------------------------------------------

        /// <summary>
        /// Two people of opposite temperament reach opposite decisions in the same beat of the
        /// same storylet, and the difference is in what they decided to communicate rather than in
        /// how it was worded.
        ///
        /// The done-when of the whole model: a merciful creditor and a vindictive one are not two
        /// vocabularies over one scene.
        /// </summary>
        [Fact]
        public void OppositePersonalitiesReachOppositeDecisionsInTheSameBeat()
        {
            Scene merciful = Scene.Create();
            Merciful(merciful.Npc(merciful.Lab.Situation.VictimId));

            Scene vindictive = Scene.Create();
            Vindictive(vindictive.Npc(vindictive.Lab.Situation.VictimId));

            SpeechActType lenient = merciful.Decide("storylet.restitution_offered", "restitution_weighed");
            SpeechActType harsh = vindictive.Decide("storylet.restitution_offered", "restitution_weighed");

            Assert.NotEqual(lenient, harsh);
            Assert.Equal(SpeechActType.Forgive, lenient);
            Assert.Contains(harsh, new[] { SpeechActType.Threaten, SpeechActType.Request, SpeechActType.Refuse });
        }

        /// <summary>
        /// The decision is a tendency rather than a script, and the trace says which terms produced
        /// it. Nothing about the actor's identity appears among them - that is BQ-145's gate, and
        /// this is the layer most tempted to break it.
        /// </summary>
        [Fact]
        public void EveryTermBehindADecisionIsCharacterStateAndNeverIdentity()
        {
            Scene scene = Scene.Create();
            Vindictive(scene.Npc(scene.Lab.Situation.VictimId));

            IntentChoice choice = scene.Choose("storylet.restitution_offered", "restitution_weighed");

            Assert.NotNull(choice.Chosen);
            Assert.NotEmpty(choice.Chosen.Reasons);
            foreach (IntentScore score in choice.Considered)
            {
                foreach (IntentReason reason in score.Reasons)
                {
                    Assert.DoesNotContain("occupation", reason.Term, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("race", reason.Term, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("archetype", reason.Term, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// One decision, worded differently by two voices, and the meaning is one value throughout.
        /// A voice that narrows the pool to nothing has said "not like that", which is the
        /// mechanism working rather than a failure.
        /// </summary>
        [Fact]
        public void TheSameSceneRealizesDifferentLinesForDifferentVoices()
        {
            Scene scene = Scene.Create();
            SpeechAct act = scene.ActOf("storylet.public_accusation", "name_charge");
            Assert.NotNull(act);

            HashSet<string> spoken = new HashSet<string>(StringComparer.Ordinal);
            foreach (VoiceProfile voice in Voices())
            {
                RealizedLine line = scene.Realizer.Realize(new RealizationRequest(act)
                {
                    Claim = scene.Theft,
                    Cast = scene.Cast,
                    Tone = voice.RequestedTone(),
                    Rng = new DeterministicRng(9001UL)
                });

                Assert.Equal(act.Signature, line.Meaning);
                if (line.Rendered)
                {
                    spoken.Add(line.Text);
                }
            }

            Assert.True(spoken.Count >= 2, "every voice said the identical sentence");
        }

        /// <summary>
        /// The speaker's mood and their tie to the listener reach wording, and reach nothing else.
        /// Same act, same seed, different feeling: the words may move and the meaning may not.
        /// </summary>
        [Fact]
        public void MoodAndTieNarrowTheWordingAndNothingElse()
        {
            Scene scene = Scene.Create();
            SpeechAct act = scene.ActOf("storylet.public_accusation", "name_charge");

            RealizationRequest calm = new RealizationRequest(act)
            {
                Claim = scene.Theft,
                Cast = scene.Cast,
                Rng = new DeterministicRng(4242UL)
            };
            RealizationRequest angry = new RealizationRequest(act)
            {
                Claim = scene.Theft,
                Cast = scene.Cast,
                Feeling = SpeakerFeeling.Felt(EmotionalState.Anger, 0.9),
                Rng = new DeterministicRng(4242UL)
            };

            IReadOnlyList<DialogueFragment> withoutMood = scene.Realizer.Candidates(FragmentPosition.Core, calm);
            IReadOnlyList<DialogueFragment> withMood = scene.Realizer.Candidates(FragmentPosition.Core, angry);

            Assert.True(withMood.Count > withoutMood.Count, "an audible mood opened no wording at all");
            Assert.Equal(act.Signature, scene.Realizer.Realize(angry).Meaning);
            Assert.Equal(act.Signature, scene.Realizer.Realize(calm).Meaning);

            // And a tie read against somebody the act does not address is refused rather than
            // quietly worded as though it had been read against the right person.
            RealizationRequest misread = new RealizationRequest(act)
            {
                Claim = scene.Theft,
                Cast = scene.Cast,
                Tie = SpeakerTie.Tied(BrilliantQuesting.Relationships.RelationKind.Friend, scene.Lab.Player)
            };
            Assert.Contains("does not address", scene.Realizer.Realize(misread).Refusal, StringComparison.Ordinal);
        }

        // -- routing --------------------------------------------------------------------------------

        /// <summary>
        /// The same situation, the same cast, and two different check outcomes send the scene down
        /// two different routes with two different terminal states.
        /// </summary>
        [Fact]
        public void DifferentCheckOutcomesRouteTheSameSituationDifferently()
        {
            Scene passing = Scene.Create();
            Scene failing = Scene.Create();

            StoryletPlay won = passing.Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.CriticalPass));
            StoryletPlay lost = failing.Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.CriticalFail));

            Assert.True(won.Played, won.Refusal);
            Assert.True(lost.Played, lost.Refusal);
            Assert.NotEqual(
                string.Join(">", won.Beats.Select(b => b.BeatId)),
                string.Join(">", lost.Beats.Select(b => b.BeatId)));
            Assert.NotEqual(string.Empty, won.Resolution);
            Assert.NotEqual(string.Empty, lost.Resolution);
        }

        /// <summary>
        /// A scene plays with nobody watching and nothing offered to the player. The world is
        /// intact afterwards and the scene reached a state somebody declared - the whole of "the
        /// player must not be the only thing that can move a storylet".
        /// </summary>
        [Fact]
        public void AScenePlaysToATerminalStateWithNoPlayerInIt()
        {
            Scene scene = Scene.Create();
            int factsBefore = scene.Lab.World.Knowledge.Facts.Count;

            StoryletPlay play = scene.Play("storylet.private_confrontation");

            Assert.True(play.Played, play.Refusal);
            Assert.NotEmpty(play.Beats);
            Assert.NotEqual(string.Empty, play.Resolution);
            Assert.DoesNotContain(scene.Lab.Player, play.Beats.Select(b => b.Speaker));
            Assert.DoesNotContain(scene.Lab.Player, play.Beats.Select(b => b.Listener));

            // Declining to be involved is not a failure state anywhere: the scene simply resolved
            // without the player, and the facts of the world are exactly as they were.
            Assert.Equal(factsBefore, scene.Lab.World.Knowledge.Facts.Count);
            Assert.Equal(TruthState.True, scene.Theft.Truth);
        }

        /// <summary>
        /// Every route a scene can take ends somewhere it declared, whatever the checks do. Run
        /// each routed storylet under every check outcome and every seed in a small sweep: no
        /// scene runs to its step bound, and no scene stops in a state nobody named.
        /// </summary>
        [Fact]
        public void EveryRoutedSceneReachesADeclaredResolutionUnderEveryCheckOutcome()
        {
            foreach (CheckOutcome outcome in Enum.GetValues(typeof(CheckOutcome)).Cast<CheckOutcome>())
            {
                for (ulong seed = 1; seed <= 4; seed++)
                {
                    Scene scene = Scene.Create();
                    foreach (StoryletOpportunity opportunity in scene.Available())
                    {
                        if (!opportunity.Definition.IsRouted)
                        {
                            continue;
                        }

                        StoryletPlay play = scene.Play(opportunity, new AlwaysResolver(outcome), seed);
                        string where = opportunity.Definition.Id + " at " + outcome + "/" + seed;

                        Assert.True(play.Played, where + ": " + play.Refusal);
                        Assert.True(play.Beats.Count < 32, where + " ran to the step bound");
                        Assert.Contains(
                            play.Resolution,
                            opportunity.Definition.Resolutions.Select(r => r.Id));
                    }
                }
            }
        }

        /// <summary>
        /// The same scene, the same seed, twice: the same beats, the same decisions, the same
        /// words. Determinism is what makes any of the rest of this debuggable.
        /// </summary>
        [Fact]
        public void ASceneReplaysIdenticallyFromTheSameSeed()
        {
            string first = Transcript(Scene.Create().Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.Pass), 77UL));
            string second = Transcript(Scene.Create().Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.Pass), 77UL));

            Assert.Equal(first, second);
            Assert.NotEqual(string.Empty, first);
        }

        /// <summary>
        /// A scene can refer back to what an earlier one resolved, and only where both gates allow.
        ///
        /// Old business is raised through `CallbackDisclosure`, which answers two separate
        /// questions: may this speaker know it happened, and would they bring it up with the person
        /// in front of them. A scene never asks either itself - it takes the permit or it does not
        /// - so a memory the simulation withheld cannot reach the words through a storylet.
        /// </summary>
        [Fact]
        public void ASceneCanRecallSettledHistoryAndNeverRecallsWhatWasWithheld()
        {
            Scene scene = Scene.Create();
            EntityId victim = scene.Lab.Situation.VictimId;
            EntityId witness = scene.Lab.Situation.WitnessId;

            // Something between these two, long enough ago to be worth remarking on.
            scene.Lab.World.Record(
                WorldEventType.Helped, witness, victim, scene.Lab.Vanilla.Now, 0.8,
                threadId: scene.Lab.Situation.Thread.Id);
            scene.Lab.Vanilla.AdvanceDays(CallbackHooks.SettledDays + 4);

            StoryletPlay play = scene.Play("storylet.request_for_help", new AlwaysResolver(CheckOutcome.Pass), 7UL, apply: false);
            Assert.True(play.Played, play.Refusal);

            List<PlayedBeat> spoke = play.Beats.Where(b => b.Act != null).ToList();
            Assert.NotEmpty(spoke);

            // Every permit a beat took is one the speaker owns and was cleared to spend on exactly
            // the person addressed. Nothing here checks that the optional callback slot happened to
            // fill - that is the realizer's coin, and the claim is about permission.
            foreach (PlayedBeat beat in spoke)
            {
                if (beat.Recalled == null)
                {
                    continue;
                }

                Assert.True(beat.Recalled.Allowed);
                Assert.Equal(beat.Speaker, beat.Recalled.Hook.Recaller);
                Assert.Equal(beat.Listener, beat.Recalled.Listener);
                Assert.True(beat.Act.IsAddressedTo(beat.Recalled.Listener));
            }

            // And a line never words a piece of history its speaker was not cleared for: the only
            // callback fragments that can appear are ones the permit admits.
            foreach (PlayedBeat beat in spoke.Where(b => b.Line != null && b.Line.Rendered && b.Recalled == null))
            {
                Assert.DoesNotContain(
                    beat.Line.Fragments,
                    id => id.StartsWith("call.history.", StringComparison.Ordinal));
            }
        }

        // -- what a scene may and may not write -----------------------------------------------------

        /// <summary>
        /// A scene played with consequences applied moves authoritative state, and moves it through
        /// the ledger - so memory, affinity and thread tension all happen where they already
        /// happen rather than in a second consequence system.
        /// </summary>
        [Fact]
        public void ConsequencesReachTheWorldThroughTheEventLedger()
        {
            Scene scene = Scene.Create();
            scene.Lab.Consequences.Attach();
            int eventsBefore = scene.Lab.World.Ledger.Events.Count;

            StoryletPlay play = scene.Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.Pass));

            Assert.True(play.Played, play.Refusal);
            Assert.NotEmpty(play.Firing.ConsequenceHookIds);
            Assert.True(
                scene.Lab.World.Ledger.Events.Count > eventsBefore,
                "a scene that recorded hooks appended nothing to history");

            foreach (WorldEvent recorded in scene.Lab.World.Ledger.Events.Skip(eventsBefore))
            {
                Assert.Equal(scene.Lab.Situation.Thread.Id, recorded.ThreadId);
                Assert.Contains(scene.Theft.Id, recorded.Related);
            }

            // And a hook only lands when the thing it records happened: the beat that offers a
            // charge and a question does not file an accusation when the question was asked.
            foreach (PlayedBeat beat in play.Beats)
            {
                if (beat.Consequences.Contains("charge_named_in_public"))
                {
                    Assert.Equal(SpeechActType.Accuse, beat.Act.Type);
                }
            }
        }

        /// <summary>
        /// The same scene played for inspection writes nothing at all - same routes, same
        /// decisions, same words, and a world that has not moved.
        /// </summary>
        [Fact]
        public void PlayingASceneForInspectionWritesNothing()
        {
            Scene applied = Scene.Create();
            Scene inspected = Scene.Create();

            StoryletPlay real = applied.Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.Pass), 5UL);
            int eventsBefore = inspected.Lab.World.Ledger.Events.Count;
            StoryletPlay dry = inspected.Play("storylet.public_accusation", new AlwaysResolver(CheckOutcome.Pass), 5UL, apply: false);

            Assert.Equal(Transcript(real), Transcript(dry));
            Assert.Equal(eventsBefore, inspected.Lab.World.Ledger.Events.Count);
        }

        /// <summary>
        /// Wording writes nothing, whatever a scene says. Realize every act a scene produced, many
        /// times over, and the knowledge graph, the ledger and the fact itself are untouched.
        /// </summary>
        [Fact]
        public void RealizationMovesNoWorldState()
        {
            Scene scene = Scene.Create();
            StoryletPlay play = scene.Play("storylet.private_confrontation", new AlwaysResolver(CheckOutcome.Pass), 11UL, apply: false);

            int facts = scene.Lab.World.Knowledge.Facts.Count;
            int events = scene.Lab.World.Ledger.Events.Count;
            int knowers = scene.Lab.World.Knowledge.Knowers(scene.Theft.Id).Count();

            foreach (PlayedBeat beat in play.Beats.Where(b => b.Act != null))
            {
                for (ulong seed = 0; seed < 8; seed++)
                {
                    scene.Realizer.Realize(new RealizationRequest(beat.Act)
                    {
                        Claim = scene.Theft,
                        Cast = scene.Cast,
                        Rng = new DeterministicRng(seed)
                    });
                }
            }

            Assert.Equal(facts, scene.Lab.World.Knowledge.Facts.Count);
            Assert.Equal(events, scene.Lab.World.Ledger.Events.Count);
            Assert.Equal(knowers, scene.Lab.World.Knowledge.Knowers(scene.Theft.Id).Count());
        }

        /// <summary>
        /// A denial in a scene is a denial whether or not it is true, and it does not touch the
        /// truth of anything. The thief who denies the theft leaves the theft exactly as true as
        /// it was, and the words are the words an honest denial of the same claim would use.
        /// </summary>
        [Fact]
        public void DenyingSomethingTrueChangesNeitherTheTruthNorTheWording()
        {
            Scene scene = Scene.Create();
            EntityId thief = scene.Lab.Situation.ThiefId;
            EntityId victim = scene.Lab.Situation.VictimId;

            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, thief, victim,
                new ActionBinding { PropositionFact = scene.Theft.Id, Item = scene.Lab.Situation.ItemId });
            Assert.NotNull(denial);

            RealizedLine said = scene.Realizer.Realize(new RealizationRequest(denial)
            {
                Claim = scene.Theft,
                Cast = scene.Cast,
                Rng = new DeterministicRng(31337UL)
            });

            Assert.True(said.Rendered, said.Refusal);
            Assert.Equal(TruthState.True, scene.Theft.Truth);
            Assert.Equal(scene.Lab.Situation.ThiefId, scene.Theft.Subject);

            // And the pool it drew from does not depend on whether the claim is true. Wording is
            // never told who is lying: if a denial of something true could reach for words a
            // denial of something false could not, the tell would be in the sentence, and a lie
            // would be catchable by ear rather than by what the listener knows.
            RealizationRequest request = new RealizationRequest(denial)
            {
                Claim = scene.Theft,
                Cast = scene.Cast
            };
            string[] whileTrue = scene.Realizer.Candidates(FragmentPosition.Core, request)
                .Select(f => f.Id).ToArray();

            scene.Theft.Truth = TruthState.False;
            string[] whileFalse = scene.Realizer.Candidates(FragmentPosition.Core, request)
                .Select(f => f.Id).ToArray();

            Assert.NotEmpty(whileTrue);
            Assert.Equal(whileTrue, whileFalse);
        }

        // -- repetition ------------------------------------------------------------------------------

        /// <summary>
        /// A line somebody would quote is spent when it lands. Said once, a memorable fragment is
        /// stale immediately; a plain one is not.
        /// </summary>
        [Fact]
        public void AMemorableLineIsSpentSoonerThanAPlainOne()
        {
            DialogueExpressionHistory history = new DialogueExpressionHistory();
            DialogueFragment plain = Marked("frag.plain", DialogueMemorability.Utility, "group.a");
            DialogueFragment signature = Marked("frag.signature", DialogueMemorability.Signature, "group.b");
            DialogueFragment guarded = Marked("frag.protected", DialogueMemorability.Protected, "group.c");
            DialogueFragment sibling = Marked("frag.sibling", DialogueMemorability.Protected, "group.c");

            history.Note(plain);
            history.Note(signature);
            history.Note(guarded);

            Assert.True(history.IsFresh(plain));
            Assert.False(history.IsFresh(signature));
            Assert.False(history.IsFresh(guarded));

            // A protected line takes its whole register down with it, so a second one of the same
            // kind cannot follow it straight away.
            Assert.False(history.IsFresh(sibling));

            history.Note(plain);
            Assert.False(history.IsFresh(plain));
        }

        /// <summary>
        /// Over one conversation the realizer does not say the same distinctive thing twice, and
        /// the shipped library is deep enough for that to be a real constraint rather than a
        /// vacuous one.
        /// </summary>
        [Fact]
        public void OneConversationDoesNotRepeatItsMemorableLines()
        {
            Scene scene = Scene.Create();
            SpeechAct ask = SpeechAct.Compose(
                SpeechActType.Ask, scene.Lab.Player, scene.Lab.Situation.WitnessId,
                new ActionBinding { PropositionFact = scene.Theft.Id, Item = scene.Lab.Situation.ItemId });

            DialogueExpressionHistory history = new DialogueExpressionHistory();
            List<string> cores = new List<string>();
            for (ulong seed = 0; seed < 10; seed++)
            {
                RealizedLine line = scene.Realizer.Realize(new RealizationRequest(ask)
                {
                    Claim = scene.Theft,
                    Cast = scene.Cast,
                    History = history,
                    Rng = new DeterministicRng(seed)
                });

                if (line.Rendered)
                {
                    cores.Add(line.Core);
                }
            }

            Assert.True(cores.Count >= 8, "the library refused most of one conversation");
            foreach (IGrouping<string, string> repeated in cores.GroupBy(id => id, StringComparer.Ordinal))
            {
                DialogueFragment fragment;
                Assert.True(scene.Realizer.Library.TryGet(repeated.Key, out fragment));
                if (fragment.Memorability != DialogueMemorability.Utility)
                {
                    Assert.True(
                        repeated.Count() <= 2,
                        repeated.Key + " was said " + repeated.Count() + " times in one exchange");
                }
            }
        }

        // -- fixtures ---------------------------------------------------------------------------------

        private static DialogueFragment Marked(string id, string memorability, string group)
        {
            return new DialogueFragment(
                id, FragmentPosition.Modifier, "Something.", null, null, null, null, group, null, memorability);
        }

        private static IEnumerable<VoiceProfile> Voices()
        {
            yield return VoiceProfile.Neutral;
            yield return new VoiceProfile { Formality = 0.9, Directness = 0.1, Warmth = 0.9, Sarcasm = 0.1 };
            yield return new VoiceProfile { Formality = 0.1, Directness = 0.9, Warmth = 0.1, Sarcasm = 0.9 };
            yield return new VoiceProfile { Formality = 0.5, Directness = 0.9, Warmth = 0.5, Sarcasm = 0.5 };
        }

        private static void Merciful(NarrativeNpc npc)
        {
            npc.Personality.Mercy = 0.95;
            npc.Personality.Warmth = 0.9;
            npc.Personality.Generosity = 0.9;
            npc.Personality.Boldness = 0.5;
            npc.ProblemSolving.Confront = 0.1;
            npc.ProblemSolving.PaySomeone = 0.6;
            npc.Emotions.Set(EmotionalState.Anger, 0.0);
            npc.Emotions.Set(EmotionalState.Relief, 0.6);
        }

        private static void Vindictive(NarrativeNpc npc)
        {
            npc.Personality.Mercy = 0.05;
            npc.Personality.Warmth = 0.1;
            npc.Personality.Generosity = 0.1;
            npc.Personality.Boldness = 0.9;
            npc.ProblemSolving.Confront = 0.95;
            npc.ProblemSolving.PaySomeone = 0.1;
            npc.Emotions.Set(EmotionalState.Anger, 0.9);
            npc.Emotions.LastUpdatedAt = GameTime.Zero;
        }

        private static string Transcript(StoryletPlay play)
        {
            return string.Join("|", play.Beats.Select(b =>
                b.BeatId + ":" + (b.Act == null ? "-" : b.Act.Signature) + ":" + (b.Line == null ? "-" : b.Line.Text)))
                + "=>" + play.Resolution;
        }

        private static IEnumerable<string> Strings(JsonValue value)
        {
            if (value == null)
            {
                yield break;
            }

            if (value.Kind == JsonKind.String)
            {
                yield return value.StringValue;
            }
            else if (value.Kind == JsonKind.Array)
            {
                foreach (JsonValue item in value.Items)
                {
                    foreach (string found in Strings(item))
                    {
                        yield return found;
                    }
                }
            }
            else if (value.Kind == JsonKind.Object)
            {
                foreach (KeyValuePair<string, JsonValue> member in value.Members)
                {
                    foreach (string found in Strings(member.Value))
                    {
                        yield return found;
                    }
                }
            }
        }

        private static void AssertRefused(string yaml, string because)
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletContent.LoadDefinitions(Parse(yaml), out diagnostics);

            ContentDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Contains(because, diagnostic.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// A storylet payload with the ordinary preamble, plus whatever the case is about.
        ///
        /// Written as the compiler's own output shape rather than as YAML, so a case here goes
        /// through exactly the reader the build runs - the compiler validates by handing its parsed
        /// payload to <see cref="StoryletContent"/>, which is what this does.
        /// </summary>
        private static string Storylet(string body)
        {
            return "{\"requiredRoles\":["
                + "{\"id\":\"accuser\",\"source\":\"AnyoneWhoKnowsFocus\"},"
                + "{\"id\":\"accused\",\"source\":\"FactSubject\"}]"
                + (body.Length == 0 ? string.Empty : "," + body)
                + "}";
        }

        private static ContentBundle Parse(string json)
        {
            return new ContentBundle(
                ContentBundle.CurrentVersion,
                new[] { new ContentRecord("storylet.under_test", "storylet", JsonValue.Parse(json)) });
        }

        private static ContentBundle Bundle()
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(loaded.Diagnostics);
            return loaded.Bundle;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return directory.FullName;
        }

        /// <summary>A resolver that always says the same thing, so a route can be tested rather than a die.</summary>
        private sealed class AlwaysResolver : ICheckResolver
        {
            private readonly CheckOutcome _outcome;

            public AlwaysResolver(CheckOutcome outcome)
            {
                _outcome = outcome;
            }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                return new CheckResult(request.Profile.Id, request.Profile.BaseDifficulty, new CheckTerm[0], 10, 10, _outcome);
            }
        }

        private sealed class Scene
        {
            private Scene(TheftLaboratory lab, StoryletEngine engine, DialogueRealizer realizer)
            {
                Lab = lab;
                Engine = engine;
                Realizer = realizer;
            }

            internal TheftLaboratory Lab { get; }

            internal StoryletEngine Engine { get; }

            internal DialogueRealizer Realizer { get; }

            internal Fact Theft => Lab.World.Knowledge.GetFact(Lab.Situation.TheftFactId);

            internal DialogueCast Cast => DialogueCast.From(
                Lab.World, Lab.Situation.ThiefId, Lab.Situation.VictimId, Lab.Situation.WitnessId, Lab.Player);

            internal static Scene Create()
            {
                ContentBundle bundle = Bundle();
                IReadOnlyList<ContentDiagnostic> storyletProblems;
                IReadOnlyList<ContentDiagnostic> fragmentProblems;
                StoryletEngine engine = StoryletContent.CreateEngine(bundle, out storyletProblems);
                DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(bundle, out fragmentProblems);

                Assert.Empty(storyletProblems);
                Assert.Empty(fragmentProblems);
                return new Scene(TheftLaboratory.Create(), engine, new DialogueRealizer(library));
            }

            internal NarrativeNpc Npc(EntityId id) => Lab.World.Registry.GetNpc(id);

            internal IReadOnlyList<StoryletOpportunity> Available()
            {
                return Engine.Find(new StoryletCastingContext(
                    Lab.World, Lab.Vanilla, Lab.Situation.Thread, Lab.Situation.TheftFactId));
            }

            internal StoryletOpportunity Opportunity(string storyletId)
            {
                StoryletOpportunity opportunity = Available().FirstOrDefault(o => o.Definition.Id == storyletId);
                Assert.True(opportunity != null, storyletId + " could not be cast");
                return opportunity;
            }

            internal StoryletPlay Play(string storyletId, ICheckResolver checks = null, ulong seed = 1UL, bool apply = true)
            {
                return Play(Opportunity(storyletId), checks, seed, apply);
            }

            internal StoryletPlay Play(StoryletOpportunity opportunity, ICheckResolver checks, ulong seed, bool apply = true)
            {
                StoryletRouter router = new StoryletRouter(Realizer, checks ?? new VanillaStyleCheckResolver(Lab.Vanilla));
                return router.Play(opportunity, new StoryletPlayContext(Lab.World, Lab.Vanilla, Lab.Situation.Thread)
                {
                    Rng = new DeterministicRng(seed),
                    ApplyConsequences = apply
                });
            }

            /// <summary>What the speaker of one beat decided, taken from a real play of the scene.</summary>
            internal IntentChoice Choose(string storyletId, string beatId)
            {
                StoryletPlay play = Play(storyletId, new AlwaysResolver(CheckOutcome.Pass), 3UL, apply: false);
                PlayedBeat beat = play.Beats.FirstOrDefault(b => b.BeatId == beatId);
                Assert.True(beat != null, beatId + " was never reached in " + storyletId);
                Assert.True(beat.Choice != null, beatId + " had nobody deciding anything");
                return beat.Choice;
            }

            internal SpeechActType Decide(string storyletId, string beatId)
            {
                IntentChoice choice = Choose(storyletId, beatId);
                Assert.True(choice.Spoke, beatId + " produced no act");
                return choice.Act.Type;
            }

            internal SpeechAct ActOf(string storyletId, string beatId)
            {
                StoryletPlay play = Play(storyletId, new AlwaysResolver(CheckOutcome.Pass), 3UL, apply: false);
                PlayedBeat beat = play.Beats.FirstOrDefault(b => b.BeatId == beatId && b.Act != null);
                Assert.True(beat != null, beatId + " said nothing in " + storyletId);
                return beat.Act;
            }
        }
    }
}
