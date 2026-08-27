using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// Applies a thread's escalation step when the situation deteriorates.
    ///
    /// Handlers are registered per situation archetype so escalation logic lives beside the
    /// archetype that understands it, while the schedule itself stays plain saveable data.
    /// </summary>
    public interface IThreadEscalationHandler
    {
        void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now);
    }

    /// <summary>
    /// Drives time. Call Advance whenever the game clock moves; every step whose day has arrived
    /// fires, in order, so a player who disappears for a fortnight comes back to a world that
    /// moved without them rather than a paused one.
    /// </summary>
    public sealed class ThreadEngine
    {
        private readonly Dictionary<string, IThreadEscalationHandler> _handlers = new Dictionary<string, IThreadEscalationHandler>();

        public void Register(string archetypeId, IThreadEscalationHandler handler)
        {
            _handlers[archetypeId] = handler;
        }

        /// <summary>Steps applied on the last Advance call. Surfaced by the lab and the tests.</summary>
        public List<string> LastApplied { get; } = new List<string>();

        public int Advance(NarrativeWorldState world, GameTime now)
        {
            LastApplied.Clear();
            int applied = 0;

            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (!thread.IsLive)
                {
                    continue;
                }

                EscalationStep step;
                while ((step = thread.NextStep(now)) != null)
                {
                    thread.CompletedSteps.Add(step.Id);
                    thread.LastAdvancedAt = now;
                    thread.State = ThreadState.Active;

                    if (_handlers.TryGetValue(thread.ArchetypeId, out IThreadEscalationHandler handler))
                    {
                        handler.Apply(world, thread, step, now);
                    }

                    LastApplied.Add(thread.ArchetypeId + "/" + step.Id);
                    applied++;

                    // A handler may resolve the thread outright; stop feeding it further steps.
                    if (!thread.IsLive)
                    {
                        break;
                    }
                }

                // Nothing left to fire and nobody has touched it: let it go quiet rather than
                // sitting in the player's face forever.
                if (thread.IsLive && thread.NextStep(now) == null && thread.CompletedSteps.Count == thread.Escalation.Count && thread.Escalation.Count > 0)
                {
                    thread.State = ThreadState.Dormant;
                }
            }

            return applied;
        }
    }
}
