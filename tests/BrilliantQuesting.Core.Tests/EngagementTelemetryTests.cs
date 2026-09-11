using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
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
    /// BQ-119: a play session reports its engagement profile in the inspector.
    ///
    /// The condition these tests hold is not that five numbers are printed but that each of them
    /// means what `engagement §6` needs it to mean. A situation the player never had a route to is
    /// not one they ignored; standing near a matter is not engaging with it; a matter still open is
    /// not an opportunity they lost; and an ending nobody is recorded as having performed is not
    /// somebody else's doing. They also hold the two properties that keep this debug-only: it
    /// stores nothing, and reading it changes nothing.
    /// </summary>
    public class EngagementTelemetryTests
    {
        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// The step's condition, asserted through the surface it is delivered on: the `why?`
        /// report a developer turns on, which is the only place these counts reach a session.
        /// </summary>
        [Fact]
        public void APlaySessionReportsItsEngagementProfileInTheInspector()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            Engage(lab);

            ActionContext context = lab.Context(lab.Situation.ThiefId);
            string report = NarrativeInspector.Explain(lab.World, lab.Vanilla, lab.Actions, context, lab.Situation.Thread);

            Assert.Contains("how much of what was generated ever reached the player", report);
            Assert.Contains("engagement profile", report);
            Assert.Contains("generated 1; surfaced 1; engaged 1", report);
            Assert.Contains("petty_theft", report);
        }

        /// <summary>
        /// All five counts the step names, on one world holding one of each: a matter nobody told
        /// the player about, one they were shown and let go, one they worked, and one somebody else
        /// closed while they watched.
        /// </summary>
        [Fact]
        public void TheProfileCountsGeneratedSurfacedEngagedIgnoredAndResolvedByOthers()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            Engage(lab);

            NarrativeThread unseen = Matter(lab, "unseen_matter");
            NarrativeThread ignored = Matter(lab, "ignored_matter");
            NarrativeThread elsewhere = Matter(lab, "handled_by_others");
            Tell(lab, ignored);
            Tell(lab, elsewhere);
            ThreadResolution.Resolve(lab.World, ignored, "quietly lapsed", lab.Situation.VictimId, lab.Vanilla.Now);
            ThreadResolution.Resolve(lab.World, elsewhere, "recovered", lab.Situation.WitnessId, lab.Vanilla.Now);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(4, profile.Generated);
            Assert.Equal(3, profile.Surfaced);
            Assert.Equal(1, profile.Engaged);
            Assert.Equal(2, profile.Ignored);
            Assert.Equal(2, profile.ResolvedByOthers);
            Assert.Equal(1, profile.Unseen);
            Assert.Equal(2, profile.Open);
            Assert.False(Entry(profile, unseen.Id).Encountered);
            Assert.True(Entry(profile, unseen.Id).Open);
        }

        // -- what surfacing is -------------------------------------------------------------------

        /// <summary>
        /// `PettyTheftSituation` is explicit that the player knows nothing at all, so a fresh save
        /// is the baseline this whole reading rests on: generated, and not yet anybody's business.
        /// </summary>
        [Fact]
        public void AMatterThePlayerHasNoRouteToIsGeneratedAndNotSurfaced()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(1, profile.Generated);
            Assert.Equal(0, profile.Surfaced);
            Assert.Equal(1, profile.Unseen);
            Assert.Equal(0, profile.Ignored);
            Assert.Contains("not surfaced: the player holds none of its claims", Entry(profile, lab.Situation.Thread.Id).Evidence);
            Assert.Contains("never surfaced", EngagementTelemetry.Describe(lab.World, lab.Vanilla));
        }

        /// <summary>
        /// Believing one of its claims is the encounter rule BQ-101 and BQ-102 already read, so
        /// "surfaced" here means what it means to the director rather than a second definition.
        /// </summary>
        [Fact]
        public void BelievingOneOfItsClaimsIsSurfacing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);

            EngagementEntry entry = Entry(EngagementTelemetry.Read(lab.World, lab.Vanilla), lab.Situation.Thread.Id);

            Assert.True(entry.Encountered);
            Assert.False(entry.Engaged);
            Assert.Contains("the player believes " + lab.Situation.TheftFactId.Value, entry.Evidence);
        }

        /// <summary>
        /// A consequence that walked into the room reached the player whether or not it taught them
        /// anything - the attention budget already spends exposure on it - so a matter that arrived
        /// in front of them has surfaced even while they still hold none of its claims.
        /// </summary>
        [Fact]
        public void AConsequenceThatArrivedWhereThePlayerStoodIsSurfacingWithoutTeachingThem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ConsequenceArrivals arrivals = new ConsequenceArrivals(lab.World, lab.Vanilla);

            ConsequenceArrivalResult arrival = arrivals.TryBringToPlayer(
                lab.Situation.Thread,
                lab.Situation.VictimId,
                lab.Player,
                lab.Vanilla.Now,
                "victim_asks_the_player");

            Assert.True(arrival.DidArrive, arrival.Reason);
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));

            EngagementEntry entry = Entry(EngagementTelemetry.Read(lab.World, lab.Vanilla), lab.Situation.Thread.Id);

            Assert.True(entry.Encountered);
            Assert.False(entry.Engaged);
            Assert.Contains("a consequence arrived where the player was", entry.Evidence);
        }

        /// <summary>
        /// Being visited while away is not being shown something. The arrival layer records the
        /// player's position at the time for exactly this reason, and a profile that read the
        /// arrival alone would count a caller at an empty house as an opportunity refused.
        /// </summary>
        [Fact]
        public void AVisitorWhoCameWhileThePlayerWasElsewhereHasNotSurfacedAnything()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId home = lab.World.NewId("zone");
            lab.World.Registry.Add(new NarrativeSite(home, "The smallholding", "home"));
            lab.Vanilla.SetHome(new HomeStateBuilder(home, "The smallholding").WithCapacity(4).Build());

            ConsequenceArrivalResult arrival = new ConsequenceArrivals(lab.World, lab.Vanilla).TryBringToHome(
                lab.Situation.Thread,
                lab.Situation.VictimId,
                EntityId.None,
                lab.Vanilla.Now,
                "victim_calls_at_the_house");

            Assert.True(arrival.DidArrive, arrival.Reason);
            Assert.DoesNotContain(ConsequenceArrivals.PlayerPresentTag, arrival.RecordedEvent.Tags);

            Assert.False(Entry(EngagementTelemetry.Read(lab.World, lab.Vanilla), lab.Situation.Thread.Id).Encountered);
        }

        // -- what engagement is ------------------------------------------------------------------

        /// <summary>
        /// The thread's own attribution rule, the same one the autonomy pass reads to decide
        /// whether the player has a matter in hand. A verb that recorded which matter it belonged
        /// to is engagement; anything else the player did that day is their own business.
        /// </summary>
        [Fact]
        public void OnlyActsTheMatterNamesCountAsEngagement()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);

            lab.World.Record(
                WorldEventType.Conversed,
                lab.Player,
                lab.Situation.WitnessId,
                lab.Vanilla.Now,
                0.2,
                lab.Zone);

            Assert.Equal(0, Entry(EngagementTelemetry.Read(lab.World, lab.Vanilla), lab.Situation.Thread.Id).PlayerActs);

            Engage(lab);

            EngagementEntry entry = Entry(EngagementTelemetry.Read(lab.World, lab.Vanilla), lab.Situation.Thread.Id);
            Assert.True(entry.Engaged);
            Assert.Equal(1, entry.PlayerActs);
            Assert.Contains("acts attributed to the player: 1", entry.Evidence);
        }

        /// <summary>Somebody else's act inside the matter is not the player's engagement.</summary>
        [Fact]
        public void AnActSomebodyElsePerformedInsideTheMatterIsNotEngagement()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);

            lab.World.Record(
                WorldEventType.AccusationMade,
                lab.Situation.VictimId,
                lab.Situation.ThiefId,
                lab.Vanilla.Now,
                0.5,
                lab.Zone,
                threadId: lab.Situation.Thread.Id);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(0, profile.Engaged);
            Assert.Equal(1, profile.Surfaced);
        }

        // -- ignored, and what is not ignored ----------------------------------------------------

        /// <summary>
        /// `engagement §6` question 6 says the player may ignore everything and lose nothing but
        /// opportunity. A matter still standing in front of them is that entitlement being
        /// exercised, not an opportunity already gone, so it is counted apart from the ones whose
        /// chance has actually passed.
        /// </summary>
        [Fact]
        public void AMatterStillOpenIsAwaitingThePlayerRatherThanIgnored()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(0, profile.Ignored);
            Assert.Equal(1, profile.Awaiting);
            Assert.True(Entry(profile, lab.Situation.Thread.Id).Open);
            Assert.Contains("surfaced, awaiting the player", EngagementTelemetry.Describe(lab.World, lab.Vanilla));
        }

        /// <summary>Shown, never touched, and over: the one the engagement test wants counted.</summary>
        [Fact]
        public void AMatterShownAndClosedWithoutThePlayerIsIgnored()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "recovered", lab.Situation.VictimId, lab.Vanilla.Now);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(1, profile.Ignored);
            Assert.Equal(0, profile.Awaiting);
            Assert.Equal(1, profile.ResolvedByOthers);
            Assert.Contains("ignored", EngagementTelemetry.Describe(lab.World, lab.Vanilla));
        }

        /// <summary>A matter the player worked is not ignored, whoever ended up closing it.</summary>
        [Fact]
        public void AMatterThePlayerWorkedIsNotIgnoredEvenWhenSomebodyElseEndsIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            Engage(lab);
            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "recovered", lab.Situation.VictimId, lab.Vanilla.Now);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(0, profile.Ignored);
            Assert.Equal(1, profile.Engaged);
            Assert.Equal(1, profile.ResolvedByOthers);
        }

        // -- who ended it ------------------------------------------------------------------------

        [Fact]
        public void AnEndingIsAttributedToWhoeverTheClosingEventNames()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            NarrativeThread theirs = Matter(lab, "handled_by_others");
            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "recovered by you", lab.Player, lab.Vanilla.Now);
            ThreadResolution.Resolve(lab.World, theirs, "settled between them", lab.Situation.VictimId, lab.Vanilla.Now);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(1, profile.ResolvedByPlayer);
            Assert.Equal(1, profile.ResolvedByOthers);
            Assert.Equal(EngagementEnding.Player, Entry(profile, lab.Situation.Thread.Id).Ending);
            Assert.Equal(lab.Situation.VictimId, Entry(profile, theirs.Id).EndedBy);
            Assert.Contains("ended by " + lab.World.Registry.NameOf(lab.Situation.VictimId), Entry(profile, theirs.Id).Evidence);
        }

        /// <summary>
        /// `D017` on a count. An ending with nobody on the record is not evidence that somebody else
        /// dealt with it, and folding it into "resolved by others" would inflate the one number the
        /// engagement test reads most with endings nobody performed.
        /// </summary>
        [Fact]
        public void AnEndingWithNoAuthorOnTheRecordIsNotCountedAsSomebodyElsesDoing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "lapsed", EntityId.None, lab.Vanilla.Now);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(0, profile.ResolvedByOthers);
            Assert.Equal(1, profile.ResolvedUnattributed);
            Assert.Equal(EngagementEnding.Unattributed, Entry(profile, lab.Situation.Thread.Id).Ending);
            Assert.Contains("resolved with no author on the record", Entry(profile, lab.Situation.Thread.Id).Evidence);
        }

        /// <summary>
        /// A quarantined matter stopped being playable without anybody resolving it, and reporting
        /// that as an ending somebody earned would credit a repair pass with a resolution.
        /// </summary>
        [Fact]
        public void AQuarantinedMatterIsWithdrawnRatherThanResolvedByAnybody()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            foreach (EntityId participant in lab.Situation.Thread.ParticipantIds.ToArray())
            {
                lab.Vanilla.Kill(participant);
                NarrativeNpc npc = lab.World.Registry.GetNpc(participant);
                if (npc != null)
                {
                    npc.Alive = false;
                }
            }

            Assert.Equal(1, ThreadLifecycle.Review(lab.World, lab.Vanilla, lab.Vanilla.Now));
            Assert.Equal(ThreadState.Quarantined, lab.Situation.Thread.State);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, lab.Vanilla);

            Assert.Equal(0, profile.ResolvedByOthers);
            Assert.Equal(0, profile.ResolvedUnattributed);
            Assert.Equal(1, profile.Withdrawn);
            Assert.Equal(1, profile.Ignored);
            Assert.True(Entry(profile, lab.Situation.Thread.Id).EndedBy.IsNone);
        }

        // -- debug only, and derived -------------------------------------------------------------

        /// <summary>
        /// Derived, not stored (`D022`): every number is read from state the save already carries,
        /// so the profile comes back identical after a reload for the same reason history does.
        /// </summary>
        [Fact]
        public void TheProfileReadsBackTheSameAfterAReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            Engage(lab);
            NarrativeThread other = Matter(lab, "handled_by_others");
            ThreadResolution.Resolve(lab.World, other, "settled", lab.Situation.WitnessId, lab.Vanilla.Now);

            string before = EngagementTelemetry.Describe(lab.World, lab.Vanilla);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(before, EngagementTelemetry.Describe(reloaded, lab.Vanilla));
            Assert.Contains("generated 2", before);
        }

        /// <summary>
        /// The property that keeps this debug telemetry rather than an authority: reading it leaves
        /// the world exactly as it was, so nothing downstream can come to depend on having been
        /// read, and the living-world plan's rule that production selection must not consult these
        /// counts holds structurally rather than by promise.
        /// </summary>
        [Fact]
        public void ReadingTheProfileChangesNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            Engage(lab);

            int events = lab.World.Ledger.Events.Count;
            int threads = lab.World.Threads.Count;
            ThreadState state = lab.Situation.Thread.State;
            GameTime advanced = lab.Situation.Thread.LastAdvancedAt;
            int beliefs = lab.World.Knowledge.BeliefsOf(lab.Player).Count();

            string first = EngagementTelemetry.Describe(lab.World, lab.Vanilla);
            string second = EngagementTelemetry.Describe(lab.World, lab.Vanilla);

            Assert.Equal(first, second);
            Assert.Equal(events, lab.World.Ledger.Events.Count);
            Assert.Equal(threads, lab.World.Threads.Count);
            Assert.Equal(state, lab.Situation.Thread.State);
            Assert.Equal(advanced, lab.Situation.Thread.LastAdvancedAt);
            Assert.Equal(beliefs, lab.World.Knowledge.BeliefsOf(lab.Player).Count());
        }

        /// <summary>
        /// A build with no player bound says so instead of reporting that nothing reached them.
        /// Zero surfaced out of four generated is a finding; zero out of four with nobody to
        /// surface anything to is a missing subject (`D017`).
        /// </summary>
        [Fact]
        public void ABuildWithNoPlayerSaysSoRatherThanCountingNothingAsReachingThem()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Tell(lab);
            SandboxVanillaState nobody = new SandboxVanillaState(EntityId.None);

            EngagementProfile profile = EngagementTelemetry.Read(lab.World, nobody);

            Assert.False(profile.PlayerKnown);
            Assert.Equal(1, profile.Generated);
            Assert.Equal(0, profile.Surfaced);
            Assert.Contains("no player is bound", EngagementTelemetry.Describe(lab.World, nobody));
            Assert.Contains("not surfaced: no player to surface it to", Entry(profile, lab.Situation.Thread.Id).Evidence);
        }

        /// <summary>An empty world reports an empty profile rather than throwing at the surface.</summary>
        [Fact]
        public void AWorldWithNoSituationsSaysSo()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            EntityId player = world.NewId("npc");

            Assert.Equal(0, EngagementTelemetry.Read(world, new SandboxVanillaState(player)).Generated);
            Assert.Contains("no situations generated yet", EngagementTelemetry.Describe(world, new SandboxVanillaState(player)));
            Assert.Contains("no situations generated yet", EngagementTelemetry.Describe(null, null));
        }

        // -- helpers -----------------------------------------------------------------------------

        private static EngagementEntry Entry(EngagementProfile profile, EntityId matterId)
        {
            return Assert.Single(profile.Matters, m => m.MatterId == matterId);
        }

        /// <summary>Somebody mentioned it to the player, which is how most matters reach them.</summary>
        private static void Tell(TheftLaboratory lab)
        {
            lab.World.Knowledge.Teach(
                lab.Player,
                lab.Situation.TheftFactId,
                KnowledgeSource.Hearsay,
                0.6,
                lab.Vanilla.Now,
                canProve: false);
        }

        private static void Tell(TheftLaboratory lab, NarrativeThread matter)
        {
            lab.World.Knowledge.Teach(
                lab.Player,
                matter.FactIds[0],
                KnowledgeSource.Hearsay,
                0.6,
                lab.Vanilla.Now,
                canProve: false);
        }

        /// <summary>
        /// The player does something about it through a verb in the library, so the act is
        /// attributed by the thread id the verb itself recorded rather than by a test's own event.
        /// </summary>
        private static void Engage(TheftLaboratory lab)
        {
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            ActionOutcome outcome = lab.Perform("intimidate", lab.Situation.ThiefId);

            Assert.NotNull(outcome.Check);
            Assert.Contains(outcome.Events, e => e.Actor == lab.Player && e.ThreadId == lab.Situation.Thread.Id);
        }

        /// <summary>A second matter of the world's, with one claim of its own to be told about.</summary>
        private static NarrativeThread Matter(TheftLaboratory lab, string archetypeId)
        {
            Fact claim = new Fact(
                lab.World.NewId("fact"),
                lab.Situation.VictimId,
                FactPredicates.Possesses,
                lab.World.NewId("item"),
                archetypeId);
            lab.World.Knowledge.AddFact(claim);

            NarrativeThread thread = new NarrativeThread(lab.World.NewId("thread"), archetypeId, lab.Vanilla.Now)
            {
                State = ThreadState.Active
            };
            thread.ParticipantIds.Add(lab.Situation.VictimId);
            thread.FactIds.Add(claim.Id);
            lab.World.Threads.Add(thread);
            return thread;
        }
    }
}
