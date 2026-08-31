using BrilliantQuesting.Integration;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class DynamicTabMemoryPolicyTests
    {
        [Fact]
        public void PreInitKeyUsesIncomingLayerRatherThanUnassignedWindowLayer()
        {
            Assert.Equal("journal1", DynamicTabMemoryPolicy.WindowKey("journal", 1));
            Assert.NotEqual(DynamicTabMemoryPolicy.WindowKey(null, 1), DynamicTabMemoryPolicy.WindowKey("journal", 1));
        }

        [Fact]
        public void OutOfRangeRememberedTabIsResetBeforeVanillaIndexesIt()
        {
            Assert.True(DynamicTabMemoryPolicy.ShouldResetRememberedTab(10, 10, rememberedTabIsDynamic: false));
            Assert.True(DynamicTabMemoryPolicy.ShouldResetRememberedTab(-1, 10, rememberedTabIsDynamic: false));
        }

        [Fact]
        public void VanillaRememberedTabSurvivesAcrossJournalOpens()
        {
            Assert.False(DynamicTabMemoryPolicy.ShouldResetRememberedTab(3, 10, rememberedTabIsDynamic: false));
        }

        [Fact]
        public void RuntimeAppendedTabIsNotPersistedAsVanillaMemory()
        {
            Assert.True(DynamicTabMemoryPolicy.ShouldResetRememberedTab(10, 11, rememberedTabIsDynamic: true));
        }
    }
}
