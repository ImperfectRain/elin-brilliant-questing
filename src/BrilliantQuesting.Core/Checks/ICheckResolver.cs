using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// Rolls a check. BQ procedural checks are replay-authoritative, so their outcomes come from
    /// a deterministic resolver. The plugin may use vanilla Check rows for presentation text, but
    /// must not choose Elin Check.Perform for composite BQ resolution merely because it exists.
    /// </summary>
    public interface ICheckResolver
    {
        CheckResult Resolve(CheckRequest request, DeterministicRng rng);
    }
}
