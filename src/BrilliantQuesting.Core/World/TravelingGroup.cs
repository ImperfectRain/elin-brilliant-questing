using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    public enum TravelingGroupState
    {
        Planned,
        Underway,
        Arrived,
        Interrupted,
        Failed
    }

    public enum TravelPhysicalPlan
    {
        /// <summary>BQ records only the journey's meaning. No physical whereabouts are asserted.</summary>
        SemanticOnly,

        /// <summary>BQ may attempt one coarse, gated relocation at the arrival milestone.</summary>
        BqRelocation
    }

    public enum TravelMovementOwnership
    {
        Unknown,
        SemanticOnly,
        BqRelocationReserved,
        BqRelocated,
        VanillaGlobalGoal,
        Refused
    }

    public sealed class TravelingGroupMember
    {
        public TravelingGroupMember(EntityId actorId)
        {
            ActorId = actorId;
            Movement = TravelMovementOwnership.Unknown;
            LastKnownZone = EntityId.None;
            Note = string.Empty;
        }

        public EntityId ActorId { get; }

        public TravelMovementOwnership Movement { get; internal set; }

        public EntityId LastKnownZone { get; internal set; }

        public string Note { get; internal set; }
    }

    /// <summary>
    /// A persistent semantic journey: who or what is going somewhere, why, and which milestone
    /// history has reached. It is not a route, a pathfinder, a tile position or a copy of any
    /// member or item it names.
    /// </summary>
    public sealed class TravelingGroup
    {
        public TravelingGroup(
            EntityId id,
            string kind,
            EntityId originId,
            EntityId destinationId,
            string purpose,
            GameTime createdAt,
            GameTime departureAt,
            GameTime expectedArrivalAt,
            TravelPhysicalPlan physicalPlan = TravelPhysicalPlan.SemanticOnly)
        {
            Id = id;
            Kind = kind ?? string.Empty;
            OriginId = originId;
            DestinationId = destinationId;
            Purpose = purpose ?? string.Empty;
            CreatedAt = createdAt;
            DepartureAt = departureAt;
            ExpectedArrivalAt = expectedArrivalAt;
            PhysicalPlan = physicalPlan;
            StateChangedAt = createdAt;
            Members = new List<TravelingGroupMember>();
            CargoIds = new List<EntityId>();
            RouteRisk = 0.0;
        }

        public EntityId Id { get; }

        /// <summary>Ontology term: caravan, patrol, pilgrimage, expedition, refugee_column.</summary>
        public string Kind { get; }

        public EntityId OriginId { get; }

        public EntityId DestinationId { get; }

        /// <summary>Ontology term, not prose: carry_alcohol, pursue_rescue, flee_war.</summary>
        public string Purpose { get; }

        public GameTime CreatedAt { get; }

        public GameTime DepartureAt { get; }

        public GameTime ExpectedArrivalAt { get; }

        public TravelPhysicalPlan PhysicalPlan { get; }

        /// <summary>Coarse pressure, not a table to roll on by itself.</summary>
        public double RouteRisk { get; set; }

        public TravelingGroupState State { get; internal set; }

        public GameTime StateChangedAt { get; internal set; }

        public EntityId ThreadId { get; set; }

        public EntityId InterruptionCauseId { get; internal set; }

        public EntityId InterruptionSiteId { get; internal set; }

        public EntityId PlannedEventId { get; internal set; }

        public EntityId DepartedEventId { get; internal set; }

        public EntityId ArrivedEventId { get; internal set; }

        public EntityId InterruptedEventId { get; internal set; }

        public EntityId FailedEventId { get; internal set; }

        public List<TravelingGroupMember> Members { get; }

        public List<EntityId> CargoIds { get; }

        public bool IsTerminal => State == TravelingGroupState.Arrived
                                  || State == TravelingGroupState.Interrupted
                                  || State == TravelingGroupState.Failed;

        public bool AddMember(EntityId actorId)
        {
            if (actorId.IsNone || Member(actorId) != null)
            {
                return false;
            }

            Members.Add(new TravelingGroupMember(actorId));
            return true;
        }

        public bool AddCargo(EntityId cargoId)
        {
            if (cargoId.IsNone || CargoIds.Contains(cargoId))
            {
                return false;
            }

            CargoIds.Add(cargoId);
            return true;
        }

        public TravelingGroupMember Member(EntityId actorId)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].ActorId == actorId)
                {
                    return Members[i];
                }
            }

            return null;
        }

        public override string ToString()
        {
            return Kind + " " + Id + " " + OriginId + " -> " + DestinationId + " [" + State + "]";
        }
    }

    public sealed class TravelingGroupLedger
    {
        private readonly Dictionary<EntityId, TravelingGroup> _byId = new Dictionary<EntityId, TravelingGroup>();

        public int Count => _byId.Count;

        public IEnumerable<TravelingGroup> Groups => _byId.Values;

        public TravelingGroup Of(EntityId id)
        {
            _byId.TryGetValue(id, out TravelingGroup group);
            return group;
        }

        public bool TryAdd(TravelingGroup group)
        {
            if (group == null || group.Id.IsNone || group.OriginId.IsNone || group.DestinationId.IsNone
                || _byId.ContainsKey(group.Id))
            {
                return false;
            }

            _byId[group.Id] = group;
            return true;
        }

        public void Restore(TravelingGroup group) => TryAdd(group);
    }

    public sealed class TravelingGroupRound
    {
        public int Planned { get; internal set; }

        public int Departed { get; internal set; }

        public int Arrived { get; internal set; }

        public int Interrupted { get; internal set; }

        public int Failed { get; internal set; }

        public int Delayed { get; internal set; }

        public List<string> Notes { get; } = new List<string>();

        public bool DidAnything => Planned > 0 || Departed > 0 || Arrived > 0
                                   || Interrupted > 0 || Failed > 0 || Delayed > 0;

        public override string ToString()
        {
            return "planned " + Planned + ", departed " + Departed + ", arrived " + Arrived
                   + ", interrupted " + Interrupted + ", failed " + Failed + ", delayed " + Delayed;
        }
    }

    public sealed class TravelingGroupLifecycle
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public TravelingGroupLifecycle(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        public TravelingGroupRound LastRound { get; private set; } = new TravelingGroupRound();

        public bool TryPlan(TravelingGroup group)
        {
            if (!_world.TravelingGroups.TryAdd(group))
            {
                return false;
            }

            if (group.PlannedEventId.IsNone)
            {
                WorldEvent planned = Record(group, WorldEventType.TravelPlanned, group.OriginId, _vanilla.Now, 0.2, "planned");
                group.PlannedEventId = planned.Id;
            }

            return true;
        }

        public TravelingGroupRound Advance(GameTime now)
        {
            TravelingGroupRound round = new TravelingGroupRound();
            foreach (TravelingGroup group in new List<TravelingGroup>(_world.TravelingGroups.Groups))
            {
                if (group.IsTerminal)
                {
                    continue;
                }

                if (group.State == TravelingGroupState.Planned && now >= group.DepartureAt)
                {
                    Depart(group, round, now);
                }

                if (group.State == TravelingGroupState.Underway && now >= group.ExpectedArrivalAt)
                {
                    TryArrive(group, round, now);
                }
            }

            LastRound = round;
            return round;
        }

        public bool TryInterrupt(EntityId groupId, EntityId causeId, EntityId siteId, GameTime now)
        {
            TravelingGroup group = _world.TravelingGroups.Of(groupId);
            if (group == null || group.IsTerminal || group.InterruptedEventId != EntityId.None)
            {
                return false;
            }

            group.State = TravelingGroupState.Interrupted;
            group.StateChangedAt = now;
            group.InterruptionCauseId = causeId;
            group.InterruptionSiteId = siteId;
            WorldEvent interrupted = Record(group, WorldEventType.TravelInterrupted, siteId, now, 0.7, "interrupted", causeId);
            group.InterruptedEventId = interrupted.Id;
            return true;
        }

        public bool TryFail(EntityId groupId, EntityId causeId, EntityId siteId, GameTime now)
        {
            TravelingGroup group = _world.TravelingGroups.Of(groupId);
            if (group == null || group.State == TravelingGroupState.Arrived || group.State == TravelingGroupState.Failed
                || group.FailedEventId != EntityId.None)
            {
                return false;
            }

            group.State = TravelingGroupState.Failed;
            group.StateChangedAt = now;
            group.InterruptionCauseId = causeId.IsNone ? group.InterruptionCauseId : causeId;
            group.InterruptionSiteId = siteId.IsNone ? group.InterruptionSiteId : siteId;
            WorldEvent failed = Record(
                group,
                WorldEventType.TravelFailed,
                group.InterruptionSiteId.IsNone ? group.OriginId : group.InterruptionSiteId,
                now,
                0.9,
                "failed",
                group.InterruptionCauseId);
            group.FailedEventId = failed.Id;
            return true;
        }

        private void Depart(TravelingGroup group, TravelingGroupRound round, GameTime now)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                ClassifyMovement(group, group.Members[i]);
            }

            group.State = TravelingGroupState.Underway;
            group.StateChangedAt = now;
            if (group.DepartedEventId.IsNone)
            {
                WorldEvent departed = Record(group, WorldEventType.TravelDeparted, group.OriginId, now, 0.4, "departed");
                group.DepartedEventId = departed.Id;
                round.Departed++;
            }
        }

        private void TryArrive(TravelingGroup group, TravelingGroupRound round, GameTime now)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                TravelingGroupMember member = group.Members[i];
                if (!ReconcileArrival(group, member))
                {
                    round.Delayed++;
                    round.Notes.Add(group.Id + " did not arrive: " + member.ActorId + " - " + member.Note);
                    return;
                }
            }

            group.State = TravelingGroupState.Arrived;
            group.StateChangedAt = now;
            if (group.ArrivedEventId.IsNone)
            {
                WorldEvent arrived = Record(group, WorldEventType.TravelArrived, group.DestinationId, now, 0.6, "arrived");
                group.ArrivedEventId = arrived.Id;
                round.Arrived++;
            }
        }

        private void ClassifyMovement(TravelingGroup group, TravelingGroupMember member)
        {
            ActorActivity activity = _vanilla.GetActorActivity(member.ActorId);
            EntityId where = !activity.CurrentZone.IsNone ? activity.CurrentZone : _vanilla.GetZoneOf(member.ActorId);
            member.LastKnownZone = where;

            VanillaMovement movement = activity.VanillaMovementState();
            if (movement == VanillaMovement.Moving)
            {
                member.Movement = TravelMovementOwnership.VanillaGlobalGoal;
                member.Note = "vanilla global travel is already moving this actor; BQ observes";
                return;
            }

            if (group.PhysicalPlan != TravelPhysicalPlan.BqRelocation)
            {
                member.Movement = TravelMovementOwnership.SemanticOnly;
                member.Note = "semantic group travel only; no physical whereabouts asserted";
                return;
            }

            if (movement == VanillaMovement.Unknown)
            {
                member.Movement = TravelMovementOwnership.SemanticOnly;
                member.Note = "vanilla movement is unknown; BQ refuses competing physical travel";
                return;
            }

            if (where.IsNone)
            {
                member.Movement = TravelMovementOwnership.Refused;
                member.Note = "vanilla whereabouts are unknown; no relocation attempted";
                return;
            }

            if (where != group.OriginId)
            {
                member.Movement = TravelMovementOwnership.Refused;
                member.Note = "actor is not at the group's origin; no location fact invented";
                return;
            }

            if (!_vanilla.Supports(VanillaCapability.MoveCharaBetweenZones)
                || !_vanilla.MayMutate(member.ActorId, MutationKind.Relocate))
            {
                member.Movement = TravelMovementOwnership.Refused;
                member.Note = "the relocation seam or mutation policy refuses this actor";
                return;
            }

            member.Movement = TravelMovementOwnership.BqRelocationReserved;
            member.Note = "BQ may attempt one coarse relocation at arrival";
        }

        private bool ReconcileArrival(TravelingGroup group, TravelingGroupMember member)
        {
            if (member.Movement == TravelMovementOwnership.VanillaGlobalGoal)
            {
                ActorActivity activity = _vanilla.GetActorActivity(member.ActorId);
                EntityId where = !activity.CurrentZone.IsNone ? activity.CurrentZone : _vanilla.GetZoneOf(member.ActorId);
                member.LastKnownZone = where;
                if (where == group.DestinationId)
                {
                    member.Note = "vanilla reports the actor at the intended destination";
                    return true;
                }

                member.Note = where.IsNone
                    ? "vanilla has not reported the actor's destination yet"
                    : "vanilla reports " + where + ", not the intended destination";
                return false;
            }

            if (member.Movement == TravelMovementOwnership.BqRelocationReserved)
            {
                if (_vanilla.TryRelocate(member.ActorId, group.DestinationId))
                {
                    member.Movement = TravelMovementOwnership.BqRelocated;
                    member.LastKnownZone = group.DestinationId;
                    member.Note = "BQ relocated this actor through the gated seam at arrival";
                    return true;
                }

                member.Movement = TravelMovementOwnership.Refused;
                member.Note = "the gated relocation write failed at arrival";
                return false;
            }

            if (member.Movement == TravelMovementOwnership.Refused)
            {
                return false;
            }

            member.Note = "semantic arrival only; no physical whereabouts asserted";
            return true;
        }

        private WorldEvent Record(
            TravelingGroup group,
            WorldEventType type,
            EntityId zone,
            GameTime now,
            double magnitude,
            string stateTag,
            EntityId causeId = default)
        {
            List<EntityId> related = new List<EntityId>();
            related.Add(group.OriginId);
            related.Add(group.DestinationId);
            for (int i = 0; i < group.Members.Count; i++)
            {
                related.Add(group.Members[i].ActorId);
            }

            for (int i = 0; i < group.CargoIds.Count; i++)
            {
                related.Add(group.CargoIds[i]);
            }

            if (!causeId.IsNone)
            {
                related.Add(causeId);
            }

            return _world.Record(
                type,
                EntityId.None,
                group.Id,
                now,
                magnitude,
                zone,
                related,
                tags: new[] { group.Kind, group.Purpose, stateTag },
                threadId: group.ThreadId);
        }
    }
}
