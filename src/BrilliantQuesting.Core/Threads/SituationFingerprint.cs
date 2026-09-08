using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// A present-day reading of an encountered matter, not a saved account of its past shape.
    /// Names, archetype ids, scalar claim wording and truth verdicts never enter the signature.
    /// Only the proposed claim and claims the player already holds contribute semantics.
    /// </summary>
    public sealed class SituationFingerprint
    {
        private SituationFingerprint() { }

        public string Domains { get; private set; }
        public bool? Urgent { get; private set; }
        public bool? Secret { get; private set; }
        public bool? RecurringPeople { get; private set; }
        public string Routes { get; private set; }

        public string Explain() => "domains=" + (Domains ?? "unknown")
            + "; pace=" + Label(Urgent, "urgent", "slow")
            + "; disclosure=" + Label(Secret, "secret", "public")
            + "; people=" + Label(RecurringPeople, "recurring", "no prior recorded encounter")
            + "; declared routes=" + (Routes ?? "unknown");

        public static SituationFingerprint Read(NarrativeWorldState world, EntityId player,
            NarrativeThread thread, GameTime at, EntityId proposedFact = default)
        {
            var result = new SituationFingerprint { Urgent = thread.Tension >= 50 };
            var domains = new SortedSet<string>(StringComparer.Ordinal);
            var people = new HashSet<EntityId>();
            foreach (EntityId id in thread.FactIds.Distinct())
            {
                if (id != proposedFact && !world.Knowledge.Knows(player, id)) continue;
                Fact fact = world.Knowledge.GetFact(id);
                if (fact == null) continue;
                string domain = Domain(fact.Predicate);
                if (domain != null) domains.Add(domain);
                result.Secret = (result.Secret ?? false) || fact.Secrecy > 0;
                if (world.Registry.GetNpc(fact.Subject) != null && fact.Subject != player)
                    people.Add(fact.Subject);
                if (world.Registry.GetNpc(fact.Object) != null && fact.Object != player)
                    people.Add(fact.Object);
            }
            result.Domains = domains.Count == 0 ? null : string.Join("+", domains);
            if (people.Count > 0)
            {
                result.RecurringPeople = false;
                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(player))
                {
                    if (belief.LearnedAt.TotalMinutes >= at.TotalMinutes || thread.FactIds.Contains(belief.FactId)) continue;
                    Fact prior = world.Knowledge.GetFact(belief.FactId);
                    if (prior != null && (people.Contains(prior.Subject) || people.Contains(prior.Object)))
                        result.RecurringPeople = true;
                }
                foreach (var entry in world.Ledger.Events)
                {
                    if (entry.Time >= at || thread.IsNamedBy(entry)) continue;
                    bool observed = entry.Actor == player || entry.Target == player || entry.Witnesses.Contains(player);
                    if (observed && (people.Contains(entry.Actor) || people.Contains(entry.Target)))
                        result.RecurringPeople = true;
                }
            }
            var routes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (RecoveryRoute route in thread.RecoveryRoutes)
                if (!string.IsNullOrEmpty(route.ActionId)) routes.Add(route.ActionId);
            result.Routes = routes.Count == 0 ? null : string.Join("+", routes);
            return result;
        }

        /// <summary>
        /// Five equally weighted axes. Missing evidence never matches missing evidence, and
        /// a known matching semantic domain is required before secondary axes can add weight.
        /// Routes describe declared possibilities, not actual play or current availability.
        /// </summary>
        public double Similarity(SituationFingerprint other)
        {
            if (Domains == null || Domains != other.Domains) return 0;
            double match = 0.2;
            if (Urgent.HasValue && Urgent == other.Urgent) match += 0.2;
            if (Secret.HasValue && Secret == other.Secret) match += 0.2;
            if (RecurringPeople.HasValue && RecurringPeople == other.RecurringPeople) match += 0.2;
            if (Routes != null && Routes == other.Routes) match += 0.2;
            return match;
        }

        private static string Label(bool? value, string yes, string no) =>
            value.HasValue ? (value.Value ? yes : no) : "unknown";

        // Semantic predicates, never archetypes, actors or authored wording. Death alone does
        // not establish violence, and an unclassified predicate must remain unknown.
        private static string Domain(string predicate)
        {
            switch (predicate)
            {
                case FactPredicates.Killed: return "violent";
                case FactPredicates.Owes:
                case FactPredicates.Needs:
                case FactPredicates.Funds:
                case FactPredicates.Hired: return "economic";
                case FactPredicates.LiedAbout:
                case FactPredicates.Extorted:
                case FactPredicates.ShelteredBy:
                case FactPredicates.WonCompetition: return "social";
                case FactPredicates.Stole:
                case FactPredicates.Forged:
                case FactPredicates.Investigating:
                case FactPredicates.Witnessed: return "investigative";
                default: return null;
            }
        }
    }

    public static class SituationRepetition
    {
        /// <summary>
        /// Compare at most three recently encountered distinct matters, within seven game days.
        /// Beliefs supply encounter times; unseen world state and proposals spend no exposure.
        /// Each prior matter counts once, shared claims cannot count as another experience.
        /// </summary>
        public static double Read(NarrativeWorldState world, EntityId player,
            IReadOnlyList<NarrativeThread> matters, EntityId proposedFact, GameTime now, out string explanation)
        {
            var encountered = new Dictionary<EntityId, long>();
            foreach (NarrativeThread thread in world.Threads)
            {
                if (thread.State == ThreadState.Quarantined) continue;
                foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(player))
                    if (thread.FactIds.Contains(belief.FactId)
                        && (!encountered.TryGetValue(thread.Id, out long time) || belief.LearnedAt.TotalMinutes < time))
                        encountered[thread.Id] = belief.LearnedAt.TotalMinutes;
            }
            var ordered = world.Threads.Where(t => encountered.ContainsKey(t.Id)
                    && !matters.Contains(t)
                    && !matters.Any(m => m.FactIds.Intersect(t.FactIds)
                        .Any(id => id == proposedFact || world.Knowledge.Knows(player, id)))
                    && Math.Max(0, now.TotalMinutes - encountered[t.Id]) < 10080)
                .OrderByDescending(t => encountered[t.Id]).ThenBy(t => t.Id);
            var recent = new List<NarrativeThread>();
            var encounteredClaims = new HashSet<EntityId>();
            foreach (NarrativeThread prior in ordered)
            {
                // Several carriers of the same encountered claim are one experience. Hidden
                // shared claims cannot establish that the player experienced them together.
                var knownClaims = prior.FactIds.Where(id => world.Knowledge.Knows(player, id)).ToList();
                bool shared = knownClaims.Any(id => encounteredClaims.Contains(id));
                encounteredClaims.UnionWith(knownClaims);
                if (shared) continue;
                recent.Add(prior);
                if (recent.Count == 3) break;
            }
            double penalty = 0;
            var readings = new List<string>();
            foreach (NarrativeThread matter in matters.OrderBy(t => t.Id))
            {
                var shape = SituationFingerprint.Read(world, player, matter,
                    encountered.TryGetValue(matter.Id, out long time) ? new GameTime(time) : now, proposedFact);
                readings.Add(matter.Id + " [" + shape.Explain() + "]");
                for (int i = 0; i < recent.Count; i++)
                {
                    NarrativeThread prior = recent[i];
                    var previous = SituationFingerprint.Read(world, player, prior, new GameTime(encountered[prior.Id]));
                    double similarity = shape.Similarity(previous);
                    // Strongest recent match, not multiplication by the number of carrier threads.
                    double weight = (1.0 / (i + 1))
                        * (1 - Math.Max(0, now.TotalMinutes - encountered[prior.Id]) / 10080.0);
                    penalty = Math.Max(penalty, similarity * weight);
                    readings.Add("compared " + prior.Id + " [" + previous.Explain() + "] similarity="
                        + similarity.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            explanation = readings.Count == 0 ? "unknown (no carrier matter)" : string.Join("; ", readings);
            return penalty;
        }
    }
}
