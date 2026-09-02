using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-118: the player can see everything they have earned that is not money or an item.
    ///
    /// The condition these tests hold is not "a list exists" but that the list is *true*: it shows
    /// what is still held rather than what once happened, it says so without the player having to
    /// be standing in front of the person who owes them, it survives a reload because it is read
    /// from the save rather than kept beside it, and it never reports an option the world cannot
    /// honour.
    /// </summary>
    public class StandingSheetTests
    {
        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// The step's condition. A favour is a stored option, and until now the only place it was
        /// visible was the dialogue node of whoever owed it - so a player who did not already
        /// suspect they were owed something had no way to find out. Asserted from a context that
        /// names somebody else entirely, because "you can see it while talking to the debtor" is
        /// the surface that already existed and is not what this step is for.
        /// </summary>
        [Fact]
        public void AnEarnedFavorIsVisibleWithoutStandingInFrontOfWhoOwesIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Help(lab, lab.Situation.VictimId, 0.7);

            StandingEntry owed = Assert.Single(Of(lab, StandingKind.OwedToYou));

            Assert.Equal(lab.Situation.VictimId, owed.Subject);
            Assert.Equal(Assert.Single(lab.World.Obligations.Records).Id, owed.RecordId);
            Assert.True(owed.Callable);
            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.VictimId), owed.Title);
            Assert.Contains("owes you a favor", owed.Title);

            string text = StandingSheet.Describe(lab.World, lab.Vanilla);
            Assert.Contains("owed to you", text);
            Assert.Contains("owes you a favor", text);
        }

        /// <summary>Nothing has been earned, and the sheet says so rather than inventing lines.</summary>
        [Fact]
        public void AFreshSaveHasNothingToShow()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.Empty(StandingSheet.Entries(lab.World, lab.Vanilla));
            Assert.Contains("nothing earned yet", StandingSheet.Describe(lab.World, lab.Vanilla));
        }

        /// <summary>
        /// The sheet is what is held, not a log of what happened. A spent favour is finished
        /// business and belongs to the Chronicle; leaving it here would turn the one surface the
        /// player checks for what they can spend into something they have to read past.
        /// </summary>
        [Fact]
        public void SpendingAFavorTakesItOffTheSheet()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Help(lab, lab.Situation.VictimId, 0.7);
            Assert.Single(Of(lab, StandingKind.OwedToYou));

            ActionOutcome spent = lab.Perform("call_favor", lab.Situation.VictimId);
            Assert.True(spent.Succeeded, spent.Narration);

            Assert.Empty(Of(lab, StandingKind.OwedToYou));
            Assert.Equal(SocialObligationStatus.Fulfilled, Assert.Single(lab.World.Obligations.Records).Status);
        }

        /// <summary>
        /// Both directions, because the done-when says owed *and owing*. Nothing in play mints a
        /// debt the player carries yet, so this drives the ledger directly - but the sheet is the
        /// surface those records will surface through when a later step writes them, and reading
        /// only one direction would be a hole nobody notices until then.
        /// </summary>
        [Fact]
        public void WhatThePlayerOwesIsListedTooAndIsNeverCallable()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            RecordObligation(lab, SocialObligationKind.Promise, debtor: lab.Player, creditor: lab.Situation.VictimId);

            StandingEntry owing = Assert.Single(Of(lab, StandingKind.YouOwe));

            Assert.Equal(lab.Situation.VictimId, owing.Subject);
            Assert.False(owing.Callable);
            Assert.Contains("you owe", owing.Title);
            Assert.Contains("promise", owing.Title);
            Assert.Empty(Of(lab, StandingKind.OwedToYou));
        }

        // -- access, membership and the game's own numbers ---------------------------------------

        /// <summary>
        /// Talking your way past a locked door is a first-class reward in `BQ-112`'s vocabulary,
        /// and the verb that grants it records the admission as an outcome note and nothing else -
        /// no event, no fact - so the moment the conversation closed, the only trace was a flag on
        /// the site that nothing ever read back to the player. The sheet reads that flag.
        /// </summary>
        [Fact]
        public void TalkingYourWayIntoALockedPlaceShowsAsADoorThatIsOpen()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            NarrativeSite room = lab.World.Registry.Add(
                new NarrativeSite(lab.Zone, "The back room", "private_room") { Restricted = true });

            Assert.Empty(Of(lab, StandingKind.Access));

            lab.Perform("persuade", lab.Situation.VictimId);
            Assert.True(room.Admits(lab.Player));

            StandingEntry access = Assert.Single(Of(lab, StandingKind.Access));
            Assert.Equal(room.Id, access.Subject);
            Assert.Contains("The back room admits you", access.Title);
            Assert.Contains("doors open to you", StandingSheet.Describe(lab.World, lab.Vanilla));
        }

        /// <summary>A place anybody may walk into is not an achievement and is not listed.</summary>
        [Fact]
        public void AnUnlockedPlaceIsNotStanding()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            NarrativeSite market = lab.World.Registry.Add(new NarrativeSite(lab.Zone, "The market", "market"));
            market.Admit(lab.Player);

            Assert.Empty(Of(lab, StandingKind.Access));
        }

        [Fact]
        public void MembershipOfAGeneratedOrganizationIsStanding()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Organization crew = lab.World.Registry.Add(
                new Organization(lab.World.NewId("org"), "The Quiet Hand", "criminal_crew"));

            Assert.Empty(Of(lab, StandingKind.Membership));

            crew.MemberIds.Add(lab.Player);

            StandingEntry membership = Assert.Single(Of(lab, StandingKind.Membership));
            Assert.Equal(crew.Id, membership.Subject);
            Assert.Contains("The Quiet Hand counts you a member", membership.Title);
            Assert.Contains("criminal crew", membership.Detail);
        }

        /// <summary>
        /// `engagement §3` counts Karma, fame and guild contribution in the same reward vocabulary
        /// as a favour, so a sheet that omitted them would answer a narrower question than the
        /// player asked. Read live from the game on every call, never stored, so there is no copy
        /// here to fall out of step with vanilla.
        /// </summary>
        [Fact]
        public void TheGamesOwnStandingNumbersAreReadLive()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Assert.Empty(Of(lab, StandingKind.VanillaStanding));

            lab.Vanilla.ChangeFame(12);
            lab.Vanilla.ChangeKarma(-8);
            lab.Vanilla.SetGuildRank(GuildId.Thieves, 3).SetGuildContribution(GuildId.Thieves, 40);

            List<StandingEntry> standing = Of(lab, StandingKind.VanillaStanding);
            Assert.Contains(standing, e => e.Title == "fame 12");
            Assert.Contains(standing, e => e.Title == "karma -8");
            Assert.Contains(standing, e => e.Title.Contains("Thieves guild, rank 3") && e.Detail.Contains("40"));

            // Not a member of the other three, so they are not standing the player holds.
            Assert.DoesNotContain(standing, e => e.Title.Contains("Mages"));
        }

        /// <summary>
        /// D017 on a surface: a build that cannot answer gets no line, never a zero. A zero
        /// printed next to "everything you have earned" is a claim about the player's standing,
        /// and an unread number is not one.
        /// </summary>
        [Fact]
        public void AStandingNumberTheBuildCannotReportIsAbsentRatherThanZero()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.ChangeFame(9);
            Assert.Contains(Of(lab, StandingKind.VanillaStanding), e => e.Title == "fame 9");

            lab.Vanilla.SetCapability(VanillaCapability.ReadWriteFame, false);

            Assert.DoesNotContain(Of(lab, StandingKind.VanillaStanding), e => e.Title.StartsWith("fame"));
        }

        // -- what the sheet must not do ----------------------------------------------------------

        /// <summary>
        /// D008 on a surface. Nothing writes a record the player did not live through yet, but the
        /// ledger's model already carries grudges and sponsorships, and once background simulation
        /// writes those, a sheet that listed every record naming the player would hand them
        /// somebody else's private reckoning as though they had been told.
        /// </summary>
        [Fact]
        public void AReckoningThePlayerNeverLivedThroughIsWithheld()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // Formed between two other people, about the player, off-screen.
            WorldEvent elsewhere = lab.World.Record(
                WorldEventType.Conversed,
                lab.Situation.VictimId,
                lab.Situation.WitnessId,
                lab.Vanilla.Now,
                0.5,
                lab.Zone);

            lab.World.Obligations.Add(new SocialObligation(
                lab.World.NewId("obl"),
                SocialObligationKind.Grudge,
                lab.Player,
                lab.Situation.WitnessId,
                EntityId.None,
                string.Empty,
                lab.Vanilla.Now,
                elsewhere.Id));

            Assert.Empty(StandingSheet.Entries(lab.World, lab.Vanilla));
        }

        /// <summary>
        /// A favour is only a reward while it can be spent, and nothing closes the obligation
        /// ledger when somebody dies - `BQ-052` inherits threads and leaves debts where they are.
        /// The record stays listed, because it is part of what the player did, and it stops
        /// claiming to be an option the world can honour.
        /// </summary>
        [Fact]
        public void AFavorFromSomebodyDeadIsShownButNotOfferedAsAnOption()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Help(lab, lab.Situation.VictimId, 0.7);
            Assert.True(Assert.Single(Of(lab, StandingKind.OwedToYou)).Callable);

            lab.Vanilla.Kill(lab.Situation.VictimId);

            StandingEntry owed = Assert.Single(Of(lab, StandingKind.OwedToYou));
            Assert.False(owed.Callable);
            Assert.Contains("is dead", owed.Detail);
            Assert.True(Assert.Single(lab.World.Obligations.Records).IsOpen);
        }

        /// <summary>
        /// Off-screen is not gone. An actor the adapter cannot resolve right now answers
        /// <see cref="VanillaLifeState.Unknown"/>, and treating that as death would blank the
        /// sheet every time the player left town.
        /// </summary>
        [Fact]
        public void SomebodyTheAdapterCannotSeeIsNotTreatedAsDead()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // Never defined in the sandbox, so the adapter has no answer about them at all -
            // which is exactly the shape of a character who is simply somewhere else.
            EntityId stranger = lab.World.Registry.Add(new NarrativeNpc(lab.World.NewId("npc"), "Maren")).Id;
            RecordObligation(lab, SocialObligationKind.Favor, debtor: stranger, creditor: lab.Player);

            Assert.Equal(VanillaLifeState.Unknown, lab.Vanilla.GetLifeState(stranger));
            Assert.True(Assert.Single(Of(lab, StandingKind.OwedToYou)).Callable);
        }

        // -- persistence -------------------------------------------------------------------------

        /// <summary>
        /// Derived, not stored (D022): every line is read from state the save already carries, so
        /// the sheet comes back identical after a reload for the same reason the ledger does.
        /// </summary>
        [Fact]
        public void TheSheetReadsBackTheSameAfterAReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.World.Registry.Add(new NarrativeSite(lab.Zone, "The back room", "private_room") { Restricted = true });
            lab.Perform("persuade", lab.Situation.VictimId);
            Help(lab, lab.Situation.VictimId, 0.7);
            lab.Vanilla.ChangeFame(5);

            string before = StandingSheet.Describe(lab.World, lab.Vanilla);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(before, StandingSheet.Describe(reloaded, lab.Vanilla));
            Assert.Contains("owes you a favor", before);
            Assert.Contains("The back room admits you", before);
            Assert.Contains("fame 5", before);
        }

        // -- helpers -----------------------------------------------------------------------------

        private static List<StandingEntry> Of(TheftLaboratory lab, StandingKind kind)
        {
            return StandingSheet.Entries(lab.World, lab.Vanilla).Where(e => e.Kind == kind).ToList();
        }

        /// <summary>A good turn, recorded the way every helping verb in the library records one.</summary>
        private static void Help(TheftLaboratory lab, EntityId who, double magnitude)
        {
            lab.World.Record(
                WorldEventType.Helped,
                lab.Player,
                who,
                lab.Vanilla.Now,
                magnitude,
                lab.Zone,
                threadId: lab.Situation.Thread.Id);
        }

        private static void RecordObligation(
            TheftLaboratory lab,
            SocialObligationKind kind,
            EntityId debtor,
            EntityId creditor)
        {
            WorldEvent source = lab.World.Record(
                WorldEventType.PromiseMade,
                debtor,
                creditor,
                lab.Vanilla.Now,
                0.5,
                lab.Zone,
                threadId: lab.Situation.Thread.Id);

            lab.World.Obligations.Add(new SocialObligation(
                lab.World.NewId("obl"),
                kind,
                debtor,
                creditor,
                EntityId.None,
                string.Empty,
                lab.Vanilla.Now,
                source.Id));
        }
    }
}
