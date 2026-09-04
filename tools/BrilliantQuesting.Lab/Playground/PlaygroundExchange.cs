using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// One exchange, with every production object it passed through kept rather than summarised.
    ///
    /// The report reads this; nothing re-derives anything from a string. A field that is null means
    /// the step did not happen - no act was composed, no callback survived both gates, nothing was
    /// committed - which is a different statement from a step that happened and came out empty, and
    /// the reporters are careful to keep the two apart.
    /// </summary>
    public sealed class PlaygroundTurn
    {
        internal PlaygroundTurn(int number, string kind)
        {
            Number = number;
            Kind = kind ?? string.Empty;
            Notes = new List<string>();
        }

        public int Number { get; }

        /// <summary>What this exchange was: a question, the same question again, a request.</summary>
        public string Kind { get; }

        /// <summary>What the listener put to the speaker.</summary>
        public SpeechAct Prompt { get; internal set; }

        /// <summary>Whether this conversation had already been asked this, before the prompt was noted.</summary>
        public bool AlreadyAsked { get; internal set; }

        /// <summary>What the speaker decided, or null for an exchange that weighed no disclosure.</summary>
        public DisclosureDecision Decision { get; internal set; }

        /// <summary>The speaker's own reading of the claim (BQ-080), derived once for the first turn.</summary>
        public ActorReaction Reaction { get; internal set; }

        /// <summary>What the speaker said, semantically. Null when the decision amounted to no act.</summary>
        public SpeechAct Reply { get; internal set; }

        /// <summary>The clearance old business was weighed under, or null when none was sought or survived.</summary>
        public CallbackPermit Callback { get; internal set; }

        /// <summary>The most salient hook the speaker may recall but would not raise here.</summary>
        public CallbackPermit WithheldCallback { get; internal set; }

        /// <summary>The continuity-humour candidate (BQ-082), when the material earns one.</summary>
        public CallbackPermit Recurrence { get; internal set; }

        /// <summary>The wording request, exactly as handed to the realizer.</summary>
        public RealizationRequest Request { get; internal set; }

        /// <summary>
        /// How many fragments each slot had to choose from, counted from the realizer's own
        /// <see cref="DialogueRealizer.Candidates"/> at the moment the request was made.
        ///
        /// Taken before realization rather than after, because the scene's weirdness allowance is
        /// spent during it: a count read afterwards would be the pool a second line would have had.
        /// Null for an exchange that worded nothing. It is the eligible pool and not the chosen
        /// one - repetition narrows this further, and what that narrowing did is read off
        /// <see cref="PlaygroundExchange.History"/>.
        /// </summary>
        public PlaygroundEligibility Eligible { get; internal set; }

        public RealizedLine Line { get; internal set; }

        /// <summary>A self-contradiction this exchange produced, as conversation state caught it.</summary>
        public DiscourseContradiction? Contradiction { get; internal set; }

        /// <summary>The deception the world recorded, when the speaker asserted against their own belief.</summary>
        public WorldEvent RecordedDeception { get; internal set; }

        /// <summary>The promise this exchange promoted into the obligation ledger, when it did.</summary>
        public WorldEvent Committed { get; internal set; }

        public int LedgerBefore { get; internal set; }

        public int LedgerAfter { get; internal set; }

        public int ObligationsBefore { get; internal set; }

        public int ObligationsAfter { get; internal set; }

        /// <summary>How many acts the conversation held once this exchange finished.</summary>
        public int ActsNoted { get; internal set; }

        /// <summary>How many questions were still hanging once this exchange finished.</summary>
        public int Unanswered { get; internal set; }

        /// <summary>
        /// Honest remarks about what did not happen and why - a promise nothing in the library has
        /// words for, an act the semantic layer never composed. Diagnostic; nothing branches on them.
        /// </summary>
        public List<string> Notes { get; }

        public bool WroteToTheLedger => LedgerAfter != LedgerBefore || ObligationsAfter != ObligationsBefore;
    }

    /// <summary>
    /// The conversation, run through the production path and nothing else.
    ///
    /// Every semantic answer in here comes from Core: <see cref="SpeechAct.Compose"/> makes the
    /// acts, <see cref="Disclosure"/> decides and composes the reply, <see cref="CallbackDisclosure"/>
    /// clears old business, <see cref="DialogueRealizer"/> finds the words,
    /// <see cref="ConversationState"/> remembers the exchange and <see cref="Deception"/> records a
    /// falsehood. This class supplies the three things Core has no authority for and says so on
    /// every one of them: which two people are talking, which claim is at issue, and - for the
    /// third exchange - that somebody undertook something.
    ///
    /// <b>It holds the transient state a conversation has and no more.</b> One
    /// <see cref="ConversationState"/>, one <see cref="DialogueExpressionHistory"/> and one
    /// <see cref="WeirdnessBudget"/>, built when the exchange begins and let go when it ends -
    /// which is what makes a second turn see the first, and is also the whole of what "multi-turn"
    /// means here. Nothing is persisted, and nothing about the exchange is written back to a
    /// character.
    /// </summary>
    public sealed class PlaygroundExchange
    {
        /// <summary>
        /// The scene's weirdness ceiling. Fixed rather than rolled so that two presets compared at
        /// one seed differ in the state under test rather than in what the budget happened to
        /// allow; a caller who wants the distribution runs <c>WeirdnessBudget.Roll</c> instead.
        /// </summary>
        public const WeirdnessLevel Ceiling = WeirdnessLevel.DistinctlyElin;

        /// <summary>Which exchange is the request and the promise, when the run has one at all.</summary>
        public const int UndertakingTurn = 3;

        private readonly PlaygroundStage _stage;
        private readonly PlaygroundRun _run;
        private readonly ConversationState _conversation = new ConversationState();
        private readonly DialogueExpressionHistory _history = new DialogueExpressionHistory();
        private readonly WeirdnessBudget _budget = new WeirdnessBudget(Ceiling);
        private readonly List<PlaygroundTurn> _turns = new List<PlaygroundTurn>();

        public PlaygroundExchange(PlaygroundStage stage, PlaygroundRun run)
        {
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public IReadOnlyList<PlaygroundTurn> Turns => _turns;

        /// <summary>What the world held before anybody was asked anything. Zeroed until <see cref="Play"/>.</summary>
        public PlaygroundWorldCounts Before { get; private set; }

        /// <summary>What the world held once everything had been said.</summary>
        public PlaygroundWorldCounts After { get; private set; }

        public ConversationState Conversation => _conversation;

        public DialogueExpressionHistory History => _history;

        public WeirdnessBudget Budget => _budget;

        /// <summary>
        /// Plays the run's turns in order: the question, the question again, and - when the run
        /// asked for one - the undertaking in third place. Every further exchange is the question
        /// again, which is what a repetition sweep needs and what nothing else asks for.
        ///
        /// Days pass between exchanges only when the run said they should. A conversation is
        /// normally one occasion, and the option exists so that a second answer can be taken from
        /// state the first one was not taken from - affect decays, threads advance - rather than
        /// from the conversation having somehow changed its own mind.
        /// </summary>
        public void Play()
        {
            Before = PlaygroundWorldCounts.Of(_stage, _run.Speaker);

            for (int turn = 1; turn <= _run.Turns; turn++)
            {
                if (turn > 1 && _run.DaysBetweenTurns > 0)
                {
                    PlaygroundState.Wait(_stage, _run.DaysBetweenTurns);
                }

                if (turn == UndertakingTurn && _run.Undertaking)
                {
                    Undertake(turn);
                }
                else
                {
                    Ask(turn);
                }
            }

            After = PlaygroundWorldCounts.Of(_stage, _run.Speaker);
        }

        /// <summary>
        /// The listener asks the speaker about the claim, and the speaker answers or does not.
        ///
        /// Asked twice, this is the whole of the multi-turn proof: the second question is the same
        /// question, conversation state says so, and the expression history carried over from the
        /// first exchange narrows which wordings are still fresh.
        /// </summary>
        private PlaygroundTurn Ask(int number)
        {
            PlaygroundTurn turn = new PlaygroundTurn(number, number == 1 ? "question" : "the same question again");
            Open(turn);

            SpeechAct ask = SpeechAct.Compose(
                SpeechActType.Ask,
                _run.Listener,
                _run.Speaker,
                new ActionBinding { PropositionFact = _stage.SubjectFactId });

            if (ask == null)
            {
                turn.Notes.Add("the semantic layer refused the question: "
                    + SpeechAct.WhyNot(
                        SpeechActType.Ask, _run.Listener, new[] { _run.Speaker },
                        new ActionBinding { PropositionFact = _stage.SubjectFactId }, EntityId.None, null));
                return Close(turn);
            }

            turn.Prompt = ask;
            turn.AlreadyAsked = _conversation.WasAlreadyAsked(ask);
            _conversation.Note(ask);

            if (number == 1)
            {
                turn.Reaction = ReactionDerivation.React(
                    _stage.World, _run.Speaker, _stage.SubjectFactId, Ceiling, _stage.Now, _stage.Vanilla);
            }

            turn.Decision = Disclosure.Decide(_stage.World, ask, _run.Speaker, _stage.Now);
            turn.Reply = Disclosure.Compose(turn.Decision, ask);

            if (turn.Reply == null)
            {
                turn.Notes.Add("the decision amounted to no act, so there was nothing to word: "
                    + turn.Decision.Strategy);
                return Close(turn);
            }

            _conversation.Note(turn.Reply);
            turn.Contradiction = _conversation.Contradicts(_stage.World, turn.Reply);

            Recall(turn);
            Word(turn);
            RecordAnyDeception(turn);
            return Close(turn);
        }

        /// <summary>
        /// The listener asks for something and the speaker undertakes it.
        ///
        /// <b>The laboratory composes both acts, and that is the honest description.</b> Nothing in
        /// Core selects a request or a promise: BQ-083 added <see cref="SpeechActType.Promise"/> to
        /// the vocabulary and left choosing one to whoever is holding the conversation. What is
        /// production here is everything downstream - whether the act is well formed at all, and
        /// <see cref="ConversationState.Commit"/>'s rules about which promise becomes durable.
        /// </summary>
        private PlaygroundTurn Undertake(int number)
        {
            PlaygroundTurn turn = new PlaygroundTurn(number, "a request, and the promise that answers it");
            Open(turn);
            turn.Notes.Add("the request and the promise are composed by the laboratory: no Core system "
                + "selects a Promise, which is BQ-083's own boundary");

            ActionBinding undertaking = new ActionBinding
            {
                PropositionFact = _stage.SubjectFactId,
                Purpose = "say what they saw when the guards ask"
            };

            SpeechAct request = SpeechAct.Compose(
                SpeechActType.Request, _run.Listener, _run.Speaker, undertaking);
            if (request == null)
            {
                turn.Notes.Add("the semantic layer refused the request");
                return Close(turn);
            }

            turn.Prompt = request;
            _conversation.Note(request);

            SpeechAct promise = SpeechAct.Compose(
                SpeechActType.Promise, _run.Speaker, _run.Listener, undertaking, EntityId.None, request);
            if (promise == null)
            {
                turn.Notes.Add("the semantic layer refused the promise");
                return Close(turn);
            }

            turn.Reply = promise;
            _conversation.Note(promise);

            Recall(turn);
            Word(turn);

            if (!_run.Commit)
            {
                turn.Notes.Add("--no-commit: the promise was noted transiently and nothing was promoted");
                return Close(turn);
            }

            turn.Committed = _conversation.Commit(_stage.World, promise, _stage.Now, _stage.Zone);
            if (turn.Committed == null)
            {
                turn.Notes.Add("ConversationState declined to promote the promise");
                return Close(turn);
            }

            // Both refusals are writes that do not happen, which is the only way to show a rule
            // that is about restraint. Neither call can leave anything behind: the first is the
            // same act a second time, and the second is an act this conversation never heard.
            turn.Notes.Add(_conversation.Commit(_stage.World, promise, _stage.Now, _stage.Zone) == null
                ? "promoting the same promise again minted nothing: one promise, one obligation"
                : "WARNING: the same promise was promoted twice");

            SpeechAct elsewhere = SpeechAct.Compose(
                SpeechActType.Promise, _run.Speaker, _run.Listener, undertaking);
            turn.Notes.Add(_conversation.Commit(_stage.World, elsewhere, _stage.Now, _stage.Zone) == null
                ? "a promise this conversation never heard was refused promotion"
                : "WARNING: an unheard promise was promoted");

            return Close(turn);
        }

        /// <summary>
        /// Old business, through both of its gates and in that order.
        ///
        /// <see cref="CallbackDisclosure.Best"/> is the safe form and the only one used: taking the
        /// most salient hook and then asking whether it may be spoken loses every sayable callback
        /// standing behind a withheld one. The withheld candidate is fetched separately, purely so
        /// the report can name the claim that closed the gate - it is never handed to wording.
        /// </summary>
        private void Recall(PlaygroundTurn turn)
        {
            CallbackSelection selection = new CallbackSelection { About = _run.Listener };

            turn.Callback = CallbackDisclosure.Best(
                _stage.World, _stage.Vanilla, _run.Speaker, _run.Listener, _stage.Now, selection);

            turn.Recurrence = CallbackDisclosure.BestRecurrence(
                _stage.World,
                _stage.Vanilla,
                _run.Speaker,
                _run.Listener,
                new ContinuityContext(_stage.Situation.Thread?.Id ?? EntityId.None, _stage.Zone),
                _stage.Now,
                selection);

            if (turn.Callback != null)
            {
                return;
            }

            IReadOnlyList<CallbackHook> hooks = CallbackHooks.For(
                _stage.World, _stage.Vanilla, _run.Speaker, _stage.Now, selection);
            for (int i = 0; i < hooks.Count; i++)
            {
                CallbackPermit permit = CallbackDisclosure.Permit(
                    _stage.World, hooks[i], _run.Listener, _stage.Now);
                if (!permit.Allowed)
                {
                    turn.WithheldCallback = permit;
                    return;
                }
            }
        }

        /// <summary>
        /// The words, under every constraint this speaker brings and no others.
        ///
        /// Each one is derived where it belongs - the voice from the profile the run was given, the
        /// vocabulary from BQ-145's reading of the speaker's identity, the forbidden manners from
        /// the rulings the decision already took - and carried here unchanged. A request the
        /// realizer would refuse is not repaired: <see cref="RealizationRequest.WhyNot"/> is
        /// consulted first so the reason reaches the report instead of a silently dropped callback.
        /// </summary>
        private void Word(PlaygroundTurn turn)
        {
            NarrativeNpc speaker = _stage.Npc(_run.Speaker);
            IdentityAffordances identity = IdentityAffordances.Of(speaker, _stage.Vanilla);

            RealizationRequest request = new RealizationRequest(turn.Reply)
            {
                Decision = turn.Decision,
                Claim = turn.Reply.About == _stage.SubjectFactId ? _stage.Subject : null,
                Cast = _stage.Cast,
                Tone = _run.Voice.RequestedTone(),
                Vocabulary = OccupationalVocabulary.RequestedVocabulary(identity),
                Forbidden = NegativeSpaceVoice.ForbiddenManners(
                    turn.Decision == null ? null : turn.Decision.Prohibitions),
                History = _history,
                WeirdnessBudget = _budget,
                Callback = turn.Callback,
                Rng = new DeterministicRng(_stage.Seed).Fork("playground|turn" + turn.Number)
            };

            string refusal = request.WhyNot();
            if (refusal.Length != 0)
            {
                turn.Notes.Add("the wording layer refused the request: " + refusal);
                return;
            }

            turn.Request = request;
            turn.Eligible = PlaygroundEligibility.Of(_stage.Realizer, request);
            turn.Line = _stage.Realizer.Realize(request);
        }

        /// <summary>
        /// A falsehood, filed once. <see cref="Deception.Assess"/> reads the belief graph and
        /// decides; this only asks, and only files what came back as insincere.
        /// </summary>
        private void RecordAnyDeception(PlaygroundTurn turn)
        {
            Veracity veracity = Deception.Assess(_stage.World, turn.Reply);
            if (!veracity.IsLie)
            {
                return;
            }

            turn.RecordedDeception = Deception.Record(_stage.World, turn.Reply, _stage.Now, _stage.Zone);
            _conversation.NoteDeception(turn.RecordedDeception);
        }

        private void Open(PlaygroundTurn turn)
        {
            turn.LedgerBefore = _stage.World.Ledger.Count;
            turn.ObligationsBefore = _stage.World.Obligations.Records.Count;
        }

        private PlaygroundTurn Close(PlaygroundTurn turn)
        {
            turn.LedgerAfter = _stage.World.Ledger.Count;
            turn.ObligationsAfter = _stage.World.Obligations.Records.Count;
            turn.ActsNoted = _conversation.Acts.Count;
            turn.Unanswered = _conversation.UnansweredQuestions.Count;
            _turns.Add(turn);
            return turn;
        }
    }
}
