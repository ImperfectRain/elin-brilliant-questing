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

        /// <summary>
        /// Links a simulation id to a live object.
        ///
        /// <b>The incumbent wins.</b> A uid that already answers to an id keeps answering to that
        /// one: a later id for the same body is recorded as another name that resolves to it, and
        /// never displaces it. Displacing it was how one physical character came to have two
        /// participating identities - the id the world model had been using since the character
        /// was staged stayed in the forward map while every fresh lookup started returning the
        /// uid-derived one, so history pointed at one of them and the live game at the other.
        ///
        /// Preventing the second id from being minted at all is <see cref="CanonicalIdFor"/>'s
        /// job. This is the floor underneath it: even if something does bind twice, the reverse
        /// map stays stable and the answer to "who is this character" stops changing underfoot.
        /// </summary>
        internal void Bind(EntityId entity, int uid)
        {
            if (entity.IsNone || uid == 0)
            {
                return;
            }

            _entityToUid[entity] = uid;
            if (!_uidToEntity.ContainsKey(uid))
            {
                _uidToEntity[uid] = entity;
            }
        }

        /// <summary>
        /// The id this character participates under: the one they already have, or a freshly
        /// minted one bound to them now.
        ///
        /// The single intake for a live character, and the reason there is one: minting first and
        /// asking afterwards is what produced two BQ identities for one Elin uid. Every caller that
        /// enrols, registers, reports on or reads a character goes through here, so a character BQ
        /// staged under an authored id is never met a second time as `npc_vanilla_&lt;uid&gt;`.
        ///
        /// Registers nothing in the world model - that is the caller's decision and stays theirs.
        /// </summary>
        internal EntityId CanonicalIdFor(Chara chara, EntityId playerId)
        {
            if (chara == null)
            {
                return EntityId.None;
            }

            if (TryGetEntity(chara.uid, out EntityId existing))
            {
                return existing;
            }

            EntityId minted = MintCharaId(chara, playerId);
            Bind(minted, chara.uid);
            return minted;
        }

        /// <summary>
        /// The id this character is known by, derived without binding or registering anything.
        ///
        /// The read-only half of <see cref="CanonicalIdFor"/>, and the one the pure reads use: the
        /// Home roll, the party, and the identity diagnostic all have to name somebody the way the
        /// rest of the mod names them, and none of them may enrol anybody in the world model by
        /// asking. Same answer as the intake for anybody already bound; for a character nobody has
        /// met it derives the id they *would* get without claiming it on their behalf.
        /// </summary>
        internal EntityId IdOf(Chara chara, EntityId playerId)
        {
            if (chara == null)
            {
                return EntityId.None;
            }

            return TryGetEntity(chara.uid, out EntityId existing)
                ? existing
                : MintCharaId(chara, playerId);
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

            // Every record, retired aliases included: an alias still names a real piece of history
            // and must keep resolving to the character it was written about.
            foreach (NarrativeNpc npc in world.Registry.AllNpcs.Values)
            {
                if (int.TryParse(npc.VanillaCharaRef, out int uid))
                {
                    Bind(npc.Id, uid);
                }
            }

            // Name recovery second, and only onto a character nobody has claimed. Two saved people
            // who happen to share a staged name would otherwise both recover onto one body and
            // become a duplicate identity that outlives the session - the failure this whole pass
            // exists to make impossible.
            foreach (NarrativeNpc npc in world.Registry.AllNpcs.Values)
            {
                if (!string.IsNullOrEmpty(npc.VanillaCharaRef))
                {
                    continue;
                }

                Chara match = FindStagedChara(npc.Name);
                if (match == null)
                {
                    continue;
                }

                if (TryGetEntity(match.uid, out EntityId already))
                {
                    log?.LogWarning("Not recovering " + npc.Name + " [" + npc.Id + "] onto uid "
                                    + match.uid + ": that character is already " + already + ".");
                    continue;
                }

                Bind(npc.Id, match.uid);
                npc.VanillaCharaRef = match.uid.ToString();
                log?.LogInfo("Recovered binding for " + npc.Name + " from loaded map (uid "
                             + match.uid + ").");
            }

            // A save written before the intake was canonical can still carry two records for one
            // character. Reconcile them into one participating actor, keeping both records so the
            // history written under either id still reads.
            IReadOnlyList<ActorIdentityIntake.Retirement> retired =
                ActorIdentityIntake.Reconcile(world, IsVanillaMinted);
            for (int i = 0; i < retired.Count; i++)
            {
                // Which of the two ids the reverse map happened to be holding depended on the
                // order the save handed them back. Point it at the one reconciliation chose, so a
                // live lookup and the world model agree about who this character is.
                PointAt(retired[i].Canonical);
                log?.LogWarning("Identity intake: " + retired[i]
                                + "; the retired id stays readable in history and stops acting.");
            }
        }

        /// <summary>
        /// Makes this id the one the character's uid resolves to, displacing an incumbent that
        /// reconciliation has just retired.
        ///
        /// The one place <see cref="Bind"/>'s incumbent rule is overridden, and only ever towards
        /// the canonical actor: a retired alias must stop being the answer to "who is standing
        /// here", or the live game and the world model would go on disagreeing after the duplicate
        /// was resolved.
        /// </summary>
        private void PointAt(EntityId canonical)
        {
            if (_entityToUid.TryGetValue(canonical, out int uid))
            {
                _uidToEntity[uid] = canonical;
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
        /// The id this character would be known by, without binding or registering anything.
        ///
        /// One convention, in one place, so that a resident listed by the Home read and the same
        /// resident later seen acting arrive at the same string instead of coming back as two
        /// people.
        ///
        /// <b>Not an intake.</b> This answers what a character *would* be called if BQ had never
        /// met them; it does not answer what they are called, because somebody BQ staged already
        /// has a name and this would give them a second one. A caller that enrols, registers or
        /// binds asks <see cref="CanonicalIdFor"/>. The two pure reads that derive an id without
        /// touching the world model - the Home roll and the companions read - call this only after
        /// <see cref="TryGetEntity"/> has said the character is unbound.
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
