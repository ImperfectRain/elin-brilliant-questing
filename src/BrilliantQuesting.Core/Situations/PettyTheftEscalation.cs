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

        public PettyTheftEscalation(IVanillaState vanilla, RumorSystem rumors)
        {
            _vanilla = vanilla;
            _rumors = rumors;
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
                    _rumors.Tell(roles.Witness, roles.Victim, roles.TheftFactId, now);
                    thread.Tension += 15;
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

        private void Accuse(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            bool victimBelieves = world.Knowledge.BelievesConfidently(roles.Victim, roles.TheftFactId, 0.3);
            if (!victimBelieves)
            {
                // Nobody told them anything they could use. The world moves on without an answer -
                // and the thread stays in the database, so it can be reopened years later.
                thread.State = ThreadState.Dormant;
                thread.Resolution = "unsolved";
                thread.OpenQuestions.Add("Nobody ever found out who took it.");
                return;
            }

            bool canProve = world.Knowledge.CanProve(roles.Victim, roles.TheftFactId);
            world.Record(
                canProve ? WorldEventType.CrimeReported : WorldEventType.FalseAccusation,
                roles.Victim,
                roles.Thief,
                now,
                0.7,
                threadId: thread.Id);

            // Being accused is itself information: the thief now knows the story is out.
            world.Knowledge.Teach(roles.Thief, roles.TheftFactId, KnowledgeSource.Participant, 1.0, now, true);
            thread.Tension += 20;
        }

        private void Feud(NarrativeWorldState world, Roles roles, GameTime now, NarrativeThread thread)
        {
            bool accused = thread.CompletedSteps.Contains("accusation") && thread.Resolution != "unsolved";
            if (!accused)
            {
                return;
            }

            world.Relationships.ConnectMutual(roles.Victim, roles.Thief, RelationKind.Enemy, -70);
            world.Record(WorldEventType.ThreadEscalated, roles.Victim, roles.Thief, now, 0.6, threadId: thread.Id);
            thread.OpenQuestions.Add("The two of them will not be in the same room again.");
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
