using System;
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

        /// <summary>
        /// Rebuilds the identity map from the save.
        ///
        /// People come back from `NarrativeNpc.VanillaCharaRef`; everything else - items above
        /// all - comes from `world.ExternalRefs`. Until that second half existed, a reload left
        /// the map holding only people, so `EntityIdFor` would mint a fresh `item_&lt;uid&gt;` for a
        /// Thing the simulation already knew under another id. The stolen ring in somebody's
        /// pack stopped matching the Possesses fact about it, and returning or keeping it
        /// silently became impossible.
        /// </summary>
        internal void BindSavedRefs(NarrativeWorldState world, ManualLogSource log = null)
        {
            int restored = 0;
            foreach (KeyValuePair<EntityId, string> pair in world.ExternalRefs)
            {
                if (int.TryParse(pair.Value, out int savedUid))
                {
                    Bind(pair.Key, savedUid);
                    restored++;
                }
            }

            if (restored > 0)
            {
                log?.LogInfo("Restored " + restored + " object binding(s) from the save.");
            }

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

        /// <summary>
        /// The id this character would be known by, without registering anything.
        ///
        /// One convention, in one place: the observer enrols people in the world model when it
        /// mints an id, and the Home read must not, but both have to arrive at the same string or
        /// a resident who is later seen acting would come back as a second person.
        /// </summary>
        internal static EntityId MintCharaId(Chara chara, EntityId playerId)
        {
            if (chara == null)
            {
                return EntityId.None;
            }

            return chara.IsPC ? playerId : EntityId.Parse(VanillaCharaPrefix + chara.uid);
        }

        private const string VanillaCharaPrefix = "npc_vanilla_";

        /// <summary>
        /// Whether this id names somebody the *game* made, as opposed to somebody this mod staged.
        ///
        /// The same convention as <see cref="MintCharaId"/>, read backwards, and deliberately in
        /// the same place: the observer mints this prefix for every character it finds already in
        /// the world, and the stager binds world-model ids to the characters it creates. So the
        /// prefix is the one durable record of which of the two a bound character is - it survives
        /// a reload with the binding map, where a set built at spawn time would not.
        /// </summary>
        internal static bool IsVanillaMinted(EntityId entity)
        {
            return entity.Value.StartsWith(VanillaCharaPrefix, StringComparison.Ordinal);
        }

        internal Thing ResolveThing(EntityId entity, Card owner)
        {
            if (owner?.things == null || !TryGetUid(entity, out int uid))
            {
                return null;
            }

            return owner.things.Find(uid);
        }

        /// <summary>
        /// Writes the map back into the world so the next load can rebuild it. Called before the
        /// world is serialised; the map is the only place the two id spaces meet, so if it is not
        /// saved the connection between them is lost with the session.
        /// </summary>
        internal void WriteSavedRefs(NarrativeWorldState world)
        {
            if (world == null)
            {
                return;
            }

            world.ExternalRefs.Clear();
            foreach (KeyValuePair<EntityId, int> pair in _entityToUid)
            {
                world.ExternalRefs[pair.Key] = pair.Value.ToString();
            }
        }

        internal void Clear()
        {
            _entityToUid.Clear();
            _uidToEntity.Clear();
        }
    }
}
