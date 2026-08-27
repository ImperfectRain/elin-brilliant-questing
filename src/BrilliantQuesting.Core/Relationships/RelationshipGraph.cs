using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Relationships
{
    public enum RelationKind
    {
        Acquaintance,
        Friend,
        Rival,
        Enemy,
        Family,
        Spouse,
        Employer,
        Employee,
        Creditor,
        Debtor,
        GuildMate,
        Accomplice
    }

    /// <summary>
    /// A directed tie. Direction matters: a creditor's view of a debtor is not the debtor's view
    /// of the creditor, and an NPC can be someone's friend without it being returned.
    /// </summary>
    public sealed class RelationshipEdge
    {
        public RelationshipEdge(EntityId from, EntityId to, RelationKind kind, int sentiment)
        {
            From = from;
            To = to;
            Kind = kind;
            Sentiment = sentiment;
        }

        public EntityId From { get; }

        public EntityId To { get; }

        public RelationKind Kind { get; set; }

        /// <summary>-100..100. Distinct from vanilla affinity, which only tracks the player.</summary>
        public int Sentiment { get; set; }

        public override string ToString() => From + " -" + Kind + "(" + Sentiment + ")-> " + To;
    }

    /// <summary>
    /// Who is tied to whom. This is what makes harm propagate: hurting a shopkeeper is cheap
    /// until the graph reveals she is the guard captain's sister.
    /// </summary>
    public sealed class RelationshipGraph
    {
        private readonly Dictionary<EntityId, List<RelationshipEdge>> _outgoing = new Dictionary<EntityId, List<RelationshipEdge>>();

        public RelationshipEdge Connect(EntityId from, EntityId to, RelationKind kind, int sentiment)
        {
            RelationshipEdge existing = Find(from, to);
            if (existing != null)
            {
                existing.Kind = kind;
                existing.Sentiment = sentiment;
                return existing;
            }

            RelationshipEdge edge = new RelationshipEdge(from, to, kind, sentiment);
            EdgesFrom(from).Add(edge);
            return edge;
        }

        /// <summary>Creates the tie in both directions. Family and marriage are symmetric; debt is not.</summary>
        public void ConnectMutual(EntityId a, EntityId b, RelationKind kind, int sentiment)
        {
            Connect(a, b, kind, sentiment);
            Connect(b, a, kind, sentiment);
        }

        public RelationshipEdge Find(EntityId from, EntityId to)
        {
            if (!_outgoing.TryGetValue(from, out List<RelationshipEdge> edges))
            {
                return null;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].To == to)
                {
                    return edges[i];
                }
            }

            return null;
        }

        public IReadOnlyList<RelationshipEdge> EdgesOf(EntityId from)
        {
            return _outgoing.TryGetValue(from, out List<RelationshipEdge> edges) ? edges : Empty;
        }

        /// <summary>Everyone who holds a tie to this entity - the people who react when it is harmed.</summary>
        public IEnumerable<RelationshipEdge> EdgesTo(EntityId target)
        {
            foreach (List<RelationshipEdge> edges in _outgoing.Values)
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    if (edges[i].To == target)
                    {
                        yield return edges[i];
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<EntityId, List<RelationshipEdge>>> All => _outgoing;

        private List<RelationshipEdge> EdgesFrom(EntityId from)
        {
            if (!_outgoing.TryGetValue(from, out List<RelationshipEdge> edges))
            {
                edges = new List<RelationshipEdge>();
                _outgoing[from] = edges;
            }

            return edges;
        }

        private static readonly RelationshipEdge[] Empty = new RelationshipEdge[0];
    }
}
