using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-038 with no game attached: the same hall, the same beast, three players.
    ///
    /// One carries the Fighters' card and rank enough to be listened to, one carries it and is
    /// nobody in the guild yet, and one carries nothing. Nothing about the road is written for the
    /// Fighters - the situation says a carter is not safe from a thing, and the network's own
    /// interest table makes that a bounty - so the run also asks the Merchants officer standing in
    /// the same hall, who reads nothing in any of it.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --authority [seed]
    /// </summary>
    internal static class AuthorityRun
    {
        /// <summary>
        /// Its own default rather than the laboratory's, so the demo shows the route working.
        /// The shared seed lands on an ordinary refusal, which is a real outcome and a poor
        /// first impression of a mechanic; either can be seen by naming a seed.
        /// </summary>
        public const ulong DefaultSeed = 3UL;

        public static void Run(ulong seed)
        {
            Banner("THE BEAST ON THE WICK ROAD (seed " + seed + ")");
            Console.WriteLine("  Something out of the Fenwyck barrow killed Halda and Orren still has to");
            Console.WriteLine("  use the road. Sera speaks for the Fighters in Derwen, Toma for the");
            Console.WriteLine("  Merchants, and neither of them has heard a word of it.\n");

            Show("a Fighters officer of rank 2", rank: 2, seed: seed);
            Show("the same player, rank 1", rank: 1, seed: seed);
            Show("no guild card at all", rank: 0, seed: seed);

            Banner("WHAT THE HALL WAS TOLD");
            Bench told = Bench.Create(seed, rank: 2);
            Console.WriteLine("  before: Sera believes it? " + told.OfficerKnows());
            told.Ask();
            Console.WriteLine("  after:  Sera believes it? " + told.OfficerKnows()
                              + ", and can prove it? " + told.OfficerCanProve());
            Console.WriteLine("  the player can still prove it: " + told.PlayerCanProve());
        }

        private static void Show(string who, int rank, ulong seed)
        {
            Bench bench = Bench.Create(seed, rank);
            Banner(who.ToUpperInvariant());

            Availability petition = bench.Can();
            Console.WriteLine("  put it to the guild: " + petition);

            if (petition.IsAvailable)
            {
                ActionOutcome outcome = bench.Ask();
                Console.WriteLine("  " + outcome.Explain().Replace("\n", "\n  "));
            }

            Console.WriteLine("  the exposure is now: " + bench.Exposure());
            Console.WriteLine("  the killing is still: " + bench.Killing() + "  (history is not answerable)");
            Console.WriteLine("  the situation is: " + bench.Situation.Thread.State
                              + (bench.Situation.Thread.Resolution == null
                                  ? string.Empty
                                  : " as " + bench.Situation.Thread.Resolution));
            Console.WriteLine("  routes still open to this build: " + string.Join(", ", bench.Families()));
            Console.WriteLine("  asking Toma instead: " + bench.CanAsk(bench.Situation.MerchantsOfficerId));
        }

        private sealed class Bench
        {
            private Bench()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public MaraudingBeastSituation Situation { get; private set; }

            public static Bench Create(ulong seed, int rank)
            {
                Bench bench = new Bench();
                NarrativeWorldState world = new NarrativeWorldState(seed);
                EntityId player = world.NewId("npc");
                EntityId hamlet = world.NewId("zone");

                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: 200, zone: hamlet);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 12);
                vanilla.SetAttribute(player, VanillaAttribute.Charisma, 12);
                vanilla.SetGuildRank(GuildId.Fighters, rank);
                vanilla.SetGuildContribution(GuildId.Fighters, rank * 30);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                bench.World = world;
                bench.Vanilla = vanilla;
                bench.Player = player;
                bench.Actions = StandardActions.CreateRegistry();
                bench.Situation = MaraudingBeastSituation.Create(
                    world, new SandboxStager(vanilla), player, hamlet, vanilla.Now);

                new ConsequenceEngine(world, vanilla).Attach();
                return bench;
            }

            public Availability Can() => CanAsk(Situation.FightersOfficerId);

            public Availability CanAsk(EntityId officer)
            {
                return Actions.Get("invoke_authority").GetAvailability(Context(officer));
            }

            public ActionOutcome Ask()
            {
                return Actions.Get("invoke_authority").Perform(Context(Situation.FightersOfficerId));
            }

            public TruthState Exposure() => World.Knowledge.GetFact(Situation.ExposureFactId).Truth;

            public TruthState Killing() => World.Knowledge.GetFact(Situation.KillingFactId).Truth;

            public bool OfficerKnows() => World.Knowledge.Knows(Situation.FightersOfficerId, Situation.ExposureFactId);

            public bool OfficerCanProve() => World.Knowledge.CanProve(Situation.FightersOfficerId, Situation.ExposureFactId);

            public bool PlayerCanProve() => World.Knowledge.CanProve(Player, Situation.ExposureFactId);

            public List<string> Families()
            {
                HashSet<ActionFamily> families = new HashSet<ActionFamily>();
                foreach (EntityId zone in new[] { Situation.HamletZoneId, Situation.HallZoneId })
                {
                    foreach (EntityId target in new[]
                    {
                        Situation.CarterId, Situation.FightersOfficerId, Situation.MerchantsOfficerId, EntityId.None
                    })
                    {
                        families.UnionWith(Actions.AvailableFamilies(Context(target, zone)));
                    }
                }

                List<string> names = new List<string>();
                foreach (ActionFamily family in families)
                {
                    names.Add(family.ToString());
                }

                names.Sort(StringComparer.Ordinal);
                return names;
            }

            private ActionContext Context(EntityId target, EntityId zone = default)
            {
                Vanilla.SetZone(Player, zone.IsNone ? Situation.HallZoneId : zone);
                return new ActionContext(World, Vanilla, new VanillaStyleCheckResolver(Vanilla), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }
        }

        private static void Banner(string title)
        {
            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));
        }
    }
}
