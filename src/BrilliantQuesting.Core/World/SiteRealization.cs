using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>One thing the built place was checked for, and what the check found.</summary>
    public sealed class SiteStructureCheck
    {
        internal SiteStructureCheck(string what, bool held, string detail)
        {
            What = what ?? string.Empty;
            Held = held;
            Detail = detail ?? string.Empty;
        }

        public string What { get; }

        public bool Held { get; }

        /// <summary>What was counted, or what was wrong. Never empty on a check that failed.</summary>
        public string Detail { get; }

        public override string ToString() => (Held ? "ok   " : "FAIL ") + What + ": " + Detail;
    }

    /// <summary>
    /// What came of trying to give one plan a body: the place, everything it does not have, and
    /// every reason it could not be built at all.
    /// </summary>
    public sealed class SiteRealizationResult
    {
        private static readonly string[] NoReasons = new string[0];
        private static readonly SiteStructureCheck[] NoChecks = new SiteStructureCheck[0];

        internal SiteRealizationResult(
            SiteStructure structure,
            IReadOnlyList<SiteStructureCheck> checks,
            IReadOnlyList<string> refusals)
        {
            Structure = structure;
            Checks = checks ?? NoChecks;
            Refusals = refusals ?? NoReasons;
        }

        /// <summary>The built place, or null where it was refused.</summary>
        public SiteStructure Structure { get; }

        /// <summary>Every validation the built place was put through, held or not.</summary>
        public IReadOnlyList<SiteStructureCheck> Checks { get; }

        /// <summary>Why there is no place. Empty when there is one.</summary>
        public IReadOnlyList<string> Refusals { get; }

        public bool Built => Structure != null && Refusals.Count == 0;
    }

    /// <summary>
    /// BQ-140. Gives one abstract plan a physical body out of authored pieces, and refuses to
    /// rather than build one that cannot be walked.
    ///
    /// <b>Structure comes from the plan, never from the tiles.</b> Which pieces are put down, how
    /// deep the place goes and what joins them are all read off the plan BQ-089 composed and BQ-092
    /// chose: a part of the plan gets a piece that can fill its socket, answers what it requires
    /// and has room for the ways that meet in it, and a route becomes the connector its own
    /// affordances name. Nothing is invented that the plan did not ask for, which is what makes two
    /// seeds different places rather than the same place redecorated - the plans differ first.
    ///
    /// <b>Refusing is the safe direction.</b> A piece nobody authored for a required part, a
    /// required part nothing can reach, an objective behind a connector this build cannot take
    /// anybody through: each of those refuses the whole realization, and nothing is staged, because
    /// a half-built site is in the save and a refused one is not (`BQ-087`). An <i>optional</i>
    /// part that cannot be built is dropped and said to have been dropped - never swapped for
    /// something else, because a hidden way quietly replaced by an open one is the place telling
    /// the player a lie the plan never told.
    ///
    /// <b>This is one family, not an engine.</b> The assembler knows how mine workings go together:
    /// pieces laid out by how far in they are, ways meeting in the pieces that have room for them.
    /// Another kind of site is another family with its own pieces, and the standing rule against a
    /// general random dungeon generator (`LW §7`, `PP`) is kept by not writing one.
    /// </summary>
    public static class SiteRealization
    {
        /// <summary>Room left between two pieces, so nothing is ever placed against something else.</summary>
        private const int Gap = 1;

        public static SiteRealizationResult Realize(
            string family,
            SiteLayout layout,
            SiteAffordance objective,
            SitePieceCatalogue catalogue,
            SiteContentsReading contents,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            if (layout == null)
            {
                return Refused("there is no plan to build");
            }

            if (catalogue == null || catalogue.Pieces.Count == 0)
            {
                return Refused("there are no authored pieces to build " + (family ?? "a place") + " out of");
            }

            if (!string.Equals(catalogue.Family, family, StringComparison.Ordinal))
            {
                return Refused("the " + catalogue.Family + " pieces do not build a " + family);
            }

            // The one write this step needs from the game, asked before anything is chosen. A
            // build that cannot make a place with a shape is told so here rather than halfway
            // through staging one (`ELIN-Q-0032`).
            if (vanilla == null)
            {
                return Refused("no build has said whether it can " + VanillaCapability.BuildPlaceStructure);
            }

            if (!vanilla.Supports(VanillaCapability.BuildPlaceStructure))
            {
                return Refused("this build cannot " + VanillaCapability.BuildPlaceStructure
                               + ", so a place with a physical shape cannot be made on it");
            }

            string objectiveNode = NodeAnswering(layout, objective);
            if (objectiveNode.Length == 0)
            {
                return Refused("this place has nowhere that answers " + objective);
            }

            List<SiteStructureOmission> omitted = new List<SiteStructureOmission>();
            Dictionary<string, SitePiece> chosen = new Dictionary<string, SitePiece>(StringComparer.Ordinal);
            List<string> refusals = new List<string>();

            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                present.Add(layout.Nodes[i].Id);
            }

            ChoosePieces(layout, catalogue, vanilla, present, chosen, omitted, refusals);
            if (refusals.Count > 0)
            {
                return new SiteRealizationResult(null, null, refusals.AsReadOnly());
            }

            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs = new Dictionary<SiteLayoutRoute, SiteRouteLeg>();
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                legs[layout.Routes[i]] = SiteRoutes.Leg(layout.Routes[i], actions, vanilla);
            }

            // A part nothing built leads to is not part of the place. Dropping it is the same rule
            // composition already applies to a plan (`BQ-089`), applied to what was actually made:
            // a connector this build cannot take anybody through leaves the room behind it standing
            // in stone nobody can reach, and a place must not contain one.
            DropUnwalkable(layout, present, legs, chosen, omitted, refusals);
            if (refusals.Count > 0)
            {
                return new SiteRealizationResult(null, null, refusals.AsReadOnly());
            }

            List<SitePlacement> placements = LayOut(layout, present, chosen);
            if (placements.Count != present.Count)
            {
                // Nothing may leave a plan without being said to have left it. Laying out reads the
                // same routes reachability just walked, so the two can only disagree through a bug -
                // and a part that quietly failed to be placed is exactly the silent omission the
                // rest of this refuses to make.
                refusals.Add(present.Count + " part(s) survived and " + placements.Count + " were placed");
                return new SiteRealizationResult(null, null, refusals.AsReadOnly());
            }

            List<SiteConnector> connectors = Join(layout, present, legs, omitted);
            List<SiteAnchor> anchors = Anchor(contents, present);

            SiteStructure structure = new SiteStructure(
                family, layout, objective, objectiveNode, placements, connectors, anchors, omitted);

            IReadOnlyList<SiteStructureCheck> checks = Validate(structure, layout);
            List<string> failures = new List<string>();
            for (int i = 0; i < checks.Count; i++)
            {
                if (!checks[i].Held)
                {
                    failures.Add(checks[i].What + ": " + checks[i].Detail);
                }
            }

            return failures.Count > 0
                ? new SiteRealizationResult(null, checks, failures.AsReadOnly())
                : new SiteRealizationResult(structure, checks, null);
        }

        // -- choosing what each part is built out of ----------------------------------------

        private static void ChoosePieces(
            SiteLayout layout,
            SitePieceCatalogue catalogue,
            IVanillaState vanilla,
            HashSet<string> present,
            Dictionary<string, SitePiece> chosen,
            List<SiteStructureOmission> omitted,
            List<string> refusals)
        {
            DeterministicRng stream = new DeterministicRng(layout.Seed)
                .Fork("site-piece")
                .Fork(layout.GrammarId);

            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                int ways = WaysMeetingIn(layout, node.Id);

                List<SitePiece> eligible = new List<SitePiece>();
                string why = string.Empty;
                if (!node.Spec.HasSocket)
                {
                    why = "the grammar names no authored piece for it";
                }
                else
                {
                    IReadOnlyList<SitePiece> candidates = catalogue.ForSocket(node.Socket);
                    if (candidates.Count == 0)
                    {
                        why = "nothing is authored for socket " + node.Socket;
                    }

                    for (int c = 0; c < candidates.Count; c++)
                    {
                        SitePiece piece = candidates[c];
                        string refusal;
                        if (!piece.Answers(node))
                        {
                            why = piece.Id + " does not afford what " + node.Id + " requires";
                        }
                        else if (piece.Ways < ways)
                        {
                            why = piece.Id + " has " + piece.Ways + " ways and " + ways + " meet in " + node.Id;
                        }
                        else if (!piece.CanBuild(vanilla, out refusal))
                        {
                            why = piece.Id + ": " + refusal;
                        }
                        else
                        {
                            eligible.Add(piece);
                        }
                    }
                }

                if (eligible.Count == 0)
                {
                    if (node.Required)
                    {
                        // Every place of this kind has this part, so a place without one is not a
                        // place of this kind. There is nothing to degrade to.
                        refusals.Add("nothing can be built for " + node.Id + " and every "
                                     + layout.GrammarId + " has one; " + why);
                    }
                    else
                    {
                        present.Remove(node.Id);
                        omitted.Add(new SiteStructureOmission(node.Id, true, why));
                    }

                    continue;
                }

                chosen[node.Id] = eligible[stream.Fork(node.Id).NextInt(eligible.Count)];
            }
        }

        /// <summary>
        /// How many ways the plan brings to this part, counted over the whole composition rather
        /// than over what survives realization.
        ///
        /// Deliberately the conservative count: a part whose optional neighbour is later dropped
        /// ends up in a piece with a way to spare, which is a room slightly larger than it needed
        /// to be. Counting the other way round could choose a piece that the finished place then
        /// carries too many ways for, and the validator would refuse a mine that was fine.
        /// </summary>
        private static int WaysMeetingIn(SiteLayout layout, string nodeId)
        {
            int ways = 0;
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                if (string.Equals(route.From, nodeId, StringComparison.Ordinal)
                    || string.Equals(route.To, nodeId, StringComparison.Ordinal))
                {
                    ways++;
                }
            }

            return ways;
        }

        // -- dropping what cannot be reached once it is built --------------------------------

        private static void DropUnwalkable(
            SiteLayout layout,
            HashSet<string> present,
            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs,
            Dictionary<string, SitePiece> chosen,
            List<SiteStructureOmission> omitted,
            List<string> refusals)
        {
            while (true)
            {
                HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = 0; i < layout.Routes.Count; i++)
                    {
                        SiteLayoutRoute route = layout.Routes[i];
                        SiteRouteLeg leg = legs[route];
                        if (!Standing(route.From, present) || !Standing(route.To, present) || !leg.Promised)
                        {
                            continue;
                        }

                        if (reached.Contains(route.From) && reached.Add(route.To))
                        {
                            grew = true;
                        }
                    }
                }

                SiteLayoutNode stranded = null;
                for (int i = 0; i < layout.Nodes.Count; i++)
                {
                    SiteLayoutNode node = layout.Nodes[i];
                    if (present.Contains(node.Id) && !reached.Contains(node.Id))
                    {
                        stranded = node;
                        break;
                    }
                }

                if (stranded == null)
                {
                    return;
                }

                string why = WhyNothingReaches(layout, stranded.Id, present, legs);
                if (stranded.Required)
                {
                    refusals.Add("nothing built can reach " + stranded.Id + " and every "
                                 + layout.GrammarId + " has one; " + why);
                    return;
                }

                present.Remove(stranded.Id);
                chosen.Remove(stranded.Id);
                omitted.Add(new SiteStructureOmission(stranded.Id, true, why));
            }
        }

        private static string WhyNothingReaches(
            SiteLayout layout,
            string nodeId,
            HashSet<string> present,
            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs)
        {
            List<string> reasons = new List<string>();
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                if (!string.Equals(route.To, nodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Standing(route.From, present))
                {
                    reasons.Add(route.From + " is not here");
                }
                else if (!legs[route].Promised)
                {
                    reasons.Add("the way from " + route.From + " is shut: " + legs[route].Refusal);
                }
            }

            return reasons.Count == 0
                ? "nothing in the plan leads to it"
                : string.Join("; ", reasons.ToArray());
        }

        // -- putting the pieces down ---------------------------------------------------------

        /// <summary>
        /// Pieces laid out by how far into the place they are: everything one way in from outside
        /// side by side, everything two ways in behind them, and so on.
        ///
        /// That is the mine's own shape - a descent, deepening away from the mouth - and it is
        /// where the bounded grid comes from. Two pieces are never placed on top of each other
        /// because a column is only ever as wide as its widest piece, which the validator then
        /// checks rather than assumes.
        /// </summary>
        private static List<SitePlacement> LayOut(
            SiteLayout layout,
            HashSet<string> present,
            Dictionary<string, SitePiece> chosen)
        {
            Dictionary<string, int> depth = Depths(layout, present);

            List<SiteLayoutNode> standing = new List<SiteLayoutNode>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                if (present.Contains(layout.Nodes[i].Id))
                {
                    standing.Add(layout.Nodes[i]);
                }
            }

            List<SitePlacement> placements = new List<SitePlacement>();
            int x = 0;
            int level = 1;
            while (true)
            {
                int y = 0;
                int widest = 0;
                bool any = false;
                for (int i = 0; i < standing.Count; i++)
                {
                    SiteLayoutNode node = standing[i];
                    int at;
                    if (!depth.TryGetValue(node.Id, out at) || at != level)
                    {
                        continue;
                    }

                    SitePiece piece = chosen[node.Id];
                    placements.Add(new SitePlacement(node, piece, x, y, at));
                    y += piece.Height + Gap;
                    widest = Math.Max(widest, piece.Width);
                    any = true;
                }

                if (!any)
                {
                    break;
                }

                x += widest + Gap;
                level++;
            }

            return placements;
        }

        private static Dictionary<string, int> Depths(SiteLayout layout, HashSet<string> present)
        {
            Dictionary<string, int> depth = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { SiteGrammar.Outside, 0 }
            };

            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < layout.Routes.Count; i++)
                {
                    SiteLayoutRoute route = layout.Routes[i];
                    if (!Standing(route.From, present) || !Standing(route.To, present))
                    {
                        continue;
                    }

                    int from;
                    if (!depth.TryGetValue(route.From, out from))
                    {
                        continue;
                    }

                    int to;
                    if (!depth.TryGetValue(route.To, out to) || from + 1 < to)
                    {
                        // A way out is written towards outside, and outside is depth zero; a place
                        // reached only by walking backwards down one is not one way in from the
                        // world, so the bolthole never pulls a room to the front of the mine.
                        if (!string.Equals(route.To, SiteGrammar.Outside, StringComparison.Ordinal))
                        {
                            depth[route.To] = from + 1;
                            grew = true;
                        }
                    }
                }
            }

            return depth;
        }

        // -- joining them ---------------------------------------------------------------------

        private static List<SiteConnector> Join(
            SiteLayout layout,
            HashSet<string> present,
            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs,
            List<SiteStructureOmission> omitted)
        {
            List<SiteConnector> connectors = new List<SiteConnector>();
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                if (!Standing(route.From, present) || !Standing(route.To, present))
                {
                    omitted.Add(new SiteStructureOmission(
                        route.From + " -> " + route.To,
                        true,
                        "one of the parts it joins is not here"));
                    continue;
                }

                SiteRouteLeg leg = legs[route];
                connectors.Add(new SiteConnector(route, KindOf(route), leg.Promised, leg.Refusal));
            }

            return connectors;
        }

        /// <summary>
        /// What a route physically is, read off what the plan already requires of it. The first
        /// requirement decides, in the order a place is actually built: something shut before
        /// something guarded, because a locked door with somebody standing at it is still a door.
        /// </summary>
        private static SiteConnectorKind KindOf(SiteLayoutRoute route)
        {
            if (Requires(route, SiteAffordance.HiddenPassage))
            {
                return SiteConnectorKind.HiddenWay;
            }

            if (Requires(route, SiteAffordance.LockedBarrier))
            {
                return SiteConnectorKind.LockedDoor;
            }

            if (Requires(route, SiteAffordance.BreakableBarrier))
            {
                return SiteConnectorKind.RubbleFall;
            }

            if (Requires(route, SiteAffordance.DiggableBypass))
            {
                return SiteConnectorKind.DugBypass;
            }

            // Waiting on somebody letting you in is a threshold somebody keeps, whether or not the
            // grammar also spelled it out: it is the difference BQ-087 built the two-ways-in rule
            // on, and a way in that reads as an opening in the rock would lose it.
            if (Requires(route, SiteAffordance.GuardedThreshold)
                || Requires(route, SiteAffordance.SocialCheckpoint)
                || route.NeedsAdmission)
            {
                return SiteConnectorKind.GuardedGate;
            }

            if (Requires(route, SiteAffordance.AlternateExit) || route.IsExit)
            {
                return SiteConnectorKind.Shaft;
            }

            return SiteConnectorKind.Passage;
        }

        private static bool Requires(SiteLayoutRoute route, SiteAffordance affordance)
        {
            for (int i = 0; i < route.Affordances.Count; i++)
            {
                if (route.Affordances[i] == affordance)
                {
                    return true;
                }
            }

            return false;
        }

        // -- where what the matter left actually is -------------------------------------------

        private static List<SiteAnchor> Anchor(SiteContentsReading contents, HashSet<string> present)
        {
            List<SiteAnchor> anchors = new List<SiteAnchor>();
            if (contents == null)
            {
                return anchors;
            }

            for (int i = 0; i < contents.Occupants.Count; i++)
            {
                SiteOccupancy occupant = contents.Occupants[i];

                // Only being held somewhere is a fact about which part of a place somebody is in
                // (`BQ-091`). Everybody else is simply here, and inventing a room for them would be
                // this layer deciding where a body stands, which is Elin's (`D021`).
                if (occupant.NodeId.Length > 0 && present.Contains(occupant.NodeId))
                {
                    anchors.Add(new SiteAnchor(
                        occupant.Id, SiteAnchorKind.Occupant, occupant.NodeId, occupant.Reason));
                }
            }

            for (int i = 0; i < contents.Cargo.Count; i++)
            {
                SiteHolding holding = contents.Cargo[i];
                if (holding.NodeId.Length > 0 && present.Contains(holding.NodeId))
                {
                    anchors.Add(new SiteAnchor(
                        holding.Item.Id, SiteAnchorKind.Cargo, holding.NodeId, holding.Reason));
                }
            }

            return anchors;
        }

        // -- proving it can be walked ----------------------------------------------------------

        /// <summary>
        /// What the built place is checked for before anybody is put in it.
        ///
        /// Validation after realization, not only before it (`PP §8`): the plan was already judged
        /// by BQ-092, and these are the things that can only be false of a thing with a shape - two
        /// pieces in the same ground, a chamber carrying more ways than it has, a part standing
        /// behind a connector nobody can pass.
        /// </summary>
        private static IReadOnlyList<SiteStructureCheck> Validate(SiteStructure structure, SiteLayout layout)
        {
            List<SiteStructureCheck> checks = new List<SiteStructureCheck>();

            List<string> missing = new List<string>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                if (node.Required && !structure.Has(node.Id))
                {
                    missing.Add(node.Id);
                }
            }

            checks.Add(new SiteStructureCheck(
                "every part the kind requires is here",
                missing.Count == 0,
                missing.Count == 0 ? structure.Placements.Count + " piece(s) placed" : string.Join(", ", missing.ToArray())));

            List<string> overlaps = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                for (int j = i + 1; j < structure.Placements.Count; j++)
                {
                    if (structure.Placements[i].Overlaps(structure.Placements[j]))
                    {
                        overlaps.Add(structure.Placements[i].NodeId + " and " + structure.Placements[j].NodeId);
                    }
                }
            }

            checks.Add(new SiteStructureCheck(
                "no two pieces stand in the same ground",
                overlaps.Count == 0,
                overlaps.Count == 0
                    ? structure.Width + "x" + structure.Height + " of ground"
                    : string.Join("; ", overlaps.ToArray())));

            List<string> crowded = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                int ways = 0;
                for (int c = 0; c < structure.Connectors.Count; c++)
                {
                    SiteConnector connector = structure.Connectors[c];
                    if (string.Equals(connector.From, placement.NodeId, StringComparison.Ordinal)
                        || string.Equals(connector.To, placement.NodeId, StringComparison.Ordinal))
                    {
                        ways++;
                    }
                }

                if (ways > placement.Piece.Ways)
                {
                    crowded.Add(placement.NodeId + " carries " + ways + " ways and "
                                + placement.Piece.Id + " has " + placement.Piece.Ways);
                }
            }

            checks.Add(new SiteStructureCheck(
                "no piece carries more ways than it has",
                crowded.Count == 0,
                crowded.Count == 0 ? "every piece has room for what meets in it" : string.Join("; ", crowded.ToArray())));

            HashSet<string> walkable = structure.Walkable();
            List<string> stranded = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                if (!walkable.Contains(structure.Placements[i].NodeId))
                {
                    stranded.Add(structure.Placements[i].NodeId);
                }
            }

            checks.Add(new SiteStructureCheck(
                "every part that was built can be walked to",
                stranded.Count == 0,
                stranded.Count == 0
                    ? walkable.Count - 1 + " part(s) reachable from outside"
                    : string.Join(", ", stranded.ToArray())));

            bool objectiveReached = structure.ObjectiveNodeId.Length > 0
                                    && walkable.Contains(structure.ObjectiveNodeId);
            checks.Add(new SiteStructureCheck(
                "what the matter came for can be walked to",
                objectiveReached,
                objectiveReached
                    ? structure.Objective + " in " + structure.ObjectiveNodeId
                    : "nothing built reaches " + structure.ObjectiveNodeId));

            List<string> unreachableContents = new List<string>();
            for (int i = 0; i < structure.Anchors.Count; i++)
            {
                SiteAnchor anchor = structure.Anchors[i];
                if (!walkable.Contains(anchor.NodeId))
                {
                    unreachableContents.Add(anchor.Id.Value + " in " + anchor.NodeId);
                }
            }

            checks.Add(new SiteStructureCheck(
                "everything the matter left is somewhere that can be walked to",
                unreachableContents.Count == 0,
                unreachableContents.Count == 0
                    ? structure.Anchors.Count + " thing(s) and person(s) anchored"
                    : string.Join(", ", unreachableContents.ToArray())));

            bool admitted = false;
            bool uninvited = false;
            for (int i = 0; i < structure.Connectors.Count; i++)
            {
                SiteConnector connector = structure.Connectors[i];
                if (!connector.IsEntry || !connector.Passable)
                {
                    continue;
                }

                admitted |= connector.NeedsAdmission;
                uninvited |= !connector.NeedsAdmission;
            }

            checks.Add(new SiteStructureCheck(
                "a way in that waits on somebody and a way in that does not",
                admitted && uninvited,
                admitted && uninvited
                    ? "both kinds of way in are built"
                    : "admitted: " + admitted + ", uninvited: " + uninvited));

            List<string> unafforded = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                for (int a = 0; a < placement.Node.Affordances.Count; a++)
                {
                    if (!placement.Piece.Affords(placement.Node.Affordances[a]))
                    {
                        unafforded.Add(placement.NodeId + " requires " + placement.Node.Affordances[a]
                                       + " and " + placement.Piece.Id + " is not one");
                    }
                }
            }

            checks.Add(new SiteStructureCheck(
                "every part is a piece that affords what it requires",
                unafforded.Count == 0,
                unafforded.Count == 0 ? "requirements met by the pieces chosen" : string.Join("; ", unafforded.ToArray())));

            return checks.AsReadOnly();
        }

        // -- shared -----------------------------------------------------------------------------

        internal static string NodeAnswering(SiteLayout layout, SiteAffordance objective)
        {
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                for (int a = 0; a < node.Affordances.Count; a++)
                {
                    if (node.Affordances[a] == objective)
                    {
                        return node.Id;
                    }
                }
            }

            return string.Empty;
        }

        private static bool Standing(string nodeId, HashSet<string> present)
        {
            return string.Equals(nodeId, SiteGrammar.Outside, StringComparison.Ordinal) || present.Contains(nodeId);
        }

        private static SiteRealizationResult Refused(string reason)
        {
            return new SiteRealizationResult(null, null, new[] { reason });
        }
    }
}
