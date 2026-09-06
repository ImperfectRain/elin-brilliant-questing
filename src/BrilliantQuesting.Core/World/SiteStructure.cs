using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// What one route between two parts of a place physically is.
    ///
    /// Derived from the requirements the plan already carries rather than authored a second time:
    /// a route the grammar called a locked barrier is a door with a lock in it, and one it called
    /// nothing is an opening. There is no kind here that some affordance does not name, because a
    /// connector this vocabulary could invent would be a physical claim the plan never made.
    /// </summary>
    public enum SiteConnectorKind
    {
        /// <summary>Nothing in the way. Most of what a place is made of.</summary>
        Passage,

        /// <summary>Somebody stands here deciding who passes.</summary>
        GuardedGate,

        /// <summary>Shut, and somebody holds the key.</summary>
        LockedDoor,

        /// <summary>Fallen stone. It can be moved at a cost.</summary>
        RubbleFall,

        /// <summary>Not a way until somebody makes one.</summary>
        DugBypass,

        /// <summary>Up and out, and not the way anybody came in.</summary>
        Shaft,

        /// <summary>There, and not apparent until it is found.</summary>
        HiddenWay
    }

    /// <summary>One authored piece, put down somewhere, standing for one part of the plan.</summary>
    public sealed class SitePlacement
    {
        internal SitePlacement(SiteLayoutNode node, SitePiece piece, int x, int y, int depth)
        {
            Node = node;
            Piece = piece;
            X = x;
            Y = y;
            Depth = depth;
        }

        public SiteLayoutNode Node { get; }

        public string NodeId => Node.Id;

        public SitePiece Piece { get; }

        public int X { get; }

        public int Y { get; }

        /// <summary>
        /// How many ways in from outside this part is: the mine's descent, counted rather than
        /// authored. The mouth is one, what the mouth opens onto is two.
        /// </summary>
        public int Depth { get; }

        public int Right => X + Piece.Width;

        public int Bottom => Y + Piece.Height;

        public bool Overlaps(SitePlacement other)
        {
            return other != null
                   && X < other.Right && other.X < Right
                   && Y < other.Bottom && other.Y < Bottom;
        }

        public override string ToString()
        {
            return NodeId + " = " + Piece.Id + " at " + X + "," + Y + " depth " + Depth;
        }
    }

    /// <summary>One realized way between two placed pieces, or out of the place entirely.</summary>
    public sealed class SiteConnector
    {
        internal SiteConnector(SiteLayoutRoute route, SiteConnectorKind kind, bool passable, string refusal)
        {
            Route = route;
            Kind = kind;
            Passable = passable;
            Refusal = refusal ?? string.Empty;
        }

        public SiteLayoutRoute Route { get; }

        public string From => Route.From;

        public string To => Route.To;

        public SiteConnectorKind Kind { get; }

        /// <summary>
        /// Whether anybody can get through it on this build (BQ-090's judgement of the same route).
        ///
        /// A rockfall is still stone on a build that cannot answer it; what changes is whether the
        /// place can be walked through it, and that is the only thing reachability may count.
        /// </summary>
        public bool Passable { get; }

        /// <summary>Why nobody can get through it. Empty where somebody can.</summary>
        public string Refusal { get; }

        public bool IsEntry => Route.IsEntry;

        public bool IsExit => Route.IsExit;

        public bool NeedsAdmission => Route.NeedsAdmission;

        public override string ToString()
        {
            return From + " -> " + To + " (" + Kind + (Passable ? ")" : ", shut: " + Refusal + ")");
        }
    }

    public enum SiteAnchorKind
    {
        Occupant,
        Cargo
    }

    /// <summary>
    /// Somebody or something the matter left here, and the part of the place it is in.
    ///
    /// The physical half of BQ-091's derivation: contents say what is here and why, and this says
    /// where. Nothing is placed at a coordinate, because vanilla owns embodiment (`D021`) - the
    /// anchor names the piece the game should put them in, and the game decides which tile.
    /// </summary>
    public sealed class SiteAnchor
    {
        internal SiteAnchor(EntityId id, SiteAnchorKind kind, string nodeId, string why)
        {
            Id = id;
            Kind = kind;
            NodeId = nodeId ?? string.Empty;
            Why = why ?? string.Empty;
        }

        public EntityId Id { get; }

        public SiteAnchorKind Kind { get; }

        public string NodeId { get; }

        /// <summary>What the simulation recorded that puts them here.</summary>
        public string Why { get; }

        public override string ToString() => Kind + " " + Id.Value + " in " + NodeId;
    }

    /// <summary>Something the plan asked for that the place does not physically have, and why not.</summary>
    public sealed class SiteStructureOmission
    {
        internal SiteStructureOmission(string what, bool optional, string reason)
        {
            What = what ?? string.Empty;
            Optional = optional;
            Reason = reason ?? string.Empty;
        }

        /// <summary>The node id or the route it stood for.</summary>
        public string What { get; }

        /// <summary>Whether the kind of place can be without it. A required part is never omitted.</summary>
        public bool Optional { get; }

        public string Reason { get; }

        public override string ToString() => What + ": " + Reason;
    }

    /// <summary>
    /// BQ-140. The physical shape of one place: which authored piece stands for each part of the
    /// plan, where each one is, what joins them, and where what the matter left is.
    ///
    /// This is the only representation in the repository that has coordinates in it, and it is
    /// deliberately not a map: no tiles, no doors, no objects. It is what an adapter needs to
    /// build the place out of authored pieces, and what a validator needs to prove the place can
    /// be walked before anything is built. The plan stays authoritative for meaning; this is its
    /// embodiment (`PP §3`).
    ///
    /// Nothing here is written to a save. A structure is derived from the grammar, the seed and
    /// the errand, all three of which the site already records, so a place reads back the same
    /// shape it was built with and Elin keeps the map it actually made (`PP §6`).
    /// </summary>
    public sealed class SiteStructure
    {
        private readonly Dictionary<string, SitePlacement> _byNode =
            new Dictionary<string, SitePlacement>(StringComparer.Ordinal);

        internal SiteStructure(
            string family,
            SiteLayout layout,
            SiteAffordance objective,
            string objectiveNodeId,
            IEnumerable<SitePlacement> placements,
            IEnumerable<SiteConnector> connectors,
            IEnumerable<SiteAnchor> anchors,
            IEnumerable<SiteStructureOmission> omitted)
        {
            Family = family ?? string.Empty;
            Layout = layout;
            Objective = objective;
            ObjectiveNodeId = objectiveNodeId ?? string.Empty;
            Placements = new List<SitePlacement>(placements ?? new SitePlacement[0]).AsReadOnly();
            Connectors = new List<SiteConnector>(connectors ?? new SiteConnector[0]).AsReadOnly();
            Anchors = new List<SiteAnchor>(anchors ?? new SiteAnchor[0]).AsReadOnly();
            Omitted = new List<SiteStructureOmission>(omitted ?? new SiteStructureOmission[0]).AsReadOnly();

            int width = 0;
            int height = 0;
            for (int i = 0; i < Placements.Count; i++)
            {
                SitePlacement placement = Placements[i];
                _byNode[placement.NodeId] = placement;
                width = Math.Max(width, placement.Right);
                height = Math.Max(height, placement.Bottom);
            }

            Width = width;
            Height = height;
        }

        public string Family { get; }

        /// <summary>The abstract plan this is the body of.</summary>
        public SiteLayout Layout { get; }

        public string GrammarId => Layout == null ? string.Empty : Layout.GrammarId;

        public ulong Seed => Layout == null ? 0UL : Layout.Seed;

        /// <summary>What the matter came here for.</summary>
        public SiteAffordance Objective { get; }

        public string ObjectiveNodeId { get; }

        public IReadOnlyList<SitePlacement> Placements { get; }

        public IReadOnlyList<SiteConnector> Connectors { get; }

        public IReadOnlyList<SiteAnchor> Anchors { get; }

        public IReadOnlyList<SiteStructureOmission> Omitted { get; }

        /// <summary>How much room the whole place takes. Bounded by construction.</summary>
        public int Width { get; }

        public int Height { get; }

        public SitePlacement PlacementOf(string nodeId)
        {
            SitePlacement placement;
            return nodeId != null && _byNode.TryGetValue(nodeId, out placement) ? placement : null;
        }

        public bool Has(string nodeId) => nodeId != null && _byNode.ContainsKey(nodeId);

        /// <summary>
        /// Every part somebody could actually get to from outside, walking only connectors this
        /// build can take them through.
        /// </summary>
        public HashSet<string> Walkable()
        {
            HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < Connectors.Count; i++)
                {
                    SiteConnector connector = Connectors[i];
                    if (connector.Passable && reached.Contains(connector.From) && reached.Add(connector.To))
                    {
                        grew = true;
                    }
                }
            }

            return reached;
        }

        /// <summary>
        /// The shape of the problem this place sets, with the scenery left out.
        ///
        /// Two structures with the same signature are the same navigation problem however
        /// differently they are built; two with different signatures are different problems. That
        /// is the distinction BQ-140's done-when turns on, so it is computed rather than asserted -
        /// and piece ids are deliberately absent from it, because a different gallery in the same
        /// graph is decoration and must not be allowed to count as variety.
        /// </summary>
        public SiteTopology Topology()
        {
            return SiteTopology.Of(this);
        }

        public override string ToString()
        {
            return Family + " " + GrammarId + " seed " + Seed + ": " + Placements.Count + " pieces, "
                   + Connectors.Count + " ways";
        }
    }

    /// <summary>
    /// The navigation and problem structure of one realized place, as something comparable.
    ///
    /// Everything here is counted off the walkable graph: which parts it has, how deep it goes,
    /// what has to be got past, how many genuinely different ways reach what the matter came for,
    /// and how many loops there are. Coordinates and piece choices are excluded on purpose -
    /// "different seeds produce different sites" has to mean the player has a different problem,
    /// not that the same corridor was drawn in another room.
    /// </summary>
    public sealed class SiteTopology
    {
        private SiteTopology(
            IReadOnlyList<string> parts,
            IReadOnlyList<string> obstacles,
            int depth,
            int loops,
            int waysToObjective,
            int distinctProblems)
        {
            Parts = parts;
            Obstacles = obstacles;
            Depth = depth;
            Loops = loops;
            WaysToObjective = waysToObjective;
            DistinctProblems = distinctProblems;
        }

        /// <summary>Every part that can be walked to, in order.</summary>
        public IReadOnlyList<string> Parts { get; }

        /// <summary>Every connector that has to be got past, as "from -> to (kind)".</summary>
        public IReadOnlyList<string> Obstacles { get; }

        /// <summary>The furthest part from outside, in ways.</summary>
        public int Depth { get; }

        /// <summary>Independent loops in the walkable graph. Zero is a tree; a place with none has no loop.</summary>
        public int Loops { get; }

        /// <summary>How many distinct walks reach what the matter came for.</summary>
        public int WaysToObjective { get; }

        /// <summary>
        /// How many of those walks are a different problem: the sequence of things that have to be
        /// got past, with the plain openings left out. Two routes that ask nothing of anybody are
        /// one problem, however different the rooms.
        /// </summary>
        public int DistinctProblems { get; }

        /// <summary>The whole shape as one comparable string.</summary>
        public string Signature { get; private set; }

        internal static SiteTopology Of(SiteStructure structure)
        {
            HashSet<string> walkable = structure.Walkable();

            List<string> parts = new List<string>();
            int depth = 0;
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                if (!walkable.Contains(placement.NodeId))
                {
                    continue;
                }

                parts.Add(placement.NodeId);
                depth = Math.Max(depth, placement.Depth);
            }

            parts.Sort(StringComparer.Ordinal);

            List<string> obstacles = new List<string>();
            int edges = 0;
            for (int i = 0; i < structure.Connectors.Count; i++)
            {
                SiteConnector connector = structure.Connectors[i];
                if (!connector.Passable || !walkable.Contains(connector.From) || !walkable.Contains(connector.To))
                {
                    continue;
                }

                edges++;
                if (connector.Kind != SiteConnectorKind.Passage)
                {
                    obstacles.Add(connector.From + " -> " + connector.To + " (" + connector.Kind + ")");
                }
            }

            obstacles.Sort(StringComparer.Ordinal);

            // Cyclomatic number over the walkable graph, counting outside as one of its parts. A
            // way in and a way out that both reach the same place is the loop `LW §7.6` asks for.
            int loops = Math.Max(0, edges - (parts.Count + 1) + 1);

            List<string> problems = new List<string>();
            int ways = CountWays(structure, walkable, problems);
            problems.Sort(StringComparer.Ordinal);

            SiteTopology topology = new SiteTopology(
                parts.AsReadOnly(), obstacles.AsReadOnly(), depth, loops, ways, Distinct(problems));

            StringBuilder signature = new StringBuilder();
            signature.Append("parts[").Append(string.Join(",", parts.ToArray())).Append(']');
            signature.Append(" past[").Append(string.Join(",", obstacles.ToArray())).Append(']');
            signature.Append(" depth ").Append(depth);
            signature.Append(" loops ").Append(loops);
            signature.Append(" ways ").Append(ways);
            signature.Append(" problems[").Append(string.Join(" | ", problems.ToArray())).Append(']');
            topology.Signature = signature.ToString();
            return topology;
        }

        private static int CountWays(SiteStructure structure, HashSet<string> walkable, List<string> problems)
        {
            if (structure.ObjectiveNodeId.Length == 0 || !walkable.Contains(structure.ObjectiveNodeId))
            {
                return 0;
            }

            List<string> path = new List<string>();
            HashSet<string> walked = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };
            int[] ways = new int[1];
            Walk(structure, walkable, SiteGrammar.Outside, path, walked, ways, problems);
            return ways[0];
        }

        private static void Walk(
            SiteStructure structure,
            HashSet<string> walkable,
            string from,
            List<string> path,
            HashSet<string> walked,
            int[] ways,
            List<string> problems)
        {
            for (int i = 0; i < structure.Connectors.Count; i++)
            {
                SiteConnector connector = structure.Connectors[i];
                if (!connector.Passable
                    || !string.Equals(connector.From, from, StringComparison.Ordinal)
                    || !walkable.Contains(connector.To)
                    || !walked.Add(connector.To))
                {
                    continue;
                }

                path.Add(connector.Kind == SiteConnectorKind.Passage ? null : connector.Kind.ToString());

                if (string.Equals(connector.To, structure.ObjectiveNodeId, StringComparison.Ordinal))
                {
                    ways[0]++;
                    problems.Add(Problem(path));
                }
                else
                {
                    Walk(structure, walkable, connector.To, path, walked, ways, problems);
                }

                path.RemoveAt(path.Count - 1);
                walked.Remove(connector.To);
            }
        }

        /// <summary>What a walk actually asks of somebody: everything that is not just an opening.</summary>
        private static string Problem(IReadOnlyList<string> path)
        {
            List<string> asked = new List<string>();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != null)
                {
                    asked.Add(path[i]);
                }
            }

            return asked.Count == 0 ? "walk in" : string.Join(" then ", asked.ToArray());
        }

        private static int Distinct(List<string> problems)
        {
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < problems.Count; i++)
            {
                unique.Add(problems[i]);
            }

            return unique.Count;
        }

        public override string ToString() => Signature;
    }
}
