using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-083. Short-term discourse memory for one conversation: what has been raised, what is
    /// still hanging, and whether the person talking just contradicted themself.
    ///
    /// The step's condition is an NPC producing the semantic equivalent of "that is not what you
    /// said five minutes ago" from recorded state - so this file proves the state exists, that it
    /// is scoped to the conversation rather than to the world, that it reuses BQ-073's own
    /// deception record instead of keeping a second one, and that a commitment survives past the
    /// conversation only when something explicitly says it should.
    /// </summary>
    public class ConversationStateTests
    {
        // -- topics, claims and questions ------------------------------------------------------------

        [Fact]
        public void TopicsClaimsAndQuestionsAreTrackedSeparately()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct question = scene.AskAboutTheft();
            SpeechAct claim = SpeechAct.Compose(
                SpeechActType.Admit, scene.Speaker, scene.Listener, scene.TheftBinding());

            conversation.Note(question);
            conversation.Note(claim);

            Assert.Equal(2, conversation.Acts.Count);
            Assert.Equal(new[] { question }, conversation.Questions);
            Assert.Equal(new[] { claim }, conversation.Claims);
        }

        /// <summary>
        /// The done-when's other half of "you already asked me that": raising the same matter
        /// twice is detected structurally, from the shared claim, never from matching words.
        /// </summary>
        [Fact]
        public void WasAlreadyAskedCatchesTheSameQuestionRaisedTwice()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct firstAsk = scene.AskAboutTheft();
            conversation.Note(firstAsk);

            SpeechAct sameAskAgain = scene.AskAboutTheft();
            Assert.True(conversation.WasAlreadyAsked(sameAskAgain));

            SpeechAct differentQuestion = SpeechAct.Compose(
                SpeechActType.Ask, scene.Listener, scene.Speaker,
                new ActionBinding { Purpose = "where were you last night" });
            Assert.False(conversation.WasAlreadyAsked(differentQuestion));
        }

        // -- answered vs unanswered ------------------------------------------------------------------

        /// <summary>
        /// A refusal or an evasion is still a response and closes the question, exactly as much as
        /// a straight answer does - only silence leaves it open.
        /// </summary>
        [Fact]
        public void UnansweredQuestionsStayDistinctFromAnsweredOnes()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct aboutTheTheft = scene.AskAboutTheft();
            SpeechAct aboutLastNight = SpeechAct.Compose(
                SpeechActType.Ask, scene.Listener, scene.Speaker,
                new ActionBinding { Purpose = "where were you last night" });
            SpeechAct aboutTheRing = SpeechAct.Compose(
                SpeechActType.Ask, scene.Listener, scene.Speaker,
                new ActionBinding { Purpose = "whose ring is that" });

            conversation.Note(aboutTheTheft);
            conversation.Note(aboutLastNight);
            conversation.Note(aboutTheRing);

            Assert.Equal(3, conversation.UnansweredQuestions.Count);

            SpeechAct answer = SpeechAct.Compose(
                SpeechActType.Answer, scene.Speaker, scene.Listener, scene.TheftBinding(),
                inReplyTo: aboutTheTheft);
            SpeechAct refusal = SpeechAct.Compose(
                SpeechActType.Refuse, scene.Speaker, scene.Listener, ActionBinding.Empty,
                inReplyTo: aboutLastNight);

            conversation.Note(answer);
            conversation.Note(refusal);

            Assert.Equal(new[] { aboutTheRing }, conversation.UnansweredQuestions);
        }

        // -- self-contradiction ------------------------------------------------------------------

        /// <summary>
        /// The step's own example, almost verbatim: the same claim, asserted and then denied by
        /// the same speaker, is caught with nothing but the two statements themselves - no belief
        /// graph, no omniscient narrator, just the conversation remembering what was said.
        /// </summary>
        [Fact]
        public void ContradictsCatchesTheSameClaimAssertedTheOtherWayRound()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct admission = SpeechAct.Compose(
                SpeechActType.Admit, scene.Speaker, scene.Listener, scene.TheftBinding());
            conversation.Note(admission);

            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, scene.Speaker, scene.Listener, scene.TheftBinding());

            DiscourseContradiction? found = conversation.Contradicts(scene.World, denial);

            Assert.True(found.HasValue);
            Assert.Equal(admission, found.Value.Earlier);
            Assert.Equal(denial, found.Value.Later);
            Assert.Contains("Affirms", found.Value.Because);
            Assert.Contains("Denies", found.Value.Because);
        }

        /// <summary>
        /// The harder shape: two different claims that are rival versions of one story (the same
        /// structural relation BQ-073 already reads against belief), both put forward as so by the
        /// same speaker. Naming somebody else the second time round is exactly the "so now you're
        /// saying you never saw him?" case CD §28.5 names.
        /// </summary>
        [Fact]
        public void ContradictsCatchesARivalVersionOfAnEarlierClaim()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct blamesTovar = SpeechAct.Compose(
                SpeechActType.Gossip, scene.Gossiper, scene.Listener,
                new ActionBinding { PropositionFact = scene.BlameFact }, scene.Bystander);
            conversation.Note(blamesTovar);

            SpeechAct blamesKip = SpeechAct.Compose(
                SpeechActType.Gossip, scene.Gossiper, scene.Listener,
                new ActionBinding { PropositionFact = scene.TheftFact }, scene.Speaker);

            DiscourseContradiction? found = conversation.Contradicts(scene.World, blamesKip);

            Assert.True(found.HasValue);
            Assert.Equal(blamesTovar, found.Value.Earlier);
            Assert.Contains("rival version", found.Value.Because);
        }

        [Fact]
        public void ContradictsIgnoresActsThatAssertNothing()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            conversation.Note(scene.AskAboutTheft());

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Speaker, scene.Listener,
                new ActionBinding { Purpose = "find out what really happened" });

            Assert.Null(conversation.Contradicts(scene.World, promise));
        }

        /// <summary>
        /// Two different people asserting rival claims is not a self-contradiction, however rival
        /// the claims are - catching that stays BQ-073's <see cref="Deception.Contradictions"/>,
        /// which reads it against the observer's own belief rather than against a third party's
        /// earlier words.
        /// </summary>
        [Fact]
        public void ContradictsNeverCrossesSpeakers()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            conversation.Note(SpeechAct.Compose(
                SpeechActType.Gossip, scene.Gossiper, scene.Listener,
                new ActionBinding { PropositionFact = scene.BlameFact }, scene.Bystander));

            SpeechAct fromSomebodyElse = SpeechAct.Compose(
                SpeechActType.Gossip, scene.Speaker, scene.Listener,
                new ActionBinding { PropositionFact = scene.TheftFact }, scene.Bystander);

            Assert.Null(conversation.Contradicts(scene.World, fromSomebodyElse));
        }

        [Fact]
        public void AllContradictionsAuditsTheWholeTranscript()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct admission = SpeechAct.Compose(
                SpeechActType.Admit, scene.Speaker, scene.Listener, scene.TheftBinding());
            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, scene.Speaker, scene.Listener, scene.TheftBinding());

            conversation.Note(admission);
            conversation.Note(denial);

            DiscourseContradiction found = Assert.Single(conversation.AllContradictions(scene.World));
            Assert.Equal(admission, found.Earlier);
            Assert.Equal(denial, found.Later);
        }

        // -- reusing BQ-073's own record, not a second one ----------------------------------------

        [Fact]
        public void NoteDeceptionFilesTheExistingRecordRatherThanMintingANewOne()
        {
            Exchange scene = Exchange.Create();
            scene.World.Knowledge.Teach(
                scene.Speaker, scene.TheftFact, KnowledgeSource.Participant, 1.0, scene.Now, false);

            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, scene.Speaker, scene.Listener, scene.TheftBinding());
            WorldEvent recorded = Deception.Record(scene.World, denial, scene.Now);
            Assert.NotNull(recorded);

            int factsAfterRecording = scene.World.Knowledge.Facts.Count;
            int eventsAfterRecording = scene.World.Ledger.Count;

            ConversationState conversation = new ConversationState();
            conversation.NoteDeception(recorded);

            RecordedStatement statement = Assert.Single(conversation.LiesTold);
            Assert.Equal(scene.Speaker, statement.Speaker);
            Assert.Equal(scene.TheftFact, statement.AssertedClaim);

            // Filing it away wrote nothing: the lie was BQ-073's record before conversation state
            // ever heard of it, and it still is.
            Assert.Equal(factsAfterRecording, scene.World.Knowledge.Facts.Count);
            Assert.Equal(eventsAfterRecording, scene.World.Ledger.Count);
        }

        // -- commitments: durable only when the caller says so ------------------------------------

        [Fact]
        public void CommitPromotesAWellFormedPromiseIntoTheObligationLedger()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Speaker, scene.Listener,
                new ActionBinding { Purpose = "bring back the ring" });
            Assert.NotNull(promise);
            conversation.Note(promise);

            WorldEvent recorded = conversation.Commit(scene.World, promise, scene.Now);

            Assert.NotNull(recorded);
            Assert.Equal(WorldEventType.PromiseMade, recorded.Type);
            Assert.Equal(scene.Speaker, recorded.Actor);
            Assert.Equal(scene.Listener, recorded.Target);

            SocialObligation obligation = Assert.Single(scene.World.Obligations.Records);
            Assert.Equal(SocialObligationKind.Promise, obligation.Kind);
            Assert.Equal(scene.Speaker, obligation.Debtor);
            Assert.Equal(scene.Listener, obligation.Creditor);
            Assert.Equal("bring back the ring", obligation.Purpose);
            Assert.Equal(recorded.Id, obligation.SourceEventId);
            Assert.True(obligation.IsOpen);
        }

        [Fact]
        public void CommitDoesNotDuplicateWhenCalledTwiceForTheSameAct()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Speaker, scene.Listener,
                new ActionBinding { Purpose = "bring back the ring" });
            conversation.Note(promise);

            WorldEvent first = conversation.Commit(scene.World, promise, scene.Now);
            WorldEvent second = conversation.Commit(scene.World, promise, scene.Now);

            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Single(scene.World.Obligations.Records);
            Assert.Single(scene.World.Ledger.OfType(WorldEventType.PromiseMade));
        }

        [Fact]
        public void CommitRefusesAnythingThatIsNotAPromise()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct answer = SpeechAct.Compose(
                SpeechActType.Answer, scene.Speaker, scene.Listener, scene.TheftBinding(),
                inReplyTo: scene.AskAboutTheft());

            Assert.Null(conversation.Commit(scene.World, answer, scene.Now));
            Assert.Empty(scene.World.Obligations.Records);
        }

        /// <summary>
        /// The load-bearing negative: noting a promise is bookkeeping the conversation always
        /// does, and by itself it commits the world to nothing. Transient debris - a promise made
        /// and never called on to matter - never becomes permanent state.
        /// </summary>
        [Fact]
        public void NotingAPromiseAloneNeverWritesTheWorld()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, scene.Speaker, scene.Listener,
                new ActionBinding { Purpose = "bring back the ring" });

            conversation.Note(promise);

            Assert.Empty(scene.World.Obligations.Records);
            Assert.Equal(0, scene.World.Ledger.Count);
        }

        // -- nothing here writes except Commit -----------------------------------------------------

        [Fact]
        public void NotingAndCheckingWriteNothing()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            int events = scene.World.Ledger.Count;
            int facts = scene.World.Knowledge.Facts.Count;
            int obligations = scene.World.Obligations.Records.Count;

            SpeechAct admission = SpeechAct.Compose(
                SpeechActType.Admit, scene.Speaker, scene.Listener, scene.TheftBinding());
            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, scene.Speaker, scene.Listener, scene.TheftBinding());

            conversation.Note(admission);
            conversation.Contradicts(scene.World, denial);
            conversation.WasAlreadyAsked(scene.AskAboutTheft());
            NarrativeInspector.DescribeConversation(scene.World, conversation);

            Assert.Equal(events, scene.World.Ledger.Count);
            Assert.Equal(facts, scene.World.Knowledge.Facts.Count);
            Assert.Equal(obligations, scene.World.Obligations.Records.Count);
        }

        [Fact]
        public void TheInspectorDescribesTheConversation()
        {
            Exchange scene = Exchange.Create();
            ConversationState conversation = new ConversationState();

            SpeechAct admission = SpeechAct.Compose(
                SpeechActType.Admit, scene.Speaker, scene.Listener, scene.TheftBinding());
            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny, scene.Speaker, scene.Listener, scene.TheftBinding());

            conversation.Note(admission);
            conversation.Note(denial);

            string described = NarrativeInspector.DescribeConversation(scene.World, conversation);

            Assert.Contains("2 act(s)", described);
            Assert.Contains("lies told:   0", described);
            Assert.Contains("contradiction:", described);
        }

        private sealed class Exchange
        {
            private Exchange(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            /// <summary>Took the ring. Also the town's most reliable source of rumours about it.</summary>
            internal EntityId Speaker { get; private set; }

            /// <summary>Spreads a rival version naming the wrong man, then the right one.</summary>
            internal EntityId Gossiper { get; private set; }

            internal EntityId Bystander { get; private set; }

            internal EntityId Listener { get; private set; }

            internal EntityId TheftFact { get; private set; }

            internal EntityId BlameFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Exchange Create()
            {
                Exchange scene = new Exchange(new NarrativeWorldState(20260903UL));
                scene.Speaker = scene.Person("Kip");
                scene.Gossiper = scene.Person("Nel");
                scene.Bystander = scene.Person("Tovar");
                scene.Listener = scene.Person("You");

                Fact theft = new Fact(
                    scene.World.NewId("fact"), scene.Speaker, FactPredicates.Stole, EntityId.None,
                    "silver ring", TruthState.True, secrecy: 40);
                scene.World.Knowledge.AddFact(theft);
                scene.TheftFact = theft.Id;

                Fact blame = new Fact(
                    scene.World.NewId("fact"), scene.Bystander, FactPredicates.Stole, EntityId.None,
                    "silver ring", TruthState.False, secrecy: 40)
                {
                    DistortionOf = theft.Id
                };
                scene.World.Knowledge.AddFact(blame);
                scene.BlameFact = blame.Id;

                return scene;
            }

            internal ActionBinding TheftBinding() => new ActionBinding { PropositionFact = TheftFact };

            internal SpeechAct AskAboutTheft()
            {
                return SpeechAct.Compose(SpeechActType.Ask, Listener, Speaker, TheftBinding());
            }

            private EntityId Person(string name)
            {
                EntityId id = World.NewId("npc");
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }
        }
    }
}
