using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// One term in why an actor chose what they chose. Diagnostic, in the same sense
    /// <c>ChemistryReason</c> and <c>DisclosureDecision.Decisive</c> are: the inspector prints
    /// them and nothing branches on the wording.
    /// </summary>
    public readonly struct IntentReason
    {
        public IntentReason(string term, double weight)
        {
            Term = term ?? string.Empty;
            Weight = weight;
        }

        public string Term { get; }

        public double Weight { get; }

        public override string ToString()
        {
            return Term + " " + (Weight >= 0 ? "+" : string.Empty) + Weight.ToString("0.##");
        }
    }

    /// <summary>What one candidate intention was worth to this actor, and why.</summary>
    public sealed class IntentScore
    {
        internal IntentScore(BeatIntention intention, SpeechAct act, double total, IReadOnlyList<IntentReason> reasons, string refusal)
        {
            Intention = intention;
            Act = act;
            Total = total;
            Reasons = reasons ?? new IntentReason[0];
            Refusal = refusal ?? string.Empty;
        }

        public BeatIntention Intention { get; }

        /// <summary>The composed act, or null when it could not be composed or was ruled out.</summary>
        public SpeechAct Act { get; }

        public double Total { get; }

        public IReadOnlyList<IntentReason> Reasons { get; }

        /// <summary>Why it was not available at all, or empty when it was.</summary>
        public string Refusal { get; }

        public bool IsAvailable => Act != null && Refusal.Length == 0;

        public override string ToString()
        {
            return Intention.Act + " " + (IsAvailable ? Total.ToString("0.##") : "(" + Refusal + ")");
        }
    }

    /// <summary>What the actor decided, with every candidate's score kept beside it.</summary>
    public sealed class IntentChoice
    {
        internal IntentChoice(IntentScore chosen, IReadOnlyList<IntentScore> considered)
        {
            Chosen = chosen;
            Considered = considered ?? new IntentScore[0];
        }

        /// <summary>The winning candidate, or null when the actor had nothing to say.</summary>
        public IntentScore Chosen { get; }

        public IReadOnlyList<IntentScore> Considered { get; }

        public bool Spoke => Chosen != null && Chosen.Act != null;

        public SpeechAct Act => Chosen?.Act;
    }

    /// <summary>
    /// Which of the things a beat makes possible this particular actor actually tries to
    /// communicate (BQ-146).
    ///
    /// The layer the whole routing model turns on. A storylet says what could sensibly be said
    /// here; this says what *this person* says, from state the simulation already holds, and it is
    /// the reason two castings of one storylet are two scenes rather than one scene with two
    /// names. A merciful creditor reaches for an offer where a vindictive one reaches for a
    /// threat, and neither of those sentences is written anywhere in content.
    ///
    /// <b>Character logic first, dice second.</b> Scores come from personality, problem-solving
    /// preference, sensitivities, current emotion, the tie to the listener, what the speaker
    /// actually knows and what is owed between them. A bounded jitter is added last, from a
    /// stream forked on the actor and the beat, so that a genuinely close call can go either way
    /// on two seeds and a clear preference never does. Randomness resolves ties; it does not make
    /// decisions.
    ///
    /// <b>It reads no identity.</b> Nothing here consults an occupation, a race, an archetype or
    /// BQ-145's affordances, and it must not start to: a Punk is not more likely to threaten
    /// somebody for being a Punk, and the anti-stereotype gate is only worth anything if the layer
    /// that decides what people say honours it. What somebody does for a living reaches a scene
    /// through eligibility, plausibility and stakes, never through temperament.
    ///
    /// <b>It creates nothing.</b> It composes candidate <see cref="SpeechAct"/>s and scores them.
    /// It writes no fact, no belief, no event and no obligation, and an act it could not compose
    /// is dropped rather than repaired - so an actor who cannot honestly say any of the things a
    /// beat offers says nothing, and the beat routes on the silence.
    /// </summary>
    public static class ActorIntent
    {
        /// <summary>
        /// How far a coin-flip may move a score. Small on purpose: it has to be able to separate
        /// two moves a character is genuinely torn between, and it must never overturn a
        /// preference the character actually has.
        /// </summary>
        public const double Jitter = 0.15;

        public static IntentChoice Choose(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId speaker,
            EntityId listener,
            Fact focus,
            IReadOnlyList<BeatIntention> intentions,
            IReadOnlyDictionary<string, EntityId> roles,
            bool inPublic,
            string beatId,
            DeterministicRng rng,
            SpeechAct inReplyTo = null)
        {
            List<IntentScore> considered = new List<IntentScore>();
            if (world == null || intentions == null || intentions.Count == 0)
            {
                return new IntentChoice(null, considered);
            }

            NarrativeNpc actor = Npc(world, speaker);
            GameTime now = vanilla == null ? GameTime.Zero : vanilla.Now;

            for (int i = 0; i < intentions.Count; i++)
            {
                considered.Add(Score(world, actor, speaker, listener, focus, intentions[i], roles, inPublic, now, beatId, rng, inReplyTo));
            }

            IntentScore best = null;
            for (int i = 0; i < considered.Count; i++)
            {
                if (considered[i].IsAvailable && (best == null || considered[i].Total > best.Total))
                {
                    best = considered[i];
                }
            }

            return new IntentChoice(best, considered);
        }

        private static IntentScore Score(
            NarrativeWorldState world,
            NarrativeNpc actor,
            EntityId speaker,
            EntityId listener,
            Fact focus,
            BeatIntention intention,
            IReadOnlyDictionary<string, EntityId> roles,
            bool inPublic,
            GameTime now,
            string beatId,
            DeterministicRng rng,
            SpeechAct inReplyTo)
        {
            EntityId referent = Referent(intention, roles);
            ActionBinding content = Content(intention, focus);

            string refusal = Ineligible(world, speaker, focus, intention);
            if (refusal.Length != 0)
            {
                return new IntentScore(intention, null, 0.0, null, refusal);
            }

            // Composed against what was just said to this speaker where there is anything, because
            // several acts are unintelligible without it - an evasion of nothing is just talk, and
            // a refusal of nothing is not a refusal. An act that only fails *because* of the
            // antecedent is retried without one when its profile permits, so an unrelated remark
            // earlier in the scene cannot make a well-formed move unsayable.
            SpeechAct antecedent = Antecedent(inReplyTo, speaker);
            SpeechAct act = SpeechAct.Compose(intention.Act, speaker, listener, content, referent, antecedent);
            if (act == null && antecedent != null && !SpeechActProfile.Of(intention.Act).AntecedentRequired)
            {
                act = SpeechAct.Compose(intention.Act, speaker, listener, content, referent);
            }

            if (act == null)
            {
                return new IntentScore(intention, null, 0.0, null,
                    SpeechAct.WhyNot(intention.Act, speaker, new[] { listener }, content, referent, antecedent));
            }

            List<IntentReason> reasons = new List<IntentReason>();
            double total = 1.0;
            total += Temperament(actor, intention.Act, reasons);
            total += Preference(actor, intention.Act, reasons);
            total += Feeling(actor, intention.Act, now, reasons);
            total += Tie(world, speaker, listener, intention.Act, reasons);
            total += Standing(world, speaker, listener, intention.Act, reasons);
            total += Conviction(world, speaker, focus, intention.Act, reasons);
            total += Exposure(actor, intention.Act, inPublic, reasons);

            double roll = Chance(rng, speaker, beatId, intention.Act);
            reasons.Add(new IntentReason("chance", roll));
            total += roll;

            return new IntentScore(intention, act, total, reasons, string.Empty);
        }

        /// <summary>
        /// The gates that are not a matter of taste.
        ///
        /// Asserting the focus needs the speaker to hold it: somebody who does not know a theft
        /// happened cannot accuse anybody of it, inform anybody about it or pass it on, and letting
        /// a score decide that would be the storylet layer granting knowledge. Owning it needs them
        /// to be the person it is about. Everything else is preference and is scored.
        ///
        /// Note what is <em>not</em> gated: a denial does not require innocence, and an accusation
        /// does not require the referent to be guilty. A false accusation and a lie are both moves
        /// the world has to be able to contain, and both are decided against the speaker's belief
        /// by <c>Deception</c> rather than prevented here.
        /// </summary>
        private static string Ineligible(NarrativeWorldState world, EntityId speaker, Fact focus, BeatIntention intention)
        {
            if (intention.Content != BeatContentSource.None && focus == null)
            {
                return "the scene has no claim to be about";
            }

            bool assertsFocus = intention.Act == SpeechActType.Accuse
                || intention.Act == SpeechActType.Inform
                || intention.Act == SpeechActType.Gossip
                || intention.Act == SpeechActType.Answer
                || intention.Act == SpeechActType.Admit;

            if (assertsFocus && focus != null && !world.Knowledge.Knows(speaker, focus.Id))
            {
                return "the speaker does not hold the claim";
            }

            if (intention.Act == SpeechActType.Admit && focus != null && focus.Subject != speaker)
            {
                return "the claim is not the speaker's to own";
            }

            return string.Empty;
        }

        /// <summary>
        /// What kind of person reaches for this move.
        ///
        /// Each term is one behavioural dimension pushed off its midpoint, so a wholly average
        /// character scores zero everywhere and every act is equally open to them - which is what
        /// makes the model a set of tendencies rather than a set of scripts. Nobody is forbidden
        /// their unlikely move; they are simply less likely to take it than somebody built for it.
        /// </summary>
        private static double Temperament(NarrativeNpc actor, SpeechActType act, List<IntentReason> reasons)
        {
            if (actor == null)
            {
                return 0.0;
            }

            PersonalityWeights p = actor.Personality;
            double total = 0.0;
            switch (act)
            {
                case SpeechActType.Accuse:
                    total += Term(reasons, "boldness", Off(p.Boldness), 0.6);
                    total += Term(reasons, "suspicion", Off(1.0 - p.Trust), 0.5);
                    total += Term(reasons, "vengefulness", Off(1.0 - p.Mercy), 0.4);
                    break;
                case SpeechActType.Threaten:
                    total += Term(reasons, "boldness", Off(p.Boldness), 0.5);
                    total += Term(reasons, "vengefulness", Off(1.0 - p.Mercy), 0.7);
                    total += Term(reasons, "coldness", Off(1.0 - p.Warmth), 0.4);
                    break;
                case SpeechActType.Deny:
                    total += Term(reasons, "deceptiveness", Off(1.0 - p.Honesty), 0.6);
                    total += Term(reasons, "pride", Off(1.0 - p.Humility), 0.3);
                    break;
                case SpeechActType.Admit:
                    total += Term(reasons, "honesty", Off(p.Honesty), 0.8);
                    total += Term(reasons, "humility", Off(p.Humility), 0.4);
                    break;
                case SpeechActType.Apologize:
                    total += Term(reasons, "humility", Off(p.Humility), 0.7);
                    total += Term(reasons, "earnestness", Off(p.Earnestness), 0.4);
                    break;
                case SpeechActType.Forgive:
                    total += Term(reasons, "mercy", Off(p.Mercy), 0.9);
                    total += Term(reasons, "warmth", Off(p.Warmth), 0.4);
                    break;
                case SpeechActType.Offer:
                    total += Term(reasons, "generosity", Off(p.Generosity), 0.6);
                    total += Term(reasons, "mercy", Off(p.Mercy), 0.4);
                    break;
                case SpeechActType.Refuse:
                    total += Term(reasons, "coldness", Off(1.0 - p.Warmth), 0.5);
                    total += Term(reasons, "closedness", Off(1.0 - p.Generosity), 0.4);
                    break;
                case SpeechActType.Evade:
                    total += Term(reasons, "timidity", Off(1.0 - p.Boldness), 0.6);
                    total += Term(reasons, "deceptiveness", Off(1.0 - p.Honesty), 0.4);
                    break;
                case SpeechActType.Gossip:
                    total += Term(reasons, "disloyalty", Off(1.0 - p.Loyalty), 0.7);
                    total += Term(reasons, "curiosity", Off(p.Curiosity), 0.3);
                    break;
                case SpeechActType.Inform:
                    total += Term(reasons, "honesty", Off(p.Honesty), 0.5);
                    total += Term(reasons, "warmth", Off(p.Warmth), 0.4);
                    break;
                case SpeechActType.Warn:
                    total += Term(reasons, "warmth", Off(p.Warmth), 0.5);
                    total += Term(reasons, "loyalty", Off(p.Loyalty), 0.4);
                    break;
                case SpeechActType.Ask:
                    total += Term(reasons, "curiosity", Off(p.Curiosity), 0.6);
                    total += Term(reasons, "suspicion", Off(1.0 - p.Trust), 0.3);
                    break;
                case SpeechActType.Request:
                    total += Term(reasons, "boldness", Off(p.Boldness), 0.4);
                    break;
                case SpeechActType.Promise:
                    total += Term(reasons, "earnestness", Off(p.Earnestness), 0.6);
                    total += Term(reasons, "loyalty", Off(p.Loyalty), 0.4);
                    break;
                case SpeechActType.Answer:
                    total += Term(reasons, "honesty", Off(p.Honesty), 0.5);
                    break;
            }

            return total;
        }

        /// <summary>
        /// How this actor habitually turns a problem into action (BQ-057), which is the durable
        /// preference personality alone does not carry.
        ///
        /// The mapping is many-to-one and partial, the same way <c>SpeechActMeaning</c>'s is: most
        /// styles are not ways of speaking at all, and the ones that are cover several acts each.
        /// A style nothing maps to contributes nothing rather than a default.
        /// </summary>
        private static double Preference(NarrativeNpc actor, SpeechActType act, List<IntentReason> reasons)
        {
            if (actor == null)
            {
                return 0.0;
            }

            ProblemSolvingProfile style = actor.ProblemSolving;
            switch (act)
            {
                case SpeechActType.Accuse:
                case SpeechActType.Threaten:
                    return Term(reasons, "confronts", Off(style.Confront), 0.8);
                case SpeechActType.Evade:
                    return Term(reasons, "avoids", Off(style.Avoid), 0.8);
                case SpeechActType.Refuse:
                    return Term(reasons, "conceals", Off(style.Conceal), 0.5);
                case SpeechActType.Gossip:
                    return Term(reasons, "publicizes", Off(style.Publicize), 0.7);
                case SpeechActType.Inform:
                    return Term(reasons, "publicizes", Off(style.Publicize), 0.4);
                case SpeechActType.Request:
                    return Term(reasons, "asks others", Off(style.AskFriends), 0.7);
                case SpeechActType.Offer:
                    return Term(reasons, "pays", Off(style.PaySomeone), 0.6);
                case SpeechActType.Deny:
                    return Term(reasons, "conceals", Off(style.Conceal), 0.6);
                case SpeechActType.Warn:
                case SpeechActType.Promise:
                    return Term(reasons, "handles it", Off(style.DoItSelf), 0.4);
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// What they are feeling right now, which is transient and decays - so the same person
        /// answers the same beat differently on two days, and returns to themself if left alone.
        /// </summary>
        private static double Feeling(NarrativeNpc actor, SpeechActType act, GameTime now, List<IntentReason> reasons)
        {
            if (actor == null)
            {
                return 0.0;
            }

            EmotionalStateProfile mood = actor.Emotions;
            double total = 0.0;
            switch (act)
            {
                case SpeechActType.Accuse:
                    total += Mood(reasons, "anger", mood, EmotionalState.Anger, now, 0.9);
                    total += Mood(reasons, "suspicion", mood, EmotionalState.Suspicion, now, 0.6);
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, -0.7);
                    total += Mood(reasons, "shame", mood, EmotionalState.Shame, now, -0.4);
                    break;
                case SpeechActType.Threaten:
                    total += Mood(reasons, "anger", mood, EmotionalState.Anger, now, 1.0);
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, -0.6);
                    break;
                case SpeechActType.Admit:
                    total += Mood(reasons, "shame", mood, EmotionalState.Shame, now, 0.8);
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, -0.4);
                    break;
                case SpeechActType.Apologize:
                    total += Mood(reasons, "shame", mood, EmotionalState.Shame, now, 0.9);
                    break;
                case SpeechActType.Forgive:
                    total += Mood(reasons, "relief", mood, EmotionalState.Relief, now, 0.6);
                    total += Mood(reasons, "affection", mood, EmotionalState.Affection, now, 0.7);
                    total += Mood(reasons, "anger", mood, EmotionalState.Anger, now, -1.0);
                    break;
                case SpeechActType.Evade:
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, 0.9);
                    total += Mood(reasons, "stress", mood, EmotionalState.Stress, now, 0.5);
                    total += Mood(reasons, "shame", mood, EmotionalState.Shame, now, 0.4);
                    break;
                case SpeechActType.Deny:
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, 0.7);
                    break;
                case SpeechActType.Refuse:
                    total += Mood(reasons, "stress", mood, EmotionalState.Stress, now, 0.6);
                    total += Mood(reasons, "grief", mood, EmotionalState.Grief, now, 0.5);
                    total += Mood(reasons, "anger", mood, EmotionalState.Anger, now, 0.3);
                    break;
                case SpeechActType.Ask:
                    total += Mood(reasons, "suspicion", mood, EmotionalState.Suspicion, now, 0.7);
                    break;
                case SpeechActType.Warn:
                case SpeechActType.Offer:
                    total += Mood(reasons, "affection", mood, EmotionalState.Affection, now, 0.5);
                    break;
                case SpeechActType.Inform:
                    total += Mood(reasons, "relief", mood, EmotionalState.Relief, now, 0.3);
                    total += Mood(reasons, "fear", mood, EmotionalState.Fear, now, -0.5);
                    break;
            }

            return total;
        }

        /// <summary>
        /// What the speaker is to the person opposite, read off the graph the world already keeps.
        ///
        /// Directed, because the graph is: being owed and owing pull in opposite directions, and a
        /// creditor pressing a debtor is not the same scene as a debtor facing a creditor.
        /// </summary>
        private static double Tie(NarrativeWorldState world, EntityId speaker, EntityId listener, SpeechActType act, List<IntentReason> reasons)
        {
            RelationshipEdge edge = world.Relationships.Find(speaker, listener);
            if (edge == null)
            {
                // A stranger volunteers less and refuses more. Not a judgement about anybody: it
                // is the absence of a reason to do otherwise.
                switch (act)
                {
                    case SpeechActType.Inform:
                    case SpeechActType.Offer:
                        return Term(reasons, "no tie", -0.3, 1.0);
                    case SpeechActType.Refuse:
                        return Term(reasons, "no tie", 0.2, 1.0);
                    default:
                        return 0.0;
                }
            }

            double warmth = edge.Sentiment / 100.0;
            double total = 0.0;
            switch (edge.Kind)
            {
                case RelationKind.Friend:
                case RelationKind.Family:
                case RelationKind.Spouse:
                    total += Adjust(reasons, "close tie", act, 0.7,
                        SpeechActType.Inform, SpeechActType.Warn, SpeechActType.Forgive, SpeechActType.Offer, SpeechActType.Promise);
                    total += Adjust(reasons, "close tie", act, -0.8, SpeechActType.Accuse, SpeechActType.Threaten, SpeechActType.Gossip);
                    break;
                case RelationKind.Rival:
                case RelationKind.Enemy:
                    total += Adjust(reasons, "hostile tie", act, 0.7, SpeechActType.Accuse, SpeechActType.Threaten, SpeechActType.Refuse);
                    total += Adjust(reasons, "hostile tie", act, -0.8, SpeechActType.Forgive, SpeechActType.Offer, SpeechActType.Inform);
                    break;
                case RelationKind.Creditor:
                    total += Adjust(reasons, "is owed", act, 0.6, SpeechActType.Request, SpeechActType.Threaten, SpeechActType.Forgive);
                    break;
                case RelationKind.Debtor:
                    total += Adjust(reasons, "owes", act, 0.6, SpeechActType.Offer, SpeechActType.Apologize, SpeechActType.Promise);
                    total += Adjust(reasons, "owes", act, -0.4, SpeechActType.Refuse);
                    break;
                case RelationKind.Accomplice:
                    total += Adjust(reasons, "shared exposure", act, 0.5, SpeechActType.Evade, SpeechActType.Deny, SpeechActType.Warn);
                    break;
            }

            if (Math.Abs(warmth) > 0.01)
            {
                total += Adjust(reasons, "sentiment", act, warmth * 0.5,
                    SpeechActType.Inform, SpeechActType.Forgive, SpeechActType.Offer, SpeechActType.Warn);
                total += Adjust(reasons, "sentiment", act, -warmth * 0.5, SpeechActType.Accuse, SpeechActType.Threaten);
            }

            return total;
        }

        /// <summary>
        /// What is actually owed between these two, as the obligation ledger holds it.
        ///
        /// Distinct from the relationship term above: a tie says what they are to each other, and
        /// this says that there is an open account. Somebody who is owed a favour has a reason to
        /// ask; somebody who owes one has a reason to offer, and to find refusing harder.
        /// </summary>
        private static double Standing(NarrativeWorldState world, EntityId speaker, EntityId listener, SpeechActType act, List<IntentReason> reasons)
        {
            bool owedToSpeaker = false;
            bool owedBySpeaker = false;
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                if (!obligation.IsOpen)
                {
                    continue;
                }

                owedToSpeaker = owedToSpeaker || (obligation.Creditor == speaker && obligation.Debtor == listener);
                owedBySpeaker = owedBySpeaker || (obligation.Debtor == speaker && obligation.Creditor == listener);
            }

            double total = 0.0;
            if (owedToSpeaker)
            {
                total += Adjust(reasons, "is owed", act, 0.5, SpeechActType.Request, SpeechActType.Forgive);
            }

            if (owedBySpeaker)
            {
                total += Adjust(reasons, "owes", act, 0.5, SpeechActType.Offer, SpeechActType.Promise, SpeechActType.Apologize);
                total += Adjust(reasons, "owes", act, -0.3, SpeechActType.Refuse, SpeechActType.Evade);
            }

            return total;
        }

        /// <summary>
        /// How firmly the speaker actually holds the claim, and whether they could show it.
        ///
        /// Evidence is what separates the accusation somebody makes from the one they only think
        /// about: being able to prove it makes naming it far easier, and holding it weakly makes
        /// asking a better move than asserting.
        /// </summary>
        private static double Conviction(NarrativeWorldState world, EntityId speaker, Fact focus, SpeechActType act, List<IntentReason> reasons)
        {
            if (focus == null)
            {
                return 0.0;
            }

            KnowledgeRecord belief;
            if (!world.Knowledge.TryGetBelief(speaker, focus.Id, out belief))
            {
                return Adjust(reasons, "holds nothing", act, 0.6, SpeechActType.Ask);
            }

            double conviction = Off(belief.Confidence);
            double total = Adjust(reasons, "conviction", act, conviction * 0.8,
                SpeechActType.Accuse, SpeechActType.Inform, SpeechActType.Answer, SpeechActType.Gossip);
            total += Adjust(reasons, "conviction", act, -conviction * 0.6, SpeechActType.Ask);

            if (world.Knowledge.CanProve(speaker, focus.Id))
            {
                total += Adjust(reasons, "can prove it", act, 0.6, SpeechActType.Accuse, SpeechActType.Inform, SpeechActType.Answer);
            }

            return total;
        }

        /// <summary>
        /// What being watched does to the move.
        ///
        /// The one term that reads the room rather than the person - and it reads the person too,
        /// because the same audience costs a great deal to somebody sensitive to being shown up
        /// and nothing at all to somebody who is not. It is why a timid actor takes a matter aside
        /// that a bold one names in the street, without either of them being scripted to.
        /// </summary>
        private static double Exposure(NarrativeNpc actor, SpeechActType act, bool inPublic, List<IntentReason> reasons)
        {
            if (actor == null || !inPublic)
            {
                return 0.0;
            }

            double exposed = actor.Sensitivities.PublicEmbarrassment;
            double timidity = Off(1.0 - actor.Personality.Boldness);
            double cost = (exposed - 0.5) + timidity;
            if (Math.Abs(cost) < 0.01)
            {
                return 0.0;
            }

            double total = Adjust(reasons, "in public", act, -cost, SpeechActType.Accuse, SpeechActType.Admit, SpeechActType.Apologize);
            total += Adjust(reasons, "in public", act, cost * 0.6, SpeechActType.Evade, SpeechActType.Refuse, SpeechActType.Deny);
            return total;
        }

        /// <summary>
        /// The coin, forked on who is deciding and where, so it is stable across runs and
        /// independent of how many other decisions were taken first.
        /// </summary>
        private static double Chance(DeterministicRng rng, EntityId speaker, string beatId, SpeechActType act)
        {
            if (rng == null)
            {
                return 0.0;
            }

            DeterministicRng stream = rng.Fork("bq146|intent|" + speaker.Value + "|" + beatId + "|" + act);
            return (stream.NextDouble() - 0.5) * 2.0 * Jitter;
        }

        private static double Adjust(List<IntentReason> reasons, string term, SpeechActType act, double weight, params SpeechActType[] applies)
        {
            for (int i = 0; i < applies.Length; i++)
            {
                if (applies[i] == act)
                {
                    return Term(reasons, term, weight, 1.0);
                }
            }

            return 0.0;
        }

        private static double Mood(
            List<IntentReason> reasons,
            string term,
            EmotionalStateProfile mood,
            EmotionalState state,
            GameTime now,
            double weight)
        {
            double intensity = mood == null ? 0.0 : mood.Get(state, now);
            return intensity <= 0.01 ? 0.0 : Term(reasons, term, intensity, weight);
        }

        private static double Term(List<IntentReason> reasons, string term, double value, double weight)
        {
            double contribution = value * weight;
            if (Math.Abs(contribution) < 0.001)
            {
                return 0.0;
            }

            reasons.Add(new IntentReason(term, contribution));
            return contribution;
        }

        /// <summary>A dimension as a signed distance from the midpoint, so the average person scores nothing.</summary>
        private static double Off(double value) => value - 0.5;

        /// <summary>
        /// The act this speaker may be answering, or null. Responding means responding to somebody
        /// who spoke to you: an act that was not addressed to this speaker is not theirs to answer,
        /// and their own last act is not something they respond to.
        /// </summary>
        private static SpeechAct Antecedent(SpeechAct inReplyTo, EntityId speaker)
        {
            if (inReplyTo == null || inReplyTo.Speaker == speaker || !inReplyTo.IsAddressedTo(speaker))
            {
                return null;
            }

            return inReplyTo;
        }

        private static EntityId Referent(BeatIntention intention, IReadOnlyDictionary<string, EntityId> roles)
        {
            if (intention.ReferentRole.Length == 0 || roles == null)
            {
                return EntityId.None;
            }

            return roles.TryGetValue(intention.ReferentRole, out EntityId referent) ? referent : EntityId.None;
        }

        private static ActionBinding Content(BeatIntention intention, Fact focus)
        {
            if (focus == null || intention.Content == BeatContentSource.None)
            {
                return ActionBinding.Empty;
            }

            if (intention.Content == BeatContentSource.FocusObject)
            {
                return new ActionBinding { Item = focus.Object };
            }

            return new ActionBinding { PropositionFact = focus.Id, Item = focus.Object };
        }

        private static NarrativeNpc Npc(NarrativeWorldState world, EntityId id)
        {
            return world.Registry.AllNpcs.TryGetValue(id, out NarrativeNpc npc) ? npc : null;
        }
    }
}
