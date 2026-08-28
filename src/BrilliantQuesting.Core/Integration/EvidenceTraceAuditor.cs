using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Integration
{
    /// <summary>Checks that proof points at something the world can actually account for.</summary>
    public sealed class EvidenceTraceAuditor
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public EvidenceTraceAuditor(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        public List<string> InvalidProofs()
        {
            List<string> invalid = new List<string>();
            foreach (Fact fact in _world.Knowledge.Facts.Values)
            {
                foreach (EntityId knower in _world.Knowledge.Knowers(fact.Id))
                {
                    if (!_world.Knowledge.TryGetBelief(knower, fact.Id, out KnowledgeRecord belief)
                        || !belief.CanProve)
                    {
                        continue;
                    }

                    bool valid = false;
                    for (int i = 0; i < belief.Proofs.Count; i++)
                    {
                        if (IsValidProof(fact, belief.Proofs[i]))
                        {
                            valid = true;
                            break;
                        }
                    }

                    if (!valid)
                    {
                        invalid.Add(knower + " can prove " + fact.Id + " without real evidence or testimony");
                    }
                }
            }

            return invalid;
        }

        private bool IsValidProof(Fact fact, ProofLink proof)
        {
            switch (proof.Kind)
            {
                case ProofKind.PhysicalEvidence:
                    return fact.EvidenceIds.Contains(proof.Entity) && EvidenceExists(proof.Entity);
                case ProofKind.WitnessTestimony:
                    return WitnessCanTestify(fact.Id, proof.Entity);
                default:
                    return false;
            }
        }

        private bool EvidenceExists(EntityId itemId)
        {
            if (_world.ExternalRefs.ContainsKey(itemId))
            {
                return true;
            }

            if (InventoryContains(_vanilla.PlayerId, itemId))
            {
                return true;
            }

            foreach (NarrativeNpc npc in _world.Registry.Npcs.Values)
            {
                if (InventoryContains(npc.Id, itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool InventoryContains(EntityId owner, EntityId itemId)
        {
            IReadOnlyList<ItemDescriptor> inventory = _vanilla.GetInventory(owner);
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool WitnessCanTestify(EntityId factId, EntityId witness)
        {
            return _world.Knowledge.TryGetBelief(witness, factId, out KnowledgeRecord belief)
                   && (belief.Source == KnowledgeSource.Witnessed || belief.Source == KnowledgeSource.Participant)
                   && belief.Confidence >= 0.5;
        }
    }
}
