using System;
using System.Collections.Generic;
using System.Globalization;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>A transient attention reading, never authoritative state or player-facing prose.</summary>
    public sealed class DevelopmentScore
    {
        public EntityId Speaker { get; internal set; }
        public EntityId FactId { get; internal set; }
        public double Salience { get; internal set; }
        public double Tension { get; internal set; }
        public double? Proximity { get; internal set; }
        public double Recurrence { get; internal set; }
        public double UnresolvedHistory { get; internal set; }
        public double? UnderusedMechanics { get; internal set; }
        public double ConsequenceVisibility { get; internal set; }
        public double Repetition { get; internal set; }
        public double RecentExposure { get; internal set; }
        public string Refusal { get; internal set; }
        public double Total => Salience + Tension + (Proximity ?? 0) + Recurrence
            + UnresolvedHistory + (UnderusedMechanics ?? 0) + ConsequenceVisibility
            - Repetition - RecentExposure;

        public string Explain()
        {
            return "salience=" + Number(Salience) + ", tension=" + Number(Tension)
                + ", proximity=" + Number(Proximity) + ", recurrence=" + Number(Recurrence)
                + ", unresolved history=" + Number(UnresolvedHistory)
                + ", underused mechanics=" + Number(UnderusedMechanics)
                + " (declared recovery-verb exposure, not action-use telemetry)"
                + ", consequence visibility=" + Number(ConsequenceVisibility)
                + ", repetition penalty=" + Number(Repetition)
                + ", recent exposure penalty=" + Number(RecentExposure)
                + ", total=" + Number(Total);
        }

        private static string Number(double? value) => value.HasValue
            ? value.Value.ToString("0.000", CultureInfo.InvariantCulture) : "unknown (no contribution)";
    }

    /// <summary>
    /// BQ-100: rank eligible news, not world outcomes. All evidence is read from existing
    /// authorities. Missing location/mechanic evidence remains unknown. No RNG or saved counters.
    /// </summary>
    public static class DevelopmentScoring
    {
        public static DevelopmentScore Read(NarrativeWorldState world, IVanillaState vanilla,
            EntityId speaker, EntityId factId, double salience, GameTime now)
        {
            var score = new DevelopmentScore { Speaker = speaker, FactId = factId, Salience = salience };
            Fact fact = world.Knowledge.GetFact(factId);
            EntityId player = vanilla.PlayerId;
            EntityId playerZone = vanilla.GetZoneOf(player);
            EntityId subjectZone = fact == null || fact.Subject.IsNone ? EntityId.None : vanilla.GetZoneOf(fact.Subject);
            if (!playerZone.IsNone && !subjectZone.IsNone)
                score.Proximity = playerZone == subjectZone ? 0.5 : 0;

            var matters = new List<NarrativeThread>();
            var participants = new HashSet<EntityId>();
            if (fact != null && !fact.Subject.IsNone && fact.Subject != player) participants.Add(fact.Subject);
            var verbs = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeThread thread in world.Threads)
            {
                if (!thread.FactIds.Contains(factId)) continue;
                matters.Add(thread);
                foreach (EntityId actor in thread.ParticipantIds)
                    if (!actor.IsNone && actor != player) participants.Add(actor);
                if (!thread.IsLive) continue;
                score.Tension = Math.Max(score.Tension, Math.Max(0, Math.Min(100, thread.Tension)) / 100.0);
                foreach (RecoveryRoute route in thread.RecoveryRoutes)
                    if (!string.IsNullOrEmpty(route.ActionId)) verbs.Add(route.ActionId);
                if (Known(world, player, thread))
                    score.UnresolvedHistory = Math.Max(score.UnresolvedHistory,
                        0.5 * Math.Min(1, Math.Max(0, (now.TotalMinutes - thread.CreatedAt.TotalMinutes) / 10080.0)));
            }

            int repeats = 0;
            int mechanicExposures = 0;
            foreach (NarrativeThread previous in world.Threads)
            {
                if (matters.Contains(previous) || !Known(world, player, previous)) continue;
                bool repeatsArchetype = false;
                foreach (NarrativeThread matter in matters)
                    if (previous.ArchetypeId == matter.ArchetypeId) repeatsArchetype = true;
                if (repeatsArchetype) repeats++;
                foreach (RecoveryRoute route in previous.RecoveryRoutes)
                    if (verbs.Contains(route.ActionId)) { mechanicExposures++; break; }
                foreach (EntityId actor in previous.ParticipantIds)
                    if (participants.Contains(actor)) score.Recurrence = 0.5;
            }
            score.Repetition = Math.Min(1, repeats * 0.25);
            if (verbs.Count > 0) score.UnderusedMechanics = 0.5 / (1 + mechanicExposures);

            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(player))
                foreach (NarrativeThread matter in matters)
                    if (matter.FactIds.Contains(belief.FactId))
                        score.RecentExposure = Math.Max(score.RecentExposure, Recency(now, belief.LearnedAt));

            foreach (WorldEvent entry in world.Ledger.Events)
            {
                bool playerInvolved = entry.Actor == player || entry.Target == player;
                if (playerInvolved && (participants.Contains(entry.Actor) || participants.Contains(entry.Target)))
                    score.Recurrence = 0.5;
                bool linked = entry.Id == fact?.OriginEvent;
                foreach (NarrativeThread matter in matters) linked |= matter.IsNamedBy(entry);
                if (!linked) continue;
                bool visible = playerInvolved || Contains(entry.Witnesses, player);
                if (visible) score.ConsequenceVisibility = 0.5;
                if (Contains(entry.Tags, ConsequenceArrivals.ArrivalTag)
                    && (Contains(entry.Tags, ConsequenceArrivals.PlayerSurfaceTag)
                        || Contains(entry.Tags, ConsequenceArrivals.PlayerPresentTag)))
                    score.RecentExposure = Math.Max(score.RecentExposure, Recency(now, entry.Time));
            }
            return score;
        }

        private static bool Known(NarrativeWorldState world, EntityId player, NarrativeThread thread)
        {
            foreach (EntityId fact in thread.FactIds) if (world.Knowledge.Knows(player, fact)) return true;
            return false;
        }

        // One in-game day; a backwards clock cannot erase a recent exposure penalty.
        private static double Recency(GameTime now, GameTime then) =>
            Math.Max(0, 1 - Math.Max(0, (now.TotalMinutes - then.TotalMinutes) / 1440.0));

        private static bool Contains<T>(IReadOnlyList<T> values, T value)
        {
            foreach (T item in values) if (EqualityComparer<T>.Default.Equals(item, value)) return true;
            return false;
        }
    }
}
