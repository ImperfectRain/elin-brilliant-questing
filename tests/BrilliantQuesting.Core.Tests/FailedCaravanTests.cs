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
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class FailedCaravanTests
    {
        [Fact]
        public void ActualOffScreenFailureProducesCausalScenarioWithOriginalCargoAndALocalLead()
        {
            Fixture f = new Fixture();
            int events = f.World.Ledger.Count;
            FailedCaravanResult result = f.Establish();
            Assert.True(result.Established, string.Join("; ", result.Refusals));
            Assert.True(result.Plan.Valid);
            Assert.Contains(result.Plan.Anchors, a => a.SubjectId == f.Cargo && a.EventId == f.Theft.Id);
            Assert.Contains(f.Cargo, result.Site.ImportantObjectIds);
            Assert.Single(f.Vanilla.GetInventory(f.Thief), i => i.Id == f.Cargo);
            Assert.Empty(f.Vanilla.GetInventory(f.Driver));
            Assert.Equal(f.Away, f.Vanilla.GetZoneOf(f.Player));
            Assert.Equal(events, f.World.Ledger.Count);
            Assert.Single(f.World.Threads);
            Assert.Contains(f.Site, f.Thread.SiteIds);
            Fact lead = f.World.Knowledge.Facts.Values.Single(x =>
                x.Subject == f.Thief && x.Predicate == FactPredicates.LocatedAt);
            Assert.Equal(f.Site, lead.Object);
            Assert.Equal(f.Group.FailedEventId, lead.OriginEvent);
            Assert.True(f.World.Knowledge.Knows(f.Thief, lead.Id));
            Assert.False(f.World.Knowledge.Knows(f.Player, lead.Id));

            // Ordinary hearsay makes the place findable; it does not turn the report into proof.
            Assert.DoesNotContain(NarrativeJournal.Entries(f.World, f.Player), e => e.FactId == lead.Id);
            Assert.True(new RumorSystem(f.World.Knowledge, f.World.Ledger, f.World.Ids)
                .Tell(f.Thief, f.Player, lead.Id, f.Vanilla.Now));
            Assert.False(f.World.Knowledge.CanProve(f.Player, lead.Id));
            Assert.Contains(NarrativeJournal.Entries(f.World, f.Player), e => e.FactId == lead.Id);
            f.Vanilla.SetZone(f.Player, lead.Object);
            SiteVisit visit = SiteGenesis.Visit(f.World, lead.Object, f.Vanilla);
            Assert.Empty(visit.MissingOccupants);
            Assert.Empty(visit.MissingCargo);
        }

        [Fact]
        public void SaveReloadAndReturnNeitherRegenerateNorRefillRecoveredCargo()
        {
            Fixture f = new Fixture();
            Assert.True(f.Establish().Established);
            Assert.True(f.Vanilla.TryTransferItem(f.Cargo, f.Thief, f.Player));
            f.World = WorldStateSerializer.Load(WorldStateSerializer.Save(f.World));
            string before = WorldStateSerializer.Save(f.World);
            FailedCaravanResult result = f.Establish(seed: 9876);
            Assert.True(result.Established);
            Assert.Equal(SiteGenesisOutcome.AlreadyEstablished, result.Genesis.Outcome);
            Assert.Single(f.Stager.Built);
            Assert.Equal(before, WorldStateSerializer.Save(f.World));
            Assert.Single(f.Vanilla.GetInventory(f.Player), i => i.Id == f.Cargo);
            Assert.Empty(f.Vanilla.GetInventory(f.Thief));
            Assert.Contains(f.Cargo, SiteGenesis.Visit(f.World, f.Site, f.Vanilla).MissingCargo);
        }

        [Fact]
        public void LatenessOrInterruptionAloneDoesNotInventFailure()
        {
            Fixture f = new Fixture(fail: false);
            f.Travel.TryInterrupt(f.Group.Id, f.Theft.Id, f.Site, f.Vanilla.Now);
            f.Vanilla.AdvanceDays(20);
            string before = WorldStateSerializer.Save(f.World);
            FailedCaravanResult result = f.Establish();
            Assert.False(result.Established);
            Assert.Contains(result.Refusals, s => s.Contains("lateness"));
            Assert.Equal(before, WorldStateSerializer.Save(f.World));
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("unrelated")]
        [InlineData("elsewhere")]
        [InlineData("before_departure")]
        public void MissingOrUnrelatedCauseCannotManufactureAWreck(string invalid)
        {
            Fixture f = new Fixture(fail: false);
            EntityId cause = f.Theft.Id;
            if (invalid == "missing") cause = EntityId.Parse("event_missing");
            else cause = f.World.Record(WorldEventType.Attacked, f.Thief,
                invalid == "unrelated" ? f.Player : f.Driver,
                invalid == "before_departure" ? new GameTime(-1) : f.Vanilla.Now,
                zone: invalid == "elsewhere" ? f.Away : f.Site, threadId: f.Thread.Id).Id;
            f.Travel.TryFail(f.Group.Id, cause, f.Site, f.Vanilla.Now);
            string before = WorldStateSerializer.Save(f.World);
            Assert.False(f.Establish().Established);
            Assert.Empty(f.Stager.Built);
            Assert.Equal(before, WorldStateSerializer.Save(f.World));
        }

        [Fact]
        public void StaleCargoReferenceIsNotRecreatedEvenWhenOtherStolenGoodsRemain()
        {
            Fixture f = new Fixture();
            Assert.True(f.Vanilla.TryTransferItem(f.Cargo, f.Thief, f.Player));
            ItemDescriptor other = new ItemDescriptor(f.World.NewId("item"), "a purse", "goods", 50);
            f.Vanilla.GiveItem(f.Thief, other);
            f.World.Record(WorldEventType.Theft, f.Thief, f.Driver, f.Vanilla.Now,
                zone: f.Site, evidence: new[] { other.Id }, threadId: f.Thread.Id);
            FailedCaravanResult result = f.Establish();
            Assert.False(result.Established);
            Assert.Contains(result.Refusals, s => s.Contains("original caravan cargo"));
            Assert.Empty(f.Stager.Built);
            Assert.DoesNotContain(f.Cargo, f.Vanilla.GetInventory(f.Thief).Select(i => i.Id));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UnknownOrDifferentPhysicalWhereaboutsDoNotBecomeSiteOccupancy(bool unknown)
        {
            Fixture f = new Fixture();
            f.Vanilla.SetZone(f.Thief, unknown ? EntityId.None : f.Away);
            FailedCaravanResult result = f.Establish();
            Assert.False(result.Established);
            Assert.Contains(result.Refusals, s => s.Contains("whereabouts"));
            Assert.Empty(f.Stager.Built);
        }

        [Theory]
        [InlineData("")]
        [InlineData("zone_unrelated")]
        public void UnverifiedOrMisdirectedMaterializationFailsClosedWithoutKnowledgeOrSiteWrites(string binding)
        {
            Fixture f = new Fixture();
            string before = WorldStateSerializer.Save(f.World);
            RefusingStager stager = new RefusingStager { Binding = binding };
            FailedCaravanResult result = f.Establish(stager: stager);
            Assert.False(result.Established);
            Assert.True(stager.SawStructure);
            Assert.NotEmpty(result.Refusals);
            Assert.Equal(before, WorldStateSerializer.Save(f.World));
            Assert.Null(f.World.Registry.GetSite(f.Site));
        }

        private sealed class RefusingStager : ISituationStager
        {
            public bool SawStructure;
            public string Binding;
            public string StageSite(SiteBlueprint blueprint)
            {
                SawStructure = blueprint.Structure != null;
                return Binding;
            }
            public void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone)
                => throw new InvalidOperationException("must not spawn actors");
            public void StageItem(EntityId owner, ItemDescriptor item)
                => throw new InvalidOperationException("must not spawn cargo");
            public string ApplySiteAddition(SiteAdditionBlueprint blueprint) => string.Empty;
        }

        private sealed class Fixture
        {
            public NarrativeWorldState World = new NarrativeWorldState(43);
            public readonly EntityId Player = EntityId.Parse("npc_player");
            public readonly EntityId Site = EntityId.Parse("zone_workings");
            public readonly EntityId Away = EntityId.Parse("zone_away");
            public readonly SandboxVanillaState Vanilla;
            public readonly SandboxStager Stager;
            public readonly TravelingGroupLifecycle Travel;
            public readonly TravelingGroup Group;
            public readonly NarrativeThread Thread;
            public readonly EntityId Thief;
            public readonly EntityId Driver;
            public readonly EntityId Cargo;
            public readonly WorldEvent Theft;
            private readonly SiteGrammar _grammar;
            private readonly SitePieceCatalogue _pieces;

            public Fixture(bool fail = true)
            {
                Vanilla = new SandboxVanillaState(Player);
                Stager = new SandboxStager(Vanilla);
                Travel = new TravelingGroupLifecycle(World, Vanilla);
                Vanilla.Define(Player, zone: Away);
                Thief = Person("Renn", Site);
                Driver = Person("Mab", Away);
                Organization crew = World.Registry.Add(new Organization(World.NewId("org"), "road crew", "bandits"));
                foreach (EntityId member in new[] { Thief, Person("Bryn", Site), Person("Tace", Site) })
                {
                    crew.MemberIds.Add(member);
                    World.Registry.GetNpc(member).OrganizationIds.Add(crew.Id);
                }
                Thread = new NarrativeThread(World.NewId("thread"), FailedCaravanSituation.ArchetypeId, Vanilla.Now);
                Thread.ParticipantIds.Add(Thief);
                Thread.ParticipantIds.Add(Driver);
                World.Threads.Add(Thread);
                ItemDescriptor cargo = new ItemDescriptor(World.NewId("item"), "wine casks", "goods", 400);
                Cargo = cargo.Id;
                Vanilla.GiveItem(Driver, cargo);
                Group = new TravelingGroup(World.NewId("travel"), "caravan", Away,
                    EntityId.Parse("zone_destination"), "carry_wine", Vanilla.Now, Vanilla.Now,
                    Vanilla.Now.PlusDays(5)) { ThreadId = Thread.Id };
                Group.AddMember(Driver);
                Group.AddCargo(Cargo);
                Travel.TryPlan(Group);
                Travel.Advance(Vanilla.Now);
                Vanilla.AdvanceDays(1);
                Assert.True(Vanilla.TryTransferItem(Cargo, Driver, Thief));
                Theft = World.Record(WorldEventType.Theft, Thief, Driver, Vanilla.Now,
                    zone: Site, evidence: new[] { Cargo }, threadId: Thread.Id);
                Thread.OriginEventId = Theft.Id;
                Fact stolen = new Fact(World.NewId("fact"), Thief, "stole", Cargo, "",
                    TruthState.True, originEvent: Theft.Id);
                stolen.EvidenceIds.Add(Cargo);
                World.Knowledge.AddFact(stolen);
                Thread.FactIds.Add(stolen.Id);
                if (fail) Travel.TryFail(Group.Id, Theft.Id, Site, Vanilla.Now);

                DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                    directory = directory.Parent;
                ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(Path.Combine(directory.FullName, "Package", "content.bqc"));
                Assert.Empty(loaded.Diagnostics);
                _grammar = SiteGrammarContent.CreateLibrary(loaded.Bundle, out var diagnostics).Get(ScenarioDungeon.GrammarId);
                Assert.Empty(diagnostics);
                _pieces = SitePieceContent.CreateCatalogue(loaded.Bundle, ScenarioDungeon.Family, out diagnostics);
                Assert.Empty(diagnostics);
            }

            private EntityId Person(string name, EntityId zone)
            {
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name));
                Stager.StageCharacter(npc.Id, new CharacterBlueprint(name), zone);
                return npc.Id;
            }

            public FailedCaravanResult Establish(ulong seed = 4, ISituationStager stager = null)
                => FailedCaravanSituation.Establish(World, Group.Id, "the workings above the road",
                    _grammar, ScenarioDungeon.Family, _pieces, seed, StandardActions.CreateRegistry(),
                    Vanilla, stager ?? Stager, Vanilla.Now);
        }
    }
}
