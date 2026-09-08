using System;
using System.IO;
using System.Linq;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    internal sealed class FailedCaravanScenario : LabScenario
    {
        public override string Id => "failed-caravan";
        public override string Summary => "find original cargo at a site caused by an off-screen caravan failure";
        public override string Description =>
            "Departs a real cargo-bearing journey, records its loss to an existing crew, and derives\n"
            + "a scenario site from that history. Shows a local lead reaching the journal by rumour,\n"
            + "then saves and returns without regenerating the site or replenishing recovered cargo.\n"
            + "Headless evidence only; native site creation remains unverified.";
        public override ulong DefaultSeed => 43;

        public override int Run(LabRunContext context)
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                directory = directory.Parent;
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(Path.Combine(
                directory == null ? "." : directory.FullName, "Package", "content.bqc"));
            if (loaded.Diagnostics.Count > 0)
            {
                foreach (ContentDiagnostic diagnostic in loaded.Diagnostics) Console.Error.WriteLine(diagnostic);
                return LabExit.ScenarioFailure;
            }
            SiteGrammarLibrary grammars = SiteGrammarContent.CreateLibrary(loaded.Bundle, out var problems);
            if (problems.Count > 0) return LabExit.ScenarioFailure;
            SitePieceCatalogue pieces = SitePieceContent.CreateCatalogue(loaded.Bundle, ScenarioDungeon.Family, out problems);
            if (problems.Count > 0) return LabExit.ScenarioFailure;

            NarrativeWorldState world = new NarrativeWorldState(context.Seed);
            EntityId player = EntityId.Parse("npc_player");
            EntityId home = EntityId.Parse("zone_home");
            EntityId site = EntityId.Parse("zone_occupied_workings");
            SandboxVanillaState vanilla = new SandboxVanillaState(player);
            SandboxStager stager = new SandboxStager(vanilla);
            vanilla.Define(player, zone: home);
            EntityId Person(string name, EntityId zone)
            {
                NarrativeNpc npc = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), name));
                stager.StageCharacter(npc.Id, new CharacterBlueprint(name), zone);
                return npc.Id;
            }
            EntityId driver = Person("Mab", home);
            EntityId thief = Person("Renn", site);
            Organization crew = world.Registry.Add(new Organization(world.NewId("org"), "the road crew", "bandits"));
            foreach (EntityId member in new[] { thief, Person("Bryn", site), Person("Tace", site) })
            {
                crew.MemberIds.Add(member);
                world.Registry.GetNpc(member).OrganizationIds.Add(crew.Id);
            }
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), FailedCaravanSituation.ArchetypeId, vanilla.Now);
            thread.ParticipantIds.Add(driver);
            thread.ParticipantIds.Add(thief);
            world.Threads.Add(thread);
            ItemDescriptor cargo = new ItemDescriptor(world.NewId("item"), "wine casks", "goods", 400);
            vanilla.GiveItem(driver, cargo);
            TravelingGroup group = new TravelingGroup(world.NewId("travel"), "caravan", home,
                EntityId.Parse("zone_destination"), "carry_wine", vanilla.Now, vanilla.Now,
                vanilla.Now.PlusDays(5)) { ThreadId = thread.Id };
            group.AddMember(driver);
            group.AddCargo(cargo.Id);
            TravelingGroupLifecycle travel = new TravelingGroupLifecycle(world, vanilla);
            travel.TryPlan(group);
            travel.Advance(vanilla.Now);
            vanilla.AdvanceDays(1);
            if (!vanilla.TryTransferItem(cargo.Id, driver, thief)) return LabExit.ScenarioFailure;
            WorldEvent theft = world.Record(WorldEventType.Theft, thief, driver, vanilla.Now,
                zone: site, evidence: new[] { cargo.Id }, threadId: thread.Id);
            thread.OriginEventId = theft.Id;
            Fact stolen = new Fact(world.NewId("fact"), thief, "stole", cargo.Id, "",
                TruthState.True, originEvent: theft.Id);
            stolen.EvidenceIds.Add(cargo.Id);
            world.Knowledge.AddFact(stolen);
            thread.FactIds.Add(stolen.Id);
            travel.TryFail(group.Id, theft.Id, site, vanilla.Now);

            FailedCaravanResult Establish() => FailedCaravanSituation.Establish(world, group.Id,
                "the occupied workings", grammars.Get(ScenarioDungeon.GrammarId), ScenarioDungeon.Family,
                pieces, context.Seed, StandardActions.CreateRegistry(), vanilla, stager, vanilla.Now);
            FailedCaravanResult made = Establish();
            if (!made.Established)
            {
                foreach (string refusal in made.Refusals) Console.Error.WriteLine(refusal);
                return LabExit.ScenarioFailure;
            }
            Console.WriteLine(NarrativeInspector.DescribeTravelingGroup(world, group.Id));
            Console.WriteLine(NarrativeInspector.DescribeScenarioPlan(world, made.Plan));
            Console.WriteLine("Player stayed at Home: " + (vanilla.GetZoneOf(player) == home));
            Console.WriteLine("Cargo is the original: " + vanilla.GetInventory(thief).Any(i => i.Id == cargo.Id));
            Fact lead = world.Knowledge.Facts.Values.Single(f => f.Subject == thief && f.Predicate == FactPredicates.LocatedAt);
            Console.WriteLine("Journal knows the location before rumour: " + world.Knowledge.Knows(player, lead.Id));
            new RumorSystem(world.Knowledge, world.Ledger, world.Ids).Tell(thief, player, lead.Id, vanilla.Now);
            Console.WriteLine(NarrativeJournal.Describe(world, player));
            vanilla.SetZone(player, lead.Object);
            SiteVisit visit = SiteGenesis.Visit(world, site, vanilla);
            Console.WriteLine("Visit missing cargo: " + visit.MissingCargo.Count);
            if (!vanilla.TryTransferItem(cargo.Id, thief, player)) return LabExit.ScenarioFailure;
            world = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            string saved = WorldStateSerializer.Save(world);
            FailedCaravanResult returned = Establish();
            Console.WriteLine("Return: " + returned.Genesis?.Outcome);
            Console.WriteLine("Site builds: " + stager.Built.Count);
            Console.WriteLine("Return changed saved history: " + (saved != WorldStateSerializer.Save(world)));
            Console.WriteLine("Recovered cargo remains with player: " + vanilla.GetInventory(player).Any(i => i.Id == cargo.Id));
            return returned.Established && stager.Built.Count == 1 && saved == WorldStateSerializer.Save(world)
                ? LabExit.Success : LabExit.ScenarioFailure;
        }
    }
}
