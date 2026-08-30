using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-037 with no game attached: one robbery on the road, four guilds, five places.
    ///
    /// Nothing here is written per guild. The robbery is stated as what happened - a guard killed,
    /// a shipment taken, a tavern left short - and each network picks up the half of it that its
    /// own interest table reads. The two things to look at are what reached each hall, none of
    /// which anybody there could have overheard, and what the contacts in the square say to a
    /// player who carries a card versus one who does not.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --guilds [seed]
    /// </summary>
    internal static class GuildRun
    {
        public static void Run(ulong seed, int days = 3)
        {
            NarrativeWorldState world = new NarrativeWorldState(seed);
            EntityId player = world.NewId("npc");
            EntityId town = world.NewId("zone");

            SandboxVanillaState vanilla = new SandboxVanillaState(player);
            vanilla.Define(player, level: 5, zone: town);
            world.Registry.Add(new NarrativeNpc(player, "You"));

            RumorSystem rumors = new RumorSystem(world.Knowledge, world.Ledger, world.Ids);
            RumorCirculation circulation = new RumorCirculation(rumors);
            TownNews news = new TownNews(rumors);

            EntityId guard = Person(world, vanilla, "Ceren", town);
            EntityId rurik = Person(world, vanilla, "Rurik", town);
            EntityId ilsa = Person(world, vanilla, "Ilsa", town);
            EntityId cargo = world.NewId("item");

            EntityId harn = Member(world, vanilla, "Harn", GuildId.Fighters, town);
            EntityId sable = Member(world, vanilla, "Sable", GuildId.Thieves, town);
            EntityId guilda = Member(world, vanilla, "Guilda", GuildId.Merchants, town);
            EntityId weiss = Member(world, vanilla, "Weiss", GuildId.Mages, town);

            // Each hall is its own place, so the only thing that can carry anything to the four
            // people sitting in them is the network they belong to.
            EntityId bram = Member(world, vanilla, "Bram", GuildId.Fighters, world.NewId("zone"));
            EntityId nix = Member(world, vanilla, "Nix", GuildId.Thieves, world.NewId("zone"));
            EntityId tam = Member(world, vanilla, "Tam", GuildId.Merchants, world.NewId("zone"));
            EntityId ivo = Member(world, vanilla, "Ivo", GuildId.Mages, world.NewId("zone"));

            EntityId killing = Claim(world, rurik, FactPredicates.Killed, guard);
            EntityId theft = Claim(world, rurik, FactPredicates.Stole, cargo, "the wine shipment");
            EntityId shortage = Claim(world, ilsa, FactPredicates.Needs, EntityId.None, "alcohol, any quality");

            // Everyone standing in the town square hears the week's news the ordinary way. Nobody
            // sitting in a hall a day away hears any of it.
            foreach (EntityId local in new[] { harn, sable, guilda, weiss })
            {
                foreach (EntityId fact in new[] { killing, theft, shortage })
                {
                    world.Knowledge.Teach(local, fact, KnowledgeSource.Hearsay, 0.8, vanilla.Now, false);
                }
            }

            Banner("ONE ROBBERY ON THE ROAD (seed " + seed + ")");
            Console.WriteLine("  Rurik killed Ceren, took the wine shipment, and Ilsa's tavern is now short.");
            Console.WriteLine("  Four guild members in the town square heard all three. Their opposite");
            Console.WriteLine("  numbers, each alone in their own hall a day away, heard none of it.\n");

            circulation.Run(world, vanilla, vanilla.Now);
            for (int day = 1; day <= days; day++)
            {
                vanilla.AdvanceDays(1);
                RumorRound round = circulation.Run(world, vanilla, vanilla.Now);
                Console.WriteLine("  day " + day + ": " + round.Routed + " through networks, "
                                  + round.Tells + " in the street");
            }

            Banner("WHAT REACHED THE HALLS");
            Report(world, "Bram   (Fighters) ", bram, killing, theft, shortage);
            Report(world, "Nix    (Thieves)  ", nix, killing, theft, shortage);
            Report(world, "Tam    (Merchants)", tam, killing, theft, shortage);
            Report(world, "Ivo    (Mages)    ", ivo, killing, theft, shortage);

            Banner("ASKING THE CONTACTS, CARRYING NO CARD");
            Answers(world, vanilla, news, harn, sable, guilda);

            vanilla.SetGuildRank(GuildId.Fighters, 1).SetGuildRank(GuildId.Thieves, 1).SetGuildRank(GuildId.Merchants, 1);

            Banner("THE SAME PEOPLE, THE SAME WEEK, WITH THE CARDS");
            Answers(world, vanilla, news, harn, sable, guilda);
        }

        private static void Answers(NarrativeWorldState world, SandboxVanillaState vanilla, TownNews news, params EntityId[] contacts)
        {
            for (int i = 0; i < contacts.Length; i++)
            {
                Console.WriteLine(world.Registry.NameOf(contacts[i]) + ":");
                IReadOnlyList<SpokenRemark> answer = news.Ask(world, vanilla, contacts[i]);
                if (answer.Count == 0)
                {
                    Console.WriteLine("    (nothing they have not already told you)");
                    continue;
                }

                for (int r = 0; r < answer.Count; r++)
                {
                    Console.WriteLine("    \"" + answer[r].Line + "\""
                                      + (answer[r].Framing == GuildFraming.None
                                          ? string.Empty
                                          : "   [" + answer[r].Network + ": " + answer[r].Framing + "]"));
                }
            }
        }

        private static void Report(NarrativeWorldState world, string who, EntityId member, EntityId killing, EntityId theft, EntityId shortage)
        {
            List<string> heard = new List<string>();
            if (world.Knowledge.Knows(member, killing))
            {
                heard.Add("the killing");
            }

            if (world.Knowledge.Knows(member, theft))
            {
                heard.Add("the shipment");
            }

            if (world.Knowledge.Knows(member, shortage))
            {
                heard.Add("the shortage");
            }

            Console.WriteLine("  " + who + " " + (heard.Count == 0 ? "nothing at all" : string.Join(", ", heard)));
        }

        private static EntityId Person(NarrativeWorldState world, SandboxVanillaState vanilla, string name, EntityId zone)
        {
            EntityId id = world.NewId("npc");
            world.Registry.Add(new NarrativeNpc(id, name));
            vanilla.Define(id, level: 3, zone: zone);
            return id;
        }

        private static EntityId Member(NarrativeWorldState world, SandboxVanillaState vanilla, string name, GuildId guild, EntityId zone)
        {
            EntityId id = Person(world, vanilla, name, zone);
            world.Registry.GetNpc(id).Roles.Add(GuildNetworks.MembershipRole(guild));
            return id;
        }

        private static EntityId Claim(NarrativeWorldState world, EntityId subject, string predicate, EntityId about, string value = null)
        {
            EntityId id = world.NewId("fact");
            world.Knowledge.AddFact(new Fact(id, subject, predicate, about, value));
            return id;
        }

        private static void Banner(string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 78));
        }
    }
}
