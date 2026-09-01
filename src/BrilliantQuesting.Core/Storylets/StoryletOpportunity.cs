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
            string refusalReason)
        {
            Definition = definition;
            FocusFactId = focusFactId;
            IsAvailable = available;
            RoleBindings = roleBindings ?? EmptyBindings;
            RefusalReason = refusalReason ?? string.Empty;
        }

        private static readonly Dictionary<string, EntityId> EmptyBindings = new Dictionary<string, EntityId>();

        public StoryletDefinition Definition { get; }

        public EntityId FocusFactId { get; }

        public bool IsAvailable { get; }

        public IReadOnlyDictionary<string, EntityId> RoleBindings { get; }

        public string RefusalReason { get; }

        public static StoryletOpportunity Available(
            StoryletDefinition definition,
            EntityId focusFactId,
            Dictionary<string, EntityId> roleBindings)
        {
            return new StoryletOpportunity(definition, focusFactId, true, roleBindings, null);
        }

        public static StoryletOpportunity Refused(
            StoryletDefinition definition,
            EntityId focusFactId,
            string reason)
        {
            return new StoryletOpportunity(definition, focusFactId, false, null, reason);
        }
    }
}
