using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-146 with no game attached: one theft, every routed scene the town can cast, played
    /// through and printed beat by beat.
    ///
    /// The point of the probe is the middle column. Nobody was assigned a line and nobody was
    /// assigned a move: each beat prints what its speaker weighed, what they decided, how the
    /// check went and what came out of it, so a reader can see a scene take a different route for
    /// a different person rather than being told it did.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run scene --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run scene --storylet storylet.public_accusation
    /// </summary>
    internal sealed class SceneScenario : LabScenario
    {
        public override string Id => "scene";

        public override string Summary => "play the routed storylets a theft supports, printing what each actor weighed";

        public override string Description =>
            "One theft, cast from the place alone, and every routed scene it supports played to a\n"
            + "terminal state. Each beat prints the candidates its speaker considered with the terms\n"
            + "behind each score, what they chose, the check that settled what was in doubt, the line\n"
            + "the fragment library found for it, and what history recorded. Consequences are applied\n"
            + "unless --dry is given, so the later scenes in a run see what the earlier ones did.";

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("storylet", "id", "play only this storylet", "every routed one"),
            new LabOption("dry", null, "play for inspection and write nothing to the world")
        };

        public override int Run(LabRunContext context)
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Consequences.Attach();

            ContentBundle bundle = Bundle(context);
            if (bundle == null)
            {
                return LabExit.ScenarioFailure;
            }

            IReadOnlyList<ContentDiagnostic> storyletProblems;
            IReadOnlyList<ContentDiagnostic> fragmentProblems;
            StoryletEngine engine = StoryletContent.CreateEngine(bundle, out storyletProblems);
            DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(bundle, out fragmentProblems);
            if (!Report(context, storyletProblems) || !Report(context, fragmentProblems))
            {
                return LabExit.ScenarioFailure;
            }

            string only = context.Arguments.String("storylet", null);
            bool apply = !context.Arguments.Has("dry");

            StoryletRouter router = new StoryletRouter(
                new DialogueRealizer(library), new VanillaStyleCheckResolver(lab.Vanilla));

            IReadOnlyList<StoryletOpportunity> opportunities = engine.Find(new StoryletCastingContext(
                lab.World, lab.Vanilla, lab.Situation.Thread, lab.Situation.TheftFactId));

            context.Header("the situation");
            context.WriteLine(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));

            int played = 0;
            foreach (StoryletOpportunity opportunity in opportunities.OrderBy(o => o.Definition.Id, StringComparer.Ordinal))
            {
                if (only != null && !string.Equals(only, opportunity.Definition.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!opportunity.Definition.IsRouted)
                {
                    continue;
                }

                context.Header(opportunity.Definition.Id);
                context.WriteLine(NarrativeInspector.DescribeCasting(opportunity));

                StoryletPlay play = router.Play(opportunity, new StoryletPlayContext(
                    lab.World, lab.Vanilla, lab.Situation.Thread)
                {
                    Rng = new DeterministicRng(context.Seed),
                    InPublic = opportunity.Definition.ToneTags.Contains("public"),
                    ApplyConsequences = apply
                });

                context.WriteLine(NarrativeInspector.DescribeStoryletPlay(lab.World, play));
                played++;
            }

            if (played == 0)
            {
                context.Error.WriteLine("no routed storylet could be cast on this theft"
                    + (only == null ? string.Empty : " matching " + only));
                return LabExit.ScenarioFailure;
            }

            context.Header("what the town is left with");
            context.WriteLine(NarrativeInspector.DescribeHistory(lab.World, limit: 12));
            return LabExit.Success;
        }

        private static bool Report(LabRunContext context, IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                context.Error.WriteLine(diagnostics[i].ToString());
            }

            return diagnostics.Count == 0;
        }

        /// <summary>The shipped bundle, or null with a reason - never an exception into the run.</summary>
        private static ContentBundle Bundle(LabRunContext context)
        {
            string root = AppContext.BaseDirectory;
            DirectoryInfo directory = new DirectoryInfo(root);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                context.Error.WriteLine("could not find the repository root from " + root);
                return null;
            }

            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                Path.Combine(directory.FullName, "Package", "content.bqc"));
            if (loaded.Diagnostics.Count > 0)
            {
                Report(context, loaded.Diagnostics);
                return null;
            }

            return loaded.Bundle;
        }
    }
}
