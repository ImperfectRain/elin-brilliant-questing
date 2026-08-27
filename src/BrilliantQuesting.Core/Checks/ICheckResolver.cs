using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Rolls a check. Two implementations are expected: the vanilla-style one in this assembly
    /// (used headless, and as the fallback in game), and a thin adapter in the plugin that calls
    /// Elin's own Check.Perform once the runtime spike confirms it is safe to do so.
    /// </summary>
    public interface ICheckResolver
    {
        CheckResult Resolve(CheckRequest request, DeterministicRng rng);
    }
}
