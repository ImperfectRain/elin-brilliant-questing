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
            Resolutions = new List<StoryletResolution>();
        }

        public string Id { get; }

        public List<string> SituationTags { get; }

        public List<string> ToneTags { get; }

        public List<StoryletPrecondition> Preconditions { get; }

        public List<StoryletRole> RequiredRoles { get; }

        public List<StoryletRole> OptionalRoles { get; }

        public List<StoryletBeat> Beats { get; }

        public List<StoryletConsequenceHook> ConsequenceHooks { get; }

        /// <summary>
        /// The states this scene can stop in (BQ-146). Empty for the id-only storylets that
        /// predate routing, which stop when their beats run out.
        /// </summary>
        public List<StoryletResolution> Resolutions { get; }

        /// <summary>
        /// Whether the beats carry enough structure to be played rather than merely listed. False
        /// for a storylet written before BQ-146, which is still a valid storylet - it simply has
        /// no route through itself, so a caller drives it.
        /// </summary>
        public bool IsRouted
        {
            get
            {
                for (int i = 0; i < Beats.Count; i++)
                {
                    if (Beats[i].Routes.Count > 0 || Beats[i].Intentions.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The beat with this id, or null. Routes are resolved through here.</summary>
        public StoryletBeat Beat(string id)
        {
            for (int i = 0; i < Beats.Count; i++)
            {
                if (string.Equals(Beats[i].Id, id, StringComparison.Ordinal))
                {
                    return Beats[i];
                }
            }

            return null;
        }
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

    /// <summary>
    /// One moment in a scene: who might speak, what they might be trying to communicate, what is
    /// in doubt, what history should record, and where the scene goes next (BQ-146).
    ///
    /// Before this a beat was an id and nothing else, which meant a storylet was an unordered bag
    /// of labels and every scene had to be driven from outside by a caller who already knew what
    /// the labels meant. That is the shape that forces either hardcoded dialogue in content or
    /// bespoke C# per storylet, and this type exists to make both unnecessary.
    ///
    /// <b>Everything on it is a reference, never a sentence.</b> The speaker is a role, the
    /// listener is a role, the intentions are <see cref="SpeechActType"/>s over the scene's own
    /// focus, the check is one of the profiles the action library already ships, the consequences
    /// are <c>WorldEventType</c>s, and the routes name other beats. There is no field a line of
    /// dialogue could be written into, which is the structural version of the rule rather than a
    /// convention an author has to keep.
    ///
    /// <b>An id-only beat is still a beat.</b> Every field below is optional, so the five
    /// storylets that shipped as bare labels keep loading and keep meaning exactly what they
    /// meant. A beat with no routes is terminal; a beat with no intentions is something that
    /// happens rather than something said.
    /// </summary>
    public sealed class StoryletBeat
    {
        public StoryletBeat(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Beat id is required.", nameof(id));
            }

            Id = id;
            Intentions = new List<BeatIntention>();
            Requires = new List<StoryletPrecondition>();
            Consequences = new List<BeatConsequence>();
            Routes = new List<BeatRoute>();
            PlayerIntersections = new List<string>();
        }

        public string Id { get; }

        /// <summary>Which role speaks, or empty for a beat nobody speaks in.</summary>
        public string SpeakerRole { get; set; } = string.Empty;

        /// <summary>Which role is spoken to. Required whenever there is a speaker.</summary>
        public string ListenerRole { get; set; } = string.Empty;

        /// <summary>
        /// Everything the speaker might be trying to communicate here. One is chosen from their
        /// own state; the list is what the situation makes sensible, never what happens.
        /// </summary>
        public List<BeatIntention> Intentions { get; }

        /// <summary>
        /// What must hold for this beat to be reachable at all, in the same vocabulary a storylet's
        /// own preconditions speak. A beat whose requirements have lapsed is skipped, which is how
        /// a scene degrades when the world moves under it instead of playing on regardless.
        /// </summary>
        public List<StoryletPrecondition> Requires { get; }

        /// <summary>The uncertainty this beat settles, or null when nothing here is in doubt.</summary>
        public BeatCheck Check { get; set; }

        /// <summary>
        /// The verbs the player may bring to bear here, as <c>ActionRegistry</c> ids. Declared so a
        /// presentation layer knows where a scene is open to interference, and validated against
        /// the registry so a storylet cannot advertise a mechanic that does not exist.
        /// </summary>
        public List<string> PlayerIntersections { get; }

        public List<BeatConsequence> Consequences { get; }

        /// <summary>
        /// Where the scene goes, in authored order, first match winning. Empty means the beat is
        /// an end in itself.
        /// </summary>
        public List<BeatRoute> Routes { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// A state a scene can stop in.
    ///
    /// Declared rather than inferred so that "several plausible terminal conditions exist" is
    /// checkable: content validation refuses a storylet whose beats can reach no resolution, and
    /// refuses a route that ends in one nobody declared. A resolution is a name and nothing else -
    /// what it costs and what it changes are the consequence hooks' business, on the beats that
    /// reach it.
    /// </summary>
    public sealed class StoryletResolution
    {
        public StoryletResolution(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Resolution id is required.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }

        public override string ToString() => Id;
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
