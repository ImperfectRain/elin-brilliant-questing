using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The closed set of spatial affordances a grammar may require of a node or a route
    /// (`LW §7.4`).
    ///
    /// This is a vocabulary of *requirements*, not of promises. A grammar saying a route needs a
    /// <see cref="LockedBarrier"/> says what the place must be like for the situation to make
    /// sense; it does not say Elin has a lock the player can pick there. Turning a required
    /// affordance into a route the player can actually take - and grading how well each one is
    /// evidenced on the live build - is BQ-090's, and this enum exists so BQ-090 has something
    /// authored to project rather than a string table to guess at.
    ///
    /// Named rather than free text for the reason every vocabulary here is named: an affordance
    /// nobody can spell wrong is one a grammar cannot silently require and never get.
    /// </summary>
    public enum SiteAffordance
    {
        /// <summary>Shut, and somebody holds the key.</summary>
        LockedBarrier,

        /// <summary>Shut, and it can be broken through at a cost.</summary>
        BreakableBarrier,

        /// <summary>Shut, and it can be gone around by digging.</summary>
        DiggableBypass,

        /// <summary>A way through that has to be found before it can be used.</summary>
        HiddenPassage,

        /// <summary>Somebody is standing there deciding who passes.</summary>
        GuardedThreshold,

        /// <summary>Where what the place keeps is actually kept.</summary>
        EvidenceCache,

        /// <summary>Somebody is being held here against their will.</summary>
        PrisonCell,

        /// <summary>Somewhere you can watch from without being in it.</summary>
        ObservationPoint,

        /// <summary>The place itself can hurt you.</summary>
        Hazard,

        /// <summary>Laid deliberately, by whoever holds the place.</summary>
        TrapCluster,

        /// <summary>You are let past because of who they think you are.</summary>
        SocialCheckpoint,

        /// <summary>A way out that is not the way in.</summary>
        AlternateExit
    }

    /// <summary>
    /// One functional part of a place - what it is for, not where it is.
    ///
    /// "Communal area", "storage", "leader space", "prisoner area" (`LW §7.3`). A node is an id and
    /// a set of requirements; the geometry that eventually satisfies it is Elin's or an authored
    /// piece's, and is never described here (`PP §2`).
    /// </summary>
    public sealed class SiteNodeSpec
    {
        public SiteNodeSpec(string id, bool required, IEnumerable<SiteAffordance> affordances, string socket)
        {
            Id = id ?? string.Empty;
            Required = required;
            Affordances = new List<SiteAffordance>(affordances ?? new SiteAffordance[0]).AsReadOnly();
            Socket = socket ?? string.Empty;
        }

        public string Id { get; }

        /// <summary>
        /// Whether every place of this kind has one. Required nodes are what makes two sites from
        /// one grammar the same kind of place; optional ones are what keeps them from being the
        /// same place.
        /// </summary>
        public bool Required { get; }

        public IReadOnlyList<SiteAffordance> Affordances { get; }

        /// <summary>
        /// The authored-piece socket this node is filled from, where the grammar names one
        /// (`PP §3`). Carried and inspectable; nothing realises it yet, because no BQ site has a
        /// physical realization to fill (BQ-140).
        /// </summary>
        public string Socket { get; }

        public bool HasSocket => Socket.Length > 0;

        public override string ToString() => Id + (Required ? string.Empty : " (optional)");
    }

    /// <summary>
    /// A route relationship: which part of the place is reached from which, and on what terms.
    ///
    /// A route out of <see cref="SiteGrammar.Outside"/> is a way in, and is the one kind of route
    /// the rest of the codebase already has a type for - <see cref="SiteApproach"/>. The terms are
    /// the same single distinction BQ-087 settled on: whether getting through waits on somebody
    /// letting you.
    /// </summary>
    public sealed class SiteRouteSpec
    {
        public SiteRouteSpec(
            string from,
            string to,
            string actionId,
            bool needsAdmission,
            IEnumerable<SiteAffordance> affordances)
        {
            From = from ?? string.Empty;
            To = to ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            NeedsAdmission = needsAdmission;
            Affordances = new List<SiteAffordance>(affordances ?? new SiteAffordance[0]).AsReadOnly();
        }

        public string From { get; }

        public string To { get; }

        /// <summary>
        /// The registered verb this route is taken with, where the grammar names one. Required on a
        /// way in, because <see cref="SiteApproach"/> is made of it; optional inside, where the
        /// relationship is often just "this is behind that".
        /// </summary>
        public string ActionId { get; }

        public bool NeedsAdmission { get; }

        public IReadOnlyList<SiteAffordance> Affordances { get; }

        /// <summary>A way in: it starts outside the place.</summary>
        public bool IsEntry => string.Equals(From, SiteGrammar.Outside, StringComparison.Ordinal);

        /// <summary>A way out that is not necessarily a way in.</summary>
        public bool IsExit => string.Equals(To, SiteGrammar.Outside, StringComparison.Ordinal);

        // Only a way in is admitted or uninvited. Inside the place the same flag says whether
        // getting through waits on somebody, and saying "uninvited" of a route between two rooms
        // would be asserting something about it that nobody authored.
        public override string ToString()
        {
            return From + " -> " + To + (ActionId.Length > 0 ? " by " + ActionId : string.Empty)
                   + (NeedsAdmission ? " (admitted)" : IsEntry ? " (uninvited)" : string.Empty);
        }
    }

    /// <summary>
    /// BQ-089. A curated kind of place: what every one of them has, what some of them have, and
    /// how the parts connect.
    ///
    /// A grammar specifies requirements, never geometry (`LW §7.3`, `PP §2`). It says a bandit camp
    /// has a way in somebody is watching, somewhere everyone sleeps, somewhere the takings are kept
    /// and somewhere the leader sits; it does not say how many tiles any of that is, which way the
    /// door faces, or which map piece is used. Composing one with a seed
    /// (<see cref="Compose"/>) produces a <see cref="SiteLayout"/> - the abstract plan - and two
    /// layouts from one grammar are the same kind of place because they share every required node
    /// and route, and different places because the optional ones differ.
    ///
    /// Grammars are authored content, not C#: they live in `content/sites/`, compile into the
    /// bundle, and are read back by <see cref="SiteGrammarContent"/>. A place kind is a catalogue
    /// entry, and catalogue entries belong where a writer can add one without a build.
    /// </summary>
    public sealed class SiteGrammar
    {
        /// <summary>
        /// The reserved node id for everywhere that is not this place. Routes out of it are the
        /// ways in; routes into it are ways out. Never declared as a node, because the world
        /// outside a site is not part of the site's plan.
        /// </summary>
        public const string Outside = "outside";

        public SiteGrammar(
            string id,
            string siteType,
            bool restricted,
            IEnumerable<SiteNodeSpec> nodes,
            IEnumerable<SiteRouteSpec> routes)
        {
            Id = id ?? string.Empty;
            SiteType = siteType ?? string.Empty;
            Restricted = restricted;
            Nodes = new List<SiteNodeSpec>(nodes ?? new SiteNodeSpec[0]).AsReadOnly();
            Routes = new List<SiteRouteSpec>(routes ?? new SiteRouteSpec[0]).AsReadOnly();
        }

        public string Id { get; }

        /// <summary>
        /// The ontology term a place of this kind carries, and the one
        /// <see cref="NarrativeSite.SiteType"/> and <see cref="SiteReuse"/> already match on. A
        /// grammar does not get a private taxonomy.
        /// </summary>
        public string SiteType { get; }

        /// <summary>Whether what a place of this kind keeps is behind somebody's permission.</summary>
        public bool Restricted { get; }

        public IReadOnlyList<SiteNodeSpec> Nodes { get; }

        public IReadOnlyList<SiteRouteSpec> Routes { get; }

        public SiteNodeSpec GetNode(string nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (string.Equals(Nodes[i].Id, nodeId, StringComparison.Ordinal))
                {
                    return Nodes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Whether both ends of this route are part of every place of this kind. Derived rather
        /// than authored: a route is exactly as required as the nodes it joins, and letting an
        /// author state it separately would let a grammar promise a required way through to an
        /// optional room.
        /// </summary>
        public bool IsRequired(SiteRouteSpec route)
        {
            return route != null && EndRequired(route.From) && EndRequired(route.To);
        }

        /// <summary>
        /// The abstract plan for one place of this kind (BQ-089's done-when).
        ///
        /// Every required node and every required route is present at every seed - that is what
        /// makes two of them recognisably the same kind of place. Each optional node is drawn
        /// independently from the seed, so which extras a particular place has is stable for that
        /// seed and different across seeds. Nothing here reads world state: what a place then
        /// *holds* - who is in it, what was taken, what happened here - is the situation's, and
        /// <see cref="SiteContents"/> derives it (BQ-091).
        /// </summary>
        public SiteLayout Compose(ulong seed)
        {
            DeterministicRng stream = new DeterministicRng(seed).Fork("site-grammar").Fork(Id);

            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            List<SiteNodeSpec> undrawn = new List<SiteNodeSpec>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                SiteNodeSpec node = Nodes[i];
                if (node.Required || stream.Fork(node.Id).Chance(0.5))
                {
                    present.Add(node.Id);
                }
                else
                {
                    undrawn.Add(node);
                }
            }

            // An optional room the drawn routes cannot reach is not part of this place: it would
            // be a node the inspector could not explain a way to, which is the one thing the
            // done-when says the plan must never contain. Required nodes cannot be dropped here,
            // because SiteGrammarContent refuses a grammar whose required core is not reachable on
            // required routes alone.
            List<SiteNodeSpec> unreachable = new List<SiteNodeSpec>();
            PruneUnreachable(present, unreachable);

            List<SiteLayoutNode> nodes = new List<SiteLayoutNode>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (present.Contains(Nodes[i].Id))
                {
                    nodes.Add(new SiteLayoutNode(Nodes[i]));
                }
            }

            List<SiteLayoutRoute> routes = new List<SiteLayoutRoute>();
            for (int i = 0; i < Routes.Count; i++)
            {
                SiteRouteSpec route = Routes[i];
                if (EndPresent(route.From, present) && EndPresent(route.To, present))
                {
                    routes.Add(new SiteLayoutRoute(route, IsRequired(route)));
                }
            }

            List<SiteOmission> omitted = new List<SiteOmission>();
            for (int i = 0; i < undrawn.Count; i++)
            {
                omitted.Add(new SiteOmission(undrawn[i], SiteOmissionReason.NotDrawn));
            }

            for (int i = 0; i < unreachable.Count; i++)
            {
                omitted.Add(new SiteOmission(unreachable[i], SiteOmissionReason.Unreachable));
            }

            return new SiteLayout(this, seed, nodes, routes, omitted);
        }

        private void PruneUnreachable(HashSet<string> present, List<SiteNodeSpec> unreachable)
        {
            while (true)
            {
                HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal) { Outside };
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = 0; i < Routes.Count; i++)
                    {
                        SiteRouteSpec route = Routes[i];
                        if (!EndPresent(route.From, present) || !EndPresent(route.To, present))
                        {
                            continue;
                        }

                        // A route is walked in the direction it is written. A way out is a way out,
                        // and a plan that treated it as a way in would let a grammar reach a room
                        // through its own bolthole.
                        if (reached.Contains(route.From) && reached.Add(route.To))
                        {
                            grew = true;
                        }
                    }
                }

                string dropped = null;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    string id = Nodes[i].Id;
                    if (present.Contains(id) && !reached.Contains(id))
                    {
                        dropped = id;
                        unreachable.Add(Nodes[i]);
                        break;
                    }
                }

                if (dropped == null)
                {
                    return;
                }

                present.Remove(dropped);
            }
        }

        private bool EndRequired(string nodeId)
        {
            if (string.Equals(nodeId, Outside, StringComparison.Ordinal))
            {
                return true;
            }

            SiteNodeSpec node = GetNode(nodeId);
            return node != null && node.Required;
        }

        private static bool EndPresent(string nodeId, HashSet<string> present)
        {
            return string.Equals(nodeId, Outside, StringComparison.Ordinal) || present.Contains(nodeId);
        }

        public override string ToString() => Id + " [" + SiteType + "]";
    }

    /// <summary>Every grammar the bundle carries, by id.</summary>
    public sealed class SiteGrammarLibrary
    {
        private readonly Dictionary<string, SiteGrammar> _byId = new Dictionary<string, SiteGrammar>(StringComparer.Ordinal);

        public SiteGrammarLibrary(IEnumerable<SiteGrammar> grammars)
        {
            List<SiteGrammar> all = new List<SiteGrammar>();
            if (grammars != null)
            {
                foreach (SiteGrammar grammar in grammars)
                {
                    if (grammar == null || _byId.ContainsKey(grammar.Id))
                    {
                        continue;
                    }

                    _byId[grammar.Id] = grammar;
                    all.Add(grammar);
                }
            }

            Grammars = all.AsReadOnly();
        }

        public IReadOnlyList<SiteGrammar> Grammars { get; }

        public SiteGrammar Get(string grammarId)
        {
            SiteGrammar grammar;
            return grammarId != null && _byId.TryGetValue(grammarId, out grammar) ? grammar : null;
        }

        /// <summary>
        /// The plan for the place a site was made from, or null when the grammar is not in this
        /// bundle. A site stores which grammar and which seed rather than the plan itself, so a
        /// content update reaches every place already in a save (`content-pipeline.md §2`).
        /// </summary>
        public SiteLayout Compose(string grammarId, ulong seed)
        {
            SiteGrammar grammar = Get(grammarId);
            return grammar == null ? null : grammar.Compose(seed);
        }

        /// <summary>The plan this place was made from, read off the place itself.</summary>
        public SiteLayout LayoutOf(NarrativeSite site)
        {
            return site == null || string.IsNullOrEmpty(site.GrammarId)
                ? null
                : Compose(site.GrammarId, site.GenerationSeed);
        }
    }
}
