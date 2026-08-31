using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class WitnessDisclosureTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");
        private static readonly EntityId Victim = EntityId.Parse("npc_victim");
        private static readonly EntityId Item = EntityId.Parse("item_ring");

        [Fact]
        public void HiddenWitnessIsNotRevealedMerelyBecauseTheyKnowTheFact()
        {
            KnowledgeGraph knowledge = Graph(out EntityId fact);
            knowledge.Teach(Witness, fact, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: false);

            EntityId shown = WitnessDisclosure.KnownWitnessToPlayer(knowledge, Player, fact, Witness, Victim);

            Assert.Equal(EntityId.None, shown);
        }

        [Fact]
        public void TalkingToTheWitnessCanNameTheWitness()
        {
            KnowledgeGraph knowledge = Graph(out EntityId fact);
            knowledge.Teach(Witness, fact, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: false);

            EntityId shown = WitnessDisclosure.KnownWitnessToPlayer(knowledge, Player, fact, Witness, Witness);

            Assert.Equal(Witness, shown);
        }

        [Fact]
        public void HearsayFromTheWitnessNamesTheWitnessButNotTheVictim()
        {
            KnowledgeGraph knowledge = Graph(out EntityId fact);
            knowledge.Teach(Witness, fact, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: false);
            knowledge.Teach(Player, fact, KnowledgeSource.Hearsay, 0.4, GameTime.Zero, false, Witness);

            EntityId shown = WitnessDisclosure.KnownWitnessToPlayer(knowledge, Player, fact, Witness, Victim);

            Assert.Equal(Witness, shown);
            Assert.NotEqual(Victim, shown);
        }

        private static KnowledgeGraph Graph(out EntityId factId)
        {
            KnowledgeGraph knowledge = new KnowledgeGraph();
            Fact fact = new Fact(EntityId.Parse("fact_theft"), Thief, FactPredicates.Stole, Item, "ring");
            knowledge.AddFact(fact);
            factId = fact.Id;
            return knowledge;
        }
    }
}
