using System;
using System.Collections;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// How a body came to hold a piece of information (BQa-018).
    ///
    /// An organization has no eyes. Every one of these names a mechanism somebody could point at
    /// in the save and say "that is how the guild found out", which is the whole reason the channel
    /// is recorded rather than the fact alone: ownership and office are not channels, and a body
    /// that owns the cart does not thereby know who took it.
    /// </summary>
    public enum InstitutionalChannel
    {
        /// <summary>Its own books, reconciled against its own holdings. No reporter.</summary>
        Accounting,

        /// <summary>A member or the leader filed what they hold. The reporter is named.</summary>
        MemberReport,

        /// <summary>Somebody watching a site or holding the body owns. The watcher is named.</summary>
        HoldingObservation,

        /// <summary>An outsider addressed the body: a customer, an authority, another body.</summary>
        ExternalReport
    }

    /// <summary>
    /// Where a receipt is in its life. A receipt that stops standing is closed, never deleted: what
    /// an institution was told and later had corrected is the record of its mistake, and a body
    /// that silently loses the false report it acted on cannot explain why it acted.
    /// </summary>
    public enum ReceiptStanding
    {
        /// <summary>On file and carrying weight. The only standing the body reads from.</summary>
        Filed,

        /// <summary>A later receipt on the same matter took over from it.</summary>
        Corrected,

        /// <summary>Withdrawn by whoever filed it. Carries no weight and never did.</summary>
        Retracted
    }

    /// <summary>
    /// One thing a body has been told, and how.
    ///
    /// Deliberately not a <see cref="KnowledgeRecord"/>: a knowledge record answers "does this
    /// knower hold this claim", and an institution's answer to that has a lifecycle a person's does
    /// not - a filing can be corrected or withdrawn, and the body then stops acting on it while the
    /// person who filed it goes on believing whatever they believe. Where a member files, the
    /// report's confidence and provability are read off that member's own knowledge record rather
    /// than invented here, so this stores provenance and lifecycle and never a second belief.
    ///
    /// The claim may be false, and nothing here checks. A body that acts on a wrong report is
    /// acting exactly as an institution does.
    /// </summary>
    public sealed class InstitutionalReceipt
    {
        public InstitutionalReceipt(
            string id,
            InstitutionalChannel channel,
            EntityId claimId,
            EntityId filedBy,
            double confidence,
            bool provable,
            GameTime filedAt)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A receipt needs an identity.", nameof(id));
            }

            Id = id;
            Channel = channel;
            ClaimId = claimId;
            FiledBy = filedBy;
            Confidence = confidence < 0.0 ? 0.0 : confidence > 1.0 ? 1.0 : confidence;
            Provable = provable;
            FiledAt = filedAt;
        }

        /// <summary>
        /// Derived from what the filing is rather than minted, so that the same filing repeated is
        /// the same receipt instead of a second one, and so that it survives a reload unchanged.
        /// </summary>
        public static string IdentityOf(
            EntityId organizationId, InstitutionalChannel channel, EntityId claimId, EntityId filedBy, GameTime filedAt)
        {
            return "rcpt:" + organizationId.Value + "|" + channel + "|" + claimId.Value
                   + "|" + (filedBy.IsNone ? "-" : filedBy.Value) + "@" + filedAt.TotalMinutes;
        }

        public string Id { get; }

        public InstitutionalChannel Channel { get; }

        /// <summary>The claim the body has been told about. A fact id, true or not.</summary>
        public EntityId ClaimId { get; }

        /// <summary>Who filed it, or none for the body's own books.</summary>
        public EntityId FiledBy { get; }

        public GameTime FiledAt { get; }

        /// <summary>How sure the filing was. The reporter's own confidence where there is one.</summary>
        public double Confidence { get; }

        /// <summary>The filing came with something that would demonstrate the claim.</summary>
        public bool Provable { get; }

        public ReceiptStanding Standing { get; private set; } = ReceiptStanding.Filed;

        /// <summary>Still on file and carrying weight.</summary>
        public bool Stands => Standing == ReceiptStanding.Filed;

        /// <summary>The <see cref="Id"/> of the receipt that took over, when corrected.</summary>
        public string SupersededBy { get; private set; } = string.Empty;

        /// <summary>When it stopped standing. Meaningless while it stands.</summary>
        public GameTime ClosedAt { get; private set; }

        /// <summary>Why it stopped standing, as a code: "corrected", "withdrawn".</summary>
        public string ClosureCode { get; private set; } = string.Empty;

        /// <summary>A later filing on the same matter takes over from this one.</summary>
        public void Correct(InstitutionalReceipt replacement, GameTime when, string code = "corrected")
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (ReferenceEquals(replacement, this))
            {
                throw new ArgumentException("A receipt cannot correct itself.", nameof(replacement));
            }

            Close(ReceiptStanding.Corrected, when, code, replacement.Id);
        }

        /// <summary>Withdrawn. The body stops reading it; the filing stays on the record.</summary>
        public void Retract(GameTime when, string code = "withdrawn")
        {
            Close(ReceiptStanding.Retracted, when, code, string.Empty);
        }

        /// <summary>Puts back exactly what a save recorded, with no policy of its own.</summary>
        public void Restore(ReceiptStanding standing, GameTime closedAt, string closureCode, string supersededBy)
        {
            Standing = standing;
            ClosedAt = standing == ReceiptStanding.Filed ? default : closedAt;
            ClosureCode = standing == ReceiptStanding.Filed ? string.Empty : GoalCodes.Normalize(closureCode);
            SupersededBy = standing == ReceiptStanding.Corrected ? supersededBy ?? string.Empty : string.Empty;
        }

        private void Close(ReceiptStanding standing, GameTime when, string code, string supersededBy)
        {
            Standing = standing;
            ClosedAt = when;
            ClosureCode = GoalCodes.Normalize(code);
            SupersededBy = supersededBy;
        }

        public override string ToString()
        {
            string text = Channel + " on " + ClaimId.Value
                          + (FiledBy.IsNone ? string.Empty : " by " + FiledBy.Value)
                          + " c" + Confidence.ToString("0.00");
            return text + (Stands ? string.Empty : " [" + Standing.ToString().ToLowerInvariant() + "]");
        }
    }

    /// <summary>
    /// What one body has on file, in filing order.
    ///
    /// Bounded, because this is saved and a body that is told the same kind of thing every week
    /// would otherwise grow its own save node without limit. Everything that still stands is kept;
    /// closed receipts are kept to <see cref="MaxClosedRetained"/>, oldest dropped first, so the
    /// recent history of corrections stays readable and the ancient one does not accumulate.
    /// </summary>
    public sealed class InstitutionalReceiptLedger : IReadOnlyList<InstitutionalReceipt>
    {
        /// <summary>How many closed receipts one body keeps.</summary>
        public const int MaxClosedRetained = 16;

        private readonly EntityId _organizationId;
        private readonly List<InstitutionalReceipt> _receipts = new List<InstitutionalReceipt>();

        internal InstitutionalReceiptLedger(EntityId organizationId)
        {
            _organizationId = organizationId;
        }

        public int Count => _receipts.Count;

        public InstitutionalReceipt this[int index] => _receipts[index];

        public IEnumerator<InstitutionalReceipt> GetEnumerator() => _receipts.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Files a receipt, or hands back the identical one already on file. Identity is what the
        /// filing is, so a pass that files the same thing twice does not give the body two of it.
        /// </summary>
        public InstitutionalReceipt File(
            InstitutionalChannel channel,
            EntityId claimId,
            EntityId filedBy,
            double confidence,
            bool provable,
            GameTime when)
        {
            if (claimId.IsNone)
            {
                throw new ArgumentException("A receipt is about a claim.", nameof(claimId));
            }

            string id = InstitutionalReceipt.IdentityOf(_organizationId, channel, claimId, filedBy, when);
            InstitutionalReceipt existing = Find(id);
            if (existing != null)
            {
                return existing;
            }

            InstitutionalReceipt receipt =
                new InstitutionalReceipt(id, channel, claimId, filedBy, confidence, provable, when);
            _receipts.Add(receipt);
            Trim();
            return receipt;
        }

        /// <summary>
        /// A later filing that takes over from whatever still stands on the same claim.
        ///
        /// The correction is a receipt in its own right - it has a channel, a reporter and a time -
        /// and the thing it corrects is closed rather than rewritten, because a body that quietly
        /// edits what it was told cannot afterwards say it was misinformed.
        /// </summary>
        public InstitutionalReceipt Correct(
            EntityId claimId,
            InstitutionalChannel channel,
            EntityId filedBy,
            double confidence,
            bool provable,
            GameTime when,
            string code = "corrected")
        {
            InstitutionalReceipt replacement = File(channel, claimId, filedBy, confidence, provable, when);
            for (int i = 0; i < _receipts.Count; i++)
            {
                InstitutionalReceipt receipt = _receipts[i];
                if (receipt.Stands && receipt.ClaimId == claimId && !ReferenceEquals(receipt, replacement))
                {
                    receipt.Correct(replacement, when, code);
                }
            }

            Trim();
            return replacement;
        }

        /// <summary>
        /// Withdraws everything standing on one claim. True when something was actually withdrawn,
        /// so a caller can tell "taken back" from "was never filed".
        /// </summary>
        public bool Retract(EntityId claimId, GameTime when, string code = "withdrawn")
        {
            bool any = false;
            for (int i = 0; i < _receipts.Count; i++)
            {
                InstitutionalReceipt receipt = _receipts[i];
                if (receipt.Stands && receipt.ClaimId == claimId)
                {
                    receipt.Retract(when, code);
                    any = true;
                }
            }

            if (any)
            {
                Trim();
            }

            return any;
        }

        public InstitutionalReceipt Find(string id)
        {
            for (int i = 0; i < _receipts.Count; i++)
            {
                if (string.Equals(_receipts[i].Id, id, StringComparison.Ordinal))
                {
                    return _receipts[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The receipt this body currently reads for a claim, or null. The latest standing filing
        /// wins: a correction is a later filing, and reading the earliest would make correcting
        /// impossible.
        /// </summary>
        public InstitutionalReceipt Standing(EntityId claimId)
        {
            InstitutionalReceipt latest = null;
            for (int i = 0; i < _receipts.Count; i++)
            {
                InstitutionalReceipt receipt = _receipts[i];
                if (!receipt.Stands || receipt.ClaimId != claimId)
                {
                    continue;
                }

                if (latest == null || receipt.FiledAt.TotalMinutes >= latest.FiledAt.TotalMinutes)
                {
                    latest = receipt;
                }
            }

            return latest;
        }

        /// <summary>Every claim the body currently reads, in stable claim order.</summary>
        public IReadOnlyList<InstitutionalReceipt> StandingReceipts()
        {
            List<InstitutionalReceipt> standing = new List<InstitutionalReceipt>();
            for (int i = 0; i < _receipts.Count; i++)
            {
                if (!_receipts[i].Stands)
                {
                    continue;
                }

                // One reading per claim: two filings that still stand on the same matter are one
                // thing the body believes, and emitting both would weigh it twice.
                InstitutionalReceipt read = Standing(_receipts[i].ClaimId);
                if (!standing.Contains(read))
                {
                    standing.Add(read);
                }
            }

            standing.Sort(ByClaim);
            return standing;
        }

        /// <summary>Puts back a saved receipt without applying the filing rules to it.</summary>
        public void Restore(InstitutionalReceipt receipt)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            _receipts.Add(receipt);
        }

        private static int ByClaim(InstitutionalReceipt left, InstitutionalReceipt right) =>
            string.CompareOrdinal(left.ClaimId.Value, right.ClaimId.Value);

        private void Trim()
        {
            int closed = 0;
            for (int i = 0; i < _receipts.Count; i++)
            {
                if (!_receipts[i].Stands)
                {
                    closed++;
                }
            }

            for (int i = 0; i < _receipts.Count && closed > MaxClosedRetained; i++)
            {
                if (_receipts[i].Stands)
                {
                    continue;
                }

                _receipts.RemoveAt(i--);
                closed--;
            }
        }
    }

    /// <summary>
    /// The filing routes, which are the only legitimate way information reaches a body (BQa-018).
    ///
    /// A member who knows something has not told anybody until somebody files it here, and that is
    /// the point of the whole file: the alternative - reading the union of every member's private
    /// beliefs - gives a guild of forty a collective mind that knows every secret any of them holds,
    /// which is the omniscient institution this step exists to refuse.
    ///
    /// Where a person files, what the body records is read off that person's own knowledge record.
    /// Confidence and provability are theirs, not a fresh number invented for the institution, and
    /// somebody who holds nothing files nothing.
    /// </summary>
    public static class InstitutionalReports
    {
        /// <summary>
        /// A member or the leader tells the body what they hold. Null when they are not in the
        /// body, or hold no such claim - which is the unreported-knowledge case, and is a silence
        /// rather than a failure.
        /// </summary>
        public static InstitutionalReceipt FileMemberReport(
            NarrativeWorldState world, Organization body, EntityId memberId, EntityId claimId, GameTime when)
        {
            if (world == null || body == null || claimId.IsNone)
            {
                return null;
            }

            if (memberId != body.LeaderId && !body.MemberIds.Contains(memberId))
            {
                return null;
            }

            if (!world.Knowledge.TryGetBelief(memberId, claimId, out KnowledgeRecord held))
            {
                return null;
            }

            return body.Receipts.File(
                InstitutionalChannel.MemberReport, claimId, memberId, held.Confidence, held.CanProve, when);
        }

        /// <summary>
        /// Somebody outside the body addresses it: a customer, an authority, a rival. Their own
        /// record is the provenance for the same reason a member's is; an outsider who holds
        /// nothing reports nothing.
        /// </summary>
        public static InstitutionalReceipt FileExternalReport(
            NarrativeWorldState world, Organization body, EntityId reporterId, EntityId claimId, GameTime when)
        {
            if (world == null || body == null || claimId.IsNone || reporterId.IsNone)
            {
                return null;
            }

            if (!world.Knowledge.TryGetBelief(reporterId, claimId, out KnowledgeRecord held))
            {
                return null;
            }

            return body.Receipts.File(
                InstitutionalChannel.ExternalReport, claimId, reporterId, held.Confidence, held.CanProve, when);
        }

        /// <summary>
        /// The body's own books say so. No reporter, and confidence is whole: an institution that
        /// has reconciled its own ledger is not guessing. The claim still has to exist, because a
        /// body cannot reconcile against nothing.
        /// </summary>
        public static InstitutionalReceipt FileAccounting(
            NarrativeWorldState world, Organization body, EntityId claimId, GameTime when)
        {
            if (world == null || body == null || claimId.IsNone || world.Knowledge.GetFact(claimId) == null)
            {
                return null;
            }

            return body.Receipts.File(InstitutionalChannel.Accounting, claimId, EntityId.None, 1.0, false, when);
        }

        /// <summary>
        /// Somebody watching a place the body holds. The watcher is named and the site has to be
        /// one of the body's, because "we own it" is not a way of seeing what happens there.
        /// </summary>
        public static InstitutionalReceipt FileHoldingObservation(
            NarrativeWorldState world, Organization body, EntityId watcherId, EntityId siteId, EntityId claimId, GameTime when)
        {
            if (world == null || body == null || claimId.IsNone || !body.SiteIds.Contains(siteId))
            {
                return null;
            }

            if (!world.Knowledge.TryGetBelief(watcherId, claimId, out KnowledgeRecord held))
            {
                return null;
            }

            return body.Receipts.File(
                InstitutionalChannel.HoldingObservation, claimId, watcherId, held.Confidence, held.CanProve, when);
        }
    }
}
