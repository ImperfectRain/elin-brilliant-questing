using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// The two-way link between the simulation's stable ids and the game's live objects.
    ///
    /// The simulation keys everything on <see cref="EntityId"/>, which outlives any Chara instance.
    /// Elin keys everything on <c>Card.uid</c>, which survives save and load but means nothing to
    /// the world model. This is the only place the two meet, and it is persisted with the world so
    /// that a character the player met eighty hours ago is still the same character.
    /// </summary>
    internal sealed class ElinBindings
    {
        private readonly Dictionary<EntityId, int> _entityToUid = new Dictionary<EntityId, int>();
        private readonly Dictionary<int, EntityId> _uidToEntity = new Dictionary<int, EntityId>();

        internal IReadOnlyDictionary<EntityId, int> All => _entityToUid;

        internal void Bind(EntityId entity, int uid)
        {
            if (entity.IsNone || uid == 0)
            {
                return;
            }

            _entityToUid[entity] = uid;
            _uidToEntity[uid] = entity;
        }

        internal void BindSavedRefs(NarrativeWorldState world, ManualLogSource log = null)
        {
            foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
            {
                if (int.TryParse(npc.VanillaCharaRef, out int uid))
                {
                    Bind(npc.Id, uid);
                    continue;
                }

                Chara match = FindStagedChara(npc.Name);
                if (match != null)
                {
                    Bind(npc.Id, match.uid);
                    npc.VanillaCharaRef = match.uid.ToString();
                    log?.LogInfo("Recovered binding for " + npc.Name + " from loaded map (uid "
                                 + match.uid + ").");
                }
            }
        }

        internal bool TryGetUid(EntityId entity, out int uid)
        {
            return _entityToUid.TryGetValue(entity, out uid);
        }

        internal bool TryGetEntity(int uid, out EntityId entity)
        {
            return _uidToEntity.TryGetValue(uid, out entity);
        }

        /// <summary>
        /// Resolves to a live Chara. Looks in the loaded zone first, then the global roster, so
        /// an NPC who has wandered off the current map is still reachable.
        /// </summary>
        internal Chara ResolveChara(EntityId entity)
        {
            if (!TryGetUid(entity, out int uid))
            {
                return null;
            }

            Chara inZone = EClass._zone?.FindChara(uid);
            if (inZone != null)
            {
                return inZone;
            }

            return EClass.game?.cards?.Find(uid);
        }

        /// <summary>
        /// Recovers a binding lost before the stager began writing `VanillaCharaRef`.
        ///
        /// It matches on `c_altName` only, because that is the field the stager sets on the
        /// characters it creates. Matching a display name as well would let the simulation adopt
        /// an ordinary villager who merely shares a generated name, and everything downstream -
        /// rewritten dialogue, forced relocation, affinity and karma writes - would then land on
        /// a character the mod does not own. A tie is refused rather than guessed.
        /// </summary>
        private static Chara FindStagedChara(string name)
        {
            if (string.IsNullOrEmpty(name) || EClass._map?.charas == null)
            {
                return null;
            }

            Chara match = null;
            foreach (Chara chara in EClass._map.charas)
            {
                if (chara == null || chara.isDead || chara.c_altName != name)
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = chara;
            }

            return match;
        }

        internal Thing ResolveThing(EntityId entity, Card owner)
        {
            if (owner?.things == null || !TryGetUid(entity, out int uid))
            {
                return null;
            }

            return owner.things.Find(uid);
        }

        internal void Clear()
        {
            _entityToUid.Clear();
            _uidToEntity.Clear();
        }
    }
}
