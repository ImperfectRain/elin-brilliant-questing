using System;
using System.Collections.Generic;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// The dimensions a caller may move without authoring a preset.
    ///
    /// Deliberately few, and every one of them names a piece of state rather than an outcome: who
    /// is talking, what the tie between them is, how the speaker came by the claim, how firmly they
    /// hold it, how they sound, how many exchanges to play. There is no option that sets a
    /// strategy, a depth, a tactic or a line of dialogue, and there is not meant to be one - a
    /// command line that could ask for a refusal directly would make every run a tautology.
    ///
    /// Adding a dimension is a field here, a case in <see cref="PlaygroundRun.ApplyOverrides"/> and
    /// a declared option on the scenario.
    /// </summary>
    public sealed class PlaygroundOptions
    {
        public ulong Seed { get; set; }

        public string Preset { get; set; }

        public string Speaker { get; set; }

        public string Listener { get; set; }

        public string Voice { get; set; }

        /// <summary>The tie the speaker holds toward the listener, replacing whatever the preset set.</summary>
        public RelationKind? Tie { get; set; }

        /// <summary>How that tie is felt, -100 to 100.</summary>
        public int? Sentiment { get; set; }

        /// <summary>How the speaker came by the claim, for a speaker who does not already hold it.</summary>
        public KnowledgeSource? Knowledge { get; set; }

        /// <summary>How firmly the claim is held, when a route is being established.</summary>
        public double? Confidence { get; set; }

        public int? Turns { get; set; }

        /// <summary>Whether a promise made in the third exchange is promoted into the ledger.</summary>
        public bool Commit { get; set; } = true;

        /// <summary>
        /// Whether the third exchange is the request-and-promise one. True is the historic rule and
        /// the only thing a command line has ever asked for; the sweep turns it off so a repetition
        /// family can put the same question enough times to exhaust a pool.
        ///
        /// An input rather than an outcome: it says whether the listener asks for a favour at all,
        /// and settles nothing about what the speaker does with the request.
        /// </summary>
        public bool Undertaking { get; set; } = true;

        /// <summary>
        /// Days that pass between one exchange and the next. Zero is the historic rule, and the
        /// only thing a single conversation should normally do.
        ///
        /// Above zero the world runs between the turns - affect decays, threads advance - which is
        /// the only way a second answer can be taken from state the first one was not taken from.
        /// Nothing about the conversation is what changes; the world is.
        /// </summary>
        public long DaysBetweenTurns { get; set; }

        /// <summary>
        /// Further authoritative writes, applied in order after the preset and the overrides above.
        ///
        /// The seam the sweep is built on. Each one names the state it changed and can reach no
        /// outcome, for the same structural reason a preset cannot: what it is handed is the stage,
        /// and the stage exposes stores rather than decisions.
        /// </summary>
        public IReadOnlyList<PlaygroundInput> Inputs { get; set; }
    }

    /// <summary>
    /// One resolved playground run: the world, the state that was written into it, the two people
    /// in the chairs, and the exchange they had.
    ///
    /// Construction order is the order the systems run, and is the reason this is a type rather
    /// than a method: the preset writes state, the overrides write state, the run is fixed, and
    /// only then does anybody ask anybody anything. Nothing after <see cref="Begin"/> writes state
    /// except through Core - a disclosure that records a deception, a promise that is promoted -
    /// and both of those are reported as ledger movements rather than hidden.
    /// </summary>
    public sealed class PlaygroundRun
    {
        /// <summary>
        /// The most exchanges one run will play.
        ///
        /// Three was the whole of what the playground needed - a question, the same question again,
        /// and the undertaking - and the ceiling is higher only so a repetition family can ask often
        /// enough to exhaust a fragment pool and watch the degrade. It is not an invitation to stage
        /// a scene: every exchange past the first is still the same question about the same claim.
        /// </summary>
        public const int MaxTurns = 12;

        private readonly List<string> _overrides = new List<string>();

        private PlaygroundRun(PlaygroundStage stage, PlaygroundPreset preset)
        {
            Stage = stage;
            Preset = preset;
        }

        public PlaygroundStage Stage { get; }

        public PlaygroundPreset Preset { get; }

        public EntityId Speaker { get; private set; }

        public EntityId Listener { get; private set; }

        public string VoiceName { get; private set; }

        public VoiceProfile Voice { get; private set; }

        public int Turns { get; private set; }

        public bool Commit { get; private set; }

        /// <summary>Whether the third exchange is the request-and-promise one.</summary>
        public bool Undertaking { get; private set; }

        /// <summary>Days the world runs between one exchange and the next. Usually none.</summary>
        public long DaysBetweenTurns { get; private set; }

        /// <summary>Every override that actually changed something, in the order it was applied.</summary>
        public IReadOnlyList<string> Overrides => _overrides;

        public PlaygroundExchange Exchange { get; private set; }

        /// <summary>
        /// Opens a world, writes the preset's state into it, applies the caller's overrides, and
        /// plays the exchange.
        ///
        /// A command line that names something that does not exist is a usage error rather than a
        /// quieter run against a default: a developer who mistypes a preset should not be handed a
        /// neutral witness and left to wonder why the output looks ordinary.
        /// </summary>
        public static PlaygroundRun Begin(PlaygroundOptions options, PlaygroundPresets presets)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            presets = presets ?? PlaygroundPresets.Default();

            string presetId = string.IsNullOrWhiteSpace(options.Preset)
                ? PlaygroundPresets.DefaultPresetId
                : options.Preset;

            PlaygroundPreset preset = presets.Find(presetId)
                ?? throw new LabArgumentException("Unknown preset '" + presetId + "'. Run 'describe playground' for the list.");

            PlaygroundStage stage = PlaygroundStage.Open(options.Seed);
            preset.Apply(stage);

            PlaygroundRun run = new PlaygroundRun(stage, preset);
            run.Resolve(options);
            run.ApplyOverrides(options);
            run.ApplyInputs(options);

            run.Exchange = new PlaygroundExchange(stage, run);
            run.Exchange.Play();
            return run;
        }

        private void Resolve(PlaygroundOptions options)
        {
            string speakerRole = options.Speaker ?? Preset.Speaker;
            string listenerRole = options.Listener ?? Preset.Listener;

            Speaker = Stage.Resolve(speakerRole);
            if (Speaker.IsNone)
            {
                throw new LabArgumentException(
                    "Unknown speaker '" + speakerRole + "'. The stage holds: " + string.Join(", ", PlaygroundRoles.All) + ".");
            }

            Listener = Stage.Resolve(listenerRole);
            if (Listener.IsNone)
            {
                throw new LabArgumentException(
                    "Unknown listener '" + listenerRole + "'. The stage holds: " + string.Join(", ", PlaygroundRoles.All) + ".");
            }

            if (Speaker == Listener)
            {
                throw new LabArgumentException("The speaker and the listener cannot be the same person.");
            }

            VoiceName = options.Voice ?? Preset.Voice;
            Voice = PlaygroundVoices.Find(VoiceName)
                ?? throw new LabArgumentException(
                    "Unknown voice '" + VoiceName + "'. The laboratory ships: " + string.Join(", ", PlaygroundVoices.All) + ".");

            Turns = options.Turns ?? Preset.Turns;
            if (Turns < 1 || Turns > MaxTurns)
            {
                throw new LabArgumentException("--turns takes a whole number from 1 to " + MaxTurns + ".");
            }

            Commit = options.Commit;
            Undertaking = options.Undertaking;
            DaysBetweenTurns = options.DaysBetweenTurns;

            if (options.Speaker != null)
            {
                _overrides.Add("speaker: " + Stage.Describe(Speaker));
            }

            if (options.Listener != null)
            {
                _overrides.Add("listener: " + Stage.Describe(Listener));
            }

            if (options.Voice != null)
            {
                _overrides.Add("voice: " + VoiceName);
            }

            if (options.Turns != null)
            {
                _overrides.Add("turns: " + Turns);
            }
        }

        /// <summary>
        /// The state dimensions a caller may move. Each writes through the store that owns the
        /// state and then reports what the store actually holds, so an override the world declined
        /// - a knowledge route for somebody who already believes the claim - reads as declined
        /// rather than as applied.
        /// </summary>
        private void ApplyOverrides(PlaygroundOptions options)
        {
            if (options.Tie != null || options.Sentiment != null)
            {
                RelationshipEdge existing = Stage.World.Relationships.Find(Speaker, Listener);
                RelationKind kind = options.Tie ?? (existing?.Kind ?? RelationKind.Acquaintance);
                int sentiment = options.Sentiment ?? (existing?.Sentiment ?? 0);
                PlaygroundState.Tie(Stage, Speaker, Listener, kind, sentiment);
                _overrides.Add("tie: " + kind + " at sentiment " + sentiment);
            }

            if (options.Knowledge == null && options.Confidence == null)
            {
                return;
            }

            KnowledgeSource source = options.Knowledge ?? KnowledgeSource.Hearsay;
            double confidence = options.Confidence ?? 0.6;
            bool held = Stage.World.Knowledge.Knows(Speaker, Stage.SubjectFactId);

            KnowledgeRecord record = PlaygroundState.Believes(
                Stage, Speaker, Stage.SubjectFactId, source, confidence, source == KnowledgeSource.Witnessed);

            _overrides.Add(held
                ? "knowledge: asked for " + source + " at " + confidence.ToString("0.00")
                  + ", and the speaker already believed it - the graph strengthens rather than re-sources, so it stands as "
                  + record.Source + " at " + record.Confidence.ToString("0.00")
                : "knowledge: " + record.Source + " at " + record.Confidence.ToString("0.00"));
        }

        /// <summary>
        /// The sweep's own writes, in the order it listed them, after everything a command line can
        /// set. Each reports itself, so an input the world declined still reads as attempted.
        /// </summary>
        private void ApplyInputs(PlaygroundOptions options)
        {
            IReadOnlyList<PlaygroundInput> inputs = options.Inputs;
            if (inputs == null)
            {
                return;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                inputs[i].Apply(Stage, Speaker, Listener);
                _overrides.Add(inputs[i].Because);
            }
        }
    }
}
