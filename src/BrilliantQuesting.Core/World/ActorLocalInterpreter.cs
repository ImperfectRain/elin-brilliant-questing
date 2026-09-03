using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.World
{
    public sealed class ActorInterpretationTrace
    {
        public ActorInterpretationTrace(
            EntityId actorId,
            EntityId sourceFactId,
            EntityId derivedFactId,
            string source,
            string lens,
            IdentityDomain lensDomain,
            string derivedPredicate,
            string derivedValue,
            double confidence,
            IReadOnlyList<string> scoreTerms)
        {
            ActorId = actorId;
            SourceFactId = sourceFactId;
            DerivedFactId = derivedFactId;
            Source = source;
            Lens = lens;
            LensDomain = lensDomain;
            DerivedPredicate = derivedPredicate;
            DerivedValue = derivedValue;
            Confidence = confidence;
            ScoreTerms = scoreTerms ?? EmptyTerms;
        }

        public EntityId ActorId { get; }

        public EntityId SourceFactId { get; }

        public EntityId DerivedFactId { get; }

        public string Source { get; }

        public string Lens { get; }

        /// <summary>
        /// The body of practical knowledge <see cref="Lens"/> is the reading of - the same
        /// <see cref="IdentityDomain"/> the winning choice weighed
        /// <see cref="IdentityAffordances.PlausibleKnowledgeOf"/> for.
        ///
        /// Carried as the enum rather than left to be recovered from <see cref="Lens"/>' prose,
        /// because a consumer that parsed the words would be a second, silent copy of the
        /// correspondence this class already knows. BQ-080 is its first reader.
        /// </summary>
        public IdentityDomain LensDomain { get; }

        public string DerivedPredicate { get; }

        public string DerivedValue { get; }

        public double Confidence { get; }

        public IReadOnlyList<string> ScoreTerms { get; }

        private static readonly string[] EmptyTerms = new string[0];
    }

    public static class ActorLocalInterpreter
    {
        /// <summary>
        /// What this observer makes of one piece of evidence.
        ///
        /// <paramref name="vanilla"/> is optional and is only ever used to ask BQ-145 what this
        /// actor's identity makes plausible. Nothing here reads a facet itself, and nothing here
        /// keeps a private idea of what a job implies: "a dead crop is soil trouble to a farmer and
        /// contamination to an alchemist" is an identity read, and the identity read has exactly
        /// one owner. With no game attached the derivation falls back to what BQ authored about
        /// this actor, which is what a headless situation staged them with.
        /// </summary>
        public static ActorInterpretationTrace Interpret(
            NarrativeWorldState world,
            EntityId actorId,
            EntityId sourceFactId,
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
                throw new InvalidOperationException("Cannot interpret for unknown actor " + actorId);
            }

            Fact source = world.Knowledge.GetFact(sourceFactId);
            if (source == null)
            {
                throw new InvalidOperationException("Cannot interpret unknown fact " + sourceFactId);
            }

            InterpretationChoice choice = Choose(actor, IdentityAffordances.Of(actor, vanilla), source);
            Fact derived = FindExisting(world, source, choice)
                           ?? CreateDerivedFact(world, source, choice);

            world.Knowledge.Teach(actorId, derived.Id, KnowledgeSource.Inference, choice.Confidence, now, false);

            return new ActorInterpretationTrace(
                actorId,
                sourceFactId,
                derived.Id,
                RenderSource(source),
                choice.Lens,
                choice.Domain,
                choice.Predicate,
                choice.Value,
                choice.Confidence,
                choice.Terms);
        }

        private static InterpretationChoice Choose(
            NarrativeNpc actor,
            IdentityAffordances identity,
            Fact source)
        {
            List<InterpretationChoice> choices = new List<InterpretationChoice>
            {
                SoilTrouble(actor, identity, source),
                Contamination(actor, identity, source),
                Sabotage(actor, identity, source)
            };

            InterpretationChoice best = choices[0];
            for (int i = 1; i < choices.Count; i++)
            {
                if (choices[i].Score > best.Score)
                {
                    best = choices[i];
                }
            }

            return best;
        }

        private static InterpretationChoice SoilTrouble(
            NarrativeNpc actor,
            IdentityAffordances identity,
            Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.35 : 0.0);
            score.Add(
                identity.ExplainKnowledge(IdentityDomain.Cultivation),
                identity.PlausibleKnowledgeOf(IdentityDomain.Cultivation) * 0.4);
            score.Add("value wealth", actor.Values.Wealth.Importance * 0.12);
            score.Add("value animals", actor.Values.Animals.Importance * 0.08);
            score.Add("sensitivity animals", actor.Sensitivities.Animals * 0.05);
            return new InterpretationChoice(
                "cultivation",
                IdentityDomain.Cultivation,
                FactPredicates.HasSoilTrouble,
                "soil trouble",
                Confidence(score.Total),
                score.Total,
                score.Terms);
        }

        private static InterpretationChoice Contamination(
            NarrativeNpc actor,
            IdentityAffordances identity,
            Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.3 : 0.0);
            score.Add(
                identity.ExplainKnowledge(IdentityDomain.Alchemy),
                identity.PlausibleKnowledgeOf(IdentityDomain.Alchemy) * 0.48);
            score.Add("value knowledge", actor.Values.Knowledge.Importance * 0.15);
            score.Add("sensitivity dishonesty", actor.Sensitivities.Dishonesty * 0.03);
            return new InterpretationChoice(
                "alchemical",
                IdentityDomain.Alchemy,
                FactPredicates.IsContaminated,
                "possible contamination",
                Confidence(score.Total),
                score.Total,
                score.Terms);
        }

        private static InterpretationChoice Sabotage(
            NarrativeNpc actor,
            IdentityAffordances identity,
            Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.25 : 0.0);
            score.Add(
                identity.ExplainKnowledge(IdentityDomain.PublicOrder),
                identity.PlausibleKnowledgeOf(IdentityDomain.PublicOrder) * 0.42);
            score.Add(
                identity.ExplainEligibility(IdentityRole.Authority),
                identity.IsEligibleFor(IdentityRole.Authority) ? 0.22 : 0.0);
            score.Add("value law", actor.Values.Law.Importance * 0.18);
            score.Add("sensitivity theft", actor.Sensitivities.Theft * 0.08);
            score.Add("sensitivity dishonesty", actor.Sensitivities.Dishonesty * 0.08);
            score.Add("emotion suspicion", actor.Emotions.Get(EmotionalState.Suspicion) * 0.08);
            return new InterpretationChoice(
                "public order",
                IdentityDomain.PublicOrder,
                FactPredicates.MayBeSabotaged,
                "possible sabotage",
                Confidence(score.Total),
                score.Total,
                score.Terms);
        }

        private static Fact CreateDerivedFact(NarrativeWorldState world, Fact source, InterpretationChoice choice)
        {
            Fact derived = new Fact(
                world.NewId("fact"),
                source.Subject,
                choice.Predicate,
                source.Object,
                choice.Value,
                TruthState.Uncertain,
                source.Secrecy,
                source.OriginEvent)
            {
                DistortionOf = source.Id
            };

            for (int i = 0; i < source.EvidenceIds.Count; i++)
            {
                derived.EvidenceIds.Add(source.EvidenceIds[i]);
            }

            world.Knowledge.AddFact(derived);
            return derived;
        }

        private static Fact FindExisting(NarrativeWorldState world, Fact source, InterpretationChoice choice)
        {
            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.Subject == source.Subject
                    && fact.Object == source.Object
                    && fact.Predicate == choice.Predicate
                    && string.Equals(fact.Value, choice.Value, StringComparison.Ordinal)
                    && fact.DistortionOf == source.Id)
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsDamagedCrop(Fact source)
        {
            return string.Equals(source.Predicate, FactPredicates.Damaged, StringComparison.Ordinal)
                   && Contains(source.Value, "crop", "field", "plant", "harvest", "blight");
        }

        private static bool Contains(string text, params string[] needles)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static double Confidence(double score)
        {
            double confidence = 0.45 + (score * 0.35);
            return confidence > 0.9 ? 0.9 : confidence < 0.45 ? 0.45 : confidence;
        }

        private static string RenderSource(Fact source)
        {
            return source.Predicate + "(" + source.Subject + ", " +
                   (source.Object.IsNone ? source.Value : source.Object.Value) + ")";
        }

        private sealed class InterpretationChoice
        {
            public InterpretationChoice(
                string lens,
                IdentityDomain domain,
                string predicate,
                string value,
                double confidence,
                double score,
                IReadOnlyList<string> terms)
            {
                Lens = lens;
                Domain = domain;
                Predicate = predicate;
                Value = value;
                Confidence = confidence;
                Score = score;
                Terms = terms;
            }

            public string Lens { get; }

            public IdentityDomain Domain { get; }

            public string Predicate { get; }

            public string Value { get; }

            public double Confidence { get; }

            public double Score { get; }

            public IReadOnlyList<string> Terms { get; }
        }

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
        }
    }
}
