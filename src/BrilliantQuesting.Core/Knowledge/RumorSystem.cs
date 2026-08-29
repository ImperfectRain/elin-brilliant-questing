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

        /// <summary>
        /// One retelling.
        ///
        /// <paramref name="factId"/> is what the speaker believes; <paramref name="saidAs"/> is
        /// what the listener is left believing. They are the same thing in an honest retelling,
        /// and separating them is what lets one primitive carry both ways a story goes wrong: a
        /// tale that garbles as it travels, and a person who says something they know is untrue.
        /// Either way the speaker's own confidence is what sets how convincing it was, because
        /// somebody repeating a story they half-believe sounds like somebody repeating a story
        /// they half-believe whatever words they use.
        /// </summary>
        public bool Tell(EntityId speaker, EntityId listener, EntityId factId, GameTime now, bool showsProof = false, EntityId saidAs = default)
        {
            if (!CanTell(speaker, listener, factId, saidAs)
                || !_knowledge.TryGetBelief(speaker, factId, out KnowledgeRecord speakerBelief))
            {
                return false;
            }

            double transmitted = speakerBelief.Confidence * TransmissionDecay;
            EntityId heard = saidAs.IsNone ? factId : saidAs;

            // Proof never travels with a story that changed on the way: the ring in the speaker's
            // hand proves what the speaker did, not what they said about somebody else.
            bool listenerCanProve = showsProof && speakerBelief.CanProve && heard == factId;
            _knowledge.Teach(
                listener,
                heard,
                KnowledgeSource.Hearsay,
                transmitted,
                now,
                listenerCanProve,
                listenerCanProve ? speakerBelief.Proofs : null,
                speaker);

            _ledger.Append(new WorldEvent(
                _ids.Next("evt"),
                heard == factId ? WorldEventType.RumorSpread : WorldEventType.RumorDistorted,
                speaker,
                listener,
                now,
                magnitude: transmitted,
                related: heard == factId ? new[] { factId } : new[] { heard, factId }));

            return true;
        }

        /// <summary>
        /// Whether that retelling would take, asked before anybody opens their mouth.
        ///
        /// <see cref="Tell"/> answers the same question by doing it, which is fine for a
        /// scheduler that can shrug and try somebody else tomorrow. It is not fine for a caller
        /// that has to render the line in the game before the listener may have it: a remark the
        /// player hears and does not learn is a wasted beat, and a remark the player learns and
        /// never hears is the omniscience the whole knowledge layer exists to prevent. Asking
        /// first is also what stops a deterministic picker from choosing the same doomed retelling
        /// every time and starving everything behind it.
        /// </summary>
        public bool CanTell(EntityId speaker, EntityId listener, EntityId factId, EntityId saidAs = default)
        {
            if (!_knowledge.TryGetBelief(speaker, factId, out KnowledgeRecord speakerBelief))
            {
                return false;
            }

            if (speakerBelief.Confidence * TransmissionDecay < GossipFloor)
            {
                return false;
            }

            return !Refuses(listener, saidAs.IsNone ? factId : saidAs);
        }

        /// <summary>
        /// Says something the speaker knows is untrue, and records that they did.
        ///
        /// Two things separate this from a retelling that merely went wrong. The liar is not
        /// weakened by the chain - they are asserting it themselves, to your face, and how
        /// convincing that is comes from them rather than from how many mouths it has been
        /// through. And the lie itself becomes a fact of the world: `X lied_about Y` is true,
        /// nobody but the liar knows it yet, and it is what makes the lie catchable later
        /// (BQ-073) instead of merely regrettable.
        ///
        /// You cannot lie about what you do not know. A speaker who genuinely believes the thing
        /// they are saying is mistaken, not dishonest, and the world should not record otherwise.
        /// </summary>
        public bool Lie(EntityId speaker, EntityId listener, EntityId aboutFactId, EntityId claimFactId, GameTime now, double conviction)
        {
            if (claimFactId == aboutFactId
                || _knowledge.GetFact(claimFactId) == null
                || !_knowledge.TryGetBelief(speaker, aboutFactId, out KnowledgeRecord known)
                || known.Confidence < 0.5)
            {
                return false;
            }

            if (Refuses(listener, claimFactId))
            {
                // The lie was still told, and it still did not take. Recording it anyway would
                // let a liar manufacture a reputation for lying they never earned in anyone's
                // hearing; the honest reading is that nothing happened.
                return false;
            }

            _knowledge.Teach(listener, claimFactId, KnowledgeSource.Hearsay, Clamp01(conviction), now, false, speaker);
            RecordTheLie(speaker, aboutFactId, now);

            _ledger.Append(new WorldEvent(
                _ids.Next("evt"),
                WorldEventType.Deceived,
                speaker,
                listener,
                now,
                magnitude: Clamp01(conviction),
                related: new[] { claimFactId, aboutFactId }));

            return true;
        }

        /// <summary>
        /// Writes down that the lie happened, once per speaker and subject. Reused rather than
        /// minted per telling: a person who repeats the same lie to six people has lied about one
        /// thing, and six identical facts would make the graph a transcript.
        /// </summary>
        private void RecordTheLie(EntityId speaker, EntityId aboutFactId, GameTime now)
        {
            foreach (Fact existing in _knowledge.Facts.Values)
            {
                if (existing.Subject == speaker
                    && existing.Predicate == FactPredicates.LiedAbout
                    && existing.Object == aboutFactId)
                {
                    return;
                }
            }

            Fact lie = new Fact(_ids.Next("fact"), speaker, FactPredicates.LiedAbout, aboutFactId, secrecy: 90);
            _knowledge.AddFact(lie);

            // The liar knows what they did. Nobody else does, and that is the point.
            _knowledge.Teach(speaker, lie.Id, KnowledgeSource.Participant, 1.0, now, false);
        }

        /// <summary>
        /// Whether this listener will simply not have it.
        ///
        /// Two cases, and the first run of circulated false beliefs produced both. Townspeople
        /// were being told they had committed the theft themselves, and believing it. And the
        /// witness who watched Kip take the locket picked up all three rival stories about who
        /// else had taken it, so the people who actually knew the truth held every false version
        /// of it too - which makes "who believes the lie" a question with everybody as its answer
        /// and empties knowledge asymmetry of any content.
        ///
        /// So: nobody accepts a claim about themselves that they are in a position to know is
        /// wrong, and nobody accepts a version that contradicts something they saw or did.
        /// Hearsay against hearsay still competes - that is how one rumour beats another, and it
        /// is what lets the victim be talked round. This only protects first-hand knowledge,
        /// which is the one thing a retelling has no business overwriting.
        /// </summary>
        private bool Refuses(EntityId listener, EntityId heardId)
        {
            Fact heard = _knowledge.GetFact(heardId);
            if (heard == null || heard.DistortionOf.IsNone)
            {
                return false;
            }

            if (listener == heard.Subject)
            {
                return true;
            }

            return _knowledge.TryGetBelief(listener, heard.DistortionOf, out KnowledgeRecord firsthand)
                   && IsFirsthand(firsthand.Source)
                   && firsthand.Confidence >= 0.8;
        }

        private static bool IsFirsthand(KnowledgeSource source)
        {
            return source == KnowledgeSource.Witnessed
                   || source == KnowledgeSource.Participant
                   || source == KnowledgeSource.Document;
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
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
