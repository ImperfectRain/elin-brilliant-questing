using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Everything the simulation has ever created, keyed by stable id. Entries are not deleted
    /// when a character dies or a site is razed - history keeps referring to them, and a dead
    /// merchant's daughter still needs someone to be angry about.
    ///
    /// People come in two kinds, and the difference is narrow and deliberate. Nearly every record
    /// is an actor in its own right. A few are retired aliases: a second id one physical character
    /// was registered under before the intake was canonical (<see cref="NarrativeNpc.AliasOf"/>).
    /// Those stay stored, stay saved and stay resolvable, because history names them - but they do
    /// not participate, because one live character is one actor.
    ///
    /// <see cref="Npcs"/> is therefore the people, and <see cref="AllNpcs"/> is the records. A
    /// consumer asking who is in the world wants the first; only save/load, existence checks over
    /// historical references, and reporting want the second. In a world with no duplicate they are
    /// the same collection.
    /// </summary>
    public sealed class EntityRegistry
    {
        private readonly Dictionary<EntityId, NarrativeNpc> _npcs = new Dictionary<EntityId, NarrativeNpc>();
        private readonly Dictionary<EntityId, NarrativeNpc> _actors = new Dictionary<EntityId, NarrativeNpc>();
        private readonly Dictionary<EntityId, Organization> _organizations = new Dictionary<EntityId, Organization>();
        private readonly Dictionary<EntityId, NarrativeSite> _sites = new Dictionary<EntityId, NarrativeSite>();

        /// <summary>Everybody who participates as an actor. The default answer to "who is here".</summary>
        public IReadOnlyDictionary<EntityId, NarrativeNpc> Npcs => _actors;

        /// <summary>
        /// Every person record, retired aliases included. For save/load, for checking that a
        /// historical reference still resolves, and for reporting - never for deciding who acts.
        /// </summary>
        public IReadOnlyDictionary<EntityId, NarrativeNpc> AllNpcs => _npcs;

        public IReadOnlyDictionary<EntityId, Organization> Organizations => _organizations;

        public IReadOnlyDictionary<EntityId, NarrativeSite> Sites => _sites;

        public NarrativeNpc Add(NarrativeNpc npc)
        {
            _npcs[npc.Id] = npc;
            if (npc.IsCanonical)
            {
                _actors[npc.Id] = npc;
            }
            else
            {
                _actors.Remove(npc.Id);
            }

            return npc;
        }

        /// <summary>
        /// Records that <paramref name="alias"/> and <paramref name="canonical"/> were two ids for
        /// one physical character, and that the canonical one is the actor.
        ///
        /// The alias keeps its record and everything in it. What it stops doing is participating:
        /// it leaves <see cref="Npcs"/>, so nothing that asks who is in the world can cast,
        /// simulate or count it as a second person. Nothing is rewritten - no fact, event,
        /// relationship or thread is repointed - because the alias is still a true name for the
        /// history written under it, and <see cref="Canonical"/> is how a live consumer gets from
        /// that name to the actor.
        ///
        /// Refused where it would be a lie or a loop: an unknown id, an id retired onto itself, or
        /// a canonical that is itself retired. Retiring the same alias onto the same canonical
        /// twice is a no-op, which is what makes it safe to run on every load.
        /// </summary>
        public bool Retire(EntityId alias, EntityId canonical)
        {
            if (alias.IsNone || canonical.IsNone || alias == canonical)
            {
                return false;
            }

            if (!_npcs.TryGetValue(alias, out NarrativeNpc aliasNpc)
                || !_npcs.TryGetValue(canonical, out NarrativeNpc canonicalNpc)
                || !canonicalNpc.IsCanonical)
            {
                return false;
            }

            aliasNpc.AliasOf = canonical;
            _actors.Remove(alias);
            return true;
        }

        /// <summary>
        /// The actor an id names. Itself for everybody, and the canonical record for a retired
        /// alias - so a historical reference reaches the living person rather than a second one.
        ///
        /// An id this registry has never heard of comes back unchanged: canonicalisation resolves
        /// names, it does not decide who exists. The walk is bounded because a chain that does not
        /// terminate is a defect and returning the last id reached is better than hanging.
        /// </summary>
        public EntityId Canonical(EntityId id)
        {
            EntityId current = id;
            for (int hops = 0; hops < MaxAliasHops; hops++)
            {
                if (!_npcs.TryGetValue(current, out NarrativeNpc npc) || npc.IsCanonical)
                {
                    return current;
                }

                current = npc.AliasOf;
            }

            return current;
        }

        /// <summary>Whether this id names somebody who participates as an actor.</summary>
        public bool IsActor(EntityId id) => !id.IsNone && _actors.ContainsKey(id);

        private const int MaxAliasHops = 8;

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
