using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Reimplements the resolution shape observed in Elin's Check class, with the arithmetic each
    /// <see cref="CheckFamily"/> actually wants (BQa-003 classified them; BQa-004 splits the sum).
    ///
    /// An <see cref="CheckFamily.Absolute"/> challenge is a fixed thing in the world, so it stays
    /// the flat sum it always was:
    ///
    ///     final DC = base DC - acting character's element/skill contribution + situational modifiers
    ///
    /// An <see cref="CheckFamily.Opposed"/> check is a contest, so what matters is the *ratio*
    /// between the two sides rather than the gap between them:
    ///
    ///     actorPower  = weighted composite of the actor's declared opposed capability
    ///     targetPower = weighted composite of the declared opposing capability
    ///     powerBands  = log2(stabilize(actorPower) / stabilize(targetPower))
    ///     final DC    = base DC - wholeNumber(powerBands x DcPerPowerBand) + situational modifiers
    ///
    /// Why: Elin's progression is effectively open-ended, and a flat difference stops meaning
    /// anything once both sides are large - 200 against 180 and 20 against 0 are the same twenty
    /// points and nothing like the same contest. A ratio keeps its meaning at any scale, so one
    /// doubling of relative power is worth <see cref="DcPerPowerBand"/> DC whether it is 2 against
    /// 1 or 2000 against 1000. Mastery is deliberately left uncapped: enough relative power really
    /// can trivialise weak opposition. A fixed challenge does not rise to meet the player, which is
    /// exactly why the absolute branch does not go through any of this.
    ///
    /// Neither branch is a staging post on the way to native resolution: this resolver is the
    /// authority for composite BQ checks, and stays so while `Check.Perform` rolls Elin's RNG
    /// rather than the persisted stream a replay is held to (`ICheckResolver`, `D079`, `D080`).
    /// </summary>
    public sealed class VanillaStyleCheckResolver : ICheckResolver
    {
        /// <summary>
        /// DC per doubling of relative power. Three keeps a band worth about the same as vanilla's
        /// own modest single-element terms, so one clear advantage moves a d20 noticeably without
        /// a single band settling the roll on its own.
        /// </summary>
        public const double DcPerPowerBand = 3.0;

        /// <summary>
        /// The one deterministic stabilizer, applied identically to both composites (BQa-004).
        ///
        /// A logarithm has nothing to say about zero, a negative composite or a non-finite one, and
        /// a ratio needs a floor on both sides or the same missing stat would read as infinite
        /// advantage on one side and infinite disadvantage on the other. Lifting both sides to the
        /// same floor keeps the ratio finite and keeps it exactly antisymmetric: no opposition at
        /// all reads as the weakest real opposition, never as a division by nothing.
        /// </summary>
        public const double MinimumStabilizedPower = 1.0;

        /// <summary>
        /// How near a whole number counts as being on it.
        ///
        /// The boundaries that matter - parity, 2:1, 4:1 - are exact powers of two, so their raw
        /// adjustments are mathematically whole. Binary floating point can land a unit in the last
        /// place below such a value, and truncation would then quietly hand back a band that was
        /// fully earned. This snaps that representation error out before the whole-number rule
        /// runs, so the boundary case is decided by the mathematics and not by the encoding.
        /// </summary>
        private const double WholeNumberTolerance = 1e-9;

        /// <summary>
        /// Bound on the adjustment. Not a mastery cap - no reachable composite comes anywhere near
        /// it, because the logarithm turns even an absurd stat into a double-digit band count. It
        /// exists so a malformed weight cannot carry the DC out of <see cref="int"/> entirely.
        /// </summary>
        private const double AdjustmentGuard = 1000000.0;

        private readonly IVanillaState _vanilla;

        public VanillaStyleCheckResolver(IVanillaState vanilla)
        {
            _vanilla = vanilla;
        }

        public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
        {
            CheckProfile profile = request.Profile;
            List<CheckTerm> terms = new List<CheckTerm>();
            int dc = profile.BaseDifficulty;
            OpposedPowerTrace opposition = null;

            if (profile.Family == CheckFamily.Opposed)
            {
                opposition = WeighRelativePower(request);
                if (opposition.AppliedDcAdjustment != 0)
                {
                    // Subtracted: a positive adjustment is the actor's advantage.
                    terms.Add(new CheckTerm("opposed power", -opposition.AppliedDcAdjustment));
                    dc -= opposition.AppliedDcAdjustment;
                }
            }
            else
            {
                dc = AddFixedChallengeTerms(request, terms, dc);
            }

            // Situational modifiers are never part of either composite: fame, rapport and hard
            // rain are facts about the occasion, not capability either side brought to it, and
            // folding them into the ratio would make them scale with the contest.
            foreach (SituationalModifier modifier in request.Modifiers)
            {
                terms.Add(new CheckTerm(modifier.Label, modifier.DcDelta));
                dc += modifier.DcDelta;
            }

            // Die and critical windows come from the profile, matching SourceCheck's per-row
            // dice / critRange / fumbleRange rather than assuming d20 with 20 and 1. Mastery
            // lowers the DC; it never abolishes a fumble the profile explicitly kept.
            int roll = rng.Roll(profile.Dice);
            CheckOutcome outcome;
            if (profile.CritRange > 0 && roll > profile.Dice - profile.CritRange)
            {
                outcome = CheckOutcome.CriticalPass;
            }
            else if (profile.FumbleRange > 0 && roll <= profile.FumbleRange)
            {
                outcome = CheckOutcome.CriticalFail;
            }
            else
            {
                outcome = roll >= dc ? CheckOutcome.Pass : CheckOutcome.Fail;
            }

            return new CheckResult(profile.Id, profile.BaseDifficulty, terms, dc, roll, outcome, opposition);
        }

        /// <summary>
        /// The flat sum, for a challenge that is not contesting anybody. Unchanged by BQa-004: a
        /// lock is as hard as it is, and relative power has no second side to be relative to.
        /// </summary>
        private int AddFixedChallengeTerms(CheckRequest request, List<CheckTerm> terms, int dc)
        {
            CheckProfile profile = request.Profile;

            foreach (CheckProfile.WeightedSkill skill in profile.ActorSkills)
            {
                int delta = -Scale(_vanilla.GetSkill(request.Actor, skill.Skill), skill.Weight);
                if (delta != 0)
                {
                    terms.Add(new CheckTerm(skill.Skill.ToString(), delta));
                    dc += delta;
                }
            }

            foreach (CheckProfile.WeightedAttribute attribute in profile.ActorAttributes)
            {
                int delta = -Scale(_vanilla.GetAttribute(request.Actor, attribute.Attribute), attribute.Weight);
                if (delta != 0)
                {
                    terms.Add(new CheckTerm(attribute.Attribute.ToString(), delta));
                    dc += delta;
                }
            }

            return dc;
        }

        /// <summary>
        /// Builds both capability composites and turns the ratio between them into whole DC.
        ///
        /// Composition happens in double precision and rounds once at the end. Rounding each stat
        /// to an integer first, as the flat sum does, would put the rounding inside the ratio where
        /// it distorts small composites much more than large ones.
        /// </summary>
        private OpposedPowerTrace WeighRelativePower(CheckRequest request)
        {
            CheckProfile profile = request.Profile;
            List<CheckPowerTerm> contributions = new List<CheckPowerTerm>();
            double actorPower = 0.0;
            double targetPower = 0.0;

            foreach (CheckProfile.WeightedSkill skill in profile.ActorSkills)
            {
                double value = _vanilla.GetSkill(request.Actor, skill.Skill) * skill.Weight;
                actorPower += value;
                Record(contributions, skill.Skill.ToString(), value, false);
            }

            foreach (CheckProfile.WeightedAttribute attribute in profile.ActorAttributes)
            {
                double value = _vanilla.GetAttribute(request.Actor, attribute.Attribute) * attribute.Weight;
                actorPower += value;
                Record(contributions, attribute.Attribute.ToString(), value, false);
            }

            // No target means the opposing terms are simply absent, as they have always been: an
            // opposed check with nobody to oppose keeps the actor's side and loses the other one.
            if (!request.Target.IsNone)
            {
                // Level enters the opposition only where the profile declared it does (BQa-003).
                // Nothing infers a universal level term from a profile that stayed quiet.
                if (profile.TargetLevelIsOpposition)
                {
                    double value = _vanilla.GetLevel(request.Target) * profile.TargetLevelWeight;
                    targetPower += value;
                    Record(contributions, "target level", value, true);
                }

                foreach (CheckProfile.WeightedAttribute resist in profile.TargetAttributes)
                {
                    double value = _vanilla.GetAttribute(request.Target, resist.Attribute) * resist.Weight;
                    targetPower += value;
                    Record(contributions, "target " + resist.Attribute, value, true);
                }
            }

            double stabilizedActor = Stabilize(actorPower);
            double stabilizedTarget = Stabilize(targetPower);
            double bands = Log2(stabilizedActor / stabilizedTarget);
            double raw = bands * DcPerPowerBand;

            return new OpposedPowerTrace(
                actorPower,
                targetPower,
                stabilizedActor,
                stabilizedTarget,
                bands,
                raw,
                WholeBands(raw),
                contributions);
        }

        private static void Record(List<CheckPowerTerm> contributions, string label, double value, bool isOpposing)
        {
            // A stat worth nothing is dropped here for the same reason it is dropped from the DC
            // trace: a check that listed every term that happened not to matter is unreadable, and
            // a term nobody could read says so itself through CheckRequest.WithUnreadTerm (D017).
            if (value != 0.0)
            {
                contributions.Add(new CheckPowerTerm(label, value, isOpposing));
            }
        }

        /// <summary>Lifts a composite to the shared floor. Also catches negative and NaN inputs.</summary>
        private static double Stabilize(double power)
        {
            return power > MinimumStabilizedPower ? power : MinimumStabilizedPower;
        }

        /// <summary>netstandard2.0 has no Math.Log2; the quotient form is exact on powers of two.</summary>
        private static double Log2(double ratio)
        {
            return Math.Log(ratio) / Math.Log(2.0);
        }

        /// <summary>
        /// The whole-number rule, chosen rather than inherited (BQa-004): round toward zero, in
        /// both directions.
        ///
        /// A partial band is a band neither side finished earning, so neither side is paid for it.
        /// The point of naming this is that C#'s cast and a mathematical floor disagree below zero,
        /// and taking whichever one fell out of the expression would make a slight disadvantage
        /// cost a whole band while the mirror-image slight advantage earned nothing. Toward zero is
        /// the only rule for which swapping the two sides exactly negates the result, which is the
        /// same symmetry the stabilizer and the logarithm already have.
        /// </summary>
        private static int WholeBands(double raw)
        {
            if (double.IsNaN(raw))
            {
                return 0;
            }

            if (raw > AdjustmentGuard)
            {
                return (int)AdjustmentGuard;
            }

            if (raw < -AdjustmentGuard)
            {
                return -(int)AdjustmentGuard;
            }

            double nearestWhole = Math.Round(raw, MidpointRounding.AwayFromZero);
            if (Math.Abs(raw - nearestWhole) <= WholeNumberTolerance)
            {
                return (int)nearestWhole;
            }

            return (int)raw;
        }

        private static int Scale(int value, double weight)
        {
            double scaled = value * weight;
            return (int)(scaled >= 0 ? scaled + 0.5 : scaled - 0.5);
        }
    }
}
