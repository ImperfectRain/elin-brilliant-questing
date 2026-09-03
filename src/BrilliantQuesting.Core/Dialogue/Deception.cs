using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// How an assertion stands to the belief of whoever made it (BQ-073, CD §14.5, §17.5).
    ///
    /// The axis a deception layer actually turns on, and it is about the speaker rather than about
    /// the world. Truth is not what makes somebody a liar - a witness repeating a story that
    /// happens to be wrong is mistaken, and a scoundrel repeating one that happens to be right is
    /// still lying about how they know it. So this is read from the belief graph, which is the
    /// only authority on what a character holds, and the world's own truth is reported beside it
    /// rather than folded into it.
    /// </summary>
    public enum Sincerity
    {
        /// <summary>
        /// The act put no claim forward: a question, a request, a refusal, an evasion. Nothing to
        /// be sincere or insincere about, which is why evading is never lying however evasive.
        /// </summary>
        NotAsserted = 0,

        /// <summary>
        /// What was said agrees with what the speaker holds. Says nothing about whether it is so:
        /// a sincere assertion of something false is an honest mistake, and keeping that separate
        /// from a lie is most of what this type exists for.
        /// </summary>
        Sincere = 1,

        /// <summary>
        /// Asserted with no belief behind it either way - not contradicted, not held. Reckless
        /// rather than dishonest, and deliberately not counted as a lie: somebody who repeats
        /// what they were handed without forming a view has not decided to deceive anybody, and
        /// the world would be recording a deception with no deceiver.
        ///
        /// It has no counterpart on the denying side. Denying a claim you have never held is an
        /// ordinary thing to do and consistent with everything you believe, so it is
        /// <see cref="Sincere"/>; asserting one is not.
        /// </summary>
        Unfounded = 2,

        /// <summary>
        /// What was said materially conflicts with what the speaker holds, firmly enough that
        /// they cannot be mistaken about their own mind: they denied a claim they hold, or put
        /// forward a rival version of one they hold. This is the lie.
        /// </summary>
        Insincere = 3
    }

    /// <summary>
    /// What one assertion amounts to, against the speaker's belief and against the world - kept as
    /// two separate readings that are never mixed.
    ///
    /// <see cref="Sincerity"/> is decided from the belief graph alone. <see cref="Accuracy"/> is
    /// the world's own view of what was asserted and exists so that an honest mistake is legible
    /// as one; nothing about the classification consults it, because a deception model that
    /// needed omniscient truth to work would silently make every wrong statement a lie.
    /// </summary>
    public readonly struct Veracity
    {
        internal Veracity(
            Sincerity sincerity,
            EntityId assertedClaim,
            SpeechActStance stance,
            EntityId contradicts,
            double conviction,
            TruthState accuracy,
            bool claimIsModelled,
            string because)
        {
            Sincerity = sincerity;
            AssertedClaim = assertedClaim;
            Stance = stance;
            Contradicts = contradicts;
            Conviction = conviction;
            Accuracy = accuracy;
            ClaimIsModelled = claimIsModelled;
            Because = because ?? string.Empty;
        }

        public Sincerity Sincerity { get; }

        /// <summary>The claim put forward, whichever way it was put.</summary>
        public EntityId AssertedClaim { get; }

        /// <summary>Whether it was put forward as so or as not so.</summary>
        public SpeechActStance Stance { get; }

        /// <summary>
        /// The belief the assertion runs against, when it runs against one. The same claim for a
        /// denial - denying what you hold contradicts the very thing it names - and a different
        /// claim when the speaker put a rival version forward instead.
        /// </summary>
        public EntityId Contradicts { get; }

        /// <summary>How firmly the speaker holds the belief they spoke against. Zero when they hold none.</summary>
        public double Conviction { get; }

        /// <summary>
        /// The world's truth of what was asserted, with a denial read the way it was meant:
        /// denying a claim the world holds false is an accurate assertion.
        ///
        /// Diagnostic and for later systems, never an input to <see cref="Sincerity"/>.
        /// <see cref="TruthState.Uncertain"/> when the world does not commit, which includes the
        /// case where the claim is not in the graph at all.
        /// </summary>
        public TruthState Accuracy { get; }

        /// <summary>Whether the claim exists in the fact graph, so <see cref="Accuracy"/> means anything.</summary>
        public bool ClaimIsModelled { get; }

        /// <summary>Which rule decided this, in words. Diagnostic; nothing branches on it.</summary>
        public string Because { get; }

        /// <summary>A deliberate falsehood, and the only thing the world records as a deception.</summary>
        public bool IsLie => Sincerity == Sincerity.Insincere;

        /// <summary>
        /// Said in good faith and untrue. The case that must never be confused with a lie: the
        /// witness who believed the garbled version and passed it on has done nothing dishonest,
        /// and a world that recorded them as a liar would poison their reputation for being wrong.
        /// </summary>
        public bool IsHonestMistake =>
            Sincerity == Sincerity.Sincere && ClaimIsModelled && Accuracy == TruthState.False;

        public override string ToString()
        {
            return Sincerity + " " + Stance + " " + AssertedClaim.Value;
        }
    }

    /// <summary>
    /// One statement recovered from history: who said what to whom, which way round, and what it
    /// ran against.
    ///
    /// The reason the ledger entry is not read field by field at call sites. A recorded deception
    /// is a small semantic structure - speaker, audience, claim, stance - and later systems
    /// (contradiction, memory, rumour, conversation state) need all four; leaving them to be
    /// unpacked from a related-id list by convention would make the convention the contract.
    /// </summary>
    public readonly struct RecordedStatement
    {
        private static readonly EntityId[] Nobody = new EntityId[0];

        internal RecordedStatement(
            EntityId eventId,
            EntityId speaker,
            EntityId audience,
            IReadOnlyList<EntityId> alsoHeard,
            EntityId assertedClaim,
            SpeechActStance stance,
            EntityId contradicts,
            GameTime when)
        {
            EventId = eventId;
            Speaker = speaker;
            Audience = audience;
            AlsoHeard = alsoHeard ?? Nobody;
            AssertedClaim = assertedClaim;
            Stance = stance;
            Contradicts = contradicts;
            When = when;
        }

        public EntityId EventId { get; }

        public EntityId Speaker { get; }

        /// <summary>Who it was said to.</summary>
        public EntityId Audience { get; }

        /// <summary>Anybody else who was there for it.</summary>
        public IReadOnlyList<EntityId> AlsoHeard { get; }

        public EntityId AssertedClaim { get; }

        public SpeechActStance Stance { get; }

        /// <summary>The claim the speaker actually held, which is the same one for a denial.</summary>
        public EntityId Contradicts { get; }

        public GameTime When { get; }

        /// <summary>
        /// Whether this is a statement whose meaning is fully recorded. False for a deception
        /// written before the stance vocabulary existed, or by a layer that never knew which way
        /// the claim was put: such an entry says a deception happened without saying what was
        /// said, and guessing the missing half would invent testimony.
        /// </summary>
        public bool Recognized => !EventId.IsNone;

        public bool WasHeardBy(EntityId listener)
        {
            if (listener == Audience)
            {
                return true;
            }

            for (int i = 0; i < AlsoHeard.Count; i++)
            {
                if (AlsoHeard[i] == listener)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Somebody now holds a belief that a statement they heard cannot be squared with.
    ///
    /// Held from the observer's side on purpose. Catching a liar is not the world announcing that
    /// a lie occurred - the ledger has known that since the moment it happened - it is one person
    /// coming to hold something that contradicts what they were told, which is why this needs an
    /// observer, why it needs them to have actually heard the statement, and why
    /// <see cref="CanProve"/> is reported separately from <see cref="Confidence"/>: being sure
    /// somebody lied and being able to show it are the two different things the whole knowledge
    /// layer is built to keep apart.
    /// </summary>
    public readonly struct Contradiction
    {
        internal Contradiction(
            RecordedStatement statement,
            EntityId observer,
            EntityId contradictingBelief,
            double confidence,
            bool canProve)
        {
            Statement = statement;
            Observer = observer;
            ContradictingBelief = contradictingBelief;
            Confidence = confidence;
            CanProve = canProve;
        }

        public RecordedStatement Statement { get; }

        public EntityId Observer { get; }

        /// <summary>The claim the observer holds that the statement runs against.</summary>
        public EntityId ContradictingBelief { get; }

        /// <summary>How firmly the observer holds it.</summary>
        public double Confidence { get; }

        /// <summary>Whether they could show a third party, rather than merely be sure.</summary>
        public bool CanProve { get; }

        public EntityId Liar => Statement.Speaker;

        public override string ToString()
        {
            return Statement.Speaker.Value + " told " + Observer.Value + " " + Statement.Stance
                + " " + Statement.AssertedClaim.Value + (CanProve ? " - provably otherwise" : " - believed otherwise");
        }
    }

    /// <summary>
    /// Lying as a semantic outcome rather than a way of writing a line (BQ-073, CD §17.5, §14.5).
    ///
    /// BQ-071 established that knowing a thing does not mean telling it, and stopped short of
    /// letting anybody say something untrue: every rung of its ladder and every rung of BQ-072's
    /// depth was the truth, less of it. That leaves the world one move short. An interrogation in
    /// which the guilty party can only decline is an interrogation the player wins by noticing who
    /// declined, and a rumour layer that can seed a false belief (BQ-020) while no character can
    /// assert one to your face has its deceptions happening off-stage.
    ///
    /// Three commitments hold this in place.
    ///
    /// <b>Belief is the authority, not truth.</b> A lie is an assertion that materially conflicts
    /// with the speaker's own belief, held firmly enough that they cannot be mistaken about it.
    /// Nothing here asks whether the claim is actually so in order to decide, so nobody is made a
    /// liar by being wrong, and no omniscient view has to be invented to classify anybody. The
    /// world's truth is reported beside the verdict, which is what makes an honest mistake legible
    /// as one rather than invisible.
    ///
    /// <b>Withholding is not asserting.</b> Refusing, changing the subject, answering a
    /// neighbouring question and giving a shallower answer are four ways of not saying something
    /// and none of them puts a claim forward. <see cref="SpeechActType.Evade"/> carries no
    /// proposition at all, which is a structural guarantee rather than a convention: there is
    /// nothing on the act for a scorer to read as an assertion.
    ///
    /// <b>What was said survives being said.</b> A recorded deception keeps the claim, the stance
    /// and the audience, so a later system can ask what somebody actually committed to rather than
    /// only that they once deceived somebody. That is what makes the contradiction catchable, and
    /// it is deliberately the whole of the conversational bookkeeping here - who is owed an answer
    /// and what a conversation is currently about is BQ-083's, not this step's.
    /// </summary>
    public static class Deception
    {
        /// <summary>
        /// How firmly a belief must be held before speaking against it is a lie rather than a
        /// muddle. The same figure disclosure uses to decide whether somebody stands behind a
        /// claim, because it is the same question: a belief too weak to be stood behind is too
        /// weak to be knowingly contradicted.
        /// </summary>
        public const double ConvictionToKnowBetter = Disclosure.ConvictionToStandBehind;

        /// <summary>
        /// What this act amounts to, given what its speaker believes. Never throws and never
        /// writes; an act nobody can be found to have a belief about comes back honestly rather
        /// than as a lie.
        /// </summary>
        public static Veracity Assess(NarrativeWorldState world, SpeechAct act)
        {
            if (world == null || act == null)
            {
                return new Veracity(
                    Sincerity.NotAsserted, EntityId.None, SpeechActStance.None, EntityId.None, 0.0,
                    TruthState.Uncertain, false, "no act to assess");
            }

            SpeechActStance stance = act.Stance;
            if (stance != SpeechActStance.Affirms && stance != SpeechActStance.Denies)
            {
                return new Veracity(
                    Sincerity.NotAsserted, act.About, stance, EntityId.None, 0.0,
                    TruthState.Uncertain, false, act.Type + " puts no claim forward either way");
            }

            EntityId claimId = act.About;
            if (claimId.IsNone)
            {
                return new Veracity(
                    Sincerity.NotAsserted, EntityId.None, stance, EntityId.None, 0.0,
                    TruthState.Uncertain, false, act.Type + " names no claim");
            }

            Fact claim = world.Knowledge.GetFact(claimId);
            TruthState accuracy = Accuracy(claim, stance);
            bool modelled = claim != null;

            bool holds = world.Knowledge.TryGetBelief(act.Speaker, claimId, out KnowledgeRecord belief);
            double conviction = holds ? belief.Confidence : 0.0;

            if (stance == SpeechActStance.Denies)
            {
                if (holds && conviction >= ConvictionToKnowBetter)
                {
                    return new Veracity(
                        Sincerity.Insincere, claimId, stance, claimId, conviction, accuracy, modelled,
                        "denies a claim they hold at " + conviction.ToString("0.00"));
                }

                return new Veracity(
                    Sincerity.Sincere, claimId, stance, EntityId.None, conviction, accuracy, modelled,
                    holds
                        ? "denies a claim they hold too weakly to be contradicting"
                        : "denies a claim they do not hold");
            }

            if (holds && conviction >= ConvictionToKnowBetter)
            {
                return new Veracity(
                    Sincerity.Sincere, claimId, stance, EntityId.None, conviction, accuracy, modelled,
                    "asserts what they believe at " + conviction.ToString("0.00"));
            }

            EntityId rival = RivalHeldBy(world, act.Speaker, claim, out double rivalConviction);
            if (!rival.IsNone)
            {
                return new Veracity(
                    Sincerity.Insincere, claimId, stance, rival, rivalConviction, accuracy, modelled,
                    "asserts one version while holding another at " + rivalConviction.ToString("0.00"));
            }

            if (holds)
            {
                return new Veracity(
                    Sincerity.Sincere, claimId, stance, EntityId.None, conviction, accuracy, modelled,
                    "asserts what they believe, weakly, at " + conviction.ToString("0.00"));
            }

            return new Veracity(
                Sincerity.Unfounded, claimId, stance, EntityId.None, 0.0, accuracy, modelled,
                "asserts a claim they hold no belief about");
        }

        /// <summary>
        /// Writes the lie into history, or does nothing at all when the act was not one.
        ///
        /// Two records, because they answer two questions and only one of them is about this
        /// conversation. `X lied_about F` is a durable fact of the world, kept once per speaker
        /// and claim however often they repeat it, known to nobody but the liar
        /// (<see cref="DeceptionRecord"/>, shared with BQ-020 so a seeded rumour and a lie told to
        /// somebody's face leave the same trace). The event is the statement: who was told, when,
        /// which claim and which way round. Losing either would lose something - the fact alone
        /// cannot say what was said, and the event alone cannot say that the person has form.
        ///
        /// Returns the event, so a caller can attach it to whatever it is doing, or null when
        /// there was nothing to record. Assessing is separate and free of side effects, so a
        /// caller may look before writing.
        /// </summary>
        public static WorldEvent Record(NarrativeWorldState world, SpeechAct act, GameTime now, EntityId zone = default)
        {
            if (world == null || act == null)
            {
                return null;
            }

            Veracity veracity = Assess(world, act);
            if (!veracity.IsLie || act.Addressees.Count == 0)
            {
                return null;
            }

            DeceptionRecord.Of(world.Knowledge, world.Ids, act.Speaker, veracity.Contradicts, now);

            // The claim put forward first, and the belief it runs against second when that is a
            // different claim. A denial contradicts the very claim it names, so it carries one -
            // and `StatementOf` is what reads this back, so no consumer depends on the layout.
            List<EntityId> related = new List<EntityId> { veracity.AssertedClaim };
            if (veracity.Contradicts != veracity.AssertedClaim && !veracity.Contradicts.IsNone)
            {
                related.Add(veracity.Contradicts);
            }

            List<EntityId> alsoHeard = new List<EntityId>();
            for (int i = 1; i < act.Addressees.Count; i++)
            {
                alsoHeard.Add(act.Addressees[i]);
            }

            return world.Record(
                WorldEventType.Deceived,
                act.Speaker,
                act.Addressees[0],
                now,
                // How knowingly, not how convincingly: what the ledger can honestly say about the
                // moment is how firmly the speaker held the thing they spoke against.
                veracity.Conviction,
                zone,
                related,
                alsoHeard,
                null,
                new[] { veracity.Stance == SpeechActStance.Denies ? EventTags.Denied : EventTags.Affirmed });
        }

        /// <summary>
        /// The statement a recorded deception was, or an unrecognized one for an entry that does
        /// not carry its meaning.
        /// </summary>
        public static RecordedStatement StatementOf(WorldEvent recorded)
        {
            if (recorded == null || recorded.Type != WorldEventType.Deceived || recorded.Related.Count == 0)
            {
                return default;
            }

            SpeechActStance stance;
            if (Tagged(recorded, EventTags.Denied))
            {
                stance = SpeechActStance.Denies;
            }
            else if (Tagged(recorded, EventTags.Affirmed))
            {
                stance = SpeechActStance.Affirms;
            }
            else
            {
                return default;
            }

            EntityId asserted = recorded.Related[0];
            EntityId contradicts = recorded.Related.Count > 1 ? recorded.Related[1] : asserted;

            return new RecordedStatement(
                recorded.Id,
                recorded.Actor,
                recorded.Target,
                recorded.Witnesses,
                asserted,
                stance,
                contradicts,
                recorded.Time);
        }

        /// <summary>
        /// Every statement this observer was told that they are now in a position to contradict,
        /// oldest first.
        ///
        /// Two conditions, and both are about the observer rather than about the world. They must
        /// have been there - a lie told in another room is not something they caught, whatever the
        /// ledger knows - and they must now hold, firmly, a belief the statement cannot be squared
        /// with. Nothing consults whether the claim is actually true, so this stays a reading of
        /// one character's knowledge and never becomes a hint dispensed by an omniscient narrator.
        ///
        /// A speaker never catches themself. The liar holds the contradicting belief by
        /// definition, and returning their own lies to them would make the list meaningless.
        /// </summary>
        public static IReadOnlyList<Contradiction> Contradictions(NarrativeWorldState world, EntityId observer)
        {
            List<Contradiction> caught = new List<Contradiction>();
            if (world == null || observer.IsNone)
            {
                return caught;
            }

            foreach (WorldEvent recorded in world.Ledger.OfType(WorldEventType.Deceived))
            {
                RecordedStatement statement = StatementOf(recorded);
                if (!statement.Recognized
                    || statement.Speaker == observer
                    || !statement.WasHeardBy(observer))
                {
                    continue;
                }

                EntityId against = HeldAgainst(world, observer, statement, out double confidence);
                if (against.IsNone)
                {
                    continue;
                }

                caught.Add(new Contradiction(
                    statement, observer, against, confidence, world.Knowledge.CanProve(observer, against)));
            }

            return caught;
        }

        /// <summary>
        /// Whether this observer holds something firm enough to put against the statement, and
        /// what it is.
        ///
        /// The mirror of <see cref="Assess"/> and deliberately the same two shapes: a denial is
        /// caught by holding the claim that was denied, and a substituted version is caught by
        /// holding a rival of it. Anything else - doubt, a weak belief, a hunch - is not catching
        /// somebody out, and treating it as one would let a suspicious player convict anybody.
        /// </summary>
        private static EntityId HeldAgainst(
            NarrativeWorldState world,
            EntityId observer,
            RecordedStatement statement,
            out double confidence)
        {
            confidence = 0.0;

            if (statement.Stance == SpeechActStance.Denies)
            {
                if (world.Knowledge.TryGetBelief(observer, statement.AssertedClaim, out KnowledgeRecord held)
                    && held.Confidence >= ConvictionToKnowBetter)
                {
                    confidence = held.Confidence;
                    return statement.AssertedClaim;
                }

                return EntityId.None;
            }

            EntityId rival = RivalHeldBy(
                world, observer, world.Knowledge.GetFact(statement.AssertedClaim), out double rivalConviction);
            confidence = rivalConviction;
            return rival;
        }

        /// <summary>
        /// A claim this person holds firmly that is a rival version of the given one: the same
        /// story with somebody else's name in it.
        ///
        /// Rival is a structural relation, not a similarity: the two are versions of one claim
        /// (<c>Fact.DistortionOf</c>, which BQ-020 already maintains) and they name different
        /// subjects, so they cannot both be so. Holding an unrelated fact about the same person is
        /// not a contradiction, and neither is holding the same claim less confidently.
        /// </summary>
        private static EntityId RivalHeldBy(
            NarrativeWorldState world,
            EntityId holder,
            Fact claim,
            out double conviction)
        {
            conviction = 0.0;
            if (claim == null)
            {
                return EntityId.None;
            }

            foreach (KnowledgeRecord held in world.Knowledge.BeliefsOf(holder))
            {
                if (held.FactId == claim.Id || held.Confidence < ConvictionToKnowBetter)
                {
                    continue;
                }

                Fact other = world.Knowledge.GetFact(held.FactId);
                if (other == null || other.Subject == claim.Subject || !Rivals(other, claim))
                {
                    continue;
                }

                conviction = held.Confidence;
                return other.Id;
            }

            return EntityId.None;
        }

        /// <summary>
        /// Whether two claims are versions of one story that cannot both be so - the same
        /// structural test <see cref="Assess"/> uses against belief, exposed so BQ-083's
        /// conversation state can ask the identical question of two statements instead of growing
        /// a second opinion about what counts as a rival.
        /// </summary>
        internal static bool Rivals(Fact a, Fact b)
        {
            return a.IsVersionOf(b.Id)
                || b.IsVersionOf(a.Id)
                || (!a.DistortionOf.IsNone && a.DistortionOf == b.DistortionOf);
        }

        /// <summary>
        /// The world's truth of what was asserted, with a denial inverted: somebody who denies a
        /// claim the world holds false has said something accurate, and reporting the claim's own
        /// truth there would call an accurate denial a false statement.
        /// </summary>
        private static TruthState Accuracy(Fact claim, SpeechActStance stance)
        {
            if (claim == null)
            {
                return TruthState.Uncertain;
            }

            if (stance != SpeechActStance.Denies)
            {
                return claim.Truth;
            }

            switch (claim.Truth)
            {
                case TruthState.True:
                    return TruthState.False;
                case TruthState.False:
                    return TruthState.True;
                default:
                    return claim.Truth;
            }
        }

        private static bool Tagged(WorldEvent recorded, string tag)
        {
            for (int i = 0; i < recorded.Tags.Count; i++)
            {
                if (recorded.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
