using System;
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
    /// BQa-008. What a goal has to be able to answer before anything starts generating goals
    /// automatically.
    ///
    /// A goal used to be a kind string, a subject, a weight, a sentence for a person to read and a
    /// boolean. Every question a later system needs answered - is this the same want I formed last
    /// pass, what would actually satisfy it, why does this person have it, did they give up or did
    /// something else take it over - had exactly one place to be answered from, and that place was
    /// the sentence. This suite is about the four answers now having somewhere of their own, and
    /// about the sentence staying a sentence.
    /// </summary>
    public class NpcGoalContractTests
    {
        private static readonly EntityId Merchant = EntityId.Parse("npc_merchant");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Reeve = EntityId.Parse("npc_reeve");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");
        private static readonly EntityId Town = EntityId.Parse("zone_town");

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// The same want read twice is one goal, and a different want is a different goal.
        ///
        /// Automatic formation runs every pass over a world that has not stopped being the way it
        /// is, so "the pressure is still there" arrives again and again. Identity is derived from
        /// the kind, the subject and the exact condition, which is why the second pass lands on the
        /// goal the first one formed instead of stacking a second copy beside it - and why a goal
        /// about a different object does not collide with it.
        /// </summary>
        [Fact]
        public void EquivalentWantsDoNotAccumulateAcrossRepeatedPasses()
        {
            NarrativeNpc merchant = new NarrativeNpc(Merchant, "Halvar");
            GoalCondition ringIsMine = GoalConditionRegistry.Create(
                GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", Ring),
                new GoalBinding("owner", Merchant));

            NpcGoal first = merchant.Goals.Adopt(
                new NpcGoal("recover_property", Ring, 80, "the ring was my mother's", ringIsMine));

            for (int pass = 0; pass < 5; pass++)
            {
                NpcGoal again = merchant.Goals.Adopt(
                    new NpcGoal("recover_property", Ring, 90, "a different sentence entirely", ringIsMine));
                Assert.Same(first, again);
            }

            Assert.Single(merchant.Goals);

            // Bindings in the other order are the same condition; a different object is not.
            NpcGoal reordered = merchant.Goals.Adopt(new NpcGoal(
                "recover_property", Ring, 80, string.Empty,
                new GoalCondition(GoalConditionKinds.PropertyOwnedBy, new[]
                {
                    new GoalBinding("owner", Merchant),
                    new GoalBinding("item", Ring)
                })));
            Assert.Same(first, reordered);

            NpcGoal aboutACart = merchant.Goals.Adopt(new NpcGoal(
                "recover_property", EntityId.Parse("item_cart"), 80, string.Empty,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.PropertyOwnedBy,
                    new GoalBinding("item", EntityId.Parse("item_cart")),
                    new GoalBinding("owner", Merchant))));
            Assert.NotSame(first, aboutACart);
            Assert.Equal(2, merchant.Goals.Count);
        }

        /// <summary>
        /// Four states, four different things to have happened, and none of them deletes the goal.
        ///
        /// "Stopped wanting it" is not one fact. A merchant whose cargo came back, one who gave up,
        /// and one who decided to sue instead have each stopped pursuing the same goal, and a
        /// consumer handed any of the three for another draws the wrong conclusion about the
        /// person. Retirement keeps the record: the goal leaves the active set and stays in the
        /// history where a later matter can read what became of it.
        /// </summary>
        [Fact]
        public void AGoalIsDistinguishableAsActiveSatisfiedAbandonedOrSuperseded()
        {
            NarrativeNpc merchant = new NarrativeNpc(Merchant, "Halvar");
            NpcGoal recovered = new NpcGoal("recover_property", Ring, 80);
            NpcGoal givenUp = new NpcGoal("recover_money", Thief, 60);
            NpcGoal replaced = new NpcGoal("clear_name", Merchant, 70);
            NpcGoal replacement = new NpcGoal("sue", Thief, 75);
            foreach (NpcGoal goal in new[] { recovered, givenUp, replaced, replacement })
            {
                merchant.Goals.Add(goal);
            }

            Assert.All(merchant.Goals, goal => Assert.True(goal.IsActive));

            recovered.Satisfy(GameTime.FromDays(3), "returned");
            givenUp.Abandon(GameTime.FromDays(4), "gave_up");
            replaced.SupersedeWith(replacement, GameTime.FromDays(5), "went_to_law");

            Assert.Equal(GoalLifecycle.Satisfied, recovered.Lifecycle);
            Assert.Equal(GoalLifecycle.Abandoned, givenUp.Lifecycle);
            Assert.Equal(GoalLifecycle.Superseded, replaced.Lifecycle);
            Assert.Equal(GoalLifecycle.Active, replacement.Lifecycle);
            Assert.Equal(replacement.Identity, replaced.SupersededBy);
            Assert.Equal("returned", recovered.RetirementCode);
            Assert.Equal(GameTime.FromDays(4), givenUp.RetiredAt);

            // Retired is not deleted, and only the active one counts as still wanted.
            Assert.Equal(4, merchant.Goals.Count);
            Assert.Single(merchant.Goals.Where(goal => goal.IsActive));
            Assert.Same(replacement, merchant.Goals.FindActive(replacement.Identity));
            Assert.Null(merchant.Goals.FindActive(recovered.Identity));

            // The boolean the rest of the simulation has always used is a view of the lifecycle,
            // not a second opinion about it.
            Assert.True(recovered.Satisfied);
            Assert.False(givenUp.Satisfied);
            Assert.False(replaced.Satisfied);
        }

        /// <summary>
        /// What would satisfy it, and why it exists, come back off disk unchanged.
        ///
        /// Both are answers a later pass has to be able to give without the world that produced
        /// them still being in memory, so both are saved. What is saved is references: the id of
        /// the reading that caused the goal and the ids it is about, never a copy of the pressure,
        /// which is derived and recomputed and would go stale the moment the world moved.
        /// </summary>
        [Fact]
        public void ConditionAndCausalSourceSurviveSaveAndReload()
        {
            NarrativeWorldState world = Theft();
            NarrativeNpc merchant = world.Registry.GetNpc(Merchant);

            ActorLocalPressure pressure = ActorPressureView
                .Of(world, Merchant, DevelopmentDetector.Detect(world))
                .First();

            NpcGoal formed = merchant.Goals.Adopt(new NpcGoal(
                "settle_the_debt",
                Thief,
                72,
                "a sentence for a person, and for nobody else",
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ObligationDischarged,
                    new GoalBinding("obligation", Debt(world).Id)),
                GoalOrigin.FromPressure(pressure, GameTime.FromDays(2))));
            formed.ActorAssessment = GoalAssessment.BelievedUnmet;
            formed.Abandon(GameTime.FromDays(9), "too_costly");

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            NpcGoal read = reloaded.Registry.GetNpc(Merchant).Goals.Single();

            Assert.Equal(formed.Identity, read.Identity);
            Assert.Equal(GoalConditionKinds.ObligationDischarged, read.Condition.Kind);
            Assert.Equal(Debt(world).Id, read.Condition.Reference("obligation"));
            Assert.Equal(GoalSourceKind.ActorPressure, read.Origin.Kind);
            Assert.Equal(pressure.Id, read.Origin.SourceId);
            Assert.Equal(pressure.DevelopmentId, read.Origin.ObjectiveCauseId);
            Assert.True(read.Origin.HasObjectiveCause);
            Assert.Equal(GameTime.FromDays(2), read.Origin.FormedAt);
            Assert.Equal(GoalLifecycle.Abandoned, read.Lifecycle);
            Assert.Equal(GameTime.FromDays(9), read.RetiredAt);
            Assert.Equal("too_costly", read.RetirementCode);
            Assert.Equal(GoalAssessment.BelievedUnmet, read.ActorAssessment);
            Assert.Equal(formed.Reason, read.Reason);

            // The reloaded goal is evaluable against the reloaded world, which is the point of
            // saving the binding rather than the reading it came from.
            Assert.Equal(GoalConditionState.Unmet, read.Evaluate(reloaded));
            Assert.Equal(GoalConditionState.Unmet, read.Evaluate(world));
        }

        /// <summary>
        /// A goal is still the same goal when its sentence is gibberish, and still evaluates the
        /// same way.
        ///
        /// `Reason` is for a person reading an inspector. Nothing may key off it, because the
        /// moment something does, prose becomes an input language and every later change to the
        /// wording is a behaviour change nobody can see coming. The check is behavioural rather
        /// than a grep: identity, deduplication, lifecycle and satisfaction all come out identical
        /// for two goals whose sentences have nothing in common.
        /// </summary>
        [Fact]
        public void NoDecisionReadsTheHumanReadableReason()
        {
            NarrativeWorldState world = Theft();
            GoalCondition condition = GoalConditionRegistry.Create(
                GoalConditionKinds.ObligationDischarged,
                new GoalBinding("obligation", Debt(world).Id));

            NpcGoal plain = new NpcGoal("settle_the_debt", Thief, 50, string.Empty, condition);
            NpcGoal florid = new NpcGoal(
                "settle_the_debt", Thief, 50,
                "satisfied recovered abandoned superseded - every word a parser might key off",
                condition);

            Assert.Equal(plain.Identity, florid.Identity);
            Assert.Equal(plain.Evaluate(world), florid.Evaluate(world));

            NarrativeNpc merchant = world.Registry.GetNpc(Merchant);
            Assert.Same(merchant.Goals.Adopt(plain), merchant.Goals.Adopt(florid));

            // Prose says nothing about state: the sentence claims it is done, the lifecycle does not.
            Assert.False(plain.Satisfied);
            Assert.True(plain.IsActive);
        }

        // -- the vocabulary ----------------------------------------------------------------------

        /// <summary>
        /// Conditions are registered terms with concrete bindings, and production cannot mint one
        /// the vocabulary does not admit.
        ///
        /// The alternative - an expression, or a central switch on goal names - is what BQa-010 and
        /// BQa-011 have to be able to add verbs without editing. Refusal happens where the
        /// condition is written, so a mis-shaped desire never reaches a save to be read back as
        /// something nothing can answer.
        /// </summary>
        [Fact]
        public void OnlyRegisteredTermsWithTheirOwnBindingsAreAdmitted()
        {
            Assert.True(GoalConditionRegistry.IsRegistered(GoalConditionKinds.ClaimUnproven));
            Assert.False(GoalConditionRegistry.IsRegistered("whatever.the.author.felt.like"));

            Assert.Throws<ArgumentException>(() => GoalConditionRegistry.Create(
                "whatever.the.author.felt.like", new GoalBinding("item", Ring)));

            Assert.Throws<ArgumentException>(() => GoalConditionRegistry.Create(
                GoalConditionKinds.PropertyOwnedBy, new GoalBinding("item", Ring)));

            Assert.Throws<ArgumentException>(() => GoalConditionRegistry.Create(
                GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", Ring),
                new GoalBinding("thief", Thief)));

            // Bindings are concrete entities and codes, not blanks and not sentences.
            Assert.Throws<ArgumentException>(() => new GoalBinding("item", EntityId.None));
            Assert.Throws<ArgumentException>(() => new GoalBinding("the item in question", Ring));
            Assert.Throws<ArgumentException>(() => new GoalCondition(
                GoalConditionKinds.PropertyOwnedBy,
                new[] { new GoalBinding("item", Ring), new GoalBinding("item", Thief) }));
        }

        /// <summary>
        /// Every registered term reads authoritative state, and says "cannot answer" rather than
        /// guessing when the record it needs is not there.
        ///
        /// The unsupported answer is the load-bearing one. A shortage term that reports relief
        /// because it found no pressure, or an ownership term that reports somebody else's because
        /// nothing recorded an owner, would each be a confident wrong answer - and a goal system
        /// acting on confident wrong answers is worse than one that waits.
        /// </summary>
        [Fact]
        public void RegisteredTermsReadStateAndRefuseToGuess()
        {
            NarrativeWorldState world = Theft();
            SocialObligation debt = Debt(world);
            Fact theft = world.Knowledge.Facts.Values.Single(fact => fact.Predicate == FactPredicates.Stole);

            Assert.Equal(GoalConditionState.Met, Read(world, GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", Ring), new GoalBinding("owner", Merchant)));
            Assert.Equal(GoalConditionState.Unmet, Read(world, GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", Ring), new GoalBinding("owner", Thief)));
            Assert.Equal(GoalConditionState.Unsupported, Read(world, GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", EntityId.Parse("item_unknown")), new GoalBinding("owner", Merchant)));

            Assert.Equal(GoalConditionState.Unmet, Read(world, GoalConditionKinds.ObligationDischarged,
                new GoalBinding("obligation", debt.Id)));
            debt.Fulfill(GameTime.FromDays(6));
            Assert.Equal(GoalConditionState.Met, Read(world, GoalConditionKinds.ObligationDischarged,
                new GoalBinding("obligation", debt.Id)));
            Assert.Equal(GoalConditionState.Unsupported, Read(world, GoalConditionKinds.ObligationDischarged,
                new GoalBinding("obligation", EntityId.Parse("obl_unknown"))));

            // The thief's own knowledge of his own deed never exposes him; the reeve's proof does.
            Assert.Equal(GoalConditionState.Met, Read(world, GoalConditionKinds.ClaimUnproven,
                new GoalBinding("claim", theft.Id), new GoalBinding("subject", Thief)));
            world.Knowledge.Teach(Reeve, theft.Id, KnowledgeSource.Witnessed, 0.9, GameTime.Zero, canProve: true);
            Assert.Equal(GoalConditionState.Unmet, Read(world, GoalConditionKinds.ClaimUnproven,
                new GoalBinding("claim", theft.Id), new GoalBinding("subject", Thief)));

            Assert.Equal(GoalConditionState.Met, Read(world, GoalConditionKinds.PersonAlive,
                new GoalBinding("person", Merchant)));
            world.Registry.GetNpc(Merchant).Alive = false;
            Assert.Equal(GoalConditionState.Unmet, Read(world, GoalConditionKinds.PersonAlive,
                new GoalBinding("person", Merchant)));
            Assert.Equal(GoalConditionState.Unsupported, Read(world, GoalConditionKinds.PersonAlive,
                new GoalBinding("person", EntityId.Parse("npc_nobody"))));

            Fact shortage = new Fact(
                world.NewId("fact"), Town, FactPredicates.Needs, EntityId.None, "grain");
            world.Knowledge.AddFact(shortage);
            world.Demands.AddOrUpdate(Town, LocalDemandCategory.Food, 40, GameTime.Zero, GameTime.FromDays(20), shortage.Id);
            Assert.Equal(GoalConditionState.Unmet, Read(world, GoalConditionKinds.DemandRelieved,
                new GoalBinding("place", Town), new GoalBinding("source", shortage.Id)));
            world.Demands.Relieve(Town, LocalDemandCategory.Food, shortage.Id, 40, 0, GameTime.FromDays(7));
            Assert.Equal(GoalConditionState.Met, Read(world, GoalConditionKinds.DemandRelieved,
                new GoalBinding("place", Town), new GoalBinding("source", shortage.Id)));

            // No record of the shortage at all is not the same as a shortage that has passed.
            Assert.Equal(GoalConditionState.Unsupported, Read(world, GoalConditionKinds.DemandRelieved,
                new GoalBinding("place", Town), new GoalBinding("source", EntityId.Parse("fact_unknown"))));
        }

        /// <summary>
        /// A desire nothing can evaluate stays a desire, inspectable and unanswered.
        ///
        /// Two cases arrive here: a goal saved before conditions existed, and a goal saved by a
        /// build that knew a term this one does not. Both must load, both must be visible, and
        /// neither may be quietly mapped onto the nearest term that happens to be registered -
        /// which is the guess that turns "I do not know what he wanted" into a wrong action.
        /// </summary>
        [Fact]
        public void UnsupportedDesiresStayInspectableAndUnanswered()
        {
            NarrativeWorldState world = Theft();
            NarrativeNpc merchant = world.Registry.GetNpc(Merchant);

            NpcGoal legacy = new NpcGoal("recover_property", Ring, 80, "written before conditions existed");
            NpcGoal fromTheFuture = new NpcGoal(
                "keep_the_shrine", Town, 50, string.Empty,
                new GoalCondition("shrine.tended", new[] { new GoalBinding("shrine", Town) }));
            merchant.Goals.Add(legacy);
            merchant.Goals.Add(fromTheFuture);

            Assert.False(legacy.HasCondition);
            Assert.Equal(GoalConditionState.Unsupported, legacy.Evaluate(world));
            Assert.True(fromTheFuture.HasCondition);
            Assert.Equal(GoalConditionState.Unsupported, fromTheFuture.Evaluate(world));
            Assert.False(GoalConditionRegistry.IsSupported(fromTheFuture.Condition));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            IReadOnlyList<NpcGoal> read = reloaded.Registry.GetNpc(Merchant).Goals;
            Assert.Equal(2, read.Count);
            Assert.False(read[0].HasCondition);
            Assert.Equal("shrine.tended", read[1].Condition.Kind);
            Assert.Equal(Town, read[1].Condition.Reference("shrine"));
            Assert.Equal(GoalConditionState.Unsupported, read[1].Evaluate(reloaded));

            // Both are still readable as what they are, and neither claims to be finished.
            Assert.Contains("recover_property", read[0].ToString());
            Assert.Contains("shrine.tended", read[1].ToString());
            Assert.All(read, goal => Assert.True(goal.IsActive));
        }

        // -- the separations the later steps depend on --------------------------------------------

        /// <summary>
        /// The world's answer and the owner's belief are two fields, and evaluating one never sets
        /// the other.
        ///
        /// Another actor can settle a debt, return a ring or bury a proof without telling anybody.
        /// If reading the condition moved the goal's lifecycle, every actor would learn the truth
        /// the instant a background pass ran, which is the omniscience the whole layer exists to
        /// prevent. Satisfaction is something a legitimate owner does to the goal, not something
        /// the evaluator does behind them.
        /// </summary>
        [Fact]
        public void ObjectiveSatisfactionNeverMovesTheGoalOrTheOwnersBelief()
        {
            NarrativeWorldState world = Theft();
            SocialObligation debt = Debt(world);
            NpcGoal goal = new NpcGoal(
                "settle_the_debt", Thief, 70, string.Empty,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.ObligationDischarged, new GoalBinding("obligation", debt.Id)));
            goal.ActorAssessment = GoalAssessment.BelievedUnmet;
            world.Registry.GetNpc(Merchant).Goals.Adopt(goal);

            // Somebody else settles it, off screen.
            debt.Forgive(GameTime.FromDays(5));

            Assert.Equal(GoalConditionState.Met, goal.Evaluate(world));
            Assert.Equal(GoalLifecycle.Active, goal.Lifecycle);
            Assert.False(goal.Satisfied);
            Assert.Equal(GoalAssessment.BelievedUnmet, goal.ActorAssessment);
            Assert.Same(goal, world.Registry.GetNpc(Merchant).Goals.FindActive(goal.Identity));

            // And the reverse: an owner can believe a thing is done that is not.
            debt.Restore(SocialObligationStatus.Open, GameTime.Zero);
            goal.ActorAssessment = GoalAssessment.BelievedMet;
            Assert.Equal(GoalConditionState.Unmet, goal.Evaluate(world));
            Assert.Equal(GoalAssessment.BelievedMet, goal.ActorAssessment);
        }

        /// <summary>
        /// Provenance is references, never a copy of the reading.
        ///
        /// The pressure a goal came from is derived and recomputed every pass. Storing a snapshot
        /// of it here would freeze one pass's urgency, stakes and certainty into the save and let a
        /// goal go on disagreeing with the world forever. What is kept is the reading's id, whether
        /// the world was objectively holding it, and the record it focused on.
        /// </summary>
        [Fact]
        public void ProvenanceCarriesReferencesAndSaysWhenThereIsNoObjectiveCause()
        {
            NarrativeWorldState world = Theft();
            ActorLocalPressure pressure = ActorPressureView
                .Of(world, Merchant, DevelopmentDetector.Detect(world))
                .First();

            GoalOrigin origin = GoalOrigin.FromPressure(pressure, GameTime.FromDays(1));
            Assert.Equal(GoalSourceKind.ActorPressure, origin.Kind);
            Assert.Equal(pressure.Id, origin.SourceId);
            Assert.Equal(pressure.FocusFactId, origin.RecordId);
            Assert.True(origin.HasObjectiveCause);

            // A sincerely mistaken belief has no objective cause, and the record says so rather
            // than inventing one.
            GoalOrigin believed = new GoalOrigin(
                GoalSourceKind.ActorPressure, "belief_only", string.Empty, EntityId.Parse("fact_rumour"));
            Assert.False(believed.HasObjectiveCause);

            // Nothing recorded a cause stays nothing recorded: unknown is not filled in later.
            NpcGoal established = new NpcGoal("keep_the_peace", Town, 40);
            Assert.Equal(GoalSourceKind.Unknown, established.Origin.Kind);
            Assert.Equal(string.Empty, established.Origin.SourceId);
            Assert.False(established.Origin.HasObjectiveCause);
        }

        // -- bounds --------------------------------------------------------------------------------

        /// <summary>
        /// One person's wants are bounded, and the bound retires rather than discards.
        ///
        /// Automatic formation runs forever, so without a cap a fifty-hour save grows a character
        /// whose goal list is longer than their history. The weakest want gives way, and it gives
        /// way into the retired history where it can still be read - which is also what keeps the
        /// history itself from becoming the unbounded list instead.
        /// </summary>
        [Fact]
        public void ActiveWantsAndRetiredHistoryAreBothBounded()
        {
            NarrativeNpc merchant = new NarrativeNpc(Merchant, "Halvar");
            List<NpcGoal> adopted = new List<NpcGoal>();
            for (int i = 0; i < NpcGoalCollection.MaxActive + NpcGoalCollection.MaxRetained + 8; i++)
            {
                adopted.Add(merchant.Goals.Adopt(new NpcGoal("want_" + i, Ring, 10 + i)));
            }

            Assert.Equal(NpcGoalCollection.MaxActive, merchant.Goals.Count(goal => goal.IsActive));
            Assert.True(merchant.Goals.Count(goal => !goal.IsActive) <= NpcGoalCollection.MaxRetained);

            // The weakest gave way, and it gave way as a retirement with a reason, not a deletion.
            Assert.Equal(GoalLifecycle.Abandoned, adopted[0].Lifecycle);
            Assert.Equal("crowded_out", adopted[0].RetirementCode);
            Assert.All(merchant.Goals.Where(goal => goal.IsActive), goal => Assert.True(goal.Weight >= 10 + 8));

            // A hand-established scenario is not automatic formation and is left exactly as authored.
            NarrativeNpc reeve = new NarrativeNpc(Reeve, "Hald");
            for (int i = 0; i < NpcGoalCollection.MaxActive + 4; i++)
            {
                reeve.Goals.Add(new NpcGoal("authored_" + i, Town, 50));
            }

            Assert.Equal(NpcGoalCollection.MaxActive + 4, reeve.Goals.Count(goal => goal.IsActive));
        }

        /// <summary>
        /// Retiring a goal writes nothing into history and replays nothing on load.
        ///
        /// A goal is current state. What happened to it belongs to the event ledger, and a
        /// lifecycle field that also tried to be a log would give a fifty-hour save one durable
        /// record per pass that changed nothing.
        /// </summary>
        [Fact]
        public void RetiringAGoalDoesNotTouchTheLedgerOrRewriteHistory()
        {
            NarrativeWorldState world = Theft();
            NarrativeNpc merchant = world.Registry.GetNpc(Merchant);
            int events = world.Ledger.Events.Count;

            NpcGoal goal = merchant.Goals.Adopt(new NpcGoal("recover_property", Ring, 80));
            goal.Satisfy(GameTime.FromDays(4), "returned");
            goal.Abandon(GameTime.FromDays(5), "changed_mind");

            Assert.Equal(events, world.Ledger.Events.Count);
            Assert.Equal(GoalLifecycle.Abandoned, goal.Lifecycle);
            Assert.Equal(GameTime.FromDays(5), goal.RetiredAt);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Assert.Equal(events, reloaded.Ledger.Events.Count);

            // A reload is a fixed point: loading and saving again changes nothing, so retirement
            // state is read back as written rather than drifting a little on every load.
            string canonical = WorldStateSerializer.Save(reloaded);
            Assert.Equal(canonical, WorldStateSerializer.Save(WorldStateSerializer.Load(canonical)));
        }

        /// <summary>
        /// A goal that turned out not to be done goes back to being wanted, and says nothing
        /// misleading while it is.
        /// </summary>
        [Fact]
        public void AReopenedGoalClearsTheRetirementThatNoLongerApplies()
        {
            NpcGoal goal = new NpcGoal("recover_property", Ring, 80);
            goal.SupersedeWith(new NpcGoal("sue", Thief, 60), GameTime.FromDays(2), "went_to_law");
            Assert.NotEqual(string.Empty, goal.SupersededBy);

            goal.Reopen();
            Assert.True(goal.IsActive);
            Assert.Equal(string.Empty, goal.SupersededBy);
            Assert.Equal(string.Empty, goal.RetirementCode);
            Assert.Equal(default, goal.RetiredAt);

            Assert.Throws<ArgumentException>(() => goal.SupersedeWith(goal, GameTime.FromDays(3)));
        }

        // -- fixture -------------------------------------------------------------------------------

        private static GoalConditionState Read(NarrativeWorldState world, string kind, params GoalBinding[] bindings) =>
            GoalConditionRegistry.Evaluate(world, GoalConditionRegistry.Create(kind, bindings));

        private static SocialObligation Debt(NarrativeWorldState world) =>
            world.Obligations.Records.Single();

        /// <summary>
        /// One theft, one open debt over it, and one town. Small on purpose: every registered term
        /// has exactly one record to read, so a term that answers from the wrong one is visible.
        /// </summary>
        private static NarrativeWorldState Theft()
        {
            NarrativeWorldState world = new NarrativeWorldState(31337);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeNpc(Merchant, "Halvar") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Thief, "Ruve") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Reeve, "Hald") { Occupation = "reeve", HomeSiteId = Town });

            Fact ownership = new Fact(
                world.NewId("fact"), Merchant, FactPredicates.Possesses, Ring, "a silver ring");
            world.Knowledge.AddFact(ownership);

            Fact theft = new Fact(
                world.NewId("fact"), Thief, FactPredicates.Stole, Ring, "a silver ring", secrecy: 50);
            world.Knowledge.AddFact(theft);
            world.Knowledge.Teach(Thief, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);

            world.Obligations.Add(new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Debt,
                Thief,
                Merchant,
                Ring,
                "the price of the ring",
                GameTime.Zero,
                EntityId.None));

            return world;
        }
    }
}
