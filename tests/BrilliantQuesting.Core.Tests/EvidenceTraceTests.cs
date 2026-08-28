using System.Collections.Generic;
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

        /// <summary>
        /// A binding is not an existence proof. `ExternalRefs` records which game object an id was
        /// bound to so identity survives a reload (BQ-010a); reading it as "the object is still
        /// there" let destroyed evidence go on proving a claim forever, which undoes the point of
        /// making evidence a real object.
        /// </summary>
        [Fact]
        public void AStaleBindingDoesNotKeepDestroyedEvidenceValid()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.TheftFactId,
                KnowledgeSource.Hearsay,
                0.9,
                lab.Vanilla.Now,
                canProve: true,
                proofs: new[] { new ProofLink(ProofKind.PhysicalEvidence, lab.Situation.ItemId) });

            // Bound, so identity survives a reload - and then destroyed.
            lab.World.ExternalRefs[lab.Situation.ItemId] = "5091";
            lab.Vanilla.DestroyItem(lab.Situation.ItemId);

            List<string> invalid = new EvidenceTraceAuditor(lab.World, lab.Vanilla).InvalidProofs();

            Assert.Contains(invalid, line => line.Contains(lab.Situation.TheftFactId.ToString()));
        }

        /// <summary>While somebody is still holding it, it proves what it always did.</summary>
        [Fact]
        public void EvidenceSomebodyIsStillCarryingRemainsValid()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.TheftFactId,
                KnowledgeSource.Hearsay,
                0.9,
                lab.Vanilla.Now,
                canProve: true,
                proofs: new[] { new ProofLink(ProofKind.PhysicalEvidence, lab.Situation.ItemId) });
            lab.World.ExternalRefs[lab.Situation.ItemId] = "5091";

            Assert.Empty(new EvidenceTraceAuditor(lab.World, lab.Vanilla).InvalidProofs());
        }
    }
}
