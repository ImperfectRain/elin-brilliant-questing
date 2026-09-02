using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>A candidate the world could have produced, and the reason it did not.</summary>
    public sealed class SuppressedCandidate
    {
        internal SuppressedCandidate(SituationCandidate candidate, string reason)
        {
            Candidate = candidate;
            Reason = reason;
        }

        public SituationCandidate Candidate { get; }

        /// <summary>Inspector-only. Why an otherwise plausible proposal was set aside.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// What a settlement currently affords, what it could produce from that, and what it was not
    /// allowed to produce twice.
    /// </summary>
    public sealed class SettlementSituationPlan
    {
        private static readonly SuppressedCandidate[] NothingSuppressed = new SuppressedCandidate[0];
        private static readonly SituationCandidate[] NoCandidates = new SituationCandidate[0];

        private readonly List<SituationCandidate> _candidates;
        private readonly List<SuppressedCandidate> _suppressed;

        internal SettlementSituationPlan(
            LocalAffordanceProfile profile,
            List<SituationCandidate> candidates,
            List<SuppressedCandidate> suppressed)
        {
            Profile = profile;
            _candidates = candidates;
            _suppressed = suppressed;
        }

        public LocalAffordanceProfile Profile { get; }

        /// <summary>Eligible proposals, best first.</summary>
        public IReadOnlyList<SituationCandidate> Candidates =>
            _candidates == null ? (IReadOnlyList<SituationCandidate>)NoCandidates : _candidates;

        /// <summary>
        /// Proposals the world state supported but repetition rules refused, each with its reason,
        /// so an empty candidate list can be told apart from a quiet settlement.
        /// </summary>
        public IReadOnlyList<SuppressedCandidate> Suppressed =>
            _suppressed == null ? (IReadOnlyList<SuppressedCandidate>)NothingSuppressed : _suppressed;

        public SituationCandidate BestCandidate => Candidates.Count == 0 ? null : Candidates[0];
    }

    /// <summary>
    /// Turns a settlement into situations: read the place, let each archetype say what it makes of
    /// it, refuse repetition, and only then touch the game.
    ///
    /// The orchestrator owns the order and nothing else. It holds no theft arithmetic - that lives
    /// in <see cref="PettyTheftPressure"/> - and the profile it reads holds none either. Adding
    /// BQ-041's shortage should mean adding a second pressure reader here, not editing either of
    /// the other two.
    /// </summary>
    public sealed class SettlementSituationGenerator
    {
        /// <summary>
        /// How long the world remembers that it already told this story.
        ///
        /// Conservative on purpose. This is repetition suppression, not the narrative director of
        /// BQ-099: it stops the same person robbing the same person of the same thing again, and
        /// deliberately does not model global content density, pacing or player attention.
        /// </summary>
        public const int RepetitionWindowDays = 30;

        private readonly PettyTheftPressure _theft = new PettyTheftPressure();

        /// <summary>
        /// Reads the settlement and scores what it could produce. Never writes to the game: a plan
        /// is a proposal, and nothing has happened until <see cref="TryGenerate"/> commits one.
        /// </summary>
        public SettlementSituationPlan Evaluate(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            LocalAffordanceProfile profile = LocalAffordanceProfile.Read(world, vanilla, zoneId);
            List<SituationCandidate> candidates = new List<SituationCandidate>();
            List<SuppressedCandidate> suppressed = new List<SuppressedCandidate>();

            IReadOnlyList<ActorAffordances> actors = profile.Actors;
            ActorAffordances[] byAttention = RankByAttention(world, profile);
            for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                ActorAffordances thief = actors[actorIndex];
                for (int targetIndex = 0; targetIndex < actors.Count; targetIndex++)
                {
                    ActorAffordances victim = actors[targetIndex];
                    if (actorIndex == targetIndex || thief.ActorId == victim.ActorId)
                    {
                        continue;
                    }

                    ActorAffordances witness = PickWitness(byAttention, thief, victim);
                    SituationCandidate candidate = _theft.Evaluate(world, profile, thief, victim, witness);

                    // Eligibility is decided on the world's own pressure, before anybody asks who
                    // the player knows. BQ-114 and BQ-115 choose between the situations a settlement
                    // already supports; neither is ever the reason one of them exists.
                    if (candidate == null || candidate.Score < PettyTheftPressure.MinimumScore)
                    {
                        continue;
                    }

                    candidate = PreferRecognisableFaces(profile, candidate);

                    string refusal = RepetitionReason(world, candidate, vanilla.Now);
                    if (refusal != null)
                    {
                        suppressed.Add(new SuppressedCandidate(candidate, refusal));
                        continue;
                    }

                    candidates.Add(candidate);
                }
            }

            candidates.Sort(CompareCandidates);
            return new SettlementSituationPlan(profile, candidates, suppressed);
        }

        public PettyTheftSituation TryGenerate(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId, GameTime now)
        {
            return TryGenerate(world, vanilla, Evaluate(world, vanilla, zoneId), zoneId, now);
        }

        /// <summary>
        /// Commits the best candidate a plan that has already been read will accept.
        ///
        /// Acts on the caller's plan rather than reading the settlement again. Evaluating twice was
        /// a doubled pass over every inventory in the zone, and worse, let the causes a caller had
        /// already reported describe a different candidate from the one built.
        ///
        /// Vanilla owns the outcome and goes first: the item has to actually move before any of
        /// this is recorded, so BQ can never hold a theft the game refused. If recording fails after
        /// the transfer, the transfer is taken back - an object moved by no event is the one thing
        /// the ledger exists to make impossible.
        /// </summary>
        public PettyTheftSituation TryGenerate(
            NarrativeWorldState world,
            IVanillaState vanilla,
            SettlementSituationPlan plan,
            EntityId zoneId,
            GameTime now)
        {
            if (plan == null)
            {
                return null;
            }

            for (int i = 0; i < plan.Candidates.Count; i++)
            {
                PettyTheftCandidate theft = new PettyTheftCandidate(plan.Candidates[i]);
                if (!vanilla.TryTransferItem(theft.Item.Id, theft.VictimId, theft.ThiefId))
                {
                    continue;
                }

                try
                {
                    return PettyTheftSituation.FromLocalAffordance(world, plan.Candidates[i], zoneId, now);
                }
                catch
                {
                    vanilla.TryTransferItem(theft.Item.Id, theft.ThiefId, theft.VictimId);
                    throw;
                }
            }

            return null;
        }

        /// <summary>
        /// BQ-114 and BQ-115. Adds whatever says the player will recognise the people a proposal
        /// is about - the history they made, or the casting decision the save made before they made
        /// any.
        ///
        /// Applied here rather than inside an archetype because it means the same thing for every
        /// archetype - a shortage that starves the shopkeeper the player buys from is the same kind
        /// of better story as a theft from the neighbour they know - and because an archetype that
        /// held its own opinion about the player would end up with as many opinions as there are
        /// archetypes.
        ///
        /// Only the principals count, and only the best-known of them: a familiar face among the
        /// people a situation is *about* is what makes it land, and knowing the witness as well
        /// does not make it land twice as hard. A cast of strangers records no term at all rather
        /// than a zero, because a stranger is an absence of history, not a measurement of it - and
        /// a cast nobody elected and nobody knows is exactly that.
        /// </summary>
        private static SituationCandidate PreferRecognisableFaces(
            LocalAffordanceProfile profile,
            SituationCandidate candidate)
        {
            ActorAffordances best = null;
            AppraiseRole(profile, candidate, SituationRoles.Actor, ref best);
            AppraiseRole(profile, candidate, SituationRoles.Target, ref best);
            if (best == null)
            {
                return candidate;
            }

            // BQ-115. History wins when there is any, because it is something the player actually
            // did; the elected face is what answers the question in a save where there is none.
            // One term either way - a face that is both known and elected is still one face.
            int known = best.Familiarity.Score;
            EarlyContact elected = best.EarlyContact;
            if (elected != null && elected.Weight > known)
            {
                return candidate.WithPressure(
                    SituationPressures.RecurringContact, elected.Weight, elected.Because);
            }

            return known <= 0
                ? candidate
                : candidate.WithPressure(
                    SituationPressures.PlayerFamiliarity, known, best.Familiarity.Because);
        }

        /// <summary>
        /// The principal the player is most likely to recognise, by whichever of the two grounds
        /// says more about them, then by id so the answer does not depend on binding order.
        /// </summary>
        private static void AppraiseRole(
            LocalAffordanceProfile profile,
            SituationCandidate candidate,
            string role,
            ref ActorAffordances best)
        {
            IReadOnlyList<EntityId> bound = candidate.ActorsIn(role);
            for (int i = 0; i < bound.Count; i++)
            {
                ActorAffordances local = profile.Of(bound[i]);
                if (local == null)
                {
                    continue;
                }

                int here = Recognisability(local);
                int incumbent = best == null ? -1 : Recognisability(best);
                if (best == null
                    || here > incumbent
                    || (here == incumbent
                        && here > 0
                        && string.CompareOrdinal(local.ActorId.Value, best.ActorId.Value) < 0))
                {
                    best = local;
                }
            }
        }

        private static int Recognisability(ActorAffordances local) =>
            Math.Max(local.Familiarity.Score, local.EarlyContact == null ? 0 : local.EarlyContact.Weight);

        /// <summary>
        /// The people here in the order they would be the one to have noticed something, most
        /// likely first.
        ///
        /// Ranked once for the settlement rather than re-ranked inside every ordered pair: how alert
        /// somebody is does not depend on who is robbing whom, and re-deriving it per pair made
        /// witness selection cubic in the size of a town. Attention decides; a genuine tie is broken
        /// by a fork of the world seed over the observer, so the answer is stable across reloads
        /// without being an artefact of the order the game enumerated the zone in - which was the
        /// old behaviour, and is not a reason for anything.
        /// </summary>
        private static ActorAffordances[] RankByAttention(NarrativeWorldState world, LocalAffordanceProfile profile)
        {
            int count = profile.Actors.Count;
            ActorAffordances[] ranked = new ActorAffordances[count];
            int[] attention = new int[count];
            ulong[] tieBreak = new ulong[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                ActorAffordances observer = profile.Actors[i];
                ranked[i] = observer;
                attention[i] = PettyTheftPressure.CanFillSocialTheftRole(observer) ? Attention(observer) : int.MinValue;
                tieBreak[i] = new DeterministicRng(world.WorldSeed)
                    .Fork(PettyTheftSituation.ArchetypeId + "|witness|" + observer.ActorId.Value)
                    .NextUInt64();
                order[i] = i;
            }

            Array.Sort(
                order,
                (left, right) =>
                {
                    int byAttention = attention[right].CompareTo(attention[left]);
                    return byAttention != 0 ? byAttention : tieBreak[left].CompareTo(tieBreak[right]);
                });

            ActorAffordances[] result = new ActorAffordances[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = ranked[order[i]];
            }

            return result;
        }

        private static int Attention(ActorAffordances observer) =>
            observer.Attribute(VanillaAttribute.Perception) + observer.Skill(VanillaSkill.SpotHidden);

        /// <summary>
        /// Who, of the people already ranked, would have seen this particular theft - or nobody.
        ///
        /// A theft between two people alone is unwitnessed, which is an ordinary theft and not a
        /// degenerate case.
        /// </summary>
        private static ActorAffordances PickWitness(
            ActorAffordances[] ranked,
            ActorAffordances thief,
            ActorAffordances victim)
        {
            for (int i = 0; i < ranked.Length; i++)
            {
                ActorAffordances observer = ranked[i];
                if (PettyTheftPressure.CanFillSocialTheftRole(observer)
                    && observer.ActorId != thief.ActorId && observer.ActorId != victim.ActorId)
                {
                    return observer;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the world has already told this story, and what to say about it.
        ///
        /// Two sources, both of them history the world already keeps: a thread that has not finished
        /// with these people, and the ledger's own record of what happened recently. Nothing here
        /// maintains a second history of what has been generated - a parallel ledger that could
        /// disagree with the real one is exactly what the event ledger exists to prevent.
        /// </summary>
        private static string RepetitionReason(NarrativeWorldState world, SituationCandidate candidate, GameTime now)
        {
            EntityId actor = candidate.ActorIn(SituationRoles.Actor);
            EntityId target = candidate.ActorIn(SituationRoles.Target);

            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread.State == ThreadState.Resolved || thread.ArchetypeId != candidate.ArchetypeId)
                {
                    continue;
                }

                if (thread.ParticipantIds.Contains(actor) && thread.ParticipantIds.Contains(target))
                {
                    return "an unresolved " + candidate.ArchetypeId + " between "
                           + world.Registry.NameOf(actor) + " and " + world.Registry.NameOf(target)
                           + " already exists";
                }
            }

            foreach (WorldEvent past in world.Ledger.OfType(WorldEventType.Theft))
            {
                if (past.Actor != actor || past.Target != target)
                {
                    continue;
                }

                long days = now.DaysSince(past.Time);
                if (days <= RepetitionWindowDays)
                {
                    return world.Registry.NameOf(actor) + " was already recorded stealing from "
                           + world.Registry.NameOf(target) + " " + days + " day(s) ago";
                }
            }

            return null;
        }

        private static int CompareCandidates(SituationCandidate a, SituationCandidate b)
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0)
            {
                return score;
            }

            int actor = string.CompareOrdinal(
                a.ActorIn(SituationRoles.Actor).Value,
                b.ActorIn(SituationRoles.Actor).Value);
            if (actor != 0)
            {
                return actor;
            }

            int target = string.CompareOrdinal(
                a.ActorIn(SituationRoles.Target).Value,
                b.ActorIn(SituationRoles.Target).Value);
            if (target != 0)
            {
                return target;
            }

            ItemDescriptor left = a.ItemIn(SituationRoles.Stake);
            ItemDescriptor right = b.ItemIn(SituationRoles.Stake);
            return string.CompareOrdinal(
                left == null ? string.Empty : left.Id.Value,
                right == null ? string.Empty : right.Id.Value);
        }
    }
}
