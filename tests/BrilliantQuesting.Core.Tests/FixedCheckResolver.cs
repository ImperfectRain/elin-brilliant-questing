using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// Forces a chosen outcome so a test can assert on consequences rather than on dice. Anything
    /// that needs to verify the arithmetic itself uses the real resolver instead.
    /// </summary>
    public sealed class FixedCheckResolver : ICheckResolver
    {
        private readonly Queue<CheckOutcome> _scripted = new Queue<CheckOutcome>();

        public FixedCheckResolver(CheckOutcome standing)
        {
            Standing = standing;
        }

        public CheckOutcome Standing { get; set; }

        public FixedCheckResolver Then(CheckOutcome outcome)
        {
            _scripted.Enqueue(outcome);
            return this;
        }

        public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
        {
            CheckOutcome outcome = _scripted.Count > 0 ? _scripted.Dequeue() : Standing;
            int roll = outcome == CheckOutcome.CriticalPass ? 20 : outcome == CheckOutcome.CriticalFail ? 1 : outcome == CheckOutcome.Pass ? 15 : 5;
            return new CheckResult(request.Profile.Id, request.Profile.BaseDifficulty, new List<CheckTerm>(), 10, roll, outcome);
        }
    }
}
