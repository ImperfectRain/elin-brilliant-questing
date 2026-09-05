using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// What the plan can say about one route on this build.
    ///
    /// Three answers rather than two, because a leg can be walkable and still demand something
    /// nobody has written a verb for - a snare on a corridor does not shut it - and the two gaps
    /// are closed by different people (`D067`). A plan may hold an
    /// <see cref="Unanswered"/> or <see cref="Unsupported"/> route; what it may not do is put one
    /// on a path something required depends on.
    /// </summary>
    public enum ScenarioSupport
    {
        /// <summary>A verb this build can be offered takes it, and it demands nothing nobody answers.</summary>
        Supported,

        /// <summary>It demands something no registered verb answers. A verb nobody has written.</summary>
        Unanswered,

        /// <summary>Verbs answer it and this build refused every one of them (`D067`).</summary>
        Unsupported
    }

    /// <summary>What a plan anchors to a region, and why that region rather than another.</summary>
    public enum ScenarioAnchorKind
    {
        /// <summary>Where this place answers the errand.</summary>
        Objective,

        /// <summary>Something the matter left here that proves or produced part of it.</summary>
        Evidence,

        /// <summary>Somebody the matter is holding here.</summary>
        Captive
    }

    /// <summary>Why the plan can say a region is occupied.</summary>
    public enum ScenarioOccupancyKind
    {
        /// <summary>The people who hold the place, standing where the grammar says somebody stands.</summary>
        Garrison,

        /// <summary>People kept here against their will.</summary>
        Held
    }

    /// <summary>Kinds of closed route the plan can walk round.</summary>
    public enum ScenarioCycleKind
    {
        /// <summary>It leaves the place and comes back, or comes back out. A way round.</summary>
        Reentrant,

        /// <summary>It stays inside the place.</summary>
        Internal
    }

    /// <summary>The topological claims a plan has to be able to make before anything realizes it.</summary>
    public enum ScenarioInvariant
    {
        /// <summary>Some region of this place answers what the errand came for.</summary>
        ObjectiveAnchored,

        /// <summary>A way this build can be offered runs from outside to that region.</summary>
        ObjectiveReachable,

        /// <summary>Every anchored piece of evidence is in a region a promised way reaches.</summary>
        EvidenceReachable,

        /// <summary>
        /// Every alternative the plan claims is a different play, not the same walk under a second
        /// name.
        /// </summary>
        AlternativesAreStructural,

        /// <summary>
        /// No path something required depends on runs through a route this build cannot keep or
        /// no verb answers.
        /// </summary>
        RequiredPathsSupported,

        /// <summary>
        /// Every id the plan names - the matter, the events behind its contents, the claims its
        /// evidence proves, the people in it - is still one the world holds.
        /// </summary>
        CausalReferencesIntact
    }

    /// <summary>One functional region of the plan: what it is for, never where it is.</summary>
    public sealed class ScenarioRegion
    {
        private static readonly ScenarioAnchor[] NoAnchors = new ScenarioAnchor[0];

        internal ScenarioRegion(SiteLayoutNode node, bool objective, bool reachable, int depth)
        {
            Node = node;
            IsObjective = objective;
            Reachable = reachable;
            Depth = depth;
            Anchors = NoAnchors;
        }

        public SiteLayoutNode Node { get; }

        public string Id => Node.Id;

        /// <summary>Every place of this kind has one. An optional region is what this seed drew.</summary>
        public bool Required => Node.Required;

        public IReadOnlyList<SiteAffordance> Affordances => Node.Affordances;

        /// <summary>The authored-piece socket this region is filled from, where the grammar names one.</summary>
        public string Socket => Node.Socket;

        public bool IsObjective { get; }

        /// <summary>Some way from outside to here can be offered on this build.</summary>
        public bool Reachable { get; }

        /// <summary>Legs on the shortest promised way in. Zero while nothing reaches it.</summary>
        public int Depth { get; }

        public IReadOnlyList<ScenarioAnchor> Anchors { get; internal set; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// One route of the plan, with the build's answer about it.
    ///
    /// The route itself is BQ-089's, unchanged; what this adds is the answer BQ-090 works out and
    /// the one comparable string that says what taking it costs, so two routes between the same
    /// regions can be told apart by what they ask rather than by their names.
    /// </summary>
    public sealed class ScenarioRoute
    {
        internal ScenarioRoute(SiteLayoutRoute route, SiteRouteLeg leg, ScenarioSupport support, string demands)
        {
            Route = route;
            Leg = leg;
            Support = support;
            Demands = demands ?? string.Empty;
        }

        public SiteLayoutRoute Route { get; }

        /// <summary>The verbs considered for it and what each of them leans on.</summary>
        public SiteRouteLeg Leg { get; }

        public string From => Route.From;

        public string To => Route.To;

        public string ActionId => Route.ActionId;

        public bool NeedsAdmission => Route.NeedsAdmission;

        public bool IsEntry => Route.IsEntry;

        public bool IsExit => Route.IsExit;

        public bool Required => Route.Required;

        public IReadOnlyList<SiteAffordance> Affordances => Route.Affordances;

        public ScenarioSupport Support { get; }

        /// <summary>
        /// What taking it asks of whoever takes it, as one comparable string. Empty when the two
        /// regions are simply next to each other, which is most of what a plan says.
        /// </summary>
        public string Demands { get; }

        public bool Free => Demands.Length == 0;

        /// <summary>The leg's own reason, empty while it is supported.</summary>
        public string Refusal =>
            Support == ScenarioSupport.Supported ? string.Empty
                : Leg == null ? "no build was asked"
                : Leg.Refusal.Length > 0 ? Leg.Refusal
                : "it demands " + Names(Leg.Unanswered) + " and nothing in the action library answers it";

        public override string ToString()
        {
            return From + " -> " + To + (Demands.Length > 0 ? " (" + Demands + ")" : string.Empty);
        }

        internal static string Names(IReadOnlyList<SiteAffordance> affordances)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < affordances.Count; i++)
            {
                names.Add(affordances[i].ToString());
            }

            return string.Join(", ", names.ToArray());
        }
    }

    /// <summary>
    /// One thing the plan pins to one region, and the recorded history that pinned it there.
    ///
    /// Everything but the region is an id the save already holds. An anchor never copies the
    /// object, the claim or the person - a corrected fact corrects every anchor that reads it, and
    /// a plan that carried its own copy would be a second history sitting beside the real one
    /// (`D039`, `D057`).
    /// </summary>
    public sealed class ScenarioAnchor
    {
        internal ScenarioAnchor(
            ScenarioAnchorKind kind,
            string regionId,
            SiteAffordance affordance,
            EntityId subjectId,
            EntityId factId,
            EntityId eventId,
            string reason)
        {
            Kind = kind;
            RegionId = regionId ?? string.Empty;
            Affordance = affordance;
            SubjectId = subjectId;
            FactId = factId;
            EventId = eventId;
            Reason = reason ?? string.Empty;
        }

        public ScenarioAnchorKind Kind { get; }

        public string RegionId { get; }

        /// <summary>The requirement of the region that made it the place for this.</summary>
        public SiteAffordance Affordance { get; }

        /// <summary>The object or the person. <see cref="EntityId.None"/> on a bare objective.</summary>
        public EntityId SubjectId { get; }

        /// <summary>The claim it proves, where this matter rests on one.</summary>
        public EntityId FactId { get; }

        /// <summary>The recorded event that put it here.</summary>
        public EntityId EventId { get; }

        public string Reason { get; }

        public override string ToString() => Kind + " in " + RegionId;
    }

    /// <summary>One person the plan can say is in one region, and what the world says put them there.</summary>
    public sealed class ScenarioOccupant
    {
        internal ScenarioOccupant(EntityId npcId, SitePresence presence, EntityId because)
        {
            NpcId = npcId;
            Presence = presence;
            Because = because;
        }

        public EntityId NpcId { get; }

        public SitePresence Presence { get; }

        /// <summary>The event or the organization that puts them here. Never a guess.</summary>
        public EntityId Because { get; }

        public override string ToString() => NpcId.Value + " (" + Presence + ")";
    }

    /// <summary>
    /// A region the plan may say is occupied, and by whom.
    ///
    /// A plan says what a place is for, not where a body is standing, so this is deliberately not
    /// "everybody, spread over the rooms". A region is an occupant region only where the grammar
    /// itself asserts somebody is in it - a threshold somebody stands at, a cell somebody is kept
    /// in - and the world then says which people those are. Anybody the plan cannot place that way
    /// is in <see cref="ScenarioPlan.UnplacedOccupants"/> and said to be, because inventing a room
    /// for them would be this layer deciding a thing BQ-140 has to decide against a real map.
    /// </summary>
    public sealed class ScenarioOccupantRegion
    {
        internal ScenarioOccupantRegion(
            string regionId,
            ScenarioOccupancyKind kind,
            SiteAffordance affordance,
            EntityId organizationId,
            IReadOnlyList<ScenarioOccupant> occupants,
            string reason)
        {
            RegionId = regionId ?? string.Empty;
            Kind = kind;
            Affordance = affordance;
            OrganizationId = organizationId;
            Occupants = occupants ?? new ScenarioOccupant[0];
            Reason = reason ?? string.Empty;
        }

        public string RegionId { get; }

        public ScenarioOccupancyKind Kind { get; }

        /// <summary>The requirement of the region that asserts somebody is in it.</summary>
        public SiteAffordance Affordance { get; }

        /// <summary>
        /// The crew holding it, where the people in it belong to one. BQ-140 may only promise
        /// distinct hostility per region where the build supports it, and this is the reference it
        /// would ask that question of.
        /// </summary>
        public EntityId OrganizationId { get; }

        public IReadOnlyList<ScenarioOccupant> Occupants { get; }

        public string Reason { get; }
    }

    /// <summary>
    /// One structurally different way to something the plan anchors.
    ///
    /// An alternative is recorded because the demands differ, never because the regions are spelled
    /// differently. Two promised ways taken with the same verbs past the same requirements are one
    /// play however many rooms apart they run, and the second is recorded in
    /// <see cref="ScenarioPlan.CollapsedAlternatives"/> as the cosmetic thing it is.
    /// </summary>
    public sealed class ScenarioAlternative
    {
        internal ScenarioAlternative(
            string targetRegionId,
            IReadOnlyList<string> regions,
            string demands,
            bool needsAdmission,
            string collapsedInto)
        {
            TargetRegionId = targetRegionId ?? string.Empty;
            Regions = regions ?? new string[0];
            Demands = demands ?? string.Empty;
            NeedsAdmission = needsAdmission;
            CollapsedInto = collapsedInto ?? string.Empty;
        }

        public string TargetRegionId { get; }

        /// <summary>The regions it runs through, outside first.</summary>
        public IReadOnlyList<string> Regions { get; }

        /// <summary>What it asks, leg by leg, with the legs that ask nothing left out.</summary>
        public string Demands { get; }

        public bool NeedsAdmission { get; }

        /// <summary>
        /// The alternative this one turned out to be a rewording of, on a collapsed entry. Empty
        /// on a real one.
        /// </summary>
        public string CollapsedInto { get; }

        public override string ToString()
        {
            return string.Join(" -> ", new List<string>(Regions).ToArray())
                   + (Demands.Length > 0 ? " (" + Demands + ")" : " (nothing to get past)");
        }
    }

    /// <summary>
    /// A closed route: somewhere the plan lets you come back round to where you were.
    ///
    /// Directed, because a plan's routes are walked in the direction they are written and a way out
    /// is not a way in (`D066`). A fork that rejoins is drawn as a ring and is not one - nobody can
    /// walk round it - so it is an <see cref="ScenarioAlternative"/> here and not a cycle, which is
    /// the difference between a claim about the graph and a claim about play.
    /// </summary>
    public sealed class ScenarioCycle
    {
        internal ScenarioCycle(ScenarioCycleKind kind, IReadOnlyList<string> regions, int demanding)
        {
            Kind = kind;
            Regions = regions ?? new string[0];
            Demanding = demanding;
        }

        public ScenarioCycleKind Kind { get; }

        /// <summary>The ring, from its first region back towards it. Never repeats a region.</summary>
        public IReadOnlyList<string> Regions { get; }

        /// <summary>How many routes round it ask anything at all.</summary>
        public int Demanding { get; }

        /// <summary>A ring nothing has to be got past on: walking, not deciding.</summary>
        public bool Cosmetic => Demanding == 0;

        public override string ToString()
        {
            List<string> ring = new List<string>(Regions);
            if (ring.Count > 0)
            {
                ring.Add(ring[0]);
            }

            return string.Join(" -> ", ring.ToArray());
        }
    }

    /// <summary>An authored-piece socket the plan carries. Nothing fills one until BQ-140.</summary>
    public sealed class ScenarioSocket
    {
        internal ScenarioSocket(string regionId, string socket, bool required)
        {
            RegionId = regionId ?? string.Empty;
            Socket = socket ?? string.Empty;
            Required = required;
        }

        public string RegionId { get; }

        public string Socket { get; }

        /// <summary>Every place of this kind has this region, so every one of them has this socket.</summary>
        public bool Required { get; }

        /// <summary>
        /// Always false here. A socket is filled by a physical realization, and no BQ site has one
        /// (BQ-140); carrying it unfilled is what lets this step be checked without one.
        /// </summary>
        public bool Filled => false;

        public override string ToString() => Socket + " in " + RegionId;
    }

    /// <summary>One invariant, whether it holds, and what it was read off.</summary>
    public sealed class ScenarioFinding
    {
        internal ScenarioFinding(ScenarioInvariant invariant, bool held, string reason)
        {
            Invariant = invariant;
            Held = held;
            Reason = reason ?? string.Empty;
        }

        public ScenarioInvariant Invariant { get; }

        public bool Held { get; }

        /// <summary>What was seen - the way that reaches it, or the region nothing reaches.</summary>
        public string Reason { get; }

        public override string ToString() => Invariant + (Held ? ": " + Reason : ": broken; " + Reason);
    }

    /// <summary>Every invariant read over one plan.</summary>
    public sealed class ScenarioValidation
    {
        internal ScenarioValidation(IReadOnlyList<ScenarioFinding> findings)
        {
            Findings = findings ?? new ScenarioFinding[0];

            bool held = true;
            for (int i = 0; i < Findings.Count; i++)
            {
                held &= Findings[i].Held;
            }

            Held = held;
        }

        public IReadOnlyList<ScenarioFinding> Findings { get; }

        /// <summary>Every invariant holds.</summary>
        public bool Held { get; }

        public ScenarioFinding Get(ScenarioInvariant invariant)
        {
            for (int i = 0; i < Findings.Count; i++)
            {
                if (Findings[i].Invariant == invariant)
                {
                    return Findings[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// BQ-139. The abstract plan for one bounded scenario location: why its spatial parts exist and
    /// how they relate, before anything physical realizes them.
    ///
    /// This is `PP §3`'s <c>ScenarioPlan</c> - the representation that is authoritative for
    /// meaning, standing above the plan a place is composed from and below the map Elin eventually
    /// owns. BQ-089 said what a kind of place requires, BQ-090 said which of it this build can
    /// keep, BQ-092 chose between candidates and BQ-091 said what the matter leaves in it; this is
    /// the one artifact that holds all four together and can be checked as a whole: the regions and
    /// the routes between them, the cycles and the alternatives, what is anchored where and what
    /// recorded event anchored it, who the plan may say is in which region, the authored-piece
    /// sockets it carries, and the invariants that must hold before any of it is built.
    ///
    /// <b>Nothing here is geometry, and nothing here is prose.</b> There is no coordinate, no tile,
    /// no room size and no line anybody reads - the regions are what the place is *for* and the
    /// reasons are the inspector's, in the same voice every other trace in this repository is
    /// written in. <see cref="PlanId"/> is a hash of the plan's meaning for exactly that reason: a
    /// plan cannot be identified by where it was drawn, because there is nowhere it was drawn.
    ///
    /// <b>References, never copies.</b> The matter, the events behind the contents, the claims the
    /// evidence proves and the people in it are ids the save already holds. The plan itself is
    /// derived and never persisted, the way <see cref="SiteLayout"/> is not: a site records the
    /// grammar and the seed, and this recomposes (`D066`).
    /// </summary>
    public sealed class ScenarioPlan
    {
        private static readonly ScenarioRegion[] NoRegions = new ScenarioRegion[0];
        private static readonly ScenarioRoute[] NoRoutes = new ScenarioRoute[0];
        private static readonly ScenarioAnchor[] NoAnchors = new ScenarioAnchor[0];
        private static readonly ScenarioOccupantRegion[] NoOccupantRegions = new ScenarioOccupantRegion[0];
        private static readonly ScenarioOccupant[] NoOccupants = new ScenarioOccupant[0];
        private static readonly ScenarioAlternative[] NoAlternatives = new ScenarioAlternative[0];
        private static readonly ScenarioCycle[] NoCycles = new ScenarioCycle[0];
        private static readonly ScenarioSocket[] NoSockets = new ScenarioSocket[0];
        private static readonly string[] NoRefusals = new string[0];

        internal ScenarioPlan(
            EntityId threadId,
            SiteCandidateSelection selection,
            SiteContentsReading contents,
            IReadOnlyList<ScenarioRegion> regions,
            IReadOnlyList<ScenarioRoute> routes,
            IReadOnlyList<ScenarioAnchor> anchors,
            IReadOnlyList<ScenarioOccupantRegion> occupantRegions,
            IReadOnlyList<ScenarioOccupant> unplaced,
            IReadOnlyList<ScenarioAlternative> alternatives,
            IReadOnlyList<ScenarioAlternative> collapsed,
            IReadOnlyList<ScenarioCycle> cycles,
            IReadOnlyList<ScenarioSocket> sockets,
            IReadOnlyList<string> refusals)
        {
            ThreadId = threadId;
            Selection = selection;
            Contents = contents;
            Regions = regions ?? NoRegions;
            Routes = routes ?? NoRoutes;
            Anchors = anchors ?? NoAnchors;
            OccupantRegions = occupantRegions ?? NoOccupantRegions;
            UnplacedOccupants = unplaced ?? NoOccupants;
            Alternatives = alternatives ?? NoAlternatives;
            CollapsedAlternatives = collapsed ?? NoAlternatives;
            Cycles = cycles ?? NoCycles;
            Sockets = sockets ?? NoSockets;
            Refusals = refusals ?? NoRefusals;
            Validation = new ScenarioValidation(null);
            PlanId = ScenarioPlanner.Identify(this);
        }

        /// <summary>The matter this place is for. A plan with no matter behind it is scenery.</summary>
        public EntityId ThreadId { get; }

        /// <summary>Every candidate weighed and the one chosen, with the reasons the rest were refused.</summary>
        public SiteCandidateSelection Selection { get; }

        /// <summary>What the matter leaves here, or null when nothing could be derived.</summary>
        public SiteContentsReading Contents { get; }

        public SiteLayout Layout => Selection == null ? null : Selection.Layout;

        public string GrammarId => Layout == null ? string.Empty : Layout.GrammarId;

        public string SiteType => Layout == null ? string.Empty : Layout.SiteType;

        /// <summary>The seed of the chosen plan, not of the batch. Recomposing it rebuilds this place.</summary>
        public ulong Seed => Layout == null ? 0UL : Layout.Seed;

        /// <summary>The seed the batch was drawn from. Replaying it re-selects this plan.</summary>
        public ulong SelectionSeed => Selection == null ? 0UL : Selection.Seed;

        public SiteAffordance Objective => Selection == null ? default(SiteAffordance) : Selection.Objective;

        /// <summary>The region this place answers the errand in. Empty when it has none.</summary>
        public string ObjectiveRegionId =>
            Selection == null || Selection.Chosen == null ? string.Empty : Selection.Chosen.ObjectiveNodeId;

        /// <summary>
        /// This plan's identity: a hash over its meaning and nothing else.
        ///
        /// The same semantic inputs and the same seed produce the same id on any machine and in any
        /// session, and no part of it comes from anything a renderer would decide - because the
        /// plan holds no such thing to begin with. Two plans with the same id say the same thing
        /// about the same place for the same matter.
        /// </summary>
        public string PlanId { get; }

        public IReadOnlyList<ScenarioRegion> Regions { get; }

        public IReadOnlyList<ScenarioRoute> Routes { get; }

        public IReadOnlyList<ScenarioAnchor> Anchors { get; }

        public IReadOnlyList<ScenarioOccupantRegion> OccupantRegions { get; }

        /// <summary>
        /// People this matter puts at the place that no region asserts a position for. Reported
        /// rather than scattered: which room a body stands in is a physical question.
        /// </summary>
        public IReadOnlyList<ScenarioOccupant> UnplacedOccupants { get; }

        public IReadOnlyList<ScenarioAlternative> Alternatives { get; }

        /// <summary>Ways that turned out to be another alternative reworded, with the one they repeat.</summary>
        public IReadOnlyList<ScenarioAlternative> CollapsedAlternatives { get; }

        public IReadOnlyList<ScenarioCycle> Cycles { get; }

        public IReadOnlyList<ScenarioSocket> Sockets { get; }

        /// <summary>Why there is no plan at all. Empty when there is one.</summary>
        public IReadOnlyList<string> Refusals { get; }

        /// <summary>
        /// Every invariant read over this plan. Set once, by the planner, after the plan exists -
        /// an invariant is a statement about a whole plan, so there is nothing to read until there
        /// is one.
        /// </summary>
        public ScenarioValidation Validation { get; internal set; }

        /// <summary>There is a plan here, whether or not every invariant holds.</summary>
        public bool Planned => Layout != null && Refusals.Count == 0;

        /// <summary>A plan that may be realized: it exists and every invariant holds.</summary>
        public bool Valid => Planned && Validation.Held;

        public ScenarioRegion GetRegion(string regionId)
        {
            for (int i = 0; i < Regions.Count; i++)
            {
                if (string.Equals(Regions[i].Id, regionId, StringComparison.Ordinal))
                {
                    return Regions[i];
                }
            }

            return null;
        }

        /// <summary>The anchors of one kind, in the order the plan derived them.</summary>
        public List<ScenarioAnchor> AnchorsOf(ScenarioAnchorKind kind)
        {
            List<ScenarioAnchor> found = new List<ScenarioAnchor>();
            for (int i = 0; i < Anchors.Count; i++)
            {
                if (Anchors[i].Kind == kind)
                {
                    found.Add(Anchors[i]);
                }
            }

            return found;
        }

        /// <summary>
        /// The plan in the shape genesis validates, contents and all, or null when there is no
        /// plan to hand over.
        ///
        /// One vocabulary for "what a place must be", as every step since BQ-087 has kept: this
        /// does not get a second description of a place, it fills in the one
        /// <see cref="SiteGenesis"/> already refuses bad versions of.
        /// </summary>
        public SitePlan NewSitePlan(EntityId siteId, string name)
        {
            if (!Planned)
            {
                return null;
            }

            SitePlan plan = Layout.NewPlan(siteId, name, ThreadId);
            if (Contents != null)
            {
                Contents.ApplyTo(plan);
            }

            return plan;
        }
    }

    /// <summary>
    /// BQ-139. Turns a matter and a kind of place into the abstract scenario plan, and reads the
    /// invariants over it.
    ///
    /// The order is the one the earlier steps fixed and is not free: candidates are chosen on
    /// shape and on what this build can keep (`D069`), and only then are the contents derived into
    /// the chosen plan (`D068`), because choosing by contents would depend on a derivation that has
    /// not happened. What this adds on top is the reading no earlier step could make - a whole plan
    /// with its cycles, its alternatives, its anchors and its occupancy in one place, checkable
    /// against the topology BQ-140 will have to realize.
    ///
    /// Nothing here writes anything. No event, no site, no save entry: a plan is a derivation, and
    /// the same inputs derive it again.
    /// </summary>
    public static class ScenarioPlanner
    {
        /// <summary>Beyond this a plan is too tangled to enumerate every ring of, and says so.</summary>
        public const int MaximumCycles = 64;

        /// <summary>
        /// A scenario plan for one matter in one kind of place, drawn from one seed.
        ///
        /// Deterministic all the way down: the batch is drawn from the seed, the contents are read
        /// out of the world in recorded order, and every list here is built in the order the plan
        /// itself declares. The same inputs give the same <see cref="ScenarioPlan.PlanId"/>.
        /// </summary>
        public static ScenarioPlan Plan(
            NarrativeWorldState world,
            EntityId threadId,
            SiteGrammar grammar,
            SiteAffordance objective,
            ulong seed,
            int batch,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            return Plan(
                world,
                threadId,
                SiteCandidates.Select(grammar, objective, seed, batch, actions, vanilla),
                actions,
                vanilla);
        }

        /// <summary>
        /// The same, over a batch somebody already drew. The refusals of the candidates that were
        /// not chosen travel with the plan, because "why this place" is half of explaining it.
        /// </summary>
        public static ScenarioPlan Plan(
            NarrativeWorldState world,
            EntityId threadId,
            SiteCandidateSelection selection,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            List<string> refusals = new List<string>();
            if (selection == null || !selection.Selected)
            {
                refusals.Add(selection == null
                    ? "no batch of plans was drawn"
                    : selection.Refusal.Length > 0 ? selection.Refusal : "no plan was chosen");
                return Refused(threadId, selection, null, refusals);
            }

            SiteLayout layout = selection.Layout;
            SiteContentsReading contents = SiteContents.Derive(world, threadId, layout, vanilla);
            if (!contents.Furnished)
            {
                for (int i = 0; i < contents.Refusals.Count; i++)
                {
                    refusals.Add(contents.Refusals[i]);
                }

                return Refused(threadId, selection, contents, refusals);
            }

            // Every region asked once. Reachability, depth, the objective and the evidence are the
            // same question of different regions, and BQ-090 answers it.
            Dictionary<string, SiteRouteProjection> reach =
                new Dictionary<string, SiteRouteProjection>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                reach[layout.Nodes[i].Id] = SiteRoutes.Project(layout, layout.Nodes[i].Id, actions, vanilla);
            }

            List<ScenarioRoute> routes = Routes(layout, actions, vanilla);
            List<ScenarioRegion> regions = Regions(layout, selection.Chosen.ObjectiveNodeId, reach);
            List<ScenarioAnchor> anchors = Anchors(selection, contents);
            Attach(regions, anchors);

            List<ScenarioOccupant> unplaced = new List<ScenarioOccupant>();
            List<ScenarioOccupantRegion> occupied = Occupancy(layout, contents, anchors, unplaced);

            List<ScenarioAlternative> collapsed = new List<ScenarioAlternative>();
            List<ScenarioAlternative> alternatives = Alternatives(anchors, reach, collapsed);

            ScenarioPlan plan = new ScenarioPlan(
                threadId,
                selection,
                contents,
                regions,
                routes,
                anchors,
                occupied,
                unplaced,
                alternatives,
                collapsed,
                Cycles(layout, routes),
                Sockets(layout),
                refusals);

            plan.Validation = Validate(world, plan, reach, routes);
            return plan;
        }

        private static ScenarioPlan Refused(
            EntityId threadId,
            SiteCandidateSelection selection,
            SiteContentsReading contents,
            IReadOnlyList<string> refusals)
        {
            return new ScenarioPlan(
                threadId, selection, contents, null, null, null, null, null, null, null, null, null,
                refusals);
        }

        // -- the graph -------------------------------------------------------------------------

        private static List<ScenarioRegion> Regions(
            SiteLayout layout, string objective, Dictionary<string, SiteRouteProjection> reach)
        {
            List<ScenarioRegion> regions = new List<ScenarioRegion>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                SiteRouteProjection projection = reach[node.Id];

                int depth = 0;
                for (int w = 0; w < projection.Promised.Count; w++)
                {
                    int legs = projection.Promised[w].Legs.Count;
                    if (depth == 0 || legs < depth)
                    {
                        depth = legs;
                    }
                }

                regions.Add(new ScenarioRegion(
                    node,
                    string.Equals(node.Id, objective, StringComparison.Ordinal),
                    projection.Promised.Count > 0,
                    depth));
            }

            return regions;
        }

        private static List<ScenarioRoute> Routes(
            SiteLayout layout, ActionRegistry actions, IVanillaState vanilla)
        {
            List<ScenarioRoute> routes = new List<ScenarioRoute>();
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                SiteRouteLeg leg = SiteRoutes.Leg(route, actions, vanilla);

                // Three answers, in this order. A requirement nobody has written a verb for is
                // named first because it is true whatever the build says; a refused promise is the
                // build's own answer; and a route is supported only when neither is outstanding.
                ScenarioSupport support = leg.Unanswered.Count > 0
                    ? ScenarioSupport.Unanswered
                    : leg.Promised ? ScenarioSupport.Supported : ScenarioSupport.Unsupported;

                routes.Add(new ScenarioRoute(route, leg, support, Demands(route)));
            }

            return routes;
        }

        /// <summary>What a route asks of whoever takes it, as one comparable string.</summary>
        private static string Demands(SiteLayoutRoute route)
        {
            List<string> parts = new List<string>();
            if (route.ActionId.Length > 0)
            {
                parts.Add(route.ActionId);
            }

            for (int i = 0; i < route.Affordances.Count; i++)
            {
                parts.Add(route.Affordances[i].ToString());
            }

            return string.Join("+", parts.ToArray());
        }

        // -- what is anchored where ------------------------------------------------------------

        private static List<ScenarioAnchor> Anchors(SiteCandidateSelection selection, SiteContentsReading contents)
        {
            List<ScenarioAnchor> anchors = new List<ScenarioAnchor>();
            string objective = selection.Chosen.ObjectiveNodeId;
            if (objective.Length > 0)
            {
                anchors.Add(new ScenarioAnchor(
                    ScenarioAnchorKind.Objective,
                    objective,
                    selection.Objective,
                    EntityId.None,
                    EntityId.None,
                    EntityId.None,
                    "this errand is after " + selection.Objective + " and this is where the place answers it"));
            }

            for (int i = 0; i < contents.Cargo.Count; i++)
            {
                SiteHolding holding = contents.Cargo[i];
                if (holding.NodeId.Length == 0)
                {
                    continue;
                }

                anchors.Add(new ScenarioAnchor(
                    ScenarioAnchorKind.Evidence,
                    holding.NodeId,
                    SiteAffordance.EvidenceCache,
                    holding.Item == null ? EntityId.None : holding.Item.Id,
                    holding.EvidenceForFact,
                    holding.Because,
                    holding.Reason));
            }

            for (int i = 0; i < contents.Occupants.Count; i++)
            {
                SiteOccupancy occupancy = contents.Occupants[i];
                if (occupancy.Presence != SitePresence.Held || occupancy.NodeId.Length == 0)
                {
                    continue;
                }

                anchors.Add(new ScenarioAnchor(
                    ScenarioAnchorKind.Captive,
                    occupancy.NodeId,
                    SiteAffordance.PrisonCell,
                    occupancy.Id,
                    EntityId.None,
                    occupancy.Because,
                    occupancy.Reason));
            }

            return anchors;
        }

        private static void Attach(List<ScenarioRegion> regions, List<ScenarioAnchor> anchors)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                List<ScenarioAnchor> mine = new List<ScenarioAnchor>();
                for (int a = 0; a < anchors.Count; a++)
                {
                    if (string.Equals(anchors[a].RegionId, regions[i].Id, StringComparison.Ordinal))
                    {
                        mine.Add(anchors[a]);
                    }
                }

                regions[i].Anchors = mine.AsReadOnly();
            }
        }

        // -- who the plan may say is where -----------------------------------------------------

        /// <summary>
        /// The regions the plan is entitled to call occupied.
        ///
        /// Two, and only where something asserts them. A region that keeps somebody
        /// (<see cref="SiteAffordance.PrisonCell"/>) holds the people the ledger says are held; the
        /// one region the grammar says somebody stands in - a threshold, a checkpoint - is where
        /// the crew holding the place is. Everybody else is unplaced and listed, because a plan
        /// that spread a crew across the rooms would be deciding, from nothing, a thing only a
        /// realized map can decide.
        /// </summary>
        private static List<ScenarioOccupantRegion> Occupancy(
            SiteLayout layout,
            SiteContentsReading contents,
            List<ScenarioAnchor> anchors,
            List<ScenarioOccupant> unplaced)
        {
            List<ScenarioOccupantRegion> regions = new List<ScenarioOccupantRegion>();

            string threshold = string.Empty;
            SiteAffordance thresholdAffordance = SiteAffordance.GuardedThreshold;
            for (int i = 0; i < layout.Nodes.Count && threshold.Length == 0; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                for (int a = 0; a < node.Affordances.Count && threshold.Length == 0; a++)
                {
                    if (node.Affordances[a] == SiteAffordance.GuardedThreshold
                        || node.Affordances[a] == SiteAffordance.SocialCheckpoint)
                    {
                        threshold = node.Id;
                        thresholdAffordance = node.Affordances[a];
                    }
                }
            }

            List<ScenarioOccupant> garrison = new List<ScenarioOccupant>();
            EntityId crew = EntityId.None;
            Dictionary<string, List<ScenarioOccupant>> held =
                new Dictionary<string, List<ScenarioOccupant>>(StringComparer.Ordinal);
            List<string> cells = new List<string>();

            for (int i = 0; i < contents.Occupants.Count; i++)
            {
                SiteOccupancy occupancy = contents.Occupants[i];
                ScenarioOccupant occupant = new ScenarioOccupant(
                    occupancy.Id, occupancy.Presence, occupancy.Because);

                if (occupancy.Presence == SitePresence.Held && occupancy.NodeId.Length > 0)
                {
                    List<ScenarioOccupant> inCell;
                    if (!held.TryGetValue(occupancy.NodeId, out inCell))
                    {
                        inCell = new List<ScenarioOccupant>();
                        held[occupancy.NodeId] = inCell;
                        cells.Add(occupancy.NodeId);
                    }

                    inCell.Add(occupant);
                    continue;
                }

                // A crew is a reference the world already holds: BQ-091 records the organization
                // as what puts a member at the place, so the plan reads it rather than deciding it.
                if (occupancy.Presence == SitePresence.Group && crew.IsNone)
                {
                    crew = occupancy.Because;
                }

                if (threshold.Length == 0)
                {
                    unplaced.Add(occupant);
                }
                else
                {
                    garrison.Add(occupant);
                }
            }

            if (garrison.Count > 0)
            {
                regions.Add(new ScenarioOccupantRegion(
                    threshold,
                    ScenarioOccupancyKind.Garrison,
                    thresholdAffordance,
                    crew,
                    garrison.AsReadOnly(),
                    "the plan says somebody stands here, and the matter says who holds the place"));
            }

            for (int i = 0; i < cells.Count; i++)
            {
                regions.Add(new ScenarioOccupantRegion(
                    cells[i],
                    ScenarioOccupancyKind.Held,
                    SiteAffordance.PrisonCell,
                    EntityId.None,
                    held[cells[i]].AsReadOnly(),
                    "the ledger records them taken in this matter and nothing since let them go"));
            }

            // Anchored captives and the cells that hold them are the same fact read twice; the
            // anchors are what the validation walks and this is what BQ-140 would populate.
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Kind == ScenarioAnchorKind.Captive && !held.ContainsKey(anchors[i].RegionId))
                {
                    unplaced.Add(new ScenarioOccupant(
                        anchors[i].SubjectId, SitePresence.Held, anchors[i].EventId));
                }
            }

            return regions;
        }

        // -- alternatives and cycles -----------------------------------------------------------

        /// <summary>
        /// The structurally different ways to each thing the plan anchors.
        ///
        /// Only promised ways, because an alternative is a claim about what somebody can actually
        /// do; and deduped by what each asks, because two ways past the same requirements with the
        /// same verbs are one play however differently the rooms are spelled. The duplicates are
        /// not dropped silently - they are the cosmetic labels this step exists to refuse, so they
        /// come back named, with the alternative they repeat.
        /// </summary>
        private static List<ScenarioAlternative> Alternatives(
            List<ScenarioAnchor> anchors,
            Dictionary<string, SiteRouteProjection> reach,
            List<ScenarioAlternative> collapsed)
        {
            List<ScenarioAlternative> alternatives = new List<ScenarioAlternative>();
            HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < anchors.Count; i++)
            {
                string target = anchors[i].RegionId;
                SiteRouteProjection projection;
                if (!targets.Add(target) || !reach.TryGetValue(target, out projection))
                {
                    continue;
                }

                Dictionary<string, ScenarioAlternative> seen =
                    new Dictionary<string, ScenarioAlternative>(StringComparer.Ordinal);
                for (int w = 0; w < projection.Promised.Count; w++)
                {
                    SiteWayThrough way = projection.Promised[w];
                    string demands = Play(way);
                    List<string> regions = new List<string> { SiteGrammar.Outside };
                    for (int leg = 0; leg < way.Legs.Count; leg++)
                    {
                        regions.Add(way.Legs[leg].To);
                    }

                    ScenarioAlternative first;
                    if (seen.TryGetValue(demands, out first))
                    {
                        collapsed.Add(new ScenarioAlternative(
                            target, regions.AsReadOnly(), demands, way.NeedsAdmission, first.ToString()));
                        continue;
                    }

                    ScenarioAlternative alternative = new ScenarioAlternative(
                        target, regions.AsReadOnly(), demands, way.NeedsAdmission, string.Empty);
                    seen[demands] = alternative;
                    alternatives.Add(alternative);
                }
            }

            return alternatives;
        }

        /// <summary>What a way is taken with, the legs nobody has to get past left out.</summary>
        private static string Play(SiteWayThrough way)
        {
            List<string> steps = new List<string>();
            for (int i = 0; i < way.Legs.Count; i++)
            {
                string demands = Demands(way.Legs[i].Route);
                if (demands.Length > 0)
                {
                    steps.Add(demands);
                }
            }

            return string.Join(" then ", steps.ToArray());
        }

        /// <summary>
        /// Every ring in the plan, found once each.
        ///
        /// Simple directed cycles over the regions and everywhere else, walked from the
        /// lowest-placed region of each ring so a ring is never reported twice under a different
        /// rotation. Bounded, because a plan tangled enough to have thousands of rings is not one
        /// this step is willing to describe as if it had read all of them.
        /// </summary>
        private static List<ScenarioCycle> Cycles(SiteLayout layout, List<ScenarioRoute> routes)
        {
            List<string> order = new List<string> { SiteGrammar.Outside };
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                order.Add(layout.Nodes[i].Id);
            }

            Dictionary<string, int> rank = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < order.Count; i++)
            {
                rank[order[i]] = i;
            }

            List<ScenarioCycle> cycles = new List<ScenarioCycle>();
            for (int start = 0; start < order.Count && cycles.Count < MaximumCycles; start++)
            {
                List<string> path = new List<string> { order[start] };
                HashSet<string> onPath = new HashSet<string>(StringComparer.Ordinal) { order[start] };
                Walk(routes, rank, order[start], order[start], path, onPath, cycles);
            }

            return cycles;
        }

        private static void Walk(
            List<ScenarioRoute> routes,
            Dictionary<string, int> rank,
            string start,
            string from,
            List<string> path,
            HashSet<string> onPath,
            List<ScenarioCycle> cycles)
        {
            if (cycles.Count >= MaximumCycles)
            {
                return;
            }

            for (int i = 0; i < routes.Count; i++)
            {
                ScenarioRoute route = routes[i];
                if (!string.Equals(route.From, from, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(route.To, start, StringComparison.Ordinal))
                {
                    if (path.Count > 1)
                    {
                        cycles.Add(Ring(routes, path));
                    }

                    continue;
                }

                int next;
                // Only regions placed after the ring's own start, so each ring is found from one
                // place and reported once.
                if (!rank.TryGetValue(route.To, out next) || next <= rank[start] || !onPath.Add(route.To))
                {
                    continue;
                }

                path.Add(route.To);
                Walk(routes, rank, start, route.To, path, onPath, cycles);
                path.RemoveAt(path.Count - 1);
                onPath.Remove(route.To);
            }
        }

        private static ScenarioCycle Ring(List<ScenarioRoute> routes, List<string> path)
        {
            int demanding = 0;
            for (int i = 0; i < path.Count; i++)
            {
                string from = path[i];
                string to = path[(i + 1) % path.Count];
                for (int r = 0; r < routes.Count; r++)
                {
                    ScenarioRoute route = routes[r];
                    if (string.Equals(route.From, from, StringComparison.Ordinal)
                        && string.Equals(route.To, to, StringComparison.Ordinal)
                        && !route.Free)
                    {
                        demanding++;
                        break;
                    }
                }
            }

            bool reentrant = false;
            for (int i = 0; i < path.Count; i++)
            {
                reentrant |= string.Equals(path[i], SiteGrammar.Outside, StringComparison.Ordinal);
            }

            return new ScenarioCycle(
                reentrant ? ScenarioCycleKind.Reentrant : ScenarioCycleKind.Internal,
                new List<string>(path).AsReadOnly(),
                demanding);
        }

        // -- sockets ---------------------------------------------------------------------------

        private static List<ScenarioSocket> Sockets(SiteLayout layout)
        {
            List<ScenarioSocket> sockets = new List<ScenarioSocket>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                if (node.Spec.HasSocket)
                {
                    sockets.Add(new ScenarioSocket(node.Id, node.Socket, node.Required));
                }
            }

            return sockets;
        }

        // -- the invariants --------------------------------------------------------------------

        private static ScenarioValidation Validate(
            NarrativeWorldState world,
            ScenarioPlan plan,
            Dictionary<string, SiteRouteProjection> reach,
            List<ScenarioRoute> routes)
        {
            List<ScenarioFinding> findings = new List<ScenarioFinding>();

            string objective = plan.ObjectiveRegionId;
            findings.Add(objective.Length > 0
                ? new ScenarioFinding(ScenarioInvariant.ObjectiveAnchored, true,
                    objective + " answers " + plan.Objective)
                : new ScenarioFinding(ScenarioInvariant.ObjectiveAnchored, false,
                    "no region of this place answers " + plan.Objective));

            ScenarioRegion objectiveRegion = plan.GetRegion(objective);
            findings.Add(objectiveRegion != null && objectiveRegion.Reachable
                ? new ScenarioFinding(ScenarioInvariant.ObjectiveReachable, true,
                    reach[objective].Promised.Count + " way(s) reach " + objective
                    + ", the shortest in " + objectiveRegion.Depth + " leg(s)")
                : new ScenarioFinding(ScenarioInvariant.ObjectiveReachable, false,
                    objective.Length == 0
                        ? "there is no objective region to reach"
                        : "no way to " + objective + " can be offered on this build"));

            findings.Add(Reachable(plan, ScenarioAnchorKind.Evidence, ScenarioInvariant.EvidenceReachable));
            findings.Add(Structural(plan));
            findings.Add(Supported(plan, reach, routes));
            findings.Add(Causal(world, plan));

            return new ScenarioValidation(findings.AsReadOnly());
        }

        private static ScenarioFinding Reachable(
            ScenarioPlan plan, ScenarioAnchorKind kind, ScenarioInvariant invariant)
        {
            List<ScenarioAnchor> anchors = plan.AnchorsOf(kind);
            for (int i = 0; i < anchors.Count; i++)
            {
                ScenarioRegion region = plan.GetRegion(anchors[i].RegionId);
                if (region == null || !region.Reachable)
                {
                    return new ScenarioFinding(invariant, false,
                        anchors[i].SubjectId.Value + " is anchored in " + anchors[i].RegionId
                        + ", and no way to it can be offered on this build");
                }
            }

            return new ScenarioFinding(invariant, true,
                anchors.Count + " anchor(s), every one of them in a region a promised way reaches");
        }

        private static ScenarioFinding Structural(ScenarioPlan plan)
        {
            Dictionary<string, string> byDemand = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Alternatives.Count; i++)
            {
                ScenarioAlternative alternative = plan.Alternatives[i];
                string key = alternative.TargetRegionId + "|" + alternative.Demands;
                if (byDemand.ContainsKey(key))
                {
                    return new ScenarioFinding(ScenarioInvariant.AlternativesAreStructural, false,
                        "two alternatives to " + alternative.TargetRegionId + " ask the same thing ("
                        + (alternative.Demands.Length > 0 ? alternative.Demands : "nothing")
                        + "), so one of them is the other renamed");
                }

                byDemand[key] = alternative.ToString();
            }

            return new ScenarioFinding(ScenarioInvariant.AlternativesAreStructural, true,
                plan.Alternatives.Count + " alternative(s), each asking something the others do not; "
                + plan.CollapsedAlternatives.Count + " way(s) collapsed into one of them");
        }

        /// <summary>
        /// Nothing required runs through a route this build cannot keep.
        ///
        /// The required paths are the shortest promised way to each anchor, which is the set BQ-140
        /// would have to be able to build. A supported route is not merely one somebody can walk:
        /// a leg can be walkable and still demand a hazard or a trap nobody has written a verb for,
        /// and a plan that called that required would be promising a route through a mechanic this
        /// build does not have.
        /// </summary>
        private static ScenarioFinding Supported(
            ScenarioPlan plan, Dictionary<string, SiteRouteProjection> reach, List<ScenarioRoute> routes)
        {
            for (int i = 0; i < plan.Anchors.Count; i++)
            {
                ScenarioAnchor anchor = plan.Anchors[i];
                SiteRouteProjection projection;
                if (!reach.TryGetValue(anchor.RegionId, out projection) || projection.Promised.Count == 0)
                {
                    return new ScenarioFinding(ScenarioInvariant.RequiredPathsSupported, false,
                        "nothing reaches " + anchor.RegionId + ", so there is no path to support");
                }

                SiteWayThrough shortest = projection.Promised[0];
                for (int w = 1; w < projection.Promised.Count; w++)
                {
                    if (projection.Promised[w].Legs.Count < shortest.Legs.Count)
                    {
                        shortest = projection.Promised[w];
                    }
                }

                for (int leg = 0; leg < shortest.Legs.Count; leg++)
                {
                    SiteRouteLeg step = shortest.Legs[leg];
                    ScenarioRoute route = Find(routes, step.Route);
                    if (route == null || route.Support == ScenarioSupport.Supported)
                    {
                        continue;
                    }

                    return new ScenarioFinding(ScenarioInvariant.RequiredPathsSupported, false,
                        "the way to " + anchor.RegionId + " runs " + route
                        + ", which is " + route.Support + "; " + route.Refusal);
                }
            }

            return new ScenarioFinding(ScenarioInvariant.RequiredPathsSupported, true,
                plan.Anchors.Count + " required path(s), every route on them supported on this build");
        }

        private static ScenarioRoute Find(List<ScenarioRoute> routes, SiteLayoutRoute route)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                if (ReferenceEquals(routes[i].Route, route))
                {
                    return routes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Every id the plan carries is still one the world holds.
        ///
        /// This is the transformation's own test. A plan is derived out of the ledger, the
        /// knowledge graph and the registry; if any of that stopped resolving on the way through,
        /// the plan is describing a history nobody has, and the point of anchoring by id rather
        /// than by copy is that the question can be asked at all.
        /// </summary>
        private static ScenarioFinding Causal(NarrativeWorldState world, ScenarioPlan plan)
        {
            if (world == null || world.GetThread(plan.ThreadId) == null)
            {
                return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, false,
                    "the world holds no matter " + plan.ThreadId.Value + " for this place to be for");
            }

            HashSet<EntityId> events = new HashSet<EntityId>();
            for (int i = 0; i < world.Ledger.Events.Count; i++)
            {
                events.Add(world.Ledger.Events[i].Id);
            }

            int checkedIds = 1;
            for (int i = 0; i < plan.Anchors.Count; i++)
            {
                ScenarioAnchor anchor = plan.Anchors[i];
                if (!anchor.EventId.IsNone && !events.Contains(anchor.EventId))
                {
                    return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, false,
                        anchor.Kind + " in " + anchor.RegionId + " names event " + anchor.EventId.Value
                        + ", which the ledger does not have");
                }

                if (!anchor.FactId.IsNone && world.Knowledge.GetFact(anchor.FactId) == null)
                {
                    return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, false,
                        anchor.Kind + " in " + anchor.RegionId + " proves claim " + anchor.FactId.Value
                        + ", which the knowledge graph does not have");
                }

                if (anchor.Kind == ScenarioAnchorKind.Captive && world.Registry.GetNpc(anchor.SubjectId) == null)
                {
                    return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, false,
                        "the plan holds " + anchor.SubjectId.Value + " in " + anchor.RegionId
                        + ", and the world knows no actor by that name");
                }

                checkedIds++;
            }

            for (int i = 0; i < plan.OccupantRegions.Count; i++)
            {
                IReadOnlyList<ScenarioOccupant> occupants = plan.OccupantRegions[i].Occupants;
                for (int o = 0; o < occupants.Count; o++)
                {
                    if (world.Registry.GetNpc(occupants[o].NpcId) == null)
                    {
                        return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, false,
                            plan.OccupantRegions[i].RegionId + " is occupied by " + occupants[o].NpcId.Value
                            + ", and the world knows no actor by that name");
                    }

                    checkedIds++;
                }
            }

            return new ScenarioFinding(ScenarioInvariant.CausalReferencesIntact, true,
                checkedIds + " reference(s) still resolve against the world they were derived from");
        }

        // -- identity --------------------------------------------------------------------------

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// A plan's identity: a hash over what the plan means.
        ///
        /// Deliberately computed rather than minted. A minted id would be a fact about when the
        /// plan was made; this is a fact about what it says, so the same semantic inputs and the
        /// same seed produce the same id on every machine and in every session, and two plans that
        /// differ anywhere that matters cannot share one. Nothing a renderer decides is in it,
        /// because nothing a renderer decides is in the plan. Written out by hand rather than taken
        /// from <c>string.GetHashCode</c>, which is not stable between runs.
        /// </summary>
        internal static string Identify(ScenarioPlan plan)
        {
            if (plan.Layout == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(plan.GrammarId).Append('|').Append(plan.SiteType).Append('|')
              .Append(plan.Layout.Restricted ? '1' : '0').Append('|')
              .Append(plan.Seed).Append('|').Append(plan.SelectionSeed).Append('|')
              .Append(plan.ThreadId.Value).Append('|').Append(plan.Objective).Append('\n');

            for (int i = 0; i < plan.Regions.Count; i++)
            {
                ScenarioRegion region = plan.Regions[i];
                sb.Append("r|").Append(region.Id).Append('|').Append(region.Required ? '1' : '0')
                  .Append('|').Append(ScenarioRoute.Names(region.Affordances))
                  .Append('|').Append(region.Socket).Append('\n');
            }

            for (int i = 0; i < plan.Routes.Count; i++)
            {
                ScenarioRoute route = plan.Routes[i];
                sb.Append("e|").Append(route.From).Append('|').Append(route.To).Append('|')
                  .Append(route.Demands).Append('|').Append(route.NeedsAdmission ? '1' : '0')
                  .Append('|').Append(route.Support).Append('\n');
            }

            for (int i = 0; i < plan.Anchors.Count; i++)
            {
                ScenarioAnchor anchor = plan.Anchors[i];
                sb.Append("a|").Append(anchor.Kind).Append('|').Append(anchor.RegionId).Append('|')
                  .Append(anchor.SubjectId.Value).Append('|').Append(anchor.FactId.Value)
                  .Append('|').Append(anchor.EventId.Value).Append('\n');
            }

            for (int i = 0; i < plan.OccupantRegions.Count; i++)
            {
                ScenarioOccupantRegion region = plan.OccupantRegions[i];
                sb.Append("o|").Append(region.RegionId).Append('|').Append(region.Kind).Append('|')
                  .Append(region.OrganizationId.Value);
                for (int o = 0; o < region.Occupants.Count; o++)
                {
                    sb.Append('|').Append(region.Occupants[o].NpcId.Value);
                }

                sb.Append('\n');
            }

            for (int i = 0; i < plan.Sockets.Count; i++)
            {
                sb.Append("s|").Append(plan.Sockets[i].RegionId).Append('|')
                  .Append(plan.Sockets[i].Socket).Append('\n');
            }

            ulong hash = FnvOffset;
            string text = sb.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= FnvPrime;
            }

            return "plan_" + hash.ToString("x16");
        }
    }
}
