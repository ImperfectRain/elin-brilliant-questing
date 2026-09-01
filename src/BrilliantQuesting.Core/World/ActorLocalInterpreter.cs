using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
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

        public string DerivedPredicate { get; }

        public string DerivedValue { get; }

        public double Confidence { get; }

        public IReadOnlyList<string> ScoreTerms { get; }

        private static readonly string[] EmptyTerms = new string[0];
    }

    public static class ActorLocalInterpreter
    {
        public static ActorInterpretationTrace Interpret(
            NarrativeWorldState world,
            EntityId actorId,
            EntityId sourceFactId,
            GameTime now)
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

            InterpretationChoice choice = Choose(actor, source);
            Fact derived = FindExisting(world, source, choice)
                           ?? CreateDerivedFact(world, source, choice);

            world.Knowledge.Teach(actorId, derived.Id, KnowledgeSource.Inference, choice.Confidence, now, false);

            return new ActorInterpretationTrace(
                actorId,
                sourceFactId,
                derived.Id,
                RenderSource(source),
                choice.Lens,
                choice.Predicate,
                choice.Value,
                choice.Confidence,
                choice.Terms);
        }

        private static InterpretationChoice Choose(NarrativeNpc actor, Fact source)
        {
            List<InterpretationChoice> choices = new List<InterpretationChoice>
            {
                SoilTrouble(actor, source),
                Contamination(actor, source),
                Sabotage(actor, source)
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

        private static InterpretationChoice SoilTrouble(NarrativeNpc actor, Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.35 : 0.0);
            score.Add("occupation cultivation", OccupationContains(actor, "farmer", "gardener", "rancher") ? 0.4 : 0.0);
            score.Add("value wealth", actor.Values.Wealth.Importance * 0.12);
            score.Add("value animals", actor.Values.Animals.Importance * 0.08);
            score.Add("sensitivity animals", actor.Sensitivities.Animals * 0.05);
            return new InterpretationChoice(
                "cultivation",
                FactPredicates.HasSoilTrouble,
                "soil trouble",
                Confidence(score.Total),
                score.Total,
                score.Terms);
        }

        private static InterpretationChoice Contamination(NarrativeNpc actor, Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.3 : 0.0);
            score.Add("occupation alchemy", OccupationContains(actor, "alchemist", "apothecary", "healer") ? 0.48 : 0.0);
            score.Add("value knowledge", actor.Values.Knowledge.Importance * 0.15);
            score.Add("sensitivity dishonesty", actor.Sensitivities.Dishonesty * 0.03);
            return new InterpretationChoice(
                "alchemical",
                FactPredicates.IsContaminated,
                "possible contamination",
                Confidence(score.Total),
                score.Total,
                score.Terms);
        }

        private static InterpretationChoice Sabotage(NarrativeNpc actor, Fact source)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("source damaged crop", IsDamagedCrop(source) ? 0.25 : 0.0);
            score.Add("occupation security", OccupationContains(actor, "guard", "reeve", "sheriff", "watch") ? 0.42 : 0.0);
            score.Add("role authority", HasRole(actor, "guard", "reeve", "authority") ? 0.22 : 0.0);
            score.Add("value law", actor.Values.Law.Importance * 0.18);
            score.Add("sensitivity theft", actor.Sensitivities.Theft * 0.08);
            score.Add("sensitivity dishonesty", actor.Sensitivities.Dishonesty * 0.08);
            score.Add("emotion suspicion", actor.Emotions.Get(EmotionalState.Suspicion) * 0.08);
            return new InterpretationChoice(
                "public order",
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

        private static bool OccupationContains(NarrativeNpc actor, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (Contains(actor.Occupation, needles[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRole(NarrativeNpc actor, params string[] roles)
        {
            foreach (string role in actor.Roles)
            {
                for (int i = 0; i < roles.Length; i++)
                {
                    if (string.Equals(role, roles[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
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
                string predicate,
                string value,
                double confidence,
                double score,
                IReadOnlyList<string> terms)
            {
                Lens = lens;
                Predicate = predicate;
                Value = value;
                Confidence = confidence;
                Score = score;
                Terms = terms;
            }

            public string Lens { get; }

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
