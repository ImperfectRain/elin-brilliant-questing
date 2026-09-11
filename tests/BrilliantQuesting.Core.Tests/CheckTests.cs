using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
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
            CheckProfile profile = new CheckProfile("test_small_die", CheckFamily.Absolute, 99).WithDice(6, critRange: 2, fumbleRange: 2);
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
            CheckProfile profile = new CheckProfile("test_bad_die", CheckFamily.Absolute, 10).WithDice(0, critRange: -3, fumbleRange: -2);

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

            CheckProfile profile = new CheckProfile("test_single_element", CheckFamily.Opposed, 14)
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

        // --- BQa-003: check-family classification -------------------------------------------

        [Fact]
        public void EveryRegisteredProfileIsClassifiedAndItsTermsAgreeWithItsFamily()
        {
            Assert.NotEmpty(ProceduralCheckProfiles.ProfileIds);

            foreach (string id in ProceduralCheckProfiles.ProfileIds)
            {
                CheckProfile profile = ProceduralCheckProfiles.ById(id);
                Assert.NotNull(profile);

                // A profile is a roll; the no-roll family never belongs on one.
                Assert.NotEqual(CheckFamily.Certain, profile.Family);

                if (profile.Family == CheckFamily.Opposed)
                {
                    // An opposed row that names nobody on the other side is an absolute check
                    // wearing the wrong label, and would pick up opposed scaling it never earned.
                    Assert.True(profile.DeclaresOpposition, id + " is opposed but declares no opposition");
                }
                else
                {
                    Assert.False(profile.DeclaresOpposition, id + " is absolute but declares opposition");
                    Assert.Equal(0.0, profile.TargetLevelWeight);
                    Assert.False(profile.TargetLevelIsOpposition);
                }

                // Whatever the family, an actor has to bring something to the attempt.
                Assert.True(
                    profile.ActorSkills.Count + profile.ActorAttributes.Count > 0,
                    id + " reads nothing off the actor");
            }
        }

        [Fact]
        public void AnAbsoluteProfileCannotQuietlyAcquireOpposition()
        {
            CheckProfile fixedChallenge = new CheckProfile("test_absolute", CheckFamily.Absolute, 12);

            Assert.Throws<InvalidOperationException>(
                () => fixedChallenge.WithTargetAttribute(VanillaAttribute.Will, 0.3));
            Assert.Throws<InvalidOperationException>(() => fixedChallenge.WithTargetLevel(0.25));
            Assert.Throws<ArgumentException>(
                () => new CheckProfile("test_certain", CheckFamily.Certain, 12));
        }

        [Fact]
        public void TargetLevelCountsAsOppositionOnlyWhereAProfileDeclaredIt()
        {
            // The only two profiles that may read a target's level are the ones that said they
            // are contests against a person. Nothing infers a universal level term.
            List<string> withLevel = new List<string>();
            foreach (string id in ProceduralCheckProfiles.ProfileIds)
            {
                CheckProfile profile = ProceduralCheckProfiles.ById(id);
                if (profile.TargetLevelWeight != 0.0)
                {
                    Assert.Equal(CheckFamily.Opposed, profile.Family);
                    Assert.True(profile.TargetLevelIsOpposition);
                    withLevel.Add(id);
                }
            }

            Assert.Equal(
                new[] { "proc_capture", "proc_extortion", "proc_intimidation" },
                Sorted(withLevel));
            Assert.False(ProceduralCheckProfiles.Deception.TargetLevelIsOpposition);
        }

        [Fact]
        public void EveryStandardVerbHasAClassifiedAnswerIncludingTheOnesThatRollNothing()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();
            Assert.NotEmpty(registry.Actions);

            List<string> rollNothing = new List<string>();
            foreach (NarrativeAction action in registry.Actions)
            {
                CheckFamily family = ProceduralCheckProfiles.FamilyForAction(action.Id);
                CheckProfile profile = ProceduralCheckProfiles.ForAction(action.Id);

                if (family == CheckFamily.Certain)
                {
                    Assert.Null(profile);
                    rollNothing.Add(action.Id);
                }
                else
                {
                    Assert.NotNull(profile);
                    Assert.Equal(profile.Family, family);
                }
            }

            // Named rather than counted: a verb losing its check should fail here, not be absorbed
            // into a tally. These are settled before any dice - you have the offering or you do
            // not, and the counter deals with you or it does not.
            Assert.Contains("make_offering", rollNothing);
            Assert.Contains("buy_supplies", rollNothing);
            Assert.Contains("invest_in_supplier", rollNothing);
        }

        // --- BQa-003: representative portable DC calculations --------------------------------

        [Fact]
        public void AnOpposedCompositeDcIsTheSumOfItsDeclaredTerms()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 20);
            vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 12);
            vanilla.SetAttribute(Player, VanillaAttribute.Strength, 30);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, 16);

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Intimidation, Player, Guard)
                .WithModifier("they have seen you lose a fight", 2);

            // base 12 + level(10 x 0.3 = 3) + will(16 x 0.35 = 6 rounded from 5.6)
            //   - negotiation(20 x 0.2 = 4) - charisma(12 x 0.15 = 2 rounded from 1.8)
            //   - strength(30 x 0.3 = 9) + 2
            CheckResult result = new VanillaStyleCheckResolver(vanilla).Resolve(request, new DeterministicRng(7));
            Assert.Equal(8, result.FinalDifficulty);
            Assert.Equal(12, result.BaseDifficulty);
        }

        [Fact]
        public void AnAbsoluteDcDoesNotMoveWithTheTarget()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 60);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, 90);
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 90);
            vanilla.SetSkill(Player, VanillaSkill.Lockpicking, 10);
            vanilla.SetSkill(Player, VanillaSkill.Stealth, 12);
            vanilla.SetAttribute(Player, VanillaAttribute.Dexterity, 15);

            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);
            CheckProfile burglary = ProceduralCheckProfiles.Burglary;

            // base 13 - lockpicking(10 x 0.35 = 4 rounded from 3.5) - stealth(12 x 0.25 = 3)
            //   - dexterity(15 x 0.2 = 3)
            int againstNobody = resolver
                .Resolve(new CheckRequest(burglary, Player, EntityId.None), new DeterministicRng(1))
                .FinalDifficulty;
            int withAFormidableBystander = resolver
                .Resolve(new CheckRequest(burglary, Player, Guard), new DeterministicRng(1))
                .FinalDifficulty;

            Assert.Equal(3, againstNobody);
            Assert.Equal(againstNobody, withAFormidableBystander);
        }

        [Fact]
        public void AnOpposedProfileWithNoTargetLosesOnlyItsOpposingTerms()
        {
            SandboxVanillaState vanilla = World();
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 10);
            vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 10);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, 20);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            CheckResult unopposed = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Persuasion, Player, EntityId.None), new DeterministicRng(2));
            CheckResult opposed = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Persuasion, Player, Guard), new DeterministicRng(2));

            // base 11 - negotiation(4) - charisma(3), then the target's Will(4) on top when there
            // is a target. An opposed check with nobody to oppose is the actor's terms alone; it
            // does not fall back to the base difficulty or refuse to resolve.
            Assert.Equal(4, unopposed.FinalDifficulty);
            Assert.Equal(8, opposed.FinalDifficulty);
            Assert.DoesNotContain("Will", unopposed.Explain());
        }

        [Fact]
        public void AStatNobodyCanReadContributesNothingAndSaysNothing()
        {
            // Both vanilla states answer an unknown skill, an unbound actor and a disabled read
            // capability with 0, so a term that could not be read is indistinguishable in the
            // trace from a term that was read and happened to be 0. That is a real limit of the
            // audit surface, recorded here rather than assumed away: a check that wants to say
            // "never asked" has to say so itself, through CheckRequest.WithUnreadTerm.
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            CheckResult silent = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Deception, Player, Guard), new DeterministicRng(4));

            Assert.Equal(ProceduralCheckProfiles.Deception.BaseDifficulty, silent.FinalDifficulty);
            Assert.Empty(silent.Terms);

            CheckResult declared = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Deception, Player, Guard)
                    .WithUnreadTerm("this build cannot read notoriety"),
                new DeterministicRng(4));

            Assert.Equal(ProceduralCheckProfiles.Deception.BaseDifficulty, declared.FinalDifficulty);
            Assert.Contains("this build cannot read notoriety", declared.Explain());
        }

        private static string[] Sorted(List<string> values)
        {
            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        private static CheckRequest Request()
        {
            CheckProfile profile = new CheckProfile("test_deception", CheckFamily.Opposed, 12)
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
