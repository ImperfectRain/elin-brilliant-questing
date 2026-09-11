using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-009. Wants that arise from what presses on somebody, and that end when it stops.
    ///
    /// The step's whole claim is that nobody has to author a follow-up goal for a person to keep
    /// having wants: state changes, the reading of it changes, and the goal set follows. So the
    /// cases here run the production entry point repeatedly over supplied time and supplied state
    /// changes and assert on the goals that result, rather than on one call's return value.
    ///
    /// Two failures are the ones worth building a fixture for, because both read plausibly in any
    /// dump. The first is omniscience: a goal formed from something true that nobody told this
    /// person. The second is its mirror: a goal closed by something true that nobody told them.
    /// </summary>
    public class ActorGoalEvolutionTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Keeper = EntityId.Parse("npc_keeper");
        private static readonly EntityId Suspect = EntityId.Parse("npc_suspect");
        private static readonly EntityId Twin = EntityId.Parse("npc_twin");
        private static readonly EntityId Accuser = EntityId.Parse("npc_accuser");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Purse = EntityId.Parse("item_purse");
        private static readonly EntityId Occurrence = EntityId.Parse("evt_market_day");

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// A month of passes over one shortage: the want appears, follows the pressure, and ends
        /// when what it wanted comes about. No fixture writes a second goal anywhere in the chain.
        /// </summary>
        [Fact]
        public void AMonthOfPassesCreatesRevisesAndSatisfiesWithoutAuthoredFollowUpGoals()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc keeper = world.Registry.GetNpc(Keeper);
            EntityId source = ShortageAt(world, Town, severity: 40);

            List<GoalChangeKind> seen = new List<GoalChangeKind>();
            for (long day = 0; day <= 30; day++)
            {
                GameTime now = GameTime.FromDays(day);

                if (day == 8)
                {
                    // It gets worse. Nothing else about the world or the person changes.
                    world.Demands.AddOrUpdate(
                        Town, LocalDemandCategory.Food, 85, GameTime.Zero, GameTime.FromDays(60), source);
                }

                if (day == 20)
                {
                    // Somebody brings grain in. The record of the shortage stops being active.
                    world.Demands.Relieve(Town, LocalDemandCategory.Food, source, 100, 0, now);
                }

                foreach (GoalChange change in Pass(world, Keeper, now))
                {
                    seen.Add(change.Kind);
                }
            }

            Assert.Equal(GoalChangeKind.Created, seen.First());
            Assert.Contains(GoalChangeKind.Reweighted, seen);
            Assert.Contains(GoalChangeKind.Satisfied, seen);

            NpcGoal want = Only(keeper, EvolvedGoalKinds.RelieveShortage);
            Assert.Equal(GoalLifecycle.Satisfied, want.Lifecycle);
            Assert.Equal("condition_met", want.RetirementCode);
            Assert.Equal(Town, want.Subject);
            Assert.Equal(GoalConditionKinds.DemandRelieved, want.Condition.Kind);
            Assert.Equal(GoalSourceKind.ActorPressure, want.Origin.Kind);

            // One want across thirty-one passes, not thirty-one copies of one want.
            Assert.Single(keeper.Goals);
        }

        /// <summary>
        /// A true condition this person has no route to produces no want, however much they would
        /// care about it if they knew. The truth is in the graph the whole time.
        /// </summary>
        [Fact]
        public void HiddenTruthAloneCreatesNoGoal()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc stranger = world.Registry.GetNpc(Stranger);

            // Everything except a way of knowing: it is about them, it is true, and it is secret.
            Fact hidden = new Fact(
                world.NewId("fact"), Stranger, FactPredicates.Stole, Purse, "a purse",
                TruthState.True, secrecy: 90, originEvent: Occurrence);
            world.Knowledge.AddFact(hidden);
            world.Obligations.Add(Debt(world, Stranger, Accuser, Occurrence));

            stranger.Sensitivities.PublicEmbarrassment = 1.0;
            stranger.Sensitivities.UnpaidDebt = 1.0;
            stranger.Values.Status.Importance = 1.0;

            for (long day = 0; day <= 30; day++)
            {
                Pass(world, Stranger, GameTime.FromDays(day));
            }

            Assert.DoesNotContain(
                stranger.Goals,
                goal => goal.Origin.Kind == GoalSourceKind.ActorPressure
                        && goal.Condition != null
                        && goal.Condition.Reference("claim") == hidden.Id);
        }

        /// <summary>
        /// A claim that is untrue, sincerely held by the person it names, is a real reason to want
        /// something. The detector emits nothing about it - its rules test for truth and return -
        /// so the want can only come from what this person believes.
        /// </summary>
        [Fact]
        public void AFalseButSincerelyHeldBeliefCreatesAGoal()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            Fact accusation = Accusation(world, TruthState.False);
            Hears(world, Suspect, accusation);

            Assert.Empty(DevelopmentDetector.Detect(world)
                .Where(development => development.FocusFactId == accusation.Id));

            Pass(world, Suspect, GameTime.Zero);

            NpcGoal want = Only(suspect, EvolvedGoalKinds.ClearName);
            Assert.Equal(GoalLifecycle.Active, want.Lifecycle);
            Assert.Equal(accusation.Id, want.Condition.Reference("claim"));
            Assert.Equal(Suspect, want.Condition.Reference("subject"));

            // The provenance says plainly that the world was not holding this too.
            Assert.False(want.Origin.HasObjectiveCause);
            Assert.Equal(accusation.Id, want.Origin.RecordId);
        }

        // -- position, character and knowledge decide, and the pressure never does ----------------

        /// <summary>
        /// Identical knowledge, different people, different wants.
        ///
        /// Both are named in the same true claim, both have heard exactly the same thing, and both
        /// are party to the same undertaking that came out of it. One wants it never shown; the
        /// other wants it made good. Nothing about their positions differs at all, so the only
        /// thing left to explain the difference is who they are.
        /// </summary>
        [Fact]
        public void DifferingValuesDecideDifferentlyOnIdenticalKnowledge()
        {
            Assert.Equal(EvolvedGoalKinds.ClearName, WantOfAnAccusedWho(Conceals));
            Assert.Equal(EvolvedGoalKinds.RepayDebt, WantOfAnAccusedWho(MakesGood));
        }

        private static string WantOfAnAccusedWho(System.Action<NarrativeNpc> character)
        {
            NarrativeWorldState world = Village();
            Fact accusation = Accusation(world, TruthState.True);
            Hears(world, Suspect, accusation);
            world.Obligations.Add(Debt(world, Suspect, Accuser, Occurrence));

            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            character(suspect);

            // The undertaking presses in its own right too, so the reading this compares is the one
            // the claim produces. Both people get exactly the same pair of readings.
            ActorGoalEvolution.Advance(
                world,
                Suspect,
                ActorPressureView.Of(world, Suspect, null),
                GameTime.Zero);

            return Sole(suspect).Kind;
        }

        private static void Conceals(NarrativeNpc npc)
        {
            npc.Personality.Honesty = 0.0;
            npc.Personality.Conventionality = 0.0;
            npc.Personality.Mercy = 0.0;
            npc.Values.Law.Importance = 0.0;
            npc.Sensitivities.PublicEmbarrassment = 1.0;
            npc.Values.Status.Importance = 1.0;
            npc.Personality.Boldness = 1.0;
        }

        private static void MakesGood(NarrativeNpc npc)
        {
            npc.Personality.Honesty = 1.0;
            npc.Personality.Conventionality = 1.0;
            npc.Personality.Mercy = 1.0;
            npc.Values.Law.Importance = 1.0;
            npc.Sensitivities.PublicEmbarrassment = 0.0;
            npc.Values.Status.Importance = 0.0;
            npc.Personality.Boldness = 0.0;
        }

        /// <summary>
        /// Identical people, different knowledge, different wants. The twin is the same character
        /// down to the last weight and has simply not heard.
        /// </summary>
        [Fact]
        public void DifferingKnowledgeDecidesDifferentlyOnIdenticalCharacter()
        {
            NarrativeWorldState world = Village();
            Fact accusation = new Fact(
                world.NewId("fact"), Suspect, FactPredicates.Stole, Purse, "a purse",
                TruthState.False, secrecy: 0, originEvent: Occurrence);
            world.Knowledge.AddFact(accusation);

            Fact aboutTheTwin = new Fact(
                world.NewId("fact"), Twin, FactPredicates.Stole, Purse, "a purse",
                TruthState.False, secrecy: 0, originEvent: Occurrence);
            world.Knowledge.AddFact(aboutTheTwin);

            Alike(world.Registry.GetNpc(Suspect), world.Registry.GetNpc(Twin));

            // One of them has heard of the claim against them. The other has not.
            world.Knowledge.Teach(Suspect, accusation.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);

            Pass(world, Suspect, GameTime.Zero);
            Pass(world, Twin, GameTime.Zero);

            Assert.Equal(EvolvedGoalKinds.ClearName, Sole(world.Registry.GetNpc(Suspect)).Kind);
            Assert.Empty(world.Registry.GetNpc(Twin).Goals);
        }

        /// <summary>
        /// A line somebody holds can leave them under pressure and wanting nothing from it.
        ///
        /// Waiting is a result. The pass says so out loud rather than returning nothing, because a
        /// character who will not take the only move on offer and a matter the vocabulary does not
        /// cover are different things and a consumer must be able to tell them apart.
        /// </summary>
        [Fact]
        public void ALineHeldCanLeaveAnActorUnderPressureAndWantingNothing()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            suspect.NegativeSpace.Declare(PersonalProhibition.NeverInvolvesAuthority, 1.0, breakable: false);

            Fact accusation = Accusation(world, TruthState.False);
            Hears(world, Suspect, accusation);

            IReadOnlyList<GoalChange> changes = Pass(world, Suspect, GameTime.Zero);

            Assert.NotEmpty(ActorPressureView.Of(world, Suspect, null));
            Assert.Equal(GoalChangeKind.NoResponse, Assert.Single(changes).Kind);
            Assert.Empty(suspect.Goals);
        }

        // -- retirement --------------------------------------------------------------------------

        /// <summary>
        /// The mirror of the omniscience rule. The condition is objectively satisfied from the
        /// first pass - nobody anywhere can demonstrate the claim - and its owner goes on wanting
        /// it, because being under pressure about something is not the same as it being so, and a
        /// want that closed itself on the save's answer would be the world telling them.
        /// </summary>
        [Fact]
        public void AGoalIsNotClosedByATruthItsOwnerHasNoRouteTo()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            Fact accusation = Accusation(world, TruthState.False);
            Hears(world, Suspect, accusation);

            for (long day = 0; day <= 20; day++)
            {
                Pass(world, Suspect, GameTime.FromDays(day));
            }

            NpcGoal want = Sole(suspect);
            Assert.Equal(GoalConditionState.Met, want.Evaluate(world));
            Assert.Equal(GoalLifecycle.Active, want.Lifecycle);
            Assert.Equal(GoalAssessment.Unknown, want.ActorAssessment);
        }

        /// <summary>
        /// A want whose cause stops pressing and whose condition did not come about ends as
        /// abandoned rather than as satisfied. A debt that was broken is not a debt that was paid,
        /// and handing a consumer one for the other misreads the person permanently.
        /// </summary>
        [Fact]
        public void AWantWhoseConditionNeverCameAboutLapsesRatherThanSatisfying()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            SocialObligation debt = world.Obligations.Add(Debt(world, Suspect, Accuser, Occurrence));

            Pass(world, Suspect, GameTime.Zero);
            NpcGoal want = Sole(suspect);
            Assert.Equal(EvolvedGoalKinds.RepayDebt, want.Kind);
            Assert.Equal(Accuser, want.Subject);

            debt.Break(GameTime.FromDays(4));
            Pass(world, Suspect, GameTime.FromDays(4));

            Assert.Equal(GoalLifecycle.Abandoned, want.Lifecycle);
            Assert.Equal("lapsed", want.RetirementCode);
            Assert.Equal(GoalConditionState.Unmet, want.Evaluate(world));
        }

        /// <summary>
        /// The same undertaking, settled rather than broken, ends as satisfied.
        /// </summary>
        [Fact]
        public void AWantWhoseConditionCameAboutIsSatisfied()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            SocialObligation debt = world.Obligations.Add(Debt(world, Suspect, Accuser, Occurrence));

            Pass(world, Suspect, GameTime.Zero);
            NpcGoal want = Sole(suspect);

            debt.Fulfill(GameTime.FromDays(6));
            Pass(world, Suspect, GameTime.FromDays(6));

            Assert.Equal(GoalLifecycle.Satisfied, want.Lifecycle);
            Assert.Equal(GoalAssessment.BelievedMet, want.ActorAssessment);
            Assert.True(want.Satisfied);
        }

        /// <summary>
        /// Under unchanged pressure one person lets a want go and another keeps it, and the one who
        /// let it go does not quietly take it back on the next morning.
        ///
        /// Giving up has to cost something or it means nothing: a pass that re-forms the want it
        /// just retired would leave an actor who gave up pursuing it anyway, and would fill the
        /// history with a retirement a day. The wait is read off the retirement the goal already
        /// records, so somebody may well come back to it much later - that is a change of heart and
        /// is the point, and what is ruled out here is the same pass undoing itself.
        /// </summary>
        [Fact]
        public void AnImpatientActorGivesUpWhereAPatientOneHoldsOn()
        {
            NarrativeWorldState world = Village();
            ShortageAt(world, Town, severity: 30);
            NarrativeNpc quick = world.Registry.GetNpc(Keeper);
            NarrativeNpc steady = world.Registry.GetNpc(Twin);

            quick.Personality.Patience = 0.0;
            quick.Personality.Earnestness = 0.0;
            steady.Personality.Patience = 1.0;
            steady.Personality.Earnestness = 1.0;

            bool gaveUp = false;
            bool tookItStraightBack = false;
            bool steadyEverRetired = false;

            for (long day = 0; day <= 30; day++)
            {
                IReadOnlyList<GoalChange> hers = Pass(world, Keeper, GameTime.FromDays(day));

                if (gaveUp && hers.Any(change => change.Kind == GoalChangeKind.Created))
                {
                    tookItStraightBack = true;
                }

                gaveUp = hers.Any(change =>
                    change.Kind == GoalChangeKind.Abandoned && change.Because == "gave_up");

                steadyEverRetired |= Pass(world, Twin, GameTime.FromDays(day))
                    .Any(change => change.Kind == GoalChangeKind.Abandoned
                                   || change.Kind == GoalChangeKind.Satisfied);
            }

            Assert.False(tookItStraightBack, "a want was re-formed the pass after it was given up");
            Assert.Contains(
                quick.Goals,
                goal => goal.Kind == EvolvedGoalKinds.RelieveShortage
                        && goal.Lifecycle == GoalLifecycle.Abandoned
                        && goal.RetirementCode == "gave_up");

            // Nothing closed itself: every want she ever held here ended by her letting it go.
            Assert.All(
                quick.Goals.Where(goal => !goal.IsActive),
                goal => Assert.Equal("gave_up", goal.RetirementCode));

            // The patient one is still at it, unretired, on the identical pressure.
            Assert.False(steadyEverRetired);
            Assert.Equal(GoalLifecycle.Active, Only(steady, EvolvedGoalKinds.RelieveShortage).Lifecycle);
        }

        /// <summary>
        /// One cause appraised again comes out as a different want, and the want it replaces is
        /// superseded rather than left standing beside it or deleted.
        /// </summary>
        [Fact]
        public void ACauseThatIsReappraisedSupersedesRatherThanStacking()
        {
            NarrativeWorldState world = Village();
            NarrativeNpc suspect = world.Registry.GetNpc(Suspect);
            suspect.Personality.Honesty = 1.0;
            suspect.Personality.Conventionality = 1.0;
            suspect.Personality.Mercy = 1.0;
            suspect.Values.Law.Importance = 1.0;
            suspect.Sensitivities.PublicEmbarrassment = 0.0;
            suspect.Values.Status.Importance = 0.0;
            suspect.Personality.Boldness = 0.0;

            Fact accusation = Accusation(world, TruthState.True);
            Hears(world, Suspect, accusation);

            Pass(world, Suspect, GameTime.Zero);
            NpcGoal first = Sole(suspect);
            Assert.Equal(EvolvedGoalKinds.ClearName, first.Kind);

            // The matter grows an undertaking of their own, out of the same occurrence.
            world.Obligations.Add(Debt(world, Suspect, Accuser, Occurrence));
            IReadOnlyList<GoalChange> changes = Pass(world, Suspect, GameTime.FromDays(2));

            Assert.Contains(changes, change => change.Kind == GoalChangeKind.Superseded);
            Assert.Equal(GoalLifecycle.Superseded, first.Lifecycle);

            NpcGoal second = Only(suspect, EvolvedGoalKinds.RepayDebt);
            Assert.Equal(second.Identity, first.SupersededBy);
            Assert.Equal(GoalLifecycle.Active, second.Lifecycle);
        }

        /// <summary>
        /// Competing wants stay bounded and stay ordered by how hard each presses on this person.
        /// Automatic formation runs forever, so the active set may not grow with playtime.
        /// </summary>
        [Fact]
        public void CompetingWantsAreCappedAndWeighted()
        {
            NarrativeWorldState world = Village();
            for (int i = 0; i < NpcGoalCollection.MaxActive + 6; i++)
            {
                EntityId creditor = EntityId.Parse("npc_creditor_" + i);
                world.Registry.Add(new NarrativeNpc(creditor, "creditor " + i));
                world.Obligations.Add(new SocialObligation(
                    world.NewId("obl"),
                    SocialObligationKind.Favor,
                    Keeper,
                    creditor,
                    EntityId.None,
                    "a day's work",
                    GameTime.Zero,
                    EntityId.Parse("evt_" + i),
                    strength: 1 + i));
            }

            Pass(world, Keeper, GameTime.Zero);

            NarrativeNpc keeper = world.Registry.GetNpc(Keeper);
            int active = keeper.Goals.Count(goal => goal.IsActive);
            Assert.True(active <= NpcGoalCollection.MaxActive, active + " active wants");
            Assert.All(keeper.Goals.Where(goal => goal.IsActive), goal => Assert.InRange(goal.Weight, 0, 100));
            Assert.Contains(keeper.Goals, goal => !goal.IsActive && goal.RetirementCode == "crowded_out");
        }

        /// <summary>
        /// A want formed by a pass goes on behaving like one across a reload.
        ///
        /// Evolution stores nothing of its own: when somebody formed a want and when they let one
        /// go are the goal's own saved provenance and retirement, and both decisions that read them
        /// - how long this person has it in them to keep a want, and how long before they would
        /// take a given-up one back on - are computed from those each pass. So the property to pin
        /// is that a reloaded actor continues the same way rather than starting their patience over.
        /// </summary>
        [Fact]
        public void AWantFormedByAPassContinuesAcrossAReload()
        {
            NarrativeWorldState world = Village();
            EntityId source = ShortageAt(world, Town, severity: 30);
            world.Registry.GetNpc(Keeper).Personality.Patience = 0.0;
            world.Registry.GetNpc(Keeper).Personality.Earnestness = 0.0;

            Pass(world, Keeper, GameTime.Zero);
            NpcGoal before = Sole(world.Registry.GetNpc(Keeper));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            NpcGoal after = Sole(reloaded.Registry.GetNpc(Keeper));

            Assert.Equal(before.Identity, after.Identity);
            Assert.Equal(before.Origin.FormedAt, after.Origin.FormedAt);
            Assert.Equal(before.Origin.SourceId, after.Origin.SourceId);
            Assert.Equal(before.Origin.RecordId, after.Origin.RecordId);

            // Day twenty on the reloaded save: the clock was running the whole time, so this is a
            // want given up rather than a want formed fresh.
            Pass(reloaded, Keeper, GameTime.FromDays(20));
            Assert.Equal(GoalLifecycle.Abandoned, after.Lifecycle);
            Assert.Equal("gave_up", after.RetirementCode);

            // And the same pass did not redispatch anything or invent a second want for it.
            Assert.Single(reloaded.Registry.GetNpc(Keeper).Goals);
            Assert.Equal(GoalConditionState.Unmet, after.Evaluate(reloaded));
            Assert.Equal(source, after.Condition.Reference("source"));
        }

        // -- fixture -----------------------------------------------------------------------------

        private static IReadOnlyList<GoalChange> Pass(NarrativeWorldState world, EntityId actorId, GameTime now)
        {
            return ActorGoalEvolution.Advance(
                world, actorId, ActorPressureView.Of(world, actorId, DevelopmentDetector.Detect(world)), now);
        }

        private static NpcGoal Sole(NarrativeNpc actor) => Assert.Single(actor.Goals);

        private static NpcGoal Only(NarrativeNpc actor, string kind) =>
            Assert.Single(actor.Goals.Where(goal => goal.Kind == kind));

        private static EntityId ShortageAt(NarrativeWorldState world, EntityId place, int severity)
        {
            Fact source = new Fact(world.NewId("fact"), place, FactPredicates.Needs, EntityId.None, "grain");
            world.Knowledge.AddFact(source);
            world.Demands.AddOrUpdate(
                place, LocalDemandCategory.Food, severity, GameTime.Zero, GameTime.FromDays(60), source.Id);
            return source.Id;
        }

        private static Fact Accusation(NarrativeWorldState world, TruthState truth)
        {
            Fact accusation = new Fact(
                world.NewId("fact"), Suspect, FactPredicates.Stole, Purse, "a purse",
                truth, secrecy: 0, originEvent: Occurrence);
            world.Knowledge.AddFact(accusation);
            return accusation;
        }

        private static void Hears(NarrativeWorldState world, EntityId actorId, Fact claim)
        {
            world.Knowledge.Teach(actorId, claim.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);
        }

        private static SocialObligation Debt(
            NarrativeWorldState world, EntityId debtor, EntityId creditor, EntityId occurrence)
        {
            return new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Favor,
                debtor,
                creditor,
                EntityId.None,
                "makes it good",
                GameTime.Zero,
                occurrence);
        }

        private static void Alike(NarrativeNpc left, NarrativeNpc right)
        {
            foreach (NarrativeNpc npc in new[] { left, right })
            {
                npc.Personality.Honesty = 0.1;
                npc.Personality.Conventionality = 0.1;
                npc.Personality.Boldness = 0.9;
                npc.Sensitivities.PublicEmbarrassment = 0.9;
                npc.Values.Status.Importance = 0.9;
                npc.HomeSiteId = Town;
            }
        }

        private static NarrativeWorldState Village()
        {
            NarrativeWorldState world = new NarrativeWorldState(11);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeNpc(Keeper, "Mira") { Occupation = "shopkeeper", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Suspect, "Bran") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Twin, "Bral") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Accuser, "Hald") { Occupation = "reeve", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Stranger, "Orvo") { HomeSiteId = Town });
            return world;
        }
    }
}
