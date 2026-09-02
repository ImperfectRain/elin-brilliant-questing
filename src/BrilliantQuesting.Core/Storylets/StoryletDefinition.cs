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

    /// <summary>
    /// What a role requires of whoever fills it.
    ///
    /// Two families. The first four are *named*: the scene or the focus fact already says who
    /// this is. The rest are *searched*: they describe a requirement, and
    /// <see cref="StoryletCasting"/> finds somebody here who meets it - which is what lets one
    /// definition play in two towns with nobody named in it.
    /// </summary>
    public enum StoryletRoleSource
    {
        /// <summary>Whoever the caller is staging the scene around. Usually who the player is with.</summary>
        Actor,

        /// <summary>The other person the caller already named.</summary>
        Target,

        /// <summary>The person the focus fact is about - the thief of "X stole Y".</summary>
        FactSubject,

        /// <summary>
        /// Whatever sits in the fact's object slot, bound only when that is a person the registry
        /// knows. For "X stole Y" the object is the ring, and a ring is nobody's accuser.
        /// </summary>
        FactObject,

        /// <summary>
        /// The legacy spelling of <see cref="AnyoneWhoKnowsFocus"/>, kept so bundles and saves
        /// written before casting existed keep loading. It searches like the new one.
        /// </summary>
        AnyParticipantWhoKnowsFocus,

        /// <summary>The person the world records as holding what the focus fact is about.</summary>
        OwnerOfFocusObject,

        /// <summary>Somebody here who knows the focus fact - a witness, an accuser, a gossip.</summary>
        AnyoneWhoKnowsFocus,

        /// <summary>Somebody here who can actually prove it, not merely believe it.</summary>
        AnyoneWhoCanProveFocus,

        /// <summary>Somebody here who holds standing of any kind - a guard, guild personnel, a mediator.</summary>
        AnyoneWithStandingHere,

        /// <summary>
        /// Somebody of the player's own household who is here: a resident of their Home, or one of
        /// the companions and pets that travel with them (BQ-123).
        ///
        /// The one searched source that asks for a *subject* rather than a speaker, so it is the
        /// one that does not require social agency. A role written against it is a role the scene
        /// is about - who was hurt, whose loss is at issue, what somebody else wants or bears a
        /// grudge against - and a chicken can be all four. A household member who is to say
        /// something asks for the thing that says it: <see cref="AnyoneWhoKnowsFocus"/> finds a
        /// witness, and being of the household is what puts them first in the order it searches,
        /// not what qualifies them.
        /// </summary>
        HouseholdMemberHere
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
