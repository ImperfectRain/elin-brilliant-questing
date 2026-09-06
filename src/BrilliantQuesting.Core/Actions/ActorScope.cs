namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// How far a registered verb reaches across actors (BQ-093).
    ///
    /// The library is one vocabulary for everybody, and that is not the same claim as "every verb
    /// is executable by every actor". A shared verb set needs a way to say *why* somebody cannot
    /// take a verb, in the same words for the player and for an NPC, so that a refusal is a
    /// recorded classification rather than an accident of which state happened to read as zero.
    ///
    /// The three answers are deliberately different questions, and only the first two are
    /// permanent:
    /// </summary>
    public enum ActorReach
    {
        /// <summary>
        /// Semantically actor-generic. Whoever is named in <see cref="ActionContext.Actor"/> may
        /// attempt it, and the availability, check and consequence paths are the same ones.
        /// </summary>
        AnyActor,

        /// <summary>
        /// Player-only because the vanilla mechanic underneath it is genuinely the player's. The
        /// Home is the player's settlement, a guild card is the player's membership, Karma and
        /// Fame are the player's standing: Elin keeps exactly one of each and there is no second
        /// one belonging to a citizen. This is not a gap to be closed later; inventing an NPC
        /// equivalent would be a second mechanic disagreeing with the visible one (`D014`, `D018`).
        /// </summary>
        PlayerOnly,

        /// <summary>
        /// Nothing about the verb is player-specific, and a capability BQ does not have yet is
        /// missing. Refused for a non-player actor until that capability exists, and named so the
        /// inspector says which one - never quietly resolved down a path that reads the player's
        /// state instead.
        /// </summary>
        AwaitingCapability
    }

    /// <summary>
    /// One verb's answer to "who may attempt this", together with the reason it gives when the
    /// answer is no.
    ///
    /// Declared by the action, the way <see cref="SpatialRouteClaim"/> is declared by the verbs
    /// that are routes, and for the same reason: a hand-kept table beside the library is the thing
    /// that lets the two drift apart. It is deliberately *not* folded into
    /// <see cref="NarrativeAction.GetAvailability"/>'s body - it is asked first, by
    /// <see cref="NarrativeAction"/> itself, so that a verb cannot forget to ask and an NPC can
    /// never fall through into a branch written for the player.
    /// </summary>
    public readonly struct ActorScope
    {
        private readonly string _reason;

        private ActorScope(ActorReach reach, string reason)
        {
            Reach = reach;
            _reason = reason;
        }

        public ActorReach Reach { get; }

        /// <summary>Why a non-player actor is refused. Empty for <see cref="ActorReach.AnyActor"/>.</summary>
        public string Reason => _reason ?? string.Empty;

        /// <summary>Anybody may attempt it. The default, and what the great majority of verbs are.</summary>
        public static ActorScope AnyActor => new ActorScope(ActorReach.AnyActor, string.Empty);

        /// <summary>
        /// The vanilla mechanic underneath is the player's own. <paramref name="reason"/> names
        /// which one, because "player only" on its own is indistinguishable from an oversight.
        /// </summary>
        public static ActorScope PlayerOnly(string reason)
        {
            return new ActorScope(ActorReach.PlayerOnly, reason);
        }

        /// <summary>
        /// Generic in principle, refused in practice until a named capability exists.
        /// <paramref name="reason"/> names the missing capability.
        /// </summary>
        public static ActorScope AwaitingCapability(string reason)
        {
            return new ActorScope(ActorReach.AwaitingCapability, reason);
        }

        /// <summary>Whether an NPC may attempt this verb at all.</summary>
        public bool AdmitsNonPlayer => Reach == ActorReach.AnyActor;

        /// <summary>
        /// Whether this context's actor is admitted, as an ordinary availability verdict so that
        /// a refusal reaches the inspector by the same route every other refusal does.
        ///
        /// <see cref="Availability.Impossible"/> rather than
        /// <see cref="Availability.NotRelevant"/>: there is no attempt here that might come off,
        /// which is the same shape the underworld contact refusal and invoking guild authority
        /// without a card already have (`PM 62`, `D012`).
        /// </summary>
        public Availability Admits(ActionContext context)
        {
            if (Reach == ActorReach.AnyActor || context == null || context.ActorIsPlayer)
            {
                return Availability.Available();
            }

            return Availability.Impossible(Reason.Length == 0
                ? "only the player can do that"
                : Reason);
        }

        public override string ToString()
        {
            return Reach == ActorReach.AnyActor ? "any actor" : Reach + ": " + Reason;
        }
    }
}
