using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Storylets
{
    public sealed class StoryletOpportunity
    {
        private StoryletOpportunity(
            StoryletDefinition definition,
            EntityId focusFactId,
            bool available,
            Dictionary<string, EntityId> roleBindings,
            List<string> castingNotes,
            StoryletChemistryScore chemistry,
            int groupsConsidered,
            bool searchTruncated,
            bool candidateBoundReached,
            string refusalReason)
        {
            Definition = definition;
            FocusFactId = focusFactId;
            IsAvailable = available;
            RoleBindings = roleBindings ?? EmptyBindings;
            CastingNotes = castingNotes ?? EmptyNotes;
            Chemistry = chemistry ?? StoryletChemistryScore.Empty;
            GroupsConsidered = groupsConsidered;
            SearchTruncated = searchTruncated;
            CandidateBoundReached = candidateBoundReached;
            RefusalReason = refusalReason ?? string.Empty;
        }

        private static readonly Dictionary<string, EntityId> EmptyBindings = new Dictionary<string, EntityId>();

        private static readonly List<string> EmptyNotes = new List<string>();

        public StoryletDefinition Definition { get; }

        public EntityId FocusFactId { get; }

        public bool IsAvailable { get; }

        public IReadOnlyDictionary<string, EntityId> RoleBindings { get; }

        /// <summary>
        /// One sentence per bound role naming who was cast and what qualified them. Inspector-only,
        /// and not persisted: a firing stores who held a role, and why they did is re-derivable
        /// from the same world state that chose them.
        /// </summary>
        public IReadOnlyList<string> CastingNotes { get; }

        /// <summary>
        /// Why this group of qualified people was preferred to the others (BQ-068). Separate from
        /// <see cref="CastingNotes"/> on purpose: those say why each of these people *could* hold
        /// their role, and this says why these people rather than the others who also could.
        /// </summary>
        public StoryletChemistryScore Chemistry { get; }

        /// <summary>How many complete qualified groups the casting pass scored to arrive here.</summary>
        public int GroupsConsidered { get; }

        /// <summary>
        /// Whether the group search stopped on its bound instead of running out of groups, so
        /// <see cref="GroupsConsidered"/> is a prefix of the qualified groups rather than all of
        /// them (BQ-068).
        /// </summary>
        public bool SearchTruncated { get; }

        /// <summary>
        /// Whether some role's shortlist filled up with people still unexamined in the pool, so
        /// the groups weighed were built from a prefix of who qualified (BQ-068).
        /// </summary>
        public bool CandidateBoundReached { get; }

        public string RefusalReason { get; }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            Dictionary<string, EntityId> roleBindings)
        {
            return new StoryletOpportunity(definition, focusFactId, true, roleBindings, null, null, 0, false, false, null);
        }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            Dictionary<string, EntityId> roleBindings,
            List<string> castingNotes)
        {
            return new StoryletOpportunity(definition, focusFactId, true, roleBindings, castingNotes, null, 0, false, false, null);
        }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            StoryletCastingResult casting)
        {
            return new StoryletOpportunity(
                definition,
                focusFactId,
                true,
                casting.Bindings,
                casting.Notes,
                casting.Chemistry,
                casting.GroupsConsidered,
                casting.SearchTruncated,
                casting.CandidateBoundReached,
                null);
        }

        public static StoryletOpportunity Refused(
            StoryletDefinition definition,
            EntityId focusFactId,
            string reason)
        {
            return new StoryletOpportunity(definition, focusFactId, false, null, null, null, 0, false, false, reason);
        }
    }
}
