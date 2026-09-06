using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// BQ-140. One authored piece of a place: the physical thing a functional part of a plan is
    /// actually built out of.
    ///
    /// "Authored atoms, procedural composition" (`PP §2`). A piece is the smallest unit this step
    /// composes and the only place geometry is allowed to be written down - a footprint, and how
    /// many ways can meet in it. What it is *for* is still the grammar's
    /// (<see cref="SiteNodeSpec"/>); a piece only says it can fill that socket, what standing in it
    /// physically affords, and what building it leans on from the live game.
    ///
    /// Pieces are content for the same reason grammars are (`D066`): a mine gallery is a catalogue
    /// entry, and a writer adding "flooded winze" should not need a build. Nothing here invents
    /// tiles: a footprint is how much room the piece takes in the site's own bounded grid, which is
    /// what an assembler needs to know to put two of them down without one landing on the other.
    /// </summary>
    public sealed class SitePiece
    {
        public SitePiece(
            string id,
            string family,
            string socket,
            int width,
            int height,
            int ways,
            IEnumerable<SiteAffordance> provides,
            RouteEvidence evidence,
            string leansOn,
            IEnumerable<VanillaCapability> needs)
        {
            Id = id ?? string.Empty;
            Family = family ?? string.Empty;
            Socket = socket ?? string.Empty;
            Width = width;
            Height = height;
            Ways = ways;
            Provides = new List<SiteAffordance>(provides ?? new SiteAffordance[0]).AsReadOnly();
            Evidence = evidence;
            LeansOn = leansOn ?? string.Empty;
            Needs = new List<VanillaCapability>(needs ?? new VanillaCapability[0]).AsReadOnly();
        }

        public string Id { get; }

        /// <summary>
        /// The one kind of site this piece belongs to. A family is a bounded set of pieces and one
        /// assembler that knows how they go together; it is not a theme, and it is not a switch on
        /// a general dungeon builder.
        /// </summary>
        public string Family { get; }

        /// <summary>The grammar socket this piece can fill (<see cref="SiteNodeSpec.Socket"/>).</summary>
        public string Socket { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>
        /// How many ways can meet in this piece. A chamber has so many mouths, and a plan that
        /// wants a part five things lead to cannot be built out of a niche with two - which is
        /// what makes the choice of piece a structural decision rather than a change of scenery.
        /// </summary>
        public int Ways { get; }

        /// <summary>What being in this piece physically affords: a face to dig, water, a way out.</summary>
        public IReadOnlyList<SiteAffordance> Provides { get; }

        /// <summary>How well what building this piece leans on is evidenced (BQ-090's grades).</summary>
        public RouteEvidence Evidence { get; }

        /// <summary>What on the live build it leans on beyond making the place at all.</summary>
        public string LeansOn { get; }

        /// <summary>Capabilities the adapter must advertise before this piece may be built.</summary>
        public IReadOnlyList<VanillaCapability> Needs { get; }

        public bool Affords(SiteAffordance affordance)
        {
            for (int i = 0; i < Provides.Count; i++)
            {
                if (Provides[i] == affordance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Everything this part of the plan requires is something this piece is.</summary>
        public bool Answers(SiteLayoutNode node)
        {
            if (node == null)
            {
                return false;
            }

            for (int i = 0; i < node.Affordances.Count; i++)
            {
                if (!Affords(node.Affordances[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether this build may be handed this piece, and what is missing where it may not.
        ///
        /// The same gate a route promise goes through (`D067`), asked of a thing rather than of a
        /// verb: a piece leaning on something never exercised is not built, and a piece leaning on
        /// nothing beyond the site itself is built anywhere the site can be.
        /// </summary>
        public bool CanBuild(IVanillaState vanilla, out string refusal)
        {
            if (vanilla == null)
            {
                refusal = "no build has said whether it can " + VanillaCapability.BuildPlaceStructure;
                return false;
            }

            if (!vanilla.Supports(VanillaCapability.BuildPlaceStructure))
            {
                refusal = "this build cannot " + VanillaCapability.BuildPlaceStructure;
                return false;
            }

            return SpatialRouteClaim.CanLeanOn(vanilla, Evidence, LeansOn, Needs, out refusal);
        }

        public override string ToString() => Id + " [" + Socket + " " + Width + "x" + Height + ", " + Ways + " ways]";
    }

    /// <summary>
    /// Every authored piece one family of sites is built from, indexed by the socket it fills.
    ///
    /// Deliberately per-family. A catalogue that held every piece for every kind of place would be
    /// the index of a general dungeon engine, and the assembler would then have to guess which
    /// pieces belong together; a family is the boundary that keeps one kind of site's proof from
    /// silently becoming a claim about all of them.
    /// </summary>
    public sealed class SitePieceCatalogue
    {
        private readonly Dictionary<string, List<SitePiece>> _bySocket =
            new Dictionary<string, List<SitePiece>>(StringComparer.Ordinal);

        private static readonly SitePiece[] None = new SitePiece[0];

        public SitePieceCatalogue(string family, IEnumerable<SitePiece> pieces)
        {
            Family = family ?? string.Empty;

            List<SitePiece> all = new List<SitePiece>();
            if (pieces != null)
            {
                foreach (SitePiece piece in pieces)
                {
                    if (piece == null || !string.Equals(piece.Family, Family, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    all.Add(piece);

                    List<SitePiece> socket;
                    if (!_bySocket.TryGetValue(piece.Socket, out socket))
                    {
                        socket = new List<SitePiece>();
                        _bySocket[piece.Socket] = socket;
                    }

                    socket.Add(piece);
                }
            }

            Pieces = all.AsReadOnly();
        }

        public string Family { get; }

        /// <summary>Every piece, in the order the bundle carries them, so assembly is reproducible.</summary>
        public IReadOnlyList<SitePiece> Pieces { get; }

        public IReadOnlyList<SitePiece> ForSocket(string socket)
        {
            List<SitePiece> pieces;
            return socket != null && _bySocket.TryGetValue(socket, out pieces) ? pieces : (IReadOnlyList<SitePiece>)None;
        }

        public override string ToString() => Family + " pieces: " + Pieces.Count;
    }
}
