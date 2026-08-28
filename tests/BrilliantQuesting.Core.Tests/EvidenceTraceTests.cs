using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class EvidenceTraceTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");

        [Fact]
        public void LaboratoryProofTracesToARealCarriedItem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.TheftFactId,
                KnowledgeSource.Document,
                1.0,
                lab.Vanilla.Now,
                canProve: true);

            Assert.Empty(new EvidenceTraceAuditor(lab.World, lab.Vanilla).InvalidProofs());
        }

        [Fact]
        public void MissingPhysicalEvidenceMakesProvabilityInvalid()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            Fact theft = new Fact(world.NewId("fact"), Player, FactPredicates.Stole, Ring, "silver ring");
            theft.EvidenceIds.Add(Ring);
            world.Knowledge.AddFact(theft);

            world.Knowledge.Teach(Player, theft.Id, KnowledgeSource.Document, 1.0, vanilla.Now, canProve: true);

            Assert.NotEmpty(new EvidenceTraceAuditor(world, vanilla).InvalidProofs());
        }

        [Fact]
        public void WitnessTestimonyIsAValidProofTrail()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            world.Registry.Add(new NarrativeNpc(Player, "Player"));
            world.Registry.Add(new NarrativeNpc(Witness, "Witness"));
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            Fact theft = new Fact(world.NewId("fact"), Player, FactPredicates.Stole, Ring, "silver ring");
            world.Knowledge.AddFact(theft);

            world.Knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, vanilla.Now, canProve: true);
            world.Knowledge.Teach(
                Player,
                theft.Id,
                KnowledgeSource.Hearsay,
                0.8,
                vanilla.Now,
                canProve: true,
                new[] { new ProofLink(ProofKind.WitnessTestimony, Witness) },
                Witness);

            Assert.Empty(new EvidenceTraceAuditor(world, vanilla).InvalidProofs());
        }
    }
}
