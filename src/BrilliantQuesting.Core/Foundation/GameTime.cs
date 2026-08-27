using System;

namespace BrilliantQuesting.Foundation
{
    /// <summary>
    /// Simulation clock, stored as whole in-game minutes since world start.
    ///
    /// The mod does not own the calendar - Elin does. This is the value an adapter fills in from
    /// the running game, and the value headless tests advance by hand. Threads escalate against
    /// day boundaries (see the hostage escalation table in the design document), so days are the
    /// unit that matters in practice.
    /// </summary>
    public readonly struct GameTime : IEquatable<GameTime>, IComparable<GameTime>
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        public static readonly GameTime Zero = new GameTime(0);

        public GameTime(long totalMinutes)
        {
            TotalMinutes = totalMinutes;
        }

        public long TotalMinutes { get; }

        public long TotalDays => TotalMinutes / MinutesPerDay;

        public int Hour => (int)(TotalMinutes / MinutesPerHour % HoursPerDay);

        public int Minute => (int)(TotalMinutes % MinutesPerHour);

        public static GameTime FromDays(long days) => new GameTime(days * MinutesPerDay);

        public static GameTime FromHours(long hours) => new GameTime(hours * MinutesPerHour);

        public GameTime PlusMinutes(long minutes) => new GameTime(TotalMinutes + minutes);

        public GameTime PlusHours(long hours) => PlusMinutes(hours * MinutesPerHour);

        public GameTime PlusDays(long days) => PlusMinutes(days * MinutesPerDay);

        public long DaysSince(GameTime earlier) => (TotalMinutes - earlier.TotalMinutes) / MinutesPerDay;

        public bool Equals(GameTime other) => TotalMinutes == other.TotalMinutes;

        public override bool Equals(object obj) => obj is GameTime other && Equals(other);

        public override int GetHashCode() => TotalMinutes.GetHashCode();

        public int CompareTo(GameTime other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public override string ToString() => "day " + TotalDays + " " + Hour.ToString("00") + ":" + Minute.ToString("00");

        public static bool operator <(GameTime a, GameTime b) => a.TotalMinutes < b.TotalMinutes;

        public static bool operator >(GameTime a, GameTime b) => a.TotalMinutes > b.TotalMinutes;

        public static bool operator <=(GameTime a, GameTime b) => a.TotalMinutes <= b.TotalMinutes;

        public static bool operator >=(GameTime a, GameTime b) => a.TotalMinutes >= b.TotalMinutes;

        public static bool operator ==(GameTime a, GameTime b) => a.Equals(b);

        public static bool operator !=(GameTime a, GameTime b) => !a.Equals(b);
    }
}
