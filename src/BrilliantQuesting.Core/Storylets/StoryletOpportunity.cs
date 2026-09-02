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
            string refusalReason)
        {
            Definition = definition;
            FocusFactId = focusFactId;
            IsAvailable = available;
            RoleBindings = roleBindings ?? EmptyBindings;
            CastingNotes = castingNotes ?? EmptyNotes;
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

        public string RefusalReason { get; }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            Dictionary<string, EntityId> roleBindings)
        {
            return new StoryletOpportunity(definition, focusFactId, true, roleBindings, null, null);
        }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            Dictionary<string, EntityId> roleBindings,
            List<string> castingNotes)
        {
            return new StoryletOpportunity(definition, focusFactId, true, roleBindings, castingNotes, null);
        }

        public static StoryletOpportunity Refused(
            StoryletDefinition definition,
            EntityId focusFactId,
            string reason)
        {
            return new StoryletOpportunity(definition, focusFactId, false, null, null, reason);
        }
    }
}
