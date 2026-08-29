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
    /// BQ-028. A hamlet whose field has gone under, a shrine at the edge of it, and two players
    /// who differ in exactly one thing: who they follow.
    ///
    /// The step's done-when is a comparison between builds rather than between families, so it is
    /// tested that way - the same world, the same pack, the same dice, and one of them has a route
    /// out of Ashfen while the other is told by name that he has none.
    /// </summary>
    public class FaithActionTests
    {
        [Fact]
        public void BothFaithVerbsAreRegisteredInTheirFamily()
        {
            ActionRegistry registry = StandardActions.CreateRegistry();

            Assert.Equal(ActionFamily.MagicFaith, registry.Get("make_offering").Family);
            Assert.Equal(ActionFamily.MagicFaith, registry.Get("invoke_blessing").Family);
            Assert.Equal(ProceduralCheckProfiles.Devotion, ProceduralCheckProfiles.ForAction("invoke_blessing"));

            // Laying goods down is not an attempt at anything, so it deliberately has no check.
            Assert.Null(ProceduralCheckProfiles.ForAction("make_offering"));
        }

        // -- the done-when ------------------------------------------------------------------

        /// <summary>
        /// The step. Two builds, identical but for their god: one lifts the blight, the other is
        /// refused by name at both verbs and never reaches a roll.
        /// </summary>
        [Fact]
        public void AKumiromiWorshipperHasARouteThroughAshfenThatAnotherDeitysFollowerDoesNot()
        {
            BlightLab devout = BlightLab.Create(follows: "Kumiromi", piety: 30);
            devout.Learn(devout.Situation.SacredMatterId);
            devout.Offer("first fruits");
            ActionOutcome answered = devout.Run("invoke_blessing", devout.Shrine);

            Assert.True(answered.Succeeded);
            Assert.Equal(TruthState.Superseded, devout.Fact(devout.Situation.BlightId).Truth);
            Assert.Equal(ThreadState.Resolved, devout.Situation.Thread.State);
            Assert.Equal("blessing_granted", devout.Situation.Thread.Resolution);

            BlightLab other = BlightLab.Create(follows: "Opatos", piety: 30);
            other.Learn(other.Situation.SacredMatterId);

            Availability offering = other.Can("make_offering", other.Shrine);
            Availability petition = other.Can("invoke_blessing", other.Shrine);

            Assert.False(offering.IsAvailable);
            Assert.Contains("Kumiromi", offering.Reason);
            Assert.False(petition.IsAvailable);
            Assert.Contains("Kumiromi does not answer a follower of Opatos", petition.Reason);
            Assert.Equal(TruthState.True, other.Fact(other.Situation.BlightId).Truth);
        }

        /// <summary>
        /// And the refusal is identity, not odds. With every roll forced to a critical success the
        /// follower of another god still has nothing to roll: the option is not there.
        /// </summary>
        [Fact]
        public void TheDeityGateIsAPreconditionAndNotADifficulty()
        {
            BlightLab other = BlightLab.Create(follows: "Opatos", piety: 99, outcome: CheckOutcome.CriticalPass);
            other.Learn(other.Situation.SacredMatterId);

            other.Offer("first fruits");
            ActionOutcome outcome = other.Run("invoke_blessing", other.Shrine);

            Assert.Null(outcome.Check);
            Assert.Equal(TruthState.True, other.Fact(other.Situation.BlightId).Truth);
            Assert.Equal(ThreadState.Active, other.Situation.Thread.State);
        }

        /// <summary>Following nobody is refused the same way, and says so rather than saying nothing.</summary>
        [Fact]
        public void AFollowerOfNobodyIsRefusedByName()
        {
            BlightLab lab = BlightLab.Create(follows: string.Empty, piety: 40);
            lab.Learn(lab.Situation.SacredMatterId);

            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(petition.IsAvailable);
            Assert.Contains("those who follow nobody", petition.Reason);
        }

        /// <summary>
        /// The exhaustive half of the claim: every verb outside the faith family, run against
        /// everybody in Ashfen in both places on its own copy of the world, and the blight never
        /// lifts. Land is not a thing that can be searched, appraised, mended or handed over.
        /// </summary>
        [Fact]
        public void NothingOutsideTheFaithFamilyCanLiftTheBlight()
        {
            List<string> lifted = new List<string>();
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                if (action.Family == ActionFamily.MagicFaith)
                {
                    continue;
                }

                for (int who = 0; who < BlightLab.PeopleToTry; who++)
                {
                    BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 40);
                    lab.Learn(lab.Situation.SacredMatterId);
                    EntityId target = lab.Everyone()[who];
                    foreach (EntityId zone in new[] { lab.Hamlet, lab.Shrine })
                    {
                        if (lab.Can(action.Id, zone, target).IsAvailable)
                        {
                            lab.Run(action.Id, zone, target);
                        }
                    }

                    if (lab.Fact(lab.Situation.BlightId).Truth != TruthState.True)
                    {
                        lifted.Add(action.Id);
                    }
                }
            }

            Assert.Empty(lifted);
        }

        /// <summary>
        /// And the sweep above is not vacuous. Ashfen is an ordinary place with ordinary people in
        /// it: four other families have verbs open there, for any build. They simply do not touch
        /// the land, which is the difference between a situation with one route and a situation
        /// where one route happens to be the one that answers this.
        /// </summary>
        [Fact]
        public void OrdinaryVerbsAreStillOpenInAshfenForEveryBuild()
        {
            BlightLab lab = BlightLab.Create(follows: "Opatos", piety: 30);

            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (EntityId zone in new[] { lab.Hamlet, lab.Shrine })
            {
                foreach (EntityId target in new[] { lab.Steward, lab.Keeper, EntityId.None })
                {
                    families.UnionWith(lab.Actions.AvailableFamilies(lab.Context(zone, target)));
                }
            }

            Assert.DoesNotContain(ActionFamily.MagicFaith, families);
            Assert.True(families.Count >= 3, "expected 3+ families still open, got " + families.Count);
        }

        [Fact]
        public void TheFaithFamilyIsOpenWhereTheOthersAreNot()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);

            Assert.Contains(ActionFamily.MagicFaith, lab.Actions.AvailableFamilies(lab.Context(lab.Shrine, EntityId.None)));
        }

        // -- the preconditions ----------------------------------------------------------------

        /// <summary>Piety is a threshold, not a penalty: too little and there is nothing to try.</summary>
        [Fact]
        public void PietyBelowWhatTheMatterAsksIsImpossibleAndNamesTheGap()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 5);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(petition.IsAvailable);
            Assert.Contains("your piety is 5", petition.Reason);
            Assert.Contains("20", petition.Reason);
        }

        [Fact]
        public void WithoutAnOfferingThereIsNothingToAskOn()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);

            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(petition.IsAvailable);
            Assert.Contains("laid nothing", petition.Reason);
        }

        /// <summary>An offering under what is asked names the gap rather than becoming a long shot.</summary>
        [Fact]
        public void AnOfferingUnderWhatIsAskedIsStillARefusal()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("copper charm");

            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(petition.IsAvailable);
            Assert.Contains("laid 4", petition.Reason);
            Assert.Contains("15", petition.Reason);
        }

        /// <summary>Two small offerings the god would not have heard become one he will.</summary>
        [Fact]
        public void OfferingsAddUp()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);

            lab.Offer("copper charm");
            Assert.False(lab.Can("invoke_blessing", lab.Shrine).IsAvailable);

            lab.Offer("seed corn");

            Assert.True(lab.Can("invoke_blessing", lab.Shrine).IsAvailable);
        }

        /// <summary>The petition is a route through the world: it happens at the shrine or not at all.</summary>
        [Fact]
        public void OffConsecratedGroundThereIsNoAltarToAskAt()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            Availability petition = lab.Can("invoke_blessing", lab.Hamlet);

            Assert.False(petition.IsAvailable);
            Assert.Contains("no altar of Kumiromi here", petition.Reason);
            Assert.False(lab.Can("make_offering", lab.Hamlet).IsAvailable);
        }

        /// <summary>
        /// Whose matter the blight is has to be learned. A devout player who walks straight to the
        /// shrine without asking anybody anything has the god and the goods and no route.
        /// </summary>
        [Fact]
        public void TheMatterMustBeKnownBeforeItCanBeAskedAbout()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Run("make_offering", lab.Shrine);

            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(petition.IsAvailable);
            Assert.Contains("whose matter this is", petition.Reason);
        }

        /// <summary>And the ordinary information verb is the door into it.</summary>
        [Fact]
        public void AskingTheShrineKeeperOpensTheFaithRoute()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Offer("first fruits");

            ActionOutcome told = lab.Run("question", lab.Shrine, lab.Keeper);

            Assert.True(told.Succeeded);
            Assert.True(lab.World.Knowledge.BelievesConfidently(lab.Player, lab.Situation.SacredMatterId));
            Assert.True(lab.Can("invoke_blessing", lab.Shrine).IsAvailable);
        }

        /// <summary>
        /// A build that cannot say who anybody follows loses the family rather than opening it to
        /// everybody - the same direction an unread quality takes on the production side.
        /// </summary>
        [Fact]
        public void ABuildThatCannotReadFaithLosesTheRouteRatherThanGainingIt()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Vanilla.SetCapability(VanillaCapability.ReadFaith, false);

            Availability offering = lab.Can("make_offering", lab.Shrine);
            Availability petition = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(offering.IsAvailable);
            Assert.False(petition.IsAvailable);
            Assert.Contains("cannot report who anybody follows", petition.Reason);
        }

        // -- the offering -----------------------------------------------------------------------

        [Fact]
        public void AnOfferingSpendsTheRealObjectAndLeavesARecord()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            EntityId fruits = lab.Item("first fruits");

            ActionOutcome outcome = lab.Offer("first fruits");

            Assert.Null(outcome.Check);
            Assert.DoesNotContain(fruits, lab.Held(lab.Player));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.OfferingMade);

            Fact standing = lab.SingleOpenFact(FactPredicates.OfferedTo);
            Assert.Equal(lab.Player, standing.Subject);
            Assert.Equal(40, DevotionSpec.Parse(standing.Value).MinimumOffering);
        }

        [Fact]
        public void AnEmptyPackHasNothingToOffer()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.EmptyThePack();

            Availability offering = lab.Can("make_offering", lab.Shrine);

            Assert.False(offering.IsAvailable);
            Assert.Contains("nothing to offer", offering.Reason);
        }

        // -- the four outcomes ------------------------------------------------------------------

        [Fact]
        public void APassSpendsWhatWasOffered()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            ActionOutcome outcome = lab.Run("invoke_blessing", lab.Shrine);

            Assert.True(outcome.Succeeded);
            Assert.Null(lab.SingleOpenFact(FactPredicates.OfferedTo));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Target == lab.Steward);
        }

        /// <summary>A generous answer leaves what was brought where it lies, and it can be asked on again.</summary>
        [Fact]
        public void ACriticalPassLeavesTheOfferingStanding()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30, outcome: CheckOutcome.CriticalPass);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            ActionOutcome outcome = lab.Run("invoke_blessing", lab.Shrine);

            Assert.True(outcome.Succeeded);
            Assert.NotNull(lab.SingleOpenFact(FactPredicates.OfferedTo));
        }

        [Fact]
        public void AFailureCostsTheOfferingAndLeavesTheBlight()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30, outcome: CheckOutcome.Fail);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            ActionOutcome outcome = lab.Run("invoke_blessing", lab.Shrine);

            Assert.False(outcome.Succeeded);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BlightId).Truth);
            Assert.Null(lab.SingleOpenFact(FactPredicates.OfferedTo));

            // The route is still there. Offer again and it can be asked again.
            lab.Offer("seed corn");
            lab.Offer("copper charm");
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.SacredMatterId).Truth);
        }

        /// <summary>
        /// The botch is the reason it is a decision. The matter passes out of the god's gift, so
        /// the route is gone - and gone for anybody, not only for the one who spoiled it.
        /// </summary>
        [Fact]
        public void ABotchedPetitionTakesTheMatterOutOfTheGodsGiftForGood()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30, outcome: CheckOutcome.CriticalFail);
            lab.Learn(lab.Situation.SacredMatterId);
            lab.Offer("first fruits");

            ActionOutcome outcome = lab.Run("invoke_blessing", lab.Shrine);

            Assert.False(outcome.Succeeded);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.SacredMatterId).Truth);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BlightId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Harmed && e.Target == lab.Steward);

            lab.Checks.Standing = CheckOutcome.CriticalPass;
            lab.Offer("seed corn");
            Availability again = lab.Can("invoke_blessing", lab.Shrine);

            Assert.False(again.IsAvailable);
            Assert.Contains("in anybody's gift", again.Reason);
        }

        // -- the specification ------------------------------------------------------------------

        [Fact]
        public void ASpecificationRoundTripsThroughTheFactItLivesIn()
        {
            DevotionSpec spec = new DevotionSpec("Kumiromi", 20, 15);

            DevotionSpec parsed = DevotionSpec.Parse(spec.ToFactValue());

            Assert.Equal("Kumiromi", parsed.Deity);
            Assert.Equal(20, parsed.MinimumPiety);
            Assert.Equal(15, parsed.MinimumOffering);
        }

        /// <summary>
        /// Ground with nothing asked for is the ordinary altar, and a malformed value costs its
        /// thresholds rather than the whole route.
        /// </summary>
        [Fact]
        public void ASpecificationWithoutThresholdsIsStillASpecification()
        {
            Assert.Equal(0, DevotionSpec.Parse("Kumiromi").MinimumPiety);
            Assert.Equal(0, DevotionSpec.Parse("Kumiromi piety").MinimumPiety);
            Assert.Equal(0, DevotionSpec.Parse("Kumiromi piety plenty").MinimumPiety);
            Assert.Null(DevotionSpec.Parse("   "));
            Assert.Null(DevotionSpec.Parse(null));
        }

        /// <summary>
        /// Nobody is not a god. An adapter that cannot name a deity must not match the matter's
        /// empty-string neighbour and hand everybody the route.
        /// </summary>
        [Fact]
        public void AnUnnamedDeityMatchesNobodyIncludingItself()
        {
            Assert.False(DevotionSpec.SameDeity(string.Empty, string.Empty));
            Assert.False(new DevotionSpec(string.Empty).IsFollowedBy(string.Empty));
            Assert.True(DevotionSpec.SameDeity("Kumiromi", "kumiromi"));
            Assert.True(DevotionSpec.SameDeity("Kumiromi", "godKumiromi"));
            Assert.False(DevotionSpec.SameDeity("Kumiromi", "Opatos"));
        }

        /// <summary>
        /// Devotional standing lives in a fact's free value, so a save is the one place it could
        /// quietly stop being readable.
        /// </summary>
        [Fact]
        public void AStandingOfferingSurvivesASaveWithItsWorthIntact()
        {
            BlightLab lab = BlightLab.Create(follows: "Kumiromi", piety: 30);
            lab.Offer("first fruits");

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Fact standing = null;
            foreach (Fact fact in reloaded.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.OfferedTo && fact.Truth == TruthState.True)
                {
                    standing = fact;
                }
            }

            Assert.NotNull(standing);
            Assert.Equal(40, DevotionSpec.Parse(standing.Value).MinimumOffering);
            Assert.Equal(
                BlightedFieldSituation.Blight.ToFactValue(),
                reloaded.Knowledge.GetFact(lab.Situation.SacredMatterId).Value);
        }

        /// <summary>Ashfen, its blighted field, and a shrine at the edge of it.</summary>
        private sealed class BlightLab
        {
            private BlightLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public BlightedFieldSituation Situation { get; private set; }

            public EntityId Hamlet => Situation.HamletZoneId;

            public EntityId Shrine => Situation.ShrineZoneId;

            public EntityId Steward => Situation.StewardId;

            public EntityId Keeper => Situation.ShrineKeeperId;

            public static BlightLab Create(string follows, int piety, CheckOutcome outcome = CheckOutcome.Pass)
            {
                BlightLab lab = new BlightLab();
                NarrativeWorldState world = new NarrativeWorldState(28028);
                EntityId player = world.NewId("npc");
                EntityId hamlet = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, money: 300, zone: hamlet);
                vanilla.SetFaith(player, follows, piety);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = BlightedFieldSituation.Create(world, stager, player, hamlet, vanilla.Now);
                lab.Situation.StockThePlayer(world, stager, player);

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            /// <summary>Everybody in Ashfen, plus nobody - which is a real target for a petition.</summary>
            public const int PeopleToTry = 3;

            public List<EntityId> Everyone()
            {
                return new List<EntityId> { Steward, Keeper, EntityId.None };
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            /// <summary>The one still-standing fact with this predicate, or null.</summary>
            public Fact SingleOpenFact(string predicate)
            {
                foreach (Fact fact in World.Knowledge.Facts.Values)
                {
                    if (fact.Predicate == predicate && fact.Truth == TruthState.True)
                    {
                        return fact;
                    }
                }

                return null;
            }

            /// <summary>Hands the player a belief the world already holds, without a conversation.</summary>
            public void Learn(EntityId factId)
            {
                World.Knowledge.Teach(Player, factId, KnowledgeSource.Hearsay, 0.9, Vanilla.Now, false);
            }

            public EntityId Item(string named)
            {
                foreach (ItemDescriptor item in Vanilla.GetInventory(Player))
                {
                    if (item.Name.Contains(named))
                    {
                        return item.Id;
                    }
                }

                return EntityId.None;
            }

            public List<EntityId> Held(EntityId owner)
            {
                List<EntityId> ids = new List<EntityId>();
                foreach (ItemDescriptor item in Vanilla.GetInventory(owner))
                {
                    ids.Add(item.Id);
                }

                return ids;
            }

            public void EmptyThePack()
            {
                foreach (EntityId id in Held(Player))
                {
                    Vanilla.DestroyItem(id);
                }
            }

            /// <summary>Lays one named thing on the shrine, so a test can choose what it is worth.</summary>
            public ActionOutcome Offer(string named)
            {
                ActionContext context = Context(Shrine, EntityId.None);
                context.SubjectItem = Item(named);
                return Actions.Get("make_offering").Perform(context);
            }

            public ActionContext Context(EntityId zone, EntityId target)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target = default)
            {
                return Actions.Get(actionId).Perform(Context(zone, target));
            }

            public Availability Can(string actionId, EntityId zone, EntityId target = default)
            {
                return Actions.Get(actionId).GetAvailability(Context(zone, target));
            }
        }
    }
}
