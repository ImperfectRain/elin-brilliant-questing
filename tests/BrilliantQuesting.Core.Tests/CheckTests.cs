using System;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class CheckTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");

        private static SandboxVanillaState World()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 20);
            return vanilla;
        }

        [Fact]
        public void SkillLowersTheDifficultyAndTheTraceSaysSo()
        {
            SandboxVanillaState vanilla = World();
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 0);
            int unskilled = resolver.Resolve(Request(), new DeterministicRng(1)).FinalDifficulty;

            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 40);
            CheckResult skilled = resolver.Resolve(Request(), new DeterministicRng(1));

            Assert.True(skilled.FinalDifficulty < unskilled);
            Assert.Contains("Negotiation", skilled.Explain());
        }

        [Fact]
        public void NaturalTwentyAndNaturalOneAlwaysCritical()
        {
            SandboxVanillaState vanilla = World();
            // An impossible difficulty: only the natural 20 can pass it.
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 400);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            bool sawCriticalPass = false;
            bool sawCriticalFail = false;
            for (int seed = 0; seed < 200 && !(sawCriticalPass && sawCriticalFail); seed++)
            {
                CheckResult result = resolver.Resolve(Request(), new DeterministicRng((ulong)seed));
                if (result.Roll == 20)
                {
                    Assert.Equal(CheckOutcome.CriticalPass, result.Outcome);
                    sawCriticalPass = true;
                }

                if (result.Roll == 1)
                {
                    Assert.Equal(CheckOutcome.CriticalFail, result.Outcome);
                    sawCriticalFail = true;
                }
            }

            Assert.True(sawCriticalPass && sawCriticalFail, "expected both criticals across 200 seeds");
        }

        [Fact]
        public void ProfileDiceAndCriticalWindowsAreHonoured()
        {
            SandboxVanillaState vanilla = World();
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);
            CheckProfile profile = new CheckProfile("test_small_die", 99).WithDice(6, critRange: 2, fumbleRange: 2);
            CheckRequest request = new CheckRequest(profile, Player, Guard);

            Assert.Equal(CheckOutcome.CriticalFail, resolver.Resolve(request, RngThatRolls(6, 1)).Outcome);
            Assert.Equal(CheckOutcome.CriticalFail, resolver.Resolve(request, RngThatRolls(6, 2)).Outcome);
            Assert.Equal(CheckOutcome.Fail, resolver.Resolve(request, RngThatRolls(6, 3)).Outcome);
            Assert.Equal(CheckOutcome.Fail, resolver.Resolve(request, RngThatRolls(6, 4)).Outcome);
            Assert.Equal(CheckOutcome.CriticalPass, resolver.Resolve(request, RngThatRolls(6, 5)).Outcome);
            Assert.Equal(CheckOutcome.CriticalPass, resolver.Resolve(request, RngThatRolls(6, 6)).Outcome);
        }

        [Fact]
        public void InvalidDiceSettingsAreClampedToPlayableValues()
        {
            CheckProfile profile = new CheckProfile("test_bad_die", 10).WithDice(0, critRange: -3, fumbleRange: -2);

            Assert.Equal(2, profile.Dice);
            Assert.Equal(0, profile.CritRange);
            Assert.Equal(0, profile.FumbleRange);
        }

        [Fact]
        public void PortableSingleElementDistributionMatchesSourceCheckShape()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 12);
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 18);
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 9);

            CheckProfile profile = new CheckProfile("test_single_element", 14)
                .WithActorSkill(VanillaSkill.Negotiation, 0.5)
                .WithTargetAttribute(VanillaAttribute.Perception, 0.5)
                .WithTargetLevel(0.25)
                .WithDice(20, critRange: 1, fumbleRange: 1);
            CheckRequest request = new CheckRequest(profile, Player, Guard).WithModifier("hard rain", 2);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            int[] portable = new int[4];
            int[] sourceCheckShape = new int[4];
            for (ulong seed = 0; seed < 1000; seed++)
            {
                portable[(int)resolver.Resolve(request, new DeterministicRng(seed)).Outcome]++;
                sourceCheckShape[(int)ResolveSingleElementLikeSourceCheck(request, vanilla, new DeterministicRng(seed))]++;
            }

            for (int i = 0; i < portable.Length; i++)
            {
                Assert.InRange(Math.Abs(portable[i] - sourceCheckShape[i]), 0, 1);
            }
        }

        [Fact]
        public void SituationalModifiersAppearInTheExplanation()
        {
            SandboxVanillaState vanilla = World();
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            CheckRequest request = Request().WithModifier("they have proof", 8);
            CheckResult result = resolver.Resolve(request, new DeterministicRng(3));

            Assert.Contains("they have proof", result.Explain());
        }

        [Fact]
        public void ATerribleLiarCanStillRollAndSometimesWin()
        {
            SandboxVanillaState vanilla = World();
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 0);
            vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 3);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            bool everSucceeded = false;
            for (int seed = 0; seed < 200; seed++)
            {
                if (resolver.Resolve(Request(), new DeterministicRng((ulong)seed)).Succeeded)
                {
                    everSucceeded = true;
                    break;
                }
            }

            Assert.True(everSucceeded, "a hopeless liar should still be allowed to get lucky");
        }

        private static CheckRequest Request()
        {
            CheckProfile profile = new CheckProfile("test_deception", 12)
                .WithActorSkill(VanillaSkill.Negotiation, 0.4)
                .WithActorAttribute(VanillaAttribute.Charisma, 0.25)
                .WithTargetAttribute(VanillaAttribute.Perception, 0.25);
            return new CheckRequest(profile, Player, Guard);
        }

        private static DeterministicRng RngThatRolls(int dice, int roll)
        {
            for (ulong seed = 0; seed < 10000; seed++)
            {
                DeterministicRng rng = new DeterministicRng(seed);
                if (rng.Roll(dice) == roll)
                {
                    return new DeterministicRng(seed);
                }
            }

            throw new InvalidOperationException("No seed found for roll " + roll + " on d" + dice + ".");
        }

        private static CheckOutcome ResolveSingleElementLikeSourceCheck(CheckRequest request, IVanillaState vanilla, DeterministicRng rng)
        {
            CheckProfile profile = request.Profile;
            int dc = profile.BaseDifficulty;

            if (!request.Target.IsNone && profile.TargetLevelWeight != 0.0)
            {
                dc += Scale(vanilla.GetLevel(request.Target), profile.TargetLevelWeight);
            }

            Assert.True(profile.ActorSkills.Count <= 1);
            Assert.Empty(profile.ActorAttributes);
            Assert.True(profile.TargetAttributes.Count <= 1);

            if (!request.Target.IsNone && profile.TargetAttributes.Count == 1)
            {
                CheckProfile.WeightedAttribute resist = profile.TargetAttributes[0];
                dc += Scale(vanilla.GetAttribute(request.Target, resist.Attribute), resist.Weight);
            }

            if (profile.ActorSkills.Count == 1)
            {
                CheckProfile.WeightedSkill skill = profile.ActorSkills[0];
                dc -= Scale(vanilla.GetSkill(request.Actor, skill.Skill), skill.Weight);
            }

            foreach (SituationalModifier modifier in request.Modifiers)
            {
                dc += modifier.DcDelta;
            }

            int roll = rng.Roll(profile.Dice);
            if (profile.CritRange > 0 && roll > profile.Dice - profile.CritRange)
            {
                return CheckOutcome.CriticalPass;
            }

            if (profile.FumbleRange > 0 && roll <= profile.FumbleRange)
            {
                return CheckOutcome.CriticalFail;
            }

            return roll >= dc ? CheckOutcome.Pass : CheckOutcome.Fail;
        }

        private static int Scale(int value, double weight)
        {
            double scaled = value * weight;
            return (int)(scaled >= 0 ? scaled + 0.5 : scaled - 0.5);
        }
    }
}
