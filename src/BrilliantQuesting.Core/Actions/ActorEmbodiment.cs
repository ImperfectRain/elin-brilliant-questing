using System.Collections.Generic;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// What a verb needs from the body of whoever performs it (BQ-093, `D021`).
    ///
    /// The master rule is that vanilla owns embodiment and BQ owns narrative meaning, and the
    /// roadmap's done-when asks that a verb needing a body either delegate to a verified vanilla
    /// path or resolve coarsely, with the choice visible. This is that choice, declared once by
    /// the verb rather than re-derived per caller - which is what makes it the same answer for
    /// the player and for an NPC.
    /// </summary>
    public enum EmbodimentMode
    {
        /// <summary>
        /// Nothing physical is claimed. The outcome is knowledge, belief, relationship,
        /// obligation, record - things Elin has no place to put and BQ owns outright. There is no
        /// body to delegate and nothing to resolve coarsely.
        /// </summary>
        Narrative,

        /// <summary>
        /// The physical half is carried by a write vanilla actually performs through the seam -
        /// an object changing hands, money moving, a resident joining the roll. The verb asks and
        /// observes the answer; it never asserts the write happened.
        /// </summary>
        Delegated,

        /// <summary>
        /// The outcome is physical and no verified vanilla path carries it. It resolves coarsely:
        /// the record says what was attempted and what it meant, and claims no position, no route
        /// and no moment (`VS 3.2`). Core acquires no movement, pathfinding or routine-task logic
        /// on this branch, which is exactly why it is a declared branch rather than a silent one.
        /// </summary>
        Coarse
    }

    /// <summary>
    /// One verb's embodiment declaration: which of the three branches it takes, what on the live
    /// build it leans on, and how well that is evidenced.
    ///
    /// It reuses <see cref="RouteEvidence"/> and <see cref="SpatialRouteClaim.CanLeanOn"/> rather
    /// than growing a second grading vocabulary. BQ-090 already had to answer "may this build be
    /// promised this?" for spatial routes, and "may this actor's body be asked for this?" is the
    /// same question about a different primitive - so it gets the same four grades, the same
    /// capability list and the same gate.
    /// </summary>
    public sealed class ActorEmbodiment
    {
        private static readonly IReadOnlyList<VanillaCapability> Nothing = new VanillaCapability[0];

        private ActorEmbodiment(
            EmbodimentMode mode,
            string leansOn,
            RouteEvidence evidence,
            IReadOnlyList<VanillaCapability> needs)
        {
            Mode = mode;
            LeansOn = leansOn ?? string.Empty;
            Evidence = evidence;
            Needs = needs ?? Nothing;
        }

        /// <summary>The default: this verb moves no bodies and claims no physical detail.</summary>
        public static readonly ActorEmbodiment Narrative =
            new ActorEmbodiment(EmbodimentMode.Narrative, string.Empty, RouteEvidence.BqAuthored, Nothing);

        /// <summary>
        /// The physical half goes through the seam. <paramref name="leansOn"/> names the write and
        /// <paramref name="needs"/> the capabilities the adapter must advertise for it.
        /// </summary>
        public static ActorEmbodiment Delegated(string leansOn, params VanillaCapability[] needs)
        {
            return new ActorEmbodiment(
                EmbodimentMode.Delegated,
                leansOn,
                RouteEvidence.SourceObserved,
                new List<VanillaCapability>(needs ?? new VanillaCapability[0]).AsReadOnly());
        }

        /// <summary>
        /// No verified vanilla path carries this, so it resolves coarsely.
        /// <paramref name="notClaimed"/> names the physical detail the resolution deliberately
        /// does not assert, because that is the sentence a reader of the inspector needs.
        /// </summary>
        public static ActorEmbodiment Coarse(string notClaimed)
        {
            return new ActorEmbodiment(EmbodimentMode.Coarse, notClaimed, RouteEvidence.BqAuthored, Nothing);
        }

        public EmbodimentMode Mode { get; }

        /// <summary>
        /// For <see cref="EmbodimentMode.Delegated"/>, the vanilla write. For
        /// <see cref="EmbodimentMode.Coarse"/>, what is deliberately not claimed. Empty otherwise.
        /// </summary>
        public string LeansOn { get; }

        public RouteEvidence Evidence { get; }

        /// <summary>Capabilities the adapter must advertise before the delegated half can be asked for.</summary>
        public IReadOnlyList<VanillaCapability> Needs { get; }

        /// <summary>
        /// Whether this build can carry the physical half, and if not, which part is missing.
        ///
        /// Narrative and coarse verbs need nothing of a build and always answer true - a coarse
        /// resolution is not a degraded delegation, it is a different and complete answer. A
        /// delegated one goes through the shared BQ-090 gate, so a build that cannot transfer
        /// items refuses in the same words a route on the same capability would.
        /// </summary>
        public bool CanEmbody(IVanillaState vanilla, out string refusal)
        {
            if (Mode != EmbodimentMode.Delegated)
            {
                refusal = string.Empty;
                return true;
            }

            return SpatialRouteClaim.CanLeanOn(vanilla, Evidence, LeansOn, Needs, out refusal);
        }

        /// <summary>One line for the inspector, naming the branch and what it rests on.</summary>
        public string Describe()
        {
            switch (Mode)
            {
                case EmbodimentMode.Delegated:
                    return "embodiment delegated to vanilla: " + LeansOn;
                case EmbodimentMode.Coarse:
                    return "embodiment resolved coarsely; not claimed: " + LeansOn;
                default:
                    return "no embodiment: nothing physical is claimed";
            }
        }

        public override string ToString() => Describe();
    }
}
