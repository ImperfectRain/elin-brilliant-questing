using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// The things an attempt can care about in the world around it (BQa-014).
    ///
    /// A closed vocabulary rather than free text, for the same reason
    /// <see cref="ActivityFacetKind"/> is one: a reading has to be able to say which question it
    /// could not answer, and a diagnostic has to be able to name it without spelling the list out
    /// again at every call site. The two lists are about different things and neither answers the
    /// other's questions - <see cref="ActivityFacetKind"/> is what the build said about somebody,
    /// this is what that means for one attempt in one place.
    /// </summary>
    public enum OpportunityFacet
    {
        /// <summary>Whether the acting party and the other party are in one place.</summary>
        CoLocation,

        /// <summary>Whether this could be done unseen.</summary>
        Privacy,

        /// <summary>Who was actually read as present. Never a guess, and never a count off screen.</summary>
        Witnesses,

        /// <summary>Whether the other party is somebody who can be dealt with at all.</summary>
        TargetAvailability,

        /// <summary>Whether the object the attempt is about is in the hands of somebody party to it.</summary>
        ObjectAccessibility,

        /// <summary>Whether vanilla has them at their work or their counter.</summary>
        WorkService,

        /// <summary>Which stretch of the day their own timetable puts them in.</summary>
        Routine,

        /// <summary>Whether Elin is already carrying somebody between zones.</summary>
        Travel,

        /// <summary>Whether somebody's hands are full of something worse.</summary>
        Danger,

        /// <summary>How long it has been since the matter last moved.</summary>
        ElapsedTime
    }

    /// <summary>How a verb gets to the other party when it needs one.</summary>
    public enum OpportunityReach
    {
        /// <summary>
        /// It has to be done with them: in the same place, at the same time. The default, because
        /// the great majority of the library is hands, faces and objects, and a default that
        /// reached anywhere would let a scheme pass rob somebody two zones away.
        /// </summary>
        Present,

        /// <summary>
        /// It travels through an established contact or report channel - a word left at the guard
        /// post, a complaint filed, a standing line to somebody who holds an office. Eligible
        /// without exact co-location precisely because it claims no meeting: nobody stood
        /// anywhere, and the record says only that the thing was communicated.
        /// </summary>
        Channel
    }

    /// <summary>
    /// One verb's declaration of how it reaches whoever it is aimed at (BQa-014).
    ///
    /// The sixth thing a verb declares about itself, beside <see cref="ActorScope"/>,
    /// <see cref="ActorEmbodiment"/>, <c>SettlesMatters</c>, <see cref="ActionEffects"/> and
    /// <see cref="ActionPostconditions"/>, and declared for the same reason as the rest: a caller
    /// weighing this verb off screen has to know whether unknown whereabouts sink it, and the verb
    /// is the only thing that knows. <see cref="ActorEmbodiment"/> answers a neighbouring but
    /// different question - what vanilla has to carry - and a verb can need no vanilla write at
    /// all and still need the two people in one room.
    /// </summary>
    public sealed class ActionReach
    {
        private ActionReach(OpportunityReach mode, string channel)
        {
            Mode = mode;
            Channel = channel ?? string.Empty;
        }

        /// <summary>The default: it has to be done with them, wherever they are.</summary>
        public static readonly ActionReach Present = new ActionReach(OpportunityReach.Present, string.Empty);

        /// <summary>
        /// It reaches through <paramref name="channel"/>, which names the established route so
        /// that a reader of the inspector can see what carried it instead of assuming a meeting.
        /// </summary>
        public static ActionReach ThroughChannel(string channel)
        {
            return new ActionReach(OpportunityReach.Channel, channel);
        }

        public OpportunityReach Mode { get; }

        /// <summary>The named route, for <see cref="OpportunityReach.Channel"/>. Empty otherwise.</summary>
        public string Channel { get; }

        public string Describe()
        {
            return Mode == OpportunityReach.Channel
                ? "reaches through " + Channel + ", claiming no meeting"
                : "has to be done with them";
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// One facet, what was read for it, and what that did to the attempt.
    ///
    /// <see cref="Known"/> is the load-bearing field and it is separate from
    /// <see cref="Weight"/> on purpose: an unread facet and a facet read as harmless are both
    /// worth 1.0, and only one of them may ever be quoted as evidence (`D017`).
    /// </summary>
    public sealed class OpportunityTerm
    {
        internal OpportunityTerm(OpportunityFacet facet, bool known, double weight, string observation, bool barring)
        {
            Facet = facet;
            Known = known;
            Weight = weight;
            Observation = observation ?? string.Empty;
            Barring = barring;
        }

        public OpportunityFacet Facet { get; }

        /// <summary>Whether the build answered this facet at all. Unknown never grants anything.</summary>
        public bool Known { get; }

        /// <summary>What it multiplied the plausibility by. 1.0 both for unread and for harmless.</summary>
        public double Weight { get; }

        /// <summary>What was actually read, in words. Every refusal and every bonus carries one.</summary>
        public string Observation { get; }

        /// <summary>Whether this term is the one that refused the attempt.</summary>
        public bool Barring { get; }

        public override string ToString()
        {
            string weight = Barring
                ? "refused"
                : "x" + Weight.ToString("0.00", CultureInfo.InvariantCulture);

            return Facet + " " + weight + ": " + Observation;
        }
    }

    /// <summary>
    /// What the world around one attempt allows and how plausible it makes it (BQa-014).
    ///
    /// This is the upgrade of <c>InterventionOpportunity</c>'s loose activity weighting into a
    /// reading about an actual attempt, and the thing it adds is that the two evidence modes are
    /// kept apart rather than averaged:
    ///
    /// <b>Observed local opportunity.</b> The zone the game is running was read. Co-location is
    /// verified, the room's contents are the witness list, and privacy is an observation.
    ///
    /// <b>Coarse off-screen opportunity.</b> Nobody's presence was read. What is still legitimately
    /// available is the save's own coarse state - which zone the game records somebody in, what
    /// vanilla has them doing, what is in whose pack - and none of it becomes a meeting, a
    /// witness, a position or a moment (`VS 3.2`, `VS 5.4`, `D021`). Two people recorded in one
    /// town is an opportunity to have crossed paths and is never proof that they did:
    /// <see cref="VerifiedCoLocation"/> is false for every coarse reading, and the witness facet
    /// is unread rather than zero.
    ///
    /// It is a read. It rolls nothing, records nothing, mutates nothing and holds nothing: the
    /// activity snapshot underneath it is transient and is asked for again next pass (`D004`,
    /// `D005`). Where it refuses, it refuses on something the seam actually answered, and the
    /// refusal quotes it.
    /// </summary>
    public sealed class ActionOpportunity
    {
        private readonly List<OpportunityTerm> _terms;

        private ActionOpportunity(
            ContextObservation mode,
            double plausibility,
            bool verifiedCoLocation,
            string refusal,
            List<OpportunityTerm> terms)
        {
            Mode = mode;
            Plausibility = plausibility < 0.0 ? 0.0 : plausibility > 1.0 ? 1.0 : plausibility;
            VerifiedCoLocation = verifiedCoLocation;
            Refusal = refusal ?? string.Empty;
            _terms = terms;
        }

        /// <summary>Which of the two evidence modes this reading was taken in.</summary>
        public ContextObservation Mode { get; }

        /// <summary>0..1. Higher is a better chance the world allowed this attempt.</summary>
        public double Plausibility { get; }

        /// <summary>
        /// Whether the two parties were read as standing in one place the game was running.
        ///
        /// False for every coarse reading, including one where the save records both of them in
        /// the same town, because that is where they are kept and not where they were seen. It is
        /// the single question anything wanting to write a meeting, a position or an eyewitness
        /// has to ask, and it is deliberately not derivable from
        /// <see cref="Plausibility"/> - an eligible coarse attempt scores well and proves nothing.
        /// </summary>
        public bool VerifiedCoLocation { get; }

        /// <summary>Whether the world allowed the attempt at all.</summary>
        public bool IsPossible => Refusal.Length == 0;

        /// <summary>Why not, quoting the observation behind it. Empty when it was allowed.</summary>
        public string Refusal { get; }

        /// <summary>Every facet that was looked at, including the ones nothing answered.</summary>
        public IReadOnlyList<OpportunityTerm> Terms => _terms;

        /// <summary>The term for one facet, or null when the reading did not look at it.</summary>
        public OpportunityTerm Term(OpportunityFacet facet)
        {
            for (int i = 0; i < _terms.Count; i++)
            {
                if (_terms[i].Facet == facet)
                {
                    return _terms[i];
                }
            }

            return null;
        }

        /// <summary>Whether this build answered that facet at all.</summary>
        public bool IsKnown(OpportunityFacet facet)
        {
            OpportunityTerm term = Term(facet);
            return term != null && term.Known;
        }

        /// <summary>
        /// Reads the world around <paramref name="context"/> for <paramref name="action"/>.
        ///
        /// Side-effect free, like every other question asked of a verb before it is taken, so a
        /// surface may read the opportunity of every option it is considering - including the ones
        /// it will refuse - without anything happening to the world or to the RNG stream.
        /// </summary>
        public static ActionOpportunity Read(NarrativeAction action, ActionContext context)
        {
            List<OpportunityTerm> terms = new List<OpportunityTerm>();
            if (action == null || context == null || context.Vanilla == null)
            {
                terms.Add(new OpportunityTerm(
                    OpportunityFacet.CoLocation, false, 0.0, "there is nothing to read this attempt in", true));
                return new ActionOpportunity(
                    ContextObservation.OffScreen, 0.0, false, "there is nothing to read this attempt in", terms);
            }

            IVanillaState vanilla = context.Vanilla;
            ContextObservation mode = context.Observation;
            ActorActivity actorActivity = vanilla.GetActorActivity(context.Actor);
            bool verified = false;
            string refusal = string.Empty;

            Bar(terms, ref refusal, ReadTravel(actorActivity, "them", "they are"));
            if (!context.Target.IsNone)
            {
                Bar(terms, ref refusal, ReadTravel(
                    vanilla.GetActorActivity(context.Target), "the other party", "the other party is"));
                Bar(terms, ref refusal, ReadTargetAvailability(context));
                Bar(terms, ref refusal, ReadCoLocation(action, context, actorActivity, mode, out verified));
            }

            terms.Add(ReadObjectAccessibility(action, context));
            terms.Add(ReadWitnesses(context, mode));
            terms.Add(ReadPrivacy(action, context, mode));
            terms.Add(ReadActivity(actorActivity));
            terms.Add(ReadRoutine(actorActivity));
            terms.Add(ReadDanger(vanilla, context));
            terms.Add(ReadElapsedTime(context));

            double plausibility = 1.0;
            for (int i = 0; i < terms.Count; i++)
            {
                plausibility *= terms[i].Weight;
            }

            return new ActionOpportunity(
                mode,
                refusal.Length > 0 ? 0.0 : plausibility,
                verified && refusal.Length == 0,
                refusal,
                terms);
        }

        /// <summary>
        /// What vanilla has somebody doing, weighed. Shared with the autonomy pass's own
        /// actor-and-place weight so that "they are asleep" is one answer rather than two.
        /// </summary>
        public static OpportunityTerm ReadActivity(ActorActivity activity)
        {
            if (activity == null || activity.CurrentActivity == ActivityFamily.Unknown)
            {
                return Unread(OpportunityFacet.WorkService, "what they are doing: unread, counted for nothing");
            }

            switch (activity.CurrentActivity)
            {
                case ActivityFamily.Combat:
                    return Read(OpportunityFacet.WorkService, 0.15, "in combat: their hands are full");
                case ActivityFamily.Sleep:
                case ActivityFamily.Needs:
                    return Read(OpportunityFacet.WorkService, 0.5, "asleep or seeing to themselves");
                default:
                    return Read(
                        OpportunityFacet.WorkService,
                        1.0,
                        "doing " + activity.CurrentActivity.ToString().ToLowerInvariant() + ": no obstacle");
            }
        }

        /// <summary>
        /// Which stretch of their own day the timetable puts them in. Shared with the autonomy
        /// pass for the same reason <see cref="ReadActivity"/> is.
        /// </summary>
        public static OpportunityTerm ReadRoutine(ActorActivity activity)
        {
            if (activity == null || activity.CurrentSpan == ActivitySpan.Unknown)
            {
                return Unread(
                    OpportunityFacet.Routine, "their routine's stretch of the day: unread, counted for nothing");
            }

            if (activity.CurrentSpan == ActivitySpan.Sleep)
            {
                return Read(OpportunityFacet.Routine, 0.6, "their routine puts them asleep about now");
            }

            return Read(
                OpportunityFacet.Routine,
                1.0,
                "their routine's stretch of the day: " + activity.CurrentSpan.ToString().ToLowerInvariant());
        }

        /// <summary>Every term, one per line, for the inspector.</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Mode == ContextObservation.Observed ? "observed local opportunity " : "coarse off-screen opportunity ");
            sb.Append(Plausibility.ToString("0.00", CultureInfo.InvariantCulture));
            if (!IsPossible)
            {
                sb.Append("; refused: ").Append(Refusal);
            }

            sb.Append(VerifiedCoLocation
                ? "; co-location verified"
                : "; no verified co-location, so nothing here is physical evidence");

            for (int i = 0; i < _terms.Count; i++)
            {
                sb.Append("\n  - ").Append(_terms[i]);
            }

            return sb.ToString();
        }

        public override string ToString() => Describe();

        // -- the facets --------------------------------------------------------------------

        /// <summary>
        /// Whether Elin is already carrying somebody between zones.
        ///
        /// The one refusal the seam can answer outright, in both evidence modes, and the one place
        /// BQ concedes embodiment rather than scheduling a second journey on top of vanilla's
        /// (`VS 3.3`, `D021`). Unknown is not a refusal: on a build where the travel facets are
        /// unread everything would be refused, and an unread facet is not "moving".
        /// </summary>
        private static OpportunityTerm ReadTravel(ActorActivity activity, string carried, string standing)
        {
            switch (activity.VanillaMovementState())
            {
                case VanillaMovement.Moving:
                    return Barred(
                        OpportunityFacet.Travel,
                        "vanilla is already carrying " + carried + " between zones; BQ leaves embodiment to Elin");
                case VanillaMovement.NotMoving:
                    return Read(
                        OpportunityFacet.Travel, 1.0, "the game says " + standing + " not being carried anywhere");
                default:
                    return Unread(
                        OpportunityFacet.Travel, "whether " + standing + " travelling: unread, counted for nothing");
            }
        }

        private static OpportunityTerm ReadTargetAvailability(ActionContext context)
        {
            if (!context.Vanilla.IsAlive(context.Target))
            {
                return Barred(
                    OpportunityFacet.TargetAvailability, "the game does not answer for the other party as a living person");
            }

            if (context.World != null && context.World.Absences.IsPhysicallyAbsent(context.Target))
            {
                return Barred(OpportunityFacet.TargetAvailability, "the other party is away from here entirely");
            }

            return Read(OpportunityFacet.TargetAvailability, 1.0, "the other party is somebody who can be dealt with");
        }

        /// <summary>
        /// Whether the two parties are in one place, and what kind of answer that is.
        ///
        /// The whole evidence-mode separation is here. An observed reading is taken in a zone the
        /// game is running, which <see cref="ActorContexts.TryBuild"/> has already held to one
        /// place, so it is verified and may be quoted. A coarse reading has only the zone the save
        /// records, which is opportunity and never proof - so it is eligible and
        /// <see cref="VerifiedCoLocation"/> stays false.
        ///
        /// Coarse and unknown is a refusal for a verb that has to be done with somebody, and that
        /// is the point of the facet: the alternative is a scheme pass picking a pocket across two
        /// zones because nothing said it could not.
        /// </summary>
        private static OpportunityTerm ReadCoLocation(
            NarrativeAction action,
            ActionContext context,
            ActorActivity actorActivity,
            ContextObservation mode,
            out bool verified)
        {
            verified = false;
            ActionReach reach = action.Reach ?? ActionReach.Present;
            IVanillaState vanilla = context.Vanilla;

            EntityId actorZone = actorActivity.CurrentZone.IsNone
                ? vanilla.GetZoneOf(context.Actor)
                : actorActivity.CurrentZone;
            EntityId targetZone = vanilla.GetZoneOf(context.Target);

            if (reach.Mode == OpportunityReach.Channel)
            {
                return Read(
                    OpportunityFacet.CoLocation,
                    1.0,
                    "no meeting is needed or claimed: it " + reach.Describe());
            }

            if (mode == ContextObservation.Observed)
            {
                if (actorZone.IsNone || targetZone.IsNone)
                {
                    // The caller says this is happening in a room it read. Refusing on a zone id
                    // nobody wrote down would not make that safer, only quieter.
                    return Unread(
                        OpportunityFacet.CoLocation,
                        "the room was read but no zone was named for both of them: counted for nothing");
                }

                if (actorZone != targetZone)
                {
                    return Barred(
                        OpportunityFacet.CoLocation,
                        "the room was read and they are not in it: " + actorZone + " against " + targetZone);
                }

                verified = true;
                return Read(
                    OpportunityFacet.CoLocation, 1.0, "read in one place the game is running: " + actorZone);
            }

            if (actorZone.IsNone || targetZone.IsNone)
            {
                return Barred(
                    OpportunityFacet.CoLocation,
                    "nothing answered where they both are, and this has to be done with them");
            }

            if (actorZone != targetZone)
            {
                return Barred(
                    OpportunityFacet.CoLocation,
                    "the save keeps them apart - " + actorZone + " against " + targetZone
                    + " - and this has to be done with them");
            }

            return Read(
                OpportunityFacet.CoLocation,
                0.85,
                "the save keeps them both in " + actorZone
                + ": a chance to have crossed paths, never proof that they did");
        }

        /// <summary>
        /// Whether the object this attempt is about is still somewhere the attempt could get at
        /// it.
        ///
        /// A weight and deliberately not a refusal. Whether a verb can proceed without its object
        /// is the verb's own question and it already answers it - <c>pickpocket</c> refuses a
        /// pocket with nothing in it, <c>return_item</c> refuses handing back what you are not
        /// carrying - and a second gate here would be a second authority saying the same thing in
        /// different words, with the wrong answer whenever a binding names an object the verb is
        /// not actually reaching for.
        ///
        /// It is read only for a verb whose declared effects are about a thing at all, bounded to
        /// the people and the place this attempt already names rather than swept out of the
        /// world, and only where the build says it can answer inventories - an unanswerable read
        /// is unknown and grants nothing rather than emptying everybody's pack.
        /// </summary>
        private static OpportunityTerm ReadObjectAccessibility(NarrativeAction action, ActionContext context)
        {
            if (context.SubjectItem.IsNone || !ActsOnObjects(action))
            {
                return Unread(OpportunityFacet.ObjectAccessibility, "no object this attempt reaches for");
            }

            IVanillaState vanilla = context.Vanilla;
            if (!vanilla.Supports(VanillaCapability.ReadInventory))
            {
                return Unread(
                    OpportunityFacet.ObjectAccessibility, "this build does not answer what anybody is carrying");
            }

            EntityId[] holders = { context.Actor, context.Target, context.ThirdParty, context.Zone };
            for (int i = 0; i < holders.Length; i++)
            {
                if (holders[i].IsNone)
                {
                    continue;
                }

                IReadOnlyList<ItemDescriptor> carried = vanilla.GetInventory(holders[i]);
                for (int h = 0; carried != null && h < carried.Count; h++)
                {
                    if (carried[h].Id == context.SubjectItem)
                    {
                        return Read(
                            OpportunityFacet.ObjectAccessibility,
                            1.0,
                            "the object is with " + holders[i]);
                    }
                }
            }

            return Read(
                OpportunityFacet.ObjectAccessibility,
                0.5,
                "nobody and nowhere this attempt names is holding " + context.SubjectItem + " any more");
        }

        /// <summary>
        /// Whether this verb's declared effects are about a thing.
        ///
        /// Read off the BQa-010 vocabulary rather than off a list of verb ids kept here, so a new
        /// verb that moves or breaks something is asked the object question without anybody
        /// editing this file. An undeclared verb answers no and gets an unread facet, which is
        /// the same reported gap <see cref="ActionRegistry.EffectCoverage"/> names.
        /// </summary>
        private static bool ActsOnObjects(NarrativeAction action)
        {
            ActionEffects effects = action.Effects;
            return effects.Advances(SemanticEffects.PossessionTransferred)
                   || effects.Advances(SemanticEffects.EvidenceRemoved)
                   || effects.Advances(SemanticEffects.EvidenceCreated)
                   || effects.Advances(SemanticEffects.ObjectRepaired)
                   || effects.Advances(SemanticEffects.ObjectDamaged);
        }

        /// <summary>
        /// Who was read as present. Off screen this is unread and never a count, which is the
        /// clause that stops a coarse pass manufacturing an alibi or an eyewitness.
        /// </summary>
        private static OpportunityTerm ReadWitnesses(ActionContext context, ContextObservation mode)
        {
            if (mode == ContextObservation.OffScreen)
            {
                return Unread(
                    OpportunityFacet.Witnesses,
                    "nobody's presence was read: this can never become a witness or an alibi");
            }

            return Read(
                OpportunityFacet.Witnesses,
                1.0,
                "the room was read and held " + context.Witnesses.Count + " other(s)");
        }

        /// <summary>
        /// Whether it could be done unseen, which only a crime asks about and only an observed
        /// reading can answer.
        /// </summary>
        private static OpportunityTerm ReadPrivacy(
            NarrativeAction action, ActionContext context, ContextObservation mode)
        {
            if (action.Family != ActionFamily.Crime)
            {
                return Unread(OpportunityFacet.Privacy, "this is not something that needs to be unseen");
            }

            if (mode == ContextObservation.OffScreen)
            {
                return Unread(OpportunityFacet.Privacy, "whether they were alone: unread, counted for nothing");
            }

            if (context.Witnesses.Count == 0)
            {
                return Read(OpportunityFacet.Privacy, 1.0, "the room was read and nobody else was in it");
            }

            return Read(
                OpportunityFacet.Privacy,
                0.7,
                context.Witnesses.Count + " other(s) were read in the room, and this wants to be unseen");
        }

        /// <summary>
        /// Whether the other party's hands are full of something worse.
        ///
        /// The other party only: the acting character's own combat is already the whole of their
        /// <see cref="ReadActivity"/> reading, and counting it twice would make a brawl a
        /// different number here than it is in the autonomy pass.
        /// </summary>
        private static OpportunityTerm ReadDanger(IVanillaState vanilla, ActionContext context)
        {
            if (context.Target.IsNone)
            {
                return Unread(OpportunityFacet.Danger, "nobody else is named in this attempt");
            }

            ActivityFamily theirs = vanilla.GetActorActivity(context.Target).CurrentActivity;
            if (theirs == ActivityFamily.Unknown)
            {
                return Unread(
                    OpportunityFacet.Danger, "what the other party is doing: unread, counted for nothing");
            }

            return theirs == ActivityFamily.Combat
                ? Read(OpportunityFacet.Danger, 0.15, "the other party is in combat")
                : Read(OpportunityFacet.Danger, 1.0, "the other party is not fighting anybody");
        }

        /// <summary>
        /// How long it has been since the matter last moved.
        ///
        /// The facet none of the activity reads answers: vanilla says what somebody is doing this
        /// minute, and whether they could have found an hour for this in the last week is a
        /// different question with a different source.
        /// </summary>
        private static OpportunityTerm ReadElapsedTime(ActionContext context)
        {
            if (context.Thread == null)
            {
                return Unread(OpportunityFacet.ElapsedTime, "no matter to have had time in");
            }

            long days = context.Now.DaysSince(context.Thread.LastAdvancedAt);
            if (days < 0)
            {
                return Unread(OpportunityFacet.ElapsedTime, "the matter's clock does not read: counted for nothing");
            }

            if (days >= 7)
            {
                return Read(
                    OpportunityFacet.ElapsedTime, 1.0, days + " days since the matter last moved: time enough");
            }

            return days >= 1
                ? Read(OpportunityFacet.ElapsedTime, 0.9, days + " day(s) since the matter last moved")
                : Read(OpportunityFacet.ElapsedTime, 0.75, "the matter moved today; there has hardly been time");
        }

        // -- term plumbing -----------------------------------------------------------------

        private static void Bar(List<OpportunityTerm> terms, ref string refusal, OpportunityTerm term)
        {
            terms.Add(term);
            if (term.Barring && refusal.Length == 0)
            {
                refusal = term.Observation;
            }
        }

        private static OpportunityTerm Read(OpportunityFacet facet, double weight, string observation)
        {
            return new OpportunityTerm(facet, true, weight, observation, false);
        }

        private static OpportunityTerm Unread(OpportunityFacet facet, string observation)
        {
            return new OpportunityTerm(facet, false, 1.0, observation, false);
        }

        private static OpportunityTerm Barred(OpportunityFacet facet, string observation)
        {
            return new OpportunityTerm(facet, true, 0.0, observation, true);
        }
    }
}
