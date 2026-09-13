using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Plugin;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-017. The half of the live join that is not Elin's.
    ///
    /// What a host adds around <see cref="ProductionCycle"/> is exactly three decisions: that
    /// repeated and overlapping callbacks reach one pass, that a pass which threw costs the rest
    /// of a day instead of duplicating a morning, and that a native outcome the host recorded
    /// itself still closes the opening it took. Those decisions carry no Elin type, so they are
    /// checkable here.
    ///
    /// Nothing here is runtime evidence. No Harmony patch, no ActPerformed, no zone, no clock: a
    /// green file proves the host's own rules, and says nothing about whether Elin calls them.
    /// </summary>
    public class LiveWorldCycleTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Keeper = EntityId.Parse("npc_keeper");
        private static readonly EntityId Suspect = EntityId.Parse("npc_suspect");
        private static readonly EntityId Accuser = EntityId.Parse("npc_accuser");
        private static readonly EntityId Neighbour = EntityId.Parse("npc_neighbour");
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Purse = EntityId.Parse("item_purse");

        /// <summary>
        /// The host fires from several hooks - an act, a completed zone visit, a conversation -
        /// and any of them can arrive repeatedly inside one day. One interval is one pass.
        /// </summary>
        [Fact]
        public void SeveralHooksReachingTheSameIntervalRunOnePass()
        {
            Host host = Host.Create();

            ProductionCyclePass first = host.Advance(4);
            int events = host.World.Ledger.Count;
            int goals = host.Goals();

            ProductionCyclePass again = host.Advance(4);
            ProductionCyclePass third = host.Advance(4);

            Assert.True(first.Ran);
            Assert.False(again.Ran);
            Assert.False(third.Ran);
            Assert.Contains("already been consumed", again.Refusal);

            // Refused rather than merely quiet: the repeats changed nothing at all.
            Assert.Equal(events, host.World.Ledger.Count);
            Assert.Equal(goals, host.Goals());
            Assert.Equal(4, host.World.ProductionCycle.LastConsumedDay);
        }

        /// <summary>
        /// The interval marker comes back off the save, not out of the host, so a reload onto the
        /// same morning does not re-run the morning and the next day is still owed.
        /// </summary>
        [Fact]
        public void AnIntervalConsumedBeforeASaveIsNotRunAgainAfterTheLoad()
        {
            Host host = Host.Create();
            host.Advance(1);
            host.Advance(2);
            host.Advance(3);

            Host reloaded = host.Reload();

            Assert.Equal(3, reloaded.World.ProductionCycle.LastConsumedDay);

            // A fresh host on a restored world knows nothing by itself; the save is what refuses.
            ProductionCyclePass sameMorning = reloaded.Advance(3);
            Assert.False(sameMorning.Ran);

            ProductionCyclePass next = reloaded.Advance(4);
            Assert.True(next.Ran);
        }

        /// <summary>
        /// A pass that throws is the mod's problem, not the player's - and retrying it is not a
        /// retry. Whatever the pass committed before it threw has already happened through the
        /// owners that own it, so the interval closes and the next one is untouched.
        /// </summary>
        [Fact]
        public void APassThatThrowsIsAbsorbedAndItsFinishedHalfIsNotReplayed()
        {
            Host host = Host.Create();

            // A pass that got as far as reading people and then lost the game underneath it.
            host.Adapter.AnswerUntil = 3;
            ProductionCyclePass failed = host.Advance(4);

            Assert.NotNull(failed);
            Assert.False(failed.Ran);
            Assert.Contains("failed partway", failed.Refusal);
            Assert.Single(host.Warnings);
            Assert.Contains("Production cycle failed partway", host.Warnings[0]);

            // Absorbed: the interval it died in is spent, so the next hook does not run the half
            // that finished a second time.
            Assert.Equal(4, host.World.ProductionCycle.LastConsumedDay);
            int events = host.World.Ledger.Count;
            int goals = host.Goals();
            Assert.False(host.Advance(4).Ran);
            Assert.Equal(events, host.World.Ledger.Count);
            Assert.Equal(goals, host.Goals());

            // Reported once per run of failures rather than once per hook.
            Assert.Single(host.Warnings);

            // And the next interval is ordinary work again.
            host.Adapter.AnswerUntil = int.MaxValue;
            Assert.True(host.Advance(5).Ran);
        }

        /// <summary>
        /// The live observer writes an act down when it happens. The cycle must not write it a
        /// second time, and must not offer what the game already took.
        /// </summary>
        [Fact]
        public void ANativeOutcomeTheHostRecordedClosesItsOpeningWithoutBeingRecordedAgain()
        {
            Host host = Host.Create();
            int events = host.World.Ledger.Count;

            host.Cycle.Observed(Lifted(), GameTime.FromDays(2));

            Assert.Equal(events, host.World.Ledger.Count);
            Assert.True(host.World.ProductionCycle.IsSpent("object|" + Purse.Value));

            ConsumedOpening closure = Assert.Single(host.World.ProductionCycle.Openings);
            Assert.Equal(ConsumedOpening.Observed, closure.Because);
            Assert.Equal(Suspect, closure.Holder);

            // An overlapping callback reporting the same outcome closes nothing twice and does
            // not rewrite who did it.
            host.Cycle.Observed(Lifted(EntityId.Parse("npc_someone_else")), GameTime.FromDays(3));
            Assert.Single(host.World.ProductionCycle.Openings);
            Assert.Equal(Suspect, host.World.ProductionCycle.Openings[0].Holder);
        }

        /// <summary>An act with no thing in it closes nothing, and is not an error.</summary>
        [Fact]
        public void AnObservationWithNoThingInItClosesNothing()
        {
            Host host = Host.Create();

            host.Cycle.Observed(null, GameTime.FromDays(2));
            host.Cycle.Observed(
                new ObservedVanillaAction(
                    ObservedVanillaActionKind.Attacked, Suspect, Accuser, EntityId.None, null, Town, "act_hit"),
                GameTime.FromDays(2));

            Assert.Empty(host.World.ProductionCycle.Openings);
        }

        private static ObservedVanillaAction Lifted(EntityId? by = null)
        {
            return new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                by ?? Suspect,
                Keeper,
                Purse,
                "embroidered purse",
                Town,
                "act_steal");
        }

        /// <summary>
        /// Authoritative initial conditions and nothing else. No goal, incident or follow-up is
        /// written after this, so anything a pass produces came out of the production owners.
        /// </summary>
        private sealed class Host
        {
            private Host(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Adapter = new FailingAdapter(vanilla);
                Consequences = new ConsequenceEngine(world, vanilla);
                Consequences.Attach();
                Cycle = new LiveWorldCycle(
                    world,
                    Adapter,
                    new VanillaStyleCheckResolver(vanilla),
                    StandardActions.CreateRegistry(),
                    message => Notes.Add(message),
                    message => Warnings.Add(message));
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public FailingAdapter Adapter { get; }

            public ConsequenceEngine Consequences { get; }

            public LiveWorldCycle Cycle { get; }

            public List<string> Notes { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            public static Host Create(ulong seed = 2117)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
                world.Registry.Add(new NarrativeNpc(Keeper, "Mira") { Occupation = "shopkeeper", HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Suspect, "Bran") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Accuser, "Hald") { Occupation = "reeve", HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Neighbour, "Orvo") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Player, "You") { HomeSiteId = Town });

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Keeper, Suspect, Accuser, Neighbour, Player })
                {
                    vanilla.Define(who, zone: Town, money: 120);
                }

                vanilla.GiveItem(Suspect, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);

                // A shortage where these people live, so a pass has somewhere to reach as well as
                // something to read: a fixture that only ever forms wants never resolves a check,
                // and then nothing is proved about a pass that dies inside one.
                Fact need = new Fact(world.NewId("fact"), Town, FactPredicates.Needs, EntityId.None, "grain");
                world.Knowledge.AddFact(need);
                world.Demands.AddOrUpdate(
                    Town, LocalDemandCategory.Food, 55, GameTime.Zero, GameTime.FromDays(120), need.Id);

                world.Obligations.Add(new SocialObligation(
                    world.NewId("obl"),
                    SocialObligationKind.Favor,
                    Suspect,
                    Accuser,
                    EntityId.None,
                    "makes it good",
                    GameTime.Zero,
                    EntityId.None));

                Fact accusation = new Fact(
                    world.NewId("fact"), Suspect, FactPredicates.Stole, Purse, "a purse",
                    TruthState.True, secrecy: 0);
                accusation.EvidenceIds.Add(Purse);
                world.Knowledge.AddFact(accusation);
                world.Knowledge.Teach(Accuser, accusation.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);
                world.Knowledge.Teach(Suspect, accusation.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);

                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    npc.Sensitivities.PublicEmbarrassment = 0.9;
                    npc.Sensitivities.UnpaidDebt = 0.9;
                    npc.Values.Status.Importance = 0.9;
                }

                return new Host(world, vanilla);
            }

            /// <summary>A new host on the restored world, as a load gives it.</summary>
            public Host Reload()
            {
                NarrativeWorldState restored = WorldStateSerializer.Load(WorldStateSerializer.Save(World));
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Keeper, Suspect, Accuser, Neighbour, Player })
                {
                    vanilla.Define(who, zone: Town, money: 120);
                }

                vanilla.GiveItem(Suspect, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));
                return new Host(restored, vanilla);
            }

            public ProductionCyclePass Advance(long day)
            {
                Vanilla.Now = GameTime.FromDays(day);
                return Cycle.Advance(Vanilla.Now);
            }

            public int Goals()
            {
                int goals = 0;
                foreach (NarrativeNpc actor in World.Registry.Npcs.Values)
                {
                    goals += actor.Goals.Count;
                }

                return goals;
            }
        }

        /// <summary>
        /// An adapter that stops answering partway through a pass.
        ///
        /// The realistic live failure, and the reason the host has to absorb one: arbitration
        /// already catches a verb that threw (BQa-015), so what reaches the host is the layer
        /// underneath - a character Elin has destroyed since the pass started reading, a
        /// capability that went away under a version change. Everything else is the sandbox's
        /// ordinary answer, so the pass is a real pass until the moment it is not.
        /// </summary>
        private sealed class FailingAdapter : IVanillaState
        {
            private readonly SandboxVanillaState _real;
            private int _reads;

            public FailingAdapter(SandboxVanillaState real)
            {
                _real = real;
            }

            /// <summary>Answer this many life-state reads, then stop answering at all.</summary>
            public int AnswerUntil { get; set; } = int.MaxValue;

            public GameTime Now => _real.Now;

            public EntityId PlayerId => _real.PlayerId;

            public bool IsAlive(EntityId chara)
            {
                if (++_reads > AnswerUntil)
                {
                    throw new InvalidOperationException("the character this reads is gone");
                }

                return _real.IsAlive(chara);
            }

            public bool Supports(VanillaCapability capability) => _real.Supports(capability);

            public NarrativeActorClass GetActorClass(EntityId chara) => _real.GetActorClass(chara);

            public NarrativeActorKind GetActorKind(EntityId chara) => _real.GetActorKind(chara);

            public SocialAgency GetSocialAgency(EntityId chara) => _real.GetSocialAgency(chara);

            public CharacterIdentity GetCharacterIdentity(EntityId chara) => _real.GetCharacterIdentity(chara);

            public ActorActivity GetActorActivity(EntityId chara) => _real.GetActorActivity(chara);

            public VanillaLifeState GetLifeState(EntityId chara) => _real.GetLifeState(chara);

            public int GetAttribute(EntityId chara, VanillaAttribute attribute) => _real.GetAttribute(chara, attribute);

            public int GetSkill(EntityId chara, VanillaSkill skill) => _real.GetSkill(chara, skill);

            public int GetLevel(EntityId chara) => _real.GetLevel(chara);

            public int GetAffinity(EntityId chara) => _real.GetAffinity(chara);

            public void ChangeAffinity(EntityId chara, int delta) => _real.ChangeAffinity(chara, delta);

            public int Karma => _real.Karma;

            public void ChangeKarma(int delta) => _real.ChangeKarma(delta);

            public int Fame => _real.Fame;

            public void ChangeFame(int delta) => _real.ChangeFame(delta);

            public int GetInfluence(EntityId townId) => _real.GetInfluence(townId);

            public void ChangeInfluence(EntityId townId, int delta) => _real.ChangeInfluence(townId, delta);

            public bool IsGuildMember(GuildId guild) => _real.IsGuildMember(guild);

            public int GetGuildRank(GuildId guild) => _real.GetGuildRank(guild);

            public int GetGuildContribution(GuildId guild) => _real.GetGuildContribution(guild);

            public string GetWorshippedDeity(EntityId chara) => _real.GetWorshippedDeity(chara);

            public int GetPiety(EntityId chara) => _real.GetPiety(chara);

            public int GetMoney(EntityId owner) => _real.GetMoney(owner);

            public bool TrySpendMoney(EntityId payer, EntityId payee, int amount) => _real.TrySpendMoney(payer, payee, amount);

            public IReadOnlyList<ItemDescriptor> GetInventory(EntityId owner) => _real.GetInventory(owner);

            public bool TryTransferItem(EntityId itemId, EntityId from, EntityId to) => _real.TryTransferItem(itemId, from, to);

            public bool TryDestroyItem(EntityId itemId, EntityId holder) => _real.TryDestroyItem(itemId, holder);

            public HomeState GetHomeState() => _real.GetHomeState();

            public bool TryAdmitResident(EntityId chara) => _real.TryAdmitResident(chara);

            public bool TryRelocate(EntityId chara, EntityId zone) => _real.TryRelocate(chara, zone);

            public IReadOnlyList<EntityId> GetPlayerCompanions() => _real.GetPlayerCompanions();

            public bool TrySendAway(EntityId chara, EntityId zone) => _real.TrySendAway(chara, zone);

            public bool TryBringBack(EntityId chara, EntityId zone) => _real.TryBringBack(chara, zone);

            public EntityId GetZoneOf(EntityId entity) => _real.GetZoneOf(entity);

            public IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId) => _real.GetCharactersInZone(zoneId);

            public VanillaGround InspectGround(EntityId zoneId, int x, int y, int width, int height)
                => _real.InspectGround(zoneId, x, y, width, height);
        }
    }
}
