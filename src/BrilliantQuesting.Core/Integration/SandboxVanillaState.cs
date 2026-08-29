using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// A headless stand-in for Elin.
    ///
    /// This is not a mock in the testing sense - it is the reference implementation of the
    /// contract. It lets the whole simulation, the action library and the three-NPC laboratory run
    /// in unit tests with no game process attached, which is what makes the design document's
    /// Gate A and Gate B checkable before a single line of Harmony patching exists.
    ///
    /// The in-game adapter must behave identically from the outside; where it cannot, it should
    /// report the missing <see cref="VanillaCapability"/> rather than lie.
    /// </summary>
    public sealed class SandboxVanillaState : VanillaStateBase, IVanillaState
    {
        private sealed class CharaState
        {
            public readonly Dictionary<VanillaAttribute, int> Attributes = new Dictionary<VanillaAttribute, int>();
            public readonly Dictionary<VanillaSkill, int> Skills = new Dictionary<VanillaSkill, int>();
            public readonly List<ItemDescriptor> Inventory = new List<ItemDescriptor>();
            public int Level = 1;
            public int Affinity;
            public int Money;
            public bool Alive = true;
            public string Deity = string.Empty;
            public int Piety;
            public EntityId Zone;

            /// <summary>
            /// What the laboratory made. Ordinary by default because a headless world is fully
            /// authored - nothing in it is unclassified by accident, and a test that wants an
            /// untellable actor says so with <see cref="SandboxVanillaState.SetActorClass"/>.
            /// </summary>
            public NarrativeActorClass ActorClass = NarrativeActorClass.OrdinaryCitizen;
        }

        private readonly Dictionary<EntityId, CharaState> _charas = new Dictionary<EntityId, CharaState>();
        private readonly Dictionary<EntityId, int> _influence = new Dictionary<EntityId, int>();
        private readonly Dictionary<GuildId, int> _guildRanks = new Dictionary<GuildId, int>();
        private readonly HashSet<VanillaCapability> _capabilities = new HashSet<VanillaCapability>();
        private readonly List<string> _refusals = new List<string>();
        private HomeState _home;

        public SandboxVanillaState(EntityId playerId)
        {
            PlayerId = playerId;
            foreach (VanillaCapability capability in (VanillaCapability[])Enum.GetValues(typeof(VanillaCapability)))
            {
                _capabilities.Add(capability);
            }

            Ensure(playerId);
        }

        public GameTime Now { get; set; } = GameTime.Zero;

        public override EntityId PlayerId { get; }

        /// <summary>
        /// Every write the mutation policy turned down, in order. The headless equivalent of the
        /// adapter's refusal log: a write that quietly did nothing has to be findable.
        /// </summary>
        public IReadOnlyList<string> Refusals => _refusals;

        public int Karma { get; private set; }

        public int Fame { get; private set; }

        /// <summary>Lets a test simulate a build where an integration is unavailable.</summary>
        public void SetCapability(VanillaCapability capability, bool supported)
        {
            if (supported)
            {
                _capabilities.Add(capability);
            }
            else
            {
                _capabilities.Remove(capability);
            }
        }

        public bool Supports(VanillaCapability capability) => _capabilities.Contains(capability);

        public void AdvanceTime(long minutes) => Now = Now.PlusMinutes(minutes);

        public void AdvanceDays(long days) => Now = Now.PlusDays(days);

        // -- authoring helpers -------------------------------------------------------------

        public SandboxVanillaState Define(EntityId chara, int level = 1, int money = 0, EntityId zone = default)
        {
            CharaState state = Ensure(chara);
            state.Level = level;
            state.Money = money;
            state.Zone = zone;
            return this;
        }

        public SandboxVanillaState SetAttribute(EntityId chara, VanillaAttribute attribute, int value)
        {
            Ensure(chara).Attributes[attribute] = value;
            return this;
        }

        public SandboxVanillaState SetSkill(EntityId chara, VanillaSkill skill, int value)
        {
            Ensure(chara).Skills[skill] = value;
            return this;
        }

        public SandboxVanillaState SetAffinity(EntityId chara, int value)
        {
            Ensure(chara).Affinity = value;
            return this;
        }

        public SandboxVanillaState SetGuildRank(GuildId guild, int rank)
        {
            _guildRanks[guild] = rank;
            return this;
        }

        public SandboxVanillaState SetFaith(EntityId chara, string deity, int piety)
        {
            CharaState state = Ensure(chara);
            state.Deity = deity ?? string.Empty;
            state.Piety = piety;
            return this;
        }

        public SandboxVanillaState GiveItem(EntityId owner, ItemDescriptor item)
        {
            Ensure(owner).Inventory.Add(item);
            return this;
        }

        /// <summary>
        /// Says what kind of person this is, and so how far the mod may reach into them. The
        /// laboratory's way of putting a story-critical NPC or an actor this build cannot
        /// classify into a test.
        /// </summary>
        public SandboxVanillaState SetActorClass(EntityId chara, NarrativeActorClass actorClass)
        {
            Ensure(chara).ActorClass = actorClass;
            return this;
        }

        public SandboxVanillaState SetZone(EntityId entity, EntityId zone)
        {
            Ensure(entity).Zone = zone;
            return this;
        }

        /// <summary>
        /// Gives the player a Home. Built through <see cref="HomeStateBuilder"/> so a headless
        /// laboratory expresses "this was never read" the same way the live adapter does.
        /// </summary>
        public SandboxVanillaState SetHome(HomeState home)
        {
            _home = home;
            return this;
        }

        public void Kill(EntityId chara) => Ensure(chara).Alive = false;

        /// <summary>
        /// Removes an object from the world entirely - burned, eaten, sold out of reach. Evidence
        /// being destroyed is a real move in this game, so the reference implementation has to be
        /// able to express it.
        ///
        /// The authoring form, which does not care who was holding it.
        /// <see cref="TryDestroyItem"/> is the contract form, and names a holder.
        /// </summary>
        public void DestroyItem(EntityId itemId)
        {
            foreach (CharaState state in _charas.Values)
            {
                for (int i = state.Inventory.Count - 1; i >= 0; i--)
                {
                    if (state.Inventory[i].Id == itemId)
                    {
                        state.Inventory.RemoveAt(i);
                    }
                }
            }
        }

        // -- IVanillaState ------------------------------------------------------------------

        public bool IsAlive(EntityId chara) => Ensure(chara).Alive;

        public int GetAttribute(EntityId chara, VanillaAttribute attribute)
        {
            Ensure(chara).Attributes.TryGetValue(attribute, out int value);
            return value;
        }

        public int GetSkill(EntityId chara, VanillaSkill skill)
        {
            Ensure(chara).Skills.TryGetValue(skill, out int value);
            return value;
        }

        public int GetLevel(EntityId chara) => Ensure(chara).Level;

        public int GetAffinity(EntityId chara) => Ensure(chara).Affinity;

        protected override void ChangeAffinityCore(EntityId chara, int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteAffinity))
            {
                return;
            }

            CharaState state = Ensure(chara);
            state.Affinity = Clamp(state.Affinity + delta, -200, 1000);
        }

        protected override void ChangeKarmaCore(int delta)
        {
            if (delta != 0 && Supports(VanillaCapability.ReadWriteKarma))
            {
                Karma = Clamp(Karma + delta, -100, 100);
            }
        }

        protected override void ChangeFameCore(int delta)
        {
            if (delta != 0 && Supports(VanillaCapability.ReadWriteFame))
            {
                Fame = Math.Max(0, Fame + delta);
            }
        }

        public int GetInfluence(EntityId townId)
        {
            _influence.TryGetValue(townId, out int value);
            return value;
        }

        protected override void ChangeInfluenceCore(EntityId townId, int delta)
        {
            if (delta != 0 && Supports(VanillaCapability.ReadWriteInfluence))
            {
                _influence[townId] = Math.Max(0, GetInfluence(townId) + delta);
            }
        }

        public bool IsGuildMember(GuildId guild) => GetGuildRank(guild) > 0;

        public int GetGuildRank(GuildId guild)
        {
            _guildRanks.TryGetValue(guild, out int rank);
            return rank;
        }

        public string GetWorshippedDeity(EntityId chara) => Ensure(chara).Deity;

        public int GetPiety(EntityId chara) => Ensure(chara).Piety;

        public int GetMoney(EntityId owner) => Ensure(owner).Money;

        /// <summary>
        /// Money is conserved or the call is refused; it is never half-applied. An unnamed payee
        /// is a deliberate sink - a fine, a tithe, money that leaves the world.
        /// </summary>
        protected override bool TrySpendMoneyCore(EntityId payer, EntityId payee, int amount)
        {
            if (amount < 0 || !Supports(VanillaCapability.SpendMoney))
            {
                return false;
            }

            CharaState from = Ensure(payer);
            CharaState to = payee.IsNone ? null : Ensure(payee);
            if (from == to || from.Money < amount)
            {
                return false;
            }

            from.Money -= amount;
            if (to != null)
            {
                to.Money += amount;
            }

            return true;
        }

        public IReadOnlyList<ItemDescriptor> GetInventory(EntityId owner) => Ensure(owner).Inventory;

        /// <summary>
        /// An item is in exactly one inventory before and after. A transfer that cannot happen
        /// reports false and moves nothing.
        /// </summary>
        protected override bool TryTransferItemCore(EntityId itemId, EntityId from, EntityId to)
        {
            // Both ends must be somebody. An item handed to nobody is a real object deleted from
            // the world, and the fact written about it afterwards would name a person who is not
            // there. The live adapter refuses this by failing to resolve the Chara; the contract
            // says so explicitly rather than relying on that.
            if (!Supports(VanillaCapability.TransferItems)
                || from == to || itemId.IsNone || from.IsNone || to.IsNone)
            {
                return false;
            }

            List<ItemDescriptor> source = Ensure(from).Inventory;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Id == itemId)
                {
                    ItemDescriptor item = source[i];
                    source.RemoveAt(i);
                    Ensure(to).Inventory.Add(item);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The object is gone or the call is refused. Destroying something the named holder is not
        /// carrying reports false and leaves every inventory alone.
        /// </summary>
        protected override bool TryDestroyItemCore(EntityId itemId, EntityId holder)
        {
            if (!Supports(VanillaCapability.DestroyItems) || itemId.IsNone || holder.IsNone)
            {
                return false;
            }

            List<ItemDescriptor> inventory = Ensure(holder).Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Id == itemId)
                {
                    inventory.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Null unless this world was given a Home and the build can read one. A player with no
        /// Home is not a player with an empty one.
        /// </summary>
        public HomeState GetHomeState()
        {
            return Supports(VanillaCapability.ReadHomeState) ? _home : null;
        }

        /// <summary>
        /// Somebody moves in, or the call is refused and the settlement is untouched. Refused for
        /// a build that cannot write residency, for a player with no Home, for a Home whose room
        /// this build will not report, for a full one, and for anybody already living there.
        ///
        /// No job is set. Elin computes what a resident does and what that does to the Home Skill
        /// elements; the reference implementation must not answer a question the game answers, so
        /// the new resident arrives with an unread job and the metrics do not move (decision D018).
        /// </summary>
        protected override bool TryAdmitResidentCore(EntityId chara)
        {
            if (!Supports(VanillaCapability.WriteHomeResidents) || chara.IsNone || chara == PlayerId)
            {
                return false;
            }

            HomeState home = GetHomeState();
            if (home == null || home.FreeCapacity <= 0 || home.IsResident(chara))
            {
                return false;
            }

            // Listed under their id: the reference implementation holds no names, the world model
            // does, and inventing one here would be the same lie an invented job would be.
            _home = home.WithResident(new HomeResident(chara, chara.ToString()));
            return true;
        }

        /// <summary>
        /// Travel, in both directions. The same move whether somebody is being sent away or
        /// brought home, which is what the two contract members are: one primitive, two
        /// permissions.
        ///
        /// Idempotent by construction - a character already in the named zone is left in it and
        /// reported as there - because reconciliation calls this whenever the game has quietly
        /// undone an absence, and a move that only worked the first time would be no enforcement
        /// at all. Nobody is created and nobody is removed: the character that arrives is the
        /// character that left.
        /// </summary>
        protected override bool MoveToZoneCore(EntityId chara, EntityId zone)
        {
            if (!Supports(VanillaCapability.MoveCharaBetweenZones)
                || chara.IsNone || zone.IsNone || chara == PlayerId)
            {
                return false;
            }

            CharaState state = Ensure(chara);
            if (!state.Alive)
            {
                return false;
            }

            state.Zone = zone;
            return true;
        }

        /// <summary>
        /// Where this entity is, or nobody for one the laboratory has never heard of. A world that
        /// invented a zone for a stranger would answer "standing right here" to every question
        /// about somebody who is not in it.
        /// </summary>
        public EntityId GetZoneOf(EntityId entity)
        {
            return _charas.TryGetValue(entity, out CharaState state) ? state.Zone : EntityId.None;
        }

        public IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId)
        {
            List<EntityId> result = new List<EntityId>();
            foreach (KeyValuePair<EntityId, CharaState> pair in _charas)
            {
                if (pair.Value.Alive && pair.Value.Zone == zoneId)
                {
                    result.Add(pair.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// The laboratory knows exactly what it made, so it answers for everybody. The player is
        /// the player; everyone else is what a test said, or an ordinary citizen.
        /// </summary>
        protected override NarrativeActorClass GetActorClassCore(EntityId chara)
        {
            return chara == PlayerId ? NarrativeActorClass.Player : Ensure(chara).ActorClass;
        }

        protected override void OnMutationRefused(string message) => _refusals.Add(message);

        private CharaState Ensure(EntityId chara)
        {
            if (!_charas.TryGetValue(chara, out CharaState state))
            {
                state = new CharaState();
                _charas[chara] = state;
            }

            return state;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
