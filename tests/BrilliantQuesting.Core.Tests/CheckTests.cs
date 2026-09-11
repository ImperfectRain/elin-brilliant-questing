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
            // The recorded baseline, still owned by the family it still describes. A fixed
            // challenge is the flat sum vanilla's own single-element row is, and BQa-004 left that
            // branch alone; the opposed half of this baseline moved on purpose and is measured in
            // OpposedScalingDepartsFromTheFlatSourceCheckSumOnPurpose below.
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 12);
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 18);
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 9);

            CheckProfile profile = new CheckProfile("test_single_element", CheckFamily.Absolute, 14)
                .WithActorSkill(VanillaSkill.Negotiation, 0.5)
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
        public void AnOpposedCompositeDcIsTheRatioBetweenItsDeclaredSides()
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

            // actor power = negotiation(20 x 0.2 = 4.0) + charisma(12 x 0.15 = 1.8)
            //   + strength(30 x 0.3 = 9.0) = 14.8
            // target power = will(16 x 0.35 = 5.6) + level(10 x 0.3 = 3.0) = 8.6
            // 14.8 / 8.6 is 0.783 bands, 2.35 raw DC, 2 whole bands.
            // base 12 - 2 + the situational 2, which stays outside the ratio.
            CheckResult result = new VanillaStyleCheckResolver(vanilla).Resolve(request, new DeterministicRng(7));
            Assert.Equal(12, result.FinalDifficulty);
            Assert.Equal(12, result.BaseDifficulty);

            OpposedPowerTrace power = Assert.IsType<OpposedPowerTrace>(result.Opposition);
            Assert.Equal(14.8, power.ActorPower, 6);
            Assert.Equal(8.6, power.TargetPower, 6);
            Assert.Equal(2, power.AppliedDcAdjustment);

            // Composition rounds once, at the end: 1.8 and 5.6 reach the composites whole rather
            // than being rounded to 2 and 6 inside the ratio, where small sides would distort most.
            Assert.Contains("Charisma 1.8", result.Explain());
            Assert.Contains("target Will 5.6", result.Explain());

            // The named contest term carries the whole adjustment; nothing else moved the DC.
            Assert.Equal(-2, Assert.Single(result.Terms, t => t.Label == "opposed power").Delta);
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

            // Actor power is negotiation(10 x 0.4 = 4.0) + charisma(10 x 0.3 = 3.0) = 7.0 either
            // way. With nobody to oppose, the opposing composite is empty and the stabilizer
            // floors it at 1.0, so 7.0 against 1.0 is 2.81 bands and 8 whole DC off the base of
            // 11. Against the guard's Will(20 x 0.2 = 4.0) it is 0.81 bands and 2 DC.
            //
            // An opposed check with nobody to oppose still resolves off the actor's own side; it
            // does not fall back to the base difficulty, refuse to resolve, or divide by nothing.
            Assert.Equal(3, unopposed.FinalDifficulty);
            Assert.Equal(9, opposed.FinalDifficulty);
            Assert.DoesNotContain("Will", unopposed.Explain());

            Assert.Equal(0.0, unopposed.Opposition.TargetPower);
            Assert.Equal(1.0, unopposed.Opposition.StabilizedTargetPower);
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

        // --- BQa-004: progression-safe opposed power-band scaling ---------------------------

        /// <summary>
        /// One opposed contest at a named actor/target power, through the production resolver.
        /// Strength and Will are single-weight terms on the intimidation-shaped profile below, so
        /// a composite is the number put in rather than something the fixture had to solve for.
        /// </summary>
        private static CheckResult Contest(int actorPower, int targetPower, params (string Label, int Delta)[] modifiers)
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            vanilla.SetAttribute(Player, VanillaAttribute.Strength, actorPower);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, targetPower);

            CheckProfile profile = new CheckProfile("test_contest", CheckFamily.Opposed, 20)
                .WithActorAttribute(VanillaAttribute.Strength, 1.0)
                .WithTargetAttribute(VanillaAttribute.Will, 1.0);
            CheckRequest request = new CheckRequest(profile, Player, Guard);
            foreach ((string Label, int Delta) modifier in modifiers)
            {
                request.WithModifier(modifier.Label, modifier.Delta);
            }

            return new VanillaStyleCheckResolver(vanilla).Resolve(request, new DeterministicRng(11));
        }

        private static int Bands(int actorPower, int targetPower)
        {
            return Contest(actorPower, targetPower).Opposition.AppliedDcAdjustment;
        }

        [Fact]
        public void EachDoublingOfRelativePowerIsOneBandWorthThreeDc()
        {
            // Parity is no adjustment at all, whatever the absolute numbers are.
            Assert.Equal(0, Bands(8, 8));
            Assert.Equal(20, Contest(8, 8).FinalDifficulty);

            Assert.Equal(3, Bands(16, 8));   // 2:1
            Assert.Equal(6, Bands(32, 8));   // 4:1
            Assert.Equal(-3, Bands(8, 16));  // 1:2
            Assert.Equal(-6, Bands(8, 32));  // 1:4

            // And the DC moves the other way from the adjustment: advantage makes it easier.
            Assert.Equal(17, Contest(16, 8).FinalDifficulty);
            Assert.Equal(23, Contest(8, 16).FinalDifficulty);
        }

        [Fact]
        public void ScalingBothSidesEquallyIsTheSameContest()
        {
            // The point of a ratio over a difference: 200 against 100 is the advantage 2 against 1
            // is, where a flat sum would call the first a hundred points and the second one point.
            Assert.Equal(3, Bands(2, 1));
            Assert.Equal(3, Bands(20, 10));
            Assert.Equal(3, Bands(200, 100));
            Assert.Equal(3, Bands(20000, 10000));

            Assert.Equal(0, Bands(7, 7));
            Assert.Equal(0, Bands(7000, 7000));
        }

        [Fact]
        public void RelativePowerIsMonotonicInBothSides()
        {
            int previous = int.MinValue;
            for (int actor = 1; actor <= 400; actor++)
            {
                int adjustment = Bands(actor, 40);
                Assert.True(adjustment >= previous, "growing actor power never made the contest harder");
                previous = adjustment;
            }

            previous = int.MaxValue;
            for (int target = 1; target <= 400; target++)
            {
                int adjustment = Bands(40, target);
                Assert.True(adjustment <= previous, "growing opposition never made the contest easier");
                previous = adjustment;
            }
        }

        [Fact]
        public void TheWholeNumberRuleRoundsTowardZeroInBothDirections()
        {
            // 3:1 is 4.75 raw DC and 1:3 is -4.75. Toward zero pays neither side for the part of
            // the band it did not finish; a mathematical floor would quietly charge the loser 5.
            Assert.Equal(4, Bands(24, 8));
            Assert.Equal(-4, Bands(8, 24));

            Assert.Equal(4.754887, Contest(24, 8).Opposition.RawDcAdjustment, 5);
            Assert.Equal(-4.754887, Contest(8, 24).Opposition.RawDcAdjustment, 5);

            // The rule, stated as the property it exists for: swapping the two sides negates the
            // adjustment exactly, at every ratio and on both sides of zero.
            for (int actor = 1; actor <= 60; actor++)
            {
                for (int target = 1; target <= 60; target++)
                {
                    Assert.Equal(-Bands(actor, target), Bands(target, actor));
                }
            }
        }

        [Fact]
        public void ExactBoundariesLandOnTheBandRatherThanAUnitBelowIt()
        {
            // Every exact power of two is a whole number of raw DC. Truncating a representation
            // error would hand back one DC less than the boundary mathematically earns.
            for (int doublings = 0; doublings <= 20; doublings++)
            {
                int actor = 1 << doublings;
                Assert.Equal(3 * doublings, Bands(actor, 1));
                Assert.Equal(-3 * doublings, Bands(1, actor));
            }
        }

        [Fact]
        public void ZeroAndNearZeroSidesUseTheOneStabilizerRatherThanAnInfinity()
        {
            // Nothing against nothing is a contest between equals, not an undefined ratio.
            CheckResult empty = Contest(0, 0);
            Assert.Equal(0, empty.Opposition.AppliedDcAdjustment);
            Assert.Equal(empty.BaseDifficulty, empty.FinalDifficulty);
            Assert.Equal(1.0, empty.Opposition.StabilizedActorPower);
            Assert.Equal(1.0, empty.Opposition.StabilizedTargetPower);

            // The floor is the same value on both sides, so a missing stat is the weakest real
            // opposition either way round and never an infinity in one direction only.
            Assert.Equal(-Bands(0, 16), Bands(16, 0));
            Assert.Equal(Bands(1, 16), Bands(0, 16));

            // A negative composite is invalid rather than a reversal: it floors like a zero.
            Assert.Equal(Bands(0, 16), Bands(-40, 16));
            Assert.Equal(1.0, Contest(-40, 16).Opposition.StabilizedActorPower);
            Assert.Equal(-40.0, Contest(-40, 16).Opposition.ActorPower);
        }

        [Fact]
        public void TheTraceExplainsTheRatioIncludingWhereTheStabilizerLiftedASide()
        {
            // The whole arithmetic has to be reproducible from the line: both composites, every
            // stat that fed them, and the band count they produced.
            string contested = Contest(16, 8).Explain();
            Assert.Contains("power 16 (Strength 16) vs 8 (target Will 8) = +1 bands", contested);
            Assert.Contains("-3 (opposed power)", contested);
            Assert.DoesNotContain("floored", contested);

            // And where the floor did the work, it says so rather than leaving "16 vs 0 = +4
            // bands" sitting there as arithmetic the reader cannot reproduce.
            string unopposed = Contest(16, 0).Explain();
            Assert.Contains("vs 0 floored to 1", unopposed);
            Assert.Contains("= +4 bands", unopposed);
            Assert.Contains("-12 (opposed power)", unopposed);
        }

        [Fact]
        public void ExtremeMasteryTrivializesWeakOppositionWithoutOverflowing()
        {
            // Not capped by default: enough relative power really should settle a weak contest.
            Assert.True(Contest(100000, 4).FinalDifficulty < 0);

            // But the die is still rolled and the profile's fumble window survives it, so mastery
            // lowers the DC without abolishing the failure the profile explicitly kept.
            CheckProfile profile = new CheckProfile("test_mastery", CheckFamily.Opposed, 20)
                .WithActorAttribute(VanillaAttribute.Strength, 1.0)
                .WithTargetAttribute(VanillaAttribute.Will, 1.0);
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            vanilla.SetAttribute(Player, VanillaAttribute.Strength, 100000);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, 4);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);
            CheckRequest request = new CheckRequest(profile, Player, Guard);

            Assert.Equal(CheckOutcome.CriticalFail, resolver.Resolve(request, RngThatRolls(20, 1)).Outcome);
            Assert.Equal(CheckOutcome.Pass, resolver.Resolve(request, RngThatRolls(20, 2)).Outcome);

            // A level far past anything the game reaches still produces an ordinary integer DC.
            CheckResult vast = Contest(int.MaxValue, 1);
            Assert.True(vast.Opposition.AppliedDcAdjustment > 0);
            Assert.True(vast.FinalDifficulty < vast.BaseDifficulty);
        }

        [Fact]
        public void SituationalModifiersStayOutsideTheRatioRatherThanScalingWithIt()
        {
            // A modifier is a fact about the occasion, so it is worth the same 4 DC in a contest
            // between equals and in a hopelessly lopsided one. Inside the ratio it would not be.
            Assert.Equal(
                Contest(8, 8).FinalDifficulty + 4,
                Contest(8, 8, ("they have proof", 4)).FinalDifficulty);
            Assert.Equal(
                Contest(512, 4).FinalDifficulty + 4,
                Contest(512, 4, ("they have proof", 4)).FinalDifficulty);

            // And it never reaches either composite.
            Assert.Equal(8.0, Contest(8, 8, ("they have proof", 4)).Opposition.ActorPower);
            Assert.Equal(8.0, Contest(8, 8, ("they have proof", 4)).Opposition.TargetPower);
        }

        [Fact]
        public void TargetLevelEntersTheOppositionOnlyWhereTheProfileDeclaredIt()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 40);
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 30);
            vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 20);
            vanilla.SetAttribute(Player, VanillaAttribute.Strength, 20);
            vanilla.SetAttribute(Player, VanillaAttribute.Will, 20);
            vanilla.SetAttribute(Guard, VanillaAttribute.Will, 20);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            // Intimidation declared a level term; interrogation did not. Both are opposed, both
            // read the same level-40 guard, and only the one that said so pays for it.
            CheckResult declared = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Intimidation, Player, Guard), new DeterministicRng(5));
            CheckResult silent = resolver.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Interrogation, Player, Guard), new DeterministicRng(5));

            Assert.Equal(40 * 0.3, Assert.Single(declared.Opposition.Contributions, c => c.Label == "target level").Value);
            Assert.DoesNotContain(silent.Opposition.Contributions, c => c.Label == "target level");

            // Levelling the guard moves the profile that declared the term and nothing else. No
            // hidden universal level scaling arrives through the power composite.
            vanilla.Define(Guard, level: 80);
            Assert.True(
                resolver.Resolve(new CheckRequest(ProceduralCheckProfiles.Intimidation, Player, Guard), new DeterministicRng(5))
                    .FinalDifficulty > declared.FinalDifficulty);
            Assert.Equal(
                silent.FinalDifficulty,
                resolver.Resolve(new CheckRequest(ProceduralCheckProfiles.Interrogation, Player, Guard), new DeterministicRng(5))
                    .FinalDifficulty);
        }

        [Fact]
        public void AFixedChallengeKeepsTheFlatSumAndNoPowerRatio()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.SetSkill(Player, VanillaSkill.Lockpicking, 10);
            vanilla.SetSkill(Player, VanillaSkill.Stealth, 12);
            vanilla.SetAttribute(Player, VanillaAttribute.Dexterity, 15);

            CheckResult burglary = new VanillaStyleCheckResolver(vanilla).Resolve(
                new CheckRequest(ProceduralCheckProfiles.Burglary, Player, EntityId.None), new DeterministicRng(1));

            // The same flat arithmetic BQa-004 found: base 13 - lockpicking(4) - stealth(3)
            // - dexterity(3). A lock does not get easier because the actor is relatively strong.
            Assert.Equal(3, burglary.FinalDifficulty);
            Assert.Null(burglary.Opposition);
            Assert.Contains(burglary.Terms, t => t.Label == "Lockpicking");
            Assert.DoesNotContain(burglary.Terms, t => t.Label == "opposed power");
        }

        [Fact]
        public void EveryOpposedProfileScalesByPowerAndEveryAbsoluteOneDoesNot()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 10);
            VanillaStyleCheckResolver resolver = new VanillaStyleCheckResolver(vanilla);

            foreach (string id in ProceduralCheckProfiles.ProfileIds)
            {
                CheckProfile profile = ProceduralCheckProfiles.ById(id);
                CheckResult result = resolver.Resolve(
                    new CheckRequest(profile, Player, Guard), new DeterministicRng(6));

                if (profile.Family == CheckFamily.Opposed)
                {
                    Assert.NotNull(result.Opposition);
                }
                else
                {
                    Assert.Null(result.Opposition);
                    Assert.DoesNotContain(result.Terms, t => t.Label == "opposed power");
                }
            }
        }

        [Fact]
        public void OpposedScalingDepartsFromTheFlatSourceCheckSumOnPurpose()
        {
            // The recorded baseline this replaces: the same single-element opposed row that used
            // to match vanilla's flat sum exactly. It no longer does, and the direction is the
            // point rather than an accident.
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, level: 5);
            vanilla.Define(Guard, level: 12);
            vanilla.SetSkill(Player, VanillaSkill.Negotiation, 18);
            vanilla.SetAttribute(Guard, VanillaAttribute.Perception, 9);

            CheckProfile profile = new CheckProfile("test_single_element_opposed", CheckFamily.Opposed, 14)
                .WithActorSkill(VanillaSkill.Negotiation, 0.5)
                .WithTargetAttribute(VanillaAttribute.Perception, 0.5)
                .WithTargetLevel(0.25)
                .WithDice(20, critRange: 1, fumbleRange: 1);
            CheckRequest request = new CheckRequest(profile, Player, Guard).WithModifier("hard rain", 2);

            CheckResult result = new VanillaStyleCheckResolver(vanilla).Resolve(request, new DeterministicRng(0));
            int flatSourceCheckSum = 14 + 3 + 5 - 9 + 2;

            // actor 9.0 against target 7.5 is 0.26 bands: near parity, and 0.79 raw DC is not yet
            // a band anyone finished earning. The flat sum read the same contest as a whole point
            // of advantage because it was counting a gap of 1.5 rather than a ratio of 1.2.
            Assert.Equal(15, flatSourceCheckSum);
            Assert.Equal(16, result.FinalDifficulty);
            Assert.Equal(0, result.Opposition.AppliedDcAdjustment);
            Assert.Equal(0.789, result.Opposition.RawDcAdjustment, 3);
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
