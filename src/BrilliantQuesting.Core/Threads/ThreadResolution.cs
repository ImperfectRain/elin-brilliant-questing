using System;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// Ending a situation, once, in one place.
    ///
    /// Every verb that could close a thread used to do it by hand - set the state, write the
    /// resolution string, add a note - and none of them told history it had happened. That is
    /// fine while the only reader is the thread itself, and wrong the moment anything has to
    /// answer "what became of that?": a thread carries its *current* state, so a resolution that
    /// lives only there has no time, no author, and nothing to say if the thread is ever reopened
    /// (BQ-052). The Chronicle needs all three, so the ending goes into the ledger like every
    /// other thing that happened, and the thread's fields become the projection of it.
    ///
    /// The event names no target and no facts on purpose. Whatever act ended the matter - the
    /// payment, the returned ring, the delivered grain - was already recorded with its own
    /// witnesses, affinity and evidence; recording the resolution *again* against the same person
    /// would pay them twice for one deed and, through <c>related</c>, quietly teach bystanders the
    /// facts the thread rested on. What this adds is the closing entry, not a second copy of the
    /// deed.
    /// </summary>
    public static class ThreadResolution
    {
        /// <summary>Prefix marking the outcome name on the resolution event's tags.</summary>
        public const string TagPrefix = "resolution:";

        /// <summary>
        /// Closes <paramref name="thread"/> as <paramref name="resolution"/> and records it.
        ///
        /// Returns the event, or null when nothing was closed - no thread, no outcome name, or a
        /// thread that is already resolved. Resolving twice is deliberately a no-op rather than a
        /// second ending: history is appended to, not rewritten, and a save that is loaded and
        /// replayed must not grow a duplicate entry.
        /// </summary>
        public static WorldEvent Resolve(
            NarrativeWorldState world,
            NarrativeThread thread,
            string resolution,
            EntityId resolvedBy,
            GameTime now,
            double magnitude = 0.5,
            EntityId zone = default)
        {
            if (world == null || thread == null || string.IsNullOrEmpty(resolution))
            {
                return null;
            }

            if (thread.State == ThreadState.Resolved)
            {
                return null;
            }

            // Before the event, not after: the consequence layer raises tension on any event
            // carrying a live thread id, so a thread that is still open when its own ending is
            // recorded would be shoved back into Active by the act of closing it.
            thread.State = ThreadState.Resolved;
            thread.Resolution = resolution;
            thread.LastAdvancedAt = now;

            return world.Record(
                WorldEventType.ThreadResolved,
                resolvedBy,
                EntityId.None,
                now,
                magnitude,
                zone,
                tags: new[] { TagPrefix + resolution },
                threadId: thread.Id);
        }

        /// <summary>
        /// The outcome name carried by a resolution event, or empty if it carries none.
        ///
        /// Read from the event rather than from the thread so a reopened-and-re-resolved thread
        /// still reports what each ending was at the time it happened.
        /// </summary>
        public static string OutcomeOf(WorldEvent worldEvent)
        {
            if (worldEvent == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                string tag = worldEvent.Tags[i];
                if (tag != null && tag.StartsWith(TagPrefix, StringComparison.Ordinal))
                {
                    return tag.Substring(TagPrefix.Length);
                }
            }

            return string.Empty;
        }
    }
}
