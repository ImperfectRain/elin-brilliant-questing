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

        /// <summary>
        /// What this place keeps is behind something somebody else holds the key to - a strongbox,
        /// a locked cabinet, a back room the shop does not show you.
        ///
        /// Deliberately about the *contents*, not about standing in the doorway. Elin decides where
        /// the player's body is, and a narrative layer that argued with it would be wrong the
        /// moment a tester walked through the door. What the simulation owns is whether the things
        /// kept here are within reach, which is exactly the question breaking in answers.
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// Who may reach what this place keeps. An owner admits people; a burglar admits themselves.
        ///
        /// Persisted, because getting in is an achievement that has to survive a save. A player who
        /// picks a lock, saves, and loads has not un-picked it.
        /// </summary>
        public List<EntityId> AdmittedIds { get; } = new List<EntityId>();

        /// <summary>Whether this character can reach what the place keeps.</summary>
        public bool Admits(EntityId who) => !Restricted || AdmittedIds.Contains(who);

        public void Admit(EntityId who)
        {
            if (!who.IsNone && !AdmittedIds.Contains(who))
            {
                AdmittedIds.Add(who);
            }
        }

        public SitePersistence Persistence { get; set; } = SitePersistence.Ephemeral;

        /// <summary>Recorded so a site can be regenerated identically after unload.</summary>
        public ulong GenerationSeed { get; set; }

        public override string ToString() => Name + " [" + SiteType + "]";
    }
}
