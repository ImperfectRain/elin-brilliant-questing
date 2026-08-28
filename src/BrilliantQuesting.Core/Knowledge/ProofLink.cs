using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    public enum ProofKind
    {
        PhysicalEvidence,
        WitnessTestimony
    }

    /// <summary>Why a character can prove a fact, not merely believe it.</summary>
    public sealed class ProofLink
    {
        public ProofLink(ProofKind kind, EntityId entity)
        {
            Kind = kind;
            Entity = entity;
        }

        public ProofKind Kind { get; }

        /// <summary>The Thing that can be shown, or the witness whose testimony backs the claim.</summary>
        public EntityId Entity { get; }

        public override string ToString() => Kind + ":" + Entity;
    }
}
