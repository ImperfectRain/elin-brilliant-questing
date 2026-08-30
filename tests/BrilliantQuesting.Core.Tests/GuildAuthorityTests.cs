using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-038. A hall in Derwen, a beast on the Wick road, and two players who differ in exactly
    /// one thing: whether they carry the Fighters' card.
    ///
    /// The step's done-when is a comparison between builds, so it is tested that way first. The
    /// rest of these pin what makes it a mechanic rather than a monster quest: nothing is written
    /// per guild, so the same verb commits the Merchants to a failed field; a guild answers only
    /// what its own reading covers, so a Merchants card buys nothing on the road; and no guild is
    /// asked to undo what already happened.
    /// </summary>
    public class GuildAuthorityTests
    {
        [Fact]
        public void TheVerbIsRegisteredAsASocialEndingWithItsOwnProfile()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            Assert.Equal(ActionFamily.Social, registry.Get("invoke_authority").Family);
            Assert.Equal(ProceduralCheckProfiles.GuildStanding, ProceduralCheckProfiles.ForAction("invoke_authority"));

            // An ending in its own situation, ranked with the other endings of that kind: above
            // everything that only stirs a problem, and below the cross-situation resolutions that
            // a seven-choice surface must never drop.
            Assert.Equal(1, OfferPresentation.Rank("invoke_authority"));
            Assert.True(OfferPresentation.Rank("invoke_authority") < OfferPresentation.Rank("persuade"));
        }

        // -- the done-when ------------------------------------------------------------------

        /// <summary>
        /// The step. Same hall, same news, same dice: the member ends the matter through the guild
        /// and the non-member is told by name that he has no standing to ask.
        /// </summary>
        [Fact]
        public void AFightersMemberCanEndTheBeastThroughAuthorityThatANonMemberCannot()
        {
            BeastLab member = BeastLab.Create(fightersRank: 2);
            ActionOutcome taken = member.Run("invoke_authority", member.Hall);

            Assert.True(taken.Succeeded);
            Assert.Equal(TruthState.Superseded, member.Fact(member.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Resolved, member.Situation.Thread.State);
            Assert.Equal("guild_answered", member.Situation.Thread.Resolution);

            BeastLab outsider = BeastLab.Create(fightersRank: 0);
            Availability petition = outsider.Can("invoke_authority", outsider.Hall);

            Assert.False(petition.IsAvailable);
            Assert.Contains("no standing in the Fighters Guild", petition.Reason);
            Assert.Equal(TruthState.True, outsider.Fact(outsider.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, outsider.Situation.Thread.State);
        }

        /// <summary>
        /// And the gate is membership, not odds. With every roll forced to a critical success the
        /// outsider still has nothing to roll - there is no attempt, and the world is untouched.
        /// </summary>
        [Fact]
        public void TheMembershipGateIsAPreconditionAndNotADifficulty()
        {
            BeastLab outsider = BeastLab.Create(fightersRank: 0, outcome: CheckOutcome.CriticalPass);

            ActionOutcome outcome = outsider.Run("invoke_authority", outsider.Hall);

            Assert.Null(outcome.Check);
            Assert.Empty(outcome.Events);
            Assert.Equal(TruthState.True, outsider.Fact(outsider.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, outsider.Situation.Thread.State);
        }

        /// <summary>
        /// What the outsider lost is one route and not the situation. Wickstead is an ordinary
        /// place with ordinary people in it, and three other families are still open to him.
        /// </summary>
        [Fact]
        public void TheOutsiderKeepsEveryRouteThatIsHisOwnHands()
        {
            BeastLab outsider = BeastLab.Create(fightersRank: 0);

            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (EntityId zone in new[] { outsider.Hamlet, outsider.Hall })
            {
                foreach (EntityId target in outsider.Everyone())
                {
                    families.UnionWith(outsider.Actions.AvailableFamilies(outsider.Context(zone, target)));
                }
            }

            Assert.True(families.Count >= 3, "expected 3+ families still open, got " + families.Count);
        }

        // -- the mechanic generalises ---------------------------------------------------------

        /// <summary>
        /// Nothing is written per guild. The same verb, in a different situation, commits the
        /// Merchants to a hamlet's failed field - because their interest table reads a damaged
        /// thing as a contract, not because anybody wrote a merchant route for Ashfen.
        /// </summary>
        [Fact]
        public void TheSameVerbCommitsTheMerchantsToAFailedField()
        {
            FieldLab lab = FieldLab.Create(merchantsRank: 2);

            Availability petition = lab.Can("invoke_authority");
            ActionOutcome taken = lab.Run("invoke_authority");

            Assert.True(petition.IsAvailable);
            Assert.Contains("Merchants Guild", petition.Reason);
            Assert.True(taken.Succeeded);
            Assert.Equal(TruthState.Superseded, lab.World.Knowledge.GetFact(lab.Situation.BlightId).Truth);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("guild_answered", lab.Situation.Thread.Resolution);
        }

        /// <summary>
        /// A guild answers what its own reading covers and nothing else. Toma stands in the same
        /// hall for the Merchants; a player who carries only their card is still told the beast is
        /// the Fighters' business and that he has no standing there.
        /// </summary>
        [Fact]
        public void AMerchantsCardBuysNothingOnTheRoad()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 0, merchantsRank: 3);

            Availability petition = lab.Can("invoke_authority", lab.Hall);

            Assert.False(petition.IsAvailable);
            Assert.Contains("no standing in the Fighters Guild", petition.Reason);
        }

        /// <summary>
        /// No guild is asked to undo the past. Once the exposure is answered the killing is still
        /// on the record and still true, and the verb has nothing left to offer even though the
        /// Fighters read a killing as their business every time.
        /// </summary>
        [Fact]
        public void HistoryIsNotAMatterAGuildIsAskedToPutRight()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2);
            lab.Run("invoke_authority", lab.Hall);

            Availability again = lab.Can("invoke_authority", lab.Hall);

            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.KillingFactId).Truth);
            Assert.False(again.IsAvailable);
            Assert.Contains("no guild here has business of its own", again.Reason);
        }

        /// <summary>
        /// A hall that is not there cannot be asked. The road is where the trouble is and the
        /// guild is what crosses the distance to it, so standing in Wickstead with the card is no
        /// route at all.
        /// </summary>
        [Fact]
        public void ThereIsNoRouteWhereNobodySpeaksForTheGuild()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2);

            Availability petition = lab.Can("invoke_authority", lab.Hamlet);

            Assert.False(petition.IsAvailable);
            Assert.Contains("no guild here", petition.Reason);
        }

        // -- standing -------------------------------------------------------------------------

        /// <summary>
        /// Rank is a threshold and says what it asks. A member of the right guild is still refused
        /// a matter this size until he is somebody in it.
        /// </summary>
        [Fact]
        public void RankBelowWhatTheMatterAsksIsARefusalThatNamesTheGap()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 1);

            Availability petition = lab.Can("invoke_authority", lab.Hall);

            Assert.False(petition.IsAvailable);
            Assert.Contains("rank 1", petition.Reason);
            Assert.Contains("rank 2", petition.Reason);
        }

        /// <summary>
        /// A build that cannot report standing loses the route rather than being handed it. An
        /// unread number is not a good one.
        /// </summary>
        [Fact]
        public void ABuildThatCannotReadGuildStandingHasNoRoute()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 3);
            lab.Vanilla.SetCapability(VanillaCapability.ReadGuildRank, false);

            Availability petition = lab.Can("invoke_authority", lab.Hall);

            Assert.False(petition.IsAvailable);
            Assert.Contains("cannot report guild standing", petition.Reason);
        }

        /// <summary>
        /// Contribution is read and moves the odds; it never opens or closes the route. Two
        /// members of the same rank differ by what they have put in.
        /// </summary>
        [Fact]
        public void ContributionCountsTowardsStandingWithoutBecomingAGate()
        {
            BeastLab bare = BeastLab.Create(fightersRank: 2);
            BeastLab earned = BeastLab.Create(fightersRank: 2, contribution: 75);

            int without = SituationalModifiers
                .GuildAuthority(bare.Context(bare.Hall, EntityId.None), GuildId.Fighters).Delta;
            int with = SituationalModifiers
                .GuildAuthority(earned.Context(earned.Hall, EntityId.None), GuildId.Fighters).Delta;

            Assert.Equal(-2, without);
            Assert.Equal(-5, with);
            Assert.True(bare.Can("invoke_authority", bare.Hall).IsAvailable);
        }

        /// <summary>
        /// Nothing is written back into Elin. The guild's own numbers are vanilla's to move, and a
        /// mod that promoted people quietly would be a second progression disagreeing with the one
        /// the player can see.
        /// </summary>
        [Fact]
        public void TakingOnAMatterMovesNoGuildNumber()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, contribution: 40);

            lab.Run("invoke_authority", lab.Hall);

            Assert.Equal(2, lab.Vanilla.GetGuildRank(GuildId.Fighters));
            Assert.Equal(40, lab.Vanilla.GetGuildContribution(GuildId.Fighters));
        }

        // -- what the asking costs ------------------------------------------------------------

        /// <summary>
        /// A hall that said no will not be asked the same thing again by the same member. The
        /// matter itself is untouched - it is the guild's willingness that was spent.
        /// </summary>
        [Fact]
        public void AnOfficerWhoRefusesIsNotAskedTheSameThingTwice()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.Fail);

            ActionOutcome refused = lab.Run("invoke_authority", lab.Hall);
            Availability again = lab.Can("invoke_authority", lab.Hall);

            Assert.False(refused.Succeeded);
            Assert.Contains(refused.Events, e => e.Type == WorldEventType.RequestDeclined);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.False(again.IsAvailable);
            Assert.Contains("already turned that down", again.Reason);
        }

        /// <summary>
        /// An ordinary no belongs to the officer who gave it. Another officer of the same guild is
        /// a different person to ask.
        /// </summary>
        [Fact]
        public void AnotherOfficerOfTheSameGuildCanStillBeAsked()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.Fail);
            EntityId second = lab.AddFightersOfficer("Wenn");

            lab.Run("invoke_authority", lab.Hall, lab.Situation.FightersOfficerId);

            Assert.False(lab.Can("invoke_authority", lab.Hall, lab.Situation.FightersOfficerId).IsAvailable);
            Assert.True(lab.Can("invoke_authority", lab.Hall, second).IsAvailable);
        }

        /// <summary>
        /// A botched asking is not a wasted turn. The matter passes out of that guild's hands
        /// altogether, so no officer in it will hear it from anybody.
        /// </summary>
        [Fact]
        public void ABotchedAskingClosesTheMatterToTheWholeGuild()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.CriticalFail);
            EntityId second = lab.AddFightersOfficer("Wenn");

            ActionOutcome botched = lab.Run("invoke_authority", lab.Hall, lab.Situation.FightersOfficerId);

            Assert.False(botched.Succeeded);
            Assert.False(lab.Can("invoke_authority", lab.Hall, second).IsAvailable);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.ExposureFactId).Truth);
        }

        /// <summary>
        /// A critical asking commits enough of the hall to finish the job: everything in the
        /// situation this network reads as its own is answered, not only the thing that was
        /// brought up.
        /// </summary>
        [Fact]
        public void ACriticalAskingAnswersEverythingOfTheGuildsOwn()
        {
            BeastLab ordinary = BeastLab.Create(fightersRank: 2);
            EntityId secondTrouble = ordinary.AddSecondExposure();
            ordinary.Run("invoke_authority", ordinary.Hall);

            Assert.Equal(TruthState.Superseded, ordinary.Fact(ordinary.Situation.ExposureFactId).Truth);
            Assert.Equal(TruthState.True, ordinary.Fact(secondTrouble).Truth);
            Assert.Equal(ThreadState.Active, ordinary.Situation.Thread.State);

            BeastLab whole = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.CriticalPass);
            EntityId alsoExposed = whole.AddSecondExposure();
            whole.Run("invoke_authority", whole.Hall);

            Assert.Equal(TruthState.Superseded, whole.Fact(whole.Situation.ExposureFactId).Truth);
            Assert.Equal(TruthState.Superseded, whole.Fact(alsoExposed).Truth);
            Assert.Equal(ThreadState.Resolved, whole.Situation.Thread.State);
        }

        // -- what the guild ends up knowing ---------------------------------------------------

        /// <summary>
        /// Putting a matter to a guild tells the guild, whatever it decides. That is what the
        /// network is for, and it is not conditional on the hall agreeing to act.
        /// </summary>
        [Fact]
        public void TheHallIsToldWhatTheMemberBroughtEvenWhenItRefuses()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.Fail);

            Assert.False(lab.World.Knowledge.Knows(lab.Situation.FightersOfficerId, lab.Situation.ExposureFactId));

            lab.Run("invoke_authority", lab.Hall);

            Assert.True(lab.World.Knowledge.Knows(lab.Situation.FightersOfficerId, lab.Situation.ExposureFactId));
        }

        /// <summary>
        /// And it is told, not shown. Proof stays with whoever holds the object: a hall can be
        /// entirely certain and still have nothing to put in front of a guard.
        /// </summary>
        [Fact]
        public void ProofDoesNotTravelToTheGuild()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.Fail);

            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.ExposureFactId));

            lab.Run("invoke_authority", lab.Hall);

            Assert.True(lab.World.Knowledge.Knows(lab.Situation.FightersOfficerId, lab.Situation.ExposureFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Situation.FightersOfficerId, lab.Situation.ExposureFactId));
        }

        /// <summary>
        /// A member cannot commit a guild to something he does not believe himself. Hearing a
        /// rumour is not bringing the hall a matter.
        /// </summary>
        [Fact]
        public void AMatterTheMemberDoesNotBelieveIsNothingToBring()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2);
            EntityId halfHeard = lab.AddRumouredExposure();

            Availability petition = lab.Can("invoke_authority", lab.Hall, EntityId.None, halfHeard);

            Assert.False(petition.IsAvailable);
            Assert.Contains("do not believe it yourself", petition.Reason);
        }

        // -- persistence ----------------------------------------------------------------------

        /// <summary>
        /// A refusal is history, so it survives a save. Reloading is not a way to ask a hall that
        /// has already said no.
        /// </summary>
        [Fact]
        public void ARefusalSurvivesSaveAndLoad()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2, outcome: CheckOutcome.Fail);
            lab.Run("invoke_authority", lab.Hall);

            string saved = WorldStateSerializer.Save(lab.World);
            lab.Reload(WorldStateSerializer.Load(saved));

            Availability again = lab.Can("invoke_authority", lab.Hall);

            Assert.False(again.IsAvailable);
            Assert.Contains("already turned that down", again.Reason);
        }

        /// <summary>
        /// And an answered matter stays answered, without the ending being written a second time.
        /// </summary>
        [Fact]
        public void AnAnsweredMatterSurvivesSaveAndLoadAndDoesNotEndTwice()
        {
            BeastLab lab = BeastLab.Create(fightersRank: 2);
            lab.Run("invoke_authority", lab.Hall);

            string saved = WorldStateSerializer.Save(lab.World);
            NarrativeWorldState loaded = WorldStateSerializer.Load(saved);
            lab.Reload(loaded);

            NarrativeThread thread = loaded.Threads[0];
            int endings = 0;
            foreach (WorldEvent recorded in loaded.Ledger.OfType(WorldEventType.ThreadResolved))
            {
                endings++;
            }

            Assert.Equal(ThreadState.Resolved, thread.State);
            Assert.Equal("guild_answered", thread.Resolution);
            Assert.Equal(1, endings);
            Assert.False(lab.Can("invoke_authority", lab.Hall).IsAvailable);
        }

        // -- benches --------------------------------------------------------------------------

        /// <summary>
        /// Wickstead, the road and the hall in Derwen, with the player's guild standing as the one
        /// thing a test varies.
        /// </summary>
        private sealed class BeastLab
        {
            private BeastLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public MaraudingBeastSituation Situation { get; private set; }

            private NarrativeThread _reloadedThread;

            public EntityId Hamlet { get; private set; }

            public EntityId Hall { get; private set; }

            public static BeastLab Create(
                int fightersRank,
                int contribution = 0,
                int merchantsRank = 0,
                CheckOutcome outcome = CheckOutcome.Pass)
            {
                BeastLab lab = new BeastLab();
                NarrativeWorldState world = new NarrativeWorldState(38038);
                EntityId player = world.NewId("npc");
                EntityId hamlet = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: 200, zone: hamlet);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 10);
                vanilla.SetGuildRank(GuildId.Fighters, fightersRank);
                vanilla.SetGuildContribution(GuildId.Fighters, contribution);
                vanilla.SetGuildRank(GuildId.Merchants, merchantsRank);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);
                lab.Situation = MaraudingBeastSituation.Create(
                    world, new SandboxStager(vanilla), player, hamlet, vanilla.Now);
                lab.Hamlet = lab.Situation.HamletZoneId;
                lab.Hall = lab.Situation.HallZoneId;

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            /// <summary>A second person in the hall who speaks for the same guild.</summary>
            public EntityId AddFightersOfficer(string name)
            {
                NarrativeNpc officer = World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name)
                {
                    Occupation = "guild officer",
                    Importance = NarrativeImportance.Known
                });
                officer.Roles.Add(AuthorityPolicy.GuildRole);
                officer.Roles.Add(GuildNetworks.FightersRole);
                new SandboxStager(Vanilla).StageCharacter(officer.Id, new CharacterBlueprint(name), Hall);
                return officer.Id;
            }

            /// <summary>
            /// A second live condition the same network reads, so what one asking answers can be
            /// told apart from what a critical one does.
            /// </summary>
            public EntityId AddSecondExposure()
            {
                Fact exposure = new Fact(
                    World.NewId("fact"),
                    Situation.CarterId,
                    FactPredicates.AtRisk,
                    Situation.BeastId,
                    "the barrow itself",
                    TruthState.True);
                World.Knowledge.AddFact(exposure);
                World.Knowledge.Teach(Player, exposure.Id, KnowledgeSource.Witnessed, 1.0, Vanilla.Now, false);
                Situation.Thread.FactIds.Add(exposure.Id);
                return exposure.Id;
            }

            /// <summary>
            /// A second live condition the same network reads, which the player has only half
            /// heard. Added rather than un-taught because a belief is never walked backwards.
            /// </summary>
            public EntityId AddRumouredExposure()
            {
                Fact exposure = new Fact(
                    World.NewId("fact"),
                    Situation.CarterId,
                    FactPredicates.AtRisk,
                    Situation.BeastId,
                    "something else out on the road",
                    TruthState.True);
                World.Knowledge.AddFact(exposure);
                World.Knowledge.Teach(Player, exposure.Id, KnowledgeSource.Hearsay, 0.2, Vanilla.Now, false);
                Situation.Thread.FactIds.Add(exposure.Id);
                return exposure.Id;
            }

            /// <summary>Everybody a petition could be aimed at, plus nobody.</summary>
            public List<EntityId> Everyone()
            {
                return new List<EntityId>
                {
                    Situation.CarterId,
                    Situation.FightersOfficerId,
                    Situation.MerchantsOfficerId,
                    EntityId.None
                };
            }

            /// <summary>
            /// Continues against a world that has been through a save. The situation object is a
            /// staging record and does not survive one; the thread does, and it is what the verb
            /// actually reads.
            /// </summary>
            public void Reload(NarrativeWorldState loaded)
            {
                World = loaded;
                Situation = null;
                _reloadedThread = loaded.Threads[0];
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            public ActionContext Context(EntityId zone, EntityId target, EntityId about = default)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation == null ? _reloadedThread : Situation.Thread,
                    SubjectFact = about
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target = default)
            {
                return Actions.Get(actionId).Perform(Context(zone, target));
            }

            public Availability Can(string actionId, EntityId zone, EntityId target = default, EntityId about = default)
            {
                return Actions.Get(actionId).GetAvailability(Context(zone, target, about));
            }
        }

        /// <summary>
        /// Ashfen, with a Merchants officer standing in it. Deliberately a situation written for
        /// another family entirely: if the same verb answers it, nothing about the guild route is
        /// scenario-specific.
        /// </summary>
        private sealed class FieldLab
        {
            private FieldLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public EntityId Officer { get; private set; }

            public BlightedFieldSituation Situation { get; private set; }

            public static FieldLab Create(int merchantsRank)
            {
                FieldLab lab = new FieldLab();
                NarrativeWorldState world = new NarrativeWorldState(38039);
                EntityId player = world.NewId("npc");
                EntityId hamlet = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, zone: hamlet);
                vanilla.SetGuildRank(GuildId.Merchants, merchantsRank);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = BlightedFieldSituation.Create(world, stager, player, hamlet, vanilla.Now);

                NarrativeNpc officer = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Guilda")
                {
                    Occupation = "guild officer",
                    Importance = NarrativeImportance.Known
                });
                officer.Roles.Add(AuthorityPolicy.GuildRole);
                officer.Roles.Add(GuildNetworks.MerchantsRole);
                stager.StageCharacter(officer.Id, new CharacterBlueprint("Guilda"), hamlet);

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Officer = officer.Id;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public ActionContext Context()
            {
                Vanilla.SetZone(Player, Situation.HamletZoneId);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, Officer)
                {
                    Thread = Situation.Thread
                };
            }

            public ActionOutcome Run(string actionId) => Actions.Get(actionId).Perform(Context());

            public Availability Can(string actionId) => Actions.Get(actionId).GetAvailability(Context());
        }
    }
}
