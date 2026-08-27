using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Everything the simulation has ever created, keyed by stable id. Entries are not deleted
    /// when a character dies or a site is razed - history keeps referring to them, and a dead
    /// merchant's daughter still needs someone to be angry about.
    /// </summary>
    public sealed class EntityRegistry
    {
        private readonly Dictionary<EntityId, NarrativeNpc> _npcs = new Dictionary<EntityId, NarrativeNpc>();
        private readonly Dictionary<EntityId, Organization> _organizations = new Dictionary<EntityId, Organization>();
        private readonly Dictionary<EntityId, NarrativeSite> _sites = new Dictionary<EntityId, NarrativeSite>();

        public IReadOnlyDictionary<EntityId, NarrativeNpc> Npcs => _npcs;

        public IReadOnlyDictionary<EntityId, Organization> Organizations => _organizations;

        public IReadOnlyDictionary<EntityId, NarrativeSite> Sites => _sites;

        public NarrativeNpc Add(NarrativeNpc npc)
        {
            _npcs[npc.Id] = npc;
            return npc;
        }

        public Organization Add(Organization organization)
        {
            _organizations[organization.Id] = organization;
            return organization;
        }

        public NarrativeSite Add(NarrativeSite site)
        {
            _sites[site.Id] = site;
            return site;
        }

        public NarrativeNpc GetNpc(EntityId id)
        {
            _npcs.TryGetValue(id, out NarrativeNpc npc);
            return npc;
        }

        public Organization GetOrganization(EntityId id)
        {
            _organizations.TryGetValue(id, out Organization organization);
            return organization;
        }

        public NarrativeSite GetSite(EntityId id)
        {
            _sites.TryGetValue(id, out NarrativeSite site);
            return site;
        }

        /// <summary>Display name for logs and dialogue; falls back to the id so traces never break.</summary>
        public string NameOf(EntityId id)
        {
            if (_npcs.TryGetValue(id, out NarrativeNpc npc))
            {
                return npc.Name;
            }

            if (_organizations.TryGetValue(id, out Organization organization))
            {
                return organization.Name;
            }

            if (_sites.TryGetValue(id, out NarrativeSite site))
            {
                return site.Name;
            }

            return id.ToString();
        }
    }
}
