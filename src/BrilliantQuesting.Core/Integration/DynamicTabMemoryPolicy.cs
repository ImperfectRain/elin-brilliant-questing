namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// Policy for positional tab memories when a modded tab is appended at runtime.
    ///
    /// Elin's Window.Init reads the remembered tab before Brilliant Questing can append its
    /// journal tab. A remembered appended index is therefore only valid inside the already-built
    /// window, never across window lifetimes.
    /// </summary>
    public static class DynamicTabMemoryPolicy
    {
        public static string WindowKey(string layerUid, int windowIndex)
        {
            return (string.IsNullOrEmpty(layerUid) ? "nolayer" : layerUid) + windowIndex;
        }

        public static bool ShouldResetRememberedTab(int rememberedIndex, int currentTabCount, bool rememberedTabIsDynamic)
        {
            return currentTabCount <= 0
                   || rememberedIndex < 0
                   || rememberedIndex >= currentTabCount
                   || rememberedTabIsDynamic;
        }
    }
}
