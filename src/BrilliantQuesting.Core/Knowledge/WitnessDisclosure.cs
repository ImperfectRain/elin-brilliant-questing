using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    public static class WitnessDisclosure
    {
        public static EntityId KnownWitnessToPlayer(
            KnowledgeGraph knowledge,
            EntityId player,
            EntityId factId,
            EntityId witness,
            EntityId currentConversationTarget)
        {
            if (knowledge == null || witness.IsNone)
            {
                return EntityId.None;
            }

            if (currentConversationTarget == witness)
            {
                return witness;
            }

            if (!knowledge.TryGetBelief(player, factId, out KnowledgeRecord playerBelief))
            {
                return EntityId.None;
            }

            if (playerBelief.ToldBy == witness)
            {
                return witness;
            }

            for (int i = 0; i < playerBelief.Proofs.Count; i++)
            {
                ProofLink proof = playerBelief.Proofs[i];
                if (proof.Kind == ProofKind.WitnessTestimony && proof.Entity == witness)
                {
                    return witness;
                }
            }

            return EntityId.None;
        }
    }
}
