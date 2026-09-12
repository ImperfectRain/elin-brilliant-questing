using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-011. What somebody could actually do about what they want, worked out from the three
    /// declarations that already existed rather than from anybody's list.
    ///
    /// The rule this replaces was two substring tests and a shrug: the verb came from whether the
    /// goal's name contained "steal", and the want was closed if the attempt succeeded and recorded
    /// anything at all. Both are the same mistake in different places - reading a label instead of
    /// asking the world - and this suite is about the two questions being asked properly and, just
    /// as importantly, being kept apart from each other. What holds in authoritative state and what
    /// the person whose want it is has any right to believe are not the same answer.
    /// </summary>
    public class GoalActionBridgeTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Merchant = EntityId.Parse("npc_merchant");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Reeve = EntityId.Parse("npc_reeve");
        private static readonly EntityId Creditor = EntityId.Parse("npc_creditor");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");
        private static readonly EntityId Cordwall = EntityId.Parse("zone_town");

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// Recovery, information and obligation each find more than one route, and every one of
        /// them is a verb that says it could make the kind of change the want asks for.
        ///
        /// Nothing here names a verb to the bridge. The chain is the want's condition term, the
        /// effect kinds that term says would move it, and the verbs that say they could make those
        /// changes - so the assertion that matters is the last one: every route's verb declares the
        /// route's effect, and that effect is one the term asked for.
        /// </summary>
        [Fact]
        public void RecoveryInformationAndObligationWantsEachFindSeveralAppropriateRoutes()
        {
            Town town = Town.Create();

            GoalRouteSearch recovery = town.Discover(town.Merchant, town.RecoveryWant());
            GoalRouteSearch telling = town.Discover(town.Merchant, town.ReportingWant());
            GoalRouteSearch debt = town.Discover(town.Debtor, town.DebtWant());

            foreach (GoalRouteSearch search in new[] { recovery, telling, debt })
            {
                Assert.True(search.IsSupported, search.Unsupported);
                Assert.True(Verbs(search).Count > 1, "only " + string.Join(",", Verbs(search)) + " was found");

                foreach (GoalRoute route in search.Routes)
                {
                    Assert.True(route.Action.Effects.Advances(route.EffectKind));
                    Assert.Contains(
                        route.EffectKind,
                        GoalConditionRegistry.AdvancedBy(route.Goal.Condition.Kind));
                }
            }

            // And the routes are the semantically right ones, which is a different claim from
            // there being several of them.
            Assert.Contains("pickpocket", Verbs(recovery));
            Assert.Contains("return_item", Verbs(recovery));
            Assert.Contains("report", Verbs(telling));
            Assert.Contains("expose", Verbs(telling));
            Assert.Contains("pay_debt", Verbs(debt));
        }

        /// <summary>
        /// An attempt that worked, and a want that is no closer, stay two separate facts.
        ///
        /// The thief lifts the ring and the theft goes into the ledger, so under the old rule -
        /// succeeded, recorded something - the merchant's want of it back would have been marked
        /// done. It is not: the ownership record still says whose the ring is, that is what the
        /// condition asks about, and the world's answer is the only thing consulted.
        /// </summary>
        [Fact]
        public void AWantIsClosedByTheWorldsAnswerAndNotByAnAttemptHavingSucceeded()
        {
            Town town = Town.Create();
            town.Vanilla.GiveItem(Merchant, new ItemDescriptor(Ring, "silver ring", "ring", 300));
            Fact whose = town.Possesses(Merchant, Ring);

            // The thief wants the ring to be theirs, and the merchant is somebody they can aim at
            // only because they hold a claim that says the ring is his. That is the whole of
            // "actor-accessible": without it, the route to him does not exist.
            town.World.Knowledge.Teach(Thief, whose.Id, KnowledgeSource.Witnessed, 0.9, town.Now, false);
            NpcGoal want = town.Want(
                Thief,
                "acquire",
                Ring,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.PropertyOwnedBy,
                    new GoalBinding("item", Ring),
                    new GoalBinding("owner", Thief)));

            town.Advance();

            OffScreenSchemeTrace trace = Assert.Single(town.Schemes.LastPass, t => t.Actor == Thief);
            Assert.True(trace.Acted);
            Assert.True(trace.Attempt.Outcome.Succeeded);
            Assert.NotEmpty(trace.Attempt.Outcome.Events);

            // The ring moved. Whose it is did not, so the want did not close.
            Assert.Equal(Thief, town.HolderOf(Ring));
            Assert.Equal(GoalConditionState.Unmet, want.Evaluate(town.World));
            Assert.Equal(GoalConditionState.Unmet, trace.ConditionAfter);
            Assert.False(trace.GoalSatisfied);
            Assert.True(want.IsActive);
            Assert.Equal(GoalAssessment.Unknown, want.ActorAssessment);
        }

        /// <summary>
        /// Somebody else can make a want true without its owner finding out.
        ///
        /// The merchant is asleep on the far road while the ring is returned to their name. The
        /// world says the condition holds - and their goal stays exactly where it was, because
        /// nothing that happened told them. Closing it here is the omniscience the step forbids;
        /// their own goal evolution retires it when the pressure stops pressing on them.
        /// </summary>
        [Fact]
        public void AnotherActorsDeedSatisfiesTheConditionWithoutTeachingItsOwner()
        {
            Town town = Town.Create();
            NpcGoal want = town.Want(Merchant, "recover_property", Ring, town.RingIsTheMerchants());
            want.ActorAssessment = GoalAssessment.BelievedUnmet;

            // The reeve settles whose it is, off in the town, with nobody telling Halvar.
            town.Possesses(Merchant, Ring);

            Assert.Equal(GoalConditionState.Met, want.Evaluate(town.World));
            Assert.True(want.IsActive);
            Assert.Equal(GoalAssessment.BelievedUnmet, want.ActorAssessment);

            // And a pass over the merchant does not hand it to them either: they attempted nothing
            // that succeeded, so nothing here is a route by which they could have learned.
            town.Advance();

            OffScreenSchemeTrace trace = town.Schemes.LastPass.SingleOrDefault(t => t.Actor == Merchant);
            if (trace != null)
            {
                Assert.False(trace.GoalSatisfied);
            }

            Assert.True(want.IsActive);
            Assert.Equal(GoalAssessment.BelievedUnmet, want.ActorAssessment);
            Assert.Equal(GoalConditionState.Met, want.Evaluate(town.World));
        }

        /// <summary>
        /// A want built on a claim that is not so is still a want somebody can act on.
        ///
        /// Halvar sincerely holds that the thief took his ring; the claim is false and the ledger
        /// says so. Nothing in the bridge asks whether the premise is true - it asks what the
        /// condition is and which verbs could move it - so the routes are there, and the person
        /// gets to be wrong out loud.
        /// </summary>
        [Fact]
        public void AWantOnASincerelyMistakenClaimStaysActionable()
        {
            Town town = Town.Create();
            Fact untrue = town.Fact(Thief, FactPredicates.Stole, Ring, "silver ring", TruthState.False);
            town.World.Knowledge.Teach(Merchant, untrue.Id, KnowledgeSource.Inference, 0.9, town.Now, false);

            NpcGoal want = town.Want(
                Merchant,
                "clear_it_up",
                untrue.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.InformationKnownBy,
                    new GoalBinding("claim", untrue.Id),
                    new GoalBinding("knower", Reeve)));

            GoalRouteSearch search = town.Discover(town.Merchant, want);

            Assert.True(search.IsSupported, search.Unsupported);
            Assert.Contains("report", Verbs(search));
            Assert.Contains(search.Routes, route => route.Target == Reeve);
            Assert.Equal(TruthState.False, town.World.Knowledge.GetFact(untrue.Id).Truth);
            Assert.Equal(GoalConditionState.Unmet, want.Evaluate(town.World));
        }

        /// <summary>
        /// Where there is no route the answer says so, and the answer is never another verb.
        ///
        /// Three different kinds of nothing, and a consumer that only asked "what matched" would
        /// read all three as the same silence: a want that never said what it wanted, a want whose
        /// term this build cannot read, and a want whose only route is a write vanilla would have
        /// to carry on a build that cannot carry it. The last one is the one that matters most,
        /// because the tempting answer is to offer something else instead.
        /// </summary>
        [Fact]
        public void MissingEffectsOrBindingsYieldAnExplicitAnswerRatherThanAnUnrelatedFallback()
        {
            Town town = Town.Create();

            GoalRouteSearch wordless = town.Discover(town.Merchant, new NpcGoal("get_it_back", Ring, 80));
            Assert.False(wordless.IsSupported);
            Assert.Contains("no machine-readable condition", wordless.Unsupported);
            Assert.Empty(wordless.Routes);

            // A save written by a build that knew a term this one does not.
            GoalRouteSearch foreign = town.Discover(
                town.Merchant,
                new NpcGoal("get_it_back", Ring, 80, string.Empty, new GoalCondition("cargo.landed")));
            Assert.False(foreign.IsSupported);
            Assert.Contains("not a condition term this build can read", foreign.Unsupported);

            // Readable, and nothing on this build can carry it. That is a wait, not a substitute.
            town.Vanilla.SetCapability(VanillaCapability.TransferItems, false);
            NpcGoal want = town.Want(Merchant, "recover_property", Ring, town.RingIsTheMerchants());
            GoalRouteSearch barred = town.Discover(town.Merchant, want);

            Assert.True(barred.IsSupported);
            Assert.DoesNotContain("pickpocket", Verbs(barred));
            Assert.DoesNotContain("return_item", Verbs(barred));
            Assert.All(barred.Routes, route => Assert.Equal(SemanticEffects.PossessionTransferred, route.EffectKind));

            town.Advance();
            OffScreenSchemeTrace trace = Assert.Single(town.Schemes.LastPass, t => t.Actor == Merchant);
            Assert.False(trace.Acted);
            Assert.Contains(trace.Searches, search => search.IsSupported);
        }

        /// <summary>
        /// The two answers come back off disk as two answers.
        ///
        /// What is saved is what BQa-008 already saved - the condition, the lifecycle and the
        /// owner's own view - and the point of round-tripping it here is that the pair survives:
        /// a want the world holds true and its owner does not believe is still exactly that after a
        /// reload, and the same routes are found for it.
        /// </summary>
        [Fact]
        public void SaveAndReloadPreservesTheObjectiveAndBelievedAnswersSeparately()
        {
            Town town = Town.Create();
            NpcGoal want = town.Want(Merchant, "recover_property", Ring, town.RingIsTheMerchants());
            want.ActorAssessment = GoalAssessment.BelievedUnmet;
            town.Possesses(Merchant, Ring);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(town.World));
            NarrativeNpc merchant = reloaded.Registry.GetNpc(Merchant);
            NpcGoal reloadedWant = Assert.Single(merchant.Goals);

            Assert.Equal(GoalLifecycle.Active, reloadedWant.Lifecycle);
            Assert.Equal(GoalAssessment.BelievedUnmet, reloadedWant.ActorAssessment);
            Assert.Equal(GoalConditionState.Met, reloadedWant.Evaluate(reloaded));

            GoalRouteSearch before = town.Discover(town.Merchant, want);
            GoalRouteSearch after = GoalRoutes.Discover(reloaded, town.Vanilla, town.Actions, merchant, reloadedWant);
            Assert.Equal(Verbs(before), Verbs(after));
        }

        /// <summary>
        /// A verb registered with vocabulary that already exists joins every want that vocabulary
        /// answers, and nothing central was edited to let it in.
        ///
        /// BQa-010 proved this for the registry's own question. What is new here is that the want
        /// finds it: the chain runs term to effect kind to verb, and this verb is reached without
        /// its id appearing in the bridge, in the condition registry, or anywhere but its own
        /// registration.
        /// </summary>
        [Fact]
        public void AddingAVerbWithExistingVocabularyJoinsRouteDiscoveryWithNoCentralSwitch()
        {
            Town town = Town.Create();
            NpcGoal want = town.Want(Merchant, "recover_property", Ring, town.RingIsTheMerchants());

            Assert.DoesNotContain("ransom", Verbs(town.Discover(town.Merchant, want)));

            town.Actions.Register(new RansomAction());

            Assert.Contains("ransom", Verbs(town.Discover(town.Merchant, want)));
        }

        // -- the step's refinements ---------------------------------------------------------------

        /// <summary>
        /// A want about somebody's own property does not need a matter to be about.
        ///
        /// Minting a <see cref="NarrativeThread"/> so that a context would validate would be
        /// inventing a situation to justify an act that needs none, and it would put a matter in
        /// the chronicle for every ordinary want anybody ever had. The same condition is asked with
        /// and without one and answers the same way.
        /// </summary>
        [Fact]
        public void TheSameWantIsAnsweredWithAndWithoutAnEstablishedMatter()
        {
            Town threadless = Town.Create();
            NpcGoal withoutAMatter = threadless.Adopt(Merchant, threadless.ReportingWant());
            GoalRouteSearch bare = threadless.Discover(threadless.Merchant, withoutAMatter);

            Town withAMatter = Town.Create();
            NpcGoal inAMatter = withAMatter.Adopt(Merchant, withAMatter.ReportingWant());
            withAMatter.Matter("theft", inAMatter.Subject, Merchant, Thief, Reeve);
            GoalRouteSearch established = withAMatter.Discover(withAMatter.Merchant, inAMatter);

            Assert.Equal(Verbs(bare), Verbs(established));
            Assert.Empty(threadless.World.Threads);

            // And it runs: the attempt goes through the shared library with no matter in hand, and
            // making it run did not put one in the chronicle.
            threadless.Advance();
            OffScreenSchemeTrace trace = Assert.Single(threadless.Schemes.LastPass, t => t.Actor == Merchant);
            Assert.True(trace.Acted);
            Assert.Null(trace.Attempt.Intent.Thread);
            Assert.Empty(threadless.World.Threads);
        }

        /// <summary>
        /// Wanting somebody told finds a way to tell them, and telling them lands.
        ///
        /// No storylet runs, no scene is searched and nothing is worded: the disclosure verb is
        /// discovered through the same registry as every other route, and what makes it causal is
        /// that the knowledge owner records the receipt. A conversation is one way people come to
        /// know things and this proves it is not the only one.
        /// </summary>
        [Fact]
        public void AReportingWantFindsAnActorToActorCommunicationRouteThatActuallyDelivers()
        {
            Town town = Town.Create();
            Fact theft = town.Fact(Thief, FactPredicates.Stole, Ring, "silver ring");
            town.World.Knowledge.Teach(Merchant, theft.Id, KnowledgeSource.Participant, 1.0, town.Now, false);

            NpcGoal want = town.Want(
                Merchant,
                "have_it_reported",
                theft.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.InformationKnownBy,
                    new GoalBinding("claim", theft.Id),
                    new GoalBinding("knower", Reeve)));

            GoalRouteSearch search = town.Discover(town.Merchant, want);
            GoalRoute toTheReeve = Assert.Single(
                search.Routes,
                route => route.Action.Id == "expose" && route.Target == Reeve);

            Assert.Equal(SemanticEffects.InformationDisclosed, toTheReeve.EffectKind);
            Assert.Equal(theft.Id, toTheReeve.SubjectFact);
            Assert.Equal(GoalConditionState.Unmet, want.Evaluate(town.World));

            ActionAttempt told = town.Attempt(Merchant, toTheReeve);

            Assert.True(told.Outcome.Succeeded);
            Assert.True(town.World.Knowledge.Knows(Reeve, theft.Id));
            Assert.Equal(GoalConditionState.Met, want.Evaluate(town.World));
            Assert.Empty(town.World.Threads);
        }

        /// <summary>
        /// The three things a communication route has to be honest about, with no scene anywhere
        /// near it: a refusal, a sincere error, and a correction.
        /// </summary>
        [Fact]
        public void CommunicationRoutesPinRefusalSincereErrorAndCorrection()
        {
            Town town = Town.Create();
            Fact untrue = town.Fact(Thief, FactPredicates.Stole, Ring, "silver ring", TruthState.False);
            town.World.Knowledge.Teach(Merchant, untrue.Id, KnowledgeSource.Inference, 0.9, town.Now, false);

            // Sincere error: what is delivered is the claim the speaker holds, false and all. The
            // reeve comes away holding it, and the ledger still says it is not so.
            NpcGoal tellTheReeve = town.Want(
                Merchant,
                "have_it_reported",
                untrue.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.InformationKnownBy,
                    new GoalBinding("claim", untrue.Id),
                    new GoalBinding("knower", Reeve)));

            GoalRoute mistaken = Assert.Single(
                town.Discover(town.Merchant, tellTheReeve).Routes,
                route => route.Action.Id == "expose" && route.Target == Reeve);
            Assert.True(town.Attempt(Merchant, mistaken).Outcome.Succeeded);
            Assert.True(town.World.Knowledge.Knows(Reeve, untrue.Id));
            Assert.Equal(TruthState.False, town.World.Knowledge.GetFact(untrue.Id).Truth);
            Assert.Equal(GoalConditionState.Met, tellTheReeve.Evaluate(town.World));

            // Refusal: saying it again is not a second telling, and the verb says so rather than
            // resolving into a repeat.
            Assert.False(town.Availability(Merchant, mistaken).IsAvailable);

            // Correction: the true account is a different claim, and telling the reeve that one is
            // a different want with its own route. Nothing rewrites what they were told before.
            Fact truth = town.Fact(Reeve, FactPredicates.Possesses, Ring, "silver ring");
            town.World.Knowledge.Teach(Merchant, truth.Id, KnowledgeSource.Participant, 1.0, town.Now, false);
            NpcGoal setItStraight = town.Want(
                Merchant,
                "set_it_straight",
                truth.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.InformationKnownBy,
                    new GoalBinding("claim", truth.Id),
                    new GoalBinding("knower", Reeve)));

            GoalRoute correction = Assert.Single(
                town.Discover(town.Merchant, setItStraight).Routes,
                route => route.Action.Id == "expose" && route.Target == Reeve);
            Assert.True(town.Attempt(Merchant, correction).Outcome.Succeeded);
            Assert.True(town.World.Knowledge.Knows(Reeve, truth.Id));
            Assert.True(town.World.Knowledge.Knows(Reeve, untrue.Id));
        }

        /// <summary>
        /// Denying a claim is not a way of telling somebody it.
        ///
        /// <c>lie</c> declared its effect as disclosure, which is true of the sentence and false of
        /// what happens to the listener's belief: whatever the roll, nobody comes away holding the
        /// claim who did not hold it before. Left as it was, a want that the reeve be told would
        /// have been offered the one verb in the library guaranteed to move it the wrong way.
        /// </summary>
        [Fact]
        public void DenyingAClaimIsNotOfferedAsAWayOfTellingSomebodyIt()
        {
            Town town = Town.Create();
            Fact theft = town.Fact(Thief, FactPredicates.Stole, Ring, "silver ring");
            town.World.Knowledge.Teach(Merchant, theft.Id, KnowledgeSource.Participant, 1.0, town.Now, false);

            NpcGoal want = town.Want(
                Merchant,
                "have_it_reported",
                theft.Id,
                GoalConditionRegistry.Create(
                    GoalConditionKinds.InformationKnownBy,
                    new GoalBinding("claim", theft.Id),
                    new GoalBinding("knower", Reeve)));

            Assert.DoesNotContain("lie", Verbs(town.Discover(town.Merchant, want)));
            Assert.Equal(
                new[] { SemanticEffects.InformationDenied },
                town.Actions.Get("lie").Effects.Effects.Select(effect => effect.Kind).ToArray());
            Assert.DoesNotContain(
                SemanticEffects.InformationDenied,
                GoalConditionRegistry.AdvancedBy(GoalConditionKinds.InformationKnownBy));
        }

        /// <summary>
        /// A want somebody gave up is not a want, and the pass does not go looking for routes to it.
        ///
        /// The gate here read "not satisfied", which is true of a want its owner abandoned and of
        /// one that was reappraised into a different want - both of which BQa-009 now produces - so
        /// the pass would have kept chasing something nobody was holding any more.
        /// </summary>
        [Fact]
        public void ARetiredWantIsNotPursuedMerelyBecauseItWasNeverSatisfied()
        {
            Town town = Town.Create();
            NpcGoal gaveUp = town.Adopt(Merchant, town.RecoveryWant());
            gaveUp.Abandon(town.Now, "gave_up");

            Assert.Equal(0, town.Advance());
            Assert.DoesNotContain(town.Schemes.LastPass, trace => trace.Actor == Merchant);
            Assert.Equal(GoalLifecycle.Abandoned, gaveUp.Lifecycle);
        }

        /// <summary>
        /// Looking for a route is a read. Asking every want what could be done about it leaves the
        /// world byte-identical, including the wants that were asked about.
        /// </summary>
        [Fact]
        public void RouteDiscoveryMutatesNothing()
        {
            Town town = Town.Create();
            NpcGoal recovery = town.Adopt(Merchant, town.RecoveryWant());
            NpcGoal debt = town.Adopt(Thief, town.DebtWant());
            NpcGoal telling = town.Adopt(Merchant, town.ReportingWant());

            string before = WorldStateSerializer.Save(town.World);
            int events = town.World.Ledger.Count;

            for (int pass = 0; pass < 3; pass++)
            {
                town.Discover(town.Merchant, recovery);
                town.Discover(town.Debtor, debt);
                town.Discover(town.Merchant, telling);
            }

            Assert.Equal(before, WorldStateSerializer.Save(town.World));
            Assert.Equal(events, town.World.Ledger.Count);
            Assert.True(recovery.IsActive);
            Assert.Equal(GoalAssessment.Unknown, recovery.ActorAssessment);
        }

        /// <summary>
        /// A term that never said what its bindings are is readable, evaluable and routeless, and
        /// says which of those it is. Guessing that "widget" meant an object would be the switch on
        /// term names this design exists to avoid.
        /// </summary>
        [Fact]
        public void ATermRegisteredWithoutSayingWhatItsBindingsAreGetsNoGuessedRoute()
        {
            Town town = Town.Create();
            GoalConditionRegistry.Register(
                "test.widget_sound",
                new[] { "widget" },
                (world, condition) => GoalConditionState.Unmet,
                new[] { SemanticEffects.ObjectRepaired });

            NpcGoal want = town.Want(
                Merchant,
                "mend_it",
                Ring,
                GoalConditionRegistry.Create("test.widget_sound", new GoalBinding("widget", Ring)));

            GoalRouteSearch search = town.Discover(town.Merchant, want);

            Assert.False(search.IsSupported);
            Assert.Contains("has not said what its bindings are", search.Unsupported);
            Assert.Equal(GoalConditionState.Unmet, want.Evaluate(town.World));
        }

        // -- fixture -----------------------------------------------------------------------------

        private static HashSet<string> Verbs(GoalRouteSearch search)
        {
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < search.Routes.Count; i++)
            {
                ids.Add(search.Routes[i].Action.Id);
            }

            return ids;
        }

        /// <summary>
        /// Four people, one ring, one debt and one authority. Initial state and controlled adapter
        /// behaviour only: nothing here supplies a follow-up want, a planner or an incident.
        /// </summary>
        private sealed class Town
        {
            private Town()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public ICheckResolver Checks { get; private set; }

            public OffScreenSchemes Schemes { get; private set; }

            public NarrativeNpc Merchant => World.Registry.GetNpc(GoalActionBridgeTests.Merchant);

            public NarrativeNpc Debtor => World.Registry.GetNpc(Thief);

            public GameTime Now => Vanilla.Now;

            public static Town Create()
            {
                Town town = new Town
                {
                    World = new NarrativeWorldState(11011),
                    Actions = StandardActions.CreateRegistry(),
                    Checks = new FixedCheckResolver(CheckOutcome.Pass),
                    Schemes = new OffScreenSchemes { MostActorsPerPass = 20, MostAttemptsPerPass = 10 }
                };

                town.Vanilla = new SandboxVanillaState(Player);
                town.World.Registry.Add(new NarrativeSite(Cordwall, "Cordwall", "town"));

                town.Add(Player, "You", 400);
                town.Add(GoalActionBridgeTests.Merchant, "Halvar", 200);
                town.Add(Thief, "Lysa", 150);
                town.Add(Reeve, "Herrick", 300);
                town.Add(Creditor, "Voss", 300);
                town.World.Registry.GetNpc(Reeve).Roles.Add(AuthorityPolicy.GuardRole);

                new ConsequenceEngine(town.World, town.Vanilla).Attach();
                town.Vanilla.AdvanceDays(30);
                return town;
            }

            public GoalRouteSearch Discover(NarrativeNpc actor, NpcGoal goal) =>
                GoalRoutes.Discover(World, Vanilla, Actions, actor, goal);

            public int Advance() => Schemes.Advance(World, Vanilla, Checks, Actions, Now);

            /// <summary>Puts a want somebody already built onto its owner.</summary>
            public NpcGoal Adopt(EntityId actor, NpcGoal goal)
            {
                World.Registry.GetNpc(actor).Goals.Add(goal);
                return goal;
            }

            public NpcGoal Want(EntityId actor, string kind, EntityId subject, GoalCondition condition)
            {
                NpcGoal goal = new NpcGoal(kind, subject, 90, string.Empty, condition);
                World.Registry.GetNpc(actor).Goals.Add(goal);
                return goal;
            }

            public GoalCondition RingIsTheMerchants() => GoalConditionRegistry.Create(
                GoalConditionKinds.PropertyOwnedBy,
                new GoalBinding("item", Ring),
                new GoalBinding("owner", GoalActionBridgeTests.Merchant));

            /// <summary>A recovery want with the knowledge that makes the thief reachable.</summary>
            public NpcGoal RecoveryWant()
            {
                Fact theft = Fact(Thief, FactPredicates.Stole, Ring, "silver ring");
                World.Knowledge.Teach(GoalActionBridgeTests.Merchant, theft.Id, KnowledgeSource.Participant, 1.0, Now, false);
                return new NpcGoal("recover_property", Ring, 90, string.Empty, RingIsTheMerchants());
            }

            public NpcGoal ReportingWant()
            {
                Fact theft = Fact(Thief, FactPredicates.Stole, Ring, "silver ring");
                World.Knowledge.Teach(GoalActionBridgeTests.Merchant, theft.Id, KnowledgeSource.Participant, 1.0, Now, false);
                return new NpcGoal(
                    "have_it_reported",
                    theft.Id,
                    85,
                    string.Empty,
                    GoalConditionRegistry.Create(
                        GoalConditionKinds.InformationKnownBy,
                        new GoalBinding("claim", theft.Id),
                        new GoalBinding("knower", Reeve)));
            }

            public NpcGoal DebtWant()
            {
                Fact owed = Fact(Thief, FactPredicates.Owes, Creditor, "90 orens");
                World.Knowledge.Teach(Thief, owed.Id, KnowledgeSource.Participant, 1.0, Now, false);
                SocialObligation obligation = new SocialObligation(
                    World.NewId("obligation"),
                    SocialObligationKind.Debt,
                    Thief,
                    Creditor,
                    owed.Id,
                    "repay 90 orens",
                    Now,
                    EntityId.None,
                    2);
                World.Obligations.Add(obligation);

                return new NpcGoal(
                    "repay_debt",
                    obligation.Id,
                    80,
                    string.Empty,
                    GoalConditionRegistry.Create(
                        GoalConditionKinds.ObligationDischarged,
                        new GoalBinding("obligation", obligation.Id)));
            }

            public Fact Fact(
                EntityId subject,
                string predicate,
                EntityId obj,
                string value,
                TruthState truth = TruthState.True)
            {
                Fact fact = new Fact(World.NewId("fact"), subject, predicate, obj, value, truth);
                World.Knowledge.AddFact(fact);
                return fact;
            }

            public Fact Possesses(EntityId owner, EntityId item) =>
                Fact(owner, FactPredicates.Possesses, item, "silver ring");

            public NarrativeThread Matter(string archetype, EntityId fact, params EntityId[] participants)
            {
                NarrativeThread thread = new NarrativeThread(World.NewId("thread"), archetype, Now)
                {
                    State = ThreadState.Active
                };
                for (int i = 0; i < participants.Length; i++)
                {
                    thread.ParticipantIds.Add(participants[i]);
                }

                thread.FactIds.Add(fact);
                thread.SiteIds.Add(Cordwall);
                World.Threads.Add(thread);
                return thread;
            }

            public EntityId HolderOf(EntityId item)
            {
                foreach (EntityId actor in World.Registry.Npcs.Keys)
                {
                    if (Vanilla.GetInventory(actor).Any(held => held.Id == item))
                    {
                        return actor;
                    }
                }

                return EntityId.None;
            }

            public ActionAttempt Attempt(EntityId actor, GoalRoute route)
            {
                ActionIntent intent = new ActionIntent(actor, route.Action.Id, route.Target, route.Because)
                {
                    SubjectFact = route.SubjectFact,
                    SubjectItem = route.SubjectItem
                };

                return ActionAttempt.Run(Actions, intent, Context(actor, route));
            }

            public Availability Availability(EntityId actor, GoalRoute route) =>
                route.Action.GetAvailability(Context(actor, route));

            private ActionContext Context(EntityId actor, GoalRoute route)
            {
                Assert.True(ActorContexts.TryBuild(
                    World, Vanilla, Checks, World.Rng, actor, route.Target, out ActionContext context, out string refusal),
                    refusal);

                context.SubjectFact = route.SubjectFact;
                context.SubjectItem = route.SubjectItem;
                context.Binding = route.Binding;
                return context;
            }

            private void Add(EntityId id, string name, int money)
            {
                World.Registry.Add(new NarrativeNpc(id, name));
                Vanilla.Define(id, zone: Cordwall, money: money);
            }
        }

        /// <summary>
        /// A verb this repository does not ship, declaring only vocabulary that already exists.
        /// Registering it is the whole of joining route discovery.
        /// </summary>
        private sealed class RansomAction : NarrativeAction
        {
            public RansomAction() : base("ransom", ActionFamily.Economic, "Buy it back")
            {
            }

            public override ActionEffects Effects => ActionEffects
                .Declaring(ActionEffect.Delegated(
                    SemanticEffects.PossessionTransferred,
                    "IVanillaState.TrySpendMoney",
                    VanillaCapability.SpendMoney))
                .NeedingAnyOf(SemanticSlots.Item);

            protected override Availability GetAvailabilityCore(ActionContext context) => Availability.Available();

            protected override ActionOutcome PerformCore(ActionContext context) =>
                new ActionOutcome(Id, null, "You buy it back.");
        }
    }
}
