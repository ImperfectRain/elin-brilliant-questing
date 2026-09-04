using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Content;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-069. Five words that are easy to collapse into one, and the tests that stop them.
    ///
    /// The step exists because a storylet system with no development layer becomes a quest
    /// generator by accident: something happens, a scene is looked for, and if a scene is found it
    /// is offered, so "the world holds an unresolved matter" and "the player has been given
    /// something to do" quietly become the same sentence. The layer's whole job is to make the
    /// first of those a thing that can exist without the second.
    ///
    /// So the file is organised around keeping the five apart:
    /// an <c>WorldEvent</c> is history and never stops being true; a <see cref="Development"/> is a
    /// reading of the present and stops being derived the moment the state behind it changes; a
    /// <c>NarrativeThread</c> is a durable matter with identity, a schedule and a save entry; a
    /// <c>StoryletDefinition</c> is an authored pattern that exists with no world attached; and a
    /// scene is one playable presentation, which can be impossible while every one of the others
    /// is still perfectly intact.
    /// </summary>
    public class DevelopmentLayerTests
    {
        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// The step's condition: a Development exists that never becomes a scene and never becomes
        /// a quest, and the world is still coherent.
        ///
        /// The pressure is a real one - an open favour somebody owes - and it is not kept away
        /// from the dramatic machinery to make the point. It names a live thread, it is handed
        /// straight to the storylet engine, and it still produces nothing, because a storylet
        /// builds roles around a claim and this pressure is not about a claim. Nothing is invented
        /// to give it somewhere to go: no thread is opened, no fact is authored, no event is
        /// appended, and the thread it names records no firing. It simply remains something the
        /// world is holding.
        /// </summary>
        [Fact]
        public void APressureCanExistThatNeverBecomesASceneAndNeverBecomesAQuest()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            SocialObligation favor = RecordFavor(lab, lab.Situation.ThiefId, lab.Situation.VictimId);

            int eventsBefore = lab.World.Ledger.Events.Count;
            int factsBefore = lab.World.Knowledge.Facts.Count;
            int threadsBefore = lab.World.Threads.Count;

            Development pressure = Assert.Single(
                DevelopmentDetector.Detect(lab.World),
                d => d.HasPressure(DevelopmentPressures.UnmetObligation));

            // It is genuinely part of the same matter - it knows the thread and the place the
            // favour was earned in - and it still cannot be dramatised, because it has no focus
            // fact for roles to hang off. The boundary is the storylet engine's own requirement,
            // not a policy that hides this development from it.
            Assert.Equal(lab.Situation.Thread.Id, pressure.ThreadId);
            Assert.Contains(lab.Zone, pressure.SiteIds);
            Assert.Equal(EntityId.None, pressure.FocusFactId);
            Assert.False(pressure.CanBeExpressedAsStorylet);
            Assert.Empty(DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, pressure));

            // ...and the world is exactly as it was. No quest, no thread, no authored truth.
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);
            Assert.Equal(factsBefore, lab.World.Knowledge.Facts.Count);
            Assert.Equal(threadsBefore, lab.World.Threads.Count);
            Assert.Empty(lab.Situation.Thread.StoryletFirings);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);

            // Coherent means the rest of the layer still works around it: the theft's own pressure
            // is in the same list and does reach the dramatic machinery.
            Development theft = Assert.Single(
                DevelopmentDetector.Detect(lab.World),
                d => d.FocusFactId == lab.Situation.TheftFactId);
            Assert.True(theft.CanBeExpressedAsStorylet);
            Assert.NotEmpty(DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, theft));

            // And the pressure ends the only way a derived reading can: the state behind it
            // changes. Nothing resolves the development, because there is nothing there to resolve.
            favor.Fulfill(lab.Vanilla.Now);
            Assert.DoesNotContain(
                DevelopmentDetector.Detect(lab.World),
                d => d.HasPressure(DevelopmentPressures.UnmetObligation));
        }

        // -- the five stay distinct --------------------------------------------------------------

        /// <summary>
        /// One theft, and all five concepts asked the question only they can answer.
        ///
        /// The thief and the witness are killed, which makes the scene unplayable and nothing
        /// else: the event still happened, the thread is still a live matter, the storylet is
        /// still an authored pattern, and the pressure the world is under has not gone anywhere -
        /// a secret nobody can prove is not less unresolved because the people involved are dead.
        /// If any of the five were quietly the same object, this is where they would move together.
        /// </summary>
        [Fact]
        public void EventDevelopmentThreadStoryletAndSceneAnswerDifferentQuestions()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();

            Development before = Assert.Single(
                DevelopmentDetector.Detect(lab.World),
                d => d.FocusFactId == lab.Situation.TheftFactId);
            Assert.NotEmpty(DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, before));

            lab.Vanilla.Kill(lab.Situation.ThiefId);
            lab.Vanilla.Kill(lab.Situation.WitnessId);

            // Scene: gone. Nobody is left to take it up with.
            Assert.False(SceneStatus.Check(lab.World, lab.Vanilla, lab.Situation.Thread, EntityId.None).IsPlayable);
            Assert.Empty(DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, before));

            // Development: unchanged. The pressure is a reading of narrative state, and no part of
            // it asks the game who is still standing.
            Development after = Assert.Single(
                DevelopmentDetector.Detect(lab.World),
                d => d.FocusFactId == lab.Situation.TheftFactId);
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.Urgency, after.Urgency);
            Assert.Equal(before.SubjectIds, after.SubjectIds);

            // Event: history, untouched by any of it.
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.Theft);

            // Thread: still the durable matter, with its own identity and schedule.
            Assert.True(lab.Situation.Thread.IsLive);
            Assert.NotEmpty(lab.Situation.Thread.Escalation);

            // Storylet: an authored pattern, which never depended on any of this. The library
            // grows; that it is unmoved by the development layer is the claim, not its size.
            Assert.NotEmpty(ShippedDefinitions());
        }

        /// <summary>
        /// Not a wrapper around the ledger. Developments are keyed by the pressure, so a matter
        /// with a long history is one pressure with several origins, and the great majority of
        /// what happens in a world creates no pressure at all.
        /// </summary>
        [Fact]
        public void ADevelopmentIsNotAnEventWrapper()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            IReadOnlyList<Development> before = DevelopmentDetector.Detect(lab.World);

            // Five more things happen around the same people. Nothing about the pressure changes,
            // because none of them changed who believes what, or what anybody can prove.
            for (int i = 0; i < 5; i++)
            {
                lab.World.Record(
                    WorldEventType.Conversed,
                    lab.Situation.VictimId,
                    lab.Situation.WitnessId,
                    lab.Vanilla.Now,
                    zone: lab.Zone,
                    threadId: lab.Situation.Thread.Id);
            }

            IReadOnlyList<Development> after = DevelopmentDetector.Detect(lab.World);
            Assert.Equal(before.Select(d => d.Id), after.Select(d => d.Id));

            // Six events, one pressure, and the one pressure keys on the fact rather than on any
            // of them - the theft is where it comes from, not what it is.
            Assert.True(lab.World.Ledger.Events.Count > after.Count);
            Assert.Equal("dev.unproven_knowledge:" + lab.Situation.TheftFactId.Value, after[0].Id);
            Assert.Equal(
                lab.World.Ledger.Events.Single(e => e.Type == WorldEventType.Theft).Id,
                Assert.Single(after[0].OriginEventIds));
        }

        /// <summary>
        /// Not a second thread system. Developments do not stand one-to-one with threads, they own
        /// no lifecycle, and the type is built so it could not acquire one: it cannot be
        /// constructed outside the detector, nothing on it can be set, and the world has no
        /// property to hang one off.
        /// </summary>
        [Fact]
        public void ADevelopmentIsNotASecondThreadSystem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // One thread, two facts, one pressure: an ownership record everybody may repeat is not
            // a matter waiting on anyone, so only the secret produces anything.
            Assert.Equal(2, lab.Situation.Thread.FactIds.Count);
            Development only = Assert.Single(DevelopmentDetector.Detect(lab.World));
            Assert.Equal(lab.Situation.TheftFactId, only.FocusFactId);

            // Resolving the thread does not resolve the pressure - the secret is still unproven -
            // which is the clearest sign the two are not the same record under two names.
            lab.Situation.Thread.State = ThreadState.Resolved;
            Assert.Single(DevelopmentDetector.Detect(lab.World));

            // It stops being derived when the state that made it changes, and only then.
            Fact theft = lab.World.Knowledge.GetFact(lab.Situation.TheftFactId);
            theft.Secrecy = 0;
            Assert.Empty(DevelopmentDetector.Detect(lab.World));

            Assert.Empty(typeof(Development).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            foreach (PropertyInfo property in typeof(Development).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.Null(property.GetSetMethod());
            }

            Assert.Null(typeof(NarrativeWorldState).GetProperty("Developments"));
            foreach (string threadOnly in new[] { "State", "Tension", "Escalation", "CompletedSteps", "OpenQuestions" })
            {
                Assert.Empty(typeof(Development).GetMember(threadOnly, BindingFlags.Public | BindingFlags.Instance));
            }
        }

        // -- persistence -------------------------------------------------------------------------

        /// <summary>
        /// The persistence decision, stated as a test: developments are not stored, because
        /// storing them would put a derived reading into the save to race the state it was read
        /// from. They survive a reload the honest way - the same authoritative state derives the
        /// same pressures, in the same order, with the same urgency.
        /// </summary>
        [Fact]
        public void DevelopmentsAreNotSavedAndAreRederivedIdenticallyAfterAReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            RecordFavor(lab, lab.Situation.ThiefId, lab.Situation.VictimId);

            IReadOnlyList<Development> before = DevelopmentDetector.Detect(lab.World);
            Assert.Equal(2, before.Count);

            string json = WorldStateSerializer.Save(lab.World);
            Assert.DoesNotContain("development", json, StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<Development> after = DevelopmentDetector.Detect(WorldStateSerializer.Load(json));

            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Id, after[i].Id);
                Assert.Equal(before[i].PressureTags, after[i].PressureTags);
                Assert.Equal(before[i].Urgency, after[i].Urgency);
                Assert.Equal(before[i].ThreadId, after[i].ThreadId);
                Assert.Equal(before[i].FocusFactId, after[i].FocusFactId);
                Assert.Equal(before[i].SubjectIds, after[i].SubjectIds);
                Assert.Equal(before[i].SiteIds, after[i].SiteIds);
                Assert.Equal(before[i].OriginEventIds, after[i].OriginEventIds);
            }
        }

        /// <summary>
        /// Detection is a read. It appends nothing, authors nothing, opens nothing, and returns
        /// the same answer however often it is asked - which is the property that lets a composer
        /// call it whenever it likes without the act of looking changing the world.
        /// </summary>
        [Fact]
        public void DetectingPressureChangesNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            RecordFavor(lab, lab.Situation.ThiefId, lab.Situation.VictimId);

            string before = WorldStateSerializer.Save(lab.World);
            IReadOnlyList<Development> first = DevelopmentDetector.Detect(lab.World);
            IReadOnlyList<Development> second = DevelopmentDetector.Detect(lab.World);
            string after = WorldStateSerializer.Save(lab.World);

            Assert.Equal(before, after);
            Assert.Equal(first.Select(d => d.Id), second.Select(d => d.Id));
        }

        // -- the bridge, and what it is not ------------------------------------------------------

        /// <summary>
        /// The one seam to the dramatic layer (CD §37, step 7 to step 8). A development hands the
        /// storylet engine the thread and the focus it already names, and adds no selection of its
        /// own: what comes back is exactly what the engine finds when asked directly.
        ///
        /// The second half is the part that matters for this step. Playing a scene about a
        /// pressure does not settle the pressure - the thread records a firing, and the world is
        /// still under exactly the same reading afterwards, because a scene is a presentation and
        /// not a resolution.
        /// </summary>
        [Fact]
        public void ADevelopmentReachesStoryletsWithoutDecidingAnythingAndWithoutBeingSpentByThem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();

            Development development = Assert.Single(DevelopmentDetector.Detect(lab.World));
            Assert.True(development.HasPressure(DevelopmentPressures.UnprovenKnowledge));
            Assert.True(development.HasPressure(DevelopmentPressures.Contested));
            Assert.Equal(lab.Situation.Thread.Id, development.ThreadId);

            IReadOnlyList<StoryletOpportunity> throughDevelopment =
                DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, development);
            IReadOnlyList<StoryletOpportunity> directly = engine.Find(
                new StoryletCastingContext(lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId));

            Assert.Equal(
                directly.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal),
                throughDevelopment.Select(o => o.Definition.Id).OrderBy(id => id, StringComparer.Ordinal));
            Assert.NotEmpty(throughDevelopment);

            engine.Fire(throughDevelopment[0], lab.Situation.Thread, lab.Vanilla.Now);

            Assert.Single(lab.Situation.Thread.StoryletFirings);
            Development stillThere = Assert.Single(DevelopmentDetector.Detect(lab.World));
            Assert.Equal(development.Id, stillThere.Id);
            Assert.Equal(development.Urgency, stillThere.Urgency);
        }

        /// <summary>
        /// The existing casting and firing behaviour is unchanged by the layer sitting above it:
        /// the five shipped storylets still fire on the one theft, still record five distinct
        /// firings, and still author nothing. BQ-065 through BQ-068 own these properties; this is
        /// here so a change to the development layer that quietly reached into them fails in the
        /// file that made the change.
        /// </summary>
        [Fact]
        public void TheDevelopmentLayerLeavesStoryletCastingAndFiringExactlyAsItWas()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            StoryletEngine engine = ShippedEngine();
            int factsBefore = lab.World.Knowledge.Facts.Count;
            int eventsBefore = lab.World.Ledger.Events.Count;

            Development development = Assert.Single(DevelopmentDetector.Detect(lab.World));
            IReadOnlyList<StoryletOpportunity> opportunities =
                DevelopmentExpression.Opportunities(engine, lab.World, lab.Vanilla, development);

            Assert.True(opportunities.Count >= 5, "only " + opportunities.Count + " scenes were available");
            foreach (StoryletOpportunity opportunity in opportunities)
            {
                Assert.True(opportunity.IsAvailable);
                Assert.NotEmpty(opportunity.RoleBindings);
                engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);
            }

            Assert.Equal(opportunities.Count, lab.Situation.Thread.StoryletFirings.Count);
            Assert.Equal(
                opportunities.Count,
                lab.Situation.Thread.StoryletFirings.Select(f => f.StoryletId).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(factsBefore, lab.World.Knowledge.Facts.Count);
            Assert.Equal(eventsBefore, lab.World.Ledger.Events.Count);
        }

        /// <summary>
        /// The inspector answers the question no other dump can: what is unresolved right now, and
        /// would any of it reach a player. A pressure that can reach nobody is printed like any
        /// other, because it is not a defect.
        /// </summary>
        [Fact]
        public void TheInspectorPrintsPressureThatCanReachAPlayerAndPressureThatCannot()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            RecordFavor(lab, lab.Situation.ThiefId, lab.Situation.VictimId);

            string dump = NarrativeInspector.DescribeDevelopments(lab.World);

            Assert.Contains("developments: 2", dump);
            Assert.Contains("dev.unproven_knowledge:" + lab.Situation.TheftFactId.Value, dump);
            Assert.Contains("dev.unmet_obligation:", dump);
            Assert.Contains("a storylet could be looked for", dump);
            Assert.Contains("no storylet can be looked for", dump);
            Assert.Contains("no focus fact", dump);
        }

        // -- helpers -----------------------------------------------------------------------------

        private static SocialObligation RecordFavor(TheftLaboratory lab, EntityId debtor, EntityId creditor)
        {
            WorldEvent source = lab.World.Record(
                WorldEventType.FavorOwed,
                debtor,
                creditor,
                lab.Vanilla.Now,
                0.5,
                lab.Zone,
                threadId: lab.Situation.Thread.Id);

            return lab.World.Obligations.Add(new SocialObligation(
                lab.World.NewId("obl"),
                SocialObligationKind.Favor,
                debtor,
                creditor,
                EntityId.None,
                "owes a favour",
                lab.Vanilla.Now,
                source.Id));
        }

        private static StoryletEngine ShippedEngine()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine engine = StoryletContent.CreateEngine(ShippedBundle(), out diagnostics);
            Assert.Empty(diagnostics);
            return engine;
        }

        private static IReadOnlyList<StoryletDefinition> ShippedDefinitions()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<StoryletDefinition> definitions = StoryletContent.LoadDefinitions(ShippedBundle(), out diagnostics);
            Assert.Empty(diagnostics);
            return definitions;
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
