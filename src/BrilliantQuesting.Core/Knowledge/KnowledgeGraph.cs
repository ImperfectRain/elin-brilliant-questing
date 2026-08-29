using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// The fact store plus the per-character belief index.
    ///
    /// Two queries drive most gameplay: "does this character know X?" (gates what they can say,
    /// report or be blackmailed over) and "can they prove it?" (gates whether authorities act).
    /// </summary>
    public sealed class KnowledgeGraph
    {
        private readonly Dictionary<EntityId, Fact> _facts = new Dictionary<EntityId, Fact>();
        private readonly Dictionary<EntityId, Dictionary<EntityId, KnowledgeRecord>> _beliefs =
            new Dictionary<EntityId, Dictionary<EntityId, KnowledgeRecord>>();

        public IReadOnlyDictionary<EntityId, Fact> Facts => _facts;

        public void AddFact(Fact fact)
        {
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            _facts[fact.Id] = fact;
        }

        public Fact GetFact(EntityId factId)
        {
            _facts.TryGetValue(factId, out Fact fact);
            return fact;
        }

        /// <summary>First fact matching subject/predicate, or null. Generators use this to avoid duplicates.</summary>
        public Fact FindFact(EntityId subject, string predicate)
        {
            foreach (Fact fact in _facts.Values)
            {
                if (fact.Subject == subject && string.Equals(fact.Predicate, predicate, StringComparison.Ordinal))
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>
        /// Records a belief. A stronger source or higher confidence upgrades an existing belief;
        /// weaker hearsay never downgrades something the character saw with their own eyes.
        /// </summary>
        public KnowledgeRecord Teach(EntityId knower, EntityId factId, KnowledgeSource source, double confidence, GameTime now, bool canProve, EntityId toldBy = default)
        {
            return Teach(knower, factId, source, confidence, now, canProve, null, toldBy);
        }

        public KnowledgeRecord Teach(EntityId knower, EntityId factId, KnowledgeSource source, double confidence, GameTime now, bool canProve, IReadOnlyList<ProofLink> proofs, EntityId toldBy = default)
        {
            if (!_facts.TryGetValue(factId, out Fact fact))
            {
                throw new InvalidOperationException("Cannot teach unknown fact " + factId);
            }

            if (!_beliefs.TryGetValue(knower, out Dictionary<EntityId, KnowledgeRecord> byFact))
            {
                byFact = new Dictionary<EntityId, KnowledgeRecord>();
                _beliefs[knower] = byFact;
            }

            IReadOnlyList<ProofLink> proofLinks = ResolveProofs(fact, knower, source, canProve, proofs);

            if (byFact.TryGetValue(factId, out KnowledgeRecord existing))
            {
                if (confidence > existing.Confidence)
                {
                    existing.Confidence = confidence;
                }

                for (int i = 0; i < proofLinks.Count; i++)
                {
                    existing.AddProof(proofLinks[i]);
                }

                return existing;
            }

            KnowledgeRecord record = new KnowledgeRecord(knower, factId, source, Clamp01(confidence), now, toldBy, proofLinks);
            byFact[factId] = record;
            return record;
        }

        public bool Knows(EntityId knower, EntityId factId)
        {
            return TryGetBelief(knower, factId, out _);
        }

        /// <summary>Knows it well enough to act on it rather than merely repeat it.</summary>
        public bool BelievesConfidently(EntityId knower, EntityId factId, double threshold = 0.5)
        {
            return TryGetBelief(knower, factId, out KnowledgeRecord record) && record.Confidence >= threshold;
        }

        public bool CanProve(EntityId knower, EntityId factId)
        {
            return TryGetBelief(knower, factId, out KnowledgeRecord record) && record.CanProve;
        }

        public IReadOnlyList<ProofLink> ProofsFor(EntityId knower, EntityId factId)
        {
            return TryGetBelief(knower, factId, out KnowledgeRecord record)
                ? record.Proofs
                : EmptyProofs;
        }

        public bool TryGetBelief(EntityId knower, EntityId factId, out KnowledgeRecord record)
        {
            record = null;
            return _beliefs.TryGetValue(knower, out Dictionary<EntityId, KnowledgeRecord> byFact)
                   && byFact.TryGetValue(factId, out record);
        }

        public IEnumerable<KnowledgeRecord> BeliefsOf(EntityId knower)
        {
            if (_beliefs.TryGetValue(knower, out Dictionary<EntityId, KnowledgeRecord> byFact))
            {
                return byFact.Values;
            }

            return EmptyBeliefs;
        }

        /// <summary>
        /// The facts these real objects could substantiate - the inverse of
        /// <see cref="Fact.EvidenceIds"/>.
        ///
        /// Investigation reads objects, not fact ids: somebody who picks up a vial has an object
        /// and no idea what it is about, and the only honest way to answer "what does this tell
        /// me?" is to ask the graph which facts hang off it. Without this query every examination
        /// verb would have to be handed the answer by whatever surface offered it, which is the
        /// same as being told.
        ///
        /// Takes a set rather than one id because the caller is normally holding a pack. Asking
        /// per object would walk the whole fact store once per item, on a path that runs every
        /// time the game asks what can be attempted here.
        /// </summary>
        public IEnumerable<Fact> FactsEvidencedBy(ICollection<EntityId> evidenceIds)
        {
            if (evidenceIds == null || evidenceIds.Count == 0)
            {
                yield break;
            }

            foreach (Fact fact in _facts.Values)
            {
                for (int i = 0; i < fact.EvidenceIds.Count; i++)
                {
                    if (evidenceIds.Contains(fact.EvidenceIds[i]))
                    {
                        yield return fact;
                        break;
                    }
                }
            }
        }

        /// <summary>Everyone who currently believes a fact. Used for exposure and blackmail scope.</summary>
        public IEnumerable<EntityId> Knowers(EntityId factId)
        {
            foreach (KeyValuePair<EntityId, Dictionary<EntityId, KnowledgeRecord>> pair in _beliefs)
            {
                if (pair.Value.ContainsKey(factId))
                {
                    yield return pair.Key;
                }
            }
        }

        /// <summary>
        /// Destroying the last piece of physical evidence does not erase beliefs - it strips the
        /// ability to prove them, which is exactly the interesting state.
        ///
        /// Only the physical proof goes. Somebody who watched the theft can still stand up and
        /// say so after the ring is melted down; burning an object does not unmake a memory. That
        /// distinction is the whole reason BQ-018 can tell "witnessed and provable" from
        /// "believed but unprovable", and clearing every proof indiscriminately would have
        /// silently collapsed the two the first time anything called this.
        /// </summary>
        public void RevokeProof(EntityId factId)
        {
            foreach (Dictionary<EntityId, KnowledgeRecord> byFact in _beliefs.Values)
            {
                if (byFact.TryGetValue(factId, out KnowledgeRecord record))
                {
                    record.Proofs.RemoveAll(proof => proof.Kind == ProofKind.PhysicalEvidence);
                }
            }
        }

        private static readonly KnowledgeRecord[] EmptyBeliefs = new KnowledgeRecord[0];
        private static readonly ProofLink[] EmptyProofs = new ProofLink[0];

        private static IReadOnlyList<ProofLink> ResolveProofs(Fact fact, EntityId knower, KnowledgeSource source, bool canProve, IReadOnlyList<ProofLink> explicitProofs)
        {
            if (!canProve)
            {
                return EmptyProofs;
            }

            List<ProofLink> result = new List<ProofLink>();
            if (explicitProofs != null)
            {
                for (int i = 0; i < explicitProofs.Count; i++)
                {
                    AddProof(result, explicitProofs[i]);
                }
            }

            if (result.Count == 0 && (source == KnowledgeSource.Witnessed || source == KnowledgeSource.Participant))
            {
                AddProof(result, new ProofLink(ProofKind.WitnessTestimony, knower));
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < fact.EvidenceIds.Count; i++)
                {
                    AddProof(result, new ProofLink(ProofKind.PhysicalEvidence, fact.EvidenceIds[i]));
                }
            }

            return result;
        }

        private static void AddProof(List<ProofLink> proofs, ProofLink proof)
        {
            if (proof == null || proof.Entity.IsNone)
            {
                return;
            }

            for (int i = 0; i < proofs.Count; i++)
            {
                if (proofs[i].Kind == proof.Kind && proofs[i].Entity == proof.Entity)
                {
                    return;
                }
            }

            proofs.Add(proof);
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }
    }
}
