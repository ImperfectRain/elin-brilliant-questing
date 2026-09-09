using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Lab.Scenes;
using BrilliantQuesting.Rewards;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Lab
{
    /// <summary>Diagnostic counts only. Missing evidence never becomes a repeated signature.</summary>
    public sealed class RepetitionMetric
    {
        public int Missing { get; private set; }
        public SortedDictionary<string, int> Counts { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public int Observations => Counts.Values.Sum();
        public int Distinct => Counts.Count;
        public int Repeats => Observations - Distinct;
        public double? RepeatRate => Observations == 0 ? null : (double)Repeats / Observations;

        public void Observe(string signature)
        {
            if (string.IsNullOrEmpty(signature)) { Missing++; return; }
            Counts.TryGetValue(signature, out int count);
            Counts[signature] = count + 1;
        }
    }

    public sealed class AntiTemplateReport
    {
        public ulong FirstSeed { get; set; }
        public int Seeds { get; set; }
        public int Runs { get; set; }
        public int Presentations { get; set; }
        public List<string> UnpresentedRuns { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public SortedDictionary<string, RepetitionMetric> Metrics { get; } =
            new SortedDictionary<string, RepetitionMetric>(StringComparer.Ordinal)
            {
                ["causalSkeleton"] = new RepetitionMetric(), ["roles"] = new RepetitionMetric(),
                ["storylets"] = new RepetitionMetric(), ["openers"] = new RepetitionMetric(),
                ["solutionFamilies"] = new RepetitionMetric(), ["rewards"] = new RepetitionMetric(),
                ["sites"] = new RepetitionMetric()
            };
        public string Scope => "One fresh production SceneSituations fixture per seed/situation; all eligible routed scenes in ordinal order. "
            + "Not a natural director frequency estimate. causalSkeleton is recorded origin event type and proposed predicate plus BQ-101 shape, not full causal topology; "
            + "roles are cast role sets; openers are rendered opener repetition groups (fragment ID fallback); "
            + "solutionFamilies are declared action IDs, not played solutions; rewards are post-scene ResolutionRewardAudit kind sets, "
            + "not promised payouts; sites are declared site types, not spatial topology. "
            + "Shape/routes/sites sampled once per fixture, roles/storylets per acknowledged play, openers per rendered line, rewards per final fixture. "
            + "Missing includes absent evidence or no observed kind/opener; it is excluded from repetition. RepeatRate=(observations-distinct)/observations; no quality threshold.";
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static class AntiTemplateHarness
    {
        public static ContentBundle LoadBundle()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                directory = directory.Parent;
            if (directory == null) throw new InvalidOperationException("Cannot locate repository content bundle.");
            var loaded = ContentBundleLoader.LoadFile(Path.Combine(directory.FullName, "Package", "content.bqc"));
            if (loaded.Diagnostics.Count != 0) throw new InvalidOperationException(string.Join("; ", loaded.Diagnostics));
            return loaded.Bundle;
        }

        public static AntiTemplateReport Run(ContentBundle bundle, ulong firstSeed, int seeds)
        {
            if (seeds < 1 || seeds > 1000) throw new ArgumentOutOfRangeException(nameof(seeds), "Use 1..1000 seeds.");
            if (firstSeed > ulong.MaxValue - (ulong)(seeds - 1)) throw new ArgumentOutOfRangeException(nameof(firstSeed));
            var library = DialogueFragmentContent.CreateLibrary(bundle, out var fragmentProblems);
            if (fragmentProblems.Count != 0) throw new InvalidOperationException(string.Join("; ", fragmentProblems));
            var report = new AntiTemplateReport { FirstSeed = firstSeed, Seeds = seeds };
            for (int i = 0; i < seeds; i++)
            foreach (var situation in SceneSituations.All.OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                ulong seed = firstSeed + (ulong)i;
                var fixture = situation.Build(seed);
                report.Runs++;
                var shape = SituationFingerprint.Read(fixture.World, fixture.Player, fixture.Thread,
                    fixture.Vanilla.Now, fixture.FocusFactId);
                report.Metrics["causalSkeleton"].Observe(ReadCausalSkeleton(fixture));
                report.Metrics["solutionFamilies"].Observe(shape.Routes);
                report.Metrics["sites"].Observe(Signature(fixture.Thread.SiteIds
                    .Select(id => fixture.World.Registry.GetSite(id)?.SiteType)));
                var engine = StoryletContent.CreateEngine(bundle, out var problems);
                if (problems.Count != 0) throw new InvalidOperationException(string.Join("; ", problems));
                var router = new StoryletRouter(new DialogueRealizer(library), new VanillaStyleCheckResolver(fixture.Vanilla));
                var opportunities = engine.Find(new StoryletCastingContext(fixture.World, fixture.Vanilla,
                    fixture.Thread, fixture.FocusFactId));
                int before = report.Presentations;
                foreach (var opportunity in opportunities.Where(o => o.Definition.IsRouted)
                    .OrderBy(o => o.Definition.Id, StringComparer.Ordinal))
                {
                    StoryletPlay play = null;
                    bool presented = engine.TryPresent(opportunity, () =>
                    {
                        play = router.Play(opportunity, new StoryletPlayContext(fixture.World, fixture.Vanilla, fixture.Thread)
                        {
                            Rng = new DeterministicRng(seed), ApplyConsequences = true,
                            InPublic = opportunity.Definition.ToneTags.Contains("public")
                        });
                        return play.Played && play.Beats.Any(b => b.Line != null && b.Line.Rendered);
                    });
                    if (!presented) continue;
                    report.Presentations++;
                    report.Metrics["storylets"].Observe(opportunity.Definition.Id);
                    report.Metrics["roles"].Observe(Signature(opportunity.RoleBindings.Keys));
                    foreach (var beat in play.Beats.Where(b => b.Line != null && b.Line.Rendered))
                    {
                        var openers = new List<string>();
                        foreach (string id in beat.Line.Fragments)
                            if (library.TryGet(id, out var fragment) && fragment.Position == FragmentPosition.Opener)
                                openers.Add(string.IsNullOrEmpty(fragment.RepetitionGroup) ? "id:" + id : "group:" + fragment.RepetitionGroup);
                        report.Metrics["openers"].Observe(Signature(openers));
                    }
                }
                if (before == report.Presentations)
                {
                    report.UnpresentedRuns.Add(situation.Id + " seed=" + seed + ": no acknowledged routed presentation");
                    report.Metrics["storylets"].Observe(null);
                    report.Metrics["roles"].Observe(null);
                }
                var rewards = new ResolutionRewardAudit(fixture.World, fixture.Player).AuditResolvedThreads();
                report.Metrics["rewards"].Observe(Signature(rewards.Kinds.Select(k => k.ToString())));
                if (rewards.ForbiddenItemPayouts.Count > 0)
                    report.Failures.Add(situation.Id + " seed=" + seed + ": forbidden item payout");
            }
            return report;
        }

        /// <summary>Read the existing semantic authorities, never names, IDs, or diagnostic English.</summary>
        public static string ReadCausalSkeleton(SceneFixture fixture)
        {
            var shape = SituationFingerprint.Read(fixture.World, fixture.Player, fixture.Thread,
                fixture.Vanilla.Now, fixture.FocusFactId);
            if (shape.Domains == null) return null;
            var origin = fixture.World.Ledger.Events.FirstOrDefault(e => e.Id == fixture.Thread.OriginEventId);
            return JsonSerializer.Serialize(new
            {
                Origin = origin?.Type.ToString(), Predicate = fixture.Focus?.Predicate,
                shape.Domains, shape.Urgent, shape.Secret, shape.RecurringPeople, shape.Routes
            });
        }

        private static string Signature(IEnumerable<string> values)
        {
            var known = values.Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToArray();
            return known.Length == 0 ? null : JsonSerializer.Serialize(known);
        }
    }
}
