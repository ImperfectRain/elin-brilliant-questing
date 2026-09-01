using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Storylets
{
    public sealed class StoryletDefinition
    {
        public StoryletDefinition(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Storylet id is required.", nameof(id));
            }

            Id = id;
            SituationTags = new List<string>();
            ToneTags = new List<string>();
            Preconditions = new List<StoryletPrecondition>();
            RequiredRoles = new List<StoryletRole>();
            OptionalRoles = new List<StoryletRole>();
            Beats = new List<StoryletBeat>();
            ConsequenceHooks = new List<StoryletConsequenceHook>();
        }

        public string Id { get; }

        public List<string> SituationTags { get; }

        public List<string> ToneTags { get; }

        public List<StoryletPrecondition> Preconditions { get; }

        public List<StoryletRole> RequiredRoles { get; }

        public List<StoryletRole> OptionalRoles { get; }

        public List<StoryletBeat> Beats { get; }

        public List<StoryletConsequenceHook> ConsequenceHooks { get; }
    }

    public sealed class StoryletRole
    {
        public StoryletRole(string id, StoryletRoleSource source)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Role id is required.", nameof(id));
            }

            Id = id;
            Source = source;
        }

        public string Id { get; }

        public StoryletRoleSource Source { get; }
    }

    public enum StoryletRoleSource
    {
        Actor,
        Target,
        FactSubject,
        FactObject,
        AnyParticipantWhoKnowsFocus
    }

    public sealed class StoryletBeat
    {
        public StoryletBeat(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Beat id is required.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }
    }

    public sealed class StoryletConsequenceHook
    {
        public StoryletConsequenceHook(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Consequence hook id is required.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }
    }

    public sealed class StoryletPrecondition
    {
        private StoryletPrecondition(StoryletPreconditionKind kind, string value, EntityId entity)
        {
            Kind = kind;
            Value = value;
            Entity = entity;
        }

        public StoryletPreconditionKind Kind { get; }

        public string Value { get; }

        public EntityId Entity { get; }

        public static StoryletPrecondition FocusPredicate(string predicate)
        {
            return new StoryletPrecondition(StoryletPreconditionKind.FocusPredicate, predicate, EntityId.None);
        }

        public static StoryletPrecondition FocusTruth(TruthState truth)
        {
            return new StoryletPrecondition(StoryletPreconditionKind.FocusTruth, truth.ToString(), EntityId.None);
        }

        public static StoryletPrecondition RoleKnowsFocus(string roleId)
        {
            return new StoryletPrecondition(StoryletPreconditionKind.RoleKnowsFocus, roleId, EntityId.None);
        }

        public static StoryletPrecondition RoleCanProveFocus(string roleId)
        {
            return new StoryletPrecondition(StoryletPreconditionKind.RoleCanProveFocus, roleId, EntityId.None);
        }

        public static StoryletPrecondition RoleAlive(string roleId)
        {
            return new StoryletPrecondition(StoryletPreconditionKind.RoleAlive, roleId, EntityId.None);
        }

        public static StoryletPrecondition FactBelongsToThread()
        {
            return new StoryletPrecondition(StoryletPreconditionKind.FactBelongsToThread, null, EntityId.None);
        }
    }

    public enum StoryletPreconditionKind
    {
        FactBelongsToThread,
        FocusPredicate,
        FocusTruth,
        RoleKnowsFocus,
        RoleCanProveFocus,
        RoleAlive
    }
}
