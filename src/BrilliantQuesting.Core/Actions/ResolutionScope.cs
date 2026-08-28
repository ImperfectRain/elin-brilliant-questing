using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// One opportunity to act, drawn for one person, spendable once.
    ///
    /// A presentation surface hands the player several options at the same moment. Nothing about
    /// the surface guarantees that only one of them arrives: Elin's own choice buttons can be
    /// triggered by a click and by a number key, `DialogDrama.Deactivate` is an empty method, and
    /// a second click can land before the first resolution has finished changing the world. Every
    /// verb here writes - affinity, karma, inventory, the ledger - so a second arrival is not a
    /// harmless repaint, it is a second consequence for one decision.
    ///
    /// The scope is also what remembers *who* the options were drawn for, so a resolution that
    /// arrives naming somebody else is refused rather than applied to the wrong person.
    /// </summary>
    public sealed class ResolutionScope
    {
        public ResolutionScope(EntityId subject)
        {
            Subject = subject;
        }

        /// <summary>The person these options were offered against.</summary>
        public EntityId Subject { get; }

        /// <summary>True once a resolution has been admitted.</summary>
        public bool IsSpent { get; private set; }

        /// <summary>The action id that spent it, for the log and the inspector.</summary>
        public string SpentBy { get; private set; }

        /// <summary>
        /// Claims the single resolution this scope allows. Returns false, with a reason, when the
        /// scope is already spent or when the claim names somebody other than the person the
        /// options were drawn for. A refused claim never spends the scope.
        /// </summary>
        public bool TryClaim(EntityId subject, string actionId, out string refusal)
        {
            if (subject != Subject)
            {
                refusal = "these options were offered against " + Subject + ", not " + subject;
                return false;
            }

            if (IsSpent)
            {
                refusal = "this conversation already resolved with " + SpentBy;
                return false;
            }

            IsSpent = true;
            SpentBy = actionId;
            refusal = null;
            return true;
        }
    }
}
