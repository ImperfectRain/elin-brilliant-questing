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
    /// the world records one so it can be caught later, is BQ-073's; how much of a single claim
    /// comes out as a tie deepens is BQ-072's. Both consume this decision rather than replacing
    /// it.
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
            return new DisclosureDecision(
                speaker, asker, factId, strategy, balance, pressures, Decisive(pressures, balance, belief.Confidence, strategy), string.Empty);
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
        /// Two of the four map cleanly onto BQ-070's ten: saying the claim is an
        /// <see cref="SpeechActType.Answer"/>, declining is a <see cref="SpeechActType.Refuse"/>.
        /// A deflection maps to nothing and returns null on purpose - the vocabulary has no
        /// <c>Evade</c>, adding one is BQ-073's call, and composing a <c>Refuse</c> instead would
        /// quietly delete the difference between letting a question go and turning it down.
        /// Nothing to disclose is likewise no act: silence is not something somebody said.
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

                default:
                    return null;
            }
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
                speaker, asker, factId, DisclosureStrategy.NothingToDisclose, 0.0, null, null, note);
        }
    }
}
