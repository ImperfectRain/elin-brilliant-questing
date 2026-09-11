using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-002. Which stream decided what, proved at the call sites that actually decide.
    ///
    /// The claim is not that <see cref="DeterministicRng.Fork"/> works - <c>FoundationTests</c>
    /// already proves a fork is independent of its parent's draw order. It is that the production
    /// route keeps the boundary the fork makes available: that rendering a scene, rendering it out
    /// of a different library, or allocating identities for what was delivered cannot move a check,
    /// an actor's decision or where the scene went; that looking at a scene and walking away costs
    /// nothing; and that a second genuine firing is a second attempt rather than a replay of the
    /// first, across a save and back.
    ///
    /// Everything runs over the shipped bundle through the production entry points, and the action
    /// context is built the way the live adapter builds it - drawing from the world's own persisted
    /// stream - so that a scene and an attempt made afterwards share exactly what they share in
    /// game.
    /// </summary>
    public class RngIsolationTests
    {
        private const string Accusation = "storylet.public_accusation";

        // -- expression cannot move simulation ------------------------------------------------------

        /// <summary>
        /// The same scene, once with somebody rendering it and once with nobody, decides the same
        /// things: the same beats, the same acts, the same rolls, the same history.
        /// </summary>
        [Fact]
        public void RenderingAScenesWordsDoesNotMoveItsDecisions()
        {
            SceneRun spoken = Run(words: true);
            SceneRun silent = Run(words: false);

            Assert.NotEmpty(spoken.Lines);
            Assert.Empty(silent.Lines);
            AssertSameSimulation(silent, spoken);
        }

        /// <summary>
        /// Changing how much wording there is to choose between changes the wording and how many
        /// times the expression stream is drawn from, and nothing else. Content is edited between
        /// releases; a check that moved when a fragment was added would make every content change a
        /// balance change.
        /// </summary>
        [Fact]
        public void DrawingDifferentWordsADifferentNumberOfTimesDoesNotMoveTheDecisions()
        {
            SceneRun full = Run(words: true);
            SceneRun narrower = Run(words: true, narrowLibrary: true);

            Assert.NotEmpty(narrower.Lines);
            Assert.NotEqual(full.Lines, narrower.Lines);
            AssertSameSimulation(full, narrower);
        }

        /// <summary>
        /// Identities allocated for what was delivered - the records a presentation layer writes
        /// about having said something - do not move the scene that follows them. Nothing keys a
        /// simulation stream on an event id, and this is what says so.
        /// </summary>
        [Fact]
        public void AllocatingDeliveryIdentitiesDoesNotMoveTheScene()
        {
            SceneRun plain = Run(words: true);
            SceneRun delivered = Run(words: true, deliveries: 7);

            Assert.Equal(7, delivered.IdsAllocatedBeforePlay);
            AssertSameSimulation(plain, delivered);
        }

        /// <summary>
        /// And the attempt made after the scene lands in the same place. The action library draws
        /// from the world's own stream, so this is the one comparison that would fail if any part
        /// of rendering had drawn from it too.
        /// </summary>
        [Fact]
        public void WhatTheWorldDoesNextIsTheSameWhetherOrNotTheSceneWasRendered()
        {
            SceneRun spoken = Run(words: true, thenExpose: true);
            SceneRun silent = Run(words: false, thenExpose: true);

            Assert.True(spoken.ExposeRoll >= 0, "the attempt afterwards never rolled");
            Assert.Equal(silent.ExposeRoll, spoken.ExposeRoll);
            Assert.Equal(silent.ExposeOutcome, spoken.ExposeOutcome);
            Assert.Equal(silent.RngStateAfter, spoken.RngStateAfter);
        }

        // -- inspection costs nothing ---------------------------------------------------------------

        /// <summary>
        /// Looking at a scene and not playing it consumes no draw, no identity and no history -
        /// and reopening the surface asks the same question again rather than being handed a fresh
        /// roll, which is what would make a refused answer worth reopening for.
        /// </summary>
        [Fact]
        public void ReopeningASceneWithoutPlayingItGainsNothing()
        {
            Fixture fixture = Fixture.Create();
            ulong stream = fixture.Lab.World.Rng.State;
            int events = fixture.Lab.World.Ledger.Count;
            IReadOnlyList<string> ids = Counters(fixture.Lab.World);

            IReadOnlyList<int> first = Rolls(fixture.Play(apply: false));
            IReadOnlyList<int> second = Rolls(fixture.Play(apply: false));
            IReadOnlyList<int> third = Rolls(fixture.Play(apply: false));

            Assert.NotEmpty(first);
            Assert.Equal(first, second);
            Assert.Equal(first, third);

            Assert.Empty(fixture.Thread.StoryletFirings);
            Assert.Equal(stream, fixture.Lab.World.Rng.State);
            Assert.Equal(events, fixture.Lab.World.Ledger.Count);
            Assert.Equal(ids, Counters(fixture.Lab.World));
        }

        /// <summary>
        /// An inspection does not age the occurrence either: the scene that is then genuinely
        /// played is the one that was inspected, not the next one along.
        /// </summary>
        [Fact]
        public void InspectingASceneDoesNotUseUpItsFirstOccurrence()
        {
            Fixture fixture = Fixture.Create();

            IReadOnlyList<int> inspected = Rolls(fixture.Play(apply: false));
            IReadOnlyList<int> played = Rolls(fixture.Play(apply: true));

            Assert.Equal(inspected, played);
            Assert.Single(fixture.Thread.StoryletFirings);
        }

        /// <summary>
        /// Discovering what could be attempted - the read-only half of every surface the player
        /// opens - spends nothing either, however many verbs it has to ask about.
        /// </summary>
        [Fact]
        public void DiscoveringWhatCouldBeAttemptedSpendsNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            ActionContext context = LiveContext(lab, lab.Situation.VictimId);
            ulong stream = lab.World.Rng.State;
            int events = lab.World.Ledger.Count;
            IReadOnlyList<string> ids = Counters(lab.World);

            List<ActionOffer> offers = lab.Actions.Discover(context, includeUnavailable: true);
            foreach (ActionOffer offer in offers)
            {
                lab.Actions.Get(offer.Action.Id).GetAvailability(context);
            }

            Assert.NotEmpty(offers);
            Assert.Equal(stream, lab.World.Rng.State);
            Assert.Equal(events, lab.World.Ledger.Count);
            Assert.Equal(ids, Counters(lab.World));
        }

        // -- a second attempt is a second attempt ---------------------------------------------------

        /// <summary>
        /// The same storylet fired again over the same matter is its own attempt. Without an
        /// occurrence key it would be the first scene again exactly, because a fork is derived from
        /// its parent's seed and knows nothing about how much has happened since.
        /// </summary>
        [Fact]
        public void ASecondGenuineFiringRollsItsOwnDice()
        {
            Fixture fixture = Fixture.Create();

            IReadOnlyList<int> first = Rolls(fixture.Play(apply: true));
            IReadOnlyList<int> second = Rolls(fixture.Play(apply: true));

            Assert.NotEmpty(first);
            Assert.NotEqual(first, second);
            Assert.Equal(2, fixture.Thread.StoryletFirings.Count);
        }

        /// <summary>
        /// And it is a stable attempt: saving between the two firings and reloading continues the
        /// occurrence rather than repeating the first scene, because the count it is keyed on is
        /// thread history the save already carries. No stream state of its own is stored for it.
        /// </summary>
        [Fact]
        public void SavingBetweenFiringsContinuesTheOccurrenceRatherThanRepeatingIt()
        {
            Fixture straightThrough = Fixture.Create();
            Rolls(straightThrough.Play(apply: true));
            IReadOnlyList<int> expected = Rolls(straightThrough.Play(apply: true));

            Fixture reloaded = Fixture.Create();
            Rolls(reloaded.Play(apply: true));
            reloaded.SaveAndReload();

            Assert.Single(reloaded.Thread.StoryletFirings);
            Assert.Equal(expected, Rolls(reloaded.Play(apply: true)));
        }

        /// <summary>
        /// A world restored with no firings recorded - which is what a save written before scenes
        /// were routed reads back as - plays its first occurrence. Nothing is inferred to have
        /// happened before it.
        /// </summary>
        [Fact]
        public void AThreadRestoredWithNoFiringsPlaysItsFirstOccurrence()
        {
            Fixture fresh = Fixture.Create();
            IReadOnlyList<int> first = Rolls(fresh.Play(apply: false));

            Fixture restored = Fixture.Create();
            restored.SaveAndReload();

            Assert.Empty(restored.Thread.StoryletFirings);
            Assert.Equal(first, Rolls(restored.Play(apply: true)));
        }

        // -- helpers --------------------------------------------------------------------------------

        private static void AssertSameSimulation(SceneRun expected, SceneRun actual)
        {
            Assert.NotEmpty(expected.Beats);
            Assert.Equal(expected.Beats, actual.Beats);
            Assert.Equal(expected.Acts, actual.Acts);
            Assert.Equal(expected.Rolls, actual.Rolls);
            Assert.Equal(expected.Outcomes, actual.Outcomes);
            Assert.Equal(expected.Resolution, actual.Resolution);
            Assert.Equal(expected.Hooks, actual.Hooks);
            Assert.Equal(expected.EventTypes, actual.EventTypes);
            Assert.Equal(expected.RngStateAfter, actual.RngStateAfter);
        }

        private static SceneRun Run(bool words, bool narrowLibrary = false, int deliveries = 0, bool thenExpose = false)
        {
            Fixture fixture = Fixture.Create();

            for (int i = 0; i < deliveries; i++)
            {
                fixture.World.ReserveEvent();
            }

            int allocated = deliveries;
            StoryletPlay play = fixture.Play(apply: true, words: words, narrowLibrary: narrowLibrary);
            SceneRun run = SceneRun.Of(fixture, play, allocated);

            if (thenExpose)
            {
                run.Expose(fixture);
            }

            run.RngStateAfter = fixture.World.Rng.State;
            return run;
        }

        private static IReadOnlyList<int> Rolls(StoryletPlay play)
        {
            List<int> rolls = new List<int>();
            foreach (PlayedBeat beat in play.Beats)
            {
                if (beat.Check != null)
                {
                    rolls.Add(beat.Check.Roll);
                }
            }

            return rolls;
        }

        /// <summary>Every id counter as it stands, so spending one anywhere shows up.</summary>
        private static IReadOnlyList<string> Counters(NarrativeWorldState world)
        {
            return world.Ids.Counters
                .Select(pair => pair.Key + "=" + pair.Value)
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The context the live adapter builds: the world's own persisted stream, not a private one.</summary>
        private static ActionContext LiveContext(TheftLaboratory lab, EntityId target)
        {
            return new ActionContext(lab.World, lab.Vanilla, lab.Checks, lab.World.Rng, lab.Player, target)
            {
                Thread = lab.Situation.Thread,
                SubjectFact = lab.Situation.TheftFactId,
                SubjectItem = lab.Situation.ItemId
            };
        }

        /// <summary>What one play decided, kept apart from how it was worded.</summary>
        private sealed class SceneRun
        {
            internal IReadOnlyList<string> Beats { get; private set; }

            internal IReadOnlyList<string> Acts { get; private set; }

            internal IReadOnlyList<int> Rolls { get; private set; }

            internal IReadOnlyList<string> Outcomes { get; private set; }

            internal IReadOnlyList<string> Hooks { get; private set; }

            internal IReadOnlyList<string> EventTypes { get; private set; }

            internal IReadOnlyList<string> Lines { get; private set; }

            internal string Resolution { get; private set; }

            internal int IdsAllocatedBeforePlay { get; private set; }

            internal ulong RngStateAfter { get; set; }

            internal int ExposeRoll { get; private set; } = -1;

            internal string ExposeOutcome { get; private set; } = string.Empty;

            internal static SceneRun Of(Fixture fixture, StoryletPlay play, int allocated)
            {
                List<string> beats = new List<string>();
                List<string> acts = new List<string>();
                List<int> rolls = new List<int>();
                List<string> outcomes = new List<string>();
                List<string> lines = new List<string>();

                foreach (PlayedBeat beat in play.Beats)
                {
                    beats.Add(beat.BeatId + (beat.Played ? "|played" : "|skipped:" + beat.Skipped));
                    acts.Add(beat.Act == null ? "silent" : beat.Act.Signature);
                    if (beat.Check != null)
                    {
                        rolls.Add(beat.Check.Roll);
                        outcomes.Add(beat.BeatId + "|" + beat.Check.Outcome);
                    }

                    if (beat.Line != null && beat.Line.Rendered)
                    {
                        lines.Add(beat.BeatId + "|" + beat.Line.Text);
                    }
                }

                return new SceneRun
                {
                    Beats = beats,
                    Acts = acts,
                    Rolls = rolls,
                    Outcomes = outcomes,
                    Lines = lines,
                    Resolution = play.Resolution,
                    Hooks = new List<string>(play.Firing.ConsequenceHookIds),
                    EventTypes = fixture.World.Ledger.Events.Select(e => e.Type.ToString()).ToList(),
                    IdsAllocatedBeforePlay = allocated
                };
            }

            /// <summary>One ordinary attempt afterwards, drawing from the world stream the way the game does.</summary>
            internal void Expose(Fixture fixture)
            {
                TheftLaboratory lab = fixture.Lab;
                lab.World.Knowledge.Teach(
                    lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

                ActionContext context = LiveContext(lab, lab.Situation.WitnessId);
                ActionOutcome outcome = lab.Actions.Get("expose").Perform(context);

                ExposeRoll = outcome.Check == null ? -1 : outcome.Check.Roll;
                ExposeOutcome = outcome.Check == null ? string.Empty : outcome.Check.Outcome.ToString();
            }
        }

        private sealed class Fixture
        {
            private static ContentBundle _bundle;

            private StoryletEngine _engine;
            private DialogueFragmentLibrary _library;

            internal TheftLaboratory Lab { get; private set; }

            internal NarrativeWorldState World { get; private set; }

            internal NarrativeThread Thread { get; private set; }

            internal static Fixture Create()
            {
                ContentBundle bundle = Bundle();
                IReadOnlyList<ContentDiagnostic> storyletProblems;
                IReadOnlyList<ContentDiagnostic> fragmentProblems;
                StoryletEngine engine = StoryletContent.CreateEngine(bundle, out storyletProblems);
                DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(bundle, out fragmentProblems);

                Assert.Empty(storyletProblems);
                Assert.Empty(fragmentProblems);

                TheftLaboratory lab = TheftLaboratory.Create();
                return new Fixture
                {
                    Lab = lab,
                    World = lab.World,
                    Thread = lab.Situation.Thread,
                    _engine = engine,
                    _library = library
                };
            }

            /// <summary>Reads the world back out of a save, and plays the restored one from then on.</summary>
            internal void SaveAndReload()
            {
                World = WorldStateSerializer.Load(WorldStateSerializer.Save(Lab.World));
                Thread = World.Threads.Single();
            }

            internal StoryletPlay Play(bool apply, bool words = false, bool narrowLibrary = false)
            {
                StoryletOpportunity opportunity = Opportunity();
                DialogueRealizer realizer = !words
                    ? null
                    : new DialogueRealizer(narrowLibrary ? Narrowed(_library) : _library);

                StoryletRouter router = new StoryletRouter(realizer, new VanillaStyleCheckResolver(Lab.Vanilla));
                return router.Play(opportunity, new StoryletPlayContext(World, Lab.Vanilla, Thread)
                {
                    Rng = new DeterministicRng(4242UL),
                    ApplyConsequences = apply
                });
            }

            private StoryletOpportunity Opportunity()
            {
                IReadOnlyList<StoryletOpportunity> found = _engine.Find(
                    new StoryletCastingContext(World, Lab.Vanilla, Thread, Lab.Situation.TheftFactId));

                StoryletOpportunity opportunity = found.FirstOrDefault(o => o.Definition.Id == Accusation);
                Assert.True(opportunity != null, Accusation + " could not be cast");
                return opportunity;
            }

            /// <summary>
            /// The same content with one way of saying each thing taken away, so the expression
            /// stream has a different number of candidates to choose between and draws differently.
            /// </summary>
            private static DialogueFragmentLibrary Narrowed(DialogueFragmentLibrary library)
            {
                DialogueFragmentLibrary narrowed = new DialogueFragmentLibrary();
                foreach (FragmentPosition position in Enum.GetValues(typeof(FragmentPosition)).Cast<FragmentPosition>())
                {
                    IReadOnlyList<DialogueFragment> slot = library.At(position);
                    int keep = slot.Count > 1 ? slot.Count - 1 : slot.Count;
                    for (int i = 0; i < keep; i++)
                    {
                        narrowed.Register(slot[i]);
                    }
                }

                return narrowed;
            }

            private static ContentBundle Bundle()
            {
                if (_bundle != null)
                {
                    return _bundle;
                }

                ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                    Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
                Assert.Empty(loaded.Diagnostics);
                _bundle = loaded.Bundle;
                return _bundle;
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
        }
    }
}
