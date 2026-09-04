using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One family of controlled comparisons: a baseline state, and a bounded set of rows that each
    /// move one named piece of it.
    ///
    /// A family is not a Cartesian product and is not meant to become one. The unit that answers
    /// "which input state changes this conversation" is one state against one changed input, and a
    /// combinatorial expansion of every axis against every other would produce a table nobody can
    /// read and no more information than the controlled pairs already carry.
    ///
    /// <b>An axis composes inputs and nothing else.</b> Every row is built from
    /// <see cref="PlaygroundInputs"/> over a preset, so an axis has no way to reach a strategy, a
    /// depth, a tactic, an act, a permit, a fragment or a line - the same structural guarantee the
    /// presets have. What an axis chooses is a world; what the row reports is Core's answer to it.
    ///
    /// Adding a family is a subclass plus one line in <see cref="PlaygroundSweepAxes.Default"/>.
    /// </summary>
    public abstract class PlaygroundSweepAxis
    {
        /// <summary>Canonical name for <c>--axis</c>. Lower case, hyphenated.</summary>
        public abstract string Id { get; }

        /// <summary>One line for the axis listing. No trailing full stop.</summary>
        public abstract string Summary { get; }

        /// <summary>The question the family exists to answer, printed above its table.</summary>
        public virtual string Question => Summary;

        /// <summary>What is deliberately held still, so a reader knows the comparison is controlled.</summary>
        public virtual string Held => "the claim, the two people talking, the seed and the weirdness ceiling";

        /// <summary>
        /// Whether the shared row table is printed for this family. False for a family whose useful
        /// output is its own tail section - a seed sweep is a distribution, not a row per point.
        /// </summary>
        public virtual bool PrintsRowTable => true;

        /// <summary>The rows, baseline first. Called once per run.</summary>
        public abstract IReadOnlyList<PlaygroundSweepRow> Rows(ulong seed);

        /// <summary>
        /// Whatever the shared table cannot say for this family. Read-only, like every reporter:
        /// the rows are already finished when this is called, and nothing here may run a system,
        /// take a decision or write to a world.
        /// </summary>
        public virtual void WriteTail(TextWriter output, PlaygroundSweepResult result)
        {
        }

        /// <summary>
        /// Checks that must hold over this family's rows, beyond the ones every family is held to.
        /// A violation fails the scenario.
        /// </summary>
        public virtual IReadOnlyList<PlaygroundSweepInvariant> Invariants => PlaygroundSweepInvariant.None;

        /// <summary>Builds one row by running the playground over a preset plus a list of inputs.</summary>
        protected static PlaygroundSweepRow Row(
            string label,
            ulong seed,
            string preset,
            IReadOnlyList<PlaygroundInput> inputs,
            Action<PlaygroundOptions> configure = null,
            bool baseline = false,
            int readAt = 1,
            string against = null,
            params string[] alsoChanged)
        {
            PlaygroundOptions options = new PlaygroundOptions
            {
                Seed = seed,
                Preset = preset,
                Turns = 1,
                Inputs = inputs
            };

            configure?.Invoke(options);

            List<string> changed = new List<string>();
            if (alsoChanged != null)
            {
                changed.AddRange(alsoChanged);
            }

            if (inputs != null)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    changed.Add(inputs[i].Because);
                }
            }

            return PlaygroundSweepRow.Of(
                label, baseline, changed, PlaygroundRun.Begin(options, PlaygroundPresets.Default()), readAt, against);
        }

        /// <summary>A list literal, so a row reads as the inputs it is rather than as plumbing.</summary>
        protected static IReadOnlyList<PlaygroundInput> Inputs(params PlaygroundInput[] inputs)
        {
            return inputs ?? new PlaygroundInput[0];
        }

        /// <summary>The constant part of a family plus this row's own change, in that order.</summary>
        protected static IReadOnlyList<PlaygroundInput> With(
            IReadOnlyList<PlaygroundInput> held, params PlaygroundInput[] changed)
        {
            List<PlaygroundInput> all = new List<PlaygroundInput>();
            if (held != null)
            {
                all.AddRange(held);
            }

            if (changed != null)
            {
                all.AddRange(changed);
            }

            return all;
        }
    }

    /// <summary>
    /// The one place that knows which families exist, indexed by id. The same shape
    /// <see cref="LabCatalog"/> and <see cref="PlaygroundPresets"/> have, and for the same reason.
    /// </summary>
    public sealed class PlaygroundSweepAxes
    {
        private readonly List<PlaygroundSweepAxis> _axes = new List<PlaygroundSweepAxis>();
        private readonly Dictionary<string, PlaygroundSweepAxis> _byId =
            new Dictionary<string, PlaygroundSweepAxis>(StringComparer.OrdinalIgnoreCase);

        public PlaygroundSweepAxes(IEnumerable<PlaygroundSweepAxis> axes)
        {
            if (axes == null)
            {
                throw new ArgumentNullException(nameof(axes));
            }

            foreach (PlaygroundSweepAxis axis in axes)
            {
                Register(axis);
            }
        }

        /// <summary>Every family the sweep ships. Add one here and nowhere else.</summary>
        public static PlaygroundSweepAxes Default()
        {
            return new PlaygroundSweepAxes(new PlaygroundSweepAxis[]
            {
                new RelationshipAxis(),
                new KnowledgeAxis(),
                new HonestyAxis(),
                new NegativeSpaceAxis(),
                new EmotionAxis(),
                new CallbackAxis(),
                new VoiceAxis(),
                new VocabularyAxis(),
                new RepetitionAxis(),
                new SeedAxis(),
                new ConversationAxis(),
                new OwnershipAxis()
            });
        }

        /// <summary>Registration order, which is also the order the listing prints.</summary>
        public IReadOnlyList<PlaygroundSweepAxis> All => _axes;

        public PlaygroundSweepAxis Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return _byId.TryGetValue(id.Trim(), out PlaygroundSweepAxis axis) ? axis : null;
        }

        private void Register(PlaygroundSweepAxis axis)
        {
            if (axis == null)
            {
                throw new ArgumentNullException(nameof(axis));
            }

            if (string.IsNullOrWhiteSpace(axis.Id))
            {
                throw new InvalidOperationException(axis.GetType().Name + " has no id.");
            }

            if (_byId.TryGetValue(axis.Id, out PlaygroundSweepAxis existing))
            {
                throw new InvalidOperationException(
                    "Sweep axis id '" + axis.Id + "' is claimed by both "
                    + existing.GetType().Name + " and " + axis.GetType().Name + ".");
            }

            _byId[axis.Id] = axis;
            _axes.Add(axis);
        }
    }
}
