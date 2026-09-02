using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// Whether somebody who knows a thing is willing to say it to the person in front of them
    /// (BQ-071, CD §17.5, PM §38).
    ///
    /// The step's whole claim is that knowing and telling are separate. A dialogue layer without
    /// this has one question - does the character know? - and so every secret in the world is one
    /// conversation from being spent; the moment a character can decline, information becomes
    /// something the player earns from a person rather than from a lookup.
    ///
    /// Four rules hold it in place.
    ///
    /// <b>Nothing unbelieved is ever disclosed.</b> The first thing this class does is ask the
    /// knowledge graph for a belief, and no belief means <see cref="DisclosureStrategy.NothingToDisclose"/>
    /// before a single pressure is weighed. Identity says what somebody would plausibly know
    /// (<c>IdentityAffordances.PlausibleKnowledgeOf</c>) and that is a casting and interpretation
    /// input, never a belief: a miller who ought to know who runs the mill does not thereby know
    /// it, and a disclosure layer that filled the gap would be inventing facts at exactly the
    /// moment the player was told they were learning one.
    ///
    /// <b>It is a character decision, not a difficulty check.</b> No dice, no
    /// <c>ICheckResolver</c>, no <c>ActionContext</c>. The same speaker asked the same thing by
    /// the same person in the same state answers the same way every time, and what changes the
    /// answer is the world changing - a tie mended, a fear decayed, a leverage spent. Persuasion
    /// remains the action layer's (D016): a check may change the state this reads, and never this
    /// reading of it.
    ///
    /// <b>Every pressure is read from state that already exists.</b> Beliefs, relationships,
    /// obligations, personality, values, sensitivities, emotions and the fact's own secrecy. There
    /// is no disclosure profile on a character, no per-topic willingness table and no accumulated
    /// score: <see cref="DisclosureDecision.Balance"/> is arithmetic performed on the spot and
    /// thrown away, so nothing here can drift out of agreement with the state it describes.
    ///
    /// <b>Withholding is never lying.</b> The strategies are four ways of being more or less
    /// forthcoming and none of them asserts anything false. Which act carries an untruth, and how
    /// the world records one so it can be caught later, is BQ-073's, and it consumes this decision
    /// rather than replacing it.
    ///
    /// BQ-072 adds the second axis without touching the first: the same weighing decides whether
    /// the claim comes out, and <see cref="DisclosureDepth"/> then decides how much of what the
    /// speaker holds comes with it. Depth is the lowest of three ceilings - what they actually
    /// know, how far the relationship reaches, and what the restraining pressures leave them free
    /// to say - so a deep tie buys more of a fact and never invents one, and never overrides a
    /// fear, a loyalty or a privacy that was holding the rest back.
    /// </summary>
    public static class Disclosure
    {
        /// <summary>
        /// Above this, the claim is put forward and stood behind; the bands below it are the rest
        /// of the ladder. Thresholds rather than a curve because the output is four discrete
        /// moves and a threshold is the thing an inspector can name.
        /// </summary>
        public const double DiscloseAt = 0.20;

        public const double HedgeAt = -0.05;

        public const double DeflectAt = -0.40;

        /// <summary>
        /// Conviction below which nobody stands behind a claim however willing they are. The same
        /// figure <c>KnowledgeGraph.BelievesConfidently</c> uses for acting on a belief, for the
        /// same reason: saying a thing you would not act on is exactly what hedging is.
        /// </summary>
        public const double ConvictionToStandBehind = 0.5;

        /// <summary>
        /// The standing at which a tie starts buying particulars rather than only an answer, and
        /// the standing at which it buys the part that does not go into an official account.
        ///
        /// Two thresholds on one reading, for the same reason the strategy bands are thresholds:
        /// the output is a small number of discrete rungs, and a threshold is the thing an
        /// inspector can name and a test can cross. What crosses them is the relationship
        /// changing - a warmer tie, a kept promise, a shelter given.
        /// </summary>
        public const double DetailAt = 0.45;

        public const double InConfidenceAt = 1.00;

        /// <summary>
        /// How much restraint - from everything that is not the relationship - keeps the rest
        /// back. Beyond <see cref="GuardedAt"/> a speaker gives particulars but not provenance;
        /// beyond <see cref="HeldBackAt"/> they give the claim and nothing around it.
        ///
        /// Read from the same pressures the strategy was banded from rather than from a second
        /// weighing, and applied as a cap rather than as a subtraction, which is what stops a warm
        /// enough tie from buying its way past a fear or a loyalty. Affection does not make
        /// somebody unafraid.
        /// </summary>
        public const double GuardedAt = 0.45;

        public const double HeldBackAt = 0.80;

        /// <summary>
        /// Below this, keeping quiet has itself become too expensive and the speaker puts
        /// something untrue in its place (BQ-073).
        ///
        /// Deliberately past <see cref="DeflectAt"/> rather than a band of its own beside it, and
        /// that is the whole argument for the number: while an open refusal or a changed subject
        /// still costs less than the claim, somebody takes one of those, and they are not lies.
        /// A falsehood is what is left when silence would itself be an answer - when the pressure
        /// is such that "I would rather not say" gives away exactly what the speaker is trying to
        /// keep. Anything shallower stays a refusal, which is why no amount of ordinary reluctance
        /// ever promotes itself into a lie.
        /// </summary>
        public const double FalsifyAt = -0.85;

        /// <summary>
        /// Honesty above which nobody lies however hard they are pressed - they refuse, and
        /// wear the consequences.
        ///
        /// A second condition rather than another term in the balance, for the same reason depth
        /// is capped rather than summed: a large enough pressure must not be able to buy its way
        /// past a person's character. An honest witness under unbearable pressure is a witness who
        /// will not say, and that is a different character from one who would.
        /// </summary>
        public const double CandourThatWillNotLie = 0.35;

        /// <summary>
        /// What this speaker would do if this person asked them about this claim now.
        ///
        /// Never null. A speaker with no belief gets a decision that says so, because "they did
        /// not answer" and "they had nothing to answer with" are different things and a caller
        /// handed null cannot tell them apart.
        /// </summary>
        public static DisclosureDecision Decide(
            NarrativeWorldState world,
            EntityId speaker,
            EntityId asker,
            EntityId factId,
            GameTime now)
        {
            if (world == null || speaker.IsNone || asker.IsNone || factId.IsNone)
            {
                return Nothing(speaker, asker, factId, "no speaker, listener or claim");
            }

            if (speaker == asker)
            {
                return Nothing(speaker, asker, factId, "nobody discloses anything to themself");
            }

            // The gate that makes the rest of the file safe to write. Everything below reads
            // belief; nothing below may invent one.
            if (!world.Knowledge.TryGetBelief(speaker, factId, out KnowledgeRecord belief))
            {
                return Nothing(speaker, asker, factId, "holds no belief about this claim");
            }

            Fact fact = world.Knowledge.GetFact(factId);
            if (fact == null)
            {
                return Nothing(speaker, asker, factId, "no such claim in the world");
            }

            NarrativeNpc npc = world.Registry.GetNpc(speaker);
            if (npc == null)
            {
                return Nothing(speaker, asker, factId, "the speaker is not a character this simulation models");
            }

            List<DisclosurePressure> pressures = new List<DisclosurePressure>();
            Weigh(pressures, Confidence(belief));
            Weigh(pressures, Candour(npc));
            Weigh(pressures, Relationship(world, npc, speaker, asker));
            Weigh(pressures, Privacy(fact, speaker));
            Weigh(pressures, Fear(world, npc, asker, now));
            Weigh(pressures, Loyalty(world, npc, speaker, asker, fact));
            Weigh(pressures, Leverage(world, speaker, fact));
            Weigh(pressures, LegalRisk(world, npc, speaker, asker, fact, belief));
            Weigh(pressures, SocialRisk(npc, speaker, fact, now));
            Weigh(pressures, Grievance(world, npc, speaker, fact, now));

            pressures.Sort(ByMagnitude);

            double balance = Sum(pressures);
            DisclosureStrategy strategy = Band(balance, belief.Confidence);

            // The second axis. Nothing above is re-read or re-weighed: how far they go is decided
            // from the belief they hold, the tie they have and the pressures already weighed.
            double standing = Standing(world, speaker, asker);
            double restraint = Restraint(pressures);
            DisclosureDepth known = KnownDepth(fact, belief);
            DisclosureDepth reached = StandingDepth(standing);
            DisclosureDepth depth = Depth(strategy, known, reached, restraint);

            // The third axis (BQ-073). Read from the same weighing again rather than from a
            // second one: what somebody does instead of answering is decided by how badly they
            // want the claim kept and by what kind of person they are, and both were already
            // established above.
            DisclosureTactic tactic = Tactic(world, npc, speaker, fact, belief, strategy, balance);

            return new DisclosureDecision(
                speaker,
                asker,
                factId,
                strategy,
                balance,
                pressures,
                Decisive(pressures, balance, belief.Confidence, strategy),
                string.Empty,
                depth,
                known,
                reached,
                standing,
                Bound(strategy, depth, known, reached, restraint),
                tactic,
                fact.Subject);
        }

        /// <summary>
        /// The same decision, taken from the question that prompted it. The speaker is whoever was
        /// asked, the claim is what the question was about, and the asker is whoever asked - so a
        /// caller holding a conversation cannot pair the wrong three by hand.
        ///
        /// Null for anything that is not a question addressed to this person about a claim.
        /// </summary>
        public static DisclosureDecision Decide(NarrativeWorldState world, SpeechAct ask, EntityId speaker, GameTime now)
        {
            if (ask == null || ask.Type != SpeechActType.Ask || !ask.IsAddressedTo(speaker) || ask.About.IsNone)
            {
                return null;
            }

            return Decide(world, speaker, ask.Speaker, ask.About, now);
        }

        /// <summary>
        /// The act a decision amounts to, or null when the vocabulary has no act for it.
        ///
        /// Three of the four map onto BQ-070's vocabulary: saying the claim is an
        /// <see cref="SpeechActType.Answer"/>, declining is a <see cref="SpeechActType.Refuse"/>,
        /// and a deflection is an <see cref="SpeechActType.Evade"/> - the act BQ-073 added for it,
        /// rather than a <c>Refuse</c>, which would quietly delete the difference between letting
        /// a question go and turning it down. Nothing to disclose is no act at all: silence is not
        /// something somebody said.
        ///
        /// A speaker who has decided to falsify (BQ-073) composes a <see cref="SpeechActType.Deny"/>
        /// whichever rung they are on, because what they are doing is asserting rather than
        /// withholding. That case is read before the ladder for exactly that reason.
        ///
        /// A hedge and a disclosure are the same act, because they are. What separates them is how
        /// far the speaker will be held to it, which lives on the decision where a later realizer
        /// and BQ-073 can both read it - and not on the act, which carries no such reading (D030).
        ///
        /// The question being answered is not optional. Both acts BQ-070 offers here need an
        /// antecedent - an answer nobody asked for is not an answer and a refusal of nothing is
        /// not a refusal - so composing without one is refused by <see cref="SpeechAct.Compose"/>
        /// rather than repaired here.
        /// </summary>
        public static SpeechAct Compose(DisclosureDecision decision, SpeechAct inReplyTo)
        {
            if (decision == null)
            {
                return null;
            }

            // The tactic is read before the ladder, because it is the tactic that decides what
            // was said: a speaker who has decided to falsify is not performing a quieter refusal,
            // they are asserting something, and reading the rung first would compose the act they
            // did not make.
            if (decision.Tactic == DisclosureTactic.Falsify)
            {
                // A denial rather than a substitute claim. Putting a rival claim forward - "it
                // was somebody else" - needs a claim to exist for them to name, and minting one
                // is a write; deciding writes nothing (BQ-071), so that route belongs to whoever
                // holds the pen. `RumorDistortion.Blame` already makes such a claim and
                // `RumorSystem.Lie` already tells it, and `Deception` scores both the same way.
                return SpeechAct.Compose(
                    SpeechActType.Deny,
                    decision.Speaker,
                    decision.Asker,
                    new ActionBinding { PropositionFact = decision.FactId },
                    decision.ClaimSubject,
                    inReplyTo);
            }

            switch (decision.Strategy)
            {
                case DisclosureStrategy.Disclose:
                case DisclosureStrategy.Hedge:
                    return SpeechAct.Compose(
                        SpeechActType.Answer,
                        decision.Speaker,
                        decision.Asker,
                        new ActionBinding { PropositionFact = decision.FactId },
                        EntityId.None,
                        inReplyTo);

                case DisclosureStrategy.Refuse:
                    return SpeechAct.Compose(
                        SpeechActType.Refuse,
                        decision.Speaker,
                        decision.Asker,
                        ActionBinding.Empty,
                        EntityId.None,
                        inReplyTo);

                case DisclosureStrategy.Deflect:
                    // The gap BQ-071 left open. The act carries no proposition at all - not the
                    // claim and not a substitute - so nothing downstream can read an evasion as
                    // having asserted anything, which is exactly what separates it from the
                    // falsehood above. Which flavour of evasion it was stays on the decision:
                    // the act says the question was slid past, and `Tactic` says whether
                    // something adjacent was offered or the subject was simply changed.
                    return SpeechAct.Compose(
                        SpeechActType.Evade,
                        decision.Speaker,
                        decision.Asker,
                        ActionBinding.Empty,
                        EntityId.None,
                        inReplyTo);

                default:
                    return null;
            }
        }

        // -- what is done instead (BQ-073) ---------------------------------------------------------

        /// <summary>
        /// What the speaker does about the question, given that they have decided how forthcoming
        /// to be.
        ///
        /// Three rules, in the order they are applied.
        ///
        /// <b>Nobody who is answering is doing anything instead.</b> Both forthcoming rungs and
        /// the no-belief case come back as <see cref="DisclosureTactic.None"/>, so the tactic
        /// axis can never disagree with the ladder about whether the claim came out.
        ///
        /// <b>A lie needs a belief to contradict.</b> Somebody who does not hold the claim firmly
        /// cannot knowingly deny it; whatever they say is a mistake or a guess, and the world
        /// would be recording a deception that nobody committed. This is the same conviction
        /// figure that decides whether a claim is stood behind, for the same reason.
        ///
        /// <b>And it needs someone who would.</b> Severe pressure alone is not enough. An honest
        /// person under it refuses, which costs them; keeping character as a hard condition rather
        /// than a weight is what stops a big enough number from making anybody a liar.
        /// </summary>
        private static DisclosureTactic Tactic(
            NarrativeWorldState world,
            NarrativeNpc npc,
            EntityId speaker,
            Fact fact,
            KnowledgeRecord belief,
            DisclosureStrategy strategy,
            double balance)
        {
            if (strategy != DisclosureStrategy.Refuse && strategy != DisclosureStrategy.Deflect)
            {
                return DisclosureTactic.None;
            }

            if (balance <= FalsifyAt
                && belief.Confidence >= ConvictionToStandBehind
                && npc.Personality.Honesty < CandourThatWillNotLie)
            {
                return DisclosureTactic.Falsify;
            }

            if (strategy == DisclosureStrategy.Refuse)
            {
                return DisclosureTactic.Decline;
            }

            return HasSomethingElseToSay(world, speaker, fact)
                ? DisclosureTactic.AnswerElsewhere
                : DisclosureTactic.ChangeSubject;
        }

        /// <summary>
        /// Whether the speaker holds something true about the same matter that they could offer
        /// in place of what was asked.
        ///
        /// Answering a different question is only available to somebody who has a different
        /// answer; without this the distinction would be decoration, and the two evasions would
        /// differ by nothing a player could ever have influenced. What counts is a second belief
        /// about the same person that is not itself kept - a substitute that is being volunteered
        /// has to be one the speaker is happy to volunteer.
        ///
        /// Rival versions of the claim itself are excluded. "It was somebody else" is not a
        /// neighbouring question truthfully answered, it is the falsehood above, and letting it in
        /// here would let a speaker lie under the name of evading.
        /// </summary>
        private static bool HasSomethingElseToSay(NarrativeWorldState world, EntityId speaker, Fact fact)
        {
            foreach (KnowledgeRecord held in world.Knowledge.BeliefsOf(speaker))
            {
                if (held.FactId == fact.Id)
                {
                    continue;
                }

                Fact other = world.Knowledge.GetFact(held.FactId);
                if (other == null
                    || other.Secrecy != 0
                    || other.Subject != fact.Subject
                    || other.IsVersionOf(fact.Id)
                    || fact.IsVersionOf(other.Id))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // -- the pressures -------------------------------------------------------------------------
        //
        // Each returns a signed weight and the state it read, or a zero-weight pressure that is
        // dropped. A term contributes nothing rather than a small constant when its state is
        // absent, so an unmodelled character is neutral instead of quietly secretive.

        private static DisclosurePressure Confidence(KnowledgeRecord belief)
        {
            double weight = (belief.Confidence - 0.5) * 0.6;
            return new DisclosurePressure(
                DisclosurePressures.Confidence,
                weight,
                "believes it at " + belief.Confidence.ToString("0.00") + " from " + belief.Source);
        }

        private static DisclosurePressure Candour(NarrativeNpc npc)
        {
            return new DisclosurePressure(
                DisclosurePressures.Candour,
                (npc.Personality.Honesty - 0.5) * 0.4,
                "honesty " + npc.Personality.Honesty.ToString("0.00"));
        }

        /// <summary>
        /// What the speaker is to whoever is asking. The one pressure PM §38 turns on, and the
        /// reason the same character is four different informants to four different people.
        ///
        /// Trust scales what a warm tie buys rather than standing as its own term: how open
        /// somebody is with a friend is a fact about them, and a suspicious person with a friend
        /// is not the same as a trusting person with an acquaintance.
        /// </summary>
        private static DisclosurePressure Relationship(NarrativeWorldState world, NarrativeNpc npc, EntityId speaker, EntityId asker)
        {
            RelationshipEdge tie = world.Relationships.Find(speaker, asker);
            if (tie == null)
            {
                // A stranger is neutral, not an enemy. Somebody nobody has any tie to gets their
                // answer from everything else about them, which is the honest reading of "we have
                // no relationship" and keeps an unpopulated graph from silencing a whole town.
                return default;
            }

            double sentiment = tie.Sentiment / 100.0;
            double openness = 0.30 + (0.30 * npc.Personality.Trust);
            double weight = (sentiment * openness) + KindBonus(tie.Kind);
            return new DisclosurePressure(
                DisclosurePressures.Relationship,
                weight,
                tie.Kind + " at sentiment " + tie.Sentiment + ", trust " + npc.Personality.Trust.ToString("0.00"));
        }

        /// <summary>
        /// What the tie is, beyond how warm it is. A creditor and a friend at the same sentiment
        /// are not owed the same candour, and an enemy who happens not to be hated yet is still
        /// somebody you do not tell things to.
        /// </summary>
        private static double KindBonus(RelationKind kind)
        {
            switch (kind)
            {
                case RelationKind.Spouse:
                    return 0.22;
                case RelationKind.Family:
                    return 0.16;
                case RelationKind.Friend:
                    return 0.12;
                case RelationKind.Accomplice:
                    return 0.10;
                case RelationKind.GuildMate:
                    return 0.08;
                case RelationKind.Rival:
                    return -0.12;
                case RelationKind.Enemy:
                    return -0.20;
                default:
                    return 0.0;
            }
        }

        private static DisclosurePressure Privacy(Fact fact, EntityId speaker)
        {
            double kept = fact.Secrecy / 100.0 * 0.55;
            double own = fact.Subject == speaker ? 0.15 : 0.0;
            double weight = -(kept + own);
            if (weight == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.Privacy,
                weight,
                own > 0.0
                    ? "secrecy " + fact.Secrecy + " and the claim is their own business"
                    : "secrecy " + fact.Secrecy);
        }

        /// <summary>
        /// Present affect, read through <c>EmotionalStateProfile</c> at the current time so that it
        /// decays. BQ-063's condition is that the same NPC answers differently when frightened and
        /// returns to baseline afterwards, and this is where that lands in dialogue.
        /// </summary>
        private static DisclosurePressure Fear(NarrativeWorldState world, NarrativeNpc npc, EntityId asker, GameTime now)
        {
            double fear = npc.Emotions.Get(EmotionalState.Fear, now);
            double stress = npc.Emotions.Get(EmotionalState.Stress, now);
            double suspicion = npc.Emotions.Get(EmotionalState.Suspicion, now);
            double weight = -((fear * 0.55) + (stress * 0.20) + (suspicion * 0.20));
            if (weight == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.Fear,
                weight,
                "fear " + fear.ToString("0.00") + ", stress " + stress.ToString("0.00") + ", suspicion " + suspicion.ToString("0.00"));
        }

        /// <summary>
        /// A tie to whoever the claim is about, which saying it would spend.
        ///
        /// Only counts when the claim is about somebody who is neither speaker nor listener -
        /// there is nothing to protect in telling a person about themself, and a claim about the
        /// speaker is <see cref="SocialRisk"/>'s. Family weighs more for whoever holds family
        /// dear, which is <c>ValueProfile</c> doing the work it exists for rather than a second
        /// table of who protects whom.
        /// </summary>
        private static DisclosurePressure Loyalty(NarrativeWorldState world, NarrativeNpc npc, EntityId speaker, EntityId asker, Fact fact)
        {
            EntityId subject = fact.Subject;
            if (subject.IsNone || subject == speaker || subject == asker)
            {
                return default;
            }

            RelationshipEdge tie = world.Relationships.Find(speaker, subject);
            if (tie == null)
            {
                return default;
            }

            double warmth = tie.Sentiment / 100.0;
            double bond = KindBonus(tie.Kind);
            if (warmth <= 0.0 && bond <= 0.0)
            {
                // No loyalty to spend. Whatever a cold tie to the subject does is grievance's.
                return default;
            }

            double care = warmth > 0.0 ? warmth : 0.0;
            double kin = tie.Kind == RelationKind.Family || tie.Kind == RelationKind.Spouse
                ? npc.Values.Family.Importance
                : 0.5;
            double weight = -((care * 0.35) + (bond > 0.0 ? bond : 0.0)) * (0.5 + npc.Personality.Loyalty) * (0.6 + (kin * 0.8));
            if (weight == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.Loyalty,
                weight,
                tie.Kind + " to " + world.Registry.NameOf(subject) + ", loyalty " + npc.Personality.Loyalty.ToString("0.00"));
        }

        /// <summary>
        /// Something the speaker holds over the claim's subject that only works while it is quiet.
        ///
        /// Read from the obligation ledger rather than from a "is blackmailing" flag: a debt the
        /// subject owes the speaker, a favour outstanding, a grudge being held. CD §15 lists
        /// profit and blackmail among the motives a secret needs, and this is the one the world
        /// already records.
        /// </summary>
        private static DisclosurePressure Leverage(NarrativeWorldState world, EntityId speaker, Fact fact)
        {
            EntityId subject = fact.Subject;
            if (subject.IsNone || subject == speaker)
            {
                return default;
            }

            SocialObligation strongest = null;
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                if (!obligation.IsOpen || obligation.Creditor != speaker || obligation.Debtor != subject)
                {
                    continue;
                }

                if (strongest == null || obligation.Strength > strongest.Strength)
                {
                    strongest = obligation;
                }
            }

            if (strongest == null)
            {
                return default;
            }

            double weight = -(0.12 + (strongest.Strength / 100.0 * 0.28));
            return new DisclosurePressure(
                DisclosurePressures.Leverage,
                weight,
                "holds an open " + strongest.Kind + " over " + world.Registry.NameOf(subject) + " at strength " + strongest.Strength);
        }

        /// <summary>
        /// How the law bears on this claim for this speaker, in one signed term.
        ///
        /// Against, and strongly, when the claim is the speaker's own crime or one they took part
        /// in - the single largest pressure in the model, because it should be. Toward when the
        /// crime is somebody else's and the speaker holds law dear, which is
        /// <c>ValueConcern.Law</c> being the reason a witness comes forward at all.
        ///
        /// One tag rather than two because it is one question asked of one claim; the direction is
        /// on the weight and the reason says which way and why.
        /// </summary>
        private static DisclosurePressure LegalRisk(
            NarrativeWorldState world,
            NarrativeNpc npc,
            EntityId speaker,
            EntityId asker,
            Fact fact,
            KnowledgeRecord belief)
        {
            if (!IsCriminal(fact.Predicate))
            {
                return default;
            }

            if (fact.Subject == speaker)
            {
                return new DisclosurePressure(
                    DisclosurePressures.LegalRisk,
                    -0.60,
                    "the claim is the speaker's own " + fact.Predicate);
            }

            if (belief.Source == KnowledgeSource.Participant)
            {
                return new DisclosurePressure(
                    DisclosurePressures.LegalRisk,
                    -0.35,
                    "was party to the " + fact.Predicate + " they would be describing");
            }

            RelationshipEdge accomplice = world.Relationships.Find(speaker, fact.Subject);
            if (accomplice != null && accomplice.Kind == RelationKind.Accomplice)
            {
                return new DisclosurePressure(
                    DisclosurePressures.LegalRisk,
                    -0.30,
                    "accomplice to " + world.Registry.NameOf(fact.Subject));
            }

            double law = (npc.Values.Law.Importance - 0.5) * 0.5;
            if (law == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.LegalRisk,
                law,
                "somebody else's " + fact.Predicate + ", and law matters " + npc.Values.Law.Importance.ToString("0.00") + " to them");
        }

        /// <summary>
        /// Whether the crime the mod already recognises is one somebody could be answerable for.
        /// The list is <c>FactPredicates</c>' own criminal set; a predicate nobody has thought
        /// about carries no legal weight, which fails toward speaking rather than toward a
        /// silence nothing justifies.
        /// </summary>
        private static bool IsCriminal(string predicate)
        {
            switch (predicate)
            {
                case FactPredicates.Stole:
                case FactPredicates.Killed:
                case FactPredicates.Forged:
                case FactPredicates.Extorted:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// What saying it would cost the speaker socially, when the claim is about them.
        ///
        /// <c>SensitivityProfile</c> and <c>EmotionalStateProfile</c> already hold both halves -
        /// the durable dread of being seen badly and the present shame - and <c>ValueConcern.Status</c>
        /// says how much standing is worth to this person. Nothing new is stored to make somebody
        /// private about themself.
        /// </summary>
        private static DisclosurePressure SocialRisk(NarrativeNpc npc, EntityId speaker, Fact fact, GameTime now)
        {
            if (fact.Subject != speaker)
            {
                return default;
            }

            double shame = npc.Emotions.Get(EmotionalState.Shame, now);
            double exposure = npc.Sensitivities.PublicEmbarrassment;
            double standing = npc.Values.Status.Importance;
            double weight = -((shame * 0.30) + (exposure * 0.25) + ((standing - 0.5) * 0.20));
            if (weight == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.SocialRisk,
                weight,
                "shame " + shame.ToString("0.00") + ", embarrassment " + exposure.ToString("0.00") + ", status " + standing.ToString("0.00"));
        }

        /// <summary>
        /// Anger at whoever the claim is about, which is the pressure that makes people talk.
        ///
        /// Its point in the model is that it moves the answer without touching the belief: the
        /// same witness holding the same knowledge tells a stranger about the man he is furious
        /// with and keeps it about the man he is fond of, and both readings come from state the
        /// world already had a reason to hold.
        /// </summary>
        private static DisclosurePressure Grievance(NarrativeWorldState world, NarrativeNpc npc, EntityId speaker, Fact fact, GameTime now)
        {
            EntityId subject = fact.Subject;
            if (subject.IsNone || subject == speaker)
            {
                return default;
            }

            double anger = npc.Emotions.Get(EmotionalState.Anger, now);
            RelationshipEdge tie = world.Relationships.Find(speaker, subject);
            double enmity = tie != null && tie.Sentiment < 0 ? -tie.Sentiment / 100.0 : 0.0;
            double weight = ((anger * 0.40) + (enmity * 0.30)) * (0.5 + npc.Personality.Vengefulness);
            if (weight == 0.0)
            {
                return default;
            }

            return new DisclosurePressure(
                DisclosurePressures.Grievance,
                weight,
                "anger " + anger.ToString("0.00") + " toward " + world.Registry.NameOf(subject) + ", vengefulness " + npc.Personality.Vengefulness.ToString("0.00"));
        }

        // -- depth (BQ-072) ------------------------------------------------------------------------
        //
        // Three ceilings and the lowest of them wins. They are ceilings rather than terms in a sum
        // on purpose: a sum lets a deep enough tie buy its way past a fear or past the edge of what
        // somebody actually knows, and both of those are things no amount of affection does.

        /// <summary>
        /// How much of what they hold comes out, given that something does.
        ///
        /// <see cref="DisclosureDepth.Nothing"/> whenever the claim is not being put forward, so
        /// the two axes never disagree about whether anything was said. Otherwise the least of
        /// what they know, what the tie reaches and what their own restraint leaves them free to
        /// give. Never below <see cref="DisclosureDepth.Gist"/> without needing a floor: somebody
        /// answering has said the claim, and no ceiling can fall beneath it.
        /// </summary>
        private static DisclosureDepth Depth(
            DisclosureStrategy strategy,
            DisclosureDepth known,
            DisclosureDepth standing,
            double restraint)
        {
            if (strategy < DisclosureStrategy.Hedge)
            {
                return DisclosureDepth.Nothing;
            }

            DisclosureDepth depth = known < standing ? known : standing;
            DisclosureDepth allowed = RestrainedDepth(restraint);
            return allowed < depth ? allowed : depth;
        }

        /// <summary>
        /// The deepest rung this belief could support - the hard cap, and the only one that is
        /// never traded against anything.
        ///
        /// Particulars require the claim to have any: a fact with no object, no value and no
        /// evidence is the whole of what its holder knows, and there is no second rung of it to
        /// unlock however close the listener is. Provenance requires them to be able to give one -
        /// their own part in it, the person who told them, or something they can produce - and to
        /// hold it firmly enough to stand behind, since <see cref="DisclosureDepth.InConfidence"/>
        /// is what somebody kept back rather than what they are still unsure of.
        ///
        /// A hearsay belief from nobody in particular, with nothing to show for it, therefore
        /// stops at <see cref="DisclosureDepth.Detail"/> no matter who is asking. That is the
        /// invariant: depth is a reading of knowledge, and a tie can only ever fail to reach it.
        /// </summary>
        private static DisclosureDepth KnownDepth(Fact fact, KnowledgeRecord belief)
        {
            bool particulars = !fact.Object.IsNone
                || (fact.Value != null && fact.Value.Length != 0)
                || fact.EvidenceIds.Count > 0;
            if (!particulars)
            {
                return DisclosureDepth.Gist;
            }

            bool provenance = FirstHand(belief.Source) || !belief.ToldBy.IsNone || belief.CanProve;
            return provenance && belief.Confidence >= ConvictionToStandBehind
                ? DisclosureDepth.InConfidence
                : DisclosureDepth.Detail;
        }

        /// <summary>
        /// Whether the way they came to hold this is something they could actually recount. Being
        /// there, reading it, being told it by the subject: each is an account of its own that a
        /// speaker can give or keep back. An inference is not - the reasoning is not a thing they
        /// witnessed - so it reaches provenance only through a named teller or a proof.
        /// </summary>
        private static bool FirstHand(KnowledgeSource source)
        {
            switch (source)
            {
                case KnowledgeSource.Witnessed:
                case KnowledgeSource.Participant:
                case KnowledgeSource.Document:
                case KnowledgeSource.Admission:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// What the relationship itself reaches, as one reading of everything the world holds
        /// about these two.
        ///
        /// Deliberately not affinity. Sentiment is one term of four: what the tie <em>is</em>
        /// carries its own weight (<see cref="KindBonus"/>, the same table willingness reads, so
        /// there is no second opinion about what a spouse is), the obligation ledger contributes
        /// what the two of them have actually done for and to each other, and a tie the listener
        /// holds back makes the relationship mutual rather than one-sided. A history of kept
        /// promises and given shelter is a deeper relationship than a warm number, and it is the
        /// difference PM §38 is really describing.
        ///
        /// Read, never stored: like <see cref="DisclosureDecision.Balance"/> this is arithmetic
        /// over the graph and the ledger, performed on the spot and discarded, so no standing can
        /// drift out of agreement with the ties and debts it describes.
        /// </summary>
        private static double Standing(NarrativeWorldState world, EntityId speaker, EntityId asker)
        {
            double standing = 0.0;
            RelationshipEdge tie = world.Relationships.Find(speaker, asker);
            if (tie != null)
            {
                standing += tie.Sentiment / 100.0;
                standing += KindBonus(tie.Kind) * 1.5;
            }

            RelationshipEdge back = world.Relationships.Find(asker, speaker);
            if (back != null && back.Sentiment > 0)
            {
                // A tie somebody holds back is not the same relationship as one they do not.
                standing += back.Sentiment / 100.0 * 0.20;
            }

            return standing + History(world, speaker, asker);
        }

        /// <summary>
        /// What the two of them have actually done for and to each other, from the obligation
        /// ledger - the part of a relationship that is a record rather than a feeling.
        ///
        /// A settled debt or a kept promise counts whichever way it ran: being helped and having
        /// helped both deepen a tie, and the ledger already says which happened and how much was
        /// at stake. Shelter and sponsorship still standing count for more, because they are the
        /// obligations somebody took a risk to enter. A broken obligation or an open grudge counts
        /// against, for the same reason and in the same units.
        ///
        /// Bounded, because history informs a relationship rather than replacing it: a ledger full
        /// of small favours should not out-weigh what the two people are to each other.
        /// </summary>
        private static double History(NarrativeWorldState world, EntityId speaker, EntityId asker)
        {
            double history = 0.0;
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                bool between = (obligation.Debtor == speaker && obligation.Creditor == asker)
                    || (obligation.Debtor == asker && obligation.Creditor == speaker);
                if (!between)
                {
                    continue;
                }

                double weight = 0.10 + (obligation.Strength / 100.0 * 0.20);
                switch (obligation.Status)
                {
                    case SocialObligationStatus.Fulfilled:
                    case SocialObligationStatus.Forgiven:
                        history += obligation.Kind == SocialObligationKind.Grudge ? 0.0 : weight;
                        break;
                    case SocialObligationStatus.Broken:
                        history -= weight;
                        break;
                    case SocialObligationStatus.Open:
                        if (obligation.Kind == SocialObligationKind.Grudge)
                        {
                            history -= weight;
                        }
                        else if (obligation.Kind == SocialObligationKind.Sanctuary
                            || obligation.Kind == SocialObligationKind.Sponsorship)
                        {
                            history += weight;
                        }

                        break;
                }
            }

            return history > 0.50 ? 0.50 : (history < -0.50 ? -0.50 : history);
        }

        /// <summary>Where a standing lands. The staging PM §38's "in stages" asks for.</summary>
        private static DisclosureDepth StandingDepth(double standing)
        {
            if (standing >= InConfidenceAt)
            {
                return DisclosureDepth.InConfidence;
            }

            return standing >= DetailAt ? DisclosureDepth.Detail : DisclosureDepth.Gist;
        }

        /// <summary>
        /// How hard everything that is not the relationship is pulling the other way: the
        /// magnitudes of the pressures against saying it, with the tie to the listener left out
        /// because that is the ceiling this one exists to be independent of.
        ///
        /// Magnitudes rather than the balance, so that a warm tie cannot net a fear away. Somebody
        /// frightened who answers a friend anyway is exactly the case this models: they speak, and
        /// they do not go on to say how they know it.
        /// </summary>
        private static double Restraint(IReadOnlyList<DisclosurePressure> pressures)
        {
            double restraint = 0.0;
            for (int i = 0; i < pressures.Count; i++)
            {
                DisclosurePressure pressure = pressures[i];
                if (!pressure.TowardDisclosure && pressure.Tag != DisclosurePressures.Relationship)
                {
                    restraint += pressure.Magnitude;
                }
            }

            return restraint;
        }

        private static DisclosureDepth RestrainedDepth(double restraint)
        {
            if (restraint >= HeldBackAt)
            {
                return DisclosureDepth.Gist;
            }

            return restraint >= GuardedAt ? DisclosureDepth.Detail : DisclosureDepth.InConfidence;
        }

        /// <summary>
        /// Which ceiling held the answer where it is, for the inspector and for anybody asking why
        /// a friend was not more forthcoming.
        ///
        /// Knowledge first when several bind at once, because "that is all they know" is the
        /// answer that stops somebody looking for a knob to turn.
        /// </summary>
        private static DisclosureLimit Bound(
            DisclosureStrategy strategy,
            DisclosureDepth depth,
            DisclosureDepth known,
            DisclosureDepth standing,
            double restraint)
        {
            if (strategy < DisclosureStrategy.Hedge)
            {
                return DisclosureLimit.Unspoken;
            }

            if (depth == DisclosureDepth.InConfidence)
            {
                // The top of the ladder: nothing was held back, so naming a constraint would be
                // naming one that did not bind.
                return DisclosureLimit.None;
            }

            if (depth == known)
            {
                return DisclosureLimit.Knowledge;
            }

            if (depth == RestrainedDepth(restraint))
            {
                return DisclosureLimit.Restraint;
            }

            return depth == standing ? DisclosureLimit.Standing : DisclosureLimit.None;
        }

        // -- banding and explanation ---------------------------------------------------------------

        /// <summary>
        /// Where a balance lands, with the one rule that is not a threshold: nobody stands behind
        /// a claim they do not hold firmly, however willing they are to mention it. That is the
        /// whole semantic content of hedging, and putting it here rather than in the arithmetic
        /// keeps it from being tradeable against a warm relationship.
        /// </summary>
        private static DisclosureStrategy Band(double balance, double confidence)
        {
            if (balance >= DiscloseAt)
            {
                return confidence >= ConvictionToStandBehind ? DisclosureStrategy.Disclose : DisclosureStrategy.Hedge;
            }

            if (balance >= HedgeAt)
            {
                return DisclosureStrategy.Hedge;
            }

            return balance >= DeflectAt ? DisclosureStrategy.Deflect : DisclosureStrategy.Refuse;
        }

        /// <summary>
        /// Which pressures settled it, by the only definition that does not require a theory:
        /// those that would have changed the answer had they not applied.
        ///
        /// A decision with no single decisive pressure returns an empty list rather than the
        /// largest one dressed up as a cause. That is worth seeing - it says the answer came from
        /// the balance rather than from one thing, and an inspector that always names a culprit
        /// would be inviting whoever reads it to tune the wrong knob.
        /// </summary>
        private static IReadOnlyList<DisclosurePressure> Decisive(
            IReadOnlyList<DisclosurePressure> pressures,
            double balance,
            double confidence,
            DisclosureStrategy strategy)
        {
            List<DisclosurePressure> decisive = new List<DisclosurePressure>();
            for (int i = 0; i < pressures.Count; i++)
            {
                if (Band(balance - pressures[i].Weight, confidence) != strategy)
                {
                    decisive.Add(pressures[i]);
                }
            }

            return decisive;
        }

        private static void Weigh(List<DisclosurePressure> pressures, DisclosurePressure pressure)
        {
            if (pressure.Tag != null && pressure.Weight != 0.0)
            {
                pressures.Add(pressure);
            }
        }

        private static double Sum(IReadOnlyList<DisclosurePressure> pressures)
        {
            double total = 0.0;
            for (int i = 0; i < pressures.Count; i++)
            {
                total += pressures[i].Weight;
            }

            return total;
        }

        /// <summary>Strongest first, then by tag so the dump is stable between runs.</summary>
        private static int ByMagnitude(DisclosurePressure a, DisclosurePressure b)
        {
            int byWeight = b.Magnitude.CompareTo(a.Magnitude);
            return byWeight != 0 ? byWeight : string.CompareOrdinal(a.Tag, b.Tag);
        }

        private static DisclosureDecision Nothing(EntityId speaker, EntityId asker, EntityId factId, string note)
        {
            return new DisclosureDecision(
                speaker,
                asker,
                factId,
                DisclosureStrategy.NothingToDisclose,
                0.0,
                null,
                null,
                note,
                DisclosureDepth.Nothing,
                DisclosureDepth.Nothing,
                DisclosureDepth.Nothing,
                0.0,
                DisclosureLimit.Unspoken,
                DisclosureTactic.None,
                EntityId.None);
        }
    }
}
