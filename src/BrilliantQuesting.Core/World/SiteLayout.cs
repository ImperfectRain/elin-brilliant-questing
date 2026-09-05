using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>One part of a place this plan has, and whether every place of the kind has one.</summary>
    public sealed class SiteLayoutNode
    {
        internal SiteLayoutNode(SiteNodeSpec spec)
        {
            Spec = spec;
        }

        public SiteNodeSpec Spec { get; }

        public string Id => Spec.Id;

        public bool Required => Spec.Required;

        public IReadOnlyList<SiteAffordance> Affordances => Spec.Affordances;

        public string Socket => Spec.Socket;

        public override string ToString() => Spec.ToString();
    }

    /// <summary>One route this plan has, between two parts it has.</summary>
    public sealed class SiteLayoutRoute
    {
        internal SiteLayoutRoute(SiteRouteSpec spec, bool required)
        {
            Spec = spec;
            Required = required;
        }

        public SiteRouteSpec Spec { get; }

        /// <summary>Both ends belong to every place of this kind, so this route does too.</summary>
        public bool Required { get; }

        public string From => Spec.From;

        public string To => Spec.To;

        public string ActionId => Spec.ActionId;

        public bool NeedsAdmission => Spec.NeedsAdmission;

        public bool IsEntry => Spec.IsEntry;

        public IReadOnlyList<SiteAffordance> Affordances => Spec.Affordances;

        public override string ToString() => Spec.ToString();
    }

    public enum SiteOmissionReason
    {
        /// <summary>This place did not draw it. Another place of the same kind may have one.</summary>
        NotDrawn,

        /// <summary>It was drawn, and nothing this place drew leads to it.</summary>
        Unreachable
    }

    /// <summary>An optional part of the kind that this particular place does not have.</summary>
    public sealed class SiteOmission
    {
        internal SiteOmission(SiteNodeSpec spec, SiteOmissionReason reason)
        {
            Spec = spec;
            Reason = reason;
        }

        public SiteNodeSpec Spec { get; }

        public string Id => Spec.Id;

        public SiteOmissionReason Reason { get; }
    }

    /// <summary>
    /// BQ-089. The abstract plan for one place: which functional parts it has, how they connect,
    /// and what each of them requires.
    ///
    /// Meaning, never geometry (`PP §3`). Nothing here is a tile, a room size or a map piece; the
    /// nodes are what the place is *for* and the routes are which part is reached from which. The
    /// plan is not stored - a site records the grammar it came from and the seed it was drawn at,
    /// and this is recomposed from the two, so a place in a fifty-hour save picks up a corrected
    /// grammar the same way a storylet picks up corrected wording.
    /// </summary>
    public sealed class SiteLayout
    {
        internal SiteLayout(
            SiteGrammar grammar,
            ulong seed,
            IEnumerable<SiteLayoutNode> nodes,
            IEnumerable<SiteLayoutRoute> routes,
            IEnumerable<SiteOmission> omitted)
        {
            Grammar = grammar;
            Seed = seed;
            Nodes = new List<SiteLayoutNode>(nodes ?? new SiteLayoutNode[0]).AsReadOnly();
            Routes = new List<SiteLayoutRoute>(routes ?? new SiteLayoutRoute[0]).AsReadOnly();
            Omitted = new List<SiteOmission>(omitted ?? new SiteOmission[0]).AsReadOnly();

            List<SiteApproach> approaches = new List<SiteApproach>();
            for (int i = 0; i < Routes.Count; i++)
            {
                SiteLayoutRoute route = Routes[i];
                if (route.IsEntry && route.ActionId.Length > 0)
                {
                    approaches.Add(new SiteApproach(route.ActionId, route.NeedsAdmission));
                }
            }

            Approaches = approaches.AsReadOnly();
        }

        public SiteGrammar Grammar { get; }

        public string GrammarId => Grammar.Id;

        public string SiteType => Grammar.SiteType;

        public bool Restricted => Grammar.Restricted;

        public ulong Seed { get; }

        public IReadOnlyList<SiteLayoutNode> Nodes { get; }

        public IReadOnlyList<SiteLayoutRoute> Routes { get; }

        /// <summary>What the kind allows that this place does not have, and why not.</summary>
        public IReadOnlyList<SiteOmission> Omitted { get; }

        /// <summary>
        /// The ways in, in the shape genesis already validates
        /// (<see cref="NarrativeSite.Approaches"/>). Every grammar guarantees at least one route
        /// that waits on somebody and at least one that does not on its required entries alone, so
        /// no seed can produce a place with one approach spelled twice.
        /// </summary>
        public IReadOnlyList<SiteApproach> Approaches { get; }

        public bool Has(string nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (string.Equals(Nodes[i].Id, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A plan for a place of this kind, ready for the matter to populate.
        ///
        /// The grammar decides what sort of place it is, what it keeps behind whose permission and
        /// how it is reached; who is in it and what it holds stay the caller's, because those come
        /// from the situation rather than from the kind. Handing back a <see cref="SitePlan"/>
        /// rather than a second description of a place keeps one vocabulary for "what a place must
        /// be" - the one <see cref="SiteGenesis"/> validates and <see cref="SiteReuse"/> weighs.
        /// </summary>
        public SitePlan NewPlan(EntityId siteId, string name, EntityId threadId)
        {
            SitePlan plan = new SitePlan(siteId, name, SiteType, threadId)
            {
                GrammarId = GrammarId,
                Seed = Seed,
                Restricted = Restricted
            };

            for (int i = 0; i < Approaches.Count; i++)
            {
                plan.Approaches.Add(Approaches[i]);
            }

            return plan;
        }
    }
}
