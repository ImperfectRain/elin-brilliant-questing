using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// One piece of unresolved pressure the world's authoritative state is currently holding.
    ///
    /// The distinction this type exists to keep, and which the storylet system quietly loses if it
    /// is missing (CD §36.5):
    ///
    /// <list type="bullet">
    /// <item>an <c>WorldEvent</c> is something that happened - it is history and never stops being true;</item>
    /// <item>a Development is that history <em>still pressing</em> - it is a reading of the present;</item>
    /// <item>a <c>NarrativeThread</c> is a durable matter with identity, a schedule, open questions and a lifecycle;</item>
    /// <item>a <c>StoryletDefinition</c> is an authored dramatic pattern that can express a pressure;</item>
    /// <item>a scene is one concrete presentation of that pattern to a player.</item>
    /// </list>
    ///
    /// Three consequences follow, and they are the whole point of the layer:
    ///
    /// A Development is <b>derived, never authored</b>. There is no public constructor: the only
    /// way to obtain one is <see cref="DevelopmentDetector.Detect"/>, so nothing can mint a
    /// pressure the world does not actually hold, and nothing can keep one alive after the state
    /// that produced it has changed. That is also why it has no <c>State</c> field and no
    /// lifecycle: a development is not resolved, it simply stops being derived.
    ///
    /// A Development <b>points at authoritative state rather than copying it</b>. It carries ids -
    /// the events it originates in, the fact it is about, the thread that carries it, who is
    /// implicated, where - and none of their contents. Nothing here is a second copy of history,
    /// so nothing here can disagree with history.
    ///
    /// A Development <b>need never become anything</b>. It does not have to reach a scene, a
    /// storylet or a quest, and most will not. That is the boundary that stops the storylet
    /// system from becoming a hidden quest generator: pressure existing is not a promise that the
    /// player will ever be offered it.
    /// </summary>
    public sealed class Development
    {
        /// <summary>
        /// Only <see cref="DevelopmentDetector"/> builds these. Internal on purpose: a Development
        /// that could be constructed could be stored, and a stored Development would be a second
        /// authority racing the state it was derived from.
        /// </summary>
        internal Development(
            string id,
            IReadOnlyList<string> pressureTags,
            EntityId threadId,
            EntityId focusFactId,
            IReadOnlyList<EntityId> originEventIds,
            IReadOnlyList<EntityId> subjectIds,
            IReadOnlyList<EntityId> siteIds,
            int urgency)
        {
            Id = id ?? string.Empty;
            PressureTags = pressureTags ?? EmptyTags;
            ThreadId = threadId;
            FocusFactId = focusFactId;
            OriginEventIds = originEventIds ?? EmptyIds;
            SubjectIds = subjectIds ?? EmptyIds;
            SiteIds = siteIds ?? EmptyIds;
            Urgency = urgency < 0 ? 0 : urgency > 100 ? 100 : urgency;
        }

        private static readonly IReadOnlyList<string> EmptyTags = new string[0];

        private static readonly IReadOnlyList<EntityId> EmptyIds = new EntityId[0];

        /// <summary>
        /// A key for the pressure, not for the occasion of noticing it: the same unresolved matter
        /// in the same world derives the same id every time, including after a reload. Not an
        /// <see cref="EntityId"/> deliberately - minting one would put a derived reading into the
        /// same id space as the persistent things it reads.
        /// </summary>
        public string Id { get; }

        public IReadOnlyList<string> PressureTags { get; }

        /// <summary>
        /// The durable matter that carries this pressure, or none. A development is not obliged to
        /// have a thread and must never create one: threads are how the world decides something is
        /// worth continuing to track, which is a decision above this layer.
        /// </summary>
        public EntityId ThreadId { get; }

        /// <summary>
        /// What the pressure is about, when it is about a fact at all. None for pressures that are
        /// between people rather than about a claim.
        /// </summary>
        public EntityId FocusFactId { get; }

        /// <summary>Where in history this pressure comes from. Ids into the ledger; never copies.</summary>
        public IReadOnlyList<EntityId> OriginEventIds { get; }

        /// <summary>Everybody implicated in the pressure, in stable id order.</summary>
        public IReadOnlyList<EntityId> SubjectIds { get; }

        public IReadOnlyList<EntityId> SiteIds { get; }

        /// <summary>
        /// 0..100. How hard this is pressing right now, so a later composer can choose between
        /// pressures. Derived from the authoritative numbers that already exist - a fact's secrecy,
        /// an obligation's strength - and never accumulated over time by this layer.
        /// </summary>
        public int Urgency { get; }

        /// <summary>
        /// Whether a storylet could be looked for at all, by the storylet engine's own
        /// requirements rather than by any policy here: casting needs a thread to check the scene
        /// against and a focus fact to build roles around. A development missing either is not
        /// hidden or suppressed - it simply is not the kind of pressure this dramatic machinery
        /// takes as input, and the world is no less coherent for holding it.
        /// </summary>
        public bool CanBeExpressedAsStorylet => !ThreadId.IsNone && !FocusFactId.IsNone;

        public bool HasPressure(string tag)
        {
            for (int i = 0; i < PressureTags.Count; i++)
            {
                if (string.Equals(PressureTags[i], tag, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return Id + " [urgency " + Urgency + "]";
        }
    }
}
