using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>Canonical coarse pressure buckets. These are narrative categories, not commodities.</summary>
    public static class LocalDemandCategory
    {
        public const string Food = "Food";
        public const string Alcohol = "Alcohol";
        public const string Medicine = "Medicine";
        public const string Lumber = "Lumber";
        public const string Textiles = "Textiles";
        public const string Weapons = "Weapons";
        public const string Luxury = "Luxury";
        public const string Labor = "Labor";
        public const string Safety = "Safety";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value.Trim();
            if (LooksLike(text, "food", "meal", "bread", "ration", "grain", "crop")) return Food;
            if (LooksLike(text, "alcohol", "drink", "ale", "wine", "cider", "beer")) return Alcohol;
            if (LooksLike(text, "medicine", "potion", "remedy", "salve", "tonic")) return Medicine;
            if (LooksLike(text, "lumber", "timber", "wood", "log", "plank")) return Lumber;
            if (LooksLike(text, "textile", "cloth", "clothing", "fabric", "fiber")) return Textiles;
            if (LooksLike(text, "weapon", "blade", "bow", "gun", "spear")) return Weapons;
            if (LooksLike(text, "luxury", "jewel", "art", "ornament", "spice")) return Luxury;
            if (LooksLike(text, "labor", "worker", "labour", "hands", "service")) return Labor;
            if (LooksLike(text, "safety", "guard", "security", "protection")) return Safety;

            return null;
        }

        private static bool LooksLike(string text, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A persistent local shortage pressure. It records how hard a place is pressed and when the
    /// pressure would naturally stop mattering if nobody intervenes.
    /// </summary>
    public sealed class LocalDemandPressure
    {
        public LocalDemandPressure(
            EntityId placeId,
            string category,
            int severity,
            GameTime beganAt,
            GameTime expectedReliefAt,
            EntityId sourceFactId)
        {
            PlaceId = placeId;
            Category = LocalDemandCategory.Normalize(category) ?? category;
            Severity = Clamp(severity, 0, 100);
            BeganAt = beganAt;
            ExpectedReliefAt = expectedReliefAt;
            SourceFactId = sourceFactId;
        }

        public EntityId PlaceId { get; }

        public string Category { get; }

        public int Severity { get; internal set; }

        public GameTime BeganAt { get; }

        public GameTime ExpectedReliefAt { get; internal set; }

        public EntityId SourceFactId { get; }

        public bool Active => Severity > 0;

        internal static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    /// <summary>
    /// Coarse pressure state for places. It answers "what is this settlement short of?" without
    /// pretending to simulate breakfast, shop stock, or individual commodity flow.
    /// </summary>
    public sealed class LocalDemandLedger
    {
        private readonly List<LocalDemandPressure> _pressures = new List<LocalDemandPressure>();

        public IReadOnlyList<LocalDemandPressure> Pressures => _pressures;

        public LocalDemandPressure AddOrUpdate(
            EntityId placeId,
            string category,
            int severity,
            GameTime beganAt,
            GameTime expectedReliefAt,
            EntityId sourceFactId)
        {
            string canonical = LocalDemandCategory.Normalize(category);
            if (placeId.IsNone || canonical == null)
            {
                return null;
            }

            LocalDemandPressure existing = Get(placeId, canonical, sourceFactId);
            if (existing == null)
            {
                existing = new LocalDemandPressure(placeId, canonical, severity, beganAt, expectedReliefAt, sourceFactId);
                _pressures.Add(existing);
                return existing;
            }

            existing.Severity = Math.Max(existing.Severity, LocalDemandPressure.Clamp(severity, 0, 100));
            if (expectedReliefAt > existing.ExpectedReliefAt)
            {
                existing.ExpectedReliefAt = expectedReliefAt;
            }

            return existing;
        }

        public LocalDemandPressure Get(EntityId placeId, string category, EntityId sourceFactId = default)
        {
            string canonical = LocalDemandCategory.Normalize(category);
            if (canonical == null)
            {
                canonical = category;
            }

            for (int i = 0; i < _pressures.Count; i++)
            {
                LocalDemandPressure pressure = _pressures[i];
                if (pressure.PlaceId == placeId
                    && pressure.Category == canonical
                    && (sourceFactId.IsNone || pressure.SourceFactId == sourceFactId))
                {
                    return pressure;
                }
            }

            return null;
        }

        public bool Relieve(EntityId placeId, string category, EntityId sourceFactId, int amount, long days, GameTime now)
        {
            LocalDemandPressure pressure = Get(placeId, category, sourceFactId);
            if (pressure == null || amount <= 0)
            {
                return false;
            }

            pressure.Severity = LocalDemandPressure.Clamp(pressure.Severity - amount, 0, 100);
            GameTime shortened = pressure.ExpectedReliefAt.PlusDays(-days);
            if (shortened < now)
            {
                shortened = now;
            }

            if (shortened < pressure.ExpectedReliefAt)
            {
                pressure.ExpectedReliefAt = shortened;
            }

            return true;
        }

        public void Restore(LocalDemandPressure pressure)
        {
            if (pressure != null)
            {
                _pressures.Add(pressure);
            }
        }
    }
}
