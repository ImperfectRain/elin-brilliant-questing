using System;
using System.IO;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    public class IntegrationHarnessTests
    {
        [Fact]
        public void SyntheticHarnessReportsEveryRegisteredProductionSystem()
        {
            IntegrationHarnessConfig config = new IntegrationHarnessConfig
            {
                Mode = IntegrationHarnessMode.Synthetic,
                Seed = 42,
                Days = 16,
                Population = 12
            };

            HarnessRunResult result = IntegrationHarness.Run(config);

            Assert.True(result.Passed, string.Join("; ", result.Failures));
            foreach (ProductionSystemDescriptor descriptor in ProductionSystemRegistry.Descriptors())
            {
                Assert.True(
                    result.Coverage.Entries.ContainsKey(descriptor.Id),
                    "Missing coverage for " + descriptor.Id);
            }

            Assert.True(result.GeneratedSituations > 0);
            Assert.Contains("live_witness_los", result.Coverage.Entries.Keys);
        }

        [Fact]
        public void CapturedSnapshotHydratesUnknownsWithoutFabricatingActorClassification()
        {
            string path = TempFile();
            try
            {
                CapturedWorldSnapshot snapshot = MinimalSnapshot();
                File.WriteAllText(path, snapshot.ToJson());

                HarnessState state = CapturedWorldSnapshot.Load(path).Hydrate();

                EntityId resident = EntityId.Parse("npc_vanilla_100");
                Assert.Equal(VanillaLifeState.Unknown, state.Vanilla.GetLifeState(EntityId.Parse("npc_missing")));
                Assert.Equal(NarrativeActorClass.Unknown, state.Vanilla.GetActorClass(resident));
                Assert.Equal(NarrativeActorKind.Unknown, state.Vanilla.GetActorKind(resident));
                Assert.Equal(SocialAgency.Unknown, state.Vanilla.GetSocialAgency(resident));
                Assert.Null(state.Vanilla.GetHomeState());
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void CapturedModeConsumesSnapshotThroughTheSameHarnessBoundary()
        {
            string path = TempFile();
            try
            {
                File.WriteAllText(path, MinimalSnapshot().ToJson());
                IntegrationHarnessConfig config = new IntegrationHarnessConfig
                {
                    Mode = IntegrationHarnessMode.Captured,
                    SnapshotPath = path,
                    Days = 1,
                    SaveReloadDay = null
                };

                HarnessRunResult result = IntegrationHarness.Run(config);

                Assert.True(result.Passed, string.Join("; ", result.Failures));
                foreach (ProductionSystemDescriptor descriptor in ProductionSystemRegistry.Descriptors())
                {
                    Assert.Contains(descriptor.Id, result.Coverage.Entries.Keys);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void NewerSnapshotSchemaFailsClearly()
        {
            string path = TempFile();
            try
            {
                File.WriteAllText(path, "{\"schemaVersion\":999,\"actors\":[]}");

                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                    () => CapturedWorldSnapshot.Load(path));

                Assert.Contains("newer than this Lab reader", ex.Message);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static CapturedWorldSnapshot MinimalSnapshot()
        {
            NarrativeWorldState world = new NarrativeWorldState(91);
            EntityId player = EntityId.Parse("npc_player");
            EntityId resident = EntityId.Parse("npc_vanilla_100");
            EntityId zone = EntityId.Parse("zone_vanilla_10");
            world.Registry.Add(new NarrativeSite(zone, "Captured Town", "town"));
            world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });
            world.Registry.Add(new NarrativeNpc(resident, "Mara"));

            return new CapturedWorldSnapshot
            {
                Source = "test-capture",
                WorldSeed = 91,
                WorldJson = WorldStateSerializer.Save(world, indented: false),
                PlayerId = player.Value,
                PrimaryZoneId = zone.Value,
                NowMinutes = 1440,
                Actors =
                {
                    new CapturedActor
                    {
                        Id = player.Value,
                        Name = "You",
                        ZoneId = zone.Value,
                        Level = 10,
                        Life = "Alive",
                        ActorClass = "Player",
                        ActorKind = "Person",
                        SocialAgency = "Full"
                    },
                    new CapturedActor
                    {
                        Id = resident.Value,
                        Name = "Mara",
                        ZoneId = zone.Value,
                        Level = 3,
                        Money = 25,
                        Life = "Alive",
                        Inventory =
                        {
                            new CapturedItem
                            {
                                Id = "item_vanilla_5",
                                Name = "apple",
                                CategoryTag = "food",
                                Value = 8,
                                Quality = 12
                            }
                        }
                    }
                },
                Sites =
                {
                    new CapturedSite
                    {
                        Id = zone.Value,
                        Name = "Captured Town",
                        SiteType = "town",
                        ZoneRef = "10"
                    }
                }
            };
        }

        private static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "bq-harness-" + Guid.NewGuid().ToString("N") + ".json");
        }
    }
}
