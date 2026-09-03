using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Continuity
{
    /// <summary>
    /// BQ-082. Whether recalling a hook here is more than an ordinary callback (CD §25).
    ///
    /// <b>It is a filter over BQ-081's own material, never a second source.</b> This never touches
    /// <c>EventLedger</c>: it takes the hooks <see cref="CallbackHooks"/> already derived and gated
    /// for one recaller and asks two questions of each - is this the kind of thing that reads as a
    /// story rather than routine business, and did it not already happen here. Everything a hook
    /// carries about who may know it and how is still <see cref="CallbackHooks"/>'s, so a person
    /// with no route to an event gets nothing from this layer either; there is no second gate to
    /// forget.
    ///
    /// <b>Memorable is a kind, not a magnitude.</b> The design's own vocabulary already names which
    /// kinds cost standing when repeated: <see cref="CallbackKind.Scandal"/> and
    /// <see cref="CallbackKind.Embarrassment"/> are the two that keep being worth bringing up,
    /// where a settled <see cref="CallbackKind.Promise"/> or a remembered <see cref="CallbackKind.Kindness"/>
    /// does not - which is also why not every callback in <see cref="CallbackHooks.For"/> qualifies.
    /// Weirdness itself is deliberately not read here: BQ-081 already established that no event
    /// carries a recorded weirdness premise, only a scene's own <c>WeirdnessBudget</c> does, and
    /// inventing one on an event to satisfy this step is exactly the fabricated-continuity BQ-082
    /// must not do.
    ///
    /// <b>"A second, unrelated context" is read off the same recorded fields <see cref="CallbackHook"/>
    /// already carries.</b> A hook still names the thread and place history recorded it under; this
    /// is not that occasion when both differ from the ones offered. No new field, no bespoke event
    /// type, and nothing that only this one incident could satisfy - any hook, from any recorder,
    /// resurfaces the same way once it is old enough, memorable enough and out of its own context.
    ///
    /// <b>It stops before wording.</b> Which hook, if any, is worth the recall is all this answers.
    /// Saying it is still <c>DialogueRealizer</c>'s, through the <c>RealizationRequest.Callback</c>
    /// seam BQ-081 built; conversation-level memory of having already used it there is BQ-083's.
    /// </summary>
    public static class CallbackRecurrence
    {
        private static readonly CallbackKind[] MemorableKinds = { CallbackKind.Scandal, CallbackKind.Embarrassment };

        /// <summary>
        /// Whether this hook is the kind of history that gains by recurring: a scandal or an
        /// embarrassment, the two kinds a small town keeps telling on somebody. Everything else -
        /// a promise, a kindness, an injury, a lost object - is reusable material without being a
        /// story, so it stays an ordinary callback rather than becoming continuity humour.
        /// </summary>
        public static bool IsMemorable(CallbackHook hook)
        {
            if (hook == null)
            {
                return false;
            }

            IReadOnlyList<CallbackKind> kinds = hook.Kinds;
            for (int i = 0; i < kinds.Count; i++)
            {
                for (int j = 0; j < MemorableKinds.Length; j++)
                {
                    if (kinds[i] == MemorableKinds[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Whether <paramref name="context"/> is not where <paramref name="hook"/> happened. Both
        /// the thread and the place have to differ from what the hook itself recorded, when either
        /// is known; a hook with no thread or an event with no thread of its own is not evidence
        /// either way, so it never blocks a match on that dimension alone.
        /// </summary>
        public static bool IsUnrelatedContext(CallbackHook hook, ContinuityContext context)
        {
            if (hook == null)
            {
                return false;
            }

            bool sameThread = !hook.ThreadId.IsNone && !context.ThreadId.IsNone && hook.ThreadId == context.ThreadId;
            bool sameSite = !hook.Place.IsNone && !context.SiteId.IsNone && hook.Place == context.SiteId;
            return !sameThread && !sameSite;
        }

        /// <summary>The whole gate: memorable, and not spoken where it already happened.</summary>
        public static bool IsContinuityHumour(CallbackHook hook, ContinuityContext context)
        {
            return IsMemorable(hook) && IsUnrelatedContext(hook, context);
        }

        /// <summary>
        /// The one hook this recaller would bring up here as continuity humour, most striking
        /// first as <see cref="CallbackHooks.SalienceOf"/> already orders them, or null when
        /// nothing in their available material earns it. A caller that gets null has proved
        /// nothing was fabricated to fill the gap - the honest answer for an ordinary scene, which
        /// is most of them.
        /// </summary>
        public static CallbackHook Best(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId recaller,
            ContinuityContext context,
            GameTime now,
            CallbackSelection selection = null)
        {
            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(world, vanilla, recaller, now, selection);
            for (int i = 0; i < hooks.Count; i++)
            {
                if (IsContinuityHumour(hooks[i], context))
                {
                    return hooks[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The thread and place a hook is being weighed against for recurrence - "here", in the sense
    /// <see cref="CallbackRecurrence.IsUnrelatedContext"/> needs it. Either may be
    /// <see cref="EntityId.None"/> when the occasion has no thread of its own or no fixed site; a
    /// blank half simply cannot rule a hook in or out on that dimension.
    /// </summary>
    public readonly struct ContinuityContext
    {
        public ContinuityContext(EntityId threadId, EntityId siteId)
        {
            ThreadId = threadId;
            SiteId = siteId;
        }

        public EntityId ThreadId { get; }

        public EntityId SiteId { get; }
    }
}
