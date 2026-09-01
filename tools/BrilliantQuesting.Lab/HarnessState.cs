using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public sealed class HarnessState
    {
        public HarnessState(
            NarrativeWorldState world,
            SandboxVanillaState vanilla,
            EntityId playerId,
            EntityId primaryZoneId,
            string source)
        {
            World = world;
            Vanilla = vanilla;
            PlayerId = playerId;
            PrimaryZoneId = primaryZoneId;
            Source = source ?? string.Empty;
        }

        public NarrativeWorldState World { get; private set; }

        public SandboxVanillaState Vanilla { get; private set; }

        public EntityId PlayerId { get; }

        public EntityId PrimaryZoneId { get; }

        public string Source { get; }

        public List<EntityId> ActorIds { get; } = new List<EntityId>();

        public List<EntityId> InventoryOwnerIds { get; } = new List<EntityId>();

        public HarnessCoverage Coverage { get; } = new HarnessCoverage();

        public void RememberActor(EntityId id)
        {
            if (!id.IsNone && !ActorIds.Contains(id))
            {
                ActorIds.Add(id);
            }
        }

        public void RememberInventoryOwner(EntityId id)
        {
            if (!id.IsNone && !InventoryOwnerIds.Contains(id))
            {
                InventoryOwnerIds.Add(id);
            }
        }

        public void RoundTripWorld()
        {
            string json = WorldStateSerializer.Save(World, indented: false);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(json);
            SandboxVanillaState rebuilt = CapturedWorldSnapshot.Capture(this).Hydrate(reloaded).Vanilla;
            World = reloaded;
            Vanilla = rebuilt;
        }
    }
}
