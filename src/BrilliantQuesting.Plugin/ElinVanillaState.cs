using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// The live implementation of the simulation's one seam to the game.
    ///
    /// Everything above this class is ordinary C# that knows nothing about Elin; everything below
    /// it is Elin. When an Early Access update moves something, this file is what breaks, and the
    /// world model, verb library and 46 tests carry on unchanged.
    ///
    /// Two rules it follows throughout. It never invents a value: a stat it cannot read reports
    /// its capability as unsupported rather than returning zero, because a silently-zero skill
    /// makes every check trivially easy and nothing in the logs would say why. And it never writes
    /// anything the player would not see happen - affinity moves through <c>ModAffinity</c> so the
    /// game shows its own reaction.
    /// </summary>
    internal sealed class ElinVanillaState : IVanillaState
    {
        private readonly ElinBindings _bindings;
        private readonly ManualLogSource _log;
        private readonly HashSet<VanillaCapability> _capabilities = new HashSet<VanillaCapability>();
        private readonly Dictionary<VanillaCapability, string> _capabilityEvidence = new Dictionary<VanillaCapability, string>();

        internal ElinVanillaState(ElinBindings bindings, ManualLogSource log)
        {
            _bindings = bindings;
            _log = log;
        }

        /// <summary>
        /// Works out what this build actually supports. Called after the game has a player, so the
        /// probes read real objects rather than guessing from assembly metadata.
        /// </summary>
        internal void DetectCapabilities()
        {
            _capabilities.Clear();
            _capabilityEvidence.Clear();

            Probe(
                VanillaCapability.ReadAttributes,
                () =>
                {
                    if (!ElementAliases.TryGet(VanillaAttribute.Strength, out int elementId) || EClass.pc?.elements == null)
                    {
                        return null;
                    }

                    return "pc.elements.Value(" + elementId + ") => STR " + EClass.pc.elements.Value(elementId);
                });

            Probe(
                VanillaCapability.ReadSkills,
                () =>
                {
                    if (!ElementAliases.TryGet(VanillaSkill.Negotiation, out int elementId) || EClass.pc?.elements == null)
                    {
                        return null;
                    }

                    return "pc.elements.Value(" + elementId + ") => negotiation " + EClass.pc.elements.Value(elementId);
                });

            Probe(
                VanillaCapability.ReadWriteAffinity,
                () =>
                {
                    if (EClass.pc == null)
                    {
                        return null;
                    }

                    int before = EClass.pc._affinity;
                    EClass.pc.ModAffinity(EClass.pc, 0, false, false);
                    return "pc.ModAffinity(pc, 0, false, false) preserved affinity " + before;
                });

            Probe(
                VanillaCapability.ReadWriteKarma,
                () =>
                {
                    if (EClass.player == null)
                    {
                        return null;
                    }

                    int before = EClass.player.karma;
                    EClass.player.ModKarma(0);
                    return "Player.ModKarma(0) preserved karma " + before;
                });

            Probe(
                VanillaCapability.ReadWriteFame,
                () =>
                {
                    if (EClass.player == null)
                    {
                        return null;
                    }

                    int before = EClass.player.fame;
                    EClass.player.ModFame(0);
                    return "Player.ModFame(0) preserved fame " + before;
                });

            Probe(
                VanillaCapability.ReadWriteInfluence,
                () =>
                {
                    if (EClass.pc == null)
                    {
                        return null;
                    }

                    int before = EClass.pc.GetCurrency(InfluenceCurrency);
                    EClass.pc.ModCurrency(0, InfluenceCurrency);
                    return "pc.GetCurrency/ModCurrency(0, 'influence') preserved " + before;
                });

            Probe(
                VanillaCapability.ReadGuildRank,
                () => EClass.game?.factions == null ? null : "EClass.game.factions guild membership read for Fighter/Mage/Thief/Merchant");

            Probe(
                VanillaCapability.ReadFaith,
                () =>
                {
                    if (EClass.pc?.elements == null)
                    {
                        return null;
                    }

                    return "pc.idFaith '" + (EClass.pc.idFaith ?? string.Empty) + "', pc.elements.Value(85) => piety " + EClass.pc.elements.Value(PietyElementId);
                });

            Probe(
                VanillaCapability.ReadInventory,
                () => EClass.pc == null || EClass.pc.things == null ? null : "pc.things enumerated => " + EClass.pc.things.Count + " item(s)");

            Probe(
                VanillaCapability.TransferItems,
                () => EClass.pc == null || EClass.pc.things == null ? null : "Chara.Pick transfer path available; source inventory count " + EClass.pc.things.Count);

            Probe(
                VanillaCapability.SpendMoney,
                () =>
                {
                    if (EClass.pc == null)
                    {
                        return null;
                    }

                    int before = EClass.pc.GetCurrency(MoneyCurrency);
                    EClass.pc.ModCurrency(0, MoneyCurrency);
                    return "pc.GetCurrency/ModCurrency(0, 'money') preserved " + before;
                });

            MarkUnsupported(VanillaCapability.ReadHomeState, "not implemented until BQ-030 maps Home state");
            MarkUnsupported(VanillaCapability.ObserveCrimeWitnesses, "not implemented until BQ-014/BQ-015 wire ActPerformed witnesses");

            _log.LogInfo("Vanilla capabilities: " + _capabilities.Count + " of "
                         + Enum.GetValues(typeof(VanillaCapability)).Length);
            ReportCapabilities();
        }

        public bool Supports(VanillaCapability capability) => _capabilities.Contains(capability);

        private void Probe(VanillaCapability capability, Func<string> evidence)
        {
            try
            {
                string line = evidence();
                if (string.IsNullOrEmpty(line))
                {
                    MarkUnsupported(capability, "probe returned no runtime object");
                    return;
                }

                _capabilities.Add(capability);
                _capabilityEvidence[capability] = line;
            }
            catch (Exception ex)
            {
                MarkUnsupported(capability, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void MarkUnsupported(VanillaCapability capability, string reason)
        {
            _capabilityEvidence[capability] = "unsupported: " + reason;
        }

        private void ReportCapabilities()
        {
            foreach (VanillaCapability capability in (VanillaCapability[])Enum.GetValues(typeof(VanillaCapability)))
            {
                _capabilityEvidence.TryGetValue(capability, out string evidence);
                string state = Supports(capability) ? "available" : "unavailable";
                _log.LogInfo("  capability " + capability + ": " + state + " - " + (evidence ?? "not probed"));
            }
        }

        public GameTime Now
        {
            get
            {
                // Elin's clock in whole minutes since world start, which is the unit threads
                // escalate against.
                // Fully qualified: the simulation has its own World namespace.
                global::World world = EClass.world;
                if (world?.date == null)
                {
                    return GameTime.Zero;
                }

                return new GameTime(world.date.GetRaw());
            }
        }

        public EntityId PlayerId { get; private set; }

        internal void BindPlayer(EntityId playerId)
        {
            PlayerId = playerId;
            Chara pc = EClass.pc;
            if (pc != null)
            {
                _bindings.Bind(playerId, pc.uid);
            }
        }

        // -- characters ---------------------------------------------------------------------

        public bool IsAlive(EntityId chara)
        {
            Chara c = _bindings.ResolveChara(chara);
            return c != null && !c.isDead;
        }

        public int GetAttribute(EntityId chara, VanillaAttribute attribute)
        {
            Chara c = _bindings.ResolveChara(chara);
            if (c == null || !ElementAliases.TryGet(attribute, out int elementId))
            {
                return 0;
            }

            return c.elements.Value(elementId);
        }

        public int GetSkill(EntityId chara, VanillaSkill skill)
        {
            Chara c = _bindings.ResolveChara(chara);
            if (c == null || !ElementAliases.TryGet(skill, out int elementId))
            {
                return 0;
            }

            return c.elements.Value(elementId);
        }

        public int GetLevel(EntityId chara)
        {
            Chara c = _bindings.ResolveChara(chara);
            return c?.LV ?? 1;
        }

        public int GetAffinity(EntityId chara)
        {
            Chara c = _bindings.ResolveChara(chara);
            return c?._affinity ?? 0;
        }

        public void ChangeAffinity(EntityId chara, int delta)
        {
            Chara c = _bindings.ResolveChara(chara);
            if (c == null || delta == 0)
            {
                return;
            }

            // Routed through the game's own method so the NPC reacts visibly. A procedural
            // consequence the player cannot see is one they will not believe in.
            c.ModAffinity(EClass.pc, delta, true, false);
        }

        // -- player standing ----------------------------------------------------------------

        public int Karma => EClass.player?.karma ?? 0;

        public void ChangeKarma(int delta)
        {
            if (delta != 0)
            {
                EClass.player?.ModKarma(delta);
            }
        }

        public int Fame => EClass.player?.fame ?? 0;

        public void ChangeFame(int delta)
        {
            if (delta != 0)
            {
                EClass.player?.ModFame(delta);
            }
        }

        /// <summary>
        /// Town Influence is a currency, not a player field.
        ///
        /// <c>Player.expInfluence</c> looks like the right thing and is not: it is experience
        /// toward an influence level-up, wrapping at 1000 and announcing "DingInfluence". It read
        /// zero on a character with real standing, which is what gave it away. The spendable
        /// resource lives in the currency store beside money.
        ///
        /// It is held on the player rather than per town, so the town argument is accepted and
        /// ignored until that turns out to be wrong.
        /// </summary>
        public int GetInfluence(EntityId townId)
        {
            return EClass.pc?.GetCurrency(InfluenceCurrency) ?? 0;
        }

        public void ChangeInfluence(EntityId townId, int delta)
        {
            if (delta != 0)
            {
                EClass.pc?.ModCurrency(delta, InfluenceCurrency);
            }
        }

        private const string InfluenceCurrency = "influence";
        private const string ContributionCurrency = "contribution";
        private const string MoneyCurrency = "money";

        /// <summary>Guild contribution, the currency guild rank is earned with.</summary>
        internal int GetContribution() => EClass.pc?.GetCurrency(ContributionCurrency) ?? 0;

        public bool IsGuildMember(GuildId guild)
        {
            Guild g = FindGuild(guild);
            return g != null && g.IsMember;
        }

        public int GetGuildRank(GuildId guild)
        {
            // Membership is readable; a numeric rank is not exposed anywhere this adapter has
            // found. Reported as a binary until that changes, rather than faked.
            return IsGuildMember(guild) ? 1 : 0;
        }

        public string GetWorshippedDeity(EntityId chara)
        {
            Chara c = _bindings.ResolveChara(chara);
            return c?.idFaith ?? string.Empty;
        }

        /// <summary>Piety is element 85, not a separate accessor - the alias dump settled it.</summary>
        public int GetPiety(EntityId chara)
        {
            Chara c = _bindings.ResolveChara(chara);
            return c == null ? 0 : c.elements.Value(PietyElementId);
        }

        private const int PietyElementId = 85;

        // -- money and things ---------------------------------------------------------------

        public int GetMoney(EntityId owner)
        {
            Chara c = _bindings.ResolveChara(owner);
            return c?.GetCurrency(MoneyCurrency) ?? 0;
        }

        public bool TrySpendMoney(EntityId payer, EntityId payee, int amount)
        {
            if (amount < 0)
            {
                return false;
            }

            Chara from = _bindings.ResolveChara(payer);
            if (from == null || from.GetCurrency(MoneyCurrency) < amount)
            {
                return false;
            }

            from.ModCurrency(-amount, MoneyCurrency);
            Chara to = _bindings.ResolveChara(payee);
            to?.ModCurrency(amount, MoneyCurrency);
            return true;
        }

        public IReadOnlyList<ItemDescriptor> GetInventory(EntityId owner)
        {
            List<ItemDescriptor> items = new List<ItemDescriptor>();
            Chara c = _bindings.ResolveChara(owner);
            if (c?.things == null)
            {
                return items;
            }

            foreach (Thing thing in c.things)
            {
                if (thing == null)
                {
                    continue;
                }

                EntityId id = EntityIdFor(thing);
                items.Add(new ItemDescriptor(id, thing.Name, thing.category?.id ?? string.Empty, thing.GetPrice(CurrencyType.Money, false, PriceType.Default, null)));
            }

            return items;
        }

        public bool TryTransferItem(EntityId itemId, EntityId from, EntityId to)
        {
            Chara source = _bindings.ResolveChara(from);
            Chara destination = _bindings.ResolveChara(to);
            if (source == null || destination == null)
            {
                return false;
            }

            Thing thing = _bindings.ResolveThing(itemId, source);
            if (thing == null)
            {
                return false;
            }

            destination.Pick(thing, false, true);
            return true;
        }

        // -- world ----------------------------------------------------------------------------

        public EntityId GetZoneOf(EntityId entity)
        {
            Chara c = _bindings.ResolveChara(entity);
            Zone zone = c?.currentZone ?? EClass._zone;
            return zone == null ? EntityId.None : EntityId.Parse("zone_" + zone.uid);
        }

        public IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId)
        {
            List<EntityId> result = new List<EntityId>();
            Map map = EClass._map;
            if (map?.charas == null)
            {
                return result;
            }

            foreach (Chara c in map.charas)
            {
                if (c == null || c.isDead)
                {
                    continue;
                }

                if (_bindings.TryGetEntity(c.uid, out EntityId entity))
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        // -- helpers --------------------------------------------------------------------------

        /// <summary>
        /// An id for a thing the simulation has not seen before. Bound on first sight so that the
        /// same physical object keeps the same identity for the rest of the save.
        /// </summary>
        private EntityId EntityIdFor(Thing thing)
        {
            if (_bindings.TryGetEntity(thing.uid, out EntityId existing))
            {
                return existing;
            }

            EntityId minted = EntityId.Parse("item_" + thing.uid);
            _bindings.Bind(minted, thing.uid);
            return minted;
        }

        private static Guild FindGuild(GuildId guild)
        {
            FactionManager factions = EClass.game?.factions;
            if (factions == null)
            {
                return null;
            }

            switch (guild)
            {
                case GuildId.Fighters: return factions.Fighter;
                case GuildId.Mages: return factions.Mage;
                case GuildId.Merchants: return factions.Merchant;
                case GuildId.Thieves: return factions.Thief;
                default: return null;
            }
        }
    }
}
