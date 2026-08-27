using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    public enum TruthState
    {
        True,
        False,
        Uncertain,
        Superseded
    }

    /// <summary>
    /// Something that is objectively so about the world - independent of who believes it.
    ///
    /// This separation is the whole point of the subsystem. The world can know that Garron funds
    /// the Red Knives while every single character remains ignorant of it, and a character can
    /// hold a confident belief that is simply false.
    /// </summary>
    public sealed class Fact
    {
        public Fact(EntityId id, EntityId subject, string predicate, EntityId objectId, string value = null, TruthState truth = TruthState.True, int secrecy = 0, EntityId originEvent = default)
        {
            Id = id;
            Subject = subject;
            Predicate = predicate;
            Object = objectId;
            Value = value;
            Truth = truth;
            Secrecy = secrecy;
            OriginEvent = originEvent;
            EvidenceIds = new List<EntityId>();
        }

        public EntityId Id { get; }

        public EntityId Subject { get; }

        public string Predicate { get; }

        public EntityId Object { get; }

        /// <summary>Free value for predicates that need a scalar or label ("12000 orens").</summary>
        public string Value { get; }

        public TruthState Truth { get; set; }

        /// <summary>0 = public knowledge, 100 = actively hidden. Raises rumor resistance.</summary>
        public int Secrecy { get; set; }

        public EntityId OriginEvent { get; }

        /// <summary>Real objects that can substantiate this: a ledger, a ring, a corpse.</summary>
        public List<EntityId> EvidenceIds { get; }

        public override string ToString()
        {
            string tail = Object.IsNone ? Value : Object.ToString();
            return Subject + " " + Predicate + " " + tail + " [" + Truth + "]";
        }
    }
}
