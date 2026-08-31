using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Durable business states. These are continuity problems, not a copy of the operator's day.
    /// </summary>
    public enum BusinessContinuityState
    {
        Normal = 0,
        Struggling = 1,
        ShortOnStock = 2,
        OwnerAbsent = 3,
        TemporarilyClosed = 4,
        ReplacementOperator = 5,
        Recovered = 6,
        Failed = 7,
        Inherited = 8
    }

    /// <summary>What the live game currently says about the person behind the counter.</summary>
    public enum OperatorAvailability
    {
        Available = 0,
        Sleeping = 1,
        AtHobby = 2,
        OffShift = 3,
        PhysicallyAbsent = 4,
        Dead = 5,
        Unknown = 6
    }

    public enum ServiceContinuitySurface
    {
        Available = 0,
        TemporarilyUnavailable = 1,
        Interrupted = 2,
        Failed = 3
    }

    /// <summary>
    /// Live service facts read from Elin. This is deliberately not saved: stock, sleep and shifts
    /// are vanilla-owned surface facts, while the ledger stores only persistent meaning.
    /// </summary>
    public sealed class BusinessServiceSnapshot
    {
        public BusinessServiceSnapshot(OperatorAvailability operatorAvailability, bool hasUsableStock)
        {
            OperatorAvailability = operatorAvailability;
            HasUsableStock = hasUsableStock;
        }

        public OperatorAvailability OperatorAvailability { get; }

        public bool HasUsableStock { get; }
    }

    /// <summary>How a business should currently be presented to the player.</summary>
    public sealed class BusinessProjection
    {
        public BusinessProjection(
            BusinessContinuityState state,
            ServiceContinuitySurface surface,
            bool visibleConsequence,
            string reason)
        {
            State = state;
            Surface = surface;
            VisibleConsequence = visibleConsequence;
            Reason = reason ?? string.Empty;
        }

        public BusinessContinuityState State { get; }

        public ServiceContinuitySurface Surface { get; }

        public bool VisibleConsequence { get; }

        public string Reason { get; }
    }

    /// <summary>One persistent shop, service counter, inn desk or comparable business.</summary>
    public sealed class BusinessRecord
    {
        public BusinessRecord(
            EntityId businessId,
            EntityId placeId,
            EntityId operatorId,
            BusinessContinuityState state,
            GameTime beganAt,
            GameTime lastChangedAt,
            EntityId causeFactId = default,
            EntityId replacementOperatorId = default,
            EntityId inheritedById = default)
        {
            BusinessId = businessId;
            PlaceId = placeId;
            OperatorId = operatorId;
            State = state;
            BeganAt = beganAt;
            LastChangedAt = lastChangedAt;
            CauseFactId = causeFactId;
            ReplacementOperatorId = replacementOperatorId;
            InheritedById = inheritedById;
        }

        public EntityId BusinessId { get; }

        public EntityId PlaceId { get; }

        public EntityId OperatorId { get; }

        public BusinessContinuityState State { get; internal set; }

        public GameTime BeganAt { get; }

        public GameTime LastChangedAt { get; internal set; }

        public EntityId CauseFactId { get; internal set; }

        public EntityId ReplacementOperatorId { get; internal set; }

        public EntityId InheritedById { get; internal set; }

        public bool HasFailedForAtLeast(GameTime now, long days)
        {
            return State == BusinessContinuityState.Failed && now >= LastChangedAt.PlusDays(days);
        }
    }

    public sealed class BusinessLedger
    {
        private readonly Dictionary<EntityId, BusinessRecord> _byBusiness = new Dictionary<EntityId, BusinessRecord>();

        public int Count => _byBusiness.Count;

        public IEnumerable<BusinessRecord> Records => _byBusiness.Values;

        public BusinessRecord Of(EntityId business)
        {
            _byBusiness.TryGetValue(business, out BusinessRecord record);
            return record;
        }

        public bool TryAdd(BusinessRecord record)
        {
            if (record == null || record.BusinessId.IsNone || record.PlaceId.IsNone || record.OperatorId.IsNone
                || _byBusiness.ContainsKey(record.BusinessId))
            {
                return false;
            }

            _byBusiness[record.BusinessId] = record;
            return true;
        }

        public void Restore(BusinessRecord record) => TryAdd(record);
    }

    /// <summary>
    /// Business continuity is persistent meaning over a real service surface. It records that a
    /// shop failed, recovered, changed operator or inherited ownership; it does not record that
    /// today's shopkeeper was asleep, at a hobby, or between shifts.
    /// </summary>
    public sealed class BusinessContinuity
    {
        private readonly NarrativeWorldState _world;

        public BusinessContinuity(NarrativeWorldState world)
        {
            _world = world;
        }

        public bool TryRegister(EntityId business, EntityId place, EntityId operatorId, GameTime now)
        {
            return _world.Businesses.TryAdd(new BusinessRecord(
                business, place, operatorId, BusinessContinuityState.Normal, now, now));
        }

        public bool TryChangeState(
            EntityId business,
            BusinessContinuityState state,
            GameTime now,
            EntityId causeFactId = default,
            EntityId actor = default,
            EntityId replacementOperatorId = default,
            EntityId inheritedById = default)
        {
            BusinessRecord record = _world.Businesses.Of(business);
            if (record == null || state == BusinessContinuityState.Normal)
            {
                return false;
            }

            record.State = state;
            record.LastChangedAt = now;
            record.CauseFactId = causeFactId;
            record.ReplacementOperatorId = replacementOperatorId;
            record.InheritedById = inheritedById;

            _world.Record(
                WorldEventType.BusinessStateChanged,
                actor.IsNone ? record.OperatorId : actor,
                business,
                now,
                0.5,
                record.PlaceId,
                causeFactId.IsNone ? null : new[] { causeFactId },
                tags: new[] { state.ToString() });
            return true;
        }

        public BusinessProjection Project(EntityId business, BusinessServiceSnapshot snapshot, GameTime now)
        {
            BusinessRecord record = _world.Businesses.Of(business);
            if (record == null)
            {
                return new BusinessProjection(
                    BusinessContinuityState.Normal,
                    ServiceContinuitySurface.Available,
                    false,
                    "untracked");
            }

            if (record.State == BusinessContinuityState.Failed)
            {
                return new BusinessProjection(
                    BusinessContinuityState.Failed,
                    ServiceContinuitySurface.Failed,
                    record.HasFailedForAtLeast(now, 30),
                    "business failed");
            }

            if (record.State == BusinessContinuityState.TemporarilyClosed
                || record.State == BusinessContinuityState.OwnerAbsent)
            {
                return new BusinessProjection(record.State, ServiceContinuitySurface.Interrupted, true, "business interrupted");
            }

            if (record.State == BusinessContinuityState.ReplacementOperator
                || record.State == BusinessContinuityState.Inherited
                || record.State == BusinessContinuityState.Recovered)
            {
                return new BusinessProjection(record.State, ServiceContinuitySurface.Available, true, "business changed");
            }

            if (snapshot != null && !snapshot.HasUsableStock)
            {
                return new BusinessProjection(
                    BusinessContinuityState.ShortOnStock,
                    ServiceContinuitySurface.Interrupted,
                    true,
                    "stock unavailable");
            }

            if (snapshot != null && IsOrdinaryTemporaryUnavailability(snapshot.OperatorAvailability))
            {
                return new BusinessProjection(
                    record.State,
                    ServiceContinuitySurface.TemporarilyUnavailable,
                    false,
                    "operator temporarily unavailable");
            }

            if (record.State == BusinessContinuityState.Struggling
                || record.State == BusinessContinuityState.ShortOnStock)
            {
                return new BusinessProjection(record.State, ServiceContinuitySurface.Interrupted, true, "business under pressure");
            }

            return new BusinessProjection(record.State, ServiceContinuitySurface.Available, false, "open");
        }

        private static bool IsOrdinaryTemporaryUnavailability(OperatorAvailability availability)
        {
            return availability == OperatorAvailability.Sleeping
                   || availability == OperatorAvailability.AtHobby
                   || availability == OperatorAvailability.OffShift;
        }
    }
}
