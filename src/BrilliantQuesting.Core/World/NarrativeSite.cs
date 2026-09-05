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

        /// <summary>
        /// The seed the place was planned from, so the same plan rebuilds the same place.
        ///
        /// Not a licence to run genesis again: <see cref="Established"/> refuses that outright. It
        /// is here for reproducing a plan under inspection, and for the day an ephemeral interior
        /// has to be rebuilt from nothing after unload.
        /// </summary>
        public ulong GenerationSeed { get; set; }

        /// <summary>
        /// Genesis has run for this place (BQ-087).
        ///
        /// The flag is what makes "a visited place is never destructively regenerated" enforceable
        /// rather than a convention: <see cref="SiteGenesis"/> hands an established site straight
        /// back instead of building a second one over it, and the flag persists, so a reload cannot
        /// turn a place the player has been into a fresh one. False on every site an archetype
        /// wrote down directly, which is correct - those were never generated.
        /// </summary>
        public bool Established { get; set; }

        /// <summary>When genesis ran. Meaningless while <see cref="Established"/> is false.</summary>
        public GameTime EstablishedAt { get; set; }

        /// <summary>
        /// The curated grammar this place was planned from (BQ-089), or empty for a place nobody
        /// planned that way.
        ///
        /// The grammar id and <see cref="GenerationSeed"/> are stored instead of the plan itself,
        /// because the plan is content and content is never written into a save: a place composed
        /// from a corrected grammar is the same place with the same history, the way a storylet
        /// that already fired reads back in the new wording of the same event.
        /// </summary>
        public string GrammarId { get; set; } = string.Empty;

        /// <summary>
        /// The ways in this place was made with. At least one that goes through somebody and at
        /// least one that does not - see <see cref="SiteApproach"/>.
        /// </summary>
        public List<SiteApproach> Approaches { get; } = new List<SiteApproach>();

        public override string ToString() => Name + " [" + SiteType + "]";
    }
}
