using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Autonomy
{
    /// <summary>
    /// How plausible it is that this actor could get at this matter at all, read from vanilla's
    /// own answer about what they are doing (BQ-094, BQ-135).
    ///
    /// The matter-sized half of the question, asked before any verb or context exists, which is
    /// why it survives BQa-014 rather than being replaced by it. What it is <em>not</em> is a
    /// second opinion about the same facets: the activity and routine weights come from
    /// <see cref="ActionOpportunity"/>'s readers, and the attempt-sized reading - co-location,
    /// witnesses, the object, the other party - belongs to <see cref="ActionOpportunity"/> alone.
    ///
    /// It is a <em>weight</em> and almost never a gate, which is the roadmap's own instruction and
    /// also the only honest shape. Elin answers what somebody is doing right now; whether they
    /// could have found an hour for a debt this week is not a question any of those facets asks.
    /// So a reading that is bad for the actor lowers the number and a reading nobody could take
    /// makes it zero, and everything else contributes nothing at all rather than a default.
    ///
    /// The one hard refusal is the one the seam can actually answer: an actor vanilla is already
    /// carrying between zones is not available to a BQ intention (`VS 3.3`). It is refused by the
    /// context builder rather than here, so this can be read for anybody.
    ///
    /// <b>What it may never become.</b> Co-location, a shared timetable and a shared workplace are
    /// opportunity and nothing more (`VS 5.4`). They raise or lower the chance that somebody tried
    /// something; they never produce eyewitness testimony, proof, an exact location claim or
    /// recognition of a person, and nothing on this reading is written to the ledger. The
    /// observation itself is transient and is never persisted (`D004`, `D005`).
    /// </summary>
    public sealed class InterventionOpportunity
    {
        /// <summary>
        /// What an unread build is worth: neither the encouragement of a confirmed opening nor
        /// the discouragement of a confirmed obstacle.
        ///
        /// Deliberately above <see cref="AutonomousInterventions.OpportunityFloor"/>'s default, so
        /// that a build answering no activity facets at all still lets the world act. Reading
        /// silence as "nobody is available" would switch autonomy off on exactly the builds whose
        /// seam is least proven, which is a failure that looks like a design decision (`D017`).
        /// </summary>
        public const double Unread = 0.6;

        private readonly List<string> _terms;

        private InterventionOpportunity(EntityId actor, double plausibility, List<string> terms)
        {
            Actor = actor;
            Plausibility = plausibility < 0.0 ? 0.0 : plausibility > 1.0 ? 1.0 : plausibility;
            _terms = terms;
        }

        public EntityId Actor { get; }

        /// <summary>0..1. Higher is a better chance this actor could have got at the matter.</summary>
        public double Plausibility { get; }

        /// <summary>
        /// Every term that moved the number, including the ones that moved it by nothing.
        ///
        /// A named zero is the point: an inspector has to be able to tell "the build did not
        /// answer where they are" from "they are elsewhere", and the two are the same number
        /// (`D017`).
        /// </summary>
        public IReadOnlyList<string> Terms => _terms;

        /// <summary>
        /// Reads <paramref name="vanilla"/>'s activity snapshot for <paramref name="actor"/> and
        /// weighs it against where the matter is.
        ///
        /// <paramref name="matterZone"/> may be <see cref="EntityId.None"/> - plenty of matters
        /// are about a person rather than a place, and a matter with no place is not thereby
        /// somewhere else.
        /// </summary>
        public static InterventionOpportunity Read(IVanillaState vanilla, EntityId actor, EntityId matterZone)
        {
            List<string> terms = new List<string>();
            if (vanilla == null || actor.IsNone)
            {
                terms.Add("nobody to read: 0");
                return new InterventionOpportunity(actor, 0.0, terms);
            }

            ActorActivity activity = vanilla.GetActorActivity(actor);
            double plausibility = Unread;

            EntityId actorZone = activity.CurrentZone.IsNone ? vanilla.GetZoneOf(actor) : activity.CurrentZone;
            if (actorZone.IsNone || matterZone.IsNone)
            {
                terms.Add("whereabouts against the matter's place: unread, counted for nothing");
            }
            else if (actorZone == matterZone)
            {
                plausibility = 1.0;
                terms.Add("in the same place as the matter: opportunity only, never evidence");
            }
            else
            {
                plausibility = 0.4;
                terms.Add("somewhere else: still possible, and only coarsely");
            }

            // What they are doing and which stretch of their day it is are read through the
            // BQa-014 facet readers rather than weighed again here, so that "they are asleep"
            // cannot be worth one number to an intervention and another to the attempt that
            // intervention then makes.
            OpportunityTerm doing = ActionOpportunity.ReadActivity(activity);
            plausibility *= doing.Weight;
            terms.Add(doing.Observation);

            OpportunityTerm routine = ActionOpportunity.ReadRoutine(activity);
            plausibility *= routine.Weight;
            terms.Add(routine.Observation);

            return new InterventionOpportunity(actor, plausibility, terms);
        }

        /// <summary>
        /// The same matter-sized weight, narrowed by what the attempt-sized reading found
        /// (BQa-014).
        ///
        /// The two are about different things and both are true at once: this one asks whether
        /// the person could have got at the matter this week, and <paramref name="attempt"/> asks
        /// whether the world allowed the particular act on the particular day. Multiplying them
        /// keeps the coarse reading honest - an eligible act in a place the save keeps them both
        /// in still scores below one, because a shared zone is a chance to have crossed paths and
        /// never a meeting. Every term of the narrower reading is carried across verbatim, so a
        /// trace still names the observation behind each move of the number.
        /// </summary>
        public InterventionOpportunity Refined(ActionOpportunity attempt)
        {
            if (attempt == null)
            {
                return this;
            }

            List<string> terms = new List<string>(_terms);
            for (int i = 0; i < attempt.Terms.Count; i++)
            {
                terms.Add(attempt.Terms[i].ToString());
            }

            return new InterventionOpportunity(Actor, Plausibility * attempt.Plausibility, terms);
        }

        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("opportunity ").Append(Plausibility.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            for (int i = 0; i < _terms.Count; i++)
            {
                sb.Append("\n  - ").Append(_terms[i]);
            }

            return sb.ToString();
        }

        public override string ToString() => Describe();
    }
}
