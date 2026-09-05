using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>One verb offered for one leg of a way through, and what the build said about it.</summary>
    public sealed class SiteRouteVerb
    {
        internal SiteRouteVerb(string actionId, SpatialRouteClaim claim, bool authored, bool promised, string refusal)
        {
            ActionId = actionId ?? string.Empty;
            Claim = claim;
            Authored = authored;
            Promised = promised;
            Refusal = refusal ?? string.Empty;
        }

        public string ActionId { get; }

        /// <summary>What the verb claims about being a way through. Null when it claims nothing.</summary>
        public SpatialRouteClaim Claim { get; }

        /// <summary>The grammar named this verb for this route, rather than it being found by requirement.</summary>
        public bool Authored { get; }

        public bool Promised { get; }

        public string Refusal { get; }

        public override string ToString()
        {
            return ActionId + (Promised ? string.Empty : " (" + Refusal + ")");
        }
    }

    /// <summary>
    /// One leg of a way through a place: a route in the plan, and the verbs that can take it on
    /// this build.
    /// </summary>
    public sealed class SiteRouteLeg
    {
        internal SiteRouteLeg(
            SiteLayoutRoute route,
            IEnumerable<SiteRouteVerb> verbs,
            IEnumerable<SiteAffordance> unanswered,
            bool promised,
            string refusal)
        {
            Route = route;
            Verbs = new List<SiteRouteVerb>(verbs ?? new SiteRouteVerb[0]).AsReadOnly();
            Unanswered = new List<SiteAffordance>(unanswered ?? new SiteAffordance[0]).AsReadOnly();
            Promised = promised;
            Refusal = refusal ?? string.Empty;
        }

        public SiteLayoutRoute Route { get; }

        public string From => Route.From;

        public string To => Route.To;

        /// <summary>Every verb considered for this leg, promised or refused, with its reason.</summary>
        public IReadOnlyList<SiteRouteVerb> Verbs { get; }

        /// <summary>
        /// Requirements this leg states that nothing in the action library answers. A leg can
        /// still be takeable with one of these outstanding - a trap does not shut a door - and the
        /// gap is reported rather than hidden, because it is the same gap a later step has to
        /// close.
        /// </summary>
        public IReadOnlyList<SiteAffordance> Unanswered { get; }

        public bool Promised { get; }

        public string Refusal { get; }

        /// <summary>The verbs this build could actually take this leg with, in registry order.</summary>
        public List<string> PromisedVerbs()
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < Verbs.Count; i++)
            {
                if (Verbs[i].Promised)
                {
                    ids.Add(Verbs[i].ActionId);
                }
            }

            return ids;
        }
    }

    /// <summary>One whole way from outside a place to the part of it that was asked about.</summary>
    public sealed class SiteWayThrough
    {
        internal SiteWayThrough(IEnumerable<SiteRouteLeg> legs)
        {
            Legs = new List<SiteRouteLeg>(legs ?? new SiteRouteLeg[0]).AsReadOnly();

            bool promised = true;
            string refusal = string.Empty;
            bool admission = false;
            for (int i = 0; i < Legs.Count; i++)
            {
                SiteRouteLeg leg = Legs[i];
                admission |= leg.Route.NeedsAdmission;
                if (!leg.Promised && promised)
                {
                    promised = false;
                    refusal = leg.From + " -> " + leg.To + ": " + leg.Refusal;
                }
            }

            Promised = promised;
            Refusal = refusal;
            NeedsAdmission = admission;
        }

        public IReadOnlyList<SiteRouteLeg> Legs { get; }

        /// <summary>The way in this starts with, which is the leg out of everywhere else.</summary>
        public SiteRouteLeg Entry => Legs.Count > 0 ? Legs[0] : null;

        /// <summary>Somewhere along it, getting through waits on somebody letting you.</summary>
        public bool NeedsAdmission { get; }

        public bool Promised { get; }

        /// <summary>The first leg that stopped it, and why. Empty while it is promised.</summary>
        public string Refusal { get; }

        /// <summary>
        /// The verbs this way is taken with, one leg at a time, empty where a leg has nothing to
        /// get past. Two ways with the same verbs in the same order are the same play; scoring how
        /// different two of them really are is BQ-092's.
        /// </summary>
        public List<string> Vocabulary()
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < Legs.Count; i++)
            {
                List<string> promised = Legs[i].PromisedVerbs();
                ids.Add(promised.Count > 0 ? string.Join("|", promised.ToArray()) : string.Empty);
            }

            return ids;
        }

        public override string ToString()
        {
            // The verb belongs in the line: two ways can run through the same parts of a place
            // and be different plays, which is the whole reason a plan has several routes between
            // one part and the next.
            List<string> steps = new List<string>();
            for (int i = 0; i < Legs.Count; i++)
            {
                SiteRouteLeg leg = Legs[i];
                steps.Add(leg.From + " -> " + leg.To
                          + (leg.Route.ActionId.Length > 0 ? " by " + leg.Route.ActionId : string.Empty));
            }

            return string.Join(", ", steps.ToArray());
        }
    }

    /// <summary>Every way through a place to one part of it, promised and refused alike.</summary>
    public sealed class SiteRouteProjection
    {
        internal SiteRouteProjection(SiteLayout layout, string objective, IEnumerable<SiteWayThrough> ways, string refusal)
        {
            Layout = layout;
            Objective = objective ?? string.Empty;
            Ways = new List<SiteWayThrough>(ways ?? new SiteWayThrough[0]).AsReadOnly();
            Refusal = refusal ?? string.Empty;

            List<SiteWayThrough> promised = new List<SiteWayThrough>();
            for (int i = 0; i < Ways.Count; i++)
            {
                if (Ways[i].Promised)
                {
                    promised.Add(Ways[i]);
                }
            }

            Promised = promised.AsReadOnly();
        }

        public SiteLayout Layout { get; }

        /// <summary>The part of the place the ways lead to.</summary>
        public string Objective { get; }

        public IReadOnlyList<SiteWayThrough> Ways { get; }

        /// <summary>The ways this build can actually be offered.</summary>
        public IReadOnlyList<SiteWayThrough> Promised { get; }

        /// <summary>Why there is nothing to project at all. Empty when the plan was read.</summary>
        public string Refusal { get; }
    }

    /// <summary>
    /// BQ-090. Reading a place's plan as ways the player can actually take, and refusing the ones
    /// this build cannot keep.
    ///
    /// BQ-089 left a vocabulary of *requirements*: a route saying it is a locked barrier says what
    /// the place must be like, not that anybody can pick it. This is the other half - who in the
    /// action library answers each requirement, what that verb leans on from the live build, and
    /// whether the build has said it can do it. A way through is promised only when every leg of
    /// it has a verb that can be taken; anything else is refused with the leg and the reason, which
    /// is the same explanation BQ-092 will score candidates with.
    ///
    /// Nothing here decides an option a player sees. Availability is still the action library's
    /// question, asked against a real world with a real barrier in it and revalidated at click
    /// time; this is the earlier question of whether the route could be offered on this build at
    /// all, which is the only one an abstract plan can answer.
    /// </summary>
    public static class SiteRoutes
    {
        public static SiteRouteProjection Project(
            SiteLayout layout,
            string objectiveNodeId,
            ActionRegistry actions,
            IVanillaState vanilla)
        {
            if (layout == null)
            {
                return new SiteRouteProjection(null, objectiveNodeId, null, "there is no plan to read");
            }

            if (string.IsNullOrEmpty(objectiveNodeId)
                || string.Equals(objectiveNodeId, SiteGrammar.Outside, StringComparison.Ordinal))
            {
                return new SiteRouteProjection(layout, objectiveNodeId, null, "no part of the place was asked about");
            }

            if (!layout.Has(objectiveNodeId))
            {
                return new SiteRouteProjection(
                    layout, objectiveNodeId, null, "this place has no " + objectiveNodeId);
            }

            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs = new Dictionary<SiteLayoutRoute, SiteRouteLeg>();
            List<SiteWayThrough> ways = new List<SiteWayThrough>();
            List<SiteLayoutRoute> path = new List<SiteLayoutRoute>();
            HashSet<string> walked = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };

            Walk(layout, SiteGrammar.Outside, objectiveNodeId, actions, vanilla, legs, path, walked, ways);

            return new SiteRouteProjection(layout, objectiveNodeId, ways, string.Empty);
        }

        private static void Walk(
            SiteLayout layout,
            string from,
            string objective,
            ActionRegistry actions,
            IVanillaState vanilla,
            Dictionary<SiteLayoutRoute, SiteRouteLeg> legs,
            List<SiteLayoutRoute> path,
            HashSet<string> walked,
            List<SiteWayThrough> ways)
        {
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                SiteLayoutRoute route = layout.Routes[i];
                if (!string.Equals(route.From, from, StringComparison.Ordinal) || !walked.Add(route.To))
                {
                    continue;
                }

                path.Add(route);

                if (string.Equals(route.To, objective, StringComparison.Ordinal))
                {
                    List<SiteRouteLeg> way = new List<SiteRouteLeg>();
                    for (int step = 0; step < path.Count; step++)
                    {
                        way.Add(LegFor(path[step], actions, vanilla, legs));
                    }

                    ways.Add(new SiteWayThrough(way));
                }
                else
                {
                    Walk(layout, route.To, objective, actions, vanilla, legs, path, walked, ways);
                }

                path.RemoveAt(path.Count - 1);
                walked.Remove(route.To);
            }
        }

        private static SiteRouteLeg LegFor(
            SiteLayoutRoute route,
            ActionRegistry actions,
            IVanillaState vanilla,
            Dictionary<SiteLayoutRoute, SiteRouteLeg> cache)
        {
            SiteRouteLeg cached;
            if (cache.TryGetValue(route, out cached))
            {
                return cached;
            }

            SiteRouteLeg leg = Evaluate(route, actions, vanilla);
            cache[route] = leg;
            return leg;
        }

        private static SiteRouteLeg Evaluate(SiteLayoutRoute route, ActionRegistry actions, IVanillaState vanilla)
        {
            List<SiteRouteVerb> verbs = new List<SiteRouteVerb>();
            HashSet<string> considered = new HashSet<string>(StringComparer.Ordinal);

            if (route.ActionId.Length > 0)
            {
                considered.Add(route.ActionId);
                verbs.Add(Offer(route.ActionId, actions, vanilla, true));
            }

            // Every registered verb that answers something this route demands. Derived rather than
            // authored: a grammar names the verb it was written around, and a build where another
            // verb answers the same requirement should be able to say so without the grammar
            // being rewritten.
            List<SiteAffordance> unanswered = new List<SiteAffordance>();
            for (int i = 0; i < route.Affordances.Count; i++)
            {
                SiteAffordance affordance = route.Affordances[i];
                bool answered = false;
                IReadOnlyList<NarrativeAction> registered = actions == null
                    ? new NarrativeAction[0]
                    : actions.Actions;

                for (int a = 0; a < registered.Count; a++)
                {
                    ISpatialRouteVerb verb = registered[a] as ISpatialRouteVerb;
                    if (verb == null || verb.SpatialRoute == null || !verb.SpatialRoute.Covers(affordance))
                    {
                        continue;
                    }

                    answered = true;
                    if (considered.Add(registered[a].Id))
                    {
                        verbs.Add(Offer(registered[a].Id, actions, vanilla, false));
                    }
                }

                if (!answered)
                {
                    unanswered.Add(affordance);
                }
            }

            bool promised = false;
            for (int i = 0; i < verbs.Count; i++)
            {
                promised |= verbs[i].Promised;
            }

            if (verbs.Count == 0)
            {
                // Nothing to get past and nobody named to get past it: the parts are simply next
                // to each other, which is most of what a plan says about a place.
                return unanswered.Count == 0
                    ? new SiteRouteLeg(route, verbs, unanswered, true, string.Empty)
                    : new SiteRouteLeg(route, verbs, unanswered, false,
                        "nothing in the action library answers " + Describe(unanswered));
            }

            if (promised)
            {
                return new SiteRouteLeg(route, verbs, unanswered, true, string.Empty);
            }

            List<string> reasons = new List<string>();
            for (int i = 0; i < verbs.Count; i++)
            {
                reasons.Add(verbs[i].ActionId + ": " + verbs[i].Refusal);
            }

            return new SiteRouteLeg(route, verbs, unanswered, false, string.Join("; ", reasons.ToArray()));
        }

        private static SiteRouteVerb Offer(string actionId, ActionRegistry actions, IVanillaState vanilla, bool authored)
        {
            NarrativeAction action = actions == null ? null : actions.Get(actionId);
            if (action == null)
            {
                return new SiteRouteVerb(actionId, null, authored, false, "no verb by that name is registered");
            }

            ISpatialRouteVerb spatial = action as ISpatialRouteVerb;
            if (spatial == null || spatial.SpatialRoute == null)
            {
                return new SiteRouteVerb(actionId, null, authored, false, "it does not take anybody through a place");
            }

            string refusal;
            bool promised = spatial.SpatialRoute.CanPromise(vanilla, out refusal);
            return new SiteRouteVerb(actionId, spatial.SpatialRoute, authored, promised, refusal);
        }

        private static string Describe(IReadOnlyList<SiteAffordance> affordances)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < affordances.Count; i++)
            {
                names.Add(affordances[i].ToString());
            }

            return string.Join(", ", names.ToArray());
        }
    }
}
