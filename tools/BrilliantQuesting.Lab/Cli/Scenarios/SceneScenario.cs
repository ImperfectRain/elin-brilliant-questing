using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Scenes;
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

        public override string Summary => "play the routed storylets a situation supports, printing what each actor weighed";

        public override string Description =>
            "One authoritative situation, cast from the place alone, and every routed scene it\n"
            + "supports played to a terminal state. Each beat prints the candidates its speaker\n"
            + "considered with the terms behind each score, what they chose, the check that settled\n"
            + "what was in doubt, the line the fragment library found for it, and what history\n"
            + "recorded. Consequences are applied unless --dry is given, so the later scenes in a run\n"
            + "see what the earlier ones did.\n"
            + "\n"
            + "--situation picks the starting world; --list-situations prints what is available. A\n"
            + "fixture authors only starting state - people, facts, knowledge, goals and a thread -\n"
            + "and the production engine finds, casts, routes and words everything after that.";

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("situation", "id", "which starting world to play against", SceneSituations.DefaultId),
            new LabOption("list-situations", null, "print the available situations and stop"),
            new LabOption("storylet", "id", "play only this storylet", "every routed one"),
            new LabOption("dry", null, "play for inspection and write nothing to the world")
        };

        public override int Run(LabRunContext context)
        {
            if (context.Arguments.Has("list-situations"))
            {
                ListSituations(context);
                return LabExit.Success;
            }

            string situationId = context.Arguments.String("situation", SceneSituations.DefaultId);
            SceneSituation situation = SceneSituations.Find(situationId);
            if (situation == null)
            {
                context.Error.WriteLine(
                    "no such situation: " + situationId + ". Known situations are " + SceneSituations.KnownIds()
                    + " (run scene --list-situations).");
                return LabExit.UsageError;
            }

            SceneFixture fixture = situation.Build(context.Seed);

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
                new DialogueRealizer(library), new VanillaStyleCheckResolver(fixture.Vanilla));

            IReadOnlyList<StoryletOpportunity> opportunities = engine.Find(new StoryletCastingContext(
                fixture.World, fixture.Vanilla, fixture.Thread, fixture.FocusFactId));

            context.Header("the situation");
            context.WriteLine(situation.Id + " - " + situation.Summary);
            context.WriteLine("focus: " + Describe(fixture));
            context.WriteLine();
            context.WriteLine(NarrativeInspector.DescribeThread(fixture.World, fixture.Thread));

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
                    fixture.World, fixture.Vanilla, fixture.Thread)
                {
                    Rng = new DeterministicRng(context.Seed),
                    InPublic = opportunity.Definition.ToneTags.Contains("public"),
                    ApplyConsequences = apply
                });

                context.WriteLine(NarrativeInspector.DescribeStoryletPlay(fixture.World, play));
                played++;
            }

            if (played == 0)
            {
                // Why nothing played is the interesting half of a probe like this, and it is the
                // engine's own answer rather than a guess: `Evaluate` names the rule that refused.
                context.Error.WriteLine("no routed storylet could be cast on " + situation.Id
                    + (only == null ? string.Empty : " matching " + only)
                    + ". The engine refused every one; nothing here invents a scene to fill the gap.");
                Refusals(context, bundle, fixture, only);
                return LabExit.ScenarioFailure;
            }

            context.Header("what the town is left with");
            context.WriteLine(NarrativeInspector.DescribeHistory(fixture.World, limit: 12));
            return LabExit.Success;
        }

        /// <summary>
        /// What the engine said about each routed storylet it turned down, for a fixture that
        /// produced no playable scene. Read straight off `StoryletEngine.Evaluate`, so the probe
        /// reports the production refusal rather than re-deriving one.
        /// </summary>
        private static void Refusals(LabRunContext context, ContentBundle bundle, SceneFixture fixture, string only)
        {
            StoryletCastingContext casting = new StoryletCastingContext(
                fixture.World, fixture.Vanilla, fixture.Thread, fixture.FocusFactId);

            IReadOnlyList<ContentDiagnostic> ignored;
            foreach (StoryletDefinition definition in StoryletContent.LoadDefinitions(bundle, out ignored)
                         .OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                if (!definition.IsRouted || (only != null && !string.Equals(only, definition.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                StoryletOpportunity refused = StoryletEngine.Evaluate(definition, casting);
                if (!refused.IsAvailable)
                {
                    context.Error.WriteLine("  " + definition.Id + ": " + refused.RefusalReason);
                }
            }
        }

        private static void ListSituations(LabRunContext context)
        {
            context.Header("situations");
            foreach (SceneSituation situation in SceneSituations.All)
            {
                context.WriteLine(Pad(situation.Id, 12) + Pad(situation.Predicate, 20) + situation.Summary);
            }

            context.WriteLine();
            context.WriteLine("The predicate column is what a storylet's FocusPredicate has to name for");
            context.WriteLine("it to be eligible here. A situation is listed only where a production");
            context.WriteLine("world builder authors that predicate and shipped content declares it.");
        }

        /// <summary>The focus as the world holds it, so a reader can see what the scenes are about.</summary>
        private static string Describe(SceneFixture fixture)
        {
            Fact focus = fixture.Focus;
            return focus == null
                ? "(the fixture built no focus fact)"
                : FactPhrasing.Claim(fixture.World.Registry, focus) + "  [" + focus.Predicate + ", " + focus.Truth + "]";
        }

        private static string Pad(string text, int width)
        {
            return text.Length >= width ? text + " " : text + new string(' ', width - text.Length);
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
