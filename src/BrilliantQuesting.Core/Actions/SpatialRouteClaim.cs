using System.Collections.Generic;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// How well the thing a route leans on is evidenced on the live build (BQ-090's evidence
    /// gate).
    ///
    /// The four grades are the repository's own, and the vocabulary the verification record
    /// already uses (`docs/elin/verification/matrix.md`): what a running game has done, what was
    /// read in the installed assemblies, what is only a symbol, and what this mod answers out of
    /// its own state. A grade is a record of what is known, never a prediction: a route is
    /// promised because something can be asked or because nothing needs to be, not because a
    /// primitive is likely to work.
    /// </summary>
    public enum RouteEvidence
    {
        /// <summary>
        /// Nothing on the live build has to exist for it. The obstacle is this mod's own state
        /// and the roll is the portable resolver, so the route works the same on every build.
        /// </summary>
        BqAuthored,

        /// <summary>Leans on a read or write a running game has exercised.</summary>
        RuntimeVerified,

        /// <summary>
        /// Leans on something read in the installed assemblies and never exercised in play.
        /// </summary>
        SourceObserved,

        /// <summary>Leans on a symbol found in metadata and nothing more.</summary>
        MetadataOnly
    }

    /// <summary>
    /// BQ-090. What one registered verb claims about being a way through part of a place: which
    /// spatial requirement it answers, what it leans on from the live build, and how well that is
    /// evidenced.
    ///
    /// A claim is made by the verb rather than kept in a table beside it, for the reason the
    /// content loader derives the registered verb ids rather than listing them: a hand-kept copy
    /// is the thing that lets a place promise a way through nobody built. It is deliberately not
    /// <see cref="NarrativeAction.GetAvailability"/> - availability answers "can this be tried
    /// here, now, against this world", and an abstract plan has no barrier object, no occupant and
    /// no zone yet. This answers the earlier question: is there any build on which this route
    /// could be offered at all.
    /// </summary>
    public sealed class SpatialRouteClaim
    {
        public SpatialRouteClaim(
            IEnumerable<SiteAffordance> answers,
            RouteEvidence evidence,
            string leansOn,
            params VanillaCapability[] needs)
        {
            Answers = new List<SiteAffordance>(answers ?? new SiteAffordance[0]).AsReadOnly();
            Evidence = evidence;
            LeansOn = leansOn ?? string.Empty;
            Needs = new List<VanillaCapability>(needs ?? new VanillaCapability[0]).AsReadOnly();
        }

        /// <summary>
        /// The spatial requirements this verb answers. A place saying a way through is a locked
        /// barrier is saying what it is like; this says who can do anything about it.
        /// </summary>
        public IReadOnlyList<SiteAffordance> Answers { get; }

        public RouteEvidence Evidence { get; }

        /// <summary>What on the live build the route leans on, named. Empty when it leans on nothing.</summary>
        public string LeansOn { get; }

        /// <summary>
        /// The capabilities the adapter must advertise for the route to be offerable. The
        /// adapter's probe is a live exercise, so a capability the build advertises settles the
        /// question <see cref="Evidence"/> can only record.
        /// </summary>
        public IReadOnlyList<VanillaCapability> Needs { get; }

        /// <summary>Whether this verb answers that requirement.</summary>
        public bool Covers(SiteAffordance affordance)
        {
            for (int i = 0; i < Answers.Count; i++)
            {
                if (Answers[i] == affordance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether this build may be offered the route, and if not, which part of it is missing.
        ///
        /// Two rules, in this order. A capability the verb needs and the build does not advertise
        /// refuses the route, because the adapter has been asked and said no. With nothing left to
        /// ask, the recorded grade decides: a route leaning on something never exercised is not
        /// promised, which is the whole of "do not promise a route whose Elin primitive has not
        /// passed the appropriate evidence level".
        ///
        /// A null <paramref name="vanilla"/> is not a build that supports everything: nobody has
        /// said, so every capability the verb needs is unanswered and the route is refused.
        /// </summary>
        public bool CanPromise(IVanillaState vanilla, out string refusal)
        {
            for (int i = 0; i < Needs.Count; i++)
            {
                if (vanilla == null)
                {
                    refusal = "no build has said whether it can " + Needs[i];
                    return false;
                }

                if (!vanilla.Supports(Needs[i]))
                {
                    refusal = "this build cannot " + Needs[i];
                    return false;
                }
            }

            if (Needs.Count == 0 && (Evidence == RouteEvidence.SourceObserved || Evidence == RouteEvidence.MetadataOnly))
            {
                refusal = "it leans on " + (LeansOn.Length > 0 ? LeansOn : "something unverified")
                          + ", which is " + Evidence + " and nothing on this build can be asked about it";
                return false;
            }

            refusal = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Implemented by the registered verbs that actually take somebody through part of a place -
    /// which today means the verbs that end with <see cref="NarrativeSite.Admit"/>.
    ///
    /// Deliberately not implemented by every verb that could plausibly be used near a door. A
    /// route promise is a promise that taking this verb gets you through, and a verb that leaves
    /// the place exactly as shut as it was is not a way in however well it reads.
    /// </summary>
    public interface ISpatialRouteVerb
    {
        SpatialRouteClaim SpatialRoute { get; }
    }
}
