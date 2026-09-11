using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class MigrationFixtureTests
    {
        private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves");

        // Enumerate the contract, not the files: deleting a fixture or bumping the schema must fail CI.
        public static IEnumerable<object[]> Versions => Enumerable.Range(1, NarrativeWorldState.CurrentSchemaVersion)
            .Select(version => new object[] { version });

        [Fact]
        public void FixtureCollectionCoversExactlyTheSupportedSchemaVersions()
        {
            Assert.Equal(Enumerable.Range(1, NarrativeWorldState.CurrentSchemaVersion)
                    .Select(version => $"schema-{version:00}.json"),
                Directory.GetFiles(FixtureDirectory, "schema-*.json").Select(Path.GetFileName).OrderBy(name => name));
        }

        [Theory]
        [MemberData(nameof(Versions))]
        public void HistoricalSaveLoadsWithoutLossAndContinuesDeterministically(int version)
        {
            string text = File.ReadAllText(Path.Combine(FixtureDirectory, $"schema-{version:00}.json"));
            JsonValue original = JsonValue.Parse(text);
            Assert.Equal(version, original.GetInt("schemaVersion"));
            // These are played saves, not empty documents that can pass by defaulting everything.
            foreach (string family in new[] { "npcs", "events", "facts", "beliefs", "memories", "threads" })
                Assert.NotEmpty(original.GetArray(family));

            WorldStateLoadResult result = WorldStateSerializer.LoadWithDiagnostics(text);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(NarrativeWorldState.CurrentSchemaVersion, result.World.SchemaVersion);
            JsonValue saved = WorldStateSerializer.ToJson(result.World);
            AssertPreserved(original, saved, "$", version);
            AssertMigrationDefaults(original, saved, version);

            string canonical = saved.ToJson(indented: true);
            WorldStateLoadResult second = WorldStateSerializer.LoadWithDiagnostics(canonical);
            Assert.Empty(second.Diagnostics);
            Assert.Equal(canonical, WorldStateSerializer.Save(second.World));

            // Attaching the host's consequence handler must not replay any historical deed.
            var vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            foreach (var npc in second.World.Registry.Npcs.Values)
                vanilla.Define(npc.Id);
            var consequences = new ConsequenceEngine(second.World, vanilla);
            consequences.Attach();
            Assert.Empty(consequences.Trace);
            Assert.Equal(0, vanilla.Karma);
            Assert.All(second.World.Registry.Npcs.Values, npc => Assert.Equal(0, vanilla.GetAffinity(npc.Id)));
            Assert.Equal(canonical, WorldStateSerializer.Save(second.World));

            var expectedRng = new DeterministicRng(ulong.Parse(original.GetString("worldSeed")));
            expectedRng.RestoreState(ulong.Parse(original.GetString("rngState")));
            for (int i = 0; i < 8; i++)
            {
                ulong expected = expectedRng.NextUInt64();
                Assert.Equal(expected, result.World.Rng.NextUInt64());
                Assert.Equal(expected, second.World.Rng.NextUInt64());
            }
            foreach (var counter in original["idCounters"].Members)
            {
                EntityId expected = EntityId.Mint(counter.Key, (ulong)counter.Value.NumberValue + 1);
                Assert.Equal(expected, result.World.NewId(counter.Key));
                Assert.Equal(expected, second.World.NewId(counter.Key));
            }
        }

        private static void AssertMigrationDefaults(JsonValue original, JsonValue saved, int version)
        {
            for (int i = 0; i < original["npcs"].Count; i++)
            {
                JsonValue npc = saved["npcs"].Items[i];
                if (version == 1)
                {
                    JsonValue old = original["npcs"].Items[i]["personality"];
                    JsonValue personality = npc["personality"];
                    Assert.Equal(old.GetNumber("courage"), personality.GetNumber("boldness"), 8);
                    Assert.Equal(old.GetNumber("sociability"), personality.GetNumber("warmth"), 8);
                    Assert.Equal(1 - old.GetNumber("greed"), personality.GetNumber("generosity"), 8);
                    Assert.Equal(1 - old.GetNumber("ambition"), personality.GetNumber("humility"), 8);
                    Assert.Equal(1 - old.GetNumber("ambition"), personality.GetNumber("statusBlindness"), 8);
                    Assert.Equal((old.GetNumber("mercy") + 1 - old.GetNumber("vengefulness")) / 2,
                        personality.GetNumber("mercy"), 8);
                    foreach (string key in new[] { "honesty", "loyalty", "curiosity" })
                        Assert.Equal(old.GetNumber(key), personality.GetNumber(key), 8);
                }
                if (version < 3) Assert.All(npc["problemSolving"].Members, p => Assert.Equal(0.5, p.Value.NumberValue));
                if (version < 4) Assert.All(npc["sensitivities"].Members, p => Assert.Equal(0.5, p.Value.NumberValue));
                if (version < 5) Assert.Equal("None", npc["contradiction"].GetString("kind"));
                if (version < 6) Assert.False(npc["quirk"].GetBool("assigned"));
                if (version < 7)
                {
                    Assert.All(npc["values"].Members, p => Assert.All(p.Value.Members, v => Assert.Equal(0.5, v.Value.NumberValue)));
                    Assert.All(npc["needs"].Members, p => Assert.Equal(0, p.Value.NumberValue));
                }
                if (version < 8) Assert.All(npc["emotions"].Members, p => Assert.Equal(0, p.Value.NumberValue));
                if (version < 10) Assert.Empty(npc["negativeSpace"].Items);
            }
            if (version < 9) Assert.All(saved["threads"].Items, thread => Assert.Empty(thread["storyletFirings"].Items));
            if (version < 11) Assert.Empty(saved["travelingGroups"].Items);
            // Provenance is written for every event from schema 12 on; an older save's events had
            // nowhere to record why they happened, and migration must leave them saying so rather
            // than inventing a cause from whatever was recorded next to them.
            if (version < 12)
                Assert.All(saved["events"].Items, worldEvent =>
                {
                    Assert.Empty(worldEvent["provenance"].GetArray("links"));
                    Assert.Null(worldEvent["provenance"]["decision"]);
                });
        }

        // New optional fields may be added; every old field and array entry must retain its value.
        // Personality v1 is the one replaced shape, checked semantically above instead.
        private static void AssertPreserved(JsonValue before, JsonValue after, string path, int version)
        {
            if (path == "$.schemaVersion" || (version == 1 && path.EndsWith(".personality", StringComparison.Ordinal))) return;
            Assert.True(after != null, $"Missing saved field {path}");
            // The reader has always normalized absent optional text to an empty string.
            if (before.Kind == JsonKind.Null && (path.EndsWith(".charaRef", StringComparison.Ordinal)
                || path.EndsWith(".occupation", StringComparison.Ordinal)))
            {
                Assert.Equal(JsonKind.String, after.Kind);
                Assert.Equal(string.Empty, after.StringValue);
                return;
            }
            Assert.True(before.Kind == after.Kind, $"Changed JSON kind at {path}");
            if (before.Kind == JsonKind.Object)
                foreach (var member in before.Members)
                    AssertPreserved(member.Value, after[member.Key], path + "." + member.Key, version);
            else if (before.Kind == JsonKind.Array)
            {
                Assert.Equal(before.Count, after.Count);
                for (int i = 0; i < before.Count; i++)
                    AssertPreserved(before.Items[i], after.Items[i], $"{path}[{i}]", version);
            }
            else
                Assert.True(before.ToJson() == after.ToJson(), $"Changed saved value at {path}: {before.ToJson()} -> {after.ToJson()}");
        }
    }
}
