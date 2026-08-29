using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// What happens to the theft if the player never turns up.
    ///
    /// This is the part that decides whether the mod is a quest generator or a world: the victim
    /// asks around, the thief stops carrying the evidence, the witness eventually says something,
    /// and an unprovable suspicion turns into an accusation and then a feud. A player who arrives
    /// on day twelve walks into a different problem than one who arrives on day one, and neither
    /// of them was punished by a timer.
    /// </summary>
    public sealed class PettyTheftEscalation : IThreadEscalationHandler
    {
        private readonly IVanillaState _vanilla;
        private readonly RumorSystem _rumors;
        private readonly RumorDistortion _distortion;

        public PettyTheftEscalation(IVanillaState vanilla, RumorSystem rumors, RumorDistortion distortion = null)
        {
            _vanilla = vanilla;
            _rumors = rumors;
            _distortion = distortion ?? new RumorDistortion();
        }

        public void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now)
        {
            Roles roles = Roles.Resolve(world, thread);
            if (roles == null)
            {
                return;
            }

            switch (step.Id)
            {
                case "victim_asks_around":
                    thread.Tension += 10;
                    world.Record(WorldEventType.ThreadEscalated, roles.Victim, EntityId.None, now, 0.3, threadId: thread.Id);
                    break;

                case "thief_hides_it":
                    HideTheEvidence(world, roles, now, thread);
                    break;

                case "witness_talks":
                    // Hearsay, and it arrives without proof - which is exactly why the victim's
                    // next move is an accusation they cannot support.
                    //
                    // The dead say nothing. `RumorSystem.Tell` is a primitive that knows about
                    // belief and not about mortality, and this caller never asked: a witness the
                    // player killed on day one still tipped the victim off on day seven, which is
                    // the same lying-dialogue failure `SceneStatus` exists to prevent, arriving
                    // through escalation instead of through presentation.
                    if (_vanilla.IsAlive(roles.Witness) && _rumors.Tell(roles.Witness, roles.Victim, roles.TheftFactId, now))
                    {
                        thread.Tension += 15;
                    }

                    break;

                case "thief_deflects":
                    Deflect(world, roles, now, thread);
                    break;

                case "accusation":
                    Accuse(world, roles, now, thread);
                    break;

                case "feud":
                    Feud(world, roles, now, thread);
                    break;
            }
        }

        /// <summary>
        /// The thief stops carrying it. Pickpocketing stops being an option and searching gets
        /// harder - the cheap route closes because the player took too long, not because a
        /// designer decided day four was the deadline.
        /// </summary>
        private void HideTheEvidence(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            Fact theft = world.Knowledge.GetFact(roles.TheftFactId);
            if (theft == null || theft.EvidenceIds.Count == 0)
            {
                return;
            }

            EntityId itemId = theft.EvidenceIds[0];
            EntityId stash = thread.SiteIds.Count > 0 ? thread.SiteIds[0] : EntityId.None;

            if (_vanilla.Supports(VanillaCapability.TransferItems) && !stash.IsNone
                && _vanilla.TryTransferItem(itemId, roles.Thief, stash))
            {
                theft.Secrecy = 80;
                world.Record(WorldEventType.ThreadEscalated, roles.Thief, EntityId.None, now, 0.4, stash, new[] { theft.Id }, threadId: thread.Id);
            }
        }

        /// <summary>
        /// The thief blames somebody else, if he is the sort who would and the story has got far
        /// enough that saying nothing has stopped working.
        ///
        /// This is the deliberate half of BQ-020, and it is not the same thing as a rumour going
        /// wrong on its own: a garbled retelling is nobody's fault, while this is a person who
        /// knows exactly what happened choosing to say otherwise. The world records both the claim
        /// and the lie, so the truth is still there to be found.
        ///
        /// Whether he does it at all is his own honesty. An honest thief - one who stole out of
        /// need rather than habit - takes what is coming instead, which is a different and better
        /// situation than the one where every culprit lies.
        /// </summary>
        private void Deflect(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            NarrativeNpc thief = world.Registry.GetNpc(roles.Thief);
            if (thief == null || !_vanilla.IsAlive(roles.Thief) || !_vanilla.IsAlive(roles.Victim))
            {
                return;
            }

            double willingness = 1.0 - thief.Personality.Honesty;
            if (willingness < 0.5 || !world.Knowledge.Knows(roles.Victim, roles.TheftFactId))
            {
                // Nobody is looking at him yet, or he is not the sort. Lying before there is
                // anything to lie about is how an NPC starts behaving like a plot device.
                return;
            }

            Fact theft = world.Knowledge.GetFact(roles.TheftFactId);
            Fact blamed = _distortion.Blame(world, _vanilla, theft, roles.Thief, roles.Victim, world.Rng);
            if (blamed == null)
            {
                return;
            }

            if (_rumors.Lie(roles.Thief, roles.Victim, roles.TheftFactId, blamed.Id, now, willingness))
            {
                thread.Tension += 10;
                thread.OpenQuestions.Add(world.Registry.NameOf(roles.Thief) + " says it was "
                                         + world.Registry.NameOf(blamed.Subject) + ".");
            }
        }

        private void Accuse(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            Fact believed = MostBelievedVersion(world, roles.Victim, roles.TheftFactId);
            if (believed == null)
            {
                // Nobody told them anything they could use. The world moves on without an answer -
                // and the thread stays in the database, so it can be reopened years later.
                thread.State = ThreadState.Dormant;
                thread.Resolution = "unsolved";
                thread.OpenQuestions.Add("Nobody ever found out who took it.");
                return;
            }

            // Who they name is who they believe did it, which is not always who did.
            EntityId accused = believed.Subject;
            bool canProve = world.Knowledge.CanProve(roles.Victim, believed.Id);

            // FalseAccusation means the claim is untrue, not that it could not be demonstrated.
            // Recording it for an unprovable-but-correct accusation would put a lie in the
            // Chronicle every time somebody was right without evidence, and circulation would
            // then distribute the error - which is precisely what this step exists to model
            // properly rather than to fake.
            WorldEventType kind = believed.IsUntrue
                ? WorldEventType.FalseAccusation
                : canProve ? WorldEventType.CrimeReported : WorldEventType.AccusationMade;

            world.Record(kind, roles.Victim, accused, now, 0.7, related: new[] { believed.Id }, threadId: thread.Id);

            // Being accused is itself information - but only the person who actually did it
            // learns anything about the theft from being named. Teaching an innocent that they
            // are a participant in a crime they did not commit would make the graph agree with
            // the rumour.
            if (accused == roles.Thief)
            {
                world.Knowledge.Teach(roles.Thief, roles.TheftFactId, KnowledgeSource.Participant, 1.0, now, true);
            }
            else
            {
                thread.OpenQuestions.Add(world.Registry.NameOf(accused) + " had nothing to do with it.");
            }

            thread.Tension += 20;
        }

        /// <summary>
        /// The version of the story this person actually holds - the true one, or whichever
        /// garbled retelling of it they believe most - provided they believe it strongly enough
        /// to act. Ties go to the truth, because somebody equally sure of two things has no
        /// reason to prefer the wrong one.
        /// </summary>
        private static Fact MostBelievedVersion(NarrativeWorldState world, EntityId knower, EntityId trueFactId)
        {
            Fact best = null;
            double bestConfidence = 0.3;

            if (world.Knowledge.TryGetBelief(knower, trueFactId, out KnowledgeRecord truth) && truth.Confidence >= bestConfidence)
            {
                best = world.Knowledge.GetFact(trueFactId);
                bestConfidence = truth.Confidence;
            }

            foreach (Fact fact in world.Knowledge.Facts.Values)
            {
                if (fact.DistortionOf != trueFactId)
                {
                    continue;
                }

                if (world.Knowledge.TryGetBelief(knower, fact.Id, out KnowledgeRecord belief)
                    && belief.Confidence > bestConfidence)
                {
                    best = fact;
                    bestConfidence = belief.Confidence;
                }
            }

            return best;
        }

        /// <summary>
        /// The households stop speaking - and it is the household he named that he stops speaking
        /// to, which is not always the one that took anything.
        ///
        /// Reading the accusation back out of the ledger rather than recomputing who the victim
        /// believes: what happened is what happened, and by day fourteen he may well believe
        /// something different from what he stood up and said on day ten. A feud with somebody he
        /// never accused would be a consequence with no cause, which is the failure standing rule
        /// 20 names.
        /// </summary>
        private void Feud(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            EntityId accused = WhoWasAccused(world, thread, roles.Victim);
            if (accused.IsNone || !thread.CompletedSteps.Contains("accusation") || thread.Resolution == "unsolved")
            {
                return;
            }

            world.Relationships.ConnectMutual(roles.Victim, accused, RelationKind.Enemy, -70);
            world.Record(WorldEventType.ThreadEscalated, roles.Victim, accused, now, 0.6, threadId: thread.Id);
            thread.OpenQuestions.Add(world.Registry.NameOf(roles.Victim) + " and "
                                     + world.Registry.NameOf(accused) + " will not be in the same room again.");
        }

        private static EntityId WhoWasAccused(NarrativeWorldState world, NarrativeThread thread, EntityId accuser)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                WorldEvent worldEvent = events[i];
                if (worldEvent.ThreadId != thread.Id || worldEvent.Actor != accuser)
                {
                    continue;
                }

                if (worldEvent.Type == WorldEventType.FalseAccusation
                    || worldEvent.Type == WorldEventType.AccusationMade
                    || worldEvent.Type == WorldEventType.CrimeReported)
                {
                    return worldEvent.Target;
                }
            }

            return EntityId.None;
        }

        /// <summary>
        /// Recovers who is who from the graph rather than from list order, so that a thread which
        /// gains participants later still resolves its roles correctly.
        /// </summary>
        private sealed class Roles
        {
            public EntityId Victim;
            public EntityId Thief;
            public EntityId Witness;
            public EntityId TheftFactId;

            public static Roles Resolve(NarrativeWorldState world, NarrativeThread thread)
            {
                Roles roles = new Roles();
                for (int i = 0; i < thread.FactIds.Count; i++)
                {
                    Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                    if (fact == null)
                    {
                        continue;
                    }

                    if (fact.Predicate == FactPredicates.Stole)
                    {
                        roles.TheftFactId = fact.Id;
                        roles.Thief = fact.Subject;
                    }
                    else if (fact.Predicate == FactPredicates.Possesses)
                    {
                        roles.Victim = fact.Subject;
                    }
                }

                if (roles.Thief.IsNone || roles.Victim.IsNone || roles.TheftFactId.IsNone)
                {
                    return null;
                }

                for (int i = 0; i < thread.ParticipantIds.Count; i++)
                {
                    EntityId participant = thread.ParticipantIds[i];
                    if (participant != roles.Thief && participant != roles.Victim)
                    {
                        roles.Witness = participant;
                        break;
                    }
                }

                return roles;
            }
        }
    }
}
