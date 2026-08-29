using System;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// How far the mod may reach into one actor, as a ladder from watching them to unmaking them.
    ///
    /// A living world needs real consequences, and indiscriminate mutation is how a mod breaks
    /// somebody's vanilla quest line, empties a shop that respawns forever, or leaves a save with
    /// two copies of a shopkeeper in it. The ladder is the whole safety argument: every reach into
    /// the game is one rung, every actor sits on one rung, and a reach happens only when the
    /// actor's rung is at least as high.
    ///
    /// The names are the design's (LW 5). The order is load-bearing and must not be reshuffled:
    /// each rung's numeric value is the <see cref="MutationKind"/> it permits.
    /// </summary>
    public enum NarrativeMutationPolicy
    {
        /// <summary>Read them. Nothing else.</summary>
        ObserveOnly = 0,

        /// <summary>They can be spoken to. Nothing about them changes.</summary>
        DialogueOnly = 1,

        /// <summary>How they feel about the player may move, and the player's standing with it.</summary>
        SocialMutable = 2,

        /// <summary>Things and money may cross between them and somebody else.</summary>
        InventoryMutable = 3,

        /// <summary>They may be moved somewhere else and stay there.</summary>
        Relocatable = 4,

        /// <summary>They may be taken out of the world locally and represented elsewhere.</summary>
        TemporarilyRemovable = 5,

        /// <summary>Anything, up to and including death.</summary>
        FullyMutable = 6
    }

    /// <summary>
    /// What one reach into the game actually does to an actor.
    ///
    /// Deliberately the same numbers as <see cref="NarrativeMutationPolicy"/>: a kind is permitted
    /// exactly when the actor's policy stands on that rung or higher, so there is one comparison
    /// rather than a table of pairs to keep consistent. <c>MutationPolicyTests</c> pins the
    /// alignment so a value added to either enum cannot silently widen what is allowed.
    /// </summary>
    public enum MutationKind
    {
        /// <summary>Words. No state moves.</summary>
        Dialogue = 1,

        /// <summary>Affinity, karma, fame, influence - what people think.</summary>
        Social = 2,

        /// <summary>Objects and money change hands, or leave the world.</summary>
        Inventory = 3,

        /// <summary>The actor ends up living somewhere else.</summary>
        Relocate = 4,

        /// <summary>The actor is absent from the map and represented in procedural state.</summary>
        TemporaryAbsence = 5,

        /// <summary>The actor dies.</summary>
        Death = 6
    }

    /// <summary>
    /// What kind of person the game thinks this is. The mod does not decide this; it asks, and an
    /// answer it did not get is <see cref="Unknown"/> rather than a guess (decision D017).
    /// </summary>
    public enum NarrativeActorClass
    {
        /// <summary>
        /// The build could not say. Never a licence: an unclassified actor keeps the reversible
        /// reaches and is refused every irreversible one, which is what makes the protection hold
        /// on a build whose classification members this mod cannot read at all.
        /// </summary>
        Unknown = 0,

        /// <summary>The player. Their standing and their purse are the mod's business; their life is not.</summary>
        Player = 1,

        /// <summary>
        /// Somebody vanilla content depends on. Observe, talk, and let them like or dislike the
        /// player; never procedurally kill or permanently relocate (LW 5.1).
        /// </summary>
        StoryCritical = 2,

        /// <summary>A named merchant or service. Social freely; absence needs lifecycle proof first.</summary>
        UniqueService = 3,

        /// <summary>An ordinary citizen, guard or generic merchant.</summary>
        OrdinaryCitizen = 4,

        /// <summary>Somebody this mod made. Safe for anything, because nothing else refers to them.</summary>
        Generated = 5
    }

    /// <summary>
    /// Names, on a seam member, what that member does to the game and to whom.
    ///
    /// Put on <see cref="IVanillaState"/> rather than on the implementations, because the contract
    /// is what has to be complete: a write added to the seam without this attribute fails the
    /// census in <c>MutationPolicyTests</c> instead of quietly becoming the one reach nothing
    /// checks. <see cref="Subjects"/> names the parameters that are the *actor* being changed -
    /// not the item, not the town - and an empty list means the subject is the player.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class VanillaMutationAttribute : Attribute
    {
        public VanillaMutationAttribute(MutationKind kind, params string[] subjects)
        {
            Kind = kind;
            Subjects = subjects ?? new string[0];
        }

        public MutationKind Kind { get; }

        /// <summary>Parameter names holding the actor being changed. Empty means the player.</summary>
        public string[] Subjects { get; }
    }

    /// <summary>
    /// The classification itself: which rung each kind of actor stands on.
    ///
    /// This is the whole of LW 5.1 and the only place it is written down. The verbs do not carry
    /// their own opinions about who may be moved; they ask here, and so does the seam.
    /// </summary>
    public static class MutationPolicies
    {
        /// <summary>
        /// The rung an actor of this class stands on.
        ///
        /// Two of these are deliberately lower than the design's eventual ceiling.
        /// <see cref="NarrativeActorClass.UniqueService"/> stops at inventory because taking a
        /// named shopkeeper out of the world needs the lifecycle proof BQ-032 owes, not because
        /// their gold is sacred. <see cref="NarrativeActorClass.OrdinaryCitizen"/> stops short of
        /// death because dying is the one change nothing can walk back, and no procedural route
        /// has yet earned it over somebody the game made.
        /// </summary>
        public static NarrativeMutationPolicy PolicyFor(NarrativeActorClass actorClass)
        {
            switch (actorClass)
            {
                case NarrativeActorClass.Generated:
                    return NarrativeMutationPolicy.FullyMutable;
                case NarrativeActorClass.OrdinaryCitizen:
                    return NarrativeMutationPolicy.TemporarilyRemovable;
                case NarrativeActorClass.UniqueService:
                    return NarrativeMutationPolicy.InventoryMutable;
                case NarrativeActorClass.Player:
                    return NarrativeMutationPolicy.InventoryMutable;
                case NarrativeActorClass.StoryCritical:
                    return NarrativeMutationPolicy.SocialMutable;
                default:
                    // Unknown. The reversible reaches stay open so a build that cannot classify
                    // anybody still plays; every irreversible one is closed, so the guarantee that
                    // a story-critical NPC is unkillable and unmovable does not depend on having
                    // recognised them.
                    return NarrativeMutationPolicy.InventoryMutable;
            }
        }

        public static bool Permits(NarrativeMutationPolicy policy, MutationKind kind)
        {
            return (int)policy >= (int)kind;
        }

        public static bool Permits(NarrativeActorClass actorClass, MutationKind kind)
        {
            return Permits(PolicyFor(actorClass), kind);
        }

        /// <summary>
        /// Whether the mod may do this to this actor, asked of the live world.
        ///
        /// The seam refuses anyway - this is for preconditions, so a verb that certainly cannot
        /// run is absent rather than offered and then declined. Impossibility, not odds.
        /// </summary>
        public static bool MayMutate(this IVanillaState vanilla, EntityId actor, MutationKind kind)
        {
            return vanilla != null && Permits(vanilla.GetActorClass(actor), kind);
        }
    }
}
