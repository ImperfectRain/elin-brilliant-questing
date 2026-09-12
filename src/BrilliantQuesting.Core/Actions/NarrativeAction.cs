using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// A reusable RPG verb.
    ///
    /// The content strategy of the whole mod rests on this class: the project invests in a small
    /// number of well-understood verbs that know how real Elin mechanics work, and lets generated
    /// situations decide when they become relevant. It does not invest in quest scripts.
    ///
    /// One vocabulary, both actor kinds (BQ-093, `CD 47.5`, `PM 35`). There is no `PlayerBribe`
    /// and no `NpcBribe`: there is <c>bribe</c>, and <see cref="ActionContext.Actor"/> says who is
    /// offering. Selection - what an NPC wants to attempt - is somebody else's question entirely
    /// and is answered before anything here is called.
    ///
    /// <see cref="GetAvailability"/> and <see cref="Perform"/> are deliberately not virtual. Each
    /// asks <see cref="ActorScope"/> first and only then hands over to the verb's own
    /// <see cref="GetAvailabilityCore"/> / <see cref="PerformCore"/>, so a verb whose body was
    /// written with the player in mind cannot be reached by an NPC merely because its author did
    /// not think to check. The refusal is structural rather than remembered.
    /// </summary>
    public abstract class NarrativeAction
    {
        protected NarrativeAction(string id, ActionFamily family, string label)
        {
            Id = id;
            Family = family;
            Label = label;
        }

        public string Id { get; }

        public ActionFamily Family { get; }

        /// <summary>Short player-facing verb ("Lie", "Pick their pocket").</summary>
        public string Label { get; }

        /// <summary>
        /// Who may attempt this verb. Actor-generic unless the verb says otherwise, because that
        /// is what the great majority of them are and a default that refused would quietly shrink
        /// the shared vocabulary every time somebody added a verb.
        /// </summary>
        public virtual ActorScope ActorScope => ActorScope.AnyActor;

        /// <summary>
        /// What this verb needs from the body of whoever performs it. Narrative unless the verb
        /// says otherwise (`D021`).
        /// </summary>
        public virtual ActorEmbodiment Embodiment => ActorEmbodiment.Narrative;

        /// <summary>
        /// Whether a successful use of this verb can end the matter it was used inside (BQ-094).
        ///
        /// A statement about what the verb is *for*, and the third thing a verb declares about
        /// itself beside <see cref="ActorScope"/> and <see cref="Embodiment"/>. It is declared
        /// rather than derived because availability cannot tell the difference: a shortage makes
        /// <c>bribe</c> and <c>buy_supplies</c> equally applicable to the hungry person, and only
        /// one of them is an answer. A caller choosing a verb on somebody's behalf has to know
        /// which, and the verb is the only thing that does - a table of helpful verbs kept beside
        /// the library would be the hand-kept list that drifts.
        ///
        /// False by default, and false is not a demotion: most of the library exists to find
        /// things out, get somebody's attention, take something, or make somebody feel a way
        /// about you, and none of that ends a situation. What is true here is exactly the set that
        /// reaches <c>ActionSupport.Resolve</c>, which is the one place a matter is ever closed.
        ///
        /// It is not a promise of success and never a reason to hide an option from the player.
        /// The player's surfaces do not read it: a player is entitled to try anything the library
        /// offers, including things that will not help.
        /// </summary>
        public virtual bool SettlesMatters => false;

        /// <summary>
        /// What kinds of state change this verb could potentially advance, and what it has to be
        /// pointed at before it means anything (BQa-010).
        ///
        /// The fourth thing a verb declares about itself, beside <see cref="ActorScope"/>,
        /// <see cref="Embodiment"/> and <see cref="SettlesMatters"/>, and declared for the same
        /// reason: a consumer choosing a verb on somebody's behalf has to know what taking it
        /// could change, and the verb is the only thing that does. <see cref="Family"/> answers a
        /// different question - which kind of character has a route in - and two verbs of one
        /// family routinely do unrelated things to the world.
        ///
        /// <see cref="ActionEffects.Undeclared"/> by default, and that default is a reported gap
        /// rather than a claim that nothing changes. It is metadata and never a promise: it
        /// executes nothing, it does not answer <see cref="GetAvailability"/>, and an attempt
        /// that fails may do something else entirely.
        /// </summary>
        public virtual ActionEffects Effects => ActionEffects.Undeclared;

        /// <summary>
        /// Whether this verb claims that kind of change at all, and - for a half vanilla has to
        /// carry - whether this build could carry it. Side-effect free, like every other question
        /// asked of a verb before it is taken.
        /// </summary>
        public bool CanPotentiallyAdvance(string effectKind, IVanillaState vanilla, out string refusal)
        {
            ActionEffect effect = Effects.Find(effectKind);
            if (effect == null)
            {
                refusal = Effects.IsDeclared
                    ? Id + " does not advance " + effectKind
                    : Id + " has declared no semantic effects";
                return false;
            }

            return effect.CanBeCarried(vanilla, out refusal);
        }

        /// <summary>
        /// Whether this makes sense here at all. Must be side-effect free: the discovery pass
        /// calls it for every registered action, including ones it will never show.
        ///
        /// The actor gate runs first, so a verb the actor may not take answers unavailable with
        /// the classification's own reason rather than with whatever the verb's body would have
        /// concluded from state that is not about them.
        /// </summary>
        public Availability GetAvailability(ActionContext context)
        {
            Availability admits = ActorScope.Admits(context);
            return admits.IsAvailable ? GetAvailabilityCore(context) : admits;
        }

        /// <summary>
        /// Resolves the attempt and writes its consequences into world history.
        ///
        /// Fails closed rather than trusting the caller: an attempt by an actor this verb does not
        /// admit produces a refusal outcome with no roll and no events, instead of running a body
        /// that would read the player's purse, standing or Home.
        /// </summary>
        public ActionOutcome Perform(ActionContext context)
        {
            Availability admits = ActorScope.Admits(context);
            if (!admits.IsAvailable)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "That is not something they can do.");
                refused.Notes.Add("refused before any roll: " + admits.Reason);
                refused.Embodiment = Embodiment;
                refused.Observation = context.Observation;
                return refused;
            }

            ActionOutcome outcome = PerformCore(context);
            if (outcome != null)
            {
                outcome.Embodiment = Embodiment;
                outcome.Observation = context.Observation;
            }

            return outcome;
        }

        /// <summary>The verb's own availability question, asked only of an actor it admits.</summary>
        protected abstract Availability GetAvailabilityCore(ActionContext context);

        /// <summary>The verb's own resolution, run only for an actor it admits.</summary>
        protected abstract ActionOutcome PerformCore(ActionContext context);
    }
}
