using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    public enum KnowledgeSource
    {
        /// <summary>Saw it happen. Highest confidence, and makes the knower a witness.</summary>
        Witnessed,

        /// <summary>Told by another character. Confidence decays with each retelling.</summary>
        Hearsay,

        /// <summary>Read it in a document or examined physical evidence.</summary>
        Document,

        /// <summary>Worked it out from other facts. Believable but not provable on its own.</summary>
        Inference,

        /// <summary>Was there. Applies to the perpetrator of the act.</summary>
        Participant
    }

    /// <summary>What one character believes about one fact, and how well they can back it up.</summary>
    public sealed class KnowledgeRecord
    {
        public KnowledgeRecord(EntityId knower, EntityId factId, KnowledgeSource source, double confidence, GameTime learnedAt, bool canProve, EntityId toldBy = default)
        {
            Knower = knower;
            FactId = factId;
            Source = source;
            Confidence = confidence;
            LearnedAt = learnedAt;
            CanProve = canProve;
            ToldBy = toldBy;
        }

        public EntityId Knower { get; }

        public EntityId FactId { get; }

        public KnowledgeSource Source { get; }

        /// <summary>0..1. Below ~0.3 a character will not act on the belief, only gossip about it.</summary>
        public double Confidence { get; set; }

        public GameTime LearnedAt { get; }

        /// <summary>
        /// Whether the knower could demonstrate it to a third party. Believing Varik ordered the
        /// beating is not the same as being able to show a guard the note that proves it.
        /// </summary>
        public bool CanProve { get; set; }

        public EntityId ToldBy { get; }
    }
}
