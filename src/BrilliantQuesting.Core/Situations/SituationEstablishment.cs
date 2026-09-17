using System;
using System.Collections.Generic;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// A committed recognition, owned by its matter. This is provenance, not a new incident,
    /// obligation or claim that somebody knows about the matter. Requirement keys stay transient.
    /// </summary>
    public sealed class SituationEstablishment
    {
        public SituationEstablishment(EntityId id, string producer, string cause,
            IReadOnlyList<SituationActorRequirement> bindings)
        {
            if (id.IsNone || string.IsNullOrEmpty(producer) || string.IsNullOrEmpty(cause))
                throw new ArgumentException("An establishment needs an identity, owner and cause.");
            Id = id;
            ProducerId = producer;
            CauseId = cause;
            var copy = new List<SituationActorRequirement>();
            foreach (SituationActorRequirement binding in bindings)
            {
                if (binding.RequiresCreation || binding.ExistingActor.IsNone)
                    throw new ArgumentException("Committed bindings must name existing entities.");
                copy.Add(binding);
            }
            Bindings = copy.AsReadOnly();
        }

        public EntityId Id { get; }
        public string ProducerId { get; }
        public string CauseId { get; }
        public IReadOnlyList<SituationActorRequirement> Bindings { get; }
    }

    /// <summary>A selection can only be issued by a ranked, admitted pass, for its first offer.</summary>
    public sealed class SelectedSituationProposal
    {
        internal SelectedSituationProposal(NarrativeWorldState world, SituationProposalOffer offer)
        {
            World = world;
            Offer = offer;
        }

        internal NarrativeWorldState World { get; }
        public SituationProposalOffer Offer { get; }

        public SituationEstablishmentResult Fulfill(GameTime now)
        {
            // Routing only. The selected producer owns validation, preparation and establishment.
            if (Offer.Producer is UnresolvedCrimeProducer crime) return crime.Fulfill(World, this, now);
            if (Offer.Producer is DamagedPropertyProducer damage) return damage.Fulfill(World, this, now);
            if (Offer.Producer is ServiceContinuityProducer service) return service.Fulfill(World, this, now);
            return SituationEstablishmentResult.Refused("the selected owner has no atomic fulfillment capability");
        }
    }

    public sealed class SituationEstablishmentResult
    {
        internal SituationEstablishmentResult(NarrativeThread thread, string refusal, List<string> diagnostics = null)
        {
            Thread = thread;
            Refusal = refusal;
            Diagnostics = (diagnostics ?? new List<string>()).AsReadOnly();
        }

        public bool Established => Thread != null;
        public NarrativeThread Thread { get; }
        public string Refusal { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        internal static SituationEstablishmentResult Refused(string reason) => new SituationEstablishmentResult(null, reason);
    }

    // Deterministic exception injection, not callbacks inside a partially staged transaction.
    internal enum EstablishmentFault { None, BeforePrepare, AfterPrepare, BeforeCommit, AfterEventStaged, AfterThreadStaged, AfterCommit }

    public sealed partial class UnresolvedCrimeProducer
    {
        public SituationEstablishmentResult Fulfill(NarrativeWorldState world, SelectedSituationProposal selected, GameTime now)
            => Fulfill(world, selected, now, EstablishmentFault.None);

        internal SituationEstablishmentResult Fulfill(NarrativeWorldState world, SelectedSituationProposal selected,
            GameTime now, EstablishmentFault fault, Action beforeCommit = null)
            => CoreSituationEstablishment.Fulfill(this, world, selected, now, fault, beforeCommit);

        internal static void Fail(EstablishmentFault fault, EstablishmentFault at)
            => CoreSituationEstablishment.Fail(fault, at);
    }

    public sealed partial class DamagedPropertyProducer
    {
        public SituationEstablishmentResult Fulfill(NarrativeWorldState world, SelectedSituationProposal selected, GameTime now)
            => CoreSituationEstablishment.Fulfill(this, world, selected, now);
    }

    public sealed partial class ServiceContinuityProducer
    {
        public SituationEstablishmentResult Fulfill(NarrativeWorldState world, SelectedSituationProposal selected, GameTime now)
            => CoreSituationEstablishment.Fulfill(this, world, selected, now);
    }

    // Shared transaction mechanics, called only by the explicitly supported Core owners above.
    internal static class CoreSituationEstablishment
    {
        internal static SituationEstablishmentResult Fulfill(ISituationProposalProducer owner,
            NarrativeWorldState world, SelectedSituationProposal selected, GameTime now,
            EstablishmentFault fault = EstablishmentFault.None, Action beforeCommit = null)
        {
            if (world == null || selected == null || !ReferenceEquals(selected.World, world)
                || !ReferenceEquals(selected.Offer.Producer, owner))
                return SituationEstablishmentResult.Refused("selection belongs to another world or owner");

            SituationProposalOffer offer = selected.Offer;
            foreach (NarrativeThread existing in world.Threads)
            {
                if (existing.Establishment == null || existing.Establishment.ProducerId != owner.ProducerId
                    || existing.Establishment.CauseId != offer.Cause.DevelopmentId) continue;
                if (existing.State == ThreadState.Quarantined)
                    return SituationEstablishmentResult.Refused("the prior establishment is quarantined");
                return SameBindings(existing.Establishment.Bindings, offer.Proposal.Candidate.ActorRequirements)
                    ? new SituationEstablishmentResult(existing, null)
                    : SituationEstablishmentResult.Refused("the committed recognition has different bindings");
            }

            string refusal = Validate(world, offer, now);
            if (refusal != null) return SituationEstablishmentResult.Refused(refusal);
            try
            {
                Fail(fault, EstablishmentFault.BeforePrepare);
                SituationCandidate candidate = offer.Proposal.Candidate;
                var record = new SituationEstablishment(world.NewId("est"), owner.ProducerId,
                    offer.Cause.DevelopmentId, candidate.ActorRequirements);
                var thread = new NarrativeThread(world.NewId("thread"), candidate.ArchetypeId, now)
                {
                    Establishment = record,
                    OriginEventId = world.NewId("evt"),
                    Tension = Math.Max(0, Math.Min(100, offer.Cause.Urgency))
                };
                foreach (SituationActorRequirement actor in record.Bindings)
                    if (!thread.ParticipantIds.Contains(actor.ExistingActor)) thread.ParticipantIds.Add(actor.ExistingActor);
                foreach (EntityId site in offer.Cause.SiteIds)
                    if (!thread.SiteIds.Contains(site)) thread.SiteIds.Add(site);
                thread.FactIds.Add(offer.Cause.FocusFactId);
                thread.GenerationCauses.AddRange(candidate.Causes);
                var links = new List<CausalLink>
                {
                    new CausalLink(CausalRole.Outcome, record.Id),
                    new CausalLink(CausalRole.Outcome, thread.Id)
                };
                foreach (EntityId origin in offer.Cause.OriginEventIds)
                    links.Add(new CausalLink(CausalRole.About, origin));
                var occurrence = new WorldEvent(thread.OriginEventId, WorldEventType.SituationEstablished,
                    EntityId.None, EntityId.None, now, magnitude: 0, threadId: thread.Id,
                    related: new[] { offer.Cause.FocusFactId },
                    provenance: new EventProvenance(links, new DecisionEvidence("situation.recognized")));
                Fail(fault, EstablishmentFault.AfterPrepare);
                beforeCommit?.Invoke();
                Fail(fault, EstablishmentFault.BeforeCommit);
                // Re-read after preparation; do not trust a pass, a former budget, or former ownership.
                refusal = Validate(world, offer, now);
                if (refusal != null) return SituationEstablishmentResult.Refused(refusal);
                return world.CommitEstablishment(thread, occurrence, fault);
            }
            catch (Exception ex)
            {
                // No callback or published world state is involved in preparation. IDs stay spent.
                return SituationEstablishmentResult.Refused("establishment failed before publication: " + ex.Message);
            }
        }

        private static string Validate(NarrativeWorldState world, SituationProposalOffer offer, GameTime now)
        {
            SituationCandidate candidate = offer.Proposal.Candidate;
            if (candidate.RequiresActorCreation || candidate.NewWeirdPremises.Count != 0
                || candidate.EstablishmentRequirement != "recognition")
                return "the owner cannot fulfill every declared requirement";
            // Recognition needs a recorded cause, including business recognition: a status alone
            // cannot justify inventing the incident behind it.
            if (offer.Cause.FocusFactId.IsNone || offer.Cause.OriginEventIds.Count == 0)
                return "the selected condition has no recorded cause";
            if (offer.Producer is ServiceContinuityProducer)
                foreach (BusinessRecord business in world.Businesses.Records)
                    if (offer.Cause.DevelopmentId == "dev.business_continuity:" + business.BusinessId.Value
                        && (business.BeganAt > now || business.LastChangedAt > now))
                        return "the business condition did not exist at establishment time";
            var current = DevelopmentDetector.Detect(world);
            string refusal = offer.Cause.RevalidationRefusal(world, current, offer.ProducerId)
                ?? world.AttentionBudget.GenerationRefusal(world);
            if (refusal != null) return refusal;
            foreach (NarrativeThread thread in world.Threads)
                if (thread.Establishment != null && thread.Establishment.ProducerId == offer.ProducerId
                    && thread.Establishment.CauseId == offer.Cause.DevelopmentId)
                    return "the condition already has a committed recognition";
            foreach (EntityId site in offer.Cause.SiteIds)
                if (world.Registry.GetSite(site) == null) return "a bound site no longer exists";
            foreach (SituationActorRequirement actor in candidate.ActorRequirements)
                if (!world.Registry.IsActor(actor.ExistingActor)) return "a bound actor no longer participates";
            foreach (Development condition in current)
            {
                if (condition.Id != offer.Cause.DevelopmentId) continue;
                SituationCandidate fresh = offer.Producer.Propose(world, condition);
                if (fresh == null || SituationProposalEcology.BindingKey(fresh) != offer.BindingKey
                    || condition.FocusFactId != offer.Cause.FocusFactId
                    || !SameIds(condition.SiteIds, offer.Cause.SiteIds)
                    || !SameIds(condition.OriginEventIds, offer.Cause.OriginEventIds))
                    return "the selected bindings or cause have changed";
            }
            if (now.TotalMinutes < 0) return "establishment time is invalid";
            foreach (EntityId origin in offer.Cause.OriginEventIds)
            {
                WorldEvent cause = world.Ledger.Find(origin);
                if (cause == null || cause.Time > now) return "the cause has no history at establishment time";
            }
            return null;
        }

        private static bool SameIds(IReadOnlyList<EntityId> a, IReadOnlyList<EntityId> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool SameBindings(IReadOnlyList<SituationActorRequirement> a, IReadOnlyList<SituationActorRequirement> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Role != b[i].Role || a[i].ExistingActor != b[i].ExistingActor || b[i].RequiresCreation) return false;
            return true;
        }

        internal static void Fail(EstablishmentFault fault, EstablishmentFault at)
        {
            if (fault == at) throw new InvalidOperationException("injected failure at " + at);
        }
    }
}
