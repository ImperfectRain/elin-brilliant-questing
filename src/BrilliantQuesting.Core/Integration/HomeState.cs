using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// Elin's six Home Skill elements, as the mod reads them.
    ///
    /// These are the numbers a settlement is actually judged by in vanilla - the same values the
    /// player watches on the Home board - and the procedural layer reads them rather than
    /// inventing a private "settlement quality" score beside them.
    /// </summary>
    public enum HomeMetric
    {
        /// <summary>Public Safety (<c>fSafety</c>).</summary>
        Safety,

        /// <summary>Public Morality (<c>fMoral</c>).</summary>
        Morality,

        /// <summary>Food Supply (<c>fFood</c>).</summary>
        Food,

        /// <summary>Soil (<c>fSoil</c>).</summary>
        Soil,

        /// <summary>Publicity (<c>fPromo</c>).</summary>
        Publicity,

        /// <summary>Administration (<c>fAdmin</c>).</summary>
        Administration
    }

    /// <summary>
    /// One person who lives at the player's Home, as the game lists them.
    ///
    /// Residency is not presence: somebody away on an errand is still a resident, and a stranger
    /// standing in the hall is not one. That distinction is why this list is read from the Home
    /// itself rather than derived from who happens to be in the zone.
    /// </summary>
    public sealed class HomeResident
    {
        public HomeResident(EntityId id, string name, string job = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            Job = job ?? string.Empty;
        }

        public EntityId Id { get; }

        public string Name { get; }

        /// <summary>
        /// What this resident does at Home, in the game's own words. Empty when this build does
        /// not say - an unread job is never reported as unemployment.
        /// </summary>
        public string Job { get; }

        public bool HasJob => Job.Length > 0;

        public override string ToString()
        {
            return Name + (HasJob ? " (" + Job + ")" : string.Empty);
        }
    }

    /// <summary>
    /// A read-only snapshot of the player's Home: who lives there, what they do, how many more it
    /// can hold, and the six Home Skill elements.
    ///
    /// Two rules hold the whole type together, and both exist because the Home verbs refuse or
    /// allow a shelter offer on these numbers.
    ///
    /// A datum this build could not read is *absent*, not zero. <see cref="TryGetMetric"/> says so
    /// out loud, and <see cref="CapacityKnown"/> does the same for room. A Home whose capacity read
    /// as a silent zero would look permanently full, and one whose safety read as a silent zero
    /// would look like a slum; both are lies the log could not explain.
    ///
    /// And the absence of the whole snapshot is its own answer: <see cref="IVanillaState.GetHomeState"/>
    /// returns null for a player with no Home at all, never an empty one.
    /// </summary>
    public sealed class HomeState
    {
        private readonly List<HomeResident> _residents;
        private readonly Dictionary<HomeMetric, int> _metrics;

        internal HomeState(
            EntityId zoneId,
            string name,
            List<HomeResident> residents,
            int capacity,
            bool capacityKnown,
            Dictionary<HomeMetric, int> metrics)
        {
            ZoneId = zoneId;
            Name = name ?? string.Empty;
            _residents = residents ?? new List<HomeResident>();
            Capacity = capacityKnown ? Math.Max(0, capacity) : 0;
            CapacityKnown = capacityKnown;
            _metrics = metrics ?? new Dictionary<HomeMetric, int>();
        }

        /// <summary>The zone the Home occupies, or <see cref="EntityId.None"/> when unread.</summary>
        public EntityId ZoneId { get; }

        public string Name { get; }

        public IReadOnlyList<HomeResident> Residents => _residents;

        public int ResidentCount => _residents.Count;

        /// <summary>How many people this Home may hold. Meaningless unless <see cref="CapacityKnown"/>.</summary>
        public int Capacity { get; }

        public bool CapacityKnown { get; }

        /// <summary>
        /// Room for one more, and zero when the build will not say how much room there is.
        ///
        /// Zero is the refusing direction on purpose: an offer that needs a bed should decline
        /// rather than promise one the settlement may not have.
        /// </summary>
        public int FreeCapacity => CapacityKnown ? Math.Max(0, Capacity - ResidentCount) : 0;

        public bool KnowsMetric(HomeMetric metric) => _metrics.ContainsKey(metric);

        public bool TryGetMetric(HomeMetric metric, out int value) => _metrics.TryGetValue(metric, out value);

        /// <summary>The value, or zero when unread. Callers with a threshold should use <see cref="TryGetMetric"/>.</summary>
        public int GetMetric(HomeMetric metric)
        {
            _metrics.TryGetValue(metric, out int value);
            return value;
        }

        public bool IsResident(EntityId chara)
        {
            for (int i = 0; i < _residents.Count; i++)
            {
                if (_residents[i].Id == chara)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The same Home with one more person living in it.
        ///
        /// A snapshot transform, not a write: what is known stays known and what was never read
        /// stays absent, so admitting somebody cannot quietly invent a capacity or a metric the
        /// game never answered. The live adapter has no use for it - it tells Elin and reads the
        /// settlement again - and the headless reference implementation moves somebody in with it.
        /// </summary>
        internal HomeState WithResident(HomeResident resident)
        {
            if (resident == null || resident.Id.IsNone || IsResident(resident.Id))
            {
                return this;
            }

            List<HomeResident> residents = new List<HomeResident>(_residents) { resident };
            return new HomeState(ZoneId, Name, residents, Capacity, CapacityKnown, new Dictionary<HomeMetric, int>(_metrics));
        }

        /// <summary>
        /// One line, written so that a live log distinguishes "read as zero" from "not read".
        /// This is the line the adapter prints on attach, and formatting it here rather than in
        /// the plugin is what lets the honesty of that line be tested with no game attached.
        /// </summary>
        public string Describe()
        {
            List<string> parts = new List<string>
            {
                (Name.Length > 0 ? "'" + Name + "'" : "(unnamed)")
                + (ZoneId.IsNone ? string.Empty : " [" + ZoneId + "]"),
                ResidentCount + " resident(s) of " + (CapacityKnown ? Capacity.ToString() : "?")
            };

            foreach (HomeMetric metric in (HomeMetric[])Enum.GetValues(typeof(HomeMetric)))
            {
                parts.Add(metric.ToString().ToLowerInvariant() + " "
                          + (TryGetMetric(metric, out int value) ? value.ToString() : "?"));
            }

            return string.Join(", ", parts.ToArray());
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// The one way a <see cref="HomeState"/> is put together.
    ///
    /// The live adapter and the headless reference implementation build their snapshots through
    /// this, so "unread" means the same thing on both sides of the seam: a datum nobody set is
    /// absent, and never a default that reads like a measurement.
    /// </summary>
    public sealed class HomeStateBuilder
    {
        private readonly List<HomeResident> _residents = new List<HomeResident>();
        private readonly Dictionary<HomeMetric, int> _metrics = new Dictionary<HomeMetric, int>();
        private readonly EntityId _zoneId;
        private readonly string _name;
        private int _capacity;
        private bool _capacityKnown;

        public HomeStateBuilder(EntityId zoneId, string name)
        {
            _zoneId = zoneId;
            _name = name ?? string.Empty;
        }

        public HomeStateBuilder WithCapacity(int capacity)
        {
            _capacity = capacity;
            _capacityKnown = true;
            return this;
        }

        /// <summary>Adds a resident. An unidentified or already-listed person is ignored.</summary>
        public HomeStateBuilder AddResident(EntityId chara, string name, string job = null)
        {
            if (chara.IsNone)
            {
                return this;
            }

            for (int i = 0; i < _residents.Count; i++)
            {
                if (_residents[i].Id == chara)
                {
                    return this;
                }
            }

            _residents.Add(new HomeResident(chara, name, job));
            return this;
        }

        public HomeStateBuilder WithMetric(HomeMetric metric, int value)
        {
            _metrics[metric] = value;
            return this;
        }

        public HomeState Build()
        {
            return new HomeState(
                _zoneId,
                _name,
                new List<HomeResident>(_residents),
                _capacity,
                _capacityKnown,
                new Dictionary<HomeMetric, int>(_metrics));
        }
    }
}
