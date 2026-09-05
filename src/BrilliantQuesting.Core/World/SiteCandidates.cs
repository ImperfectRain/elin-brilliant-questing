using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The ways a composed plan can be wrong enough not to be used (`PP §2`, `LW §7.6`).
    ///
    /// Each of these is a refusal rather than a low score, because each of them makes the place
    /// fail to be about what the matter came for. A place that is merely plain scores badly and is
    /// still offered when it is the best of the batch; a place whose objective cannot be reached is
    /// not a worse place, it is not a place for this matter at all.
    /// </summary>
    public enum SiteFlaw
    {
        /// <summary>
        /// Nothing in the plan leads from outside to what the matter came for - or the place has
        /// nowhere that answers it at all.
        /// </summary>
        UnreachableObjective,

        /// <summary>
        /// The ways in are in the wrong order relative to the objective: the place advertises a
        /// way in that waits on nobody, and every way to what the matter came for waits on
        /// somebody letting you past anyway. The second approach is then decoration, and BQ-087's
        /// two-ways-in contract is true of the place and false of the errand.
        /// </summary>
        AccessOrderingFailure,

        /// <summary>
        /// Several ways to the objective, one play: every route that can be offered is taken with
        /// the same verbs in the same order, so the alternatives are the same walk renamed.
        /// </summary>
        RoutesCollapseIntoOnePlay,

        /// <summary>
        /// A loop nobody would go round, or a shortcut that makes the rest pointless: two routes
        /// between the same two parts where one asks nothing, or a way to the objective that asks
        /// nothing of anybody anywhere along it.
        /// </summary>
        UselessLoopOrTrivialShortcut,

        /// <summary>
        /// Walking without deciding: a plan of any size with no branch anywhere in it, or one made
        /// mostly of empty rooms with nothing in them and nowhere onward.
        /// </summary>
        BacktrackingOrLowInformation,

        /// <summary>
        /// The place keeps something somewhere no promised way reaches. Distinct from an
        /// unreachable objective: what the matter came for is fine, and the rest of what the place
        /// keeps cannot be got at.
        /// </summary>
        InaccessibleEvidence,

        /// <summary>
        /// The plan holds a way to the objective and this build refused the promise: a capability
        /// the adapter does not advertise, or a primitive whose evidence never rose above being
        /// read (`D067`).
        /// </summary>
        RoutePromiseUnsupported,

        /// <summary>
        /// The plan holds a way to the objective and nothing in the action library answers what a
        /// leg of it demands. Not the build's fault and not a promise to grade - a gap in the
        /// verbs, reported as its own thing so it is not mistaken for one.
        /// </summary>
        RequirementUnanswered
    }

    /// <summary>One reason a composed plan was refused, in the words the inspector prints.</summary>
    public sealed class SiteCandidateFlaw
    {
        internal SiteCandidateFlaw(SiteFlaw kind, string reason)
        {
            Kind = kind;
            Reason = reason ?? string.Empty;
        }

        public SiteFlaw Kind { get; }

        public string Reason { get; }

        public override string ToString() => Kind + ": " + Reason;
    }

    /// <summary>
    /// What one composed plan is worth, measured rather than asserted.
    ///
    /// Six parts, `LW §7.6`'s own list, each between nothing and one, and each carrying the counts
    /// it was worked out from so the inspector can print the measurement beside the score. Equal
    /// weight, deliberately: nothing yet says a place with two ways in matters more than a place
    /// whose evidence is spread, and inventing a weighting would be inventing a claim.
    /// </summary>
    public sealed class SiteCandidateScore
    {
        internal SiteCandidateScore(
            int nodes,
            int reachableNodes,
            int promisedWays,
            int distinctPlays,
            bool admittedWay,
            bool uninvitedWay,
            int shortestWayLegs,
            int keepNodes,
            int reachableKeepNodes,
            int realAlternatives,
            int mechanics)
        {
            Nodes = nodes;
            ReachableNodes = reachableNodes;
            PromisedWays = promisedWays;
            DistinctPlays = distinctPlays;
            AdmittedWay = admittedWay;
            UninvitedWay = uninvitedWay;
            ShortestWayLegs = shortestWayLegs;
            KeepNodes = keepNodes;
            ReachableKeepNodes = reachableKeepNodes;
            RealAlternatives = realAlternatives;
            Mechanics = mechanics;

            Reachability = nodes == 0 ? 0.0 : (double)reachableNodes / nodes;
            RouteDiversity = (Capped(distinctPlays, 3) + (admittedWay && uninvitedWay ? 1.0 : 0.0)) / 2.0;
            ObjectiveSeparation = Capped(shortestWayLegs, 3);
            EvidenceDistribution = keepNodes == 0
                ? 0.0
                : (double)reachableKeepNodes / keepNodes * Capped(keepNodes, 2);
            LoopQuality = Capped(realAlternatives, 2);
            MechanicVocabulary = Capped(mechanics, 4);
        }

        // -- the six, each 0..1 ----------------------------------------------------------------

        /// <summary>How much of the place a player can actually get to on this build.</summary>
        public double Reachability { get; }

        /// <summary>
        /// How many genuinely different plays reach the objective, and whether both terms BQ-087
        /// asks of a place - let in, and not - are among them.
        /// </summary>
        public double RouteDiversity { get; }

        /// <summary>How far in what the matter came for is. A cache by the door is worth little.</summary>
        public double ObjectiveSeparation { get; }

        /// <summary>How much of what the place keeps is somewhere reachable, and in how many places.</summary>
        public double EvidenceDistribution { get; }

        /// <summary>
        /// Alternatives that are worth having: two routes between the same parts that ask
        /// different things, and a way out that is not the way in.
        /// </summary>
        public double LoopQuality { get; }

        /// <summary>
        /// How many different spatial requirements the promised ways actually exercise with a verb
        /// that answers them. A requirement nobody answers is not vocabulary (`D067`).
        /// </summary>
        public double MechanicVocabulary { get; }

        /// <summary>The six, averaged. Selection compares this and nothing else.</summary>
        public double Total =>
            (Reachability + RouteDiversity + ObjectiveSeparation
             + EvidenceDistribution + LoopQuality + MechanicVocabulary) / 6.0;

        // -- what each was worked out from -----------------------------------------------------

        public int Nodes { get; }

        public int ReachableNodes { get; }

        public int PromisedWays { get; }

        public int DistinctPlays { get; }

        public bool AdmittedWay { get; }

        public bool UninvitedWay { get; }

        public int ShortestWayLegs { get; }

        /// <summary>Parts of the place that hold something: what it keeps, and who it holds.</summary>
        public int KeepNodes { get; }

        public int ReachableKeepNodes { get; }

        public int RealAlternatives { get; }

        public int Mechanics { get; }

        private static double Capped(int value, int cap)
        {
            if (value <= 0)
            {
                return 0.0;
            }

            return value >= cap ? 1.0 : (double)value / cap;
        }
    }

    /// <summary>
    /// One composed plan weighed for one errand: what it scored, and every reason it cannot be
    /// used.
    /// </summary>
    public sealed class SitePlanCandidate
    {
        private static readonly SiteCandidateFlaw[] Nothing = new SiteCandidateFlaw[0];

        internal SitePlanCandidate(
            ulong seed,
            SiteLayout layout,
            SiteAffordance objective,
            string objectiveNodeId,
            SiteRouteProjection toObjective,
            IReadOnlyList<SiteCandidateFlaw> flaws,
            SiteCandidateScore score)
        {
            Seed = seed;
            Layout = layout;
            Objective = objective;
            ObjectiveNodeId = objectiveNodeId ?? string.Empty;
            ToObjective = toObjective;
            Flaws = flaws ?? Nothing;
            Score = score;
        }

        public ulong Seed { get; }

        public SiteLayout Layout { get; }

        /// <summary>What the matter came for, as a requirement rather than as a room.</summary>
        public SiteAffordance Objective { get; }

        /// <summary>The part of this place that answers it. Empty when it has none.</summary>
        public string ObjectiveNodeId { get; }

        /// <summary>The ways to it, promised and refused alike. Null when the place has no objective.</summary>
        public SiteRouteProjection ToObjective { get; }

        public IReadOnlyList<SiteCandidateFlaw> Flaws { get; }

        public SiteCandidateScore Score { get; }

        public bool Usable => Flaws.Count == 0;

        /// <summary>Set on the one candidate selection landed on, so a trace can mark it.</summary>
        public bool Chosen { get; internal set; }
    }

    /// <summary>Which of several composed plans a matter should be given, and why not the others.</summary>
    public sealed class SiteCandidateSelection
    {
        private static readonly SitePlanCandidate[] NothingConsidered = new SitePlanCandidate[0];

        internal SiteCandidateSelection(
            SiteGrammar grammar,
            SiteAffordance objective,
            ulong seed,
            IReadOnlyList<SitePlanCandidate> considered,
            SitePlanCandidate chosen,
            string refusal)
        {
            Grammar = grammar;
            Objective = objective;
            Seed = seed;
            Considered = considered ?? NothingConsidered;
            Chosen = chosen;
            Refusal = refusal ?? string.Empty;
        }

        public SiteGrammar Grammar { get; }

        public SiteAffordance Objective { get; }

        /// <summary>The seed the batch was drawn from. The same seed draws the same batch.</summary>
        public ulong Seed { get; }

        /// <summary>Every plan weighed, in the order they were drawn.</summary>
        public IReadOnlyList<SitePlanCandidate> Considered { get; }

        /// <summary>The one to use, or null when every candidate was refused.</summary>
        public SitePlanCandidate Chosen { get; }

        /// <summary>Why there is nothing to use. Empty when there is.</summary>
        public string Refusal { get; }

        public bool Selected => Chosen != null;

        public SiteLayout Layout => Chosen == null ? null : Chosen.Layout;

        /// <summary>
        /// The chosen plan in the shape genesis validates and reuse weighs, or null when nothing
        /// was chosen. The same exit BQ-089 gives a single composition - selection does not get a
        /// second vocabulary for "what a place must be".
        /// </summary>
        public SitePlan NewPlan(EntityId siteId, string name, EntityId threadId)
        {
            return Chosen == null ? null : Chosen.Layout.NewPlan(siteId, name, threadId);
        }
    }

    /// <summary>
    /// BQ-092. Draw several plans for a place, refuse the ones that are wrong for the errand, and
    /// take the best of what is left.
    ///
    /// "Generate, score, validate" (`PP §2`, `LW §7.6`) needs something to vary and something to
    /// judge against. What varies is BQ-089's seed: every required part is in every composition and
    /// the optional ones are drawn, so a batch of seeds is a batch of genuinely different places of
    /// one kind. What it is judged against is the errand rather than the kind - the objective is
    /// passed in as a requirement (`EvidenceCache` for what a place keeps, `PrisonCell` for who it
    /// holds), and the same grammar is a good plan for one errand and a refused one for another.
    /// Naming the objective as an affordance rather than as a room id keeps a storylet, an
    /// archetype or a matter from having to know a grammar's node names.
    ///
    /// Nothing here reads world state and nothing here is content. Scoring judges the *shape* of a
    /// plan and the promises this build can keep; what the place then holds is the situation's, and
    /// <see cref="SiteContents"/> derives it once a plan has been chosen (`D068`).
    ///
    /// A refusal is not a low score. Six qualities are measured and averaged, and the best average
    /// wins; but a plan whose objective cannot be reached, whose alternate routes are one route
    /// renamed, or whose promise this build cannot keep is refused outright with the reason,
    /// because those are not places that are worse - they are places this errand cannot happen in.
    /// </summary>
    public static class SiteCandidates
    {
        /// <summary>
        /// How many plans are drawn when a caller does not say. Enough that the optional parts
        /// differ across the batch, few enough that weighing them is a handful of graph walks.
        /// </summary>
        public const int DefaultBatch = 6;

        /// <summary>
        /// A plan for this errand, drawn from this seed, or the reasons there is none.
        ///
        /// The batch is deterministic: the same grammar, seed, count and build draw the same
        /// candidates with the same scores and the same refusals, so a selected place replays
        /// exactly (`PP §8`).
        /// </summary>
        public static SiteCandidateSelection Select(
            SiteGrammar grammar,
            SiteAffordance objective,
            ulong seed,
            int batch,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            if (grammar == null)
            {
                return new SiteCandidateSelection(
                    null, objective, seed, null, null, "there is no grammar to draw plans from");
            }

            if (batch <= 0)
            {
                return new SiteCandidateSelection(
                    grammar, objective, seed, null, null, "no plans were asked for");
            }

            DeterministicRng stream = new DeterministicRng(seed).Fork("site-candidates").Fork(grammar.Id);

            List<SitePlanCandidate> considered = new List<SitePlanCandidate>();
            SitePlanCandidate best = null;
            for (int i = 0; i < batch; i++)
            {
                SitePlanCandidate candidate = Weigh(
                    grammar.Compose(stream.NextUInt64()), objective, actions, vanilla);
                considered.Add(candidate);

                // Ties go to the plan drawn first, so a batch has one answer rather than an
                // answer that depends on which comparison ran.
                if (candidate.Usable && (best == null || candidate.Score.Total > best.Score.Total))
                {
                    best = candidate;
                }
            }

            if (best == null)
            {
                return new SiteCandidateSelection(
                    grammar,
                    objective,
                    seed,
                    considered,
                    null,
                    "none of the " + considered.Count + " plan(s) drawn can host an errand after "
                    + objective);
            }

            best.Chosen = true;
            return new SiteCandidateSelection(grammar, objective, seed, considered, best, string.Empty);
        }

        /// <summary>
        /// One composed plan, weighed for one errand. Reads the plan and the action library and
        /// changes nothing.
        /// </summary>
        public static SitePlanCandidate Weigh(
            SiteLayout layout,
            SiteAffordance objective,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            if (layout == null)
            {
                return new SitePlanCandidate(
                    0,
                    null,
                    objective,
                    string.Empty,
                    null,
                    new List<SiteCandidateFlaw>
                    {
                        new SiteCandidateFlaw(SiteFlaw.UnreachableObjective, "there is no plan to weigh")
                    }.AsReadOnly(),
                    new SiteCandidateScore(0, 0, 0, 0, false, false, 0, 0, 0, 0, 0));
            }

            List<SiteCandidateFlaw> flaws = new List<SiteCandidateFlaw>();
            string objectiveNode = NodeAnswering(layout, objective);

            // Every part of the place, once: reachability, evidence access and the objective all
            // ask the same question of different nodes.
            Dictionary<string, SiteRouteProjection> reach = new Dictionary<string, SiteRouteProjection>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                string id = layout.Nodes[i].Id;
                reach[id] = SiteRoutes.Project(layout, id, actions, vanilla);
            }

            SiteRouteProjection toObjective = null;
            if (objectiveNode.Length == 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.UnreachableObjective,
                    "this place has nowhere that answers " + objective));
            }
            else
            {
                toObjective = reach[objectiveNode];
                Judge(layout, objective, objectiveNode, toObjective, reach, flaws);
            }

            JudgeShape(layout, flaws);

            return new SitePlanCandidate(
                layout.Seed,
                layout,
                objective,
                objectiveNode,
                toObjective,
                flaws.AsReadOnly(),
                Measure(layout, objectiveNode, toObjective, reach));
        }

        // -- the refusals ----------------------------------------------------------------------

        private static void Judge(
            SiteLayout layout,
            SiteAffordance objective,
            string objectiveNode,
            SiteRouteProjection toObjective,
            Dictionary<string, SiteRouteProjection> reach,
            List<SiteCandidateFlaw> flaws)
        {
            // The usual unreachable objective is the one above: a place with nowhere that answers
            // the errand at all. A part that is in the plan and has nothing leading to it is
            // something BQ-089's composition already drops rather than composes, so this is the
            // invariant carried into the report rather than a case a grammar can produce - and if
            // it ever does fire, the plan is wrong in a way worth reading about.
            if (toObjective.Ways.Count == 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.UnreachableObjective,
                    "nothing in the plan leads from outside to " + objectiveNode
                    + ", which is where this place answers " + objective));
            }
            else
            {
                JudgeAccessOrdering(layout, objectiveNode, toObjective, flaws);
                JudgePromises(objectiveNode, toObjective, flaws);
                JudgePlays(objectiveNode, toObjective, flaws);
                JudgeShortcut(objectiveNode, toObjective, flaws);
            }

            // What else the place keeps. The objective is judged on its own terms above, so an
            // errand after the cache does not hear about the cache twice.
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                if (string.Equals(node.Id, objectiveNode, StringComparison.Ordinal)
                    || !Answers(node.Affordances, SiteAffordance.EvidenceCache))
                {
                    continue;
                }

                if (reach[node.Id].Promised.Count == 0)
                {
                    flaws.Add(new SiteCandidateFlaw(
                        SiteFlaw.InaccessibleEvidence,
                        "the place keeps something in " + node.Id + " and no way to it can be offered"));
                }
            }
        }

        /// <summary>
        /// The plan says you can get in without asking and the errand says otherwise. BQ-087's two
        /// ways in are a promise about the place; this is that promise measured against what the
        /// matter actually came for.
        /// </summary>
        private static void JudgeAccessOrdering(
            SiteLayout layout,
            string objectiveNode,
            SiteRouteProjection toObjective,
            List<SiteCandidateFlaw> flaws)
        {
            string uninvited = string.Empty;
            for (int i = 0; i < layout.Approaches.Count; i++)
            {
                if (!layout.Approaches[i].NeedsAdmission)
                {
                    uninvited = layout.Approaches[i].ActionId;
                    break;
                }
            }

            if (uninvited.Length == 0)
            {
                return;
            }

            for (int i = 0; i < toObjective.Ways.Count; i++)
            {
                if (!toObjective.Ways[i].NeedsAdmission)
                {
                    return;
                }
            }

            flaws.Add(new SiteCandidateFlaw(
                SiteFlaw.AccessOrderingFailure,
                "the plan offers a way in that waits on nobody (" + uninvited
                + ") and every one of the " + toObjective.Ways.Count + " way(s) to " + objectiveNode
                + " waits on somebody letting you past, so the uninvited way in reaches nothing this errand wants"));
        }

        /// <summary>
        /// A plan with a way to the objective and no way this build can keep. The two causes are
        /// separated because they are fixed by different people: a refused promise is the build's
        /// answer (`D067`), and an unanswered requirement is a verb nobody has written.
        /// </summary>
        private static void JudgePromises(
            string objectiveNode,
            SiteRouteProjection toObjective,
            List<SiteCandidateFlaw> flaws)
        {
            if (toObjective.Promised.Count > 0)
            {
                return;
            }

            string unsupported = string.Empty;
            string unanswered = string.Empty;
            for (int i = 0; i < toObjective.Ways.Count; i++)
            {
                IReadOnlyList<SiteRouteLeg> legs = toObjective.Ways[i].Legs;
                for (int l = 0; l < legs.Count; l++)
                {
                    SiteRouteLeg leg = legs[l];
                    if (leg.Promised)
                    {
                        continue;
                    }

                    for (int v = 0; v < leg.Verbs.Count && unsupported.Length == 0; v++)
                    {
                        SiteRouteVerb verb = leg.Verbs[v];
                        if (!verb.Promised && verb.Claim != null)
                        {
                            unsupported = leg.From + " -> " + leg.To + " by " + verb.ActionId
                                          + ": " + verb.Refusal;
                        }
                    }

                    if (unanswered.Length == 0 && leg.Unanswered.Count > 0)
                    {
                        unanswered = leg.From + " -> " + leg.To + " demands " + leg.Unanswered[0];
                    }
                }
            }

            if (unsupported.Length > 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.RoutePromiseUnsupported,
                    "no way to " + objectiveNode + " can be offered on this build; " + unsupported));
            }

            if (unanswered.Length > 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.RequirementUnanswered,
                    "no way to " + objectiveNode + " can be offered; " + unanswered
                    + " and nothing in the action library answers it"));
            }

            if (unsupported.Length == 0 && unanswered.Length == 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.RoutePromiseUnsupported,
                    "no way to " + objectiveNode + " can be offered on this build; "
                    + (toObjective.Ways.Count > 0 ? toObjective.Ways[0].Refusal : "no reason was recorded")));
            }
        }

        /// <summary>
        /// Several ways and one play. `Vocabulary` records what each leg is taken with; a leg
        /// nobody has to get past contributes nothing to the play, because walking one room
        /// further is not a decision.
        /// </summary>
        private static void JudgePlays(
            string objectiveNode,
            SiteRouteProjection toObjective,
            List<SiteCandidateFlaw> flaws)
        {
            if (toObjective.Promised.Count < 2)
            {
                return;
            }

            HashSet<string> plays = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < toObjective.Promised.Count; i++)
            {
                plays.Add(Play(toObjective.Promised[i]));
            }

            if (plays.Count > 1)
            {
                return;
            }

            string only = string.Empty;
            foreach (string play in plays)
            {
                only = play;
            }

            flaws.Add(new SiteCandidateFlaw(
                SiteFlaw.RoutesCollapseIntoOnePlay,
                toObjective.Promised.Count + " ways to " + objectiveNode + " and one play: every one of them is "
                + (only.Length > 0 ? only : "walked with nothing to get past")));
        }

        /// <summary>
        /// What the place keeps, lying where anybody can pick it up. A way whose every leg asks
        /// nothing of anybody makes the rest of the plan decoration.
        /// </summary>
        private static void JudgeShortcut(
            string objectiveNode,
            SiteRouteProjection toObjective,
            List<SiteCandidateFlaw> flaws)
        {
            for (int i = 0; i < toObjective.Promised.Count; i++)
            {
                SiteWayThrough way = toObjective.Promised[i];
                bool asksNothing = true;
                for (int l = 0; l < way.Legs.Count && asksNothing; l++)
                {
                    asksNothing = Demands(way.Legs[l].Route).Length == 0;
                }

                if (asksNothing)
                {
                    flaws.Add(new SiteCandidateFlaw(
                        SiteFlaw.UselessLoopOrTrivialShortcut,
                        "a trivial shortcut: " + way + " reaches " + objectiveNode
                        + " without anybody being asked anything"));
                    return;
                }
            }
        }

        /// <summary>
        /// What the plan looks like, judged without reference to any errand: a duplicate route
        /// that asks nothing, a plan with no branch in it, and a plan made of empty dead ends.
        /// </summary>
        private static void JudgeShape(SiteLayout layout, List<SiteCandidateFlaw> flaws)
        {
            string duplicated = FreeDuplicate(layout);
            if (duplicated.Length > 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.UselessLoopOrTrivialShortcut,
                    "a useless loop: " + duplicated
                    + " is in the plan twice and one of them asks nothing, so the other is never worth taking"));
            }

            if (layout.Nodes.Count < MinimumShapedPlan)
            {
                return;
            }

            int branches = 0;
            Dictionary<string, int> outgoing = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                string from = layout.Routes[i].From;
                int count;
                outgoing[from] = (outgoing.TryGetValue(from, out count) ? count : 0) + 1;
            }

            foreach (KeyValuePair<string, int> pair in outgoing)
            {
                if (pair.Value > 1)
                {
                    branches++;
                }
            }

            if (branches == 0)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.BacktrackingOrLowInformation,
                    "a low-information corridor: " + layout.Nodes.Count
                    + " parts and not one of them offers a choice of where to go next"));
            }

            int empty = 0;
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                int onward;
                if (!outgoing.TryGetValue(node.Id, out onward))
                {
                    onward = 0;
                }

                if (onward == 0 && node.Affordances.Count == 0 && !node.Spec.HasSocket)
                {
                    empty++;
                }
            }

            if (empty * EmptyDeadEndDenominator >= layout.Nodes.Count * EmptyDeadEndNumerator)
            {
                flaws.Add(new SiteCandidateFlaw(
                    SiteFlaw.BacktrackingOrLowInformation,
                    "pathological backtracking: " + empty + " of " + layout.Nodes.Count
                    + " parts are walked into and back out of with nothing in them and nowhere onward"));
            }
        }

        /// <summary>
        /// Two routes between the same two parts where one asks nothing. The demanding one is then
        /// never worth taking, which makes it length rather than a choice.
        /// </summary>
        private static string FreeDuplicate(SiteLayout layout)
        {
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute first = layout.Routes[i];
                for (int j = i + 1; j < layout.Routes.Count; j++)
                {
                    SiteLayoutRoute second = layout.Routes[j];
                    if (string.Equals(first.From, second.From, StringComparison.Ordinal)
                        && string.Equals(first.To, second.To, StringComparison.Ordinal)
                        && (Demands(first).Length == 0 || Demands(second).Length == 0))
                    {
                        return first.From + " -> " + first.To;
                    }
                }
            }

            return string.Empty;
        }

        // -- the measurements ------------------------------------------------------------------

        private static SiteCandidateScore Measure(
            SiteLayout layout,
            string objectiveNode,
            SiteRouteProjection toObjective,
            Dictionary<string, SiteRouteProjection> reach)
        {
            int reachable = 0;
            int keep = 0;
            int keepReachable = 0;
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                SiteLayoutNode node = layout.Nodes[i];
                bool offered = reach[node.Id].Promised.Count > 0;
                if (offered)
                {
                    reachable++;
                }

                if (Answers(node.Affordances, SiteAffordance.EvidenceCache)
                    || Answers(node.Affordances, SiteAffordance.PrisonCell))
                {
                    keep++;
                    if (offered)
                    {
                        keepReachable++;
                    }
                }
            }

            HashSet<string> plays = new HashSet<string>(StringComparer.Ordinal);
            HashSet<SiteAffordance> mechanics = new HashSet<SiteAffordance>();
            bool admitted = false;
            bool uninvited = false;
            int shortest = 0;
            int promised = toObjective == null ? 0 : toObjective.Promised.Count;
            for (int i = 0; i < promised; i++)
            {
                SiteWayThrough way = toObjective.Promised[i];
                plays.Add(Play(way));
                admitted |= way.NeedsAdmission;
                uninvited |= !way.NeedsAdmission;
                if (shortest == 0 || way.Legs.Count < shortest)
                {
                    shortest = way.Legs.Count;
                }

                for (int l = 0; l < way.Legs.Count; l++)
                {
                    SiteRouteLeg leg = way.Legs[l];
                    for (int a = 0; a < leg.Route.Affordances.Count; a++)
                    {
                        SiteAffordance affordance = leg.Route.Affordances[a];
                        if (Answered(leg, affordance))
                        {
                            mechanics.Add(affordance);
                        }
                    }
                }
            }

            return new SiteCandidateScore(
                layout.Nodes.Count,
                reachable,
                promised,
                plays.Count,
                admitted,
                uninvited,
                shortest,
                keep,
                keepReachable,
                Alternatives(layout),
                mechanics.Count);
        }

        /// <summary>
        /// Alternatives worth having: two routes between the same parts asking different things,
        /// and a way out that is not a way in. A second route that asks the same as the first is
        /// not counted, which is the same judgement <see cref="JudgePlays"/> makes of two ways.
        /// </summary>
        private static int Alternatives(SiteLayout layout)
        {
            int count = 0;
            HashSet<string> counted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute first = layout.Routes[i];
                for (int j = i + 1; j < layout.Routes.Count; j++)
                {
                    SiteLayoutRoute second = layout.Routes[j];
                    if (!string.Equals(first.From, second.From, StringComparison.Ordinal)
                        || !string.Equals(first.To, second.To, StringComparison.Ordinal)
                        || string.Equals(Demands(first), Demands(second), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (counted.Add(first.From + " -> " + first.To))
                    {
                        count++;
                    }
                }

                if (first.IsExit && !first.IsEntry)
                {
                    count++;
                }
            }

            return count;
        }

        // -- reading the plan ------------------------------------------------------------------

        /// <summary>
        /// What the way is actually taken with, legs that ask nothing left out. Two ways that
        /// differ only in how many empty rooms they cross are the same play, because walking one
        /// room further is not a decision anybody makes.
        /// </summary>
        private static string Play(SiteWayThrough way)
        {
            List<string> steps = new List<string>();
            List<string> vocabulary = way.Vocabulary();
            for (int i = 0; i < vocabulary.Count; i++)
            {
                if (vocabulary[i].Length > 0)
                {
                    steps.Add(vocabulary[i]);
                }
            }

            return string.Join(" then ", steps.ToArray());
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

        private static bool Answered(SiteRouteLeg leg, SiteAffordance affordance)
        {
            for (int i = 0; i < leg.Unanswered.Count; i++)
            {
                if (leg.Unanswered[i] == affordance)
                {
                    return false;
                }
            }

            for (int i = 0; i < leg.Verbs.Count; i++)
            {
                SiteRouteVerb verb = leg.Verbs[i];
                if (verb.Promised && verb.Claim != null && verb.Claim.Covers(affordance))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NodeAnswering(SiteLayout layout, SiteAffordance affordance)
        {
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                if (Answers(layout.Nodes[i].Affordances, affordance))
                {
                    return layout.Nodes[i].Id;
                }
            }

            return string.Empty;
        }

        private static bool Answers(IReadOnlyList<SiteAffordance> affordances, SiteAffordance wanted)
        {
            for (int i = 0; i < affordances.Count; i++)
            {
                if (affordances[i] == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Below this a plan is too small to have a shape worth complaining about: a place of two
        /// rooms has no branch because there is nowhere to branch to.
        /// </summary>
        private const int MinimumShapedPlan = 4;

        // Two thirds. A dead end with nothing in it is a flaw in any plan and a scored one; a plan
        // that is mostly them is a plan the player walks in and out of for nothing.
        private const int EmptyDeadEndNumerator = 2;
        private const int EmptyDeadEndDenominator = 3;
    }
}
