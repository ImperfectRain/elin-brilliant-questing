using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Two statements that cannot both stand, from the same speaker inside one conversation.
    ///
    /// Deliberately not <see cref="Contradiction"/> (BQ-073): that type is one person's belief set
    /// against somebody else's testimony, read from history and needing an observer who was there.
    /// This is a speaker's own words against their own earlier words, read from nothing but the
    /// exchange in progress - "that is not what you said five minutes ago" needs no belief graph,
    /// only a memory of the five minutes.
    /// </summary>
    public readonly struct DiscourseContradiction
    {
        internal DiscourseContradiction(SpeechAct earlier, SpeechAct later, string because)
        {
            Earlier = earlier;
            Later = later;
            Because = because ?? string.Empty;
        }

        /// <summary>What they said first.</summary>
        public SpeechAct Earlier { get; }

        /// <summary>What they just said, which does not square with it.</summary>
        public SpeechAct Later { get; }

        /// <summary>Which rule caught it, in words. Diagnostic; nothing branches on it.</summary>
        public string Because { get; }
    }

    /// <summary>
    /// Short-term discourse memory for one conversation in progress (BQ-083, CD §28.5).
    ///
    /// BQ-070 through BQ-077 gave a conversation meaning, tone and character, one exchange at a
    /// time; nothing so far remembers the exchange itself. Without this, an NPC can be asked the
    /// same question twice without noticing, can contradict what they said minutes earlier and
    /// nobody present can hold it against them, and a promise made in dialogue evaporates the
    /// moment the scene ends. This layer holds exactly that, and nothing else.
    ///
    /// <b>It is transient by construction.</b> An instance belongs to whichever caller is running
    /// one conversation - built when it starts, discarded when it ends, the same lifecycle
    /// <c>DialogueExpressionHistory</c> (BQ-078) already has. It is never attached to
    /// <c>NarrativeNpc</c>, never saved, and adds no schema: `docs/agent/decisions.md` (D037) says
    /// this in advance, and this type is what it was said about.
    ///
    /// <b>It is not a second belief graph, event ledger, relationship store or obligation
    /// system.</b> It holds <see cref="SpeechAct"/> instances the caller already produced and
    /// reads them back; it does not decide what is true, does not decide what anybody believes,
    /// and does not itself judge who owes whom anything long-term. What was said and heard just
    /// now is this type's whole authority.
    ///
    /// <b>Lies stay BQ-073's.</b> <see cref="NoteDeception"/> files away the
    /// <see cref="RecordedStatement"/> a caller already obtained from <see cref="Deception.Record"/>;
    /// it never classifies an assertion and never writes a second deception record, so a lie told
    /// in this conversation is filed once, not twice.
    ///
    /// <b>A commitment survives the conversation only when something says it should.</b>
    /// <see cref="Commit"/> is the one write this type performs, and it is never automatic: every
    /// <see cref="SpeechActType.Promise"/> is noted like any other act, and only the ones a caller
    /// hands to <see cref="Commit"/> become a durable <see cref="SocialObligation"/> - the ledger
    /// BQ-071's disclosure pressure, BQ-077's negative-space lines and the standing sheet already
    /// read. There is no second obligation system here, only a doorway into the one that exists,
    /// and the doorway opens onto this conversation only: a promise this exchange never heard is
    /// not one it can vouch for, whoever asks it to.
    /// </summary>
    public sealed class ConversationState
    {
        private readonly List<SpeechAct> _acts = new List<SpeechAct>();
        private readonly HashSet<SpeechAct> _resolved = new HashSet<SpeechAct>();
        private readonly HashSet<SpeechAct> _committed = new HashSet<SpeechAct>();
        private readonly List<RecordedStatement> _deceptions = new List<RecordedStatement>();

        /// <summary>Every act exchanged so far, in the order it was said.</summary>
        public IReadOnlyList<SpeechAct> Acts => _acts;

        /// <summary>Every lie this conversation has been told, as BQ-073 already recorded it.</summary>
        public IReadOnlyList<RecordedStatement> LiesTold => _deceptions;

        /// <summary>
        /// Adds one act to the transcript. If it responds to an earlier one, that earlier act
        /// stops counting as unanswered - whatever it was answered with, including a refusal or an
        /// evasion, both of which are a response and neither of which is silence.
        /// </summary>
        public void Note(SpeechAct act)
        {
            if (act == null)
            {
                return;
            }

            if (act.InReplyTo != null)
            {
                _resolved.Add(act.InReplyTo);
            }

            _acts.Add(act);
        }

        /// <summary>
        /// Files a lie this conversation already produced. Takes the event BQ-073's
        /// <see cref="Deception.Record"/> wrote and reads it back with <see cref="Deception.StatementOf"/>;
        /// never assesses, never records, so calling this can never mint a second trace of the
        /// same lie.
        ///
        /// Filing one twice is filing, not lying twice. A caller that notes a deception where it
        /// happens and again on a sweep of the ledger is describing one event both times, and
        /// <see cref="RecordedStatement.EventId"/> is what says so - the ledger entry's own
        /// identity, which is the only identity a statement has that survives being read back.
        /// Comparing on it rather than on the struct keeps "filed once, not twice" true of the
        /// transient list as well as of the durable record, which is what the sentence was always
        /// meant to promise.
        /// </summary>
        public void NoteDeception(WorldEvent recorded)
        {
            RecordedStatement statement = Deception.StatementOf(recorded);
            if (!statement.Recognized)
            {
                return;
            }

            for (int i = 0; i < _deceptions.Count; i++)
            {
                if (_deceptions[i].EventId == statement.EventId)
                {
                    return;
                }
            }

            _deceptions.Add(statement);
        }

        /// <summary>Every question asked so far.</summary>
        public IReadOnlyList<SpeechAct> Questions => OfType(SpeechActType.Ask);

        /// <summary>
        /// Questions nobody has yet responded to. Distinct from a question no one intends to
        /// answer - a refusal or an evasion closes this list the same as a straight answer does,
        /// because all three are somebody having addressed the question, and only silence has not.
        /// </summary>
        public IReadOnlyList<SpeechAct> UnansweredQuestions
        {
            get
            {
                List<SpeechAct> unanswered = new List<SpeechAct>();
                for (int i = 0; i < _acts.Count; i++)
                {
                    SpeechAct act = _acts[i];
                    if (act.Type == SpeechActType.Ask && !_resolved.Contains(act))
                    {
                        unanswered.Add(act);
                    }
                }

                return unanswered;
            }
        }

        /// <summary>Every assertion made so far - an act that put a claim forward as so or not so.</summary>
        public IReadOnlyList<SpeechAct> Claims
        {
            get
            {
                List<SpeechAct> claims = new List<SpeechAct>();
                for (int i = 0; i < _acts.Count; i++)
                {
                    SpeechActStance stance = _acts[i].Stance;
                    if (stance == SpeechActStance.Affirms || stance == SpeechActStance.Denies)
                    {
                        claims.Add(_acts[i]);
                    }
                }

                return claims;
            }
        }

        /// <summary>
        /// "You already asked me that." True when the same speaker has already asked about the
        /// identical matter earlier in this conversation - the same claim, item, destination or
        /// purpose the action layer already treats as one matter, never a comparison of wording.
        /// </summary>
        public bool WasAlreadyAsked(SpeechAct question)
        {
            if (question == null || question.Type != SpeechActType.Ask)
            {
                return false;
            }

            for (int i = 0; i < _acts.Count; i++)
            {
                SpeechAct earlier = _acts[i];
                if (earlier == question || earlier.Type != SpeechActType.Ask)
                {
                    continue;
                }

                if (earlier.Speaker == question.Speaker && SameMatter(earlier.Content, question.Content))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// "That is not what you said five minutes ago." Whether this statement conflicts with an
        /// earlier one the same speaker made in this conversation, or null when it does not.
        ///
        /// Two shapes, and they mirror the two shapes <see cref="Deception.Assess"/> already
        /// classifies as insincere against belief - here read statement against statement instead:
        /// the same claim put forward the other way round, or a rival version of the claim (the
        /// same story with a different subject, structurally, per <c>Fact.DistortionOf</c>) put
        /// forward as though it were the first. Only assertions can conflict; a question, a
        /// request or a promise puts nothing forward to be squared with anything.
        ///
        /// This never consults the belief graph or the event ledger - both statements have to
        /// already be in this conversation for the comparison to run, so the "actor who can
        /// legitimately compare" is simply whoever is in the room for both of them. Reaching
        /// further back is BQ-073's <see cref="Deception.Contradictions"/>, which reads durable
        /// history instead of this scene.
        /// </summary>
        public DiscourseContradiction? Contradicts(NarrativeWorldState world, SpeechAct statement)
        {
            if (statement == null || !IsAssertion(statement.Stance))
            {
                return null;
            }

            for (int i = 0; i < _acts.Count; i++)
            {
                SpeechAct earlier = _acts[i];
                if (earlier == statement)
                {
                    continue;
                }

                DiscourseContradiction? found = Conflict(world, earlier, statement);
                if (found.HasValue)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Every self-contradiction sitting in the transcript so far, oldest offending pair first -
        /// the post-hoc audit <see cref="NarrativeInspector"/> uses to dump a whole conversation,
        /// as opposed to <see cref="Contradicts"/> checking one new statement against what came
        /// before it.
        /// </summary>
        public IReadOnlyList<DiscourseContradiction> AllContradictions(NarrativeWorldState world)
        {
            List<DiscourseContradiction> found = new List<DiscourseContradiction>();
            for (int later = 1; later < _acts.Count; later++)
            {
                for (int earlier = 0; earlier < later; earlier++)
                {
                    DiscourseContradiction? conflict = Conflict(world, _acts[earlier], _acts[later]);
                    if (conflict.HasValue)
                    {
                        found.Add(conflict.Value);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Whether <paramref name="later"/> conflicts with <paramref name="earlier"/>: the same
        /// claim put the other way round, or a rival version of it - the two shapes
        /// <see cref="Deception.Assess"/> already treats as insincere against belief, read here
        /// statement against statement instead.
        /// </summary>
        private static DiscourseContradiction? Conflict(NarrativeWorldState world, SpeechAct earlier, SpeechAct later)
        {
            if (earlier.Speaker != later.Speaker || !IsAssertion(earlier.Stance) || !IsAssertion(later.Stance))
            {
                return null;
            }

            EntityId earlierClaim = earlier.About;
            EntityId laterClaim = later.About;
            if (earlierClaim.IsNone || laterClaim.IsNone)
            {
                return null;
            }

            if (earlierClaim == laterClaim)
            {
                return earlier.Stance != later.Stance
                    ? new DiscourseContradiction(
                        earlier, later, "said " + earlier.Stance + " of this and now says " + later.Stance)
                    : (DiscourseContradiction?)null;
            }

            if (world != null && earlier.Stance == SpeechActStance.Affirms && later.Stance == SpeechActStance.Affirms)
            {
                Fact earlierFact = world.Knowledge.GetFact(earlierClaim);
                Fact laterFact = world.Knowledge.GetFact(laterClaim);
                if (earlierFact != null && laterFact != null && Deception.Rivals(earlierFact, laterFact))
                {
                    return new DiscourseContradiction(earlier, later, "put forward a rival version of an earlier claim");
                }
            }

            return null;
        }

        /// <summary>
        /// Promotes a promise into the durable obligation ledger BQ-071's disclosure pressure and
        /// the standing sheet already read - the same shape <c>ConsequenceEngine.AccrueFavor</c>
        /// already writes for a kept favor: a <see cref="WorldEventType.PromiseMade"/> event first,
        /// then a <see cref="SocialObligation"/> naming it as its source.
        ///
        /// Never automatic. Every promise made is noted transiently like any other act; this is
        /// the one call that says a particular one matters once the conversation is over, and
        /// deciding that is left entirely to the caller. Returns null and writes nothing for
        /// anything that is not a well-formed promise, and for a promise already committed once -
        /// the same act handed here twice mints one obligation, not two.
        ///
        /// <b>It promotes this conversation's promises and no others.</b> An act that was never
        /// <see cref="Note"/>d here is refused, because the whole of what this type is entitled to
        /// say about a promise is that it was said in the exchange it holds. Promoting one from
        /// somewhere else would be conversation state vouching for words it never heard - the
        /// durable ledger would carry an obligation whose only witness is a caller's say-so, and
        /// "a promise made in this conversation becomes durable only when explicitly promoted"
        /// would be a rule about the second half of the sentence alone.
        ///
        /// <b>Who is owed is named, never inferred from position.</b> <see cref="SpeechAct"/>
        /// sorts its audience by id and says in as many words that the order is staging rather
        /// than meaning, so "the first addressee" is not a fact about the promise - it is a fact
        /// about how two ids happen to sort. A promise made to one person needs no help: that
        /// person is the creditor. A promise made in front of several has to say which of them it
        /// is <em>to</em>, and <paramref name="creditor"/> is where the caller says it; anybody
        /// else addressed is a witness to the event, which is what they were. Refusing the
        /// ambiguous case is deliberate - one debtor and one creditor is the shape
        /// <see cref="SocialObligation"/> has, and inventing a promise owed to a group would be
        /// inventing an obligation kind the ledger does not model.
        /// </summary>
        public WorldEvent Commit(
            NarrativeWorldState world,
            SpeechAct promise,
            GameTime now,
            EntityId zone = default,
            EntityId creditor = default)
        {
            if (world == null
                || promise == null
                || promise.Type != SpeechActType.Promise
                || !WasNoted(promise)
                || _committed.Contains(promise))
            {
                return null;
            }

            EntityId owed = Creditor(promise, creditor);
            if (owed.IsNone)
            {
                return null;
            }

            EntityId debtor = promise.Speaker;

            List<EntityId> related = null;
            if (!promise.Content.PropositionFact.IsNone)
            {
                related = new List<EntityId> { promise.Content.PropositionFact };
            }

            List<EntityId> witnesses = null;
            for (int i = 0; i < promise.Addressees.Count; i++)
            {
                if (promise.Addressees[i] == owed)
                {
                    continue;
                }

                witnesses = witnesses ?? new List<EntityId>();
                witnesses.Add(promise.Addressees[i]);
            }

            WorldEvent recorded = world.Record(
                WorldEventType.PromiseMade, debtor, owed, now, 0.5, zone, related, witnesses);

            world.Obligations.Add(new SocialObligation(
                world.NewId("obl"),
                SocialObligationKind.Promise,
                debtor,
                owed,
                promise.Content.PropositionFact,
                promise.Content.Purpose ?? string.Empty,
                now,
                recorded.Id));

            _committed.Add(promise);
            return recorded;
        }

        /// <summary>
        /// Who the promise is owed to, or nobody when that cannot be said without guessing.
        ///
        /// Unnamed and one addressee is the ordinary case and needs nothing from the caller.
        /// Unnamed and several is the case with no answer in the act, and there is no default
        /// that is not a guess. A name that was not spoken to is not a creditor of anything said
        /// here.
        /// </summary>
        private static EntityId Creditor(SpeechAct promise, EntityId named)
        {
            if (named.IsNone)
            {
                return promise.Addressees.Count == 1 ? promise.Addressees[0] : EntityId.None;
            }

            return promise.IsAddressedTo(named) ? named : EntityId.None;
        }

        /// <summary>
        /// Whether this exact act is in the transcript. Identity, not equivalence: two promises of
        /// the same thing said by the same person are two promises, and only the one this
        /// conversation actually heard is this conversation's to promote.
        /// </summary>
        private bool WasNoted(SpeechAct act)
        {
            for (int i = 0; i < _acts.Count; i++)
            {
                if (ReferenceEquals(_acts[i], act))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAssertion(SpeechActStance stance)
        {
            return stance == SpeechActStance.Affirms || stance == SpeechActStance.Denies;
        }

        private static bool SameMatter(ActionBinding a, ActionBinding b)
        {
            a = a ?? ActionBinding.Empty;
            b = b ?? ActionBinding.Empty;

            if (a.HasProposition || b.HasProposition)
            {
                return a.PropositionFact == b.PropositionFact;
            }

            if (a.HasItem || b.HasItem)
            {
                return a.Item == b.Item;
            }

            if (a.HasDestination || b.HasDestination)
            {
                return a.Destination == b.Destination;
            }

            return string.Equals(a.Purpose ?? string.Empty, b.Purpose ?? string.Empty, System.StringComparison.Ordinal);
        }

        private List<SpeechAct> OfType(SpeechActType type)
        {
            List<SpeechAct> found = new List<SpeechAct>();
            for (int i = 0; i < _acts.Count; i++)
            {
                if (_acts[i].Type == type)
                {
                    found.Add(_acts[i]);
                }
            }

            return found;
        }
    }
}
