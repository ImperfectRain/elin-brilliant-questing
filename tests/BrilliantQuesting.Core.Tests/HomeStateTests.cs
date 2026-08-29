using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-030: the Home can be read, and what could not be read says so.
    ///
    /// These drive <see cref="SandboxVanillaState"/>, the reference implementation of the seam.
    /// The live adapter cannot be exercised without a running game, so the rules pinned here are
    /// the rules it is written to honour: absence is absence, a player with no Home is not a
    /// player with an empty one, and residency is not presence.
    /// </summary>
    public class HomeStateTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Farmhand = EntityId.Parse("npc_farmhand");
        private static readonly EntityId Cook = EntityId.Parse("npc_cook");
        private static readonly EntityId Visitor = EntityId.Parse("npc_visitor");
        private static readonly EntityId HomeZone = EntityId.Parse("zone_home");

        private static HomeState FullyRead()
        {
            return new HomeStateBuilder(HomeZone, "Little Garden")
                .WithCapacity(4)
                .AddResident(Farmhand, "Rina", "farmer")
                .AddResident(Cook, "Bem", "cook")
                .WithMetric(HomeMetric.Safety, 12)
                .WithMetric(HomeMetric.Morality, 7)
                .WithMetric(HomeMetric.Food, 30)
                .WithMetric(HomeMetric.Soil, 5)
                .WithMetric(HomeMetric.Publicity, 2)
                .WithMetric(HomeMetric.Administration, 9)
                .Build();
        }

        private static SandboxVanillaState WorldWithHome(HomeState home)
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);
            vanilla.Define(Player, zone: HomeZone);
            vanilla.SetHome(home);
            return vanilla;
        }

        // -- the positive path -----------------------------------------------------------------

        [Fact]
        public void AReadHomeReportsResidentsJobsCapacityAndEveryHomeSkill()
        {
            HomeState home = WorldWithHome(FullyRead()).GetHomeState();

            Assert.Equal(HomeZone, home.ZoneId);
            Assert.Equal("Little Garden", home.Name);
            Assert.Equal(2, home.ResidentCount);
            Assert.True(home.IsResident(Cook));
            Assert.Equal("cook", home.Residents[1].Job);
            Assert.True(home.CapacityKnown);
            Assert.Equal(4, home.Capacity);
            Assert.Equal(2, home.FreeCapacity);

            Assert.True(home.TryGetMetric(HomeMetric.Safety, out int safety));
            Assert.Equal(12, safety);
            Assert.Equal(30, home.GetMetric(HomeMetric.Food));
            Assert.Equal(9, home.GetMetric(HomeMetric.Administration));
        }

        [Fact]
        public void ResidencyIsNotPresence()
        {
            SandboxVanillaState vanilla = WorldWithHome(FullyRead());

            // The cook is away for the day; a visitor is standing in the hall.
            vanilla.SetZone(Farmhand, HomeZone);
            vanilla.SetZone(Cook, EntityId.Parse("zone_market"));
            vanilla.SetZone(Visitor, HomeZone);

            HomeState home = vanilla.GetHomeState();

            Assert.True(home.IsResident(Cook));
            Assert.False(home.IsResident(Visitor));
            Assert.Contains(Visitor, vanilla.GetCharactersInZone(HomeZone));
        }

        // -- absence is absence ----------------------------------------------------------------

        [Fact]
        public void APlayerWithNoHomeReportsNothingRatherThanAnEmptyHome()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(Player);

            Assert.Null(vanilla.GetHomeState());
        }

        [Fact]
        public void ABuildThatCannotReadHomeStateReportsNothing()
        {
            SandboxVanillaState vanilla = WorldWithHome(FullyRead());
            vanilla.SetCapability(VanillaCapability.ReadHomeState, false);

            Assert.Null(vanilla.GetHomeState());
        }

        [Fact]
        public void AnUnreadHomeSkillIsAbsentRatherThanZero()
        {
            HomeState home = new HomeStateBuilder(HomeZone, "Little Garden")
                .WithMetric(HomeMetric.Safety, 0)
                .Build();

            // Safety really is zero here, and morality was never read. Both answer GetMetric with
            // a nought, which is exactly why a threshold has to ask the other question.
            Assert.True(home.TryGetMetric(HomeMetric.Safety, out int safety));
            Assert.Equal(0, safety);
            Assert.False(home.KnowsMetric(HomeMetric.Morality));
            Assert.False(home.TryGetMetric(HomeMetric.Morality, out _));
            Assert.Equal(0, home.GetMetric(HomeMetric.Morality));
        }

        [Fact]
        public void AnUnreadCapacityOffersNoRoomRatherThanUnlimitedRoom()
        {
            HomeState home = new HomeStateBuilder(HomeZone, "Little Garden")
                .AddResident(Farmhand, "Rina")
                .Build();

            Assert.False(home.CapacityKnown);
            Assert.Equal(0, home.FreeCapacity);
        }

        [Fact]
        public void AFullHomeHasNoFreeCapacityAndAnOverfullOneNeverGoesNegative()
        {
            HomeState home = new HomeStateBuilder(HomeZone, "Little Garden")
                .WithCapacity(1)
                .AddResident(Farmhand, "Rina")
                .AddResident(Cook, "Bem")
                .Build();

            Assert.Equal(2, home.ResidentCount);
            Assert.Equal(0, home.FreeCapacity);
        }

        // -- the list itself -------------------------------------------------------------------

        [Fact]
        public void APersonListedTwiceIsOneResidentAndAnUnidentifiedOneIsNobody()
        {
            HomeState home = new HomeStateBuilder(HomeZone, "Little Garden")
                .AddResident(Farmhand, "Rina", "farmer")
                .AddResident(Farmhand, "Rina", "farmer")
                .AddResident(EntityId.None, "a shape in the doorway")
                .Build();

            Assert.Equal(1, home.ResidentCount);
        }

        [Fact]
        public void AResidentWithNoReadableJobSaysSoRatherThanReadingAsUnemployed()
        {
            HomeState home = new HomeStateBuilder(HomeZone, "Little Garden")
                .AddResident(Farmhand, "Rina")
                .Build();

            Assert.False(home.Residents[0].HasJob);
            Assert.Equal(string.Empty, home.Residents[0].Job);
        }

        // -- the line the adapter prints -------------------------------------------------------

        [Fact]
        public void TheDescriptionCarriesEveryHomeSkillAndTheZoneItIsIn()
        {
            string line = FullyRead().Describe();

            Assert.Contains("'Little Garden' [zone_home]", line);
            Assert.Contains("2 resident(s) of 4", line);
            Assert.Contains("safety 12", line);
            Assert.Contains("morality 7", line);
            Assert.Contains("food 30", line);
            Assert.Contains("soil 5", line);
            Assert.Contains("publicity 2", line);
            Assert.Contains("administration 9", line);
        }

        [Fact]
        public void TheDescriptionMarksWhatWasNotReadRatherThanPrintingAZero()
        {
            string line = new HomeStateBuilder(HomeZone, "Little Garden")
                .WithMetric(HomeMetric.Food, 0)
                .Build()
                .Describe();

            Assert.Contains("resident(s) of ?", line);
            Assert.Contains("food 0", line);
            Assert.Contains("safety ?", line);
        }
    }
}
