using System;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
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
