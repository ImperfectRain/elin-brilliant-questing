using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// What kind of trouble a producer reads, so a pass can say in one word that it is not all the
    /// same trouble. Families, not archetypes: an archetype is a thing that can be built, and
    /// nothing here builds anything.
    /// </summary>
    public static class SituationProposalFamilies
    {
        /// <summary>A wrong or a breakage over a thing somebody holds, works or lives in.</summary>
        public const string Property = "property";

        /// <summary>A trade, a service or a supply that is not serving as it should.</summary>
        public const string Business = "business";

        /// <summary>A debt between people, or a body carrying something of its own.</summary>
        public const string Institution = "institution";
    }

    /// <summary>
    /// Roles the generic proposal layer names, beside the ones an archetype names for itself.
    ///
    /// <see cref="Party"/> exists because a condition usually knows who is in it without knowing
    /// what each of them is to it. The detector refuses to decide who depends on a broken mill
    /// wheel, and a proposal that guessed would be inventing exactly the backstory this step
    /// forbids - so everybody a condition implicates is a party until an authoritative record says
    /// which of them is the actor and which the wronged.
    /// </summary>
    public static class SituationProposalRoles
    {
        /// <summary>Somebody the condition already implicates, bound in the condition's own id order.</summary>
        public const string Party = "party";

        /// <summary>The organization the condition is about, where one is.</summary>
        public const string Body = "body";
    }

    /// <summary>Terms the proposal layer contributes. One, and it is the world's own number.</summary>
    public static class SituationProposalPressures
    {
        /// <summary>
        /// How hard the condition behind the proposal is pressing, taken from the development
        /// rather than recomputed. A second opinion about urgency would be a second authority.
        /// </summary>
        public const string ConditionUrgency = "condition_urgency";
    }

    /// <summary>
    /// The world condition a proposal is bound to, and what it takes to say it still holds.
    ///
    /// Identity is the development's, which is keyed on the condition rather than on the occasion
    /// of noticing it: the same unresolved matter reads the same id on the next pass and after a
    /// reload, so a proposal made twice is recognisably the same proposal rather than a second one
    /// about the same trouble. Nothing here is saved - a cause is a reading, exactly like the
    /// development it names.
    /// </summary>
    public sealed class SituationProposalCause
    {
        internal SituationProposalCause(Development condition)
        {
            DevelopmentId = condition.Id;
            PressureTags = condition.PressureTags;
            ThreadId = condition.ThreadId;
            FocusFactId = condition.FocusFactId;
            SubjectIds = condition.SubjectIds;
            SiteIds = condition.SiteIds;
            OriginEventIds = condition.OriginEventIds;
            Urgency = condition.Urgency;
        }

        /// <summary>The development's stable id. The proposal's cause identity.</summary>
        public string DevelopmentId { get; }

        public IReadOnlyList<string> PressureTags { get; }

        /// <summary>The matter already carrying this condition, or none.</summary>
        public EntityId ThreadId { get; }

        /// <summary>
        /// A condition nothing durable is tracking yet. These are the ordinary case rather than the
        /// edge one: most of what a world is holding has never been anybody's matter.
        /// </summary>
        public bool Threadless => ThreadId.IsNone;

        public EntityId FocusFactId { get; }

        public IReadOnlyList<EntityId> SubjectIds { get; }

        public IReadOnlyList<EntityId> SiteIds { get; }

        public IReadOnlyList<EntityId> OriginEventIds { get; }

        public int Urgency { get; }

        /// <summary>
        /// Why this proposal may no longer be acted on, or null while it may.
        ///
        /// A proposal is a description of a world that was read at some point, and the point of
        /// separating production from fulfilment is that the two need not be the same moment. So
        /// the cause answers the only question a later fulfilment actually has: is the condition
        /// still being derived, does everything it binds still exist, and has something else taken
        /// the matter over in the meantime. A pure read, like everything else here.
        /// </summary>
        public string RevalidationRefusal(NarrativeWorldState world, IReadOnlyList<Development> current)
        {
            if (world == null) return "there is no world to revalidate against";

            Development held = null;
            if (current != null)
            {
                for (int i = 0; i < current.Count && held == null; i++)
                {
                    if (current[i] != null && string.Equals(current[i].Id, DevelopmentId, StringComparison.Ordinal))
                    {
                        held = current[i];
                    }
                }
            }

            if (held == null) return "the world no longer holds " + DevelopmentId;

            if (!FocusFactId.IsNone)
            {
                Fact focus = world.Knowledge.GetFact(FocusFactId);
                if (focus == null) return "the claim " + FocusFactId.Value + " it is about is gone";
            }

            for (int i = 0; i < SubjectIds.Count; i++)
            {
                EntityId subject = SubjectIds[i];
                if (world.Registry.GetNpc(subject) == null && world.Registry.GetOrganization(subject) == null)
                {
                    return world.Registry.NameOf(subject) + " is no longer in the registry";
                }
            }

            // Somebody else's matter may have taken the condition over since it was read, which is
            // read off the condition as it stands now rather than off the thread this cause was
            // captured with - a proposal made about a threadless condition is exactly the one that
            // can be overtaken, and asking the old answer would never notice.
            NarrativeThread carrier = world.GetThread(held.ThreadId);
            if (carrier != null && carrier.State != ThreadState.Resolved)
            {
                return "an existing matter now carries " + DevelopmentId;
            }

            return null;
        }
    }

    /// <summary>
    /// One read-only producer: a family's reading of one condition the world is already holding.
    ///
    /// A producer binds what exists and describes what would still be required. It never commits,
    /// never transfers, never opens a thread, and - the rule this whole step turns on - never
    /// creates the incident that makes its own proposal worth making. Recognition binds a theft
    /// somebody already committed; it does not commit one in order to recognise it.
    /// </summary>
    public interface ISituationProposalProducer
    {
        /// <summary>Stable across passes and saves: half of every proposal key.</summary>
        string ProducerId { get; }

        /// <summary>One of <see cref="SituationProposalFamilies"/>.</summary>
        string Family { get; }

        /// <summary>
        /// What this producer makes of the condition, or null where it makes nothing of it.
        /// Called with the world only so that authoritative records can be read, never written.
        /// </summary>
        SituationCandidate Propose(NarrativeWorldState world, Development condition);
    }

    /// <summary>
    /// A producer's proposal, with the cause it came from and the producer that owns it.
    ///
    /// The owner is recorded because fulfilment is somebody's job and it is not the ranker's: a
    /// selected proposal goes back to the producer that made it, which is the only thing that knows
    /// what its requirements meant.
    /// </summary>
    public sealed class SituationProposalOffer
    {
        internal SituationProposalOffer(
            ISituationProposalProducer producer,
            SituationProposalCause cause,
            SituationProposal proposal,
            string bindingKey)
        {
            Producer = producer;
            Cause = cause;
            Proposal = proposal;
            BindingKey = bindingKey;
        }

        public ISituationProposalProducer Producer { get; }

        public string ProducerId => Producer.ProducerId;

        public string Family => Producer.Family;

        public SituationProposalCause Cause { get; }

        public SituationProposal Proposal { get; }

        /// <summary>
        /// What this proposal actually bound, as one stable string. The cause says which condition
        /// it is about; this says whether a later pass reading that same condition reached the same
        /// people and places, which is a different question and the one a stale binding fails.
        /// </summary>
        public string BindingKey { get; }

        public string Key => Proposal.Key;

        public bool RequiresCreation =>
            Proposal.Candidate.RequiresActorCreation || Proposal.Candidate.NewWeirdPremises.Count > 0
            || Proposal.Candidate.EstablishmentRequirement != null;

        /// <summary>Inspector-only. Describes requirements; asserts nothing about fulfilling them.</summary>
        public string Explain()
        {
            return ProducerId + ": " + Proposal.Explain()
                   + "; cause=" + Cause.DevelopmentId
                   + (Cause.Threadless ? " (threadless)" : " (carried by " + Cause.ThreadId.Value + ")")
                   + "; bindings=" + BindingKey;
        }
    }

    /// <summary>A proposal the world supported and an existing authority refused, with the reason.</summary>
    public sealed class SuppressedProposal
    {
        internal SuppressedProposal(SituationProposalOffer offer, string reason)
        {
            Offer = offer;
            Reason = reason;
        }

        public SituationProposalOffer Offer { get; }

        /// <summary>Inspector-only. Why an otherwise honest proposal was set aside.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// What the world could currently be asked for, ranked, with what it was not allowed to ask
    /// for and why.
    ///
    /// Nothing in here has happened. A pass is a description of a read, and reading it twice with
    /// the same state produces the same description.
    /// </summary>
    public sealed class SituationProposalPass
    {
        private static readonly SituationProposalOffer[] NoOffers = new SituationProposalOffer[0];
        private static readonly SuppressedProposal[] NothingSuppressed = new SuppressedProposal[0];
        private static readonly string[] NoFamilies = new string[0];
        private static readonly Development[] NoConditions = new Development[0];

        internal SituationProposalPass(
            NarrativeWorldState world,
            IReadOnlyList<Development> conditions,
            IReadOnlyList<SituationProposalOffer> offers,
            IReadOnlyList<SuppressedProposal> suppressed,
            IReadOnlyList<string> families)
        {
            _world = world;
            Conditions = conditions ?? NoConditions;
            Offers = offers ?? NoOffers;
            Suppressed = suppressed ?? NothingSuppressed;
            Families = families ?? NoFamilies;
        }

        private readonly NarrativeWorldState _world;

        /// <summary>Select the admitted winner without allocation or fulfillment.</summary>
        public SelectedSituationProposal Select() => Offers.Count == 0 ? null : new SelectedSituationProposal(_world, Offers[0]);

        /// <summary>The conditions the pass was read from, in the detector's stable order.</summary>
        public IReadOnlyList<Development> Conditions { get; }

        /// <summary>Admitted proposals, best first, through the existing ranking authority.</summary>
        public IReadOnlyList<SituationProposalOffer> Offers { get; }

        public IReadOnlyList<SuppressedProposal> Suppressed { get; }

        /// <summary>The distinct families among <see cref="Offers"/>, in ordinal order.</summary>
        public IReadOnlyList<string> Families { get; }

        public SituationProposalOffer Best => Offers.Count == 0 ? null : Offers[0];

        /// <summary>The offer with this key, or null. Fulfilment routes back through this.</summary>
        public SituationProposalOffer Find(string key)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (string.Equals(Offers[i].Key, key, StringComparison.Ordinal)) return Offers[i];
            }

            return null;
        }

        /// <summary>Inspector-only account of the pass. Reads nothing it has not already read.</summary>
        public string Explain()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("situation proposals: ").Append(Offers.Count.ToString(CultureInfo.InvariantCulture))
              .Append(" from ").Append(Conditions.Count.ToString(CultureInfo.InvariantCulture))
              .Append(Conditions.Count == 1 ? " condition" : " conditions")
              .Append(" across ").Append(Families.Count.ToString(CultureInfo.InvariantCulture))
              .Append(Families.Count == 1 ? " family" : " families");
            for (int i = 0; i < Families.Count; i++) sb.Append(i == 0 ? ": " : ", ").Append(Families[i]);
            sb.Append('\n');

            for (int i = 0; i < Offers.Count; i++) sb.Append("  ").Append(Offers[i].Explain()).Append('\n');

            for (int i = 0; i < Suppressed.Count; i++)
            {
                sb.Append("  suppressed ").Append(Suppressed[i].Offer.Key)
                  .Append(": ").Append(Suppressed[i].Reason).Append('\n');
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// The producers the world is currently read through, and the one pass that reads them.
    ///
    /// Before this, generic production was one settlement's theft arithmetic: everything the world
    /// could be asked for came out of one pressure family, and a town with a failed shop, an open
    /// debt and a guild with plans in it proposed nothing at all. The ecology is the other half of
    /// the seam BQ-103 built - selection already knew how to compare reuse against hypothetical
    /// creation, and had one producer to compare.
    ///
    /// Three rules hold for every producer and are what make the set safe to grow:
    ///
    /// <list type="bullet">
    /// <item><b>Read-only.</b> A pass reads authoritative state and derived pressure. It writes
    /// nothing, mints no id, opens no thread and moves no object. Requirements are described and
    /// never satisfied - fulfilment is BQa-022's, after a proposal has actually won.</item>
    /// <item><b>Binds existing causes.</b> A producer reads a condition the world was already
    /// holding. It cannot commit the theft, empty the shelf or invent the witness that would make
    /// its own proposal worth making; <see cref="SettlementSituationGenerator.TryGenerateSelected"/>
    /// performs a founding transfer and is explicit staging, not recognition.</item>
    /// <item><b>Stable identity.</b> The key is the producer and the condition, both stable across
    /// passes and reloads, so the same trouble read twice is one proposal and the cause can be
    /// asked again later whether it still holds.</item>
    /// </list>
    /// </summary>
    public sealed class SituationProposalEcology
    {
        private static readonly Development[] NoConditions = new Development[0];

        private readonly List<ISituationProposalProducer> _producers;

        public SituationProposalEcology(IEnumerable<ISituationProposalProducer> producers)
        {
            if (producers == null) throw new ArgumentNullException(nameof(producers));
            _producers = new List<ISituationProposalProducer>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISituationProposalProducer producer in producers)
            {
                if (producer == null) throw new ArgumentException("Null producer.", nameof(producers));
                if (!seen.Add(producer.ProducerId))
                    throw new ArgumentException("Duplicate producer id: " + producer.ProducerId, nameof(producers));
                _producers.Add(producer);
            }
        }

        /// <summary>
        /// The supported producers, enumerated rather than discovered: property, business and
        /// social/institutional readings over the stores that already own them.
        /// </summary>
        public static SituationProposalEcology Standard()
        {
            return new SituationProposalEcology(new ISituationProposalProducer[]
            {
                new UnresolvedCrimeProducer(),
                new DamagedPropertyProducer(),
                new ServiceContinuityProducer(),
                new LocalSupplyProducer(),
                new OpenObligationProducer(),
                new InstitutionalMandateProducer()
            });
        }

        public IReadOnlyList<ISituationProposalProducer> Producers => _producers;

        /// <summary>Reads the whole world's conditions and proposes over them.</summary>
        public SituationProposalPass Read(NarrativeWorldState world)
        {
            return Read(world, world == null ? NoConditions : DevelopmentDetector.Detect(world));
        }

        /// <summary>
        /// Proposes over conditions somebody else has already read - normally a bounded pass's
        /// own detection, so a live consumer pays for one reading rather than two.
        /// </summary>
        public SituationProposalPass Read(NarrativeWorldState world, IReadOnlyList<Development> conditions)
        {
            if (world == null || conditions == null || conditions.Count == 0)
            {
                return new SituationProposalPass(world, conditions, null, null, null);
            }

            // The director's own admission gate, asked once for the pass and not re-derived here.
            string admission = world.AttentionBudget.GenerationRefusal(world);

            var proposals = new List<SituationProposal>();
            var byKey = new Dictionary<string, SituationProposalOffer>(StringComparer.Ordinal);
            var suppressed = new List<SuppressedProposal>();

            for (int c = 0; c < conditions.Count; c++)
            {
                Development condition = conditions[c];
                if (condition == null) continue;

                for (int p = 0; p < _producers.Count; p++)
                {
                    ISituationProposalProducer producer = _producers[p];
                    SituationCandidate candidate = producer.Propose(world, condition);
                    if (candidate == null) continue;

                    var cause = new SituationProposalCause(condition);
                    string key = producer.ProducerId + "/" + condition.Id;
                    var offer = new SituationProposalOffer(
                        producer, cause, new SituationProposal(key, candidate), BindingKey(candidate));

                    // Repetition first, then admission - the order the settlement owner already
                    // uses, and for the same reason: a matter somebody is already telling is not
                    // refused by a budget, it is simply not a proposal.
                    NarrativeThread carrier = world.GetThread(condition.ThreadId);
                    if (carrier != null && carrier.State != ThreadState.Resolved)
                    {
                        suppressed.Add(new SuppressedProposal(
                            offer, "an existing matter already carries " + condition.Id));
                        continue;
                    }

                    if (admission != null)
                    {
                        suppressed.Add(new SuppressedProposal(offer, admission));
                        continue;
                    }

                    proposals.Add(offer.Proposal);
                    byKey.Add(key, offer);
                }
            }

            IReadOnlyList<SituationProposal> ranked = SituationProposalSelection.Rank(proposals);
            var offers = new List<SituationProposalOffer>(ranked.Count);
            var families = new List<string>();
            for (int i = 0; i < ranked.Count; i++)
            {
                SituationProposalOffer offer = byKey[ranked[i].Key];
                offers.Add(offer);
                if (!families.Contains(offer.Family)) families.Add(offer.Family);
            }

            families.Sort(StringComparer.Ordinal);
            return new SituationProposalPass(
                world, conditions, offers.AsReadOnly(), suppressed.AsReadOnly(), families.AsReadOnly());
        }

        /// <summary>
        /// Everything a candidate actually bound, in the candidate's own sorted requirement order
        /// plus its declared requirements, so two readings of one condition can be compared.
        /// </summary>
        internal static string BindingKey(SituationCandidate candidate)
        {
            var sb = new StringBuilder(candidate.ArchetypeId);
            foreach (SituationActorRequirement actor in candidate.ActorRequirements)
            {
                sb.Append('|').Append(actor.Role).Append('=')
                  .Append(actor.RequiresCreation ? "new:" + actor.CreationKey : actor.ExistingActor.Value);
            }

            EntityId place = candidate.SiteIn(SituationRoles.Place);
            if (!place.IsNone) sb.Append("|place=").Append(place.Value);
            for (int i = 0; i < candidate.NewWeirdPremises.Count; i++)
                sb.Append("|premise=new:").Append(candidate.NewWeirdPremises[i]);
            if (candidate.EstablishmentRequirement != null)
                sb.Append("|establishment=new:").Append(candidate.EstablishmentRequirement);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Shared, boring binding work: the terms and the parties every family reads the same way.
    ///
    /// Only what the condition itself names is bound. A site it names is the place, a person it
    /// names is a party and a body it names is a body; nothing is looked up, inferred or filled in
    /// from elsewhere, because a proposal that quietly widened its own cast would be the first step
    /// back towards a universal generator.
    /// </summary>
    internal static class ProposalBinding
    {
        internal static SituationCandidateBuilder Open(
            NarrativeWorldState world, Development condition, string archetypeId, string because)
        {
            var builder = new SituationCandidateBuilder(archetypeId);
            builder.Pressure(
                SituationProposalPressures.ConditionUrgency,
                condition.Urgency,
                because);
            builder.Cause("cause: " + condition.Id + ", "
                          + (condition.ThreadId.IsNone
                              ? "carried by no matter yet"
                              : "carried by matter " + condition.ThreadId.Value));

            for (int i = 0; i < condition.SiteIds.Count; i++)
            {
                if (i == 0) builder.BindSite(SituationRoles.Place, condition.SiteIds[i]);
                else builder.Cause("also at " + world.Registry.NameOf(condition.SiteIds[i]));
            }

            return builder;
        }

        /// <summary>Binds everybody the condition names, and says how many that was.</summary>
        internal static int BindParties(
            NarrativeWorldState world, Development condition, SituationCandidateBuilder builder)
        {
            int bound = 0;
            for (int i = 0; i < condition.SubjectIds.Count; i++)
            {
                EntityId subject = condition.SubjectIds[i];
                if (world.Registry.GetNpc(subject) != null)
                {
                    builder.Bind(SituationProposalRoles.Party, subject);
                    bound++;
                }
                else if (world.Registry.GetOrganization(subject) != null)
                {
                    builder.Bind(SituationProposalRoles.Body, subject);
                    bound++;
                }
            }

            return bound;
        }

        /// <summary>The candidate, or null where the condition gave it nothing existing to be about.</summary>
        internal static SituationCandidate Close(SituationCandidateBuilder builder, int boundParties)
        {
            SituationCandidate candidate = builder.Build();
            bool hasPlace = !candidate.SiteIn(SituationRoles.Place).IsNone;
            return boundParties == 0 && !hasPlace ? null : candidate;
        }

        internal static bool Tagged(Development condition, string tag) => condition.HasPressure(tag);
    }

    /// <summary>
    /// Property: a wrong the world holds as true that nothing has answered.
    ///
    /// The one producer where roles are asserted rather than left generic, because the claim itself
    /// asserts them: a "stole" fact names who took and who was taken from. It binds that theft; it
    /// does not perform one. Whether the wrong can be recovered, proved or repaid is nothing this
    /// says - it says only that the world is holding it and that these are the people in it.
    /// </summary>
    public sealed partial class UnresolvedCrimeProducer : ISituationProposalProducer
    {
        public const string Archetype = "property_recovery";

        public string ProducerId => "property/unresolved_crime";

        public string Family => SituationProposalFamilies.Property;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            if (!ProposalBinding.Tagged(condition, DevelopmentPressures.UnresolvedCrime)) return null;

            Fact wrong = condition.FocusFactId.IsNone ? null : world.Knowledge.GetFact(condition.FocusFactId);
            if (wrong == null) return null;

            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                "the world holds " + world.Registry.NameOf(wrong.Subject) + " " + wrong.Predicate
                + " " + world.Registry.NameOf(wrong.Object) + " with nothing recording it answered");
            builder.RequireEstablishmentRecord("recognition");

            int bound = 0;
            if (world.Registry.GetNpc(wrong.Subject) != null)
            {
                builder.Bind(SituationRoles.Actor, wrong.Subject);
                bound++;
            }

            if (world.Registry.GetNpc(wrong.Object) != null)
            {
                builder.Bind(SituationRoles.Target, wrong.Object);
                bound++;
            }

            // Anybody else the condition implicates without the claim naming what they are to it.
            for (int i = 0; i < condition.SubjectIds.Count; i++)
            {
                EntityId subject = condition.SubjectIds[i];
                if (subject == wrong.Subject || subject == wrong.Object) continue;
                if (world.Registry.GetNpc(subject) == null) continue;
                builder.Bind(SituationProposalRoles.Party, subject);
                bound++;
            }

            return ProposalBinding.Close(builder, bound);
        }
    }

    /// <summary>
    /// Property: something is broken, blighted or spoiled and has not been put right.
    ///
    /// Most damage is nobody's crime, and the claim says so by naming a place rather than a
    /// culprit. So this binds the thing and whoever the claim names as depending on it, and
    /// proposes nothing about how it came to be broken.
    /// </summary>
    public sealed class DamagedPropertyProducer : ISituationProposalProducer
    {
        public const string Archetype = "property_repair";

        public string ProducerId => "property/damaged";

        public string Family => SituationProposalFamilies.Property;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            if (!ProposalBinding.Tagged(condition, DevelopmentPressures.DamagedProperty)) return null;

            Fact damage = condition.FocusFactId.IsNone ? null : world.Knowledge.GetFact(condition.FocusFactId);
            if (damage == null) return null;

            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                "the world holds " + world.Registry.NameOf(damage.Subject) + " damaged"
                + (string.IsNullOrEmpty(damage.Value) ? string.Empty : " (" + damage.Value + ")")
                + " with nothing recording it repaired");

            // The damaged thing is a place in its own right even where the condition named no site.
            if (!damage.Subject.IsNone && world.Registry.GetNpc(damage.Subject) == null)
            {
                builder.BindSite(SituationRoles.Place, damage.Subject);
            }

            int bound = ProposalBinding.BindParties(world, condition, builder);
            return ProposalBinding.Close(builder, bound);
        }
    }

    /// <summary>
    /// Business: a tracked trade that is not serving as it should, or one coming back.
    ///
    /// Both states propose, because a replacement operator needing custom is as much a reason to
    /// act as a shuttered counter - and a producer that only ever read failure would describe a
    /// town nobody recognises. The live shop surface is not read here at all: an operator asleep is
    /// the working day, and the ledger's durable meaning is the only thing that presses.
    /// </summary>
    public sealed class ServiceContinuityProducer : ISituationProposalProducer
    {
        public const string Archetype = "service_continuity";

        public string ProducerId => "business/service_continuity";

        public string Family => SituationProposalFamilies.Business;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            bool interrupted = ProposalBinding.Tagged(condition, DevelopmentPressures.ServiceInterruption);
            bool recovering = ProposalBinding.Tagged(condition, DevelopmentPressures.Recovering);
            if (!interrupted && !recovering) return null;

            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                interrupted
                    ? "the business ledger records this trade interrupted"
                    : "the business ledger records this trade recovering and still owed custom");

            int bound = ProposalBinding.BindParties(world, condition, builder);
            return ProposalBinding.Close(builder, bound);
        }
    }

    /// <summary>
    /// Business: somebody, or somewhere, is short of something.
    ///
    /// The coarse categories BQ-050 tracks and the stated needs behind them, which the detector has
    /// already merged into one condition where they are the same shortage. No inventory, shop stock
    /// or vanilla economy is read, and nothing is inferred from what a town ate today.
    /// </summary>
    public sealed class LocalSupplyProducer : ISituationProposalProducer
    {
        public const string Archetype = "local_supply";

        public string ProducerId => "business/local_supply";

        public string Family => SituationProposalFamilies.Business;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            if (!ProposalBinding.Tagged(condition, DevelopmentPressures.Shortage)) return null;

            Fact need = condition.FocusFactId.IsNone ? null : world.Knowledge.GetFact(condition.FocusFactId);
            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                need == null || string.IsNullOrEmpty(need.Value)
                    ? "the demand ledger records this place short and unrelieved"
                    : "the demand ledger records an unrelieved want of " + need.Value);

            int bound = 0;
            if (need != null && world.Registry.GetNpc(need.Subject) != null)
            {
                builder.Bind(SituationRoles.Target, need.Subject);
                bound++;
            }

            for (int i = 0; i < condition.SubjectIds.Count; i++)
            {
                EntityId subject = condition.SubjectIds[i];
                if (need != null && subject == need.Subject) continue;
                if (world.Registry.GetNpc(subject) == null) continue;
                builder.Bind(SituationProposalRoles.Party, subject);
                bound++;
            }

            return ProposalBinding.Close(builder, bound);
        }
    }

    /// <summary>
    /// Social: a debt nobody has settled, forgiven or broken.
    ///
    /// The debtor and the creditor are both parties and neither is named as such, because the
    /// condition does not name them: an obligation is pressure between two people rather than about
    /// a claim, which is why it has no focus fact and why a proposal about it must not pretend to
    /// know which way round it runs.
    /// </summary>
    public sealed class OpenObligationProducer : ISituationProposalProducer
    {
        public const string Archetype = "obligation_settlement";

        public string ProducerId => "institution/open_obligation";

        public string Family => SituationProposalFamilies.Institution;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            if (!ProposalBinding.Tagged(condition, DevelopmentPressures.UnmetObligation)) return null;

            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                ProposalBinding.Tagged(condition, DevelopmentPressures.Adversarial)
                    ? "the obligation ledger records an open debt held against somebody"
                    : "the obligation ledger records an open debt owed to somebody");

            int bound = ProposalBinding.BindParties(world, condition, builder);
            return ProposalBinding.Close(builder, bound);
        }
    }

    /// <summary>
    /// Institutional: a body carrying a goal of its own that nothing has satisfied.
    ///
    /// The one producer that declares a requirement, and it declares exactly one: a body whose own
    /// records show an unsatisfied goal and nobody on its roll to carry it needs a hand before
    /// anything of its own can happen. That purpose is the body's, recorded before this was read
    /// and independent of the proposal - which is the whole difference between a requirement and a
    /// backstory. The requirement stays a description: no id is reserved, no actor exists, and
    /// nothing here can make one.
    /// </summary>
    public sealed class InstitutionalMandateProducer : ISituationProposalProducer
    {
        public const string Archetype = "institutional_mandate";

        public string ProducerId => "institution/mandate";

        public string Family => SituationProposalFamilies.Institution;

        public SituationCandidate Propose(NarrativeWorldState world, Development condition)
        {
            if (!ProposalBinding.Tagged(condition, DevelopmentPressures.OrganizationStake)) return null;

            Organization body = null;
            for (int i = 0; i < condition.SubjectIds.Count && body == null; i++)
            {
                body = world.Registry.GetOrganization(condition.SubjectIds[i]);
            }

            if (body == null) return null;

            SituationCandidateBuilder builder = ProposalBinding.Open(
                world, condition, Archetype,
                ProposalBinding.Tagged(condition, DevelopmentPressures.Opportunity)
                    ? body.Name + " records an unsatisfied goal it stands to gain by"
                    : body.Name + " records an unsatisfied goal of its own");

            int bound = ProposalBinding.BindParties(world, condition, builder);

            bool hasHand = world.Registry.GetNpc(body.LeaderId) != null;
            for (int i = 0; i < body.MemberIds.Count && !hasHand; i++)
            {
                hasHand = world.Registry.GetNpc(body.MemberIds[i]) != null;
            }

            if (!hasHand)
            {
                builder.RequireNewActor(SituationProposalRoles.Party, "mandate/" + body.Id.Value + "/hand");
                builder.Cause("requires: " + body.Name
                              + " has nobody on its roll to carry a goal it already holds");
            }

            return ProposalBinding.Close(builder, bound);
        }
    }
}
