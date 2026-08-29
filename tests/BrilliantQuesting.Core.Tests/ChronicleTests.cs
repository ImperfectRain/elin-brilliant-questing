using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
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
    /// BQ-034: what is finished is a reading of the ledger, and it reads back the same after a
    /// reload, without telling the player anything their character did not learn.
    /// </summary>
    public class ChronicleTests
    {
        [Fact]
        public void AnUnresolvedSituationLeavesNothingInTheChronicle()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.Empty(Chronicle.Entries(lab.World, lab.Player));
            Assert.Contains("nothing finished yet", Chronicle.Describe(lab.World, lab.Player));
        }

        [Fact]
        public void AResolvedSituationLeavesAReadableEntryNamingWhatWasDone()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("question", lab.Situation.WitnessId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(lab.World, lab.Player));

            Assert.Equal(lab.Situation.Thread.Id, entry.ThreadId);
            Assert.Equal("property_returned", entry.Outcome);
            Assert.Contains(entry.WhatThePlayerDid, act => act.Type == WorldEventType.ItemReturned
                                                          && act.Towards == lab.Situation.VictimId);

            string text = Chronicle.Describe(lab.World, lab.Player);
            Assert.Contains("property returned", text);
            Assert.Contains("item returned", text);
        }

        [Fact]
        public void AnActThatNamesTheMattersOwnFactCountsAsPartOfIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, true);

            // Telling the victim who took it records the theft fact but no thread id, which is
            // how most of the library still writes history. It is plainly part of this matter.
            lab.Perform("expose", lab.Situation.VictimId, c => c.SubjectFact = lab.Situation.TheftFactId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(lab.World, lab.Player));

            Assert.Contains(entry.WhatThePlayerDid, act => act.Type == WorldEventType.SecretRevealed);
            Assert.Contains(entry.WhatThePlayerDid, act => act.Type == WorldEventType.ItemReturned);
        }

        [Fact]
        public void TheEntrySurvivesSaveAndLoad()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("question", lab.Situation.WitnessId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Equal(Chronicle.Describe(lab.World, lab.Player), Chronicle.Describe(reloaded, lab.Player));
            ChronicleEntry entry = Assert.Single(Chronicle.Entries(reloaded, lab.Player));
            Assert.Equal("property_returned", entry.Outcome);
        }

        [Fact]
        public void ReloadingASaveDoesNotAddASecondEnding()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Assert.Single(reloaded.Ledger.Events, e => e.Type == WorldEventType.ThreadResolved);
        }

        [Fact]
        public void TheChronicleReportsOnlyWhatThePlayerBelievesAboutTheMatter()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            // The theft itself is in the thread and true in the world, and the player never
            // learned it. A record that named the thief here would hand them the answer for
            // having handed back a ring.
            Assert.NotNull(lab.World.Knowledge.GetFact(lab.Situation.TheftFactId));
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(lab.World, lab.Player));

            Assert.DoesNotContain(entry.WhatWasKnown, known => known.FactId == lab.Situation.TheftFactId);
            Assert.DoesNotContain(FactPredicates.Stole, Chronicle.Describe(lab.World, lab.Player));
        }

        [Fact]
        public void ABeliefTheMatterRestedOnIsCarriedWithItsJournalTag()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, false, lab.Situation.WitnessId);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            ChronicleEntry entry = Assert.Single(Chronicle.Entries(lab.World, lab.Player));
            JournalEntry known = Assert.Single(entry.WhatWasKnown, k => k.FactId == lab.Situation.TheftFactId);

            Assert.Equal(JournalTag.Reported, known.Tag);
        }

        [Fact]
        public void AResolutionSomebodyElseCarriedOutStaysOutOfThePlayersChronicle()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            WorldEvent resolved = ThreadResolution.Resolve(
                lab.World, lab.Situation.Thread, "settled_between_them", lab.Situation.VictimId, lab.Vanilla.Now);

            Assert.NotNull(resolved);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Empty(Chronicle.Entries(lab.World, lab.Player));
        }

        [Fact]
        public void ResolvingASituationTwiceDoesNotRewriteHistory()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "property_returned", lab.Player, lab.Vanilla.Now);
            WorldEvent second = ThreadResolution.Resolve(
                lab.World, lab.Situation.Thread, "property_kept", lab.Player, lab.Vanilla.Now.PlusDays(1));

            Assert.Null(second);
            Assert.Equal("property_returned", lab.Situation.Thread.Resolution);
            Assert.Single(lab.World.Ledger.Events, e => e.Type == WorldEventType.ThreadResolved);
            Assert.Single(Chronicle.Entries(lab.World, lab.Player));
        }

        [Fact]
        public void RecordingTheEndingDoesNotReopenTheSituation()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ThreadResolution.Resolve(lab.World, lab.Situation.Thread, "property_returned", lab.Player, lab.Vanilla.Now, 0.9);

            // The consequence layer pushes any thread named by a fresh event back to Active. The
            // ending has to be written before it is announced, or a situation closes itself open.
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
        }

        [Fact]
        public void EndingAMatterCostsTheParticipantsNoExtraStandingOfItsOwn()
        {
            DebtScene scene = DebtScene.Create();
            int before = scene.Vanilla.GetAffinity(scene.Situation.CreditorId);

            ActionContext context = scene.Context(scene.Situation.CreditorId);
            context.ThirdParty = scene.Situation.DebtorId;
            scene.Actions.Get("pay_debt").Perform(context);

            int accounted = scene.World.Memories.AccountedAffinity(scene.Situation.CreditorId, scene.Player);

            // The payment moved the creditor. The record of the matter closing must not move them
            // a second time for the same deed.
            Assert.Equal(before + accounted, scene.Vanilla.GetAffinity(scene.Situation.CreditorId));
            Assert.Equal("debt_paid", Assert.Single(Chronicle.Entries(scene.World, scene.Player)).Outcome);
        }

        private sealed class DebtScene
        {
            private DebtScene()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public DebtSituation Situation { get; private set; }

            public static DebtScene Create()
            {
                DebtScene scene = new DebtScene();
                NarrativeWorldState world = new NarrativeWorldState(4711);
                EntityId player = world.NewId("npc");
                EntityId zone = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, money: 4000, zone: zone);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                scene.World = world;
                scene.Vanilla = vanilla;
                scene.Player = player;
                scene.Actions = StandardActions.CreateRegistry();
                scene.Situation = DebtSituation.Create(world, new SandboxStager(vanilla), zone, vanilla.Now);

                new ConsequenceEngine(world, vanilla).Attach();
                return scene;
            }

            public ActionContext Context(EntityId target)
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }
        }
    }
}
