using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Lab.Scenes;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public sealed class SpatialRangeReport
    {
        public ulong FirstSeed { get; set; }
        public int Seeds { get; set; }
        public int Runs { get; set; }
        public int Accepted { get; set; }
        public List<string> Rejections { get; } = new List<string>();
        public SortedDictionary<string, RepetitionMetric> Metrics { get; } = new SortedDictionary<string, RepetitionMetric>(StringComparer.Ordinal);
        public string Scope => "One selected ScenarioPlan per shipped grammar/seed, using the production theft fixture with an explicitly staged three-person crew and EvidenceCache objective. "
            + "Rejections are excluded from metrics. Directed graph canonicalization ignores grammar, region, socket, entity names and IDs. "
            + "routeTopology retains outside/objective roles; experientialTopology also retains affordances, required flags, verbs, admission and support. "
            + "cycleCount uses the planner's directed simple rings (bounded by MaximumCycles). objectiveSeparation is shortest promised entry depth, not tile distance. "
            + "routeMechanics counts declared route demands/support and structural versus collapsed alternatives, not played solutions. "
            + "evidenceDistribution and encounterEcology are depth/role/count distributions, including unplaced occupants; not native encounters. "
            + "historyReadability measures causal anchor linkage and reachability only, not human comprehension. Empty evidence is missing, not repetition. "
            + "Graph searches exceeding 100000 permutations are missing, never guessed equivalent. RepeatRate=(observations-distinct)/observations; no quality threshold or live map claim.";
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static class SpatialRangeHarness
    {
        public static SpatialRangeReport Run(ContentBundle bundle, ulong firstSeed, int seeds)
        {
            if (seeds < 1 || seeds > 1000) throw new ArgumentOutOfRangeException(nameof(seeds));
            if (firstSeed > ulong.MaxValue - (ulong)(seeds - 1)) throw new ArgumentOutOfRangeException(nameof(firstSeed));
            var library = SiteGrammarContent.CreateLibrary(bundle, out var problems);
            if (problems.Count != 0) throw new InvalidOperationException(string.Join("; ", problems));
            var report = new SpatialRangeReport { FirstSeed = firstSeed, Seeds = seeds };
            foreach (string axis in new[] { "routeTopology", "experientialTopology", "cycleCount", "objectiveSeparation", "routeMechanics", "evidenceDistribution", "encounterEcology", "historyReadability" })
                report.Metrics.Add(axis, new RepetitionMetric());
            for (int i = 0; i < seeds; i++)
            foreach (var grammar in library.Grammars.OrderBy(g => g.Id, StringComparer.Ordinal))
            {
                ulong seed = firstSeed + (ulong)i;
                var fixture = BuildFixture(seed);
                var plan = Plan(fixture, grammar, seed);
                report.Runs++;
                if (!plan.Valid)
                {
                    report.Rejections.Add(grammar.Id + " seed=" + seed + ": " + string.Join("; ", plan.Refusals.Concat(
                        plan.Validation.Findings.Where(f => !f.Held).Select(f => f.ToString()))));
                    continue;
                }
                report.Accepted++;
                foreach (var pair in Measure(plan)) report.Metrics[pair.Key].Observe(pair.Value);
            }
            return report;
        }

        public static ScenarioPlan Plan(SceneFixture fixture, SiteGrammar grammar, ulong seed) => ScenarioPlanner.Plan(
            fixture.World, fixture.Thread.Id, grammar, SiteAffordance.EvidenceCache, seed,
            SiteCandidates.DefaultBatch, StandardActions.CreateRegistry(), fixture.Vanilla);

        public static SceneFixture BuildFixture(ulong seed)
        {
            var fixture = SceneSituations.Find("theft").Build(seed);
            var world = fixture.World;
            var holder = world.Registry.GetNpc(fixture.Focus.Subject);
            var crew = world.Registry.Add(new Organization(world.NewId("org"), "diagnostic crew", "criminal_crew") { LeaderId = holder.Id });
            crew.MemberIds.Add(holder.Id);
            holder.OrganizationIds.Add(crew.Id);
            for (int i = 0; i < 2; i++)
            {
                var member = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "diagnostic member"));
                member.OrganizationIds.Add(crew.Id);
                crew.MemberIds.Add(member.Id);
                fixture.Vanilla.Define(member.Id, zone: fixture.Vanilla.GetZoneOf(holder.Id));
            }
            return fixture;
        }

        public static SortedDictionary<string, string> Measure(ScenarioPlan plan)
        {
            if (plan == null || !plan.Valid) throw new ArgumentException("Metrics require a valid selected plan.", nameof(plan));
            var evidence = plan.Anchors.Where(a => a.Kind == ScenarioAnchorKind.Evidence).ToArray();
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["routeTopology"] = Topology(plan, false),
                ["experientialTopology"] = Topology(plan, true),
                ["cycleCount"] = Json(plan.Cycles.Count),
                ["objectiveSeparation"] = Json(plan.GetRegion(plan.ObjectiveRegionId).Depth),
                ["routeMechanics"] = Json(new { Routes = Sorted(plan.Routes.Select(RouteLabel)), Structural = plan.Alternatives.Count, Collapsed = plan.CollapsedAlternatives.Count }),
                ["evidenceDistribution"] = evidence.Length == 0 ? null : Json(Sorted(evidence.Select(a => Json(new { a.Kind, a.Affordance, plan.GetRegion(a.RegionId).Depth, plan.GetRegion(a.RegionId).IsObjective, plan.GetRegion(a.RegionId).Reachable })))),
                ["encounterEcology"] = plan.OccupantRegions.Count == 0 && plan.UnplacedOccupants.Count == 0 ? null : Json(new
                {
                    Regions = Sorted(plan.OccupantRegions.Select(o => Json(new { o.Kind, o.Affordance, Depth = plan.GetRegion(o.RegionId)?.Depth, Presence = Sorted(o.Occupants.Select(p => p.Presence.ToString())) }))),
                    Unplaced = Sorted(plan.UnplacedOccupants.Select(o => o.Presence.ToString()))
                }),
                ["historyReadability"] = evidence.Length == 0 ? null : Json(new
                {
                    Anchors = evidence.Length,
                    Linked = evidence.Count(a => !a.EventId.IsNone && !a.FactId.IsNone),
                    Reachable = evidence.Count(a => plan.GetRegion(a.RegionId).Reachable),
                    Intact = plan.Validation.Get(ScenarioInvariant.CausalReferencesIntact).Held
                })
            };
        }

        private static string Json(object value) => JsonSerializer.Serialize(value);
        private static string[] Sorted(IEnumerable<string> values) => values.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        private static string RouteLabel(ScenarioRoute route) => Json(new { route.ActionId, route.NeedsAdmission, route.Required, route.Support, Affordances = Sorted(route.Affordances.Select(a => a.ToString())) });

        // Exact directed multigraph labeling. Refinement only partitions the search; the final
        // adjacency serialization prevents equal-degree, non-isomorphic graphs from colliding.
        public static string Topology(ScenarioPlan plan, bool mechanics)
        {
            var ids = new[] { SiteGrammar.Outside }.Concat(plan.Regions.Select(r => r.Id)).ToArray();
            var labels = new[] { "outside" }.Concat(plan.Regions.Select(r => mechanics
                ? Json(new { r.IsObjective, r.Required, Affordances = Sorted(r.Affordances.Select(a => a.ToString())) })
                : Json(r.IsObjective))).ToArray();
            var edges = plan.Routes.Select(r => (From: Array.IndexOf(ids, r.From), To: Array.IndexOf(ids, r.To), Label: mechanics ? RouteLabel(r) : "route")).ToArray();
            int[] Rank(string[] values)
            {
                var order = values.Distinct().OrderBy(v => v, StringComparer.Ordinal).ToArray();
                return values.Select(v => Array.IndexOf(order, v)).ToArray();
            }
            var colors = Rank(labels);
            for (int round = 0; round < ids.Length; round++)
            {
                var next = Rank(ids.Select((_, n) => Json(new { Color = colors[n],
                    Out = Sorted(edges.Where(e => e.From == n).Select(e => Json(new { e.Label, Color = colors[e.To] }))),
                    In = Sorted(edges.Where(e => e.To == n).Select(e => Json(new { e.Label, Color = colors[e.From] }))) })).ToArray());
                bool stable = next.Distinct().Count() == colors.Distinct().Count();
                colors = next;
                if (stable) break;
            }
            var groups = Enumerable.Range(0, ids.Length).GroupBy(n => colors[n]).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
            long searches = 1;
            foreach (var group in groups)
                for (int i = 2; i <= group.Length; i++) { searches *= i; if (searches > 100000) return null; }
            string best = null;
            var ordering = new List<int>();
            void Visit(int groupIndex)
            {
                if (groupIndex == groups.Length)
                {
                    var inverse = new int[ids.Length];
                    for (int i = 0; i < ordering.Count; i++) inverse[ordering[i]] = i;
                    string candidate = Json(new { Nodes = ordering.Select(n => labels[n]).ToArray(),
                        Edges = Sorted(edges.Select(e => Json(new { From = inverse[e.From], To = inverse[e.To], e.Label }))) });
                    if (best == null || string.CompareOrdinal(candidate, best) < 0) best = candidate;
                    return;
                }
                var group = groups[groupIndex];
                void Permute(int offset)
                {
                    if (offset == group.Length)
                    {
                        ordering.AddRange(group); Visit(groupIndex + 1); ordering.RemoveRange(ordering.Count - group.Length, group.Length); return;
                    }
                    for (int i = offset; i < group.Length; i++)
                    {
                        (group[offset], group[i]) = (group[i], group[offset]);
                        Permute(offset + 1);
                        (group[offset], group[i]) = (group[i], group[offset]);
                    }
                }
                Permute(0);
            }
            Visit(0);
            return best;
        }
    }
}
