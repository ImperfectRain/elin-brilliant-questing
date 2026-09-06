using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// One bounded physical addition somebody wants made to a place that already exists (BQ-143).
    ///
    /// Three fields, and every one of them is an identity rather than an instruction: what the
    /// addition is called, what it is built out of, and which part of the place it opens off. Where
    /// it actually goes is not the caller's to say - <see cref="SiteMutation"/> looks for ground
    /// nothing is standing on and refuses when it cannot find any, and a plan that named
    /// coordinates would be a plan that could name somebody's cellar.
    /// </summary>
    public sealed class SiteAdditionPlan
    {
        public SiteAdditionPlan(string additionId, string pieceId, string anchorNodeId)
        {
            AdditionId = additionId ?? string.Empty;
            PieceId = pieceId ?? string.Empty;
            AnchorNodeId = anchorNodeId ?? string.Empty;
        }

        /// <summary>
        /// What this addition is, as a name that means the same thing on every save.
        ///
        /// The whole of mutation identity. Two calls carrying this string are the same addition
        /// however far apart they are, and that is what makes "applied at most once" checkable
        /// without comparing geometry - which would be comparing the mod's picture of the map
        /// against a map the player has since dug through.
        /// </summary>
        public string AdditionId { get; }

        /// <summary>The authored piece the addition is built out of.</summary>
        public string PieceId { get; }

        /// <summary>The part of the place it opens off, named as the plan names it.</summary>
        public string AnchorNodeId { get; }

        public override string ToString() => AdditionId + " = " + PieceId + " off " + AnchorNodeId;
    }

    /// <summary>
    /// One addition a place has actually been given, and everything the simulation keeps about it.
    ///
    /// The footprint is recorded rather than looked up from the piece on purpose: it is the ground
    /// this addition took, which stays true when a later bundle rewrites the piece or drops it
    /// entirely. A place must not forget which of its ground is spoken for because content moved.
    /// </summary>
    public sealed class SiteAddition
    {
        public SiteAddition(
            string additionId,
            string pieceId,
            string anchorNodeId,
            int x,
            int y,
            int width,
            int height,
            GameTime appliedAt,
            string externalRef)
        {
            AdditionId = additionId ?? string.Empty;
            PieceId = pieceId ?? string.Empty;
            AnchorNodeId = anchorNodeId ?? string.Empty;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            AppliedAt = appliedAt;
            ExternalRef = externalRef ?? string.Empty;
        }

        public string AdditionId { get; }

        public string PieceId { get; }

        public string AnchorNodeId { get; }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>When it was applied. A place records when it changed, the way it records when it began.</summary>
        public GameTime AppliedAt { get; }

        /// <summary>The adapter's handle for what it made, opaque here like every other external ref.</summary>
        public string ExternalRef { get; }

        /// <summary>
        /// The part id this addition stands as in the derived shape of the place.
        ///
        /// Prefixed so it can never collide with a part the grammar named: a grammar that later
        /// grows a node called <c>powder_store</c> must not silently become the same part as an
        /// addition somebody applied under that name.
        /// </summary>
        public string NodeId => Prefix + AdditionId;

        internal const string Prefix = "added:";

        public override string ToString()
        {
            return AdditionId + " = " + PieceId + " at " + X + "," + Y + " off " + AnchorNodeId;
        }
    }

    public enum SiteMutationOutcome
    {
        /// <summary>The place now has the addition, and did not before.</summary>
        Applied,

        /// <summary>The place already had it. Nothing was asked of the build and nothing changed.</summary>
        AlreadyApplied,

        /// <summary>Nothing was added, and every reason is on the result.</summary>
        Refused
    }

    /// <summary>What came of asking for one addition.</summary>
    public sealed class SiteMutationResult
    {
        private static readonly string[] Nothing = new string[0];

        internal SiteMutationResult(
            SiteMutationOutcome outcome,
            NarrativeSite site,
            SiteAddition addition,
            IReadOnlyList<string> considered,
            IReadOnlyList<string> refusals)
        {
            Outcome = outcome;
            Site = site;
            Addition = addition;
            Considered = considered ?? Nothing;
            Refusals = refusals ?? Nothing;
        }

        public SiteMutationOutcome Outcome { get; }

        public NarrativeSite Site { get; }

        /// <summary>
        /// The addition, for both <see cref="SiteMutationOutcome.Applied"/> and
        /// <see cref="SiteMutationOutcome.AlreadyApplied"/> - the second hands back the one the
        /// place already has, so a caller that has lost track cannot tell the two apart by
        /// guessing. Null where nothing was added.
        /// </summary>
        public SiteAddition Addition { get; }

        /// <summary>
        /// Every patch of ground that was looked at and what was found on it, in the order they
        /// were tried. This is the inspector's answer to "why is it there" and, when nothing was
        /// added, to "why is it nowhere".
        /// </summary>
        public IReadOnlyList<string> Considered { get; }

        /// <summary>Why nothing was added. Empty when something was.</summary>
        public IReadOnlyList<string> Refusals { get; }

        /// <summary>The place changed because of this call.</summary>
        public bool Changed => Outcome == SiteMutationOutcome.Applied;
    }

    /// <summary>
    /// BQ-143. Gives one place that already exists one more piece, once, or refuses.
    ///
    /// This is a proof about persistence and safety, not a development system. There is no
    /// schedule, nothing decides on its own that a place should grow, and there is no vocabulary
    /// here for changing a place in any other way: the only physical change this repository can
    /// make to a place it has already made is to add one authored piece beside a part that is
    /// already there, joined to it by an opening. Everything else - moving, removing, resizing,
    /// rebuilding - has no code path, which is the point of writing the narrow one.
    ///
    /// <b>Six questions, answered before anything is written.</b> Which place (its
    /// <see cref="NarrativeSite.Id"/>, which persists and which genesis registered); which addition
    /// (<see cref="SiteAdditionPlan.AdditionId"/>, a name rather than a shape); whether it has been
    /// applied already (<see cref="NarrativeSite.Additions"/>, on the site, in the save); what Elin
    /// owns (the tiles - nothing here is a map, and the mod keeps no copy of one); what BQ persists
    /// (the record, and only the record); and what protects the player's own changes (the ground is
    /// asked about, and anything but <see cref="VanillaGround.Free"/> refuses).
    ///
    /// <b>Idempotency is a record, not a resemblance.</b> An addition already in the list ends the
    /// call before the build is asked for anything, so a second attempt cannot stage, cannot write
    /// and cannot append. A *different* name for an addition off the same part is refused too: one
    /// part carries at most one addition, so a caller cannot duplicate a thing physically by
    /// renaming it.
    ///
    /// <b>Refusing beats guessing, everywhere.</b> A place whose shape cannot be derived, a part
    /// that is not in it, a piece the bundle does not have, ground the build will not call free -
    /// each of those refuses, and the reasons come back rather than being logged and lost. Unknown
    /// ground refuses like occupied ground, because a build that cannot see what is standing there
    /// is exactly the build that must not build there (`D017`).
    ///
    /// <b>Nothing else about the place is touched.</b> No occupant is staged, no cargo is placed,
    /// no approach is added, the manifest is not rewritten, identity and the zone handle are the
    /// same afterwards, and nothing is dispatched to the ledger - a place gaining a store room is
    /// not something that happened to anybody, exactly as genesis is not (`D058`).
    /// </summary>
    public static class SiteMutation
    {
        /// <summary>Room left around an addition, so nothing is ever put down against something else.</summary>
        private const int Gap = 1;

        /// <summary>
        /// Adds one authored piece to a place that already exists, or says why it did not.
        ///
        /// The order of the checks is the safety property: identity, then whether it has been done,
        /// then whether this build may do it at all, then where it could go, and only then the
        /// write. Every step before the write is a read, so a refused mutation leaves the world
        /// exactly as it found it.
        /// </summary>
        public static SiteMutationResult Apply(
            NarrativeWorldState world,
            EntityId siteId,
            SiteAdditionPlan plan,
            SiteGrammarLibrary grammars,
            SitePieceCatalogue pieces,
            ActionRegistry actions,
            IVanillaState vanilla,
            ISituationStager stager,
            GameTime now)
        {
            if (world == null || plan == null || stager == null)
            {
                return Refused(null, "an addition needs a world, a plan and a stager");
            }

            if (plan.AdditionId.Length == 0)
            {
                // An addition nobody can name cannot be proved to have happened once, because
                // there would be nothing to look up on the second attempt.
                return Refused(null, "an addition with no name cannot be applied once");
            }

            NarrativeSite site = world.Registry.GetSite(siteId);
            if (site == null)
            {
                return Refused(null, "the world has no place " + siteId.Value + " to add to");
            }

            if (!site.Established)
            {
                // Adding to a place genesis has not made would be genesis under another name, and
                // it would be doing it without the plan genesis validates.
                return Refused(site, site.Name + " was never established, so there is nothing to add to");
            }

            if (site.Persistence != SitePersistence.Persistent)
            {
                // A throwaway interior is rebuilt from nothing when it is next needed, so an
                // addition to one is either lost or applied again every time - and neither of those
                // is what this proves.
                return Refused(site, site.Name + " is a throwaway interior; an addition to it would not survive it");
            }

            SiteAddition already = site.AdditionOf(plan.AdditionId);
            if (already != null)
            {
                // Before the build is asked anything at all. A second attempt costs a dictionary
                // lookup, stages nothing and appends nothing, and it answers the same on a build
                // that has since lost the capability - the place has the addition either way.
                return new SiteMutationResult(SiteMutationOutcome.AlreadyApplied, site, already, null, null);
            }

            if (vanilla == null)
            {
                return Refused(site, "no build has said whether it can " + VanillaCapability.AddPlaceFixture);
            }

            if (!vanilla.Supports(VanillaCapability.AddPlaceFixture))
            {
                return Refused(
                    site,
                    "this build cannot " + VanillaCapability.AddPlaceFixture
                    + ", so a place it already made cannot be added to");
            }

            SiteRealizationResult shape = ScenarioDungeon.StructureOf(site, grammars, pieces, actions, vanilla);
            if (!shape.Built)
            {
                // The shape is how this knows which ground the place is already standing on. A
                // place whose shape cannot be read is a place where every patch is ambiguous, and
                // ambiguous ground is never written on.
                List<string> reasons = new List<string>();
                reasons.Add("the shape of " + site.Name + " cannot be read, so no ground in it is known to be free");
                for (int i = 0; i < shape.Refusals.Count; i++)
                {
                    reasons.Add(shape.Refusals[i]);
                }

                return new SiteMutationResult(SiteMutationOutcome.Refused, site, null, null, reasons.AsReadOnly());
            }

            SiteStructure structure = shape.Structure;
            SitePlacement anchor = structure.PlacementOf(plan.AnchorNodeId);
            if (anchor == null)
            {
                return Refused(site, site.Name + " has no " + plan.AnchorNodeId + " for an addition to open off");
            }

            string occupied = AdditionOff(site, plan.AnchorNodeId);
            if (occupied != null)
            {
                // A second addition off the same part under a different name would be the same
                // physical thing twice. The name is the identity; this is the shape's half of it.
                return Refused(
                    site,
                    plan.AnchorNodeId + " already carries the addition " + occupied
                    + "; one part takes one addition");
            }

            SitePiece piece = pieces == null ? null : pieces.Get(plan.PieceId);
            if (piece == null)
            {
                return Refused(site, "no authored piece " + plan.PieceId + " to add");
            }

            string refusal;
            if (!piece.CanBuild(vanilla, out refusal))
            {
                return Refused(site, plan.PieceId + " cannot be built on this build: " + refusal);
            }

            List<string> considered = new List<string>();
            EntityId zone = SiteGenesis.ZoneOf(site);
            int x;
            int y;
            if (!FindGround(structure, anchor, piece, zone, vanilla, considered, out x, out y))
            {
                return new SiteMutationResult(
                    SiteMutationOutcome.Refused,
                    site,
                    null,
                    considered.AsReadOnly(),
                    new[] { "no ground beside " + plan.AnchorNodeId + " is free for " + plan.PieceId });
            }

            SiteAdditionBlueprint blueprint = new SiteAdditionBlueprint(
                site.Id,
                site.VanillaZoneRef,
                plan.AdditionId,
                piece.Id,
                x,
                y,
                piece.Width,
                piece.Height);

            string handle = stager.ApplySiteAddition(blueprint);
            if (string.IsNullOrEmpty(handle))
            {
                // Fail closed, the way genesis does. Nothing has been written at this point, so a
                // refusal costs an addition that did not happen rather than a record of one the map
                // does not have.
                return new SiteMutationResult(
                    SiteMutationOutcome.Refused,
                    site,
                    null,
                    considered.AsReadOnly(),
                    new[] { "the adapter would not add " + piece.Id + " to " + site.Name + " on this build" });
            }

            SiteAddition addition = new SiteAddition(
                plan.AdditionId,
                piece.Id,
                plan.AnchorNodeId,
                x,
                y,
                piece.Width,
                piece.Height,
                now,
                handle);

            site.Additions.Add(addition);
            return new SiteMutationResult(
                SiteMutationOutcome.Applied, site, addition, considered.AsReadOnly(), null);
        }

        /// <summary>
        /// The shape of a place with everything it has been given since, folded in.
        ///
        /// Called by <see cref="ScenarioDungeon.StructureOf"/> so that every reader of a place's
        /// shape sees the same place. An addition is one more piece standing on ground the record
        /// names, joined to the part it opens off by an opening - so it can be walked to exactly
        /// when that part can, and it changes nothing about how anything else is reached.
        /// </summary>
        internal static SiteStructure Fold(SiteStructure structure, NarrativeSite site, SitePieceCatalogue pieces)
        {
            if (structure == null || site == null || site.Additions.Count == 0)
            {
                return structure;
            }

            List<SitePlacement> placements = new List<SitePlacement>(structure.Placements);
            List<SiteConnector> connectors = new List<SiteConnector>(structure.Connectors);
            List<SiteStructureOmission> omitted = new List<SiteStructureOmission>(structure.Omitted);

            for (int i = 0; i < site.Additions.Count; i++)
            {
                SiteAddition addition = site.Additions[i];
                SitePlacement anchor = structure.PlacementOf(addition.AnchorNodeId);
                if (anchor == null)
                {
                    // The part it opened off is not in the place any more - a grammar this bundle
                    // has changed under a save. Said, rather than quietly reattached somewhere
                    // else: the addition is somewhere, and this build no longer knows where.
                    omitted.Add(new SiteStructureOmission(
                        addition.NodeId,
                        true,
                        "the " + addition.AnchorNodeId + " it opens off is not in this place any more"));
                    continue;
                }

                SiteLayoutNode node = new SiteLayoutNode(
                    new SiteNodeSpec(addition.NodeId, false, null, string.Empty));

                placements.Add(new SitePlacement(
                    node, PieceFor(addition, pieces), addition.X, addition.Y, anchor.Depth));

                connectors.Add(new SiteConnector(
                    new SiteLayoutRoute(
                        new SiteRouteSpec(addition.AnchorNodeId, addition.NodeId, string.Empty, false, null),
                        false),
                    SiteConnectorKind.Passage,
                    true,
                    null));
            }

            return new SiteStructure(
                structure.Family,
                structure.Layout,
                structure.Objective,
                structure.ObjectiveNodeId,
                placements,
                connectors,
                structure.Anchors,
                omitted);
        }

        /// <summary>
        /// What the addition is made of, as the bundle has it - and, where the bundle no longer has
        /// it, a piece of exactly the ground the record says it took.
        ///
        /// Content drift must not make a place forget which of its ground is spoken for. The
        /// footprint is the load-bearing half of an addition for everything that comes after it,
        /// because it is what the next addition has to keep off; what the piece affords is not, and
        /// a stand-in that claimed affordances nobody authored would be worse than one that claims
        /// none.
        /// </summary>
        private static SitePiece PieceFor(SiteAddition addition, SitePieceCatalogue pieces)
        {
            SitePiece authored = pieces == null ? null : pieces.Get(addition.PieceId);
            if (authored != null && authored.Width == addition.Width && authored.Height == addition.Height)
            {
                return authored;
            }

            return new SitePiece(
                addition.PieceId,
                pieces == null ? string.Empty : pieces.Family,
                string.Empty,
                addition.Width,
                addition.Height,
                2,
                null,
                RouteEvidence.BqAuthored,
                string.Empty,
                null);
        }

        /// <summary>
        /// The first patch of ground beside the part it opens off that nothing is standing on.
        ///
        /// Four patches, in one fixed order, each one touching the part the addition opens off,
        /// because an addition is cut *off* something rather than dropped anywhere in the place.
        /// Each is checked twice: against what this mod knows it built, and then against what the
        /// game says is actually there. The second check is the one that protects a player's own
        /// work, and it is why the first is not enough - the mod's picture of the place is of the
        /// place as it was made, and the player has been living in it since.
        /// </summary>
        private static bool FindGround(
            SiteStructure structure,
            SitePlacement anchor,
            SitePiece piece,
            EntityId zone,
            IVanillaState vanilla,
            List<string> considered,
            out int freeX,
            out int freeY)
        {
            freeX = 0;
            freeY = 0;

            int[][] candidates =
            {
                new[] { anchor.Right + Gap, anchor.Y },
                new[] { anchor.X, anchor.Bottom + Gap },
                new[] { anchor.X - Gap - piece.Width, anchor.Y },
                new[] { anchor.X, anchor.Y - Gap - piece.Height }
            };

            string[] sides = { "right of", "below", "left of", "above" };

            for (int i = 0; i < candidates.Length; i++)
            {
                int x = candidates[i][0];
                int y = candidates[i][1];
                string where = sides[i] + " " + anchor.NodeId + " at " + x + "," + y;

                if (x < 0 || y < 0)
                {
                    // The place's own grid starts at the origin. Ground outside it is ground this
                    // mod has never described, not ground it knows to be empty.
                    considered.Add(where + ": outside the ground the place stands on");
                    continue;
                }

                string standing = WhatStandsThere(structure, x, y, piece.Width, piece.Height);
                if (standing != null)
                {
                    considered.Add(where + ": " + standing + " is there");
                    continue;
                }

                VanillaGround ground = vanilla.InspectGround(zone, x, y, piece.Width, piece.Height);
                if (ground != VanillaGround.Free)
                {
                    considered.Add(where + ": the build says " + Describe(ground));
                    continue;
                }

                considered.Add(where + ": free");
                freeX = x;
                freeY = y;
                return true;
            }

            return false;
        }

        private static string Describe(VanillaGround ground)
        {
            switch (ground)
            {
                case VanillaGround.PlayerChanged:
                    return "the player has changed this ground";
                case VanillaGround.Occupied:
                    return "something already stands there";
                default:
                    return "it cannot tell what is there";
            }
        }

        /// <summary>
        /// What of the place is already on this ground, counting the room a piece is never put
        /// down without, or null where nothing is.
        /// </summary>
        private static string WhatStandsThere(SiteStructure structure, int x, int y, int width, int height)
        {
            int left = x - Gap;
            int top = y - Gap;
            int right = x + width + Gap;
            int bottom = y + height + Gap;

            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                if (left < placement.Right && placement.X < right
                    && top < placement.Bottom && placement.Y < bottom)
                {
                    return placement.NodeId;
                }
            }

            return null;
        }

        private static string AdditionOff(NarrativeSite site, string anchorNodeId)
        {
            for (int i = 0; i < site.Additions.Count; i++)
            {
                if (string.Equals(site.Additions[i].AnchorNodeId, anchorNodeId, StringComparison.Ordinal))
                {
                    return site.Additions[i].AdditionId;
                }
            }

            return null;
        }

        private static SiteMutationResult Refused(NarrativeSite site, string reason)
        {
            return new SiteMutationResult(SiteMutationOutcome.Refused, site, null, null, new[] { reason });
        }
    }
}
