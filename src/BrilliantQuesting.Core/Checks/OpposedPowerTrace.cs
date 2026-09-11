using System.Collections.Generic;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Why an opposed check's difficulty moved (BQa-004).
    ///
    /// An opposed DC is no longer a flat sum of everybody's stats, so the trace cannot be a list of
    /// signed integers any more without lying about how the number was reached. This records the
    /// step that a list of deltas cannot: the two capability composites, the log2 ratio between
    /// them, and the whole-number adjustment that actually reached the DC. The individual stats
    /// stay named in <see cref="Contributions"/> so "why did that happen" still has an answer
    /// without re-running the simulation.
    ///
    /// <see cref="CheckResult.Terms"/> keeps its invariant meanwhile: every entry in it is a real
    /// signed contribution to the DC, and this composite arrives there as the single
    /// <c>opposed power</c> term rather than as its parts.
    /// </summary>
    public sealed class OpposedPowerTrace
    {
        public OpposedPowerTrace(
            double actorPower,
            double targetPower,
            double stabilizedActorPower,
            double stabilizedTargetPower,
            double powerBands,
            double rawDcAdjustment,
            int appliedDcAdjustment,
            IReadOnlyList<CheckPowerTerm> contributions)
        {
            ActorPower = actorPower;
            TargetPower = targetPower;
            StabilizedActorPower = stabilizedActorPower;
            StabilizedTargetPower = stabilizedTargetPower;
            PowerBands = powerBands;
            RawDcAdjustment = rawDcAdjustment;
            AppliedDcAdjustment = appliedDcAdjustment;
            Contributions = contributions;
        }

        /// <summary>Weighted composite of the actor's side, before the stabilizer.</summary>
        public double ActorPower { get; }

        /// <summary>Weighted composite of the opposing side, before the stabilizer.</summary>
        public double TargetPower { get; }

        /// <summary>
        /// The actor composite as the logarithm actually saw it. Differs from
        /// <see cref="ActorPower"/> only where the stabilizer had to lift a zero, a fraction, a
        /// negative or a non-finite value off the floor.
        /// </summary>
        public double StabilizedActorPower { get; }

        /// <summary>The opposing composite as the logarithm actually saw it.</summary>
        public double StabilizedTargetPower { get; }

        /// <summary>
        /// Doublings of relative power: log2(stabilized actor / stabilized target). Positive means
        /// the actor is ahead. Exactly negated when the two sides are swapped.
        /// </summary>
        public double PowerBands { get; }

        /// <summary>Bands times the DC value of a band, before the whole-number rule.</summary>
        public double RawDcAdjustment { get; }

        /// <summary>
        /// The whole-number adjustment that reached the DC, which is subtracted from it: positive
        /// is an advantage. See <see cref="VanillaStyleCheckResolver"/> for the rounding rule.
        /// </summary>
        public int AppliedDcAdjustment { get; }

        /// <summary>
        /// Every stat that fed a composite, named and weighted. A stat worth 0 is dropped exactly
        /// as it is from <see cref="CheckResult.Terms"/>, and for the same reason.
        /// </summary>
        public IReadOnlyList<CheckPowerTerm> Contributions { get; }
    }

    /// <summary>One weighted stat inside a capability composite.</summary>
    public readonly struct CheckPowerTerm
    {
        public CheckPowerTerm(string label, double value, bool isOpposing)
        {
            Label = label;
            Value = value;
            IsOpposing = isOpposing;
        }

        public string Label { get; }

        /// <summary>The vanilla value already multiplied by its profile weight.</summary>
        public double Value { get; }

        /// <summary>Whether this fed the opposing composite rather than the actor's.</summary>
        public bool IsOpposing { get; }
    }
}
