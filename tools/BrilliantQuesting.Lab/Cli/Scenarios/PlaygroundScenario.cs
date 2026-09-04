using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Playground;
using BrilliantQuesting.Relationships;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The conversation playground: one authored state, one exchange, and every system that had a
    /// say in it shown deciding.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground --preset loyal-liar
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground --preset settled-history --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground --preset promise-exchange --no-commit
    ///
    /// Every semantic answer in the output comes from Core. The scenario chooses a world, a state
    /// and two people; it does not choose a strategy, a depth, a tactic, a permit or a line, and
    /// there is no option that lets a caller choose one either.
    /// </summary>
    internal sealed class PlaygroundScenario : LabScenario
    {
        public override string Id => "playground";

        public override string Summary =>
            "one exchange over authored state, with every system that decided it shown deciding";

        public override string Description =>
            "Puts two of the theft laboratory's people in a conversation and prints the whole path:\n"
            + "what the world holds, what qualified whom for which scene, what the speaker decided about\n"
            + "the claim and why, what old business they may raise and would raise with this listener,\n"
            + "what constrained the wording, the line, and what any of it changed. Nothing is decided\n"
            + "here - the presets write state and Core answers.\n"
            + "\n"
            + PresetHelp()
            + "\n"
            + "voices: " + string.Join(", ", PlaygroundVoices.All) + "\n"
            + "roles:  " + string.Join(", ", PlaygroundRoles.All);

        public override ulong DefaultSeed => LabDefaults.Seed;

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("preset", "id", "which authored state to run", PlaygroundPresets.DefaultPresetId),
            new LabOption("speaker", "role", "who answers", "the preset's own"),
            new LabOption("listener", "role", "who asks", "the preset's own"),
            new LabOption("voice", "id", "how the speaker sounds", "the preset's own"),
            new LabOption("tie", "kind", "the speaker's tie to the listener", "the preset's own"),
            new LabOption("sentiment", "n", "how that tie is felt, -100 to 100", "the preset's own"),
            new LabOption("knowledge", "route", "how the speaker came by the claim", "the preset's own"),
            new LabOption("confidence", "0..1", "how firmly they hold it", "the preset's own"),
            new LabOption("turns", "1-12", "how many exchanges to play", "the preset's own"),
            new LabOption("no-commit", null, "leave a promise transient instead of promoting it")
        };

        public override int Run(LabRunContext context)
        {
            PlaygroundRun run = PlaygroundRun.Begin(
                ReadOptions(context.Arguments, context.Seed), PlaygroundPresets.Default());

            PlaygroundReporters.Default().Write(context.Output, run);
            return LabExit.Success;
        }

        /// <summary>
        /// Reads the state dimensions off the command line. Shared with the contrast scenario, so
        /// the two cannot drift about what an option means.
        /// </summary>
        internal static PlaygroundOptions ReadOptions(LabArguments arguments, ulong seed)
        {
            return new PlaygroundOptions
            {
                Seed = seed,
                Preset = arguments.String("preset", null),
                Speaker = arguments.String("speaker", null),
                Listener = arguments.String("listener", null),
                Voice = arguments.String("voice", null),
                Tie = Tie(arguments.String("tie", null)),
                Sentiment = arguments.Has("sentiment") ? arguments.Int("sentiment", 0) : (int?)null,
                Knowledge = Route(arguments.String("knowledge", null)),
                Confidence = Fraction(arguments, "confidence"),
                Turns = arguments.Has("turns") ? arguments.Int("turns", 2) : (int?)null,
                Commit = !arguments.Flag("no-commit")
            };
        }

        private static RelationKind? Tie(string value)
        {
            if (value == null)
            {
                return null;
            }

            foreach (RelationKind kind in (RelationKind[])Enum.GetValues(typeof(RelationKind)))
            {
                if (string.Equals(kind.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }

            throw new LabArgumentException(
                "Unknown tie '" + value + "'. The graph knows: " + string.Join(", ", Enum.GetNames(typeof(RelationKind))) + ".");
        }

        private static KnowledgeSource? Route(string value)
        {
            if (value == null)
            {
                return null;
            }

            foreach (KnowledgeSource source in (KnowledgeSource[])Enum.GetValues(typeof(KnowledgeSource)))
            {
                if (string.Equals(source.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return source;
                }
            }

            throw new LabArgumentException(
                "Unknown knowledge route '" + value + "'. The graph knows: "
                + string.Join(", ", Enum.GetNames(typeof(KnowledgeSource))) + ".");
        }

        private static double? Fraction(LabArguments arguments, string name)
        {
            string raw = arguments.String(name, null);
            if (raw == null)
            {
                return null;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || value < 0.0
                || value > 1.0)
            {
                throw new LabArgumentException("Option --" + name + " takes a number from 0 to 1, got '" + raw + "'.");
            }

            return value;
        }

        internal static string PresetHelp()
        {
            StringWriter help = new StringWriter();
            help.WriteLine("presets:");
            foreach (PlaygroundPreset preset in PlaygroundPresets.Default().All)
            {
                help.WriteLine("  " + LabText.Column(preset.Id, 20) + preset.Summary);
            }

            return help.ToString();
        }
    }
}
