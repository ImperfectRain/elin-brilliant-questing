using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// Moves beliefs between characters. Deliberately lossy: each retelling costs confidence and
    /// never transfers the ability to prove anything, so a rumour spreads widely while remaining
    /// useless to authorities until someone produces the ledger.
    /// </summary>
    public sealed class RumorSystem
    {
        private readonly KnowledgeGraph _knowledge;
        private readonly EventLedger _ledger;
        private readonly IdMinter _ids;

        public RumorSystem(KnowledgeGraph knowledge, EventLedger ledger, IdMinter ids)
        {
            _knowledge = knowledge;
            _ledger = ledger;
            _ids = ids;
        }

        /// <summary>Confidence retained per retelling.</summary>
        public double TransmissionDecay { get; set; } = 0.7;

        /// <summary>Below this, a character no longer considers the rumour worth repeating.</summary>
        public double GossipFloor { get; set; } = 0.15;

        public bool Tell(EntityId speaker, EntityId listener, EntityId factId, GameTime now, bool showsProof = false)
        {
            if (!_knowledge.TryGetBelief(speaker, factId, out KnowledgeRecord speakerBelief))
            {
                return false;
            }

            double transmitted = speakerBelief.Confidence * TransmissionDecay;
            if (transmitted < GossipFloor)
            {
                return false;
            }

            // Proof only travels when the speaker actually hands over or displays the evidence.
            bool listenerCanProve = showsProof && speakerBelief.CanProve;
            _knowledge.Teach(
                listener,
                factId,
                KnowledgeSource.Hearsay,
                transmitted,
                now,
                listenerCanProve,
                listenerCanProve ? speakerBelief.Proofs : null,
                speaker);

            _ledger.Append(new WorldEvent(
                _ids.Next("evt"),
                WorldEventType.RumorSpread,
                speaker,
                listener,
                now,
                magnitude: transmitted,
                related: new[] { factId }));

            return true;
        }

        /// <summary>
        /// One round of gossip through a group - a tavern, a guild hall, a settlement. Returns the
        /// characters who newly learned the fact so the caller can react without re-scanning.
        /// </summary>
        public List<EntityId> Circulate(EntityId factId, IReadOnlyList<EntityId> population, GameTime now, DeterministicRng rng, double chancePerListener = 0.35)
        {
            List<EntityId> speakers = new List<EntityId>();
            foreach (EntityId knower in _knowledge.Knowers(factId))
            {
                speakers.Add(knower);
            }

            List<EntityId> learned = new List<EntityId>();
            foreach (EntityId speaker in speakers)
            {
                for (int i = 0; i < population.Count; i++)
                {
                    EntityId listener = population[i];
                    if (listener == speaker || _knowledge.Knows(listener, factId))
                    {
                        continue;
                    }

                    if (rng.Chance(chancePerListener) && Tell(speaker, listener, factId, now))
                    {
                        learned.Add(listener);
                    }
                }
            }

            return learned;
        }
    }
}
