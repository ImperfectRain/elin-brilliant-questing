using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-143. Cuts one store into a mine that already exists and prints what happened to it.
    ///
    /// The claim is about persistence and safety, and both are things somebody should be able to
    /// watch rather than take a test's word for. So this establishes a mine, prints its shape,
    /// applies one addition, asks for the same addition again, saves and reloads twice with a
    /// fortnight in between, and prints the place after each - then paves the ground around a
    /// second mine's workings with something the player built and prints the refusal.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run site-addition
    /// </summary>
    internal sealed class SiteAdditionScenario : LabScenario
    {
        private const string StoreId = "mine.powder_store";

        private const string StorePiece = "piece.mine.tool_niche";

        private const string Workings = "workings";

        public override string Id => "site-addition";

        public override string Summary => "add one authored piece to a mine that already exists, twice, across two saves";

        public override string Description =>
            "Establishes a scenario dungeon, applies one bounded additive mutation to it, asks for the\n"
            + "same mutation again, saves and reloads it twice with elapsed days in between, and prints\n"
            + "the place each time - then shows the refusal when the ground the addition would take is\n"
            + "ground the player has changed.";

        public override bool UsesSeed => false;

        public override int Run(LabRunContext context)
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(BundlePath());
            if (loaded.Diagnostics.Count > 0)
            {
                foreach (ContentDiagnostic diagnostic in loaded.Diagnostics)
                {
                    Console.Error.WriteLine(diagnostic);
                }

                return LabExit.ScenarioFailure;
            }

            IReadOnlyList<ContentDiagnostic> problems;
            SiteGrammarLibrary grammars = SiteGrammarContent.CreateLibrary(loaded.Bundle, out problems);
            SitePieceCatalogue pieces = SitePieceContent.CreateCatalogue(
                loaded.Bundle, ScenarioDungeon.Family, out problems);
            ActionRegistry actions = StandardActions.CreateRegistry();

            SandboxVanillaState build = new SandboxVanillaState(EntityId.Parse("npc_player"));
            SandboxStager stager = new SandboxStager(build);
            NarrativeWorldState world = Crew(build);
            NarrativeThread thread = world.Threads[0];

            EntityId siteId = world.NewId("zone");
            ScenarioDungeonResult made = ScenarioDungeon.Establish(
                world,
                siteId,
                "the workings above the drove road",
                thread.Id,
                SiteAffordance.EvidenceCache,
                4,
                grammars,
                pieces,
                actions,
                build,
                stager,
                build.Now);

            if (!made.Created)
            {
                Console.Error.WriteLine("no mine was made: " + string.Join("; ", made.Refusals));
                return LabExit.ScenarioFailure;
            }

            Console.WriteLine("the mine as it was made");
            Print(world, siteId, grammars, pieces, actions, build);

            build.AdvanceDays(2);
            Console.WriteLine();
            Console.WriteLine("cutting " + StoreId + " off " + Workings);
            SiteMutationResult first = Apply(world, siteId, grammars, pieces, actions, build, stager);
            Report(first);

            Console.WriteLine();
            Console.WriteLine("asking for it again");
            Report(Apply(world, siteId, grammars, pieces, actions, build, stager));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Console.WriteLine();
            Console.WriteLine("after save and reload");
            Print(reloaded, siteId, grammars, pieces, actions, build);

            build.AdvanceDays(14);
            NarrativeWorldState twice = WorldStateSerializer.Load(WorldStateSerializer.Save(reloaded));
            Console.WriteLine();
            Console.WriteLine("after fourteen days and a second save and reload");
            Print(twice, siteId, grammars, pieces, actions, build);

            Console.WriteLine();
            Console.WriteLine("asking again on the reloaded world");
            Report(Apply(twice, siteId, grammars, pieces, actions, build, stager));

            Console.WriteLine();
            Console.WriteLine("and where the player has been building");
            EntityId second = world.NewId("zone");
            ScenarioDungeonResult other = ScenarioDungeon.Establish(
                world,
                second,
                "the workings under the scarp",
                thread.Id,
                SiteAffordance.EvidenceCache,
                9,
                grammars,
                pieces,
                actions,
                build,
                stager,
                build.Now);

            if (other.Created)
            {
                Pave(world, second, grammars, pieces, actions, build);
                Report(Apply(world, second, grammars, pieces, actions, build, stager));
            }

            Console.WriteLine();
            Console.WriteLine("  places built: " + stager.Built.Count + "; additions applied: " + stager.Added.Count);
            return LabExit.Success;
        }

        private static SiteMutationResult Apply(
            NarrativeWorldState world,
            EntityId siteId,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            SandboxVanillaState build,
            ISituationStager stager)
        {
            return SiteMutation.Apply(
                world,
                siteId,
                new SiteAdditionPlan(StoreId, StorePiece, Workings),
                grammars,
                pieces,
                actions,
                build,
                stager,
                build.Now);
        }

        private static void Report(SiteMutationResult result)
        {
            Console.WriteLine("  " + result.Outcome
                              + (result.Addition == null ? string.Empty : ": " + result.Addition));
            for (int i = 0; i < result.Considered.Count; i++)
            {
                Console.WriteLine("    looked at " + result.Considered[i]);
            }

            for (int i = 0; i < result.Refusals.Count; i++)
            {
                Console.WriteLine("    refused: " + result.Refusals[i]);
            }
        }

        private static void Print(
            NarrativeWorldState world,
            EntityId siteId,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            SandboxVanillaState build)
        {
            NarrativeSite site = world.Registry.GetSite(siteId);
            SiteRealizationResult shape = ScenarioDungeon.StructureOf(site, grammars, pieces, actions, build);
            if (!shape.Built)
            {
                Console.WriteLine("  no shape: " + string.Join("; ", shape.Refusals));
                return;
            }

            for (int i = 0; i < shape.Structure.Placements.Count; i++)
            {
                Console.WriteLine("    " + shape.Structure.Placements[i]);
            }

            Console.WriteLine("  additions on record: " + site.Additions.Count);
            for (int i = 0; i < site.Additions.Count; i++)
            {
                Console.WriteLine("    " + site.Additions[i] + " applied at day "
                                  + site.Additions[i].AppliedAt.TotalMinutes / (60 * 24));
            }
        }

        /// <summary>Says the player has built over every patch an addition off the workings could take.</summary>
        private static void Pave(
            NarrativeWorldState world,
            EntityId siteId,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            SandboxVanillaState build)
        {
            NarrativeSite site = world.Registry.GetSite(siteId);
            SiteRealizationResult shape = ScenarioDungeon.StructureOf(site, grammars, pieces, actions, build);
            if (!shape.Built)
            {
                return;
            }

            SitePlacement anchor = shape.Structure.PlacementOf(Workings);
            EntityId zone = SiteGenesis.ZoneOf(site);
            for (int x = anchor.X - 8; x < anchor.Right + 8; x++)
            {
                for (int y = anchor.Y - 8; y < anchor.Bottom + 8; y++)
                {
                    build.SetGround(zone, x, y, VanillaGround.PlayerChanged);
                }
            }
        }

        /// <summary>The least a mine can be furnished from: a theft, a crew, and one thing taken.</summary>
        private static NarrativeWorldState Crew(SandboxVanillaState build)
        {
            NarrativeWorldState world = new NarrativeWorldState(143);
            EntityId town = world.NewId("zone");

            EntityId thief = Person(world, build, "Renn", town);
            EntityId victim = Person(world, build, "Mab", town);

            Organization crew = world.Registry.Add(
                new Organization(world.NewId("org"), "the road crew", "criminal_crew") { LeaderId = thief });
            foreach (EntityId member in new[]
                     {
                         thief, Person(world, build, "Bryn", town), Person(world, build, "Tace", town)
                     })
            {
                crew.MemberIds.Add(member);
                world.Registry.GetNpc(member).OrganizationIds.Add(crew.Id);
            }

            ItemDescriptor strongbox = new ItemDescriptor(
                world.NewId("item"), "a banded strongbox", "goods", 400, "chest");
            build.GiveItem(thief, strongbox);

            WorldEvent theft = world.Record(
                WorldEventType.Theft,
                thief,
                victim,
                build.Now,
                magnitude: 0.6,
                zone: town,
                evidence: new[] { strongbox.Id });

            Fact stole = new Fact(
                world.NewId("fact"),
                thief,
                "stole",
                strongbox.Id,
                string.Empty,
                TruthState.True,
                secrecy: 60,
                originEvent: theft.Id);
            stole.EvidenceIds.Add(strongbox.Id);
            world.Knowledge.AddFact(stole);

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "road_crew", build.Now)
            {
                State = ThreadState.Active,
                OriginEventId = theft.Id
            };
            thread.FactIds.Add(stole.Id);
            thread.ParticipantIds.Add(thief);
            thread.ParticipantIds.Add(victim);
            world.Threads.Add(thread);
            return world;
        }

        private static EntityId Person(
            NarrativeWorldState world, SandboxVanillaState build, string name, EntityId zone)
        {
            NarrativeNpc npc = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), name));
            build.Define(npc.Id, level: 3, money: 20, zone: zone);
            return npc.Id;
        }

        private static string BundlePath()
        {
            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (directory != null
                   && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            return directory == null
                ? System.IO.Path.Combine("Package", "content.bqc")
                : System.IO.Path.Combine(directory.FullName, "Package", "content.bqc");
        }
    }
}
