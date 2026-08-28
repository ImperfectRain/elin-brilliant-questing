using System;
using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class PersistenceTests
    {
        private static TheftLaboratory PlayedScenario()
        {
            TheftLaboratory lab = TheftLaboratory.Create(31337);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("question", lab.Situation.WitnessId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);
            lab.AdvanceDays(6);
            return lab;
        }

        [Fact]
        public void HistoryAndBeliefSurviveARoundTrip()
        {
            TheftLaboratory lab = PlayedScenario();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(lab.World.Ledger.Count, reloaded.Ledger.Count);
            Assert.Equal(lab.World.Knowledge.Facts.Count, reloaded.Knowledge.Facts.Count);
            Assert.Equal(lab.World.Registry.Npcs.Count, reloaded.Registry.Npcs.Count);

            lab.World.Knowledge.TryGetBelief(lab.Player, lab.Situation.TheftFactId, out KnowledgeRecord before);
            reloaded.Knowledge.TryGetBelief(lab.Player, lab.Situation.TheftFactId, out KnowledgeRecord after);

            Assert.NotNull(after);
            Assert.Equal(before.Confidence, after.Confidence, 4);
            Assert.Equal(before.CanProve, after.CanProve);
            Assert.Equal(before.Source, after.Source);
            Assert.Equal(before.Proofs.Count, after.Proofs.Count);
        }

        [Fact]
        public void MemoriesKeepTheAffinityTheyAccountFor()
        {
            TheftLaboratory lab = PlayedScenario();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            int before = lab.World.Memories.AccountedAffinity(lab.Situation.VictimId, lab.Player);
            int after = reloaded.Memories.AccountedAffinity(lab.Situation.VictimId, lab.Player);

            Assert.NotEqual(0, before);
            Assert.Equal(before, after);
        }

        [Fact]
        public void ThreadProgressAndResolutionSurvive()
        {
            TheftLaboratory lab = PlayedScenario();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            NarrativeThread thread = Assert.Single(reloaded.Threads);
            Assert.Equal(lab.Situation.Thread.State, thread.State);
            Assert.Equal(lab.Situation.Thread.Resolution, thread.Resolution);
            Assert.Equal(lab.Situation.Thread.CompletedSteps, thread.CompletedSteps);
            Assert.Equal(lab.Situation.Thread.Escalation.Count, thread.Escalation.Count);
        }

        [Fact]
        public void LoadingASaveDoesNotReapplyFiftyHoursOfConsequences()
        {
            TheftLaboratory lab = PlayedScenario();
            int affinityBefore = lab.Vanilla.GetAffinity(lab.Situation.VictimId);
            int karmaBefore = lab.Vanilla.Karma;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            new ConsequenceEngine(reloaded, lab.Vanilla).Attach();

            Assert.Equal(affinityBefore, lab.Vanilla.GetAffinity(lab.Situation.VictimId));
            Assert.Equal(karmaBefore, lab.Vanilla.Karma);

            // And the restored memories were not duplicated by the restore itself.
            Assert.Single(reloaded.Memories.MemoriesAbout(lab.Situation.VictimId, lab.Player));
        }

        /// <summary>
        /// BQ-010: saving and reloading in the middle of a situation is transparent. Playing on
        /// after a reload must give exactly what playing on without one would have given.
        ///
        /// This is the property BQ-005a made possible. The projector used to draw from
        /// `Rng.Fork("drama")`, and Fork derives from the seed rather than the live state, so the
        /// stream restarted every time the save was opened - the first roll after any reload was
        /// always the roll the session had already made. Drawing from the persisted stream is what
        /// makes the seam invisible, and this is the test that would fail if anyone re-forked it.
        /// </summary>
        [Fact]
        public void PlayingOnAfterAReloadMatchesPlayingOnWithout()
        {
            List<string> uninterrupted = PlayOn(PlayedScenario().World);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(PlayedScenario().World));
            List<string> acrossASave = PlayOn(reloaded);

            Assert.Equal(uninterrupted, acrossASave);
        }

        /// <summary>
        /// The trap itself, pinned where it can be tested.
        ///
        /// Fork is derived from the parent's seed, not its live state, and its own position is
        /// never persisted - so a forked stream is identical however far the parent has advanced,
        /// and restarts from the beginning every time the save is opened. That is correct for its
        /// intended use (replaying a situation from a seed) and wrong for anything that resolves
        /// player actions, which is what BQ-005a had to undo. Anyone tempted to fork a stream for
        /// live play should read this test first.
        /// </summary>
        [Fact]
        public void AForkedStreamIgnoresHowFarTheParentHasGone()
        {
            DeterministicRng parent = new DeterministicRng(1234);
            List<string> early = Draw(parent.Fork("drama"));

            for (int i = 0; i < 50; i++)
            {
                parent.Roll(20);
            }

            Assert.Equal(early, Draw(parent.Fork("drama")));
            Assert.NotEqual(early, Draw(parent));
        }

        private static List<string> Draw(DeterministicRng rng)
        {
            List<string> rolls = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                rolls.Add(rng.Roll(20).ToString());
            }

            return rolls;
        }

        /// <summary>
        /// The bug a tester hit: after reloading, the ring could no longer be given back or kept.
        ///
        /// Identity of anything that is not a person lives only in the adapter's uid map, and the
        /// map was not saved. On reload the adapter minted a fresh id for the same Thing, so it
        /// stopped matching the Possesses fact the whole situation is written against and both
        /// resolution verbs quietly went unavailable. The map now round-trips.
        /// </summary>
        [Fact]
        public void TheAdaptersIdentityMapSurvivesTheSave()
        {
            TheftLaboratory lab = PlayedScenario();
            lab.World.ExternalRefs[lab.Situation.ItemId] = "5091";
            lab.World.ExternalRefs[lab.Situation.ThiefId] = "510";

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal("5091", reloaded.ExternalRefs[lab.Situation.ItemId]);
            Assert.Equal("510", reloaded.ExternalRefs[lab.Situation.ThiefId]);
        }

        /// <summary>
        /// A save written before the map existed has no node at all, and must still load - with
        /// an empty map, which is exactly the state those saves were already in. That is why this
        /// is additive rather than a schema bump.
        /// </summary>
        [Fact]
        public void ASaveWithoutTheMapStillLoads()
        {
            JsonValue old = JsonValue.Object()
                .Set("schemaVersion", NarrativeWorldState.CurrentSchemaVersion)
                .Set("worldSeed", "42")
                .Set("rngState", "42");

            NarrativeWorldState reloaded = WorldStateSerializer.FromJson(old);

            Assert.Empty(reloaded.ExternalRefs);
            Assert.Equal(42UL, reloaded.WorldSeed);
        }

        /// <summary>The ledger must not gain a duplicate id by passing through the save.</summary>
        [Fact]
        public void NoEventIsDuplicatedByTheRoundTrip()
        {
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(PlayedScenario().World));

            HashSet<EntityId> seen = new HashSet<EntityId>();
            foreach (WorldEvent worldEvent in reloaded.Ledger.Events)
            {
                Assert.True(seen.Add(worldEvent.Id), "event " + worldEvent.Id + " appears twice after a reload");
            }
        }

        /// <summary>
        /// Draws the next few rolls from a world's own stream. What they are does not matter;
        /// that both worlds produce the same ones is the whole point.
        /// </summary>
        private static List<string> PlayOn(NarrativeWorldState world)
        {
            List<string> rolls = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                rolls.Add(world.Rng.Roll(20).ToString());
            }

            return rolls;
        }

        [Fact]
        public void IdCountersSurviveSoHistoryIsNeverOverwritten()
        {
            TheftLaboratory lab = PlayedScenario();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            EntityId next = reloaded.NewId("npc");

            Assert.False(reloaded.Registry.Npcs.ContainsKey(next));
            Assert.False(lab.World.Registry.Npcs.ContainsKey(next));
        }

        [Fact]
        public void ASaveFromANewerBuildIsRefusedRatherThanMisread()
        {
            TheftLaboratory lab = PlayedScenario();
            JsonValue root = WorldStateSerializer.ToJson(lab.World);
            string tampered = root.ToJson().Replace("\"schemaVersion\":1", "\"schemaVersion\":99");

            Assert.Throws<NotSupportedException>(() => WorldStateSerializer.Load(tampered));
        }

        [Fact]
        public void AMigrationStepUpgradesAnOlderDocument()
        {
            // Version 0 is fictional - what is under test is that the mechanism runs.
            SaveMigrations.Register(0, document => document.Set("schemaVersion", 1));

            JsonValue old = JsonValue.Object().Set("schemaVersion", 0).Set("worldSeed", "7").Set("rngState", "7");
            NarrativeWorldState migrated = WorldStateSerializer.Load(old.ToJson());

            Assert.Equal(1, migrated.SchemaVersion);
            Assert.Equal(7UL, migrated.WorldSeed);
        }

        [Fact]
        public void TheSaveIsHumanReadableForAuditing()
        {
            TheftLaboratory lab = PlayedScenario();
            string json = WorldStateSerializer.Save(lab.World, indented: true);

            Assert.Contains("\"schemaVersion\"", json);
            Assert.Contains("\"beliefs\"", json);
            Assert.Contains("\"escalation\"", json);
            Assert.Contains('\n', json);
        }
    }
}
