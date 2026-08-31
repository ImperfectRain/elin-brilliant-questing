using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Obligations
{
    public sealed class SocialObligationLedger
    {
        private readonly List<SocialObligation> _records = new List<SocialObligation>();

        public IReadOnlyList<SocialObligation> Records => _records;

        public SocialObligation Add(SocialObligation obligation)
        {
            _records.Add(obligation);
            return obligation;
        }

        public void Restore(SocialObligation obligation)
        {
            _records.Add(obligation);
        }

        public SocialObligation Find(EntityId id)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    return _records[i];
                }
            }

            return null;
        }

        public SocialObligation FindOpenFavor(EntityId debtor, EntityId creditor, ActionBinding binding)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                SocialObligation obligation = _records[i];
                if (obligation.Kind == SocialObligationKind.Favor
                    && obligation.IsOpen
                    && obligation.Debtor == debtor
                    && obligation.Creditor == creditor
                    && Matches(obligation, binding))
                {
                    return obligation;
                }
            }

            return null;
        }

        private static bool Matches(SocialObligation obligation, ActionBinding binding)
        {
            if (binding == null)
            {
                binding = ActionBinding.Empty;
            }

            bool hasSubject = !obligation.Subject.IsNone;
            bool hasPurpose = !string.IsNullOrEmpty(obligation.Purpose);
            if (!hasSubject && !hasPurpose)
            {
                return true;
            }

            if (hasSubject
                && (obligation.Subject == binding.PropositionFact
                    || obligation.Subject == binding.Item
                    || obligation.Subject == binding.Destination))
            {
                return true;
            }

            return hasPurpose && obligation.Purpose == (binding.Purpose ?? string.Empty);
        }
    }
}
