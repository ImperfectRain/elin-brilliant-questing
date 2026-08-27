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
    }
}
