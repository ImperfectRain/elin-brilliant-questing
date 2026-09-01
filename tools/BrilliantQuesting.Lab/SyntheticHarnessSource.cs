using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public static class SyntheticHarnessSource
    {
        public static HarnessState Build(IntegrationHarnessConfig config)
        {
            NarrativeWorldState world = new NarrativeWorldState(config.Seed);
            EntityId player = EntityId.Parse("npc_player");
            EntityId town = EntityId.Parse("zone_synthetic_town");
            EntityId home = EntityId.Parse("zone_synthetic_home");
            SandboxVanillaState vanilla = new SandboxVanillaState(player);
            HarnessState state = new HarnessState(world, vanilla, player, town, "synthetic");

            world.Registry.Add(new NarrativeSite(town, "Synthetic Town", "town"));
            world.Registry.Add(new NarrativeSite(home, "Synthetic Home", "home"));
            world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major, HomeSiteId = home });
            vanilla.Define(player, 8, 1000, town);
            vanilla.SetAttribute(player, VanillaAttribute.Dexterity, 10);
            vanilla.SetAttribute(player, VanillaAttribute.Charisma, 10);
            vanilla.SetSkill(player, VanillaSkill.Cooking, 10);
            state.RememberActor(player);
            state.RememberInventoryOwner(player);

            DeterministicRng rng = new DeterministicRng(config.Seed).Fork("integration|synthetic");
            EntityId firstResident = EntityId.None;
            for (int i = 0; i < config.Population; i++)
            {
                EntityId id = world.NewId("npc");
                if (firstResident.IsNone)
                {
                    firstResident = id;
                }

                NarrativeNpc npc = world.Registry.Add(new NarrativeNpc(id, "Resident " + (i + 1))
                {
                    Occupation = i == 0 ? "shopkeeper" : "local",
                    HomeSiteId = town,
                    Importance = NarrativeImportance.Known
                });

                npc.Personality.Greed = i == 1 ? 0.85 : 0.25 + rng.NextInt(50) / 100.0;
                npc.Personality.Honesty = i == 1 ? 0.2 : 0.55;
                npc.Personality.Courage = 0.35 + rng.NextInt(50) / 100.0;
                vanilla.Define(id, 1 + rng.NextInt(12), i == 1 ? 10 : 150 + rng.NextInt(900), town);
                vanilla.SetActorClass(id, NarrativeActorClass.OrdinaryCitizen);
                vanilla.SetActorKind(id, NarrativeActorKind.Person);
                vanilla.SetSocialAgency(id, SocialAgency.Full);
                vanilla.SetAttribute(id, VanillaAttribute.Dexterity, i == 1 ? 15 : 7 + rng.NextInt(8));
                vanilla.SetAttribute(id, VanillaAttribute.Perception, i == 2 ? 15 : 5 + rng.NextInt(10));
                vanilla.SetSkill(id, VanillaSkill.Pickpocket, i == 1 ? 9 : rng.NextInt(3));
                vanilla.SetSkill(id, VanillaSkill.Stealth, i == 1 ? 7 : rng.NextInt(4));
                vanilla.SetSkill(id, VanillaSkill.SpotHidden, i == 2 ? 8 : rng.NextInt(5));

                if (i == 0)
                {
                    vanilla.GiveItem(id, new ItemDescriptor(
                        EntityId.Parse("item_synthetic_heirloom"),
                        "silver keepsake",
                        "jewelry",
                        900,
                        "ring",
                        40));
                }

                state.RememberActor(id);
                state.RememberInventoryOwner(id);
            }

            if (config.Population >= 3)
            {
                world.Relationships.ConnectMutual(
                    EntityId.Parse("npc_00000001"),
                    EntityId.Parse("npc_00000003"),
                    RelationKind.Acquaintance,
                    25);
            }

            if (!firstResident.IsNone)
            {
                vanilla.SetHome(new HomeStateBuilder(home, "Synthetic Home")
                    .WithCapacity(1)
                    .AddResident(firstResident, world.Registry.NameOf(firstResident), "cook")
                    .WithMetric(HomeMetric.Food, 12)
                    .WithMetric(HomeMetric.Safety, 20)
                    .Build());
            }

            return state;
        }
    }
}
