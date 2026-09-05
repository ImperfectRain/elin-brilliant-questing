using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>One finished matter, as the player is entitled to remember it.</summary>
    public sealed class ChronicleEntry
    {
        public ChronicleEntry(
            EntityId threadId,
            string archetypeId,
            string outcome,
            GameTime resolvedAt,
            IReadOnlyList<JournalEntry> whatWasKnown,
            IReadOnlyList<ChronicleAct> whatThePlayerDid)
        {
            ThreadId = threadId;
            ArchetypeId = archetypeId;
            Outcome = outcome;
            ResolvedAt = resolvedAt;
            WhatWasKnown = whatWasKnown;
            WhatThePlayerDid = whatThePlayerDid;
        }

        public EntityId ThreadId { get; }

        public string ArchetypeId { get; }

        /// <summary>The outcome name recorded when it ended - "debt_paid", "sheltered".</summary>
        public string Outcome { get; }

        public GameTime ResolvedAt { get; }

        /// <summary>
        /// What the player believed about this matter, carrying the journal's own tags. Beliefs,
        /// not truth: a case closed on a mistaken conviction reads back as the mistake.
        /// </summary>
        public IReadOnlyList<JournalEntry> WhatWasKnown { get; }

        public IReadOnlyList<ChronicleAct> WhatThePlayerDid { get; }
    }

    /// <summary>Something the player did inside a matter, as history recorded it.</summary>
    public sealed class ChronicleAct
    {
        public ChronicleAct(WorldEventType type, EntityId towards, GameTime at)
        {
            Type = type;
            Towards = towards;
            At = at;
        }

        public WorldEventType Type { get; }

        /// <summary>Who it was done to, or none.</summary>
        public EntityId Towards { get; }

        public GameTime At { get; }
    }

    /// <summary>
    /// The record of what is finished, as distinct from the journal's record of what is open.
    ///
    /// Derived, not stored. Every part of an entry is already in the save - the resolution event
    /// in the ledger, the thread it names, the player's own acts inside it, the beliefs they hold
    /// about it - so the Chronicle is a reading of history rather than a second copy of it, and
    /// it survives a reload for the same reason the ledger does. Nothing here can be edited into
    /// saying something that did not happen.
    ///
    /// It is a player surface, so it obeys the same rule the journal does (`LW §3.3`, `D008`): it
    /// shows what the player could know. A matter appears once the player has ended it themselves;
    /// a situation resolved elsewhere in the world stays out until something in the world tells
    /// them of it. Inside an entry, who did what comes from the player's beliefs, tagged, so an
    /// unsolved theft closed on a wrong name reads as the suspicion it was rather than as fact.
    ///
    /// Presentation is deliberately plain - one line per act, the event's own name. Arranging
    /// this into something a player would retell is <see cref="ChronicleNarrative"/> (BQ-117),
    /// which reads the same history rather than rewording it: a phrasebook in either place would
    /// be a second, half-written narrator to keep in step with the first.
    /// </summary>
    public static class Chronicle
    {
        public static IReadOnlyList<ChronicleEntry> Entries(NarrativeWorldState world, EntityId player)
        {
            List<ChronicleEntry> entries = new List<ChronicleEntry>();
            if (world == null || player.IsNone)
            {
                return entries;
            }

            Dictionary<EntityId, JournalEntry> beliefs = new Dictionary<EntityId, JournalEntry>();
            IReadOnlyList<JournalEntry> journal = NarrativeJournal.Entries(world, player);
            for (int i = 0; i < journal.Count; i++)
            {
                beliefs[journal[i].FactId] = journal[i];
            }

            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent resolution = events[i];
                if (resolution.Type != WorldEventType.ThreadResolved || resolution.Actor != player)
                {
                    continue;
                }

                NarrativeThread thread = world.GetThread(resolution.ThreadId);
                if (thread == null)
                {
                    continue;
                }

                string outcome = ThreadResolution.OutcomeOf(resolution);
                entries.Add(new ChronicleEntry(
                    thread.Id,
                    thread.ArchetypeId,
                    string.IsNullOrEmpty(outcome) ? thread.Resolution ?? string.Empty : outcome,
                    resolution.Time,
                    KnownFacts(thread, beliefs),
                    ActsBy(world, player, thread, resolution.Time)));
            }

            return entries;
        }

        public static string Describe(NarrativeWorldState world, EntityId player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Brilliant Questing chronicle\n");

            IReadOnlyList<ChronicleEntry> entries = Entries(world, player);
            if (entries.Count == 0)
            {
                sb.Append("  nothing finished yet\n");
                return sb.ToString();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ChronicleEntry entry = entries[i];
                sb.Append("  ").Append(entry.ArchetypeId).Append(" - ").Append(Words(entry.Outcome));
                sb.Append(" (day ").Append(entry.ResolvedAt.TotalDays).Append(")\n");

                for (int k = 0; k < entry.WhatWasKnown.Count; k++)
                {
                    JournalEntry known = entry.WhatWasKnown[k];
                    sb.Append("    what you knew: [").Append(known.Tag).Append("] ").Append(known.Text).Append('\n');
                }

                for (int a = 0; a < entry.WhatThePlayerDid.Count; a++)
                {
                    ChronicleAct act = entry.WhatThePlayerDid[a];
                    sb.Append("    what you did: day ").Append(act.At.TotalDays).Append(", ").Append(Words(act.Type.ToString()));
                    if (!act.Towards.IsNone)
                    {
                        sb.Append(" - ").Append(world.Registry.NameOf(act.Towards));
                    }

                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        private static IReadOnlyList<JournalEntry> KnownFacts(NarrativeThread thread, Dictionary<EntityId, JournalEntry> beliefs)
        {
            List<JournalEntry> known = new List<JournalEntry>();
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (beliefs.TryGetValue(thread.FactIds[i], out JournalEntry entry))
                {
                    known.Add(entry);
                }
            }

            return known;
        }

        /// <summary>
        /// What the player themselves did inside this matter, up to the moment it ended.
        ///
        /// Their own acts, so no knowledge test is needed - a person knows what they did. The
        /// resolution entry itself is left out: it is the heading, not one more deed.
        ///
        /// An act belongs to the matter when history already says so: the event names the thread,
        /// or it names one of the facts the thread rests on. Both links are recorded at the time
        /// by the verb itself, so nothing here has to guess from who was standing where. A verb
        /// that records neither - and several still do not carry a thread id - leaves its act out
        /// rather than being inferred back in; widening that attribution means changing what those
        /// verbs record, which also changes thread tension, and is not this step's business.
        /// </summary>
        private static IReadOnlyList<ChronicleAct> ActsBy(NarrativeWorldState world, EntityId player, NarrativeThread thread, GameTime resolvedAt)
        {
            List<ChronicleAct> acts = new List<ChronicleAct>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.Actor != player
                    || worldEvent.Type == WorldEventType.ThreadResolved
                    || worldEvent.Time > resolvedAt)
                {
                    continue;
                }

                if (worldEvent.ThreadId != thread.Id && !NamesAFactOf(worldEvent, thread))
                {
                    continue;
                }

                acts.Add(new ChronicleAct(worldEvent.Type, worldEvent.Target, worldEvent.Time));
            }

            return acts;
        }

        private static bool NamesAFactOf(WorldEvent worldEvent, NarrativeThread thread)
        {
            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                if (thread.FactIds.Contains(worldEvent.Related[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// "debt_paid" and "ItemReturned" both become readable without a phrasebook.
        ///
        /// Public because every surface that reads this history has to spell an outcome the same
        /// way, and a copy per surface is how two readings of one ledger start disagreeing about
        /// what a verb is called. It formats an identifier and knows nothing else - the wording
        /// with variants and voice is still the content pipeline's.
        /// </summary>
        public static string Words(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "resolved";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }

                if (char.IsUpper(c) && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }
}
