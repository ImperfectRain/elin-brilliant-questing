using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-140. Draws a run of scenario dungeons and prints what each seed actually made.
    ///
    /// The done-when is that a handful of authored pieces produce meaningfully different
    /// navigation and problem structures across seeds, and that is a claim somebody should be able
    /// to check by looking rather than by trusting a test. So this prints one place in full and
    /// then the shape of every seed beside it, with the count of distinct shapes at the bottom.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run dungeon
    /// </summary>
    internal sealed class DungeonScenario : LabScenario
    {
        public override string Id => "dungeon";

        public override string Summary => "draw scenario dungeons across seeds and compare their shapes";

        public override string Description =>
            "Composes the collapsed-mine grammar for a run of seeds, realizes each one out of the\n"
            + "authored mine pieces, prints the first in full, and lists every seed's navigation and\n"
            + "problem structure so different seeds can be seen to be different places.";

        public override bool UsesSeed => false;

        public override int Run(LabRunContext context)
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(BundlePath());
            if (loaded.Diagnostics.Count > 0)
            {
                foreach (ContentDiagnostic diagnostic in loaded.Diagnostics)
                {
                    Console.Error.WriteLine(diagnostic);
                }

                return LabExit.ScenarioFailure;
            }

            IReadOnlyList<ContentDiagnostic> problems;
            SiteGrammarLibrary grammars = SiteGrammarContent.CreateLibrary(loaded.Bundle, out problems);
            SitePieceCatalogue pieces = SitePieceContent.CreateCatalogue(
                loaded.Bundle, ScenarioDungeon.Family, out problems);

            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);
            List<ulong> seeds = new List<ulong>();
            for (ulong seed = 1; seed <= 16; seed++)
            {
                seeds.Add(seed);
            }

            IReadOnlyList<SiteStructure> drawn = ScenarioDungeon.Draw(
                seeds,
                SiteAffordance.EvidenceCache,
                grammars,
                pieces,
                StandardActions.CreateRegistry(),
                build);

            if (drawn.Count == 0)
            {
                Console.Error.WriteLine("no seed produced a buildable mine.");
                return LabExit.ScenarioFailure;
            }

            Console.WriteLine(NarrativeInspector.DescribeSiteStructure(
                SiteRealization.Realize(
                    ScenarioDungeon.Family,
                    drawn[0].Layout,
                    drawn[0].Objective,
                    pieces,
                    null,
                    StandardActions.CreateRegistry(),
                    build)));

            Console.WriteLine("shapes across " + drawn.Count + " seed(s)");
            HashSet<string> shapes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> problemsSeen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < drawn.Count; i++)
            {
                SiteStructure structure = drawn[i];
                SiteTopology topology = structure.Topology();
                shapes.Add(topology.Signature);
                problemsSeen.Add(string.Join(" | ", topology.Obstacles));

                Console.WriteLine(
                    "  seed " + structure.Seed
                    + ": " + topology.Parts.Count + " part(s), depth " + topology.Depth
                    + ", " + topology.Loops + " loop(s), " + topology.WaysToObjective + " way(s) to what it keeps"
                    + ", " + topology.DistinctProblems + " of them a different problem");
                Console.WriteLine("    past: " + string.Join(", ", topology.Obstacles));
            }

            Console.WriteLine();
            Console.WriteLine("  distinct navigation shapes: " + shapes.Count);
            Console.WriteLine("  distinct sets of things to get past: " + problemsSeen.Count);
            return LabExit.Success;
        }

        private static string BundlePath()
        {
            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (directory != null
                   && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            return directory == null
                ? System.IO.Path.Combine("Package", "content.bqc")
                : System.IO.Path.Combine(directory.FullName, "Package", "content.bqc");
        }
    }
}
