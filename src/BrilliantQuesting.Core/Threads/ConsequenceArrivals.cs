using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    public enum ConsequenceArrivalOutcome
    {
        Arrived,
        AlreadyArrived,
        Refused
    }

    public sealed class ConsequenceArrivalResult
    {
        private ConsequenceArrivalResult(ConsequenceArrivalOutcome outcome, string reason, WorldEvent recordedEvent)
        {
            Outcome = outcome;
            Reason = reason ?? string.Empty;
            RecordedEvent = recordedEvent;
        }

        public ConsequenceArrivalOutcome Outcome { get; }

        public string Reason { get; }

        public WorldEvent RecordedEvent { get; }

        public bool DidArrive => Outcome == ConsequenceArrivalOutcome.Arrived;

        internal static ConsequenceArrivalResult Arrived(WorldEvent recordedEvent, string reason)
        {
            return new ConsequenceArrivalResult(ConsequenceArrivalOutcome.Arrived, reason, recordedEvent);
        }

        internal static ConsequenceArrivalResult Already(string reason)
        {
            return new ConsequenceArrivalResult(ConsequenceArrivalOutcome.AlreadyArrived, reason, null);
        }

        internal static ConsequenceArrivalResult Refused(string reason)
        {
            return new ConsequenceArrivalResult(ConsequenceArrivalOutcome.Refused, reason, null);
        }
    }

    /// <summary>
    /// Brings a consequence of existing history to a place the player can encounter it.
    ///
    /// This is not a director and not a random encounter generator: a caller names an unresolved
    /// thread, the actor carrying the consequence, the surface it should reach, and the facts that
    /// made it due. The only physical claim made here is the gated relocation of an already-known
    /// actor to an already-known player surface. If that cannot be verified, no arrival is written.
    /// </summary>
    public sealed class ConsequenceArrivals
    {
        public const string ArrivalTag = "consequence_arrival";
        public const string HomeSurfaceTag = "surface:home";
        public const string PlayerSurfaceTag = "surface:player";

        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public ConsequenceArrivals(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        public ConsequenceArrivalResult TryBringToHome(
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            GameTime now,
            string reason,
            IReadOnlyList<EntityId> related = null,
            WorldEventType eventType = WorldEventType.ThreadEscalated,
            double magnitude = 0.5)
        {
            if (_world == null || _vanilla == null)
            {
                return ConsequenceArrivalResult.Refused("world or vanilla state is missing");
            }

            HomeState home = _vanilla?.GetHomeState();
            if (home == null)
            {
                return ConsequenceArrivalResult.Refused("Home is unreadable or absent");
            }

            if (home.ZoneId.IsNone)
            {
                return ConsequenceArrivalResult.Refused("Home zone is unknown");
            }

            if (_world.Registry.GetSite(home.ZoneId) == null)
            {
                _world.Registry.Add(new NarrativeSite(
                    home.ZoneId,
                    string.IsNullOrEmpty(home.Name) ? "Home" : home.Name,
                    "home"));
            }

            return TryBringToSurface(thread, actor, target, home.ZoneId, now, reason, related, eventType, magnitude, HomeSurfaceTag);
        }

        public ConsequenceArrivalResult TryBringToPlayer(
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            GameTime now,
            string reason,
            IReadOnlyList<EntityId> related = null,
            WorldEventType eventType = WorldEventType.ThreadEscalated,
            double magnitude = 0.5)
        {
            EntityId playerZone = _vanilla == null ? EntityId.None : _vanilla.GetZoneOf(_vanilla.PlayerId);
            if (playerZone.IsNone)
            {
                return ConsequenceArrivalResult.Refused("player zone is unknown");
            }

            return TryBringToSurface(thread, actor, target, playerZone, now, reason, related, eventType, magnitude, PlayerSurfaceTag);
        }

        private ConsequenceArrivalResult TryBringToSurface(
            NarrativeThread thread,
            EntityId actor,
            EntityId target,
            EntityId zone,
            GameTime now,
            string reason,
            IReadOnlyList<EntityId> related,
            WorldEventType eventType,
            double magnitude,
            string surfaceTag)
        {
            if (_world == null || _vanilla == null)
            {
                return ConsequenceArrivalResult.Refused("world or vanilla state is missing");
            }

            if (thread == null || !CanSurface(thread))
            {
                return ConsequenceArrivalResult.Refused("thread cannot surface");
            }

            if (actor.IsNone || _world.Registry.GetNpc(actor) == null)
            {
                return ConsequenceArrivalResult.Refused("arriving actor is not a known character");
            }

            if (zone.IsNone)
            {
                return ConsequenceArrivalResult.Refused("arrival zone is unknown");
            }

            string reasonTag = ReasonTag(reason);
            if (AlreadyRecorded(thread.Id, actor, target, surfaceTag, reasonTag))
            {
                return ConsequenceArrivalResult.Already("arrival was already recorded");
            }

            if (_vanilla.GetLifeState(actor) != VanillaLifeState.Alive)
            {
                return ConsequenceArrivalResult.Refused("arriving actor is not verifiably alive");
            }

            EntityId current = _vanilla.GetZoneOf(actor);
            if (current.IsNone)
            {
                return ConsequenceArrivalResult.Refused("arriving actor whereabouts are unknown");
            }

            if (current != zone)
            {
                if (!_vanilla.Supports(VanillaCapability.MoveCharaBetweenZones))
                {
                    return ConsequenceArrivalResult.Refused("character relocation is unavailable on this build");
                }

                if (!_vanilla.MayMutate(actor, MutationKind.Relocate))
                {
                    return ConsequenceArrivalResult.Refused("mutation policy refuses to relocate the arriving actor");
                }

                if (!_vanilla.TryRelocate(actor, zone) || _vanilla.GetZoneOf(actor) != zone)
                {
                    return ConsequenceArrivalResult.Refused("relocation was not verified");
                }
            }

            if (thread.State == ThreadState.Dormant || thread.State == ThreadState.Latent)
            {
                thread.State = ThreadState.Active;
            }

            thread.LastAdvancedAt = now;
            WorldEvent recorded = _world.Record(
                eventType,
                actor,
                target,
                now,
                magnitude,
                zone,
                related: related,
                tags: new[] { ArrivalTag, surfaceTag, reasonTag },
                threadId: thread.Id);

            return ConsequenceArrivalResult.Arrived(recorded, "arrived at " + zone);
        }

        private bool AlreadyRecorded(EntityId threadId, EntityId actor, EntityId target, string surfaceTag, string reasonTag)
        {
            foreach (WorldEvent worldEvent in _world.Ledger.Events)
            {
                if (worldEvent.ThreadId == threadId
                    && worldEvent.Actor == actor
                    && worldEvent.Target == target
                    && HasTag(worldEvent, ArrivalTag)
                    && HasTag(worldEvent, surfaceTag)
                    && HasTag(worldEvent, reasonTag))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanSurface(NarrativeThread thread)
        {
            return thread.State == ThreadState.Latent
                   || thread.State == ThreadState.Active
                   || thread.State == ThreadState.Dormant;
        }

        private static string ReasonTag(string reason)
        {
            return "arrival_reason:" + (string.IsNullOrEmpty(reason) ? "unspecified" : reason);
        }

        private static bool HasTag(WorldEvent worldEvent, string tag)
        {
            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                if (worldEvent.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
