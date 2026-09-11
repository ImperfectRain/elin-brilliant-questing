using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// The work set <see cref="DevelopmentDetector.Detect(BrilliantQuesting.World.NarrativeWorldState, DevelopmentScope)"/>
    /// is allowed to look at.
    ///
    /// Detection is a pure reading, so it is always *correct* to read the whole world - and that
    /// is exactly the trap. A detector with one rule over one store can afford to walk every fact
    /// in the save; a detector with rules over facts, obligations, demands, businesses and
    /// organizations cannot, and BQ-107/BQ-108 already bought bounded off-screen work that an
    /// unbounded per-tick pressure scan would hand straight back.
    ///
    /// So the caller says what changed and detection enumerates *from* that, rather than walking
    /// each store and filtering into it. The difference is the whole point: filtering still costs
    /// the scan. A scope names ids per store because the stores are keyed differently - a fact and
    /// an organization are not interchangeable lookups - and because a work set that is one
    /// undifferentiated bag of ids cannot be enumerated from at all.
    ///
    /// It is inspectable on purpose (<see cref="ToString"/>): "which pressures did this tick
    /// consider, and why those" is a question a budget has to be able to answer.
    ///
    /// A work set answers for what is in it. Where one standing condition is recorded in two
    /// stores - a shortage in the demand ledger and the need claim it cites - naming one gives
    /// that store's reading under the shared identity, and naming both gives the whole; a later
    /// pass naming the other source lands on the same development rather than a second one.
    ///
    /// <see cref="EntireWorld"/> is the honest name for the unbounded reading. It stays supported
    /// for the inspector, the Lab and tests over small fixture worlds, and it is not what a live
    /// per-tick consumer should be handed.
    /// </summary>
    public sealed class DevelopmentScope
    {
        /// <summary>
        /// Read every store in full. Correct, deterministic, and O(world) - a diagnostic and
        /// fixture reading, not a per-tick one.
        /// </summary>
        public static readonly DevelopmentScope EntireWorld = new DevelopmentScope(true);

        private readonly List<EntityId> _facts = new List<EntityId>();
        private readonly List<EntityId> _obligations = new List<EntityId>();
        private readonly List<EntityId> _sites = new List<EntityId>();
        private readonly List<EntityId> _businesses = new List<EntityId>();
        private readonly List<EntityId> _organizations = new List<EntityId>();

        private DevelopmentScope(bool entireWorld)
        {
            IsEntireWorld = entireWorld;
        }

        /// <summary>An empty bounded work set. Adding nothing to it detects nothing, which is a
        /// correct answer for a tick in which nothing changed.</summary>
        public static DevelopmentScope Affected()
        {
            return new DevelopmentScope(false);
        }

        public bool IsEntireWorld { get; }

        /// <summary>Claims, crimes, damage and needs to re-read.</summary>
        public IReadOnlyList<EntityId> FactIds => _facts;

        /// <summary>Social debts to re-read, by obligation id.</summary>
        public IReadOnlyList<EntityId> ObligationIds => _obligations;

        /// <summary>Places whose local demand to re-read.</summary>
        public IReadOnlyList<EntityId> SiteIds => _sites;

        public IReadOnlyList<EntityId> BusinessIds => _businesses;

        public IReadOnlyList<EntityId> OrganizationIds => _organizations;

        /// <summary>How many entries a bounded pass will enumerate. Meaningless for
        /// <see cref="EntireWorld"/>, which is bounded by the save rather than by a work set.</summary>
        public int Count => _facts.Count + _obligations.Count + _sites.Count + _businesses.Count + _organizations.Count;

        public DevelopmentScope Fact(EntityId id) => Add(_facts, id);

        public DevelopmentScope Obligation(EntityId id) => Add(_obligations, id);

        public DevelopmentScope Site(EntityId id) => Add(_sites, id);

        public DevelopmentScope Business(EntityId id) => Add(_businesses, id);

        public DevelopmentScope Organization(EntityId id) => Add(_organizations, id);

        private DevelopmentScope Add(List<EntityId> into, EntityId id)
        {
            if (IsEntireWorld)
            {
                throw new InvalidOperationException(
                    "DevelopmentScope.EntireWorld is the unbounded reading and is shared; "
                    + "build a bounded work set with DevelopmentScope.Affected().");
            }

            if (!id.IsNone && !into.Contains(id))
            {
                into.Add(id);
                into.Sort();
            }

            return this;
        }

        public override string ToString()
        {
            if (IsEntireWorld)
            {
                return "scope: entire world";
            }

            StringBuilder sb = new StringBuilder("scope:");
            Describe(sb, "facts", _facts);
            Describe(sb, "obligations", _obligations);
            Describe(sb, "sites", _sites);
            Describe(sb, "businesses", _businesses);
            Describe(sb, "organizations", _organizations);
            return Count == 0 ? "scope: nothing affected" : sb.ToString();
        }

        private static void Describe(StringBuilder sb, string label, List<EntityId> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }

            sb.Append(' ').Append(label).Append('=');
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(ids[i].Value);
            }
        }
    }
}
