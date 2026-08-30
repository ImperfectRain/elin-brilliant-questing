using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
    internal sealed class ElinVanillaState : VanillaStateBase, IVanillaState
    {
        private readonly ElinBindings _bindings;
        private readonly ManualLogSource _log;
        private readonly HashSet<VanillaCapability> _capabilities = new HashSet<VanillaCapability>();
        private readonly Dictionary<VanillaCapability, string> _capabilityEvidence = new Dictionary<VanillaCapability, string>();

        private readonly bool _offscreenAbsenceAllowed;

        internal ElinVanillaState(ElinBindings bindings, ManualLogSource log, bool offscreenAbsenceAllowed = false)
        {
            _bindings = bindings;
            _log = log;
            _offscreenAbsenceAllowed = offscreenAbsenceAllowed;
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
                VanillaCapability.DestroyItems,
                () => EClass.pc == null || EClass.pc.things == null ? null : "Thing.Destroy path available; holder inventory read back after the call");

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

            Probe(
                VanillaCapability.ReadHomeState,
                () =>
                {
                    HomeState home = ElinHomeState.Read(_bindings, PlayerId, _log);
                    return home == null ? null : "EClass.Branch => " + home.Describe();
                },
                "no Home on this save, or EClass.Branch could not be read");

            // A write that must not be exercised to be probed: moving somebody into the player's
            // Home is not a no-op the way ModCurrency(0) is. What is checked is that the branch
            // exposes a member this build can call, and the call itself is verified where it
            // happens, by asking the settlement who lives there afterwards.
            Probe(
                VanillaCapability.WriteHomeResidents,
                () =>
                {
                    string member = ElinHomeState.AdmitMemberName(_log);
                    return member == null
                        ? null
                        : "EClass.Branch." + member + "(Chara) resolved; residency confirmed by re-reading the branch after the call";
                },
                "no Home on this save, or its branch exposes no member that takes a Chara");

            // Off unless the player has said otherwise, whatever this build can do. BQ-032 is the
            // one step in the plan that can corrupt a save, and the roadmap's condition for
            // shipping it enabled is an adversarial run on a disposable save - which is a thing a
            // person does, not a thing a probe can find out. Until then a Grade B absence is
            // impossible in game and the situations fall back to Grade A, which writes nothing.
            Probe(
                VanillaCapability.MoveCharaBetweenZones,
                () => _offscreenAbsenceAllowed ? ElinPresence.ResolvedMembers(_log) : null,
                _offscreenAbsenceAllowed
                    ? "this build exposes no member that moves a Chara to a named Zone"
                    : "off-screen absence is disabled in the configuration");

            Probe(
                VanillaCapability.ObserveCrimeWitnesses,
                () =>
                {
                    if (EClass._map?.charas == null || EClass.pc == null)
                    {
                        return null;
                    }

                    return "EVENT.ActPerformed observer uses map.charas, Chara.Dist, Chara.CanSeeLos, sight radius and stealth/perception checks";
                });

            _log.LogInfo("Vanilla capabilities: " + _capabilities.Count + " of "
                         + Enum.GetValues(typeof(VanillaCapability)).Length);
            ReportCapabilities();
        }

        public bool Supports(VanillaCapability capability) => _capabilities.Contains(capability);

        /// <summary>
        /// The largest single step any one consequence may take. Nothing the simulation does moves
        /// standing by more than a handful of points, so a value outside this band is a bug
        /// upstream - an uninitialised field, a runaway loop, an overflow - and applying it would
        /// wreck a save that the player cannot repair. Refused loudly rather than clamped, because
        /// a clamped write hides the bug and still lies to the simulation about what happened.
        /// </summary>
        private const int MaxStandingStep = 1000;

        private bool WithinBand(string what, int delta)
        {
            if (delta >= -MaxStandingStep && delta <= MaxStandingStep)
            {
                return true;
            }

            Refuse("change " + what, "a step of " + delta + " is outside +/-" + MaxStandingStep
                                     + "; treating it as a bug rather than an intention");
            return false;
        }

        /// <summary>Every refused write says so. A write that quietly does nothing is unfindable.</summary>
        private void Refuse(string what, string why)
        {
            _log.LogWarning("Refused to " + what + ": " + why + ".");
        }

        /// <summary>
        /// The mutation policy turned a write down. Same log as every other refusal, because from
        /// the player's side there is no difference between "the game would not" and "the mod
        /// would not": both are a thing that did not happen and has to be findable.
        /// </summary>
        protected override void OnMutationRefused(string message) => _log.LogWarning(message);

        /// <summary>
        /// How far the mod may reach into this character. A character this build cannot resolve
        /// is Unknown rather than ordinary - a stale binding must never be the thing that makes
        /// somebody relocatable.
        /// </summary>
        protected override NarrativeActorClass GetActorClassCore(EntityId chara)
        {
            if (chara == PlayerId)
            {
                return NarrativeActorClass.Player;
            }

            Chara resolved = _bindings.ResolveChara(chara);
            if (resolved == null)
            {
                return NarrativeActorClass.Unknown;
            }

            // Somebody this mod staged. The game made the Chara, but nothing in the game refers to
            // them, so they carry none of the obligations classification exists to protect - and
            // asking Elin whether one of our own refugees is story-critical would answer "not
            // readable" on many builds and quietly close the shelter routes she exists for.
            if (!ElinBindings.IsVanillaMinted(chara))
            {
                return NarrativeActorClass.Generated;
            }

            return ElinActorClasses.Classify(resolved, _log);
        }

        /// <param name="absentReason">
        /// Why an empty probe means "not here" for this capability. Most reads are missing only
        /// when the runtime object is; a Home is also legitimately absent on a save where the
        /// player owns no land, and the log should not call that a failure.
        /// </param>
        private void Probe(VanillaCapability capability, Func<string> evidence, string absentReason = null)
        {
            try
            {
                string line = evidence();
                if (string.IsNullOrEmpty(line))
                {
                    MarkUnsupported(capability, absentReason ?? "probe returned no runtime object");
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

        public override EntityId PlayerId => _playerId;

        private EntityId _playerId;

        internal void BindPlayer(EntityId playerId)
        {
            _playerId = playerId;
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

        protected override void ChangeAffinityCore(EntityId chara, int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteAffinity))
            {
                return;
            }

            Chara c = _bindings.ResolveChara(chara);
            if (c == null || c.isDead)
            {
                Refuse("change affinity", chara + " is not a live character");
                return;
            }

            if (!WithinBand("affinity", delta))
            {
                return;
            }

            // Routed through the game's own method so the NPC reacts visibly. A procedural
            // consequence the player cannot see is one they will not believe in.
            c.ModAffinity(EClass.pc, delta, true, false);
        }

        // -- player standing ----------------------------------------------------------------

        public int Karma => EClass.player?.karma ?? 0;

        protected override void ChangeKarmaCore(int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteKarma) || !WithinBand("karma", delta))
            {
                return;
            }

            EClass.player?.ModKarma(delta);
        }

        public int Fame => EClass.player?.fame ?? 0;

        protected override void ChangeFameCore(int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteFame) || !WithinBand("fame", delta))
            {
                return;
            }

            EClass.player?.ModFame(delta);
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

        protected override void ChangeInfluenceCore(EntityId townId, int delta)
        {
            if (delta == 0 || !Supports(VanillaCapability.ReadWriteInfluence) || !WithinBand("influence", delta))
            {
                return;
            }

            // ModCurrency will not take a balance below zero for us, so clamp the spend to what
            // is actually there rather than asking the game to go negative.
            int held = EClass.pc?.GetCurrency(InfluenceCurrency) ?? 0;
            int applied = delta < 0 && held + delta < 0 ? -held : delta;
            if (applied != 0)
            {
                EClass.pc?.ModCurrency(applied, InfluenceCurrency);
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

        /// <summary>
        /// The member's rank in that guild, as vanilla keeps it.
        ///
        /// This used to report a binary - one for a member, zero for anybody else - which made
        /// every member of every guild the same person to anything that read standing. Vanilla has
        /// the real number on the faction relation and uses it itself for titles, salary and
        /// rank-gated benefits, so BQ reads it rather than inventing a second scale
        /// (`FIX-ELIN-007`).
        ///
        /// Zero for anybody who is not a member, and zero on a build where the faction manager
        /// cannot be reached - which every threshold refuses rather than waves through.
        ///
        /// The relation is read only behind <c>IsMember</c>, which is itself
        /// <c>relation.type == 2</c>: whatever the relation is, vanilla has already dereferenced
        /// it by the time this returns anything but zero.
        /// </summary>
        public int GetGuildRank(GuildId guild)
        {
            Guild g = FindGuild(guild);
            if (g == null || !g.IsMember)
            {
                return 0;
            }

            try
            {
                return g.relation == null ? 0 : Math.Max(0, g.relation.rank);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// What the member has earned in that guild, as its own progression rather than as the
        /// player-wide contribution currency.
        ///
        /// `Card.GetCurrency("contribution")` is one purse for all four guilds, so it cannot say
        /// which guild is owed anything; `FactionRelation.exp` is per-guild and is what vanilla
        /// itself advances a member on.
        /// </summary>
        public int GetGuildContribution(GuildId guild)
        {
            Guild g = FindGuild(guild);
            if (g == null || !g.IsMember)
            {
                return 0;
            }

            try
            {
                return g.relation == null ? 0 : Math.Max(0, g.relation.exp);
            }
            catch (Exception)
            {
                return 0;
            }
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

        /// <summary>
        /// Moves money, or refuses. Both ends are resolved before either is touched.
        ///
        /// The earlier version debited the payer, then resolved the payee, then credited it if it
        /// happened to be there - so a payee who had died, wandered off or was never bound
        /// destroyed the money and the method still reported success. A named payee that cannot be
        /// found is now a refusal. An unnamed one is a deliberate sink, which is how the contract
        /// reads in <c>SandboxVanillaState</c>: a fine or a bribe can leave the world.
        /// </summary>
        protected override bool TrySpendMoneyCore(EntityId payer, EntityId payee, int amount)
        {
            if (amount < 0 || !Supports(VanillaCapability.SpendMoney))
            {
                return false;
            }

            Chara from = _bindings.ResolveChara(payer);
            if (from == null)
            {
                Refuse("spend money", "payer " + payer + " is not bound to a live character");
                return false;
            }

            Chara to = null;
            if (!payee.IsNone)
            {
                to = _bindings.ResolveChara(payee);
                if (to == null)
                {
                    Refuse("spend money", "payee " + payee + " is not bound to a live character");
                    return false;
                }
            }

            if (from.GetCurrency(MoneyCurrency) < amount)
            {
                return false;
            }

            from.ModCurrency(-amount, MoneyCurrency);
            to?.ModCurrency(amount, MoneyCurrency);
            return true;
        }

        private bool _reportedQualitySource;

        /// <summary>
        /// How well an object was made, or zero when this build will not say.
        ///
        /// Zero is the safe answer rather than a guess: a demand with a threshold refuses it, so a
        /// build whose quality cannot be read loses the route that hands over ready-made goods and
        /// keeps the one that works from raw stock. Inventing a number would instead have people
        /// accept things they should not.
        /// </summary>
        private int QualityOf(Thing thing)
        {
            if (!VanillaApiReflection.TryReadQuality(thing, out int quality, out string source))
            {
                return 0;
            }

            if (!_reportedQualitySource)
            {
                _reportedQualitySource = true;
                _log.LogInfo("Reading item quality from Thing." + source + ".");
            }

            return quality;
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

                try
                {
                    EntityId id = EntityIdFor(thing);
                    items.Add(new ItemDescriptor(
                        id,
                        BareName(thing.Name),
                        thing.category?.id ?? string.Empty,
                        thing.GetPrice(CurrencyType.Money, false, PriceType.Default, null),
                        null,
                        QualityOf(thing)));
                }
                catch (Exception ex)
                {
                    // One unreadable object must not empty somebody's inventory as far as the
                    // simulation is concerned - that would silently make a theft impossible.
                    _log.LogWarning("Skipped an unreadable item on " + c.Name + " ("
                                    + ex.GetType().Name + ").");
                }
            }

            return items;
        }

        /// <summary>
        /// Moves a real object between two real inventories, and reports whether it arrived.
        ///
        /// <c>Pick</c> can decline - weight, capacity, a container that will not take the thing -
        /// and says so only by leaving the item where it was. Reporting success without looking
        /// would tell the simulation an item changed hands when it did not, and every fact,
        /// consequence and piece of evidence downstream would be built on that.
        /// </summary>
        protected override bool TryTransferItemCore(EntityId itemId, EntityId from, EntityId to)
        {
            if (!Supports(VanillaCapability.TransferItems) || from == to)
            {
                return false;
            }

            Chara source = _bindings.ResolveChara(from);
            Chara destination = _bindings.ResolveChara(to);
            if (source == null || destination == null)
            {
                Refuse("transfer item", "one end of " + from + " -> " + to + " is not bound to a live character");
                return false;
            }

            Thing thing = _bindings.ResolveThing(itemId, source);
            if (thing == null)
            {
                return false;
            }

            try
            {
                Thing picked = destination.Pick(thing, false, true);
                if (picked != null && picked.uid != thing.uid)
                {
                    _bindings.Bind(itemId, picked.uid);
                    _log.LogInfo("Transfer of " + thing.Name + " merged into destination stack uid "
                                 + picked.uid + "; BQ binding updated from source uid " + thing.uid + ".");
                }
            }
            catch (Exception ex)
            {
                Refuse("transfer item", thing.Name + " could not be picked up (" + ex.GetType().Name + ")");
                return false;
            }

            // Ask the world rather than trusting the call. Pick may stack into a different
            // destination Thing and destroy the source Thing, so the original uid surviving is not
            // required; the current binding must be absent from the source and present at the
            // destination.
            bool arrived = _bindings.ResolveThing(itemId, source) == null
                           && _bindings.ResolveThing(itemId, destination) != null;
            if (!arrived)
            {
                Refuse("transfer item", thing.Name + " did not reach " + destination.Name);
            }

            return arrived;
        }

        /// <summary>
        /// Takes a real object out of the world, and reports whether it actually went.
        ///
        /// <c>Destroy</c> is checked the same way <see cref="TryTransferItem"/> checks
        /// <c>Pick</c>: by asking the holder afterwards rather than by trusting the call. The
        /// simulation revokes every proof that rested on this object the moment this returns true,
        /// and a burned ledger that is somehow still in the pack would leave a case unprovable
        /// while the evidence for it sat in the player's inventory.
        /// </summary>
        protected override bool TryDestroyItemCore(EntityId itemId, EntityId holder)
        {
            if (!Supports(VanillaCapability.DestroyItems))
            {
                return false;
            }

            Chara owner = _bindings.ResolveChara(holder);
            if (owner == null)
            {
                Refuse("destroy item", holder + " is not bound to a live character");
                return false;
            }

            Thing thing = _bindings.ResolveThing(itemId, owner);
            if (thing == null)
            {
                return false;
            }

            string name = thing.Name;
            try
            {
                thing.Destroy();
            }
            catch (Exception ex)
            {
                Refuse("destroy item", name + " could not be destroyed (" + ex.GetType().Name + ")");
                return false;
            }

            bool gone = _bindings.ResolveThing(itemId, owner) == null;
            if (!gone)
            {
                Refuse("destroy item", name + " is still in " + owner.Name + "'s keeping");
            }

            return gone;
        }

        // -- home -----------------------------------------------------------------------------

        /// <summary>
        /// The player's Home, re-read on every call because residents move in, jobs change and the
        /// Home Skill elements drift with the settlement.
        ///
        /// Null when this build could not read a branch and when the player simply has no Home -
        /// the two are the same answer to the simulation, which may not act on a Home it cannot
        /// see. Deliberately not gated on <see cref="VanillaCapability.ReadHomeState"/>, which is
        /// what the probe found at attach: a player who buys land in hour nine has a Home, and a
        /// capability line written before they did must not be what refuses to see it. Callers ask
        /// the direct question by reading the snapshot.
        /// </summary>
        public HomeState GetHomeState()
        {
            return ElinHomeState.Read(_bindings, PlayerId, _log);
        }

        /// <summary>
        /// Moves somebody onto the player's settlement roll, and reports whether they are on it
        /// afterwards. Gated on the capability, unlike the read: this one alters a save, so a
        /// build whose probe found no member to call must not try the call anyway.
        /// </summary>
        protected override bool TryAdmitResidentCore(EntityId chara)
        {
            if (!Supports(VanillaCapability.WriteHomeResidents) || chara.IsNone || chara == PlayerId)
            {
                return false;
            }

            return ElinHomeState.TryAdmit(_bindings, PlayerId, chara, _log);
        }

        // -- whereabouts ------------------------------------------------------------------------

        /// <summary>
        /// Travel, in both directions - the same move whether somebody is being sent away or
        /// brought home. Gated on the capability like the Home write, and for the same reason: this
        /// one alters where a save keeps a person, so a build whose probe found no member to call
        /// must not try the call anyway.
        /// </summary>
        protected override bool MoveToZoneCore(EntityId chara, EntityId zone)
        {
            if (!Supports(VanillaCapability.MoveCharaBetweenZones) || chara == PlayerId)
            {
                return false;
            }

            return ElinPresence.TryMove(_bindings.ResolveChara(chara), zone, _log);
        }

        // -- world ----------------------------------------------------------------------------

        /// <summary>
        /// Where the game keeps this entity. Nobody when it cannot say - never the zone the player
        /// happens to be standing in.
        ///
        /// The fallback used to be <c>EClass._zone</c> for anything unresolved, which reads as "in
        /// front of you" and is the one answer this must never invent: it made every unresolvable
        /// character co-located with the player, so `follow` offered to tail somebody who was not
        /// there and reconciliation would have read an absentee it could not resolve as having come
        /// home. The player keeps the fallback, because their zone is the current one by definition
        /// and they may be asked about before their binding exists.
        /// </summary>
        public EntityId GetZoneOf(EntityId entity)
        {
            Chara c = _bindings.ResolveChara(entity);
            if (c != null)
            {
                return ElinPresence.IdOf(c.currentZone);
            }

            return entity == PlayerId ? ElinPresence.IdOf(EClass._zone) : EntityId.None;
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
        /// <summary>
        /// Elin item names arrive with their own article - "a paper engraved cup". The simulation
        /// composes its own determiners around a bare noun, which is what let the first live run
        /// say "You hand the A Paper Engraved Cup back to Tovar". Stripping it here rather than in
        /// each line of prose keeps one rule in one place, at the seam where game text enters.
        /// </summary>
        private static string BareName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            string[] articles = { "a ", "an ", "the " };
            for (int i = 0; i < articles.Length; i++)
            {
                if (name.Length > articles[i].Length
                    && name.StartsWith(articles[i], StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(articles[i].Length);
                }
            }

            return name;
        }

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
