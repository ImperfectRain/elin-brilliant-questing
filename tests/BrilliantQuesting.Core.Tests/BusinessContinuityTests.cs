using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-051. A business continuity problem is durable narrative state; an ordinary unavailable
    /// operator is just the live service surface Elin currently presents.
    /// </summary>
    public class BusinessContinuityTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Operator = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Replacement = EntityId.Parse("npc_replacement");
        private static readonly EntityId Shop = EntityId.Parse("business_shop");
        private static readonly EntityId Market = EntityId.Parse("zone_market");
        private static readonly EntityId Debt = EntityId.Parse("fact_debt");

        [Fact]
        public void AShopThePlayerLetsFailIsStillFailedAMonthLater()
        {
            NarrativeWorldState world = World();
            BusinessContinuity businesses = new BusinessContinuity(world);
            Assert.True(businesses.TryRegister(Shop, Market, Operator, GameTime.Zero));

            Assert.True(businesses.TryChangeState(
                Shop,
                BusinessContinuityState.Failed,
                GameTime.FromDays(1),
                Debt,
                Player));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            BusinessProjection projection = new BusinessContinuity(reloaded).Project(
                Shop,
                new BusinessServiceSnapshot(OperatorAvailability.Available, true),
                GameTime.FromDays(32));

            Assert.Equal(BusinessContinuityState.Failed, reloaded.Businesses.Of(Shop).State);
            Assert.Equal(ServiceContinuitySurface.Failed, projection.Surface);
            Assert.True(projection.VisibleConsequence);
            Assert.True(reloaded.Businesses.Of(Shop).HasFailedForAtLeast(GameTime.FromDays(32), 30));
        }

        [Theory]
        [InlineData(OperatorAvailability.Sleeping)]
        [InlineData(OperatorAvailability.AtHobby)]
        [InlineData(OperatorAvailability.OffShift)]
        public void OrdinaryOperatorUnavailabilityDoesNotBecomeABusinessState(OperatorAvailability availability)
        {
            NarrativeWorldState world = World();
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Market, Operator, GameTime.Zero);

            BusinessProjection projection = businesses.Project(
                Shop,
                new BusinessServiceSnapshot(availability, true),
                GameTime.FromDays(4));

            Assert.Equal(BusinessContinuityState.Normal, world.Businesses.Of(Shop).State);
            Assert.Equal(BusinessContinuityState.Normal, projection.State);
            Assert.Equal(ServiceContinuitySurface.TemporarilyUnavailable, projection.Surface);
            Assert.False(projection.VisibleConsequence);
            Assert.DoesNotContain(world.Ledger.Events, e => e.Type == WorldEventType.BusinessStateChanged);
        }

        [Fact]
        public void EmptyStockProjectsShortOnStockWithoutInventingSavedStock()
        {
            NarrativeWorldState world = World();
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Market, Operator, GameTime.Zero);

            BusinessProjection projection = businesses.Project(
                Shop,
                new BusinessServiceSnapshot(OperatorAvailability.Available, false),
                GameTime.FromDays(2));

            Assert.Equal(BusinessContinuityState.Normal, world.Businesses.Of(Shop).State);
            Assert.Equal(BusinessContinuityState.ShortOnStock, projection.State);
            Assert.Equal(ServiceContinuitySurface.Interrupted, projection.Surface);
            Assert.True(projection.VisibleConsequence);
        }

        [Fact]
        public void ChangedOperatorsAreDurableConsequences()
        {
            NarrativeWorldState world = World();
            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(Shop, Market, Operator, GameTime.Zero);

            Assert.True(businesses.TryChangeState(
                Shop,
                BusinessContinuityState.ReplacementOperator,
                GameTime.FromDays(8),
                replacementOperatorId: Replacement));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            BusinessRecord record = reloaded.Businesses.Of(Shop);

            Assert.Equal(BusinessContinuityState.ReplacementOperator, record.State);
            Assert.Equal(Replacement, record.ReplacementOperatorId);
            Assert.Equal(WorldEventType.BusinessStateChanged, Assert.Single(reloaded.Ledger.Events).Type);
        }

        [Fact]
        public void OlderSavesWithoutBusinessesStillLoad()
        {
            JsonValue old = JsonValue.Object()
                .Set("schemaVersion", NarrativeWorldState.CurrentSchemaVersion)
                .Set("worldSeed", "42")
                .Set("rngState", "42");

            NarrativeWorldState reloaded = WorldStateSerializer.FromJson(old);

            Assert.Equal(0, reloaded.Businesses.Count);
        }

        private static NarrativeWorldState World()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            world.Registry.Add(new NarrativeSite(Market, "Kell's Ford market", "market"));
            world.Registry.Add(new NarrativeNpc(Operator, "Mira") { Occupation = "shopkeeper" });
            world.Registry.Add(new NarrativeNpc(Replacement, "Haron") { Occupation = "merchant" });
            return world;
        }
    }
}
