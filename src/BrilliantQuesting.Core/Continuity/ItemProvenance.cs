using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Continuity
{
    /// <summary>
    /// What one recorded event did with an object (PM §21).
    ///
    /// The design's list is longer than this one, and the difference is the whole admission rule:
    /// a role exists here only where something in the simulation actually records it. "Found on
    /// the corpse of", "inherited by" and "recovered at" have no recorder, so they are absent
    /// rather than guessed at from a death, a will nobody wrote down or the zone a search happened
    /// in - the same rule <see cref="CallbackKind"/> keeps for nicknames. "Owned by" is absent for
    /// a different reason: who is holding a thing now is Elin's own inventory, read live, and a
    /// history that answered it would be a second, staler claim about the same question.
    ///
    /// The vocabulary grows when a recorder for one of them exists.
    /// </summary>
    public enum ProvenanceRole
    {
        /// <summary>Somebody made it, as the game's own production recorded it.</summary>
        Made,

        /// <summary>It went from one pair of hands to another willingly - a gift, a payment, a sale.</summary>
        Given,

        /// <summary>It was handed back to the person it belonged to.</summary>
        Returned,

        /// <summary>It was taken.</summary>
        Stolen,

        /// <summary>It was put beyond reach - burnt, melted, fed to a fire.</summary>
        Destroyed,

        /// <summary>A household took it into its keeping.</summary>
        Kept,

        /// <summary>
        /// It was part of what happened without changing hands: shown, argued over, examined,
        /// pointed at during an accusation. The design's "used in" and "evidence in", which the
        /// ledger does not tell apart and this does not pretend to.
        /// </summary>
        Cited
    }

    /// <summary>
    /// One object's part in one recorded event, as history has it.
    ///
    /// <b>A reference, never a copy.</b> Everything here is either an id the save already holds or
    /// a reading of that event's own recorded fields, on exactly the terms <c>D039</c> sets for a
    /// callback: there is no provenance store, no index and no save entry, so an object's history
    /// survives a reload because the ledger does, and a corrected event corrects every entry that
    /// reads it. Nothing here is prose.
    ///
    /// <b>Notability is not a flag.</b> `PM §21`'s "track only notable objects" needs no notable
    /// bit and no per-object budget, because nothing is tracked: an object has provenance exactly
    /// when history recorded something about it, and the berry nobody wrote anything down about
    /// derives an empty list at no cost.
    /// </summary>
    public sealed class ProvenanceEntry
    {
        internal ProvenanceEntry(
            EntityId itemId,
            WorldEvent worldEvent,
            ProvenanceRole role,
            long ageInDays,
            EntityId recognizer,
            CallbackRoute? recognizedVia)
        {
            ItemId = itemId;
            EventId = worldEvent.Id;
            EventType = worldEvent.Type;
            Role = role;
            Actor = worldEvent.Actor;
            Other = worldEvent.Target;
            Place = worldEvent.Zone;
            ThreadId = worldEvent.ThreadId;
            At = worldEvent.Time;
            AgeInDays = ageInDays;
            Recognizer = recognizer;
            RecognizedVia = recognizedVia;
        }

        /// <summary>The object this is about.</summary>
        public EntityId ItemId { get; }

        /// <summary>The event in the ledger. The only source of what happened.</summary>
        public EntityId EventId { get; }

        public WorldEventType EventType { get; }

        public ProvenanceRole Role { get; }

        /// <summary>Whoever did it, as history recorded them.</summary>
        public EntityId Actor { get; }

        /// <summary>Whoever it was done to, or nobody when the event names no second party.</summary>
        public EntityId Other { get; }

        public EntityId Place { get; }

        /// <summary>The matter the verb that wrote it recorded, when it recorded one.</summary>
        public EntityId ThreadId { get; }

        public GameTime At { get; }

        /// <summary>Whole in-game days between the event and the moment this was derived at.</summary>
        public long AgeInDays { get; }

        /// <summary>
        /// Whose reading of the object's history this is, or <see cref="EntityId.None"/> for the
        /// world's own - see <see cref="ItemProvenance.Of"/>.
        /// </summary>
        public EntityId Recognizer { get; }

        /// <summary>
        /// How <see cref="Recognizer"/> comes to know this happened, and null when nobody is
        /// asking. It is <see cref="CallbackRoute"/> rather than a second enum on purpose:
        /// recognizing the ring in somebody's hand and being entitled to bring the theft up are
        /// the same knowledge question, and answering it twice is how the two answers start to
        /// disagree.
        /// </summary>
        public CallbackRoute? RecognizedVia { get; }

        public override string ToString() =>
            Role + " " + ItemId + " @" + EventId + (RecognizedVia.HasValue ? " (" + Recognizer + " " + RecognizedVia.Value + ")" : string.Empty);
    }

    /// <summary>
    /// What an object has been through, and who can tell (BQ-085).
    ///
    /// Derived from the event ledger on demand, never stored. That is not an optimisation: an
    /// object's history is already in the save because the events are, and a second copy on the
    /// object could outlive the events, disagree with them and need migrating - the reasoning of
    /// <c>D005</c> and <c>D039</c>, applied to things instead of to people.
    ///
    /// <b>The object is read out of one field.</b> Only <see cref="WorldEvent.Evidence"/> means
    /// "this object was part of what happened". <see cref="WorldEvent.Related"/> is a general list
    /// of ids whose meaning changes from verb to verb - a claim here, a forged document there - so
    /// reading it as provenance would mean inventing the relationship it then reported.
    ///
    /// <b>Recognition is a knowledge gate, not a roll.</b> Whether somebody knows an object when
    /// they see it is whether they have a route to the history it carries, and the route is
    /// <see cref="CallbackHooks.TryRoute"/>'s - the same gate that decides whether they may bring
    /// that history up at all. So nothing here can hand somebody a past they were never part of,
    /// and showing a ring to a stranger tells them nothing.
    /// </summary>
    public static class ItemProvenance
    {
        private static readonly ProvenanceEntry[] NoEntries = new ProvenanceEntry[0];
        private static readonly NarrativeThread[] NoThreads = new NarrativeThread[0];

        /// <summary>
        /// Everything history recorded about this object, oldest first, with nobody's knowledge
        /// consulted. The world's own answer: for what a particular person can tell from it, ask
        /// <see cref="RecognizedBy"/>.
        /// </summary>
        public static IReadOnlyList<ProvenanceEntry> Of(NarrativeWorldState world, EntityId itemId, GameTime now)
        {
            return Derive(world, itemId, EntityId.None, now);
        }

        /// <summary>
        /// The part of that history this person could place the object by, oldest first.
        ///
        /// Empty for somebody with no route to any of it, which is the whole of "showing it to the
        /// wrong person achieves nothing" - expressed as which entries exist rather than as a rule
        /// consumers have to remember.
        /// </summary>
        public static IReadOnlyList<ProvenanceEntry> RecognizedBy(
            NarrativeWorldState world,
            EntityId itemId,
            EntityId viewer,
            GameTime now)
        {
            return viewer.IsNone ? NoEntries : Derive(world, itemId, viewer, now);
        }

        /// <summary>
        /// The matters still open that these entries belong to.
        ///
        /// A thread is named by an entry only where something recorded says so: the event names
        /// the thread, the thread names the event as its origin, or a claim the thread rests on
        /// was begun by it. Nothing is matched on time, place or subject - a coincidence is not a
        /// connection, and a ring that turns up in the same room as an unrelated quarrel must not
        /// wake it.
        ///
        /// Resolved, quarantined and inherited matters are not open. A resolved matter is over,
        /// and producing the object again does not reopen it.
        /// </summary>
        public static IReadOnlyList<NarrativeThread> OpenMatters(
            NarrativeWorldState world,
            IReadOnlyList<ProvenanceEntry> entries)
        {
            if (world == null || entries == null || entries.Count == 0)
            {
                return NoThreads;
            }

            List<NarrativeThread> matters = new List<NarrativeThread>();
            for (int t = 0; t < world.Threads.Count; t++)
            {
                NarrativeThread thread = world.Threads[t];
                if (thread == null || !IsOpen(thread))
                {
                    continue;
                }

                for (int e = 0; e < entries.Count; e++)
                {
                    if (Names(world, thread, entries[e]))
                    {
                        matters.Add(thread);
                        break;
                    }
                }
            }

            return matters.Count == 0 ? NoThreads : matters;
        }

        /// <summary>
        /// Which recorded act an event was, as far as this object is concerned.
        ///
        /// Public because "why does the ledger call that a citation rather than a sale" is a
        /// question the inspector has to be able to answer, and because the mapping is the
        /// interesting half of the derivation.
        /// </summary>
        public static ProvenanceRole RoleOf(WorldEventType type)
        {
            switch (type)
            {
                case WorldEventType.GoodsProduced:
                    return ProvenanceRole.Made;

                // One role for every willing transfer. The ledger records a gift, a payment and a
                // fence's purchase with the same verb, so naming one of them here would be this
                // layer deciding something history did not.
                case WorldEventType.ItemGiven:
                    return ProvenanceRole.Given;

                case WorldEventType.ItemReturned:
                    return ProvenanceRole.Returned;

                case WorldEventType.Theft:
                    return ProvenanceRole.Stolen;

                case WorldEventType.EvidenceDestroyed:
                    return ProvenanceRole.Destroyed;

                case WorldEventType.TakenIn:
                    return ProvenanceRole.Kept;

                default:
                    return ProvenanceRole.Cited;
            }
        }

        private static IReadOnlyList<ProvenanceEntry> Derive(
            NarrativeWorldState world,
            EntityId itemId,
            EntityId viewer,
            GameTime now)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (itemId.IsNone)
            {
                return NoEntries;
            }

            List<ProvenanceEntry> entries = new List<ProvenanceEntry>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (!Contains(worldEvent.Evidence, itemId))
                {
                    continue;
                }

                CallbackRoute? via = null;
                if (!viewer.IsNone)
                {
                    CallbackRoute route;
                    if (!CallbackHooks.TryRoute(world, worldEvent, viewer, out route))
                    {
                        continue;
                    }

                    via = route;
                }

                entries.Add(new ProvenanceEntry(
                    itemId,
                    worldEvent,
                    RoleOf(worldEvent.Type),
                    now.DaysSince(worldEvent.Time),
                    viewer,
                    via));
            }

            return entries.Count == 0 ? NoEntries : entries;
        }

        private static bool IsOpen(NarrativeThread thread)
        {
            return thread.State == ThreadState.Latent
                   || thread.State == ThreadState.Active
                   || thread.State == ThreadState.Dormant;
        }

        private static bool Names(NarrativeWorldState world, NarrativeThread thread, ProvenanceEntry entry)
        {
            if (!entry.ThreadId.IsNone && entry.ThreadId == thread.Id)
            {
                return true;
            }

            if (!thread.OriginEventId.IsNone && thread.OriginEventId == entry.EventId)
            {
                return true;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && !fact.OriginEvent.IsNone && fact.OriginEvent == entry.EventId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<EntityId> ids, EntityId id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
