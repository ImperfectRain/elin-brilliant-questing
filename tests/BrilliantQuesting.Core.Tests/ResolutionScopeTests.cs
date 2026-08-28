using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-007: one decision produces exactly one consequence, and it lands on the person the
    /// options were drawn for.
    /// </summary>
    public class ResolutionScopeTests
    {
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Witness = EntityId.Parse("npc_witness");

        [Fact]
        public void TheFirstClaimSucceeds()
        {
            ResolutionScope scope = new ResolutionScope(Thief);

            Assert.True(scope.TryClaim(Thief, "question", out string refusal));
            Assert.Null(refusal);
            Assert.True(scope.IsSpent);
            Assert.Equal("question", scope.SpentBy);
        }

        /// <summary>The double-click case: the same option arriving twice.</summary>
        [Fact]
        public void TheSameOptionArrivingTwiceResolvesOnce()
        {
            ResolutionScope scope = new ResolutionScope(Thief);

            Assert.True(scope.TryClaim(Thief, "question", out _));
            Assert.False(scope.TryClaim(Thief, "question", out string refusal));
            Assert.Contains("already resolved", refusal);
            Assert.Equal("question", scope.SpentBy);
        }

        /// <summary>The click-then-number-key case: two different options from one offering.</summary>
        [Fact]
        public void ASecondDifferentOptionFromTheSameOfferingIsRefused()
        {
            ResolutionScope scope = new ResolutionScope(Thief);

            Assert.True(scope.TryClaim(Thief, "question", out _));
            Assert.False(scope.TryClaim(Thief, "pickpocket", out string refusal));
            Assert.Contains("already resolved", refusal);
            Assert.Equal("question", scope.SpentBy);
        }

        /// <summary>The actor-swap case: the resolution names somebody the options were not drawn for.</summary>
        [Fact]
        public void AClaimAgainstADifferentPersonIsRefused()
        {
            ResolutionScope scope = new ResolutionScope(Thief);

            Assert.False(scope.TryClaim(Witness, "question", out string refusal));
            Assert.Contains("offered against", refusal);
            Assert.False(scope.IsSpent);
        }

        /// <summary>A refusal must not consume the opportunity the right person still has.</summary>
        [Fact]
        public void ARefusedClaimLeavesTheScopeSpendable()
        {
            ResolutionScope scope = new ResolutionScope(Thief);

            Assert.False(scope.TryClaim(Witness, "question", out _));
            Assert.True(scope.TryClaim(Thief, "question", out _));
        }

        [Fact]
        public void SeparateOfferingsAreIndependent()
        {
            ResolutionScope first = new ResolutionScope(Thief);
            ResolutionScope second = new ResolutionScope(Thief);

            Assert.True(first.TryClaim(Thief, "question", out _));
            Assert.True(second.TryClaim(Thief, "question", out _));
        }
    }
}
