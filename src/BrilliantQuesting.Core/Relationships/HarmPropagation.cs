using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Relationships
{
    /// <summary>
    /// One person's share of somebody else's misfortune, and the tie it arrived through.
    ///
    /// The tie is kept because a consequence nobody can explain is a consequence the player reads
    /// as noise: "her brother" is the whole difference between a mysterious affinity drop and a
    /// town that turned out to be a family.
    /// </summary>
    public sealed class TieReaction
    {
        public TieReaction(EntityId reactor, EntityId victim, RelationKind through, int delta)
        {
            Reactor = reactor;
            Victim = victim;
            Through = through;
            Delta = delta;
        }

        public EntityId Reactor { get; }

        public EntityId Victim { get; }

        public RelationKind Through { get; }

        /// <summary>Signed disposition shift toward whoever did it. Never zero.</summary>
        public int Delta { get; }

        public override string ToString() => Reactor + " " + Through + " " + Victim + " " + Delta;
    }

    /// <summary>
    /// How far harm travels along a tie.
    ///
    /// Hurting a shopkeeper has always cost exactly what hurting a shopkeeper costs. It should
    /// also cost whatever her brother thinks of it, and the point of deriving that from the graph
    /// is that nothing anywhere says "this shopkeeper has a brother who is a guard". The pair is
    /// not scripted; the tie is, and the tie was already there for other reasons.
    ///
    /// Three rules keep it from becoming a town-wide punishment multiplier:
    ///
    /// **Closeness carries, distance does not.** A spouse carries most of it, a guildmate a
    /// quarter, an acquaintance almost none. The weights are damping factors, not amplifiers:
    /// no tie carries more of a blow than the person who took it.
    ///
    /// **Warmth carries it, not the label.** The share scales with the tie's sentiment, so an
    /// estranged brother barely reacts and a hostile one does not react at all. Hostile ties are
    /// deliberately silent rather than pleased: turning "I hurt someone you hate" into goodwill
    /// would make assault a way to farm relationships, which is a worse mechanic than the one
    /// this step exists to add.
    ///
    /// **Only the people who would hear.** Reactions are ranked by how much they move and the
    /// weakest are dropped, so a well-connected victim produces a handful of people who noticed
    /// rather than a mailing list. Rule 4 is what this is about: a tie close enough to carry a
    /// measurable share is a tie close enough that the victim tells them.
    /// </summary>
    public static class HarmPropagation
    {
        /// <summary>
        /// Target-affinity swing at which an event stops being a slight and starts being harm.
        /// Read off the consequence profile rather than a list of event types, so a new harmful
        /// verb propagates the day it is added and a trespass never does.
        /// </summary>
        public const int HarmThreshold = -15;

        /// <summary>People who react to one event. The rest of the graph is not news.</summary>
        public const int MaxReactors = 8;

        private static readonly Dictionary<RelationKind, double> Carried = new Dictionary<RelationKind, double>
        {
            { RelationKind.Spouse, 0.60 },
            { RelationKind.Family, 0.50 },
            { RelationKind.Friend, 0.40 },
            { RelationKind.Employer, 0.35 },
            { RelationKind.Employee, 0.35 },
            { RelationKind.Accomplice, 0.30 },
            { RelationKind.GuildMate, 0.25 },

            // Money is a tie, but it is a tie to the debt rather than to the person. A creditor
            // minds losing a debtor more than he minds the debtor.
            { RelationKind.Creditor, 0.20 },
            { RelationKind.Debtor, 0.15 },
            { RelationKind.Acquaintance, 0.10 },
            { RelationKind.Rival, 0.0 },
            { RelationKind.Enemy, 0.0 }
        };

        /// <summary>Share of a blow this kind of tie passes on, before sentiment is applied.</summary>
        public static double CarriedBy(RelationKind kind)
        {
            return Carried.TryGetValue(kind, out double weight) ? weight : 0.0;
        }

        /// <summary>
        /// Who else this hurt, strongest first.
        /// </summary>
        /// <param name="graph">The tie graph. Only edges pointing at the victim are consulted.</param>
        /// <param name="victim">Who it happened to.</param>
        /// <param name="targetAffinity">What the event did to the victim's own disposition.</param>
        /// <param name="magnitude">Event severity, already clamped by the caller.</param>
        /// <param name="eligible">
        /// Whether a given character may react at all. The caller owns that question because it
        /// depends on things the graph has no business knowing: whether they are a real character,
        /// whether they are alive, whether they are the one who did it, and whether they are the
        /// player - whose feelings are the player's own and never simulated for them.
        /// </param>
        public static IReadOnlyList<TieReaction> Reactions(
            RelationshipGraph graph,
            EntityId victim,
            int targetAffinity,
            double magnitude,
            Func<EntityId, bool> eligible,
            int maxReactors = MaxReactors)
        {
            List<TieReaction> reactions = new List<TieReaction>();
            if (graph == null || victim.IsNone || targetAffinity > HarmThreshold)
            {
                return reactions;
            }

            foreach (RelationshipEdge edge in graph.EdgesTo(victim))
            {
                if (edge.From == victim || edge.Sentiment <= 0)
                {
                    continue;
                }

                if (eligible != null && !eligible(edge.From))
                {
                    continue;
                }

                double share = CarriedBy(edge.Kind) * (edge.Sentiment / 100.0);
                int delta = Round(targetAffinity * magnitude * share);
                if (delta == 0)
                {
                    // A tie this distant is not a channel. Nothing reaches them, and forcing a
                    // minimum here is how every stranger in town ends up with an opinion.
                    continue;
                }

                reactions.Add(new TieReaction(edge.From, victim, edge.Kind, delta));
            }

            // Ordered so the same save produces the same reactions in the same order: strongest
            // first, ties broken by id rather than by dictionary iteration.
            reactions.Sort(CompareStrength);

            if (maxReactors >= 0 && reactions.Count > maxReactors)
            {
                reactions.RemoveRange(maxReactors, reactions.Count - maxReactors);
            }

            return reactions;
        }

        private static int CompareStrength(TieReaction left, TieReaction right)
        {
            int byMagnitude = Math.Abs(right.Delta).CompareTo(Math.Abs(left.Delta));
            return byMagnitude != 0 ? byMagnitude : left.Reactor.CompareTo(right.Reactor);
        }

        private static int Round(double value)
        {
            return (int)(value >= 0 ? value + 0.5 : value - 0.5);
        }
    }
}
