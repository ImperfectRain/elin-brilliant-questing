using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// What one actor makes of one event, beyond what they take it to be (CD §22.3).
    ///
    /// BQ-064 answers "what is this?" and its answer is already actor-local. This answers the two
    /// questions that come after it and are not the same question: <b>what about it is mine</b>
    /// (<see cref="Concern"/>) and <b>what would I do about it</b> (<see cref="Response"/>), plus
    /// <b>how odd it strikes me</b> (<see cref="Registers"/>). CD §22.3's own example is exactly
    /// this shape - the pragmatist, the zealot and the merchant are not three wordings of one
    /// remark about a speaking cow, they are three different things to have noticed about it and
    /// three different next moves.
    ///
    /// <b>There is no text on this type and no vocabulary of its own.</b> Every axis is an
    /// existing canonical actor vocabulary: <see cref="ValueConcern"/> is BQ-061's,
    /// <see cref="ProblemSolvingStyle"/> is BQ-062's, <see cref="WeirdnessLevel"/> is BQ-079's, and
    /// the interpretation is BQ-064's own trace, carried whole. Nothing here is a fragment, a tone
    /// or a phrase, and nothing here reads the event's wording - which is what makes "no bespoke
    /// text for that event" a fact about the type rather than a discipline about its content.
    ///
    /// <b>The event is untouched.</b> <see cref="SourceFactId"/> is carried, never rewritten, and
    /// two actors reacting to it produce two reactions and one unchanged fact. What differs
    /// between them is entirely on their side of the reading.
    /// </summary>
    public sealed class ActorReaction
    {
        private static readonly string[] NoTerms = new string[0];

        internal ActorReaction(
            EntityId actorId,
            EntityId sourceFactId,
            ActorInterpretationTrace interpretation,
            ValueConcern concern,
            ProblemSolvingStyle response,
            WeirdnessLevel premise,
            WeirdnessLevel registers,
            double intensity,
            IReadOnlyList<string> concernTerms,
            IReadOnlyList<string> responseTerms)
        {
            ActorId = actorId;
            SourceFactId = sourceFactId;
            Interpretation = interpretation;
            Concern = concern;
            Response = response;
            Premise = premise;
            Registers = registers;
            Intensity = intensity;
            ConcernTerms = concernTerms ?? NoTerms;
            ResponseTerms = responseTerms ?? NoTerms;
        }

        public EntityId ActorId { get; }

        /// <summary>The event reacted to, exactly as it was given. Never rewritten by reacting.</summary>
        public EntityId SourceFactId { get; }

        /// <summary>BQ-064's reading of the event for this actor, carried whole rather than re-derived.</summary>
        public ActorInterpretationTrace Interpretation { get; }

        /// <summary>Which of the actor's own concerns the event engages hardest.</summary>
        public ValueConcern Concern { get; }

        /// <summary>What this actor leans toward doing about it, in BQ-062's vocabulary.</summary>
        public ProblemSolvingStyle Response { get; }

        /// <summary>How central the scene's absurd premise is, as the caller stated it (BQ-079).</summary>
        public WeirdnessLevel Premise { get; }

        /// <summary>
        /// How odd the premise lands <em>for this actor</em>: <see cref="Premise"/> less what CD
        /// §23's character-weirdness tier already says they take in stride, floored at
        /// <see cref="WeirdnessLevel.Mundane"/>.
        ///
        /// This is why "it has always been opinionated" is a reaction and not a failure to react.
        /// A character the town already finds strange is the one least struck by a strange event,
        /// and CD §22.4's "not every NPC is eccentric" needs the converse to be sayable too.
        /// </summary>
        public WeirdnessLevel Registers { get; }

        /// <summary>How hard the reaction is held, 0..1. Concern pressure scaled by how confidently the event was read.</summary>
        public double Intensity { get; }

        /// <summary>Why this concern won, term by term, in the shape BQ-064's score terms already use.</summary>
        public IReadOnlyList<string> ConcernTerms { get; }

        /// <summary>Why this response won, term by term.</summary>
        public IReadOnlyList<string> ResponseTerms { get; }

        /// <summary>
        /// A wording-free identity for the reaction, in the sense <c>SpeechAct.Signature</c> is one
        /// for a meaning: same reaction, same string; different reaction, different string. It is
        /// what lets a test prove that five actors reacted five ways without anyone having written
        /// five lines.
        /// </summary>
        public string Signature =>
            Concern + "|" + Response + "|" + Registers + "|"
            + (Interpretation == null ? string.Empty : Interpretation.DerivedPredicate) + "|"
            + SourceFactId.Value;

        public override string ToString() => Signature;
    }

    /// <summary>
    /// BQ-080. The step from an interpretation to a reaction, and the last one before wording.
    ///
    /// <b>It invents no vocabulary.</b> The concern it picks is one of BQ-061's eight, the response
    /// one of BQ-062's fourteen, and both are scored out of state the actor already carries -
    /// values, sensitivities, personality, emotion, problem-solving preferences, and the identity
    /// affordances BQ-145 derives. There is no reaction ontology here, no per-event table, and no
    /// second personality model: an actor with no profile of their own reacts out of the defaults
    /// they were staged with, and two actors staged identically react identically.
    ///
    /// <b>It reads no words.</b> Nothing in the derivation touches <c>Fact.Value</c>,
    /// <c>Fact.Predicate</c>'s spelling or any other prose on the event. Retitling the event
    /// changes no reaction to it, which is the mechanical form of "no bespoke text for that
    /// event": there is nowhere for such text to be read even if somebody wrote it.
    ///
    /// <b>It stops before realization.</b> A reaction is a meaning, exactly as a
    /// <see cref="SpeechAct"/> is. Saying one aloud needs fragments authored for reactions, which
    /// are content (BQ-132) and do not exist yet; until they do, <see cref="ActorReaction"/> is
    /// what a scene holds and <see cref="ActorReaction.Registers"/> is what a later step will hand
    /// <see cref="RealizationRequest.WeirdnessBudget"/> so that a reaction stays drier than the
    /// event it is to (CD §22.4).
    ///
    /// <b>It writes nothing of its own.</b> <see cref="Derive"/> is pure. <see cref="React"/>
    /// writes only what <see cref="ActorLocalInterpreter.Interpret"/> already writes - the derived
    /// belief that observer now holds - and never the event.
    /// </summary>
    public static class ReactionDerivation
    {
        /// <summary>
        /// How much of a concern is in play before the actor's own weighting of it. Every concern
        /// gets it, which is what keeps <see cref="ValueConcern.Faith"/> and
        /// <see cref="ValueConcern.Freedom"/> - the two with neither a sensitivity nor a body of
        /// practical knowledge bearing on them - winnable on importance alone. Without it, "what
        /// you care about most is what you take up" would silently exclude two of the eight.
        /// </summary>
        private const double Baseline = 0.30;

        /// <summary>Interpret the event for this actor (BQ-064), then react to what they made of it.</summary>
        public static ActorReaction React(
            NarrativeWorldState world,
            EntityId actorId,
            EntityId sourceFactId,
            WeirdnessLevel premise,
            GameTime now,
            IVanillaState vanilla = null)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            NarrativeNpc actor = world.Registry.GetNpc(actorId);
            if (actor == null)
            {
                throw new InvalidOperationException("Cannot derive a reaction for unknown actor " + actorId);
            }

            ActorInterpretationTrace interpretation =
                ActorLocalInterpreter.Interpret(world, actorId, sourceFactId, now, vanilla);

            return Derive(actor, interpretation, premise, now, vanilla);
        }

        /// <summary>
        /// The reaction an actor in this state has to an event they read this way. Pure: it holds
        /// no world, writes nothing, draws no random numbers, and gives the same answer every time
        /// for the same actor state and the same interpretation.
        /// </summary>
        public static ActorReaction Derive(
            NarrativeNpc actor,
            ActorInterpretationTrace interpretation,
            WeirdnessLevel premise,
            GameTime now,
            IVanillaState vanilla = null)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (interpretation == null)
            {
                throw new ArgumentNullException(nameof(interpretation));
            }

            IdentityAffordances identity = IdentityAffordances.Of(actor, vanilla);

            ValueConcern concern = ValueConcern.Family;
            double pressure = -1.0;
            IReadOnlyList<string> concernTerms = null;

            foreach (ValueConcern candidate in Concerns)
            {
                ScoreBreakdown score = ScoreConcern(actor, identity, interpretation, candidate);
                double candidatePressure = Pressure(actor, candidate, score.Total);
                score.Note("pressure", candidatePressure);
                if (candidatePressure > pressure)
                {
                    concern = candidate;
                    pressure = candidatePressure;
                    concernTerms = score.Terms;
                }
            }

            ProblemSolvingStyle response = ProblemSolvingStyle.Confront;
            double best = double.NegativeInfinity;
            IReadOnlyList<string> responseTerms = null;

            foreach (ProblemSolvingStyle candidate in Styles)
            {
                ScoreBreakdown score = ScoreResponse(actor, concern, candidate, now);
                if (score.Total > best)
                {
                    response = candidate;
                    best = score.Total;
                    responseTerms = score.Terms;
                }
            }

            return new ActorReaction(
                actor.Id,
                interpretation.SourceFactId,
                interpretation,
                concern,
                response,
                premise,
                RegistersAs(actor, premise),
                Clamp01(pressure * interpretation.Confidence),
                concernTerms,
                responseTerms);
        }

        /// <summary>
        /// How odd this premise is to this actor: the scene's level less the tier CD §23 already
        /// assigned them, floored at <see cref="WeirdnessLevel.Mundane"/>. Reads
        /// <see cref="CharacterQuirkProfile.Weirdness"/> and nothing else - a quirk's
        /// <see cref="CharacterQuirk"/> says what somebody is odd about, which is not the same
        /// question and is not this step's to answer.
        /// </summary>
        public static WeirdnessLevel RegistersAs(NarrativeNpc actor, WeirdnessLevel premise)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            int left = (int)premise - (int)actor.Quirk.Weirdness;
            return left <= 0 ? WeirdnessLevel.Mundane : (WeirdnessLevel)left;
        }

        /// <summary>
        /// What this concern reaches for first, when nothing else pulls harder.
        ///
        /// A correspondence between two vocabularies that already exist, not a rule about who
        /// somebody is: it says what answering a concern looks like, and the actor's own
        /// <see cref="ProblemSolvingProfile"/> still decides whether they take it. An actor whose
        /// preferences point elsewhere overrides this every time, which is why it carries less
        /// weight below than the preference itself.
        /// </summary>
        public static ProblemSolvingStyle ReachesFor(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Family:
                    return ProblemSolvingStyle.Flee;
                case ValueConcern.Wealth:
                    return ProblemSolvingStyle.PaySomeone;
                case ValueConcern.Law:
                    return ProblemSolvingStyle.AskAuthority;
                case ValueConcern.Faith:
                    return ProblemSolvingStyle.SeekReligiousHelp;
                case ValueConcern.Status:
                    return ProblemSolvingStyle.Publicize;
                case ValueConcern.Animals:
                    return ProblemSolvingStyle.Wait;
                case ValueConcern.Knowledge:
                    return ProblemSolvingStyle.DoItSelf;
                case ValueConcern.Freedom:
                    return ProblemSolvingStyle.Avoid;
                default:
                    throw new ArgumentOutOfRangeException(nameof(concern), concern, "Unknown value concern.");
            }
        }

        /// <summary>
        /// How much of an event a concern has in it, before the actor's own weighting.
        ///
        /// Four kinds of term, every one of them named in the trace: the baseline every concern
        /// gets, the durable sensitivities that bear on it (BQ-063), the identity affordances that
        /// make somebody able to see that side of it at all (BQ-145), and the one that ties the
        /// reaction to the event - whether BQ-064's chosen lens is a reading of this concern's own
        /// domain. That last term is the reason the same event reaches two actors as two different
        /// concerns rather than as one concern held with two intensities.
        /// </summary>
        private static ScoreBreakdown ScoreConcern(
            NarrativeNpc actor,
            IdentityAffordances identity,
            ActorInterpretationTrace interpretation,
            ValueConcern concern)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("baseline", Baseline);

            foreach (SensitivityTopic topic in SensitivitiesOf(concern))
            {
                score.Add(
                    "sensitivity " + Name(topic.ToString()),
                    actor.Sensitivities.Get(topic) * 0.25);
            }

            IdentityDomain? domain = DomainOf(concern);
            if (domain.HasValue)
            {
                score.Add(
                    identity.ExplainKnowledge(domain.Value),
                    identity.PlausibleKnowledgeOf(domain.Value) * 0.30);
                score.Add(
                    "interpretation lens " + interpretation.Lens,
                    interpretation.LensDomain == domain.Value ? 0.25 : 0.0);
            }

            IdentityRole? role = RoleOf(concern);
            if (role.HasValue)
            {
                score.Add(
                    identity.ExplainEligibility(role.Value),
                    identity.IsEligibleFor(role.Value) ? 0.20 : 0.0);
            }

            return score;
        }

        /// <summary>
        /// BQ-062's own weighting, unchanged: what the concern is worth to this actor, discounted
        /// by how far they will bend on it. Kept identical to <c>MissingGoatProblemSolver</c>'s so
        /// that "how much a threatened value presses" has one answer in the simulation rather than
        /// two that drift.
        /// </summary>
        private static double Pressure(NarrativeNpc actor, ValueConcern concern, double engagement)
        {
            ValueConcernProfile value = actor.Values.Get(concern);
            return Clamp01(engagement * value.Importance * (1.0 - (value.Flexibility * 0.5)));
        }

        /// <summary>
        /// What this actor leans toward doing. Their durable preference carries it; one personality
        /// weight and, where one bears on it, one emotion bias it; and the concern that won adds
        /// what answering that concern usually looks like. The preference is deliberately the
        /// largest single term - a reaction is meant to reveal who somebody is, and the profile
        /// that says how they turn problems into action is the most direct statement of that the
        /// simulation holds.
        /// </summary>
        private static ScoreBreakdown ScoreResponse(
            NarrativeNpc actor,
            ValueConcern concern,
            ProblemSolvingStyle style,
            GameTime now)
        {
            PersonalityWeights personality = actor.Personality;
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("style preference " + Name(style.ToString()), actor.ProblemSolving.Get(style));

            switch (style)
            {
                case ProblemSolvingStyle.Confront:
                    score.Add("personality boldness", personality.Boldness * 0.20);
                    score.Add("emotion anger", Emotion(actor, EmotionalState.Anger, now) * 0.15);
                    break;
                case ProblemSolvingStyle.Avoid:
                    score.Add("personality low boldness", (1.0 - personality.Boldness) * 0.20);
                    score.Add("emotion fear", Emotion(actor, EmotionalState.Fear, now) * 0.15);
                    break;
                case ProblemSolvingStyle.AskAuthority:
                    score.Add("personality orderliness", personality.Orderliness * 0.20);
                    score.Add("emotion suspicion", Emotion(actor, EmotionalState.Suspicion, now) * 0.15);
                    break;
                case ProblemSolvingStyle.AskFriends:
                    score.Add("personality warmth", personality.Warmth * 0.20);
                    score.Add("emotion stress", Emotion(actor, EmotionalState.Stress, now) * 0.10);
                    break;
                case ProblemSolvingStyle.PaySomeone:
                    score.Add("personality generosity", personality.Generosity * 0.20);
                    break;
                case ProblemSolvingStyle.DoItSelf:
                    score.Add("personality curiosity", personality.Curiosity * 0.20);
                    break;
                case ProblemSolvingStyle.Manipulate:
                    score.Add("personality low honesty", (1.0 - personality.Honesty) * 0.20);
                    score.Add("emotion suspicion", Emotion(actor, EmotionalState.Suspicion, now) * 0.10);
                    break;
                case ProblemSolvingStyle.UseViolence:
                    score.Add("personality low mercy", (1.0 - personality.Mercy) * 0.20);
                    score.Add("emotion anger", Emotion(actor, EmotionalState.Anger, now) * 0.20);
                    break;
                case ProblemSolvingStyle.SeekGuild:
                    score.Add("personality loyalty", personality.Loyalty * 0.20);
                    break;
                case ProblemSolvingStyle.SeekReligiousHelp:
                    score.Add("personality earnestness", personality.Earnestness * 0.20);
                    score.Add("emotion grief", Emotion(actor, EmotionalState.Grief, now) * 0.10);
                    break;
                case ProblemSolvingStyle.Wait:
                    score.Add("personality patience", personality.Patience * 0.20);
                    score.Add("emotion relief", Emotion(actor, EmotionalState.Relief, now) * 0.10);
                    break;
                case ProblemSolvingStyle.Flee:
                    score.Add("personality low boldness", (1.0 - personality.Boldness) * 0.15);
                    score.Add("emotion fear", Emotion(actor, EmotionalState.Fear, now) * 0.25);
                    break;
                case ProblemSolvingStyle.Publicize:
                    score.Add("personality low humility", (1.0 - personality.Humility) * 0.20);
                    score.Add("emotion anger", Emotion(actor, EmotionalState.Anger, now) * 0.10);
                    break;
                case ProblemSolvingStyle.Conceal:
                    score.Add("personality low honesty", (1.0 - personality.Honesty) * 0.15);
                    score.Add("emotion shame", Emotion(actor, EmotionalState.Shame, now) * 0.20);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown problem-solving style.");
            }

            score.Add(
                "concern " + Name(concern.ToString()) + " reaches for " + Name(ReachesFor(concern).ToString()),
                ReachesFor(concern) == style ? 0.35 : 0.0);
            return score;
        }

        /// <summary>
        /// The durable triggers that bear on a concern (BQ-063). A correspondence between two
        /// existing vocabularies and nothing more: it says which sensitivities are about the same
        /// thing a concern is about, never what anybody concludes.
        /// </summary>
        private static IReadOnlyList<SensitivityTopic> SensitivitiesOf(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Family:
                    return new[] { SensitivityTopic.FamilyThreat };
                case ValueConcern.Wealth:
                    return new[] { SensitivityTopic.UnpaidDebt };
                case ValueConcern.Law:
                    return new[] { SensitivityTopic.Theft, SensitivityTopic.Dishonesty };
                case ValueConcern.Status:
                    return new[] { SensitivityTopic.Status, SensitivityTopic.PublicEmbarrassment };
                case ValueConcern.Animals:
                    return new[] { SensitivityTopic.Animals };
                case ValueConcern.Freedom:
                    return new[] { SensitivityTopic.Violence };
                default:
                    return NoSensitivities;
            }
        }

        /// <summary>
        /// Which body of practical knowledge bears on a concern - what knowing that work lets
        /// somebody see, never what somebody of that work believes. Read the same way BQ-064 reads
        /// it: through <see cref="IdentityAffordances"/>, which derives from observed and authored
        /// facets, so nothing here can turn a race, an archetype or a job title into an opinion.
        /// <see cref="ValueConcern.Faith"/> and <see cref="ValueConcern.Family"/> have none - no
        /// domain in the vocabulary is about them - and win on importance instead.
        /// </summary>
        private static IdentityDomain? DomainOf(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Wealth:
                    return IdentityDomain.Trade;
                case ValueConcern.Law:
                    return IdentityDomain.PublicOrder;
                case ValueConcern.Animals:
                    return IdentityDomain.Cultivation;
                case ValueConcern.Knowledge:
                    return IdentityDomain.Alchemy;
                default:
                    return null;
            }
        }

        /// <summary>Which role a concern is answerable through. Eligibility only, exactly as BQ-145 means it.</summary>
        private static IdentityRole? RoleOf(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Law:
                    return IdentityRole.Authority;
                case ValueConcern.Status:
                    return IdentityRole.GuildStanding;
                case ValueConcern.Wealth:
                    return IdentityRole.ServiceOperator;
                default:
                    return null;
            }
        }

        private static double Emotion(NarrativeNpc actor, EmotionalState emotion, GameTime now)
        {
            return actor.Emotions.Get(emotion, now);
        }

        /// <summary><c>AskAuthority</c> becomes <c>ask authority</c>, so a trace term reads as prose.</summary>
        private static string Name(string value)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]))
                {
                    sb.Append(' ');
                }

                sb.Append(char.ToLowerInvariant(value[i]));
            }

            return sb.ToString();
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }

        private static readonly SensitivityTopic[] NoSensitivities = new SensitivityTopic[0];

        /// <summary>
        /// Both vocabularies in declaration order, so the winner of a tie is the one the enum
        /// declares first - the same rule for every actor, every event and every run.
        /// </summary>
        private static readonly ValueConcern[] Concerns =
        {
            ValueConcern.Family, ValueConcern.Wealth, ValueConcern.Law, ValueConcern.Faith,
            ValueConcern.Status, ValueConcern.Animals, ValueConcern.Knowledge, ValueConcern.Freedom
        };

        private static readonly ProblemSolvingStyle[] Styles =
        {
            ProblemSolvingStyle.Confront, ProblemSolvingStyle.Avoid, ProblemSolvingStyle.AskAuthority,
            ProblemSolvingStyle.AskFriends, ProblemSolvingStyle.PaySomeone, ProblemSolvingStyle.DoItSelf,
            ProblemSolvingStyle.Manipulate, ProblemSolvingStyle.UseViolence, ProblemSolvingStyle.SeekGuild,
            ProblemSolvingStyle.SeekReligiousHelp, ProblemSolvingStyle.Wait, ProblemSolvingStyle.Flee,
            ProblemSolvingStyle.Publicize, ProblemSolvingStyle.Conceal
        };

        private sealed class ScoreBreakdown
        {
            private readonly List<string> _terms = new List<string>();

            public IReadOnlyList<string> Terms => _terms;

            public double Total { get; private set; }

            public void Add(string name, double value)
            {
                Total += value;
                _terms.Add(name + " " + value.ToString("0.00"));
            }

            /// <summary>Records a figure the trace should show without letting it into the total.</summary>
            public void Note(string name, double value)
            {
                _terms.Add(name + " " + value.ToString("0.00"));
            }
        }
    }
}
