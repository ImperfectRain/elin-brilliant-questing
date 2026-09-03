using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// One authored starting state for the conversation playground.
    ///
    /// A preset writes authoritative state and stops. It does not choose a strategy, a depth, a
    /// tactic, a permit or a line, and it has no way to: <see cref="Apply"/> is handed a
    /// <see cref="PlaygroundStage"/> and the stage exposes the stores, not the decisions. What a
    /// preset is claiming, therefore, is only "a world where this is true", and whether that world
    /// produces a refusal or a confession is Core's answer rather than the preset's.
    ///
    /// <see cref="Speaker"/>, <see cref="Listener"/> and <see cref="Voice"/> are defaults a caller
    /// may override. They are part of the preset because a state that only makes sense with a
    /// particular pair in the chairs would otherwise be one command line away from being
    /// misleading - a hostile witness aimed at the wrong listener is simply a different scene.
    ///
    /// Adding an experiment is a subclass plus one line in <see cref="PlaygroundPresets.Default"/>.
    /// </summary>
    public abstract class PlaygroundPreset
    {
        /// <summary>Canonical name for <c>--preset</c>. Lower case, hyphenated.</summary>
        public abstract string Id { get; }

        /// <summary>One line for the listing. No trailing full stop.</summary>
        public abstract string Summary { get; }

        /// <summary>What this state is meant to expose, for <c>describe</c>. Defaults to the summary.</summary>
        public virtual string Description => Summary;

        /// <summary>Who is answering, by role name.</summary>
        public virtual string Speaker => PlaygroundRoles.Witness;

        /// <summary>Who is asking, by role name.</summary>
        public virtual string Listener => PlaygroundRoles.Player;

        /// <summary>The voice handed to the speaker. Laboratory authorship - see <see cref="PlaygroundVoices"/>.</summary>
        public virtual string Voice => PlaygroundVoices.Neutral;

        /// <summary>
        /// How many exchanges the run plays by default: 1 the question, 2 the question asked
        /// again, 3 the request and the promise that answers it.
        /// </summary>
        public virtual int Turns => 2;

        /// <summary>Writes the state. Called once, before anything is asked of anybody.</summary>
        public abstract void Apply(PlaygroundStage stage);
    }

    /// <summary>
    /// The one place that knows which presets exist, indexed by id so nothing else matches names.
    /// The same shape <see cref="Cli.LabCatalog"/> has, and for the same reason.
    /// </summary>
    public sealed class PlaygroundPresets
    {
        /// <summary>The preset a run uses when the caller names none.</summary>
        public const string DefaultPresetId = "neutral-witness";

        private readonly List<PlaygroundPreset> _presets = new List<PlaygroundPreset>();
        private readonly Dictionary<string, PlaygroundPreset> _byId =
            new Dictionary<string, PlaygroundPreset>(StringComparer.OrdinalIgnoreCase);

        public PlaygroundPresets(IEnumerable<PlaygroundPreset> presets)
        {
            if (presets == null)
            {
                throw new ArgumentNullException(nameof(presets));
            }

            foreach (PlaygroundPreset preset in presets)
            {
                Register(preset);
            }
        }

        /// <summary>Every preset the playground ships. Add one here and nowhere else.</summary>
        public static PlaygroundPresets Default()
        {
            return new PlaygroundPresets(new PlaygroundPreset[]
            {
                new NeutralWitnessPreset(),
                new HostileWitnessPreset(),
                new TrustedConfidantPreset(),
                new LoyalLiarPreset(),
                new PrincipledRefuserPreset(),
                new KinLinePreset(),
                new HearsayVictimPreset(),
                new LivedTradePreset(),
                new SettledHistoryPreset(),
                new GuardedHistoryPreset(),
                new PromiseExchangePreset()
            });
        }

        /// <summary>Registration order, which is also the order the listing prints.</summary>
        public IReadOnlyList<PlaygroundPreset> All => _presets;

        public PlaygroundPreset Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return _byId.TryGetValue(id.Trim(), out PlaygroundPreset preset) ? preset : null;
        }

        public PlaygroundPreset DefaultPreset => Find(DefaultPresetId);

        private void Register(PlaygroundPreset preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (string.IsNullOrWhiteSpace(preset.Id))
            {
                throw new InvalidOperationException(preset.GetType().Name + " has no id.");
            }

            if (_byId.TryGetValue(preset.Id, out PlaygroundPreset existing))
            {
                throw new InvalidOperationException(
                    "Playground preset id '" + preset.Id + "' is claimed by both "
                    + existing.GetType().Name + " and " + preset.GetType().Name + ".");
            }

            _byId[preset.Id] = preset;
            _presets.Add(preset);
        }
    }
}
