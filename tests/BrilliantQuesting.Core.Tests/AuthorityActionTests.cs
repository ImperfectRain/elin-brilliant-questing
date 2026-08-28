using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class AuthorityActionTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Guild = EntityId.Parse("npc_guild");
        private static readonly EntityId Court = EntityId.Parse("npc_court");
        private static readonly EntityId Suspect = EntityId.Parse("npc_suspect");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");
        private static readonly EntityId Zone = EntityId.Parse("zone_town");

        [Fact]
        public void GuardActsOnTheSameAccusationWhenThePlayerHasProof()
        {
            Scene scene = CreateScene(Guard, "guard");
            scene.World.Knowledge.Teach(Player, scene.Fact.Id, KnowledgeSource.Document, 1.0, scene.Vanilla.Now, canProve: true);

            ActionOutcome outcome = scene.Report();

            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.CrimeReported);
            Assert.True(scene.World.Knowledge.CanProve(Guard, scene.Fact.Id));
        }

        [Fact]
        public void GuardReboundsTheSameAccusationWhenItIsOnlyBelieved()
        {
            Scene scene = CreateScene(Guard, "guard");
            scene.World.Knowledge.Teach(Player, scene.Fact.Id, KnowledgeSource.Hearsay, 0.8, scene.Vanilla.Now, canProve: false);

            ActionOutcome outcome = scene.Report();

            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.FalseAccusation);
            Assert.True(scene.World.Knowledge.Knows(Suspect, scene.World.Knowledge.FindFact(Player, FactPredicates.Investigating).Id));
            Assert.False(scene.World.Knowledge.Knows(Guard, scene.Fact.Id));
        }

        [Fact]
        public void GuildFilesTheSameAccusationAsRumorAtLowConfidence()
        {
            Scene scene = CreateScene(Guild, "guild clerk");
            scene.World.Knowledge.Teach(Player, scene.Fact.Id, KnowledgeSource.Hearsay, 0.2, scene.Vanilla.Now, canProve: false);

            ActionOutcome outcome = scene.Report();

            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.RumorSpread);
            Assert.False(scene.World.Knowledge.CanProve(Guild, scene.Fact.Id));
        }

        [Fact]
        public void GuildOpensInquiryForBelievedButUnprovableAccusation()
        {
            Scene scene = CreateScene(Guild, "guild clerk");
            scene.World.Knowledge.Teach(Player, scene.Fact.Id, KnowledgeSource.Hearsay, 0.8, scene.Vanilla.Now, canProve: false);

            ActionOutcome outcome = scene.Report();

            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.SecretRevealed);
            Assert.True(scene.World.Knowledge.Knows(Guild, scene.Fact.Id));
            Assert.False(scene.World.Knowledge.CanProve(Guild, scene.Fact.Id));
        }

        [Fact]
        public void CourtWillOnlyActOnPhysicalProof()
        {
            Scene scene = CreateScene(Court, "magistrate");
            scene.World.Knowledge.Teach(
                Player,
                scene.Fact.Id,
                KnowledgeSource.Witnessed,
                1.0,
                scene.Vanilla.Now,
                canProve: true,
                new[] { new ProofLink(ProofKind.WitnessTestimony, Player) });

            ActionOutcome witnessOnly = scene.Report();

            Assert.Contains(witnessOnly.Events, e => e.Type == WorldEventType.SecretRevealed);
            Assert.DoesNotContain(witnessOnly.Events, e => e.Type == WorldEventType.CrimeReported);

            Scene physical = CreateScene(Court, "magistrate");
            physical.World.Knowledge.Teach(Player, physical.Fact.Id, KnowledgeSource.Document, 1.0, physical.Vanilla.Now, canProve: true);

            ActionOutcome physicalProof = physical.Report();

            Assert.Contains(physicalProof.Events, e => e.Type == WorldEventType.CrimeReported);
        }

        [Fact]
        public void ReportIsOnlyOfferedToAuthorityFigures()
        {
            Scene scene = CreateScene(EntityId.Parse("npc_neighbor"), "neighbour");

            Availability availability = StandardActions.CreateRegistry().Get("report").GetAvailability(scene.Context());

            Assert.False(availability.IsAvailable);
            Assert.Contains("no authority", availability.Reason);
        }

        private static Scene CreateScene(EntityId authority, string occupation)
        {
            NarrativeWorldState world = new NarrativeWorldState(99);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            world.Registry.Add(new NarrativeNpc(Suspect, "Suspect"));
            world.Registry.Add(new NarrativeNpc(authority, "Authority") { Occupation = occupation });
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: Zone);
            vanilla.Define(Suspect, zone: Zone);
            vanilla.Define(authority, zone: Zone);
            vanilla.GiveItem(Player, new ItemDescriptor(Ring, "silver ring", "jewelry", 500));
            Fact fact = new Fact(world.NewId("fact"), Suspect, FactPredicates.Stole, Ring, "silver ring");
            fact.EvidenceIds.Add(Ring);
            world.Knowledge.AddFact(fact);
            return new Scene(world, vanilla, authority, fact);
        }

        private sealed class Scene
        {
            public Scene(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId authority, Fact fact)
            {
                World = world;
                Vanilla = vanilla;
                Authority = authority;
                Fact = fact;
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public EntityId Authority { get; }

            public Fact Fact { get; }

            public ActionContext Context()
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, Authority)
                {
                    SubjectFact = Fact.Id
                };
            }

            public ActionOutcome Report()
            {
                return StandardActions.CreateRegistry().Get("report").Perform(Context());
            }
        }
    }
}
