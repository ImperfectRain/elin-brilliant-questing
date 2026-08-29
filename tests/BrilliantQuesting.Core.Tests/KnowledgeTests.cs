using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class KnowledgeTests
    {
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");

        private static (KnowledgeGraph, Fact) Scene()
        {
            KnowledgeGraph knowledge = new KnowledgeGraph();
            Fact theft = new Fact(EntityId.Parse("fact_1"), Thief, FactPredicates.Stole, EntityId.Parse("item_1"), "silver ring");
            theft.EvidenceIds.Add(EntityId.Parse("item_1"));
            knowledge.AddFact(theft);
            return (knowledge, theft);
        }

        [Fact]
        public void AWorldFactIsNotTheSameAsSomeoneKnowingIt()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();

            Assert.NotNull(knowledge.GetFact(theft.Id));
            Assert.False(knowledge.Knows(Victim, theft.Id));
            Assert.False(knowledge.Knows(Guard, theft.Id));
        }

        [Fact]
        public void SeeingItMeansKnowingIt_ButNotBeingAbleToProveIt()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: false);

            Assert.True(knowledge.Knows(Witness, theft.Id));
            Assert.True(knowledge.BelievesConfidently(Witness, theft.Id));
            Assert.False(knowledge.CanProve(Witness, theft.Id));
        }

        [Fact]
        public void ProvableBeliefNamesWhatProvesIt()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();

            knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);

            ProofLink proof = Assert.Single(knowledge.ProofsFor(Witness, theft.Id));
            Assert.Equal(ProofKind.WitnessTestimony, proof.Kind);
            Assert.Equal(Witness, proof.Entity);
        }

        [Fact]
        public void HearsayLosesConfidenceAndNeverCarriesProof()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            EventLedger ledger = new EventLedger();
            RumorSystem rumors = new RumorSystem(knowledge, ledger, new IdMinter());

            knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
            Assert.True(rumors.Tell(Witness, Victim, theft.Id, GameTime.Zero));
            Assert.True(rumors.Tell(Victim, Guard, theft.Id, GameTime.Zero));

            knowledge.TryGetBelief(Victim, theft.Id, out KnowledgeRecord victimBelief);
            knowledge.TryGetBelief(Guard, theft.Id, out KnowledgeRecord guardBelief);

            Assert.True(victimBelief.Confidence < 1.0);
            Assert.True(guardBelief.Confidence < victimBelief.Confidence);
            Assert.False(victimBelief.CanProve);
            Assert.False(guardBelief.CanProve);
        }

        [Fact]
        public void ProofTravelsOnlyWhenTheEvidenceIsShown()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            RumorSystem rumors = new RumorSystem(knowledge, new EventLedger(), new IdMinter());
            knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);

            rumors.Tell(Witness, Guard, theft.Id, GameTime.Zero, showsProof: true);

            Assert.True(knowledge.CanProve(Guard, theft.Id));
            Assert.Equal(knowledge.ProofsFor(Witness, theft.Id), knowledge.ProofsFor(Guard, theft.Id));
        }

        [Fact]
        public void DestroyingEvidenceStripsProofButNotBelief()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            knowledge.Teach(Victim, theft.Id, KnowledgeSource.Document, 0.9, GameTime.Zero, canProve: true);

            knowledge.RevokeProof(theft.Id);

            Assert.True(knowledge.Knows(Victim, theft.Id));
            Assert.False(knowledge.CanProve(Victim, theft.Id));
        }

        /// <summary>
        /// A claim standing on two objects loses one leg when one of them burns, and keeps the
        /// other. The fact-keyed revocation cannot express that - it takes every physical proof
        /// the claim has - which is why destroying a thing is keyed by the thing.
        /// </summary>
        [Fact]
        public void BurningOneObjectLeavesTheOtherEvidenceStanding()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            EntityId ledger = EntityId.Parse("item_2");
            theft.EvidenceIds.Add(ledger);
            knowledge.Teach(Victim, theft.Id, KnowledgeSource.Document, 0.9, GameTime.Zero, canProve: true);
            Assert.Equal(2, knowledge.ProofsFor(Victim, theft.Id).Count);

            knowledge.RevokeProofOfItem(EntityId.Parse("item_1"));

            Assert.True(knowledge.CanProve(Victim, theft.Id));
            Assert.Equal(ledger, Assert.Single(knowledge.ProofsFor(Victim, theft.Id)).Entity);
        }

        /// <summary>
        /// Selling a thing is not unmaking it. The seller cannot produce it any more; whoever has
        /// it now still can, and both people still know perfectly well what it showed.
        /// </summary>
        [Fact]
        public void PartingWithAnObjectCostsOnlyTheSellerTheirProof()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            EntityId ring = EntityId.Parse("item_1");
            knowledge.Teach(Victim, theft.Id, KnowledgeSource.Document, 0.9, GameTime.Zero, canProve: true);
            knowledge.Teach(Guard, theft.Id, KnowledgeSource.Document, 0.9, GameTime.Zero, canProve: true);

            knowledge.RevokeProofOfItem(Victim, ring);

            Assert.False(knowledge.CanProve(Victim, theft.Id));
            Assert.True(knowledge.Knows(Victim, theft.Id));
            Assert.True(knowledge.CanProve(Guard, theft.Id));
        }

        [Fact]
        public void AStrongerSourceUpgradesABeliefButWeakGossipDoesNotDowngradeIt()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            knowledge.Teach(Guard, theft.Id, KnowledgeSource.Hearsay, 0.4, GameTime.Zero, canProve: false);
            knowledge.Teach(Guard, theft.Id, KnowledgeSource.Document, 0.95, GameTime.Zero, canProve: true);
            knowledge.Teach(Guard, theft.Id, KnowledgeSource.Hearsay, 0.2, GameTime.Zero, canProve: false);

            knowledge.TryGetBelief(Guard, theft.Id, out KnowledgeRecord belief);
            Assert.Equal(0.95, belief.Confidence, 3);
            Assert.True(belief.CanProve);
        }

        [Fact]
        public void CirculationOnlyReachesPeopleWhoWereToldSomething()
        {
            (KnowledgeGraph knowledge, Fact theft) = Scene();
            RumorSystem rumors = new RumorSystem(knowledge, new EventLedger(), new IdMinter());
            knowledge.Teach(Witness, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: false);

            List<EntityId> crowd = new List<EntityId> { Victim, Guard, Thief };
            rumors.Circulate(theft.Id, crowd, GameTime.Zero, new DeterministicRng(5), chancePerListener: 1.0);

            Assert.True(knowledge.Knows(Victim, theft.Id));
            Assert.True(knowledge.Knows(Guard, theft.Id));
        }

        /// <summary>
        /// Burning the evidence takes the object away, not the memory of the person who watched.
        /// Clearing every proof would collapse "witnessed and provable" into "believed but
        /// unprovable", which is the distinction the whole authority layer rests on.
        /// </summary>
        [Fact]
        public void DestroyingEvidenceLeavesAnEyewitnessAbleToTestify()
        {
            EntityId watcher = EntityId.Parse("npc_watcher");
            EntityId holder = EntityId.Parse("npc_holder");
            EntityId ring = EntityId.Parse("item_ring");

            KnowledgeGraph knowledge = new KnowledgeGraph();
            Fact theft = new Fact(EntityId.Parse("fact_1"), EntityId.Parse("npc_thief"), FactPredicates.Stole, ring, "silver ring");
            theft.EvidenceIds.Add(ring);
            knowledge.AddFact(theft);

            // One saw it happen; the other only has the ring.
            knowledge.Teach(watcher, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
            knowledge.Teach(
                holder, theft.Id, KnowledgeSource.Hearsay, 0.9, GameTime.Zero,
                canProve: true,
                proofs: new[] { new ProofLink(ProofKind.PhysicalEvidence, ring) });

            Assert.True(knowledge.CanProve(watcher, theft.Id));
            Assert.True(knowledge.CanProve(holder, theft.Id));

            knowledge.RevokeProof(theft.Id);

            // The ring is gone, so the person relying on it can no longer demonstrate anything -
            // but they still believe it.
            Assert.False(knowledge.CanProve(holder, theft.Id));
            Assert.True(knowledge.Knows(holder, theft.Id));

            // Watching something does not burn.
            Assert.True(knowledge.CanProve(watcher, theft.Id));
        }
    }
}
