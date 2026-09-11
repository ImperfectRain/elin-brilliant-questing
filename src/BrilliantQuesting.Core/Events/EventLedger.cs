using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Events
{
    /// <summary>
    /// Append-only history plus a dispatch point. Subscribers (memory, knowledge, relationships,
    /// threads) react to each appended event; reactions may append further events, so dispatch is
    /// queued rather than recursive to keep a single player action from cascading without bound.
    /// </summary>
    public sealed class EventLedger
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();
        private readonly Dictionary<EntityId, WorldEvent> _byId = new Dictionary<EntityId, WorldEvent>();
        private readonly List<Action<WorldEvent>> _listeners = new List<Action<WorldEvent>>();
        private readonly Queue<WorldEvent> _pending = new Queue<WorldEvent>();
        private bool _dispatching;

        /// <summary>Guard against a reaction loop; exceeding it is a bug, not a gameplay state.</summary>
        public int MaxCascadeDepth { get; set; } = 64;

        public IReadOnlyList<WorldEvent> Events => _events;

        public int Count => _events.Count;

        public void Subscribe(Action<WorldEvent> listener)
        {
            if (listener != null)
            {
                _listeners.Add(listener);
            }
        }

        public void Append(WorldEvent worldEvent)
        {
            if (worldEvent == null)
            {
                throw new ArgumentNullException(nameof(worldEvent));
            }

            _events.Add(worldEvent);
            _byId[worldEvent.Id] = worldEvent;
            _pending.Enqueue(worldEvent);

            if (_dispatching)
            {
                return;
            }

            _dispatching = true;
            try
            {
                int dispatched = 0;
                while (_pending.Count > 0)
                {
                    if (++dispatched > MaxCascadeDepth)
                    {
                        _pending.Clear();
                        throw new InvalidOperationException(
                            "Event cascade exceeded " + MaxCascadeDepth + " reactions; a listener is looping.");
                    }

                    WorldEvent next = _pending.Dequeue();
                    for (int i = 0; i < _listeners.Count; i++)
                    {
                        _listeners[i](next);
                    }
                }
            }
            finally
            {
                _dispatching = false;
            }
        }

        public IEnumerable<WorldEvent> Involving(EntityId entity)
        {
            foreach (WorldEvent e in _events)
            {
                if (e.Actor == entity || e.Target == entity || Contains(e.Related, entity) || Contains(e.Witnesses, entity))
                {
                    yield return e;
                }
            }
        }

        public IEnumerable<WorldEvent> OfType(WorldEventType type)
        {
            foreach (WorldEvent e in _events)
            {
                if (e.Type == type)
                {
                    yield return e;
                }
            }
        }

        public IEnumerable<WorldEvent> Since(GameTime time)
        {
            foreach (WorldEvent e in _events)
            {
                if (e.Time >= time)
                {
                    yield return e;
                }
            }
        }

        internal void RestoreWithoutDispatch(WorldEvent worldEvent)
        {
            _events.Add(worldEvent);
            _byId[worldEvent.Id] = worldEvent;
        }

        /// <summary>
        /// The event with this id, or null.
        ///
        /// History is append-only and grows for the life of a save, so callers that need one
        /// occasion by id must not pay a walk of the whole ledger for it. The index is the
        /// ledger's own - the list still owns the order, and this answers identity only.
        /// </summary>
        public WorldEvent Find(EntityId eventId)
        {
            if (eventId.IsNone)
            {
                return null;
            }

            WorldEvent found;
            _byId.TryGetValue(eventId, out found);
            return found;
        }

        private static bool Contains(IReadOnlyList<EntityId> list, EntityId id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
