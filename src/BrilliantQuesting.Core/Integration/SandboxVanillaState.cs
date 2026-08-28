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
    public sealed class SandboxVanillaState : IVanillaState
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
        }

        private readonly Dictionary<EntityId, CharaState> _charas = new Dictionary<EntityId, CharaState>();
        private readonly Dictionary<EntityId, int> _influence = new Dictionary<EntityId, int>();
        private readonly Dictionary<GuildId, int> _guildRanks = new Dictionary<GuildId, int>();
        private readonly HashSet<VanillaCapability> _capabilities = new HashSet<VanillaCapability>();

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

        public EntityId PlayerId { get; }

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

        public SandboxVanillaState SetZone(EntityId entity, EntityId zone)
        {
            Ensure(entity).Zone = zone;
            return this;
        }

        public void Kill(EntityId chara) => Ensure(chara).Alive = false;

        /// <summary>
        /// Removes an object from the world entirely - burned, eaten, sold out of reach. Evidence
        /// being destroyed is a real move in this game, so the reference implementation has to be
        /// able to express it.
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

        public void ChangeAffinity(EntityId chara, int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteAffinity))
            {
                return;
            }

            CharaState state = Ensure(chara);
            state.Affinity = Clamp(state.Affinity + delta, -200, 1000);
        }

        public void ChangeKarma(int delta)
        {
            if (delta != 0 && Supports(VanillaCapability.ReadWriteKarma))
            {
                Karma = Clamp(Karma + delta, -100, 100);
            }
        }

        public void ChangeFame(int delta)
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

        public void ChangeInfluence(EntityId townId, int delta)
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
        public bool TrySpendMoney(EntityId payer, EntityId payee, int amount)
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
        public bool TryTransferItem(EntityId itemId, EntityId from, EntityId to)
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

        public EntityId GetZoneOf(EntityId entity) => Ensure(entity).Zone;

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
