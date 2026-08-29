using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-032. Somebody is away, and stays away.
    ///
    /// The step's done-when is a durability claim rather than a feature: a Grade B absence has to
    /// survive citizen refresh, zone unload/reload and save/load *with no duplication*. Those three
    /// are the same hostile act from the simulation's point of view - the game putting somebody
    /// back where it last wrote them, behind the mod's back - so they are staged that way here,
    /// against the reference implementation, which is the only place a headless run can stage them
    /// at all. What is deliberately not claimed is that a real Elin build behaves like this; that
    /// is what the adversarial run on a disposable save is for, and until it happens the capability
    /// stays off in game.
    ///
    /// The duplication half is asserted structurally after every hostile act: one record per
    /// person, one person per id, and a registry that never grows. A test that only checked where
    /// somebody was would pass just as happily with two of them.
    /// </summary>
    public class AbsenceTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Fence = EntityId.Parse("npc_fence");
        private static readonly EntityId Guard = EntityId.Parse("npc_guard");
        private static readonly EntityId Protected = EntityId.Parse("npc_story");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Road = EntityId.Parse("zone_road");

        private const string Reason = "gone to ground";

        // -- the departure itself -------------------------------------------------------------

        [Fact]
        public void SendingSomebodyAwayMovesThemAndPutsItOnTheRecord()
        {
            Lab lab = Lab.Create();

            Assert.True(lab.SendFenceAway());

            Assert.Equal(Road, lab.Vanilla.GetZoneOf(Fence));
            Assert.DoesNotContain(Fence, lab.Vanilla.GetCharactersInZone(Town));
            Assert.Contains(Fence, lab.Vanilla.GetCharactersInZone(Road));

            ActorAbsence absence = lab.World.Absences.Of(Fence);
            Assert.Equal(AbsenceGrade.Physical, absence.Grade);
            Assert.Equal(Road, absence.AwayZoneId);
            Assert.Equal(Town, absence.HomeZoneId);
            Assert.Equal(Reason, absence.Reason);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.WentAbsent && e.Actor == Fence);
            lab.AssertNobodyIsInTwoPlaces();
        }

        /// <summary>
        /// The game refused, so nothing is written down. An absence recorded on the strength of a
        /// call that failed is the simulation describing a town the player is not looking at.
        /// </summary>
        [Fact]
        public void ADepartureTheBuildCannotMakeIsNotRecorded()
        {
            Lab lab = Lab.Create();
            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, false);

            Assert.False(lab.SendFenceAway());

            Assert.Equal(0, lab.World.Absences.Count);
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.DoesNotContain(lab.World.Ledger.Events, e => e.Type == WorldEventType.WentAbsent);
        }

        [Fact]
        public void ADepartureWithNowhereToGoIsRefused()
        {
            Lab lab = Lab.Create();

            Assert.False(lab.Absences.TrySendAway(Fence, EntityId.None, Reason, lab.In(4)));
            Assert.False(lab.Absences.TrySendAway(Fence, Town, Reason, lab.In(4)));

            Assert.Equal(0, lab.World.Absences.Count);
        }

        /// <summary>
        /// The BQ-031 guarantee, still standing at the new rung: the two classes the policy
        /// protects cannot be taken off the map, and the refusal is the gate's rather than a rule
        /// this class remembered to apply.
        /// </summary>
        [Fact]
        public void ProtectedAndUnclassifiedActorsCannotBeSentAway()
        {
            Lab lab = Lab.Create();

            lab.Vanilla.SetActorClass(Guard, NarrativeActorClass.Unknown);

            // The precondition answers first, so no departure is ever attempted: impossibility,
            // not a roll that fails.
            Assert.False(lab.Absences.CanLeave(Protected, Road));
            Assert.False(lab.Absences.CanLeave(Guard, Road));
            Assert.False(lab.Absences.TrySendAway(Protected, Road, Reason, lab.In(4)));
            Assert.False(lab.Absences.TrySendAway(Guard, Road, Reason, lab.In(4)));
            Assert.Empty(lab.Vanilla.Refusals);

            // And the gate underneath it refuses the same two writes anyway, loudly, for the paths
            // nobody thought about.
            Assert.False(lab.Vanilla.TrySendAway(Protected, Road));
            Assert.False(lab.Vanilla.TrySendAway(Guard, Road));
            Assert.Equal(2, lab.Vanilla.Refusals.Count);
            Assert.Contains("StoryCritical", lab.Vanilla.Refusals[0]);
            Assert.Contains("Unknown", lab.Vanilla.Refusals[1]);

            Assert.Equal(0, lab.World.Absences.Count);
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Protected));
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Guard));
        }

        [Fact]
        public void AbsenceIsNeverFiledTwiceForTheSamePerson()
        {
            Lab lab = Lab.Create();
            Assert.True(lab.SendFenceAway());

            Assert.False(lab.SendFenceAway());
            Assert.False(lab.Absences.TryWithdrawService(Fence, "shut up shop", lab.In(2)));

            Assert.Equal(1, lab.World.Absences.Count);
            Assert.Equal(AbsenceGrade.Physical, lab.World.Absences.GradeOf(Fence));
        }

        /// <summary>
        /// The one-record rule where it actually lives. The lifecycle asks before it moves
        /// anybody, but the load path does not go through the lifecycle at all, so the ledger
        /// itself has to be the thing that cannot hold two records for one person - otherwise a
        /// save with a duplicated node would come back with somebody away twice.
        /// </summary>
        [Fact]
        public void TheLedgerItselfHoldsOneRecordPerPerson()
        {
            AbsenceLedger ledger = new AbsenceLedger();
            ActorAbsence first = new ActorAbsence(
                Fence, AbsenceGrade.Physical, Reason, GameTime.Zero, GameTime.FromDays(4), Road, Town);

            Assert.True(ledger.TryAdd(first));
            Assert.False(ledger.TryAdd(new ActorAbsence(
                Fence, AbsenceGrade.ServiceOnly, "shut up shop", GameTime.Zero, GameTime.FromDays(9))));
            ledger.Restore(new ActorAbsence(
                Fence, AbsenceGrade.ServiceOnly, "shut up shop", GameTime.Zero, GameTime.FromDays(9)));

            Assert.Equal(1, ledger.Count);
            Assert.Same(first, ledger.Of(Fence));
        }

        /// <summary>A save written before any of this existed opens with nobody away.</summary>
        [Fact]
        public void ASaveWithNoAbsenceNodeLoadsWithNobodyAway()
        {
            Lab lab = Lab.Create();
            string json = WorldStateSerializer.Save(lab.World).Replace("\"absences\"", "\"absencesWasNotAThing\"");

            NarrativeWorldState reloaded = WorldStateSerializer.Load(json);

            Assert.Equal(0, reloaded.Absences.Count);
            Assert.Equal(lab.World.Registry.Npcs.Count, reloaded.Registry.Npcs.Count);
        }

        // -- the hostile half -----------------------------------------------------------------

        /// <summary>
        /// A citizen refresh: the town repopulates and puts its fence back on the street. The
        /// simulation still says she is away, so the next reconciliation makes that true again.
        /// </summary>
        [Fact]
        public void ACitizenRefreshThatPutsThemBackIsUndone()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.TheGamePutsThemBack(Fence);
            AbsenceRound round = lab.Absences.Reconcile();

            Assert.Equal(1, round.Enforced);
            Assert.Equal(Road, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(1, lab.World.Absences.Count);
            lab.AssertNobodyIsInTwoPlaces();
        }

        /// <summary>
        /// The done-when, staged end to end: a departure, then every way the game is known to undo
        /// one, then a save and a load, then the same hostility again on the far side.
        /// </summary>
        [Fact]
        public void AGradeBAbsenceSurvivesRefreshReloadAndSaveLoadWithoutDuplication()
        {
            Lab lab = Lab.Create();
            int people = lab.World.Registry.Npcs.Count;
            Assert.True(lab.SendFenceAway());

            // Citizen refresh, then a zone unloaded and rebuilt on the way back in. Both reach the
            // simulation as the same thing: the game has her standing in the town again.
            foreach (int _ in new[] { 1, 2 })
            {
                lab.TheGamePutsThemBack(Fence);
                Assert.Equal(1, lab.Absences.Reconcile().Enforced);
                Assert.Equal(Road, lab.Vanilla.GetZoneOf(Fence));
                lab.AssertNobodyIsInTwoPlaces();
            }

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            Lab after = lab.Reopened(reloaded);

            ActorAbsence absence = reloaded.Absences.Of(Fence);
            Assert.Equal(AbsenceGrade.Physical, absence.Grade);
            Assert.Equal(Road, absence.AwayZoneId);
            Assert.Equal(Town, absence.HomeZoneId);
            Assert.Equal(Reason, absence.Reason);
            Assert.Equal(lab.World.Absences.Of(Fence).ExpectedReturn, absence.ExpectedReturn);

            // Loading a save is the third hostile act: the game puts everybody where it last wrote
            // them, which for a zone it has rebuilt is the town.
            after.TheGamePutsThemBack(Fence);
            Assert.Equal(1, after.Absences.Reconcile().Enforced);

            Assert.Equal(Road, after.Vanilla.GetZoneOf(Fence));
            Assert.Equal(1, reloaded.Absences.Count);
            Assert.Equal(people, reloaded.Registry.Npcs.Count);
            after.AssertNobodyIsInTwoPlaces();
        }

        /// <summary>
        /// Reconciliation is called from every re-entry the game has, so doing it twice must cost
        /// nothing. A pass that reported work every time would be re-issuing a move on every tick.
        /// </summary>
        [Fact]
        public void ReconcilingAgainDoesNothing()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            for (int i = 0; i < 5; i++)
            {
                AbsenceRound round = lab.Absences.Reconcile();
                Assert.False(round.DidAnything);
            }

            Assert.Equal(Road, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(1, lab.World.Absences.Count);
            lab.AssertNobodyIsInTwoPlaces();
        }

        /// <summary>
        /// An unanswerable question is not evidence. A character the game cannot currently place -
        /// an unloaded zone, a binding not yet recovered - must not be read as having come home,
        /// or every pass would fight an absence that was never undone.
        /// </summary>
        [Fact]
        public void TheGameNotSayingWhereSomebodyIsIsNotEvidenceTheyAreBack()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.SetZone(Fence, EntityId.None);
            AbsenceRound round = lab.Absences.Reconcile();

            Assert.False(round.DidAnything);
            Assert.Equal(AbsenceGrade.Physical, lab.World.Absences.GradeOf(Fence));
        }

        // -- coming back ----------------------------------------------------------------------

        [Fact]
        public void TheyComeBackWhenTheirTimeIsUpAndOnlyOnce()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.AdvanceDays(5);
            AbsenceRound round = lab.Absences.Reconcile();

            Assert.Equal(1, round.Returned);
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(0, lab.World.Absences.Count);
            Assert.False(lab.Absences.Reconcile().DidAnything);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.Returned);
            lab.AssertNobodyIsInTwoPlaces();
        }

        [Fact]
        public void AnIndefiniteAbsenceStaysUntilSomethingEndsIt()
        {
            Lab lab = Lab.Create();
            Assert.True(lab.Absences.TrySendAway(Fence, Road, Reason, ActorAbsence.NoScheduledReturn));

            lab.Vanilla.AdvanceDays(90);
            Assert.False(lab.Absences.Reconcile().DidAnything);
            Assert.Equal(AbsenceGrade.Physical, lab.World.Absences.GradeOf(Fence));

            Assert.True(lab.Absences.TryEnd(Fence, "sent for"));
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(0, lab.World.Absences.Count);
        }

        /// <summary>
        /// The failure this whole design is arranged around. Somebody the mod moved is brought home
        /// even after the game has decided they are a person the mod may not touch - the return is
        /// a withdrawal, not a reach - because the alternative is a villager left in the wrong town
        /// for the rest of the save.
        /// </summary>
        [Fact]
        public void SomebodyWhoseClassificationChangedWhileAwayStillComesHome()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.SetActorClass(Fence, NarrativeActorClass.StoryCritical);
            lab.Vanilla.AdvanceDays(5);

            Assert.Equal(1, lab.Absences.Reconcile().Returned);
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(0, lab.World.Absences.Count);
            Assert.Empty(lab.Vanilla.Refusals);
        }

        /// <summary>
        /// Somebody due home that the mod cannot currently move keeps their record, and the round
        /// says so. Dropping it would leave a person the mod moved with nothing left that remembers
        /// to move them back - which is the one way this step could quietly cost somebody a save.
        /// </summary>
        [Fact]
        public void SomebodyWhoCannotBeBroughtHomeYetKeepsTheirRecordAndIsReported()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, false);
            lab.Vanilla.AdvanceDays(5);
            AbsenceRound stuck = lab.Absences.Reconcile();

            Assert.Equal(1, stuck.Stuck);
            Assert.Equal(0, stuck.Returned);
            Assert.True(stuck.DidAnything);
            Assert.Equal(1, lab.World.Absences.Count);
            Assert.Equal(Road, lab.Vanilla.GetZoneOf(Fence));

            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, true);

            Assert.Equal(1, lab.Absences.Reconcile().Returned);
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.Equal(0, lab.World.Absences.Count);
        }

        /// <summary>
        /// The mod can no longer keep them away and the game has them standing in the market. Say
        /// the smaller true thing - their counter is shut - rather than the larger false one.
        /// </summary>
        [Fact]
        public void AnAbsenceTheModCanNoLongerEnforceIsDemotedRatherThanBelieved()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.SetCapability(VanillaCapability.MoveCharaBetweenZones, false);
            lab.TheGamePutsThemBack(Fence);
            AbsenceRound round = lab.Absences.Reconcile();

            Assert.Equal(1, round.Demoted);
            Assert.Equal(AbsenceGrade.ServiceOnly, lab.World.Absences.GradeOf(Fence));
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.False(lab.World.Absences.IsPhysicallyAbsent(Fence));
        }

        [Fact]
        public void SomebodyWhoDiedWhileAwayStopsBeingAway()
        {
            Lab lab = Lab.Create();
            lab.SendFenceAway();

            lab.Vanilla.Kill(Fence);
            lab.Absences.Reconcile();

            Assert.Equal(0, lab.World.Absences.Count);
            Assert.False(lab.Absences.Reconcile().DidAnything);
        }

        // -- grade A --------------------------------------------------------------------------

        /// <summary>
        /// The safe grade writes nothing into the game at all: she is standing exactly where she
        /// was, and the mutation gate was never asked, because there was nothing to ask about.
        /// </summary>
        [Fact]
        public void WithdrawingAServiceLeavesTheGameAlone()
        {
            Lab lab = Lab.Create();

            Assert.True(lab.Absences.TryWithdrawService(Fence, "shut up shop", lab.In(3)));

            Assert.Equal(AbsenceGrade.ServiceOnly, lab.World.Absences.GradeOf(Fence));
            Assert.Equal(Town, lab.Vanilla.GetZoneOf(Fence));
            Assert.Contains(Fence, lab.Vanilla.GetCharactersInZone(Town));
            Assert.True(lab.Vanilla.IsAlive(Fence));
            Assert.Empty(lab.Vanilla.Refusals);
            Assert.False(lab.Absences.Reconcile().DidAnything);
        }

        /// <summary>
        /// A service NPC the policy will not let the mod move can still stop trading, which is the
        /// point of there being two grades: the canonical missing shopkeeper is available on every
        /// build, and only the physical half waits on the lifecycle proof.
        /// </summary>
        [Fact]
        public void SomebodyWhoCannotBeMovedCanStillShutUpShop()
        {
            Lab lab = Lab.Create();
            lab.Vanilla.SetActorClass(Fence, NarrativeActorClass.UniqueService);

            Assert.False(lab.Absences.CanLeave(Fence, Road));
            Assert.False(lab.Absences.TrySendAway(Fence, Road, Reason, lab.In(3)));
            Assert.True(lab.Absences.TryWithdrawService(Fence, "shut up shop", lab.In(3)));

            Assert.Equal(AbsenceGrade.ServiceOnly, lab.World.Absences.GradeOf(Fence));
        }

        [Fact]
        public void AWithdrawnServiceComesBackOnSchedule()
        {
            Lab lab = Lab.Create();
            lab.Absences.TryWithdrawService(Fence, "shut up shop", lab.In(3));

            lab.Vanilla.AdvanceDays(4);

            Assert.Equal(1, lab.Absences.Reconcile().Returned);
            Assert.Equal(0, lab.World.Absences.Count);
        }

        // -- what an absence actually closes ---------------------------------------------------

        /// <summary>
        /// Grade A has teeth because a role is something somebody does, not something they are:
        /// while her trade is shut she is not a fence, so every verb that needs one refuses in the
        /// same words - and she is still standing there to be talked to, which is the whole
        /// difference between the grades.
        /// </summary>
        [Fact]
        public void AFenceWhoHasShutUpShopIsNotAFenceButIsStillAPerson()
        {
            Lab lab = Lab.Create();
            Assert.True(lab.CanReach("fence"));

            lab.Absences.TryWithdrawService(Fence, "shut up shop", lab.In(3));

            Assert.False(lab.CanReach("fence"));
            Assert.True(lab.Offer("question", Fence).Availability.IsAvailable);

            lab.Absences.TryEnd(Fence, "opened up again");
            Assert.True(lab.CanReach("fence"));
        }

        /// <summary>
        /// A guard who is not taking statements is not an authority, and the report route closes
        /// with them rather than needing its own opinion about absence.
        /// </summary>
        [Fact]
        public void AGuardWhoIsNotOnDutyTakesNoReport()
        {
            Lab lab = Lab.Create();
            ActionContext context = lab.Context(Guard);
            Assert.NotEqual(AuthorityRole.None, AuthorityPolicy.RoleOf(context, Guard));

            lab.Absences.TryWithdrawService(Guard, "off duty", lab.In(1));

            Assert.Equal(AuthorityRole.None, AuthorityPolicy.RoleOf(lab.Context(Guard), Guard));
        }

        /// <summary>
        /// Grade B closes everything Grade A does and one thing more: there is nobody there. The
        /// verbs answer that from one shared precondition rather than each carrying its own idea of
        /// who counts as reachable.
        /// </summary>
        [Fact]
        public void SomebodyWhoHasLeftTownCannotBeDealtWithAtAll()
        {
            Lab lab = Lab.Create();
            Assert.True(lab.Offer("question", Fence).Availability.IsAvailable);

            lab.SendFenceAway();

            Assert.False(lab.Offer("question", Fence).Availability.IsAvailable);
            Assert.False(lab.Offer("pickpocket", Fence).Availability.IsAvailable);
            Assert.False(lab.Offer("rapport", Fence).Availability.IsAvailable);
            Assert.False(lab.CanReach("fence"));
        }

        // -- fixture ---------------------------------------------------------------------------

        /// <summary>
        /// A town, a road out of it, and three people the mutation policy treats differently.
        /// Narrow on purpose: this step is a lifecycle, and a situation staged around it would only
        /// be scenery over the thing being proved.
        /// </summary>
        private sealed class Lab
        {
            private Lab(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Absences = new AbsenceLifecycle(world, vanilla);
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public AbsenceLifecycle Absences { get; }

            public static Lab Create()
            {
                NarrativeWorldState world = new NarrativeWorldState(4242);
                world.Registry.Add(new NarrativeNpc(Player, "Player"));
                world.Registry.Add(new NarrativeNpc(Fence, "Sasha") { Occupation = "receiver" })
                    .Roles.Add(UnderworldPolicy.FenceRole);
                world.Registry.Add(new NarrativeNpc(Guard, "Ovel") { Occupation = "guard" })
                    .Roles.Add(AuthorityPolicy.GuardRole);
                world.Registry.Add(new NarrativeNpc(Protected, "Someone the story needs"));

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                vanilla.Define(Player, zone: Town, money: 200);
                vanilla.Define(Fence, zone: Town, money: 400);
                vanilla.Define(Guard, zone: Town, money: 50);
                vanilla.Define(Protected, zone: Town, money: 50);
                vanilla.SetActorClass(Protected, NarrativeActorClass.StoryCritical);

                // Something worth moving, something worth taking, and something worth asking about,
                // so every route this step can close is open before anybody goes away.
                vanilla.GiveItem(Player, new ItemDescriptor(EntityId.Parse("item_ring"), "silver ring", "jewelry", 300));
                vanilla.GiveItem(Fence, new ItemDescriptor(EntityId.Parse("item_purse"), "purse", "misc", 40));
                vanilla.SetGuildRank(GuildId.Thieves, 1);

                Fact rumour = new Fact(world.NewId("fact"), Protected, FactPredicates.Stole,
                    EntityId.Parse("item_ring"), "silver ring");
                world.Knowledge.AddFact(rumour);
                world.Knowledge.Teach(Fence, rumour.Id, KnowledgeSource.Witnessed, 0.9, vanilla.Now, canProve: false);
                world.Knowledge.Teach(Guard, rumour.Id, KnowledgeSource.Hearsay, 0.4, vanilla.Now, canProve: false);
                return new Lab(world, vanilla);
            }

            /// <summary>The same game, seen by a world that has just been loaded from a save.</summary>
            public Lab Reopened(NarrativeWorldState reloaded) => new Lab(reloaded, Vanilla);

            public GameTime In(long days) => Vanilla.Now.PlusDays(days);

            public bool SendFenceAway() => Absences.TrySendAway(Fence, Road, Reason, In(4));

            /// <summary>
            /// The hostile act, in the one form the simulation ever sees it: the game has put
            /// somebody back where it thinks they belong, without telling anybody. A citizen
            /// refresh, a zone rebuilt on entry and a reloaded save all arrive as exactly this.
            /// </summary>
            public void TheGamePutsThemBack(EntityId who) => Vanilla.SetZone(who, Town);

            public ActionContext Context(EntityId target)
            {
                return new ActionContext(
                    World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target);
            }

            public ActionOffer Offer(string id, EntityId target)
            {
                NarrativeAction action = StandardActions.CreateRegistry().Get(id);
                return new ActionOffer(action, action.GetAvailability(Context(target)));
            }

            /// <summary>Whether the underworld can still find somebody in this town who does that work.</summary>
            public bool CanReach(string actionId) => Offer(actionId, EntityId.None).Availability.IsAvailable;

            /// <summary>
            /// The duplication check, asserted after every hostile act rather than at the end: one
            /// absence per person, one instance of each person across every zone, and a cast that
            /// never grew. Somebody standing in two places, or filed twice, fails here whatever the
            /// zone assertions say.
            /// </summary>
            public void AssertNobodyIsInTwoPlaces()
            {
                List<EntityId> away = World.Absences.Active.Select(a => a.ActorId).ToList();
                Assert.Equal(away.Count, away.Distinct().Count());

                List<EntityId> everywhere = new List<EntityId>();
                everywhere.AddRange(Vanilla.GetCharactersInZone(Town));
                everywhere.AddRange(Vanilla.GetCharactersInZone(Road));
                Assert.Equal(everywhere.Count, everywhere.Distinct().Count());

                foreach (EntityId absent in away)
                {
                    Assert.Single(everywhere, id => id == absent);
                }

                Assert.Equal(4, World.Registry.Npcs.Count);
            }
        }
    }
}
