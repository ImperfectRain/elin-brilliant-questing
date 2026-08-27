using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    public enum SitePersistence
    {
        /// <summary>Throwaway interior. Must write consequences back to its thread before unloading.</summary>
        Ephemeral,

        /// <summary>Stays on the world map because the player changed it or it still matters.</summary>
        Persistent
    }

    /// <summary>
    /// A place the simulation cares about. This is a descriptor, not a map: Elin's own zone
    /// generation builds the terrain, and the narrative layer decorates it with occupants,
    /// evidence and access constraints.
    /// </summary>
    public sealed class NarrativeSite
    {
        public NarrativeSite(EntityId id, string name, string siteType)
        {
            Id = id;
            Name = name;
            SiteType = siteType;
            OccupantIds = new List<EntityId>();
            ImportantObjectIds = new List<EntityId>();
        }

        public EntityId Id { get; }

        public string Name { get; set; }

        /// <summary>Ontology term: "hideout", "ruin", "camp", "workshop", "shrine", "estate".</summary>
        public string SiteType { get; }

        /// <summary>Handle for the live Elin Zone once generated.</summary>
        public string VanillaZoneRef { get; set; }

        public EntityId ControllingOrganizationId { get; set; }

        public List<EntityId> OccupantIds { get; }

        public List<EntityId> ImportantObjectIds { get; }

        public int DangerLevel { get; set; }

        public SitePersistence Persistence { get; set; } = SitePersistence.Ephemeral;

        /// <summary>Recorded so a site can be regenerated identically after unload.</summary>
        public ulong GenerationSeed { get; set; }

        public override string ToString() => Name + " [" + SiteType + "]";
    }
}
