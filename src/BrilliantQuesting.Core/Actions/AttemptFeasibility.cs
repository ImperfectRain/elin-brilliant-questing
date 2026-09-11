using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Whether an attempt is possible at all, and only then whether anything about it is
    /// genuinely uncertain (BQa-005).
    ///
    /// Two questions that are asked in one order and never the other way round. Feasibility is
    /// settled first by <see cref="NarrativeAction.GetAvailability"/> - actor scope, binding,
    /// knowledge, resources, presence, capability - and an attempt it refuses is over: there is
    /// nothing left to be uncertain about, so nothing here classifies one, and no dice are reached
    /// on the way out. Asking the other way round is how a system ends up rolling to see whether
    /// somebody managed to reveal a secret they never heard, and calling a good roll a success.
    ///
    /// The certainty question is deliberately narrower than the feasibility one. It does not ask
    /// how likely the attempt is; it asks whether the attempt contains uncertainty worth rolling.
    /// That answer comes from the verb's own contract through
    /// <see cref="ProceduralCheckProfiles.FamilyForAction"/> - a verb with no profile is settled
    /// by whether you have the thing and whether the counter deals - and never from the arithmetic
    /// a resolver would produce. A DC so favourable that failure is rare is still an uncertain
    /// attempt: mastery buys a low number, and BQa-004 left the profile's dice and critical windows
    /// untouched precisely so a low number cannot abolish the roll. Reading certainty off a DC
    /// would also make it depend on who is attempting, when it is a fact about the verb.
    ///
    /// Nothing here is a second opinion about either question. Availability is asked of the verb,
    /// the family is read off the classification BQa-003 declared, and this is the order the two
    /// are asked in, written once so that every performing surface asks them the same way instead
    /// of each remembering to.
    /// </summary>
    public readonly struct AttemptFeasibility
    {
        private AttemptFeasibility(Availability availability, CheckFamily? uncertainty, CheckProfile profile)
        {
            Availability = availability;
            Uncertainty = uncertainty;
            Profile = profile;
        }

        /// <summary>The verb's own feasibility verdict, reason included.</summary>
        public Availability Availability { get; }

        public bool IsPossible => Availability.IsAvailable;

        /// <summary>Why not, or the note the verb attached to an available attempt.</summary>
        public string Reason => Availability.Reason;

        /// <summary>
        /// What kind of uncertainty a possible attempt holds, and null when it is not possible.
        ///
        /// Null rather than <see cref="CheckFamily.Certain"/>, which would be the wrong answer in
        /// the most dangerous direction: "there is no uncertainty here" reads as permission to
        /// resolve without a roll, and a refused attempt must not resolve at all.
        /// </summary>
        public CheckFamily? Uncertainty { get; }

        /// <summary>
        /// The profile a genuinely uncertain attempt routes to. Null both for a refused attempt
        /// and for a semantically certain one, which are distinguished by <see cref="IsPossible"/>.
        /// </summary>
        public CheckProfile Profile { get; }

        /// <summary>Possible, and the verb's contract says there is nothing to roll.</summary>
        public bool IsCertain => IsPossible && Uncertainty == CheckFamily.Certain;

        /// <summary>Possible, and a classified check is owed.</summary>
        public bool IsUncertain => IsPossible && Uncertainty.HasValue && Uncertainty.Value != CheckFamily.Certain;

        /// <summary>
        /// An attempt that never reached classification, for the callers that refuse one before a
        /// verb is in hand at all - an unregistered id, a missing context.
        /// </summary>
        public static AttemptFeasibility Blocked(Availability availability)
        {
            return new AttemptFeasibility(availability, null, null);
        }

        /// <summary>
        /// Asks the two questions in order: is this possible, and if it is, what kind of
        /// uncertainty does it hold.
        ///
        /// Side-effect free, like the availability call underneath it, so a surface may classify
        /// every option it is considering - including the ones it will refuse - without anything
        /// happening to the world or to the RNG stream.
        /// </summary>
        public static AttemptFeasibility Classify(NarrativeAction action, ActionContext context)
        {
            if (action == null)
            {
                return Blocked(Availability.Impossible("there is no such verb"));
            }

            if (context == null)
            {
                return Blocked(Availability.NotRelevant("nothing to attempt in"));
            }

            Availability availability = action.GetAvailability(context);
            if (!availability.IsAvailable)
            {
                return Blocked(availability);
            }

            return new AttemptFeasibility(
                availability,
                ProceduralCheckProfiles.FamilyForAction(action.Id),
                ProceduralCheckProfiles.ForAction(action.Id));
        }

        /// <summary>The two verdicts as one line, for the inspector.</summary>
        public override string ToString()
        {
            if (!IsPossible)
            {
                return Availability.ToString() + "; nothing classified";
            }

            if (IsCertain)
            {
                return "available; certain, no check";
            }

            string family = Uncertainty.Value.ToString().ToLowerInvariant();
            return Profile == null
                ? "available; uncertain (" + family + ")"
                : "available; uncertain (" + family + " via " + Profile.Id + " dc" + Profile.BaseDifficulty + ")";
        }
    }
}
