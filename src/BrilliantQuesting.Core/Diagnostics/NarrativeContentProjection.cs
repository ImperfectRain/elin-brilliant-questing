using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    public enum NarrativeContentClass
    {
        Situation,
        Request,
        Opportunity,
        Event
    }

    public sealed class NarrativeContentEntry
    {
        public NarrativeContentEntry(
            NarrativeContentClass contentClass,
            EntityId threadId,
            string title,
            string detail,
            GameTime at,
            EntityId factId = default,
            EntityId eventId = default)
        {
            ContentClass = contentClass;
            ThreadId = threadId;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            At = at;
            FactId = factId;
            EventId = eventId;
        }

        public NarrativeContentClass ContentClass { get; }

        public EntityId ThreadId { get; }

        public string Title { get; }

        public string Detail { get; }

        public GameTime At { get; }

        public EntityId FactId { get; }

        public EntityId EventId { get; }
    }

    /// <summary>
    /// Player-facing content is a derived reading of threads, facts and history. It is not a
    /// quest table: a single causal chain may project as a situation, a request, an opportunity
    /// and an event depending on which surface is asking.
    /// </summary>
    public static class NarrativeContentProjection
    {
        public static IReadOnlyList<NarrativeContentEntry> Entries(NarrativeWorldState world, EntityId player)
        {
            List<NarrativeContentEntry> entries = new List<NarrativeContentEntry>();
            if (world == null || player.IsNone)
            {
                return entries;
            }

            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (!thread.IsLive)
                {
                    continue;
                }

                bool threadIsKnown = PlayerCanKnowThread(world, player, thread);
                if (threadIsKnown)
                {
                    entries.Add(new NarrativeContentEntry(
                        NarrativeContentClass.Situation,
                        thread.Id,
                        Words(thread.ArchetypeId),
                        "tension " + thread.Tension,
                        thread.CreatedAt));
                }

                AddKnownFactContent(world, player, thread, entries);
                AddKnownEventContent(world, player, thread, entries);
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        public static IReadOnlyList<NarrativeContentEntry> BoardEntries(NarrativeWorldState world, EntityId player)
        {
            List<NarrativeContentEntry> requests = new List<NarrativeContentEntry>();
            IReadOnlyList<NarrativeContentEntry> entries = Entries(world, player);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ContentClass == NarrativeContentClass.Request)
                {
                    requests.Add(entries[i]);
                }
            }

            return requests;
        }

        private static void AddKnownFactContent(
            NarrativeWorldState world,
            EntityId player,
            NarrativeThread thread,
            List<NarrativeContentEntry> entries)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                EntityId factId = thread.FactIds[i];
                if (!world.Knowledge.TryGetBelief(player, factId, out KnowledgeRecord belief))
                {
                    continue;
                }

                Fact fact = world.Knowledge.GetFact(factId);
                if (fact == null || fact.Truth == TruthState.Superseded)
                {
                    continue;
                }

                NarrativeContentClass? contentClass = ClassForFact(fact);
                if (!contentClass.HasValue)
                {
                    continue;
                }

                entries.Add(new NarrativeContentEntry(
                    contentClass.Value,
                    thread.Id,
                    TitleForFact(world, fact, contentClass.Value),
                    FactPhrasing.Claim(world.Registry, fact),
                    belief.LearnedAt,
                    factId: fact.Id));
            }
        }

        private static void AddKnownEventContent(
            NarrativeWorldState world,
            EntityId player,
            NarrativeThread thread,
            List<NarrativeContentEntry> entries)
        {
            HashSet<EntityId> named = new HashSet<EntityId>();
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.ThreadId != thread.Id && !NamesFactOf(worldEvent, thread))
                {
                    continue;
                }

                if (worldEvent.Type == WorldEventType.ThreadResolved || !PlayerCanKnow(world, player, worldEvent))
                {
                    continue;
                }

                if (!named.Add(worldEvent.Id))
                {
                    continue;
                }

                entries.Add(new NarrativeContentEntry(
                    NarrativeContentClass.Event,
                    thread.Id,
                    Words(worldEvent.Type.ToString()),
                    EventDetail(world, worldEvent),
                    worldEvent.Time,
                    eventId: worldEvent.Id));
            }
        }

        private static NarrativeContentClass? ClassForFact(Fact fact)
        {
            switch (fact.Predicate)
            {
                case FactPredicates.Needs:
                case FactPredicates.AtRisk:
                    return NarrativeContentClass.Request;

                case FactPredicates.Damaged:
                case FactPredicates.BlocksAccessTo:
                case FactPredicates.LocatedAt:
                    return NarrativeContentClass.Opportunity;

                default:
                    return null;
            }
        }

        private static string TitleForFact(NarrativeWorldState world, Fact fact, NarrativeContentClass contentClass)
        {
            switch (contentClass)
            {
                case NarrativeContentClass.Request:
                    return "Request from " + world.Registry.NameOf(fact.Subject);

                case NarrativeContentClass.Opportunity:
                    return "Opportunity: " + world.Registry.NameOf(fact.Subject);

                default:
                    return Words(fact.Predicate);
            }
        }

        private static bool PlayerCanKnow(NarrativeWorldState world, EntityId player, WorldEvent worldEvent)
        {
            if (worldEvent.Actor == player || worldEvent.Target == player)
            {
                return true;
            }

            for (int i = 0; i < worldEvent.Witnesses.Count; i++)
            {
                if (worldEvent.Witnesses[i] == player)
                {
                    return true;
                }
            }

            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                if (world.Knowledge.Knows(player, worldEvent.Related[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PlayerCanKnowThread(NarrativeWorldState world, EntityId player, NarrativeThread thread)
        {
            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (world.Knowledge.Knows(player, thread.FactIds[i]))
                {
                    return true;
                }
            }

            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if ((worldEvent.ThreadId == thread.Id || NamesFactOf(worldEvent, thread))
                    && PlayerCanKnow(world, player, worldEvent))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NamesFactOf(WorldEvent worldEvent, NarrativeThread thread)
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

        private static string EventDetail(NarrativeWorldState world, WorldEvent worldEvent)
        {
            string actor = world.Registry.NameOf(worldEvent.Actor);
            string target = world.Registry.NameOf(worldEvent.Target);
            if (worldEvent.Target.IsNone)
            {
                return actor;
            }

            return actor + " -> " + target;
        }

        private static int CompareEntries(NarrativeContentEntry left, NarrativeContentEntry right)
        {
            int byThread = left.ThreadId.CompareTo(right.ThreadId);
            if (byThread != 0)
            {
                return byThread;
            }

            int byClass = left.ContentClass.CompareTo(right.ContentClass);
            if (byClass != 0)
            {
                return byClass;
            }

            int byTime = left.At.TotalMinutes.CompareTo(right.At.TotalMinutes);
            if (byTime != 0)
            {
                return byTime;
            }

            int byFact = left.FactId.CompareTo(right.FactId);
            return byFact != 0 ? byFact : left.EventId.CompareTo(right.EventId);
        }

        private static string Words(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('_', ' ');
        }
    }
}
