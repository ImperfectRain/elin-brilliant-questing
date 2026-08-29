using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    public enum JournalTag
    {
        Known,
        Reported,
        Suspected,
        Disputed,
        Rumour
    }

    public sealed class JournalEntry
    {
        public JournalEntry(EntityId factId, string text, JournalTag tag, KnowledgeSource source, double confidence, bool canProve, GameTime learnedAt)
        {
            FactId = factId;
            Text = text;
            Tag = tag;
            Source = source;
            Confidence = confidence;
            CanProve = canProve;
            LearnedAt = learnedAt;
        }

        public EntityId FactId { get; }

        public string Text { get; }

        public JournalTag Tag { get; }

        public KnowledgeSource Source { get; }

        public double Confidence { get; }

        public bool CanProve { get; }

        public GameTime LearnedAt { get; }
    }

    /// <summary>
    /// Player-facing projection of what the player believes.
    /// </summary>
    public static class NarrativeJournal
    {
        public static IReadOnlyList<JournalEntry> Entries(NarrativeWorldState world, EntityId player)
        {
            List<JournalEntry> entries = new List<JournalEntry>();
            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(player))
            {
                Fact fact = world.Knowledge.GetFact(belief.FactId);
                if (fact == null)
                {
                    continue;
                }

                entries.Add(new JournalEntry(
                    fact.Id,
                    Render(world, fact),
                    TagFor(world, player, fact, belief),
                    belief.Source,
                    belief.Confidence,
                    belief.CanProve,
                    belief.LearnedAt));
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        public static string Describe(NarrativeWorldState world, EntityId player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Brilliant Questing journal\n");

            IReadOnlyList<JournalEntry> entries = Entries(world, player);
            if (entries.Count == 0)
            {
                sb.Append("  nothing known yet\n");
                return sb.ToString();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                JournalEntry entry = entries[i];
                sb.Append("  [").Append(entry.Tag).Append("] ").Append(entry.Text);
                sb.Append("  (").Append(entry.Source);
                sb.Append(", confidence ").Append(entry.Confidence.ToString("0.00"));
                sb.Append(entry.CanProve ? ", proof)" : ", no proof)");
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static JournalTag TagFor(NarrativeWorldState world, EntityId player, Fact fact, KnowledgeRecord belief)
        {
            if (IsDisputed(world, player, fact))
            {
                return JournalTag.Disputed;
            }

            if (belief.Source == KnowledgeSource.Hearsay)
            {
                return WasHeardAsRumour(world, player, fact.Id) || belief.ToldBy.IsNone
                    ? JournalTag.Rumour
                    : JournalTag.Reported;
            }

            if (belief.Source == KnowledgeSource.Inference || belief.Confidence < 0.7)
            {
                return JournalTag.Suspected;
            }

            return JournalTag.Known;
        }

        private static bool IsDisputed(NarrativeWorldState world, EntityId player, Fact fact)
        {
            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(player))
            {
                if (belief.FactId == fact.Id)
                {
                    continue;
                }

                Fact other = world.Knowledge.GetFact(belief.FactId);
                if (other != null && Contradicts(fact, other))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contradicts(Fact left, Fact right)
        {
            if (left.Id == right.DistortionOf || right.Id == left.DistortionOf)
            {
                return true;
            }

            return left.Subject == right.Subject
                   && string.Equals(left.Predicate, right.Predicate, StringComparison.Ordinal)
                   && (left.Object != right.Object || !string.Equals(left.Value, right.Value, StringComparison.Ordinal));
        }

        private static bool WasHeardAsRumour(NarrativeWorldState world, EntityId player, EntityId factId)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.Target != player)
                {
                    continue;
                }

                if (worldEvent.Type != WorldEventType.RumorSpread
                    && worldEvent.Type != WorldEventType.RumorDistorted)
                {
                    continue;
                }

                for (int r = 0; r < worldEvent.Related.Count; r++)
                {
                    if (worldEvent.Related[r] == factId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string Render(NarrativeWorldState world, Fact fact)
        {
            string subject = world.Registry.NameOf(fact.Subject);
            string obj = world.Registry.Npcs.ContainsKey(fact.Object)
                ? world.Registry.NameOf(fact.Object)
                : !string.IsNullOrEmpty(fact.Value) ? fact.Value : fact.Object.Value;
            return subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj;
        }

        private static int CompareEntries(JournalEntry left, JournalEntry right)
        {
            int byTime = left.LearnedAt.TotalMinutes.CompareTo(right.LearnedAt.TotalMinutes);
            return byTime != 0 ? byTime : left.FactId.CompareTo(right.FactId);
        }
    }
}
