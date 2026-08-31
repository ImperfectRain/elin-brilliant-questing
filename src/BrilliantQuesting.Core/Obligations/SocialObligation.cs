using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Obligations
{
    public enum SocialObligationKind
    {
        Debt,
        Promise,
        Sponsorship,
        Sanctuary,
        Grudge,
        Favor
    }

    public enum SocialObligationStatus
    {
        Open,
        Fulfilled,
        Forgiven,
        Broken
    }

    /// <summary>
    /// A concrete social debt between two entities. This is deliberately not affinity: it has a
    /// debtor, a creditor, a source in history and a scope it can be called against.
    /// </summary>
    public sealed class SocialObligation
    {
        public SocialObligation(
            EntityId id,
            SocialObligationKind kind,
            EntityId debtor,
            EntityId creditor,
            EntityId subject,
            string purpose,
            GameTime createdAt,
            EntityId sourceEventId,
            int strength = 1)
        {
            Id = id;
            Kind = kind;
            Debtor = debtor;
            Creditor = creditor;
            Subject = subject;
            Purpose = purpose ?? string.Empty;
            CreatedAt = createdAt;
            SourceEventId = sourceEventId;
            Strength = strength < 1 ? 1 : strength;
            Status = SocialObligationStatus.Open;
            ResolvedAt = GameTime.Zero;
        }

        public EntityId Id { get; }

        public SocialObligationKind Kind { get; }

        public EntityId Debtor { get; }

        public EntityId Creditor { get; }

        public EntityId Subject { get; }

        public string Purpose { get; }

        public GameTime CreatedAt { get; }

        public EntityId SourceEventId { get; }

        public int Strength { get; }

        public SocialObligationStatus Status { get; private set; }

        public GameTime ResolvedAt { get; private set; }

        public bool IsOpen => Status == SocialObligationStatus.Open;

        public void Restore(SocialObligationStatus status, GameTime resolvedAt)
        {
            Status = status;
            ResolvedAt = resolvedAt;
        }

        public void Fulfill(GameTime when)
        {
            Status = SocialObligationStatus.Fulfilled;
            ResolvedAt = when;
        }

        public void Forgive(GameTime when)
        {
            Status = SocialObligationStatus.Forgiven;
            ResolvedAt = when;
        }

        public void Break(GameTime when)
        {
            Status = SocialObligationStatus.Broken;
            ResolvedAt = when;
        }
    }
}
