using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class InvestigationActionTests
    {
        /// <summary>
        /// The step's done-when: the case is closed on evidence, and nobody told the player
        /// anything. The route below never speaks to a soul - it reads a body, reads a shelf,
        /// reads a room and tests a bottle - and it ends with an authority acting on physical
        /// proof.
        /// </summary>
        [Fact]
        public void ADeathIsSolvedByEvidenceWithNobodyTellingThePlayerAnything()
        {
            DeathLab lab = DeathLab.Create();

            // The body is where it fell. Searching the room recovers it.
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.CorpseId);

            // What killed him is only on the body, and only a forensic reading gets it.
            ActionOutcome forensics = lab.Run("examine_corpse", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);
            Assert.True(forensics.Succeeded);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.CauseFactId));

            // The room still shows who was in it, and where they went.
            ActionOutcome trail = lab.Run("track", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);
            Assert.True(trail.Succeeded);
            Fact whereabouts = lab.World.Knowledge.Facts.Values.Single(
                f => f.Predicate == FactPredicates.LocatedAt && f.Subject == lab.Situation.PoisonerId);
            Assert.Equal(lab.Situation.HomeZoneId, whereabouts.Object);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, whereabouts.Id));

            // The apothecary never says a word; his ledger does.
            ActionOutcome records = lab.Run("search_records", lab.Situation.ShopZoneId, lab.Situation.ApothecaryId, EntityId.None);
            Assert.True(records.Succeeded);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.SupplyFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.SupplyFactId));

            // Reading it is not holding it. The vial is at the house, so that is where it is taken.
            lab.Run("search", lab.Situation.HomeZoneId, EntityId.None, lab.Situation.SupplyFactId);
            Assert.Contains(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.VialId);

            ActionOutcome identified = lab.Run("identify_substance", lab.Situation.HomeZoneId, EntityId.None, EntityId.None);
            Assert.True(identified.Succeeded);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.KillFactId));

            ActionOutcome report = lab.Run("report", lab.Situation.SceneZoneId, lab.Situation.GuardId, lab.Situation.KillFactId);
            Assert.Contains(report.Notes, note => note.Contains("accepted it on PhysicalProof"));
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.CrimeReported
                                                          && e.Related.Contains(lab.Situation.KillFactId));
            Assert.True(lab.World.Knowledge.CanProve(lab.Situation.GuardId, lab.Situation.KillFactId));

            // The point of the whole route: not one belief the player holds came out of anybody's
            // mouth. Everything is a body, a book, a bottle or a floor.
            foreach (KnowledgeRecord belief in lab.World.Knowledge.BeliefsOf(lab.Player))
            {
                Assert.NotEqual(KnowledgeSource.Hearsay, belief.Source);
                Assert.True(belief.ToldBy.IsNone, "a belief arrived from " + belief.ToldBy);
            }
        }

        [Fact]
        public void NoSocialVerbCanOpenTheCaseBecauseNobodyKnowsAnythingToTell()
        {
            DeathLab lab = DeathLab.Create();

            // The apothecary is the most cooperative person in the scenario and still has nothing
            // on the killing: questioning him can never reach the fact the case turns on.
            ActionContext talking = lab.Context(lab.Situation.ShopZoneId, lab.Situation.ApothecaryId, EntityId.None);
            lab.Actions.Get("question").Perform(talking);

            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.KillFactId));
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.CauseFactId));
        }

        [Fact]
        public void ForensicsNeedsTheBodyInHandRatherThanTheRoomItIsIn()
        {
            DeathLab lab = DeathLab.Create();

            Availability before = lab.Actions.Get("examine_corpse")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));
            Assert.False(before.IsAvailable);
            Assert.Contains("not carrying remains", before.Reason);

            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);

            Availability after = lab.Actions.Get("examine_corpse")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));
            Assert.True(after.IsAvailable);
        }

        [Fact]
        public void SearchingARoomCannotReachEvidenceThatIsSomewhereElse()
        {
            DeathLab lab = DeathLab.Create();

            // The vial is in the poisoner's house. Standing over the body and searching finds the
            // story and not the bottle.
            ActionOutcome outcome = lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.KillFactId);

            Assert.True(outcome.Succeeded);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.VialId);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.KillFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.KillFactId));
        }

        [Fact]
        public void ReadingRecordsLeavesTheRecordsWhereTheyWere()
        {
            DeathLab lab = DeathLab.Create();

            lab.Run("search_records", lab.Situation.ShopZoneId, lab.Situation.ApothecaryId, EntityId.None);

            Assert.Contains(lab.Vanilla.GetInventory(lab.Situation.ApothecaryId), item => item.Id == lab.Situation.LedgerId);
            Assert.DoesNotContain(lab.Vanilla.GetInventory(lab.Player), item => item.Id == lab.Situation.LedgerId);
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.SupplyFactId));
        }

        [Fact]
        public void GettingCaughtInSomebodyElsesRecordsIsRecordedAsTrespass()
        {
            DeathLab lab = DeathLab.Create(CheckOutcome.CriticalFail);
            ActionContext context = lab.Context(lab.Situation.ShopZoneId, lab.Situation.ApothecaryId, EntityId.None);
            context.Witnesses.Add(lab.Situation.ApothecaryId);

            ActionOutcome outcome = lab.Actions.Get("search_records").Perform(context);

            Assert.Equal(CheckOutcome.CriticalFail, outcome.Outcome);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Trespass);
        }

        /// <summary>
        /// A botched reading is not "you learn nothing". It is a wrong answer held confidently,
        /// standing beside a true one that is untouched - the state a later contradiction can
        /// actually work on.
        /// </summary>
        [Fact]
        public void AMisreadBodyProducesAConfidentWrongConclusionRatherThanNothing()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);

            lab.Checks.Standing = CheckOutcome.CriticalFail;
            ActionOutcome outcome = lab.Run("examine_corpse", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);

            Fact wrong = lab.World.Knowledge.Facts.Values.Single(f => f.DistortionOf == lab.Situation.CauseFactId);
            Assert.Equal(TruthState.False, wrong.Truth);
            Assert.True(lab.World.Knowledge.BelievesConfidently(lab.Player, wrong.Id));
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.CauseFactId));
            Assert.Equal(TruthState.True, lab.World.Knowledge.GetFact(lab.Situation.CauseFactId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.RumorDistorted);
        }

        /// <summary>The way back out of that mistake, using nothing but the two claims themselves.</summary>
        [Fact]
        public void ComparingTwoVersionsOfTheSameClaimDiscardsTheFalseOne()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);

            lab.Checks.Standing = CheckOutcome.CriticalFail;
            lab.Run("examine_corpse", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);
            Fact wrong = lab.World.Knowledge.Facts.Values.Single(f => f.DistortionOf == lab.Situation.CauseFactId);

            // Then somebody reads it right, so both versions are held at once.
            lab.Checks.Standing = CheckOutcome.Pass;
            lab.Run("examine_corpse", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.CauseFactId));

            ActionOutcome compared = lab.Run("compare_testimony", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);

            Assert.True(compared.Succeeded);
            Assert.True(lab.World.Knowledge.BelievesConfidently(lab.Player, lab.Situation.CauseFactId));
            Assert.False(lab.World.Knowledge.BelievesConfidently(lab.Player, wrong.Id));

            // Not deleted - still there, too weak to act on, and available to be argued about.
            Assert.True(lab.World.Knowledge.Knows(lab.Player, wrong.Id));
        }

        [Fact]
        public void ComparingIsNotOfferedWhileEverythingHeldAgrees()
        {
            DeathLab lab = DeathLab.Create();

            Availability availability = lab.Actions.Get("compare_testimony")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));

            Assert.False(availability.IsAvailable);
            Assert.Contains("contradicts", availability.Reason);
        }

        [Fact]
        public void ATrailGoesColdAfterAFewDays()
        {
            DeathLab lab = DeathLab.Create();
            ActionContext fresh = lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None);
            Assert.True(lab.Actions.Get("track").GetAvailability(fresh).IsAvailable);

            lab.Vanilla.AdvanceDays(TrackAction.TrailDays + 1);

            Availability cold = lab.Actions.Get("track").GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));
            Assert.False(cold.IsAvailable);
            Assert.Contains("left a trail", cold.Reason);
        }

        [Fact]
        public void TrackingDoesNotRepeatItselfOnceThePlayerKnowsWhereTheyWent()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("track", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);

            Availability again = lab.Actions.Get("track")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));

            Assert.False(again.IsAvailable);
        }

        [Fact]
        public void FollowingSomebodyWhoIsNotHereIsNotOffered()
        {
            DeathLab lab = DeathLab.Create();

            Availability elsewhere = lab.Actions.Get("follow")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, lab.Situation.PoisonerId, EntityId.None));
            Assert.False(elsewhere.IsAvailable);
            Assert.Contains("not here", elsewhere.Reason);

            Availability here = lab.Actions.Get("follow")
                .GetAvailability(lab.Context(lab.Situation.HomeZoneId, lab.Situation.PoisonerId, EntityId.None));
            Assert.True(here.IsAvailable);
        }

        /// <summary>
        /// Catching somebody at what they did is proof, and it is the only route to proof that
        /// needs no object at all - the witness is the player.
        /// </summary>
        [Fact]
        public void FollowingWellEnoughCatchesThemAtIt()
        {
            DeathLab lab = DeathLab.Create(CheckOutcome.CriticalPass);

            ActionOutcome outcome = lab.Run("follow", lab.Situation.HomeZoneId, lab.Situation.PoisonerId, EntityId.None);

            Assert.True(outcome.Succeeded);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.KillFactId));
        }

        [Fact]
        public void ATailThatIsNoticedTellsThemTheyAreBeingLookedInto()
        {
            DeathLab lab = DeathLab.Create(CheckOutcome.CriticalFail);

            lab.Run("follow", lab.Situation.HomeZoneId, lab.Situation.PoisonerId, EntityId.None);

            Fact investigating = lab.World.Knowledge.Facts.Values.Single(
                f => f.Predicate == FactPredicates.Investigating && f.Object == lab.Situation.PoisonerId);
            Assert.True(lab.World.Knowledge.Knows(lab.Situation.PoisonerId, investigating.Id));
        }

        /// <summary>
        /// One "investigating" claim per person looked into. Reusing a single claim per actor told
        /// the second suspect that the player was after the first.
        /// </summary>
        [Fact]
        public void BeingLookedIntoIsKeptPerSuspect()
        {
            DeathLab lab = DeathLab.Create(CheckOutcome.CriticalFail);

            lab.Run("follow", lab.Situation.HomeZoneId, lab.Situation.PoisonerId, EntityId.None);
            lab.Run("follow", lab.Situation.ShopZoneId, lab.Situation.ApothecaryId, EntityId.None);

            List<Fact> claims = lab.World.Knowledge.Facts.Values
                .Where(f => f.Predicate == FactPredicates.Investigating).ToList();

            Assert.Equal(2, claims.Count);
            Assert.False(lab.World.Knowledge.Knows(
                lab.Situation.ApothecaryId,
                claims.Single(f => f.Object == lab.Situation.PoisonerId).Id));
        }

        [Fact]
        public void OverheardTalkIsFiledAsHearsayAndCannotBeProved()
        {
            DeathLab lab = DeathLab.Create();

            // Put two people who know something in one room with the player.
            lab.Vanilla.SetZone(lab.Situation.PoisonerId, lab.Situation.ShopZoneId);
            ActionOutcome outcome = lab.Run("eavesdrop", lab.Situation.ShopZoneId, EntityId.None, EntityId.None);

            Assert.True(outcome.Succeeded);
            KnowledgeRecord picked = lab.World.Knowledge.BeliefsOf(lab.Player)
                .Single(b => b.Source == KnowledgeSource.Hearsay);
            Assert.False(picked.CanProve);
            Assert.False(picked.ToldBy.IsNone);
        }

        [Fact]
        public void ThereIsNothingToOverhearWhenNobodyElseIsInTheRoom()
        {
            DeathLab lab = DeathLab.Create();

            Availability alone = lab.Actions.Get("eavesdrop")
                .GetAvailability(lab.Context(lab.Situation.HomeZoneId, EntityId.None, EntityId.None));

            Assert.False(alone.IsAvailable);
            Assert.Contains("talking", alone.Reason);
        }

        /// <summary>
        /// A generalist can always turn a thing over. What they usually cannot do is come away
        /// with something they could show anybody.
        /// </summary>
        [Fact]
        public void InspectingReadsAnythingButRarelyProducesProof()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);

            ActionOutcome outcome = lab.Run("inspect", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);

            Assert.True(outcome.Succeeded);
            Assert.True(lab.World.Knowledge.Knows(lab.Player, lab.Situation.CauseFactId));
            Assert.False(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.CauseFactId));
        }

        [Fact]
        public void ExaminationIsUnavailableWhereTheBuildCannotReadInventories()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);
            lab.Vanilla.SetCapability(VanillaCapability.ReadInventory, false);

            Availability availability = lab.Actions.Get("examine_corpse")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));

            Assert.False(availability.IsAvailable);
            Assert.Contains("on this build", availability.Reason);
        }

        /// <summary>
        /// Reading is not a check the player can farm until it lands: a discipline that has
        /// already produced proof has nothing left to be pointed at.
        /// </summary>
        [Fact]
        public void ADisciplineStopsOfferingItselfOnceItHasProvedWhatItCan()
        {
            DeathLab lab = DeathLab.Create();
            lab.Run("search", lab.Situation.SceneZoneId, EntityId.None, lab.Situation.DeathFactId);
            lab.Run("examine_corpse", lab.Situation.SceneZoneId, EntityId.None, EntityId.None);

            Availability availability = lab.Actions.Get("examine_corpse")
                .GetAvailability(lab.Context(lab.Situation.SceneZoneId, EntityId.None, EntityId.None));

            Assert.False(availability.IsAvailable);
        }

        /// <summary>
        /// The read/translate split. Both verbs are pointed at paper the actor is carrying; what
        /// separates them is whether the writing was meant to be understood, which the graph
        /// already records as how hard somebody worked to keep the fact quiet.
        /// </summary>
        [Fact]
        public void PlainWritingIsReadAndObscuredWritingIsTranslated()
        {
            PaperLab plain = PaperLab.Create(secrecy: ReadDocumentAction.ObscuredAt - 1);

            Assert.True(plain.Actions.Get("read").GetAvailability(plain.Context()).IsAvailable);
            Availability wrongTool = plain.Actions.Get("translate").GetAvailability(plain.Context());
            Assert.False(wrongTool.IsAvailable);
            Assert.Contains("cannot already read", wrongTool.Reason);

            ActionOutcome outcome = plain.Actions.Get("read").Perform(plain.Context());
            Assert.True(plain.World.Knowledge.CanProve(plain.Player, plain.FactId));
            Assert.Equal(ProceduralCheckProfiles.Documents.Id, outcome.Check.ProfileId);
        }

        [Fact]
        public void ACodedDocumentIsNotReadableUntilItIsWorkedOut()
        {
            PaperLab coded = PaperLab.Create(secrecy: ReadDocumentAction.ObscuredAt);

            Availability plainly = coded.Actions.Get("read").GetAvailability(coded.Context());
            Assert.False(plainly.IsAvailable);
            Assert.Contains("nothing written", plainly.Reason);

            ActionOutcome outcome = coded.Actions.Get("translate").Perform(coded.Context());
            Assert.True(outcome.Succeeded);
            Assert.True(coded.World.Knowledge.CanProve(coded.Player, coded.FactId));
            Assert.Equal(ProceduralCheckProfiles.Translation.Id, outcome.Check.ProfileId);
        }

        /// <summary>
        /// A specialist that cannot recognise the material still leaves a route open: the
        /// generalist reads anything, so a mis-tagged object never dead-ends an investigation.
        /// </summary>
        [Fact]
        public void AnObjectNoSpecialistRecognisesIsStillInspectable()
        {
            PaperLab odd = PaperLab.Create(secrecy: 10, category: "unfiled", name: "an odd little thing");

            Assert.False(odd.Actions.Get("read").GetAvailability(odd.Context()).IsAvailable);
            Assert.False(odd.Actions.Get("identify_substance").GetAvailability(odd.Context()).IsAvailable);
            Assert.True(odd.Actions.Get("inspect").GetAvailability(odd.Context()).IsAvailable);
        }

        [Fact]
        public void ReadingRecordsIsNotOfferedWithoutSomebodyWhoseRecordsTheyAre()
        {
            PaperLab lab = PaperLab.Create(secrecy: 10);

            Availability availability = lab.Actions.Get("search_records").GetAvailability(lab.Context());

            Assert.False(availability.IsAvailable);
            Assert.Contains("keeps records", availability.Reason);
        }

        [Fact]
        public void EveryInvestigationVerbNamesTheCheckItRolls()
        {
            string[] verbs =
            {
                "inspect", "examine_corpse", "read", "translate", "identify_substance",
                "search_records", "track", "follow", "eavesdrop", "compare_testimony"
            };

            ActionRegistry registry = StandardActions.CreateRegistry();
            foreach (string verb in verbs)
            {
                Assert.NotNull(registry.Get(verb));
                Assert.NotNull(ProceduralCheckProfiles.ForAction(verb));
                Assert.Equal(ActionFamily.Information, registry.Get(verb).Family);
            }
        }

        /// <summary>One reader, one document, one fact. Enough to test which discipline claims it.</summary>
        private sealed class PaperLab
        {
            private PaperLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public EntityId Player { get; private set; }

            public EntityId FactId { get; private set; }

            public static PaperLab Create(int secrecy, string category = "book", string name = "a folded note")
            {
                PaperLab lab = new PaperLab();
                NarrativeWorldState world = new NarrativeWorldState(24025);
                EntityId player = world.NewId("npc");
                EntityId zone = world.NewId("zone");
                EntityId subject = world.NewId("npc");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 4, money: 0, zone: zone);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });
                world.Registry.Add(new NarrativeNpc(subject, "Garron"));

                EntityId paperId = world.NewId("item");
                vanilla.GiveItem(player, new ItemDescriptor(paperId, name, category, 5, "book"));

                Fact funding = new Fact(world.NewId("fact"), subject, FactPredicates.Funds, EntityId.None, "the Red Knives", TruthState.True, secrecy);
                funding.EvidenceIds.Add(paperId);
                world.Knowledge.AddFact(funding);

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.FactId = funding.Id;
                return lab;
            }

            public ActionContext Context()
            {
                return new ActionContext(World, Vanilla, new FixedCheckResolver(CheckOutcome.Pass), World.Rng, Player, EntityId.None);
            }
        }

        private sealed class DeathLab
        {
            private DeathLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public UnexplainedDeathSituation Situation { get; private set; }

            public static DeathLab Create(CheckOutcome outcome = CheckOutcome.Pass)
            {
                DeathLab lab = new DeathLab();
                NarrativeWorldState world = new NarrativeWorldState(24024);
                EntityId player = world.NewId("npc");
                EntityId scene = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 6, money: 200, zone: scene);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);
                lab.Situation = UnexplainedDeathSituation.Create(world, new SandboxStager(vanilla), player, scene, vanilla.Now);
                vanilla.Kill(lab.Situation.VictimId);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            public ActionContext Context(EntityId zone, EntityId target, EntityId subjectFact)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread,
                    SubjectFact = subjectFact
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target, EntityId subjectFact)
            {
                return Actions.Get(actionId).Perform(Context(zone, target, subjectFact));
            }
        }
    }
}
