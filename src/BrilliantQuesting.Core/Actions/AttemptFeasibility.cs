using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Whether the world allowed an attempt, whether the verb allows it, and only then whether
    /// anything about it is genuinely uncertain (BQa-005, BQa-014).
    ///
    /// Three questions that are asked in one order and never another. Opportunity is settled
    /// first by <see cref="ActionOpportunity.Read"/> - travel, co-location, the other party, the
    /// object - because it is about the place and the hour rather than about the verb, and an
    /// attempt the world refuses is over before the verb is consulted at all. Feasibility is
    /// settled next by <see cref="NarrativeAction.GetAvailability"/> - actor scope, binding,
    /// knowledge, resources, presence, capability - and an attempt it refuses is over too: there is
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
    /// Nothing here is a second opinion about any of them. Opportunity is read off the seam,
    /// availability is asked of the verb, the family is read off the classification BQa-003
    /// declared, and this is the order they are asked in, written once so that every performing
    /// surface asks them the same way instead of each remembering to.
    /// </summary>
    public readonly struct AttemptFeasibility
    {
        private AttemptFeasibility(
            Availability availability,
            ActionOpportunity opportunity,
            CheckFamily? uncertainty,
            CheckProfile profile)
        {
            Availability = availability;
            Opportunity = opportunity;
            Uncertainty = uncertainty;
            Profile = profile;
        }

        /// <summary>The verb's own feasibility verdict, reason included.</summary>
        public Availability Availability { get; }

        /// <summary>
        /// What the world around the attempt allowed, and how plausible it made it (BQa-014).
        ///
        /// Null only for an attempt refused before a verb and a context were both in hand. It is
        /// carried rather than collapsed into <see cref="Availability"/> because the two say
        /// different things: the verb's refusal is about the attempt, this one is about the place
        /// and the hour, and a caller weighing several options wants the plausibility of the ones
        /// that were allowed as much as the reason for the ones that were not.
        /// </summary>
        public ActionOpportunity Opportunity { get; }

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
            return new AttemptFeasibility(availability, null, null, null);
        }

        /// <summary>An attempt the world refused, carrying the reading that refused it.</summary>
        private static AttemptFeasibility Blocked(Availability availability, ActionOpportunity opportunity)
        {
            return new AttemptFeasibility(availability, opportunity, null, null);
        }

        /// <summary>
        /// Asks the three questions in order: did the world allow this, does the verb allow it,
        /// and if it does, what kind of uncertainty does it hold.
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

            // Three questions now, still in one order and never another. Opportunity is asked
            // first because it is the one that can be answered without knowing anything about the
            // verb's own rules: somebody Elin is carrying between zones, or two people the save
            // keeps a valley apart, are not a hard attempt but no attempt at all. Asking it after
            // availability would let a verb spend its own reasoning - and, worse, let a caller
            // read a considered "yes" - on a place where the act could not occur.
            ActionOpportunity opportunity = ActionOpportunity.Read(action, context);
            if (!opportunity.IsPossible)
            {
                return Blocked(Availability.NotRelevant(opportunity.Refusal), opportunity);
            }

            Availability availability = action.GetAvailability(context);
            if (!availability.IsAvailable)
            {
                return Blocked(availability, opportunity);
            }

            return new AttemptFeasibility(
                availability,
                opportunity,
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

            if (Opportunity != null && Opportunity.Plausibility < 1.0)
            {
                // Named here rather than only in the reading, because a surface that prints one
                // line about an attempt should not have to already suspect the place was against
                // it before it goes looking.
                string plausibility = Opportunity.Plausibility.ToString(
                    "0.00", System.Globalization.CultureInfo.InvariantCulture);
                return "available (opportunity " + plausibility + "); " + Tail();
            }

            return "available; " + Tail();
        }

        private string Tail()
        {
            if (IsCertain)
            {
                return "certain, no check";
            }

            string family = Uncertainty.Value.ToString().ToLowerInvariant();
            return Profile == null
                ? "uncertain (" + family + ")"
                : "uncertain (" + family + " via " + Profile.Id + " dc" + Profile.BaseDifficulty + ")";
        }
    }
}
