using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// One indivisible opening that has already been spent, and what spent it (BQa-016).
    ///
    /// Not a reservation. A reservation is a claim on something nobody has taken yet, and BQa-015
    /// is explicit that those never reach a save; this is the opposite record - the thing is gone,
    /// and the marker exists so a later pass does not offer it again. <see cref="Because"/>
    /// separates the two ways an opening closes, because they are not the same evidence: a
    /// committed attempt is this simulation's own doing, while an observed native outcome is
    /// something Elin did that BQ only wrote down.
    /// </summary>
    public sealed class ConsumedOpening
    {
        /// <summary>Spent by an attempt this simulation ran and committed.</summary>
        public const string Committed = "committed";

        /// <summary>Spent by something the game did, supplied to the cycle as already observed.</summary>
        public const string Observed = "observed";

        public ConsumedOpening(string contestKey, EntityId holder, GameTime when, string because)
        {
            ContestKey = contestKey ?? string.Empty;
            Holder = holder;
            When = when;
            Because = string.IsNullOrEmpty(because) ? Committed : because;
        }

        /// <summary>The <see cref="Actions.ActionContest.Key"/> that is now closed.</summary>
        public string ContestKey { get; }

        /// <summary>Who spent it, or <see cref="EntityId.None"/> where the game named nobody.</summary>
        public EntityId Holder { get; }

        public GameTime When { get; }

        /// <summary><see cref="Committed"/> or <see cref="Observed"/>.</summary>
        public string Because { get; }

        public override string ToString()
        {
            return ContestKey + " spent by " + (Holder.IsNone ? "nobody named" : Holder.Value)
                + " (" + Because + ", " + When + ")";
        }
    }

    /// <summary>
    /// What the production cycle has already done, kept across a save (BQa-016).
    ///
    /// Two markers and nothing else, because two are what the cycle cannot re-derive.
    ///
    /// <b>The interval it last consumed.</b> A cycle is bounded to one day, and a host that fires
    /// the same day twice - a reload onto the same morning, a hook that runs again after a zone
    /// change - must not get a second pass out of it. <see cref="HasConsumed"/> is what makes
    /// replaying an already-consumed interval harmless rather than merely unlikely.
    ///
    /// <b>The indivisible openings it has spent.</b> Whose turn it is can be recomputed from
    /// <see cref="NarrativeNpc.LastSimulatedAt"/>, which the save already carries, so no cursor is
    /// stored for it. What cannot be recomputed is that one particular purse was already lifted by
    /// an attempt whose own record says only that somebody lifted a purse - and an opening offered
    /// a second time is the one failure BQa-015's transient claims cannot prevent, because they
    /// expire with their batch. Markers are what one pass may not offer another, never permission
    /// to act: nothing here says anybody wants anything.
    ///
    /// Everything else about a pass - the work set, the readings, the rankings, the claims - stays
    /// derived and transient, and a pass that spent nothing leaves this byte-identical.
    /// </summary>
    public sealed class ProductionCycleLedger
    {
        /// <summary>No cycle has run in this world yet. Not day zero: day zero is a real day.</summary>
        public const long NeverRun = long.MinValue;

        private readonly List<ConsumedOpening> _openings = new List<ConsumedOpening>();
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The last interval a pass ran for, or <see cref="NeverRun"/>.</summary>
        public long LastConsumedDay { get; set; } = NeverRun;

        /// <summary>
        /// How many spent openings are remembered.
        ///
        /// A cap rather than a growing list, because the save has to stay bounded in a world that
        /// has been played for a year. Eviction is oldest-first and is a real cost: an opening
        /// forgotten this way could be offered again. It is set high enough that reaching it means
        /// far more indivisible changes than a town produces, and the alternative - a save that
        /// grows without limit - is worse than a stale purse.
        /// </summary>
        public int MostRemembered { get; set; } = 512;

        /// <summary>Every spent opening, oldest first.</summary>
        public IReadOnlyList<ConsumedOpening> Openings => _openings;

        /// <summary>Whether a pass has already been consumed for <paramref name="day"/> or later.</summary>
        public bool HasConsumed(long day)
        {
            return LastConsumedDay != NeverRun && day <= LastConsumedDay;
        }

        /// <summary>Whether this contest has already been closed by somebody.</summary>
        public bool IsSpent(string contestKey)
        {
            return !string.IsNullOrEmpty(contestKey) && _keys.Contains(contestKey);
        }

        /// <summary>
        /// Marks an opening spent. Idempotent: the first record of a closure is the true one, and
        /// a second caller reporting the same closure must not rewrite who did it.
        /// </summary>
        public ConsumedOpening Spend(string contestKey, EntityId holder, GameTime when, string because)
        {
            if (string.IsNullOrEmpty(contestKey))
            {
                return null;
            }

            if (_keys.Contains(contestKey))
            {
                return null;
            }

            ConsumedOpening opening = new ConsumedOpening(contestKey, holder, when, because);
            Add(opening);
            return opening;
        }

        /// <summary>Puts a saved marker back, for the serializer. Order is the saved order.</summary>
        public void Restore(ConsumedOpening opening)
        {
            if (opening == null || string.IsNullOrEmpty(opening.ContestKey) || _keys.Contains(opening.ContestKey))
            {
                return;
            }

            Add(opening);
        }

        private void Add(ConsumedOpening opening)
        {
            _openings.Add(opening);
            _keys.Add(opening.ContestKey);

            while (_openings.Count > Math.Max(1, MostRemembered))
            {
                _keys.Remove(_openings[0].ContestKey);
                _openings.RemoveAt(0);
            }
        }

        public override string ToString()
        {
            return "last consumed day "
                + (LastConsumedDay == NeverRun ? "none" : LastConsumedDay.ToString())
                + "; spent openings " + _openings.Count;
        }
    }
}
