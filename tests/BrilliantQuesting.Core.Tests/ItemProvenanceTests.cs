using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Continuity;
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
    /// BQ-085. An object carries its history, and the history is the ledger's - not a second
    /// record kept on the thing.
    ///
    /// The step's own test is the last one in the design's synergy chain (`PM §51`): a ring is
    /// stolen, the matter goes quiet, the player turns the ring up much later, and showing it to
    /// somebody who knew it puts the matter back in play. The rest of these pin the boundaries
    /// that make that safe - a stranger learns nothing from being shown it, a settled matter stays
    /// settled, and nothing is matched on coincidence.
    /// </summary>
    public class ItemProvenanceTests
    {
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");

        /// <summary>
        /// The done-when, end to end. Nothing is scripted for it: the theft is the laboratory's
        /// own, the dormancy is the escalation running out, and the reopening comes from the ring
        /// being placed by the person it was taken from.
        /// </summary>
        [Fact]
        public void ShowingARecoveredObjectToSomebodyWhoKnowsItReopensTheMatterMonthsLater()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);

            lab.AdvanceDays(90);
            Assert.Equal(ThreadState.Dormant, lab.Situation.Thread.State);

            // Months later, the player has it. They still know nothing about the theft.
            lab.Vanilla.GiveItem(lab.Player, ring);
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));

            ActionOutcome outcome = lab.Perform("show_item", lab.Situation.VictimId);

            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Single(outcome.Events);
            Assert.Equal(WorldEventType.ObjectRecognized, outcome.Events[0].Type);
            Assert.Contains(ring.Id, outcome.Events[0].Evidence);
            Assert.Contains(lab.World.Ledger.OfType(WorldEventType.ThreadReactivated), e => e.ThreadId == lab.Situation.Thread.Id);
        }

        /// <summary>
        /// The gate, from the other side. Somebody with no route to any of the ring's history
        /// cannot place it, so there is nothing to show them - and the option is not offered
        /// rather than offered and refused.
        /// </summary>
        [Fact]
        public void SomebodyWithNoRouteToTheHistoryCannotPlaceTheObject()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);
            EntityId stranger = AddStranger(lab);

            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);

            Assert.Empty(ItemProvenance.RecognizedBy(lab.World, ring.Id, stranger, lab.Vanilla.Now));

            Availability availability = lab.Actions.Get("show_item").GetAvailability(Context(lab, stranger));
            Assert.False(availability.IsAvailable);
            Assert.Equal(ThreadState.Dormant, lab.Situation.Thread.State);
        }

        /// <summary>
        /// Holding a thing is not knowing it. The player carries the ring for months and still has
        /// no route to any of its history - which is why they need somebody else to place it, and
        /// why carrying evidence around cannot quietly become knowing what it proves.
        /// </summary>
        [Fact]
        public void CarryingAnObjectGrantsNoHistoryWithIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);

            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);

            Assert.NotEmpty(ItemProvenance.Of(lab.World, ring.Id, lab.Vanilla.Now));
            Assert.Empty(ItemProvenance.RecognizedBy(lab.World, ring.Id, lab.Player, lab.Vanilla.Now));
            Assert.NotEmpty(ItemProvenance.RecognizedBy(lab.World, ring.Id, lab.Situation.VictimId, lab.Vanilla.Now));
        }

        /// <summary>
        /// Producing an object is not testimony. The person who recognizes it learns nothing they
        /// did not already have a route to, and neither does anybody standing about - the event
        /// names no claim precisely so that the consequence layer has none to teach.
        /// </summary>
        [Fact]
        public void RecognizingAnObjectTeachesNobodyAnything()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);
            EntityId stranger = AddStranger(lab);

            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);

            IReadOnlyList<EntityId> knownBefore = Believers(lab, lab.Situation.TheftFactId);

            ActionOutcome outcome = lab.Perform("show_item", lab.Situation.VictimId);

            Assert.Empty(outcome.Events[0].Related);
            Assert.Contains(stranger, outcome.Events[0].Witnesses);
            Assert.Equal(knownBefore, Believers(lab, lab.Situation.TheftFactId));
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));
        }

        /// <summary>
        /// A matter that is over is over. The victim still knows the ring perfectly well; there is
        /// simply nothing left for producing it to reopen.
        /// </summary>
        [Fact]
        public void AnObjectFromASettledMatterReopensNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);

            lab.AdvanceDays(90);
            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "returned", lab.Player, lab.Vanilla.Now);
            lab.Vanilla.GiveItem(lab.Player, ring);

            IReadOnlyList<ProvenanceEntry> recognized =
                ItemProvenance.RecognizedBy(lab.World, ring.Id, lab.Situation.VictimId, lab.Vanilla.Now);

            Assert.NotEmpty(recognized);
            Assert.Empty(ItemProvenance.OpenMatters(lab.World, recognized));
            Assert.False(lab.Actions.Get("show_item").GetAvailability(Context(lab, lab.Situation.VictimId)).IsAvailable);
        }

        /// <summary>
        /// A thread is reached only through something recorded. Another matter that happens to be
        /// open in the same place, with the same people, at the same time is still not this
        /// object's matter, and producing the ring must not wake it.
        /// </summary>
        [Fact]
        public void AnUnrelatedOpenMatterInTheSamePlaceIsNotReachedByTheObject()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);

            NarrativeThread neighbouring = new NarrativeThread(lab.World.NewId("thread"), "unrelated", lab.Vanilla.Now)
            {
                State = ThreadState.Dormant
            };
            neighbouring.ParticipantIds.Add(lab.Situation.VictimId);
            neighbouring.SiteIds.Add(lab.Zone);
            lab.World.Threads.Add(neighbouring);

            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);
            lab.Perform("show_item", lab.Situation.VictimId);

            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Equal(ThreadState.Dormant, neighbouring.State);
        }

        /// <summary>
        /// Roles are read off what history recorded and nothing else. The theft is a theft; the
        /// showing that followed it is a citation, because the ledger does not say the object
        /// changed hands there.
        /// </summary>
        [Fact]
        public void RolesAreReadOffTheRecordedActRatherThanInferred()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);

            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);
            lab.Perform("show_item", lab.Situation.VictimId);

            IReadOnlyList<ProvenanceEntry> history = ItemProvenance.Of(lab.World, ring.Id, lab.Vanilla.Now);

            Assert.Equal(ProvenanceRole.Stolen, history[0].Role);
            Assert.Equal(lab.Situation.ThiefId, history[0].Actor);
            Assert.Equal(lab.Situation.VictimId, history[0].Other);
            Assert.True(history[0].AgeInDays >= 90);
            Assert.Equal(ProvenanceRole.Cited, history[history.Count - 1].Role);
            Assert.Equal(WorldEventType.ObjectRecognized, history[history.Count - 1].EventType);
        }

        /// <summary>
        /// "Track only notable objects" needs no notable flag, because nothing is tracked: an
        /// object history never mentioned derives an empty list, and no store grew to say so.
        /// </summary>
        [Fact]
        public void AnObjectHistoryNeverMentionedHasNoProvenance()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor berry = new ItemDescriptor(lab.World.NewId("item"), "berry", "food", 1);
            lab.Vanilla.GiveItem(lab.Player, berry);

            Assert.Empty(ItemProvenance.Of(lab.World, berry.Id, lab.Vanilla.Now));
            Assert.Empty(ItemProvenance.RecognizedBy(lab.World, berry.Id, lab.Situation.VictimId, lab.Vanilla.Now));
        }

        /// <summary>
        /// Derived, never stored. A save round trip carries the events, so the same history comes
        /// back off the reloaded world without provenance having a save shape of its own.
        /// </summary>
        [Fact]
        public void ProvenanceSurvivesASaveBecauseTheLedgerDoes()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);
            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);
            lab.Perform("show_item", lab.Situation.VictimId);

            IReadOnlyList<ProvenanceEntry> before =
                ItemProvenance.RecognizedBy(lab.World, ring.Id, lab.Situation.VictimId, lab.Vanilla.Now);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            IReadOnlyList<ProvenanceEntry> after =
                ItemProvenance.RecognizedBy(reloaded, ring.Id, lab.Situation.VictimId, lab.Vanilla.Now);

            Assert.NotEmpty(before);
            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].EventId, after[i].EventId);
                Assert.Equal(before[i].Role, after[i].Role);
                Assert.Equal(before[i].RecognizedVia, after[i].RecognizedVia);
            }
        }

        /// <summary>
        /// `D011`: reading an object for what it carries needs the object. Knowing where the ring
        /// is does not let the player produce it.
        /// </summary>
        [Fact]
        public void TheObjectHasToBeInHand()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            TakeTheRing(lab);
            lab.AdvanceDays(90);

            Assert.Empty(lab.Vanilla.GetInventory(lab.Player));
            Assert.False(lab.Actions.Get("show_item").GetAvailability(Context(lab, lab.Situation.VictimId)).IsAvailable);
        }

        /// <summary>
        /// Being shown a thing is information, not a kindness. Nothing anybody could farm moves -
        /// giving the ring back is a different verb, and that is where the credit lives.
        /// </summary>
        [Fact]
        public void ProducingAnObjectBuysNoStanding()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ItemDescriptor ring = TakeTheRing(lab);
            lab.AdvanceDays(90);
            lab.Vanilla.GiveItem(lab.Player, ring);

            int affinity = lab.Vanilla.GetAffinity(lab.Situation.VictimId);
            int karma = lab.Vanilla.Karma;
            int fame = lab.Vanilla.Fame;

            lab.Perform("show_item", lab.Situation.VictimId);
            lab.Perform("show_item", lab.Situation.VictimId);

            Assert.Equal(affinity, lab.Vanilla.GetAffinity(lab.Situation.VictimId));
            Assert.Equal(karma, lab.Vanilla.Karma);
            Assert.Equal(fame, lab.Vanilla.Fame);
        }

        /// <summary>Lifts the ring out of the thief's pack before the escalation hides it.</summary>
        private static ItemDescriptor TakeTheRing(TheftLaboratory lab)
        {
            IReadOnlyList<ItemDescriptor> carried = lab.Vanilla.GetInventory(lab.Situation.ThiefId);
            return Assert.Single(carried);
        }

        private static EntityId AddStranger(TheftLaboratory lab)
        {
            lab.World.Registry.Add(new NarrativeNpc(Stranger, "Stranger"));
            lab.Vanilla.Define(Stranger, zone: lab.Zone);
            return Stranger;
        }

        private static ActionContext Context(TheftLaboratory lab, EntityId target)
        {
            ActionContext context = lab.Context(target);
            context.SubjectItem = EntityId.None;
            return context;
        }

        private static IReadOnlyList<EntityId> Believers(TheftLaboratory lab, EntityId factId)
        {
            List<EntityId> believers = new List<EntityId>();
            foreach (KeyValuePair<EntityId, NarrativeNpc> npc in lab.World.Registry.AllNpcs)
            {
                if (lab.World.Knowledge.Knows(npc.Key, factId))
                {
                    believers.Add(npc.Key);
                }
            }

            believers.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return believers;
        }
    }
}
