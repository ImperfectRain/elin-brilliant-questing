using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>What a single causal reference turned out to point at, once it was looked up.</summary>
    public sealed class ResolvedCause
    {
        public ResolvedCause(CausalRole role, EntityId reference, WorldEvent occurrence, Fact claim, NarrativeThread matter)
        {
            Role = role;
            Reference = reference;
            Occurrence = occurrence;
            Claim = claim;
            Matter = matter;
        }

        public CausalRole Role { get; }

        public EntityId Reference { get; }

        /// <summary>The event it names, when it names one that is still in the ledger.</summary>
        public WorldEvent Occurrence { get; }

        /// <summary>The claim it names, when it names one the knowledge graph still holds.</summary>
        public Fact Claim { get; }

        /// <summary>The matter it names, when it names one the world still carries.</summary>
        public NarrativeThread Matter { get; }

        /// <summary>
        /// The reference survived but what it pointed at did not - a fact dropped with a
        /// quarantined matter, a record an older save never had. The link is still reported,
        /// because a dangling reference is information and deleting it would be a quiet lie.
        /// </summary>
        public bool IsMissing => Occurrence == null && Claim == null && Matter == null;

        public override string ToString()
        {
            if (Occurrence != null) return Role + " " + Occurrence.Type + " " + Reference;
            if (Claim != null) return Role + " claim " + Reference;
            if (Matter != null) return Role + " matter " + Reference;
            return Role + " " + Reference + " (missing)";
        }
    }

    /// <summary>
    /// The causal reading of one recorded event: its objective causes, what it is about, the
    /// motive evidence behind it and what it produced, each resolved against the world.
    ///
    /// Three kinds of thing are deliberately kept apart, because collapsing them is the mistake
    /// this whole seam exists to prevent. <see cref="Triggers"/> and <see cref="About"/> are
    /// occurrences the world holds to have happened. <see cref="Motives"/> are what the actor
    /// acted on, which may be perfectly false. <see cref="Outcomes"/> are what the act produced.
    /// </summary>
    public sealed class CausalReading
    {
        private static readonly ResolvedCause[] None = new ResolvedCause[0];

        internal CausalReading(WorldEvent worldEvent, IReadOnlyList<ResolvedCause> causes)
        {
            Event = worldEvent;
            Causes = causes ?? None;
            Triggers = InRole(Causes, CausalRole.Trigger);
            About = InRole(Causes, CausalRole.About);
            Motives = InRole(Causes, CausalRole.Motive);
            Outcomes = InRole(Causes, CausalRole.Outcome);
        }

        public WorldEvent Event { get; }

        public IReadOnlyList<ResolvedCause> Causes { get; }

        /// <summary>Occurrences that prompted this one.</summary>
        public IReadOnlyList<ResolvedCause> Triggers { get; }

        /// <summary>Occurrences this one names without having been prompted by them.</summary>
        public IReadOnlyList<ResolvedCause> About { get; }

        /// <summary>What the actor acted on. Motive evidence, not a claim about the world.</summary>
        public IReadOnlyList<ResolvedCause> Motives { get; }

        /// <summary>What this occurrence produced.</summary>
        public IReadOnlyList<ResolvedCause> Outcomes { get; }

        /// <summary>The reason codes behind a committed decision, when one was retained.</summary>
        public DecisionEvidence Decision => Event.Provenance.Decision;

        /// <summary>Nothing was recorded about why this happened. Not the same as "nothing caused it".</summary>
        public bool IsUnknown => Event.Provenance.IsUnknown;

        private static IReadOnlyList<ResolvedCause> InRole(IReadOnlyList<ResolvedCause> causes, CausalRole role)
        {
            List<ResolvedCause> found = null;
            for (int i = 0; i < causes.Count; i++)
            {
                if (causes[i].Role != role)
                {
                    continue;
                }

                found ??= new List<ResolvedCause>(2);
                found.Add(causes[i]);
            }

            return found == null ? (IReadOnlyList<ResolvedCause>)None : found;
        }
    }

    /// <summary>
    /// Reads recorded causal provenance back out of the ledger.
    ///
    /// <b>Read-only, and that is a contract rather than an accident.</b> Nothing here mints an id,
    /// records an event, teaches a belief or advances a stream, so considering a causal question
    /// and then deciding against it costs the world nothing. An inspector that consumed an event
    /// identity every time somebody opened it would make opening it a change to history.
    ///
    /// Nothing here infers. An event with no recorded provenance reads as unknown; it is never
    /// given the event before it in the list as a cause.
    /// </summary>
    public static class CausalHistory
    {
        private static readonly WorldEvent[] NoEvents = new WorldEvent[0];

        /// <summary>Resolves one event's recorded causes against the world as it stands now.</summary>
        public static CausalReading Read(NarrativeWorldState world, WorldEvent worldEvent)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (worldEvent == null) throw new ArgumentNullException(nameof(worldEvent));

            IReadOnlyList<CausalLink> links = worldEvent.Provenance.Links;
            if (links.Count == 0)
            {
                return new CausalReading(worldEvent, null);
            }

            List<ResolvedCause> causes = new List<ResolvedCause>(links.Count);
            for (int i = 0; i < links.Count; i++)
            {
                EntityId reference = links[i].Reference;
                causes.Add(new ResolvedCause(
                    links[i].Role,
                    reference,
                    FindEvent(world, reference),
                    world.Knowledge.GetFact(reference),
                    world.GetThread(reference)));
            }

            return new CausalReading(worldEvent, causes);
        }

        /// <summary>Resolves one event's recorded causes, by id. Null when the ledger has no such event.</summary>
        public static CausalReading Read(NarrativeWorldState world, EntityId eventId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            WorldEvent worldEvent = FindEvent(world, eventId);
            return worldEvent == null ? null : Read(world, worldEvent);
        }

        /// <summary>
        /// Every later event that names this one in the given role. The inverse of a link, computed
        /// on demand: there is no index to keep current and none to migrate.
        /// </summary>
        public static IReadOnlyList<WorldEvent> Naming(NarrativeWorldState world, EntityId eventId, CausalRole role)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (eventId.IsNone)
            {
                return NoEvents;
            }

            List<WorldEvent> found = null;
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                IReadOnlyList<CausalLink> links = events[i].Provenance.Links;
                for (int link = 0; link < links.Count; link++)
                {
                    if (links[link].Role != role || links[link].Reference != eventId)
                    {
                        continue;
                    }

                    found ??= new List<WorldEvent>(2);
                    found.Add(events[i]);
                    break;
                }
            }

            return found == null ? (IReadOnlyList<WorldEvent>)NoEvents : found;
        }

        /// <summary>The event with this id, or null. The ledger owns identity as well as order.</summary>
        public static WorldEvent FindEvent(NarrativeWorldState world, EntityId eventId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return world.Ledger.Find(eventId);
        }
    }
}
