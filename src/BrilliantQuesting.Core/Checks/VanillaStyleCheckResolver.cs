using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Reimplements the resolution shape observed in Elin's Check class:
    ///
    ///     final DC = base DC
    ///              + target level contribution
    ///              + target element contribution
    ///              - acting character's element/skill contribution
    ///              + situational modifiers
    ///     roll 1d20; natural 20 is a critical pass, natural 1 a critical fail,
    ///     otherwise roll >= final DC passes.
    ///
    /// This exists so the simulation can be developed and tested with no game process attached.
    /// In game the intention is still to defer to vanilla Check where it maps cleanly - the point
    /// of matching the arithmetic is that swapping resolvers should not re-balance the content.
    /// </summary>
    public sealed class VanillaStyleCheckResolver : ICheckResolver
    {
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

            if (!request.Target.IsNone && profile.TargetLevelWeight != 0.0)
            {
                int delta = Scale(_vanilla.GetLevel(request.Target), profile.TargetLevelWeight);
                if (delta != 0)
                {
                    terms.Add(new CheckTerm("target level", delta));
                    dc += delta;
                }
            }

            if (!request.Target.IsNone)
            {
                foreach (CheckProfile.WeightedAttribute resist in profile.TargetAttributes)
                {
                    int delta = Scale(_vanilla.GetAttribute(request.Target, resist.Attribute), resist.Weight);
                    if (delta != 0)
                    {
                        terms.Add(new CheckTerm("target " + resist.Attribute, delta));
                        dc += delta;
                    }
                }
            }

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

            foreach (SituationalModifier modifier in request.Modifiers)
            {
                terms.Add(new CheckTerm(modifier.Label, modifier.DcDelta));
                dc += modifier.DcDelta;
            }

            // Die and critical windows come from the profile, matching SourceCheck's per-row
            // dice / critRange / fumbleRange rather than assuming d20 with 20 and 1.
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

            return new CheckResult(profile.Id, profile.BaseDifficulty, terms, dc, roll, outcome);
        }

        private static int Scale(int value, double weight)
        {
            double scaled = value * weight;
            return (int)(scaled >= 0 ? scaled + 0.5 : scaled - 0.5);
        }
    }
}
