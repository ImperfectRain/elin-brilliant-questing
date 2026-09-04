using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-066. The five authored storylets, played against the one theft the laboratory stages.
    ///
    /// The step's done-when has two halves and this file is both of them. That all five *fire* is
    /// the easy half. The half worth testing is that they are five scenes rather than one scene
    /// with five vocabularies: the difference has to live in what each one requires, who it needs,
    /// what happens in it and what it leaves behind - not in its wording, which Core never sees.
    ///
    /// Every scene here is cast from the place alone (<see cref="StoryletCastingContext"/> with no
    /// <c>Actor</c> and no <c>Target</c>), because a caller who names people would be proving its
    /// own arithmetic rather than the content's.
    ///
    /// The other half of the file is the rule the storylets exist under: they dramatize what the
    /// world already holds and never author it. So the interesting cases are the ones where the
    /// world stops holding it - a theft no longer true, a focus no longer part of the thread, an
    /// ownership record that has lapsed, the only witness two towns away - and in every one of
    /// them the scenes that depended on it stop rather than supply the missing fact themselves.
    /// </summary>
    public class FirstFiveStoryletTests
    {
        private static readonly string[] FirstFive =
        {
            "storylet.public_accusation",
            "storylet.private_confrontation",
            "storylet.request_for_help",
            "storylet.confession",
            "storylet.gossip"
        };

        /// <summary>The two of the five whose scene is about somebody's loss and so needs an owner.</summary>
        private static readonly string[] NeedAnInjuredParty =
        {
            "storylet.request_for_help",
            "storylet.confession"
        };

        [Fact]
        public void AllFiveFireOnTheOneTheftAndEachRecordsADifferentScene()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();

            List<StoryletOpportunity> opportunities = OnlyTheFive(engine.Find(Casting(lab)));

            Assert.Equal(
                FirstFive.OrderBy(id => id, StringComparer.Ordinal),
                opportunities.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal));

            foreach (StoryletOpportunity opportunity in opportunities)
            {
                engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);
            }

            IReadOnlyList<StoryletFiring> firings = lab.Situation.Thread.StoryletFirings;
            Assert.Equal(5, firings.Count);
            Assert.Equal(5, firings.Select(f => f.StoryletId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(firings, f => Assert.Equal(lab.Situation.TheftFactId, f.FocusFactId));

            // What was actually recorded, scene by scene: who was in it, what happened, what it
            // left behind. Nothing here is a wording difference - Core holds no wording.
            foreach (StoryletFiring firing in firings)
            {
                Assert.True(firing.RoleBindings.Count >= 2, firing.StoryletId + " is a scene with nobody in it");
                Assert.True(firing.BeatIds.Count >= 4, firing.StoryletId + " has no shape to play");
                Assert.NotEmpty(firing.ConsequenceHookIds);
            }

            for (int i = 0; i < firings.Count; i++)
            {
                for (int j = i + 1; j < firings.Count; j++)
                {
                    StoryletFiring left = firings[i];
                    StoryletFiring right = firings[j];
                    string pair = left.StoryletId + " and " + right.StoryletId;

                    // Different people are wanted for different things: no two of the five ask
                    // for the same set of roles.
                    Assert.False(
                        left.RoleBindings.Keys.OrderBy(k => k, StringComparer.Ordinal)
                            .SequenceEqual(right.RoleBindings.Keys.OrderBy(k => k, StringComparer.Ordinal)),
                        pair + " cast the same roles");

                    // Not merely a different order of the same moments: no beat is shared at all.
                    Assert.Empty(left.BeatIds.Intersect(right.BeatIds, StringComparer.Ordinal));

                    // And each leaves its own mark, so the consequence layer can tell which scene
                    // the town actually witnessed.
                    Assert.Empty(left.ConsequenceHookIds.Intersect(right.ConsequenceHookIds, StringComparer.Ordinal));
                }
            }
        }

        [Fact]
        public void TheFiveDifferInWhatTheyRequireAndNotOnlyInWhatTheyPlay()
        {
            IReadOnlyList<StoryletDefinition> five = ShippedDefinitions();

            // Two genuinely different requirement shapes, not five spellings of one. Three scenes
            // need somebody who knows and the person the fact is about; two need the injured party
            // as well, which is the difference the engine can actually refuse on - see
            // OnlyTheScenesAboutSomebodysLossStopWhenTheWorldRecordsNoOwner.
            foreach (StoryletDefinition definition in five)
            {
                bool needsOwner = NeedAnInjuredParty.Contains(definition.Id, StringComparer.Ordinal);
                Assert.Equal(
                    needsOwner,
                    definition.RequiredRoles.Any(r => r.Source == StoryletRoleSource.OwnerOfFocusObject));
                Assert.Equal(needsOwner ? 3 : 2, definition.RequiredRoles.Count);

                // Every scene is about the person the fact is about, and needs somebody the world
                // says actually knows it. Neither is ever a role the storylet supplies itself.
                Assert.Contains(definition.RequiredRoles, r => r.Source == StoryletRoleSource.FactSubject);
                Assert.Contains(definition.RequiredRoles, r => r.Source == StoryletRoleSource.AnyoneWhoKnowsFocus);

                // The theft is the subject of all five, and all five insist it is true.
                Assert.Contains(
                    definition.Preconditions,
                    p => p.Kind == StoryletPreconditionKind.FocusPredicate && p.Value == FactPredicates.Stole);
                Assert.Contains(
                    definition.Preconditions,
                    p => p.Kind == StoryletPreconditionKind.FocusTruth && p.Value == TruthState.True.ToString());
                Assert.Contains(definition.Preconditions, p => p.Kind == StoryletPreconditionKind.FactBelongsToThread);
            }

            // Beats and hooks are disjoint across the whole set, and every scene is filed under
            // its own situation and its own tone.
            Assert.Equal(
                five.Sum(d => d.Beats.Count),
                five.SelectMany(d => d.Beats.Select(b => b.Id)).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                five.Sum(d => d.ConsequenceHooks.Count),
                five.SelectMany(d => d.ConsequenceHooks.Select(h => h.Id)).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(5, five.Select(d => string.Join("+", d.SituationTags)).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(5, five.Select(d => string.Join("+", d.ToneTags)).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(5, five.Select(d => d.Beats.Count + ":" + string.Join(",", d.RequiredRoles.Select(r => r.Id))).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void OnlyTheScenesAboutSomebodysLossStopWhenTheWorldRecordsNoOwner()
        {
            // The ownership record lapses - the ring changed hands, or the claim was withdrawn.
            // Nobody is now recorded as having lost anything, so the two scenes built around the
            // loss have no injured party to cast and say so. They do not fall back to the ring.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            lab.World.Knowledge.GetFact(lab.Situation.OwnershipFactId).Truth = TruthState.False;

            List<StoryletOpportunity> opportunities = OnlyTheFive(engine.Find(Casting(lab)));

            Assert.Equal(
                FirstFive.Except(NeedAnInjuredParty, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal),
                opportunities.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal));

            foreach (StoryletDefinition definition in ShippedDefinitions().Where(d => NeedAnInjuredParty.Contains(d.Id, StringComparer.Ordinal)))
            {
                StoryletOpportunity refused = StoryletEngine.Evaluate(definition, Casting(lab));

                Assert.False(refused.IsAvailable);
                Assert.Contains("cannot be cast", refused.RefusalReason);
                Assert.Empty(refused.RoleBindings);
            }

            // The three that still play never named the stolen thing as a person to make up the
            // difference; every role any of them holds is somebody the registry knows.
            foreach (StoryletOpportunity opportunity in opportunities)
            {
                Assert.DoesNotContain(lab.Situation.ItemId, opportunity.RoleBindings.Values);
                Assert.All(opportunity.RoleBindings.Values, actor => Assert.NotNull(lab.World.Registry.GetNpc(actor)));
            }
        }

        [Fact]
        public void NoSceneFiresOnATheftTheWorldNoLongerHoldsTrue()
        {
            // A storylet dramatizes a fact; it is not evidence for one. When the theft stops being
            // true the scenes stop, rather than continuing to stage an accusation that would be
            // the only thing left asserting it.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            lab.World.Knowledge.GetFact(lab.Situation.TheftFactId).Truth = TruthState.Uncertain;

            Assert.Empty(engine.Find(Casting(lab)));
            AssertEveryoneRefuses(lab, "focus truth is not True");
            Assert.Empty(lab.Situation.Thread.StoryletFirings);
        }

        [Fact]
        public void NoSceneFiresOnceTheFocusHasLeftTheThread()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            lab.Situation.Thread.FactIds.Remove(lab.Situation.TheftFactId);

            Assert.Empty(engine.Find(Casting(lab)));
            AssertEveryoneRefuses(lab, "no longer part of the thread");
            Assert.Empty(lab.Situation.Thread.StoryletFirings);
        }

        [Fact]
        public void NoSceneInventsSomebodyWhoKnowsWhenTheOnlyWitnessIsElsewhere()
        {
            // All five need a mouth that already knows: an accuser, a challenger, a requester, a
            // listener, a gossip. The thief knows too, and is taken by the role the fact names, so
            // with the witness out of town there is nobody left - and no scene supplies one.
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            lab.Vanilla.SetZone(lab.Situation.WitnessId, lab.World.NewId("zone"));

            Assert.Empty(OnlyTheFive(engine.Find(Casting(lab))));
            AssertEveryoneRefuses(lab, "cannot be cast");

            // Bring her home and every one of them is playable again: the refusal was about the
            // world, not about the content.
            lab.Vanilla.SetZone(lab.Situation.WitnessId, lab.Zone);
            Assert.Equal(5, OnlyTheFive(engine.Find(Casting(lab))).Count);
        }

        [Fact]
        public void PlayingAllFiveAuthorsNoFactAndNoEvent()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            Fact theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);
            int factsBefore = lab.World.Knowledge.Facts.Count;
            int eventsBefore = lab.World.Ledger.Events.Count;
            int knowersBefore = lab.World.Knowledge.Knowers(theft.Id).Count();

            foreach (StoryletOpportunity opportunity in OnlyTheFive(engine.Find(Casting(lab))))
            {
                engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);
            }

            Assert.Equal(5, lab.Situation.Thread.StoryletFirings.Count);
            Assert.Equal(factsBefore, lab.World.Knowledge.Facts.Count);
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);

            // Five scenes about the theft, and the theft itself is exactly as it was: same truth,
            // same subject, same object, and not one person newly made to know it. Whether a scene
            // spreads what it says is the consequence layer's answer, not the storylet's.
            Assert.Equal(TruthState.True, theft.Truth);
            Assert.Equal(lab.Situation.ThiefId, theft.Subject);
            Assert.Equal(lab.Situation.ItemId, theft.Object);
            Assert.Equal(knowersBefore, lab.World.Knowledge.Knowers(theft.Id).Count());
        }

        /// <summary>
        /// The five, out of whatever else the shipped library now offers on a theft.
        ///
        /// This file is BQ-066's proof about five particular scenes, and it used to enumerate the
        /// whole result because five was all there was. Keeping that would make every future
        /// storylet a failing test here while proving nothing about these five.
        /// </summary>
        private static List<StoryletOpportunity> OnlyTheFive(IReadOnlyList<StoryletOpportunity> opportunities)
        {
            return opportunities.Where(o => FirstFive.Contains(o.Definition.Id, StringComparer.Ordinal)).ToList();
        }

        private static void AssertEveryoneRefuses(TheftLaboratory lab, string reason)
        {
            foreach (StoryletDefinition definition in ShippedDefinitions())
            {
                StoryletOpportunity opportunity = StoryletEngine.Evaluate(definition, Casting(lab));

                Assert.False(opportunity.IsAvailable, definition.Id + " fired anyway");
                Assert.Contains(reason, opportunity.RefusalReason);
            }
        }

        /// <summary>The scene cast from the place alone: a thread, a fact and a town, nobody named.</summary>
        private static StoryletCastingContext Casting(TheftLaboratory lab)
        {
            return new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId);
        }

        private static IReadOnlyList<StoryletDefinition> ShippedDefinitions()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<StoryletDefinition> definitions = StoryletContent.LoadDefinitions(ShippedBundle(), out diagnostics);

            Assert.Empty(diagnostics);
            List<StoryletDefinition> five = definitions
                .Where(d => FirstFive.Contains(d.Id, StringComparer.Ordinal))
                .ToList();
            Assert.Equal(FirstFive.Length, five.Count);
            return five;
        }

        private static StoryletEngine ShippedEngine()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine engine = StoryletContent.CreateEngine(ShippedBundle(), out diagnostics);
            Assert.Empty(diagnostics);
            return engine;
        }

        private static ContentBundle ShippedBundle()
        {
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);
            return bundle.Bundle;
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
