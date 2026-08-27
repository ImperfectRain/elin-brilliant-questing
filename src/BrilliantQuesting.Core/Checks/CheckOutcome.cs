namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// The four results vanilla Elin's Check class already produces. The procedural layer never
    /// reduces an interaction to pass/fail: CriticalPass should hand out something extra, and
    /// CriticalFail should create a new problem rather than simply refusing the player.
    /// </summary>
    public enum CheckOutcome
    {
        CriticalFail = 0,
        Fail = 1,
        Pass = 2,
        CriticalPass = 3
    }

    public static class CheckOutcomeExtensions
    {
        public static bool IsSuccess(this CheckOutcome outcome)
        {
            return outcome == CheckOutcome.Pass || outcome == CheckOutcome.CriticalPass;
        }

        public static bool IsCritical(this CheckOutcome outcome)
        {
            return outcome == CheckOutcome.CriticalPass || outcome == CheckOutcome.CriticalFail;
        }
    }
}
