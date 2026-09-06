using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// What one actor means to attempt: a registered verb, against somebody, about something, and
    /// the sentence from goal formation that produced it (BQ-093).
    ///
    /// The join between the two halves of the step, and it is deliberately thin. Selection - needs,
    /// values, emotion, problem-solving style, personal prohibitions, what the world affords -
    /// happens entirely before this exists and is none of resolution's business. Resolution is the
    /// shared library and knows nothing about motive. An intent is the handover, so neither half
    /// has to reach into the other.
    /// </summary>
    public sealed class ActionIntent
    {
        public ActionIntent(EntityId actor, string actionId, EntityId target, string because)
        {
            Actor = actor;
            ActionId = actionId ?? string.Empty;
            Target = target;
            Because = because ?? string.Empty;
        }

        public EntityId Actor { get; }

        /// <summary>A registered <see cref="NarrativeAction.Id"/>. Never a style label.</summary>
        public string ActionId { get; }

        public EntityId Target { get; }

        /// <summary>The goal-formation line this came out of, for the inspector.</summary>
        public string Because { get; }

        /// <summary>The matter the verb is about, when selection knew one.</summary>
        public EntityId SubjectFact { get; set; }

        public EntityId SubjectItem { get; set; }

        public NarrativeThread Thread { get; set; }

        /// <summary>
        /// The intent a goal-formation trace chose, or null when the chosen candidate is one no
        /// registered verb means.
        ///
        /// Null is a real answer and the honest one: a trace that chose "wait" or "accuse a rival"
        /// has decided something, and what it decided is not an attempt this library can carry.
        /// Substituting the best-scoring *attemptable* candidate here would silently make the
        /// actor do something they did not choose.
        /// </summary>
        public static ActionIntent FromGoalChoice(GoalFormationTrace trace, EntityId target)
        {
            GoalActionTrace chosen = trace?.ChosenAction;
            if (chosen == null || !chosen.IsAttemptable)
            {
                return null;
            }

            return new ActionIntent(trace.ActorId, chosen.RegisteredActionId, target, trace.Desire)
            {
                SubjectItem = trace.CandidateGoal != null ? trace.CandidateGoal.Subject : EntityId.None
            };
        }
    }

    /// <summary>
    /// One attempt, kept together with the verdicts that produced it, so an inspector can show
    /// intention, availability and resolution as one line of causality.
    ///
    /// A record, not a resolver. It decides nothing: <see cref="ActionAttempt.Run"/> is the same
    /// three calls the player's own surface makes - look the verb up in the registry, ask it
    /// whether it is available, perform it - written once so that "the NPC took the same path"
    /// is a fact about the code rather than a claim in a document. There is no NPC branch in it
    /// and there must never be one.
    /// </summary>
    public sealed class ActionAttempt
    {
        private ActionAttempt(ActionIntent intent, NarrativeAction action, Availability availability, ActionOutcome outcome, string refusal)
        {
            Intent = intent;
            Action = action;
            Availability = availability;
            Outcome = outcome;
            Refusal = refusal ?? string.Empty;
        }

        public ActionIntent Intent { get; }

        /// <summary>The registered verb, or null when the registry has no such id.</summary>
        public NarrativeAction Action { get; }

        public Availability Availability { get; }

        /// <summary>Null when the attempt never got as far as resolving.</summary>
        public ActionOutcome Outcome { get; }

        /// <summary>Why nothing happened, or empty when something did.</summary>
        public string Refusal { get; }

        public bool Resolved => Outcome != null;

        /// <summary>
        /// Looks the intent's verb up, asks it whether it applies, and performs it if it does.
        ///
        /// Exactly what <c>TheftLaboratory.Perform</c> and the live drama surface do for the
        /// player, and by construction the only path either actor kind has: the availability
        /// question is the verb's, the roll is the shared resolver's, and the consequences go into
        /// the same ledger through the verb's own <see cref="NarrativeAction.Perform"/>.
        /// </summary>
        public static ActionAttempt Run(ActionRegistry registry, ActionIntent intent, ActionContext context)
        {
            if (registry == null || intent == null || context == null)
            {
                return new ActionAttempt(intent, null, Availability.NotRelevant("nothing to attempt"), null, "nothing to attempt");
            }

            NarrativeAction action = registry.Get(intent.ActionId);
            if (action == null)
            {
                string missing = "no registered verb with id '" + intent.ActionId + "'";
                return new ActionAttempt(intent, null, Availability.Impossible(missing), null, missing);
            }

            Availability availability = action.GetAvailability(context);
            if (!availability.IsAvailable)
            {
                return new ActionAttempt(intent, action, availability, null, availability.Reason);
            }

            return new ActionAttempt(intent, action, availability, action.Perform(context), null);
        }

        /// <summary>The whole chain as text: intention, verb, verdict, roll, and what was recorded.</summary>
        public string Explain()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("intent: ").Append(Intent == null ? "(none)" : Intent.ActionId);
            if (Intent != null && Intent.Because.Length > 0)
            {
                sb.Append(" because ").Append(Intent.Because);
            }

            sb.Append("\n  availability: ").Append(Availability);
            if (Action != null)
            {
                sb.Append("\n  actor scope: ").Append(Action.ActorScope);
                sb.Append("\n  ").Append(Action.Embodiment.Describe());
            }

            if (Outcome != null)
            {
                sb.Append("\n  ").Append(Outcome.Explain().Replace("\n", "\n  "));
            }
            else
            {
                sb.Append("\n  nothing resolved: ").Append(Refusal);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Builds the <see cref="ActionContext"/> an attempt runs in, for whoever is acting.
    ///
    /// The player's surfaces have always done this by hand, and they could, because a conversation
    /// guarantees the two people are in the same room and the room is the one the game is running.
    /// Nothing guarantees that for anybody else, so the same construction has to be asked for
    /// rather than assumed - which is the actual work of "NPCs use the same resolver": not a
    /// second resolver, a correctly built context.
    ///
    /// Where the actor is comes from the BQ-135 activity snapshot, the one seam read for transient
    /// vanilla activity, so this adds no private probe into a `Chara` of its own. Nothing here is
    /// kept: the snapshot is held for this one construction and asked for again next time.
    /// </summary>
    public static class ActorContexts
    {
        /// <summary>
        /// A context for <paramref name="actor"/> acting on <paramref name="target"/>, or a
        /// refusal saying which of the four requirements failed.
        ///
        /// Fails closed, and the closed direction is the physical one. An actor whose whereabouts
        /// nothing answered has no zone to draw witnesses from, and an actor vanilla is currently
        /// carrying between zones is not anywhere at all - `VS 3.3` makes both part of whether an
        /// actor is available to a BQ intention, and inventing a place for either would be
        /// exactly the fabricated physical detail `VS 3.2` and `D021` forbid. A movement reading
        /// of <see cref="VanillaMovement.Unknown"/> is not a refusal: on every build where the
        /// travel facets are unread, everything would be refused and the honest reading of an
        /// unread facet is not "moving".
        /// </summary>
        public static bool TryBuild(
            NarrativeWorldState world,
            IVanillaState vanilla,
            ICheckResolver checks,
            DeterministicRng rng,
            EntityId actor,
            EntityId target,
            out ActionContext context,
            out string refusal)
        {
            context = null;

            if (world == null || vanilla == null || checks == null || rng == null)
            {
                refusal = "no world to act in";
                return false;
            }

            if (actor.IsNone || !vanilla.IsAlive(actor))
            {
                refusal = "the actor is not somebody the game can answer for";
                return false;
            }

            ActorActivity activity = vanilla.GetActorActivity(actor);
            if (activity.VanillaMovementState() == VanillaMovement.Moving)
            {
                refusal = "vanilla is already carrying them between zones";
                return false;
            }

            // The snapshot's zone is the seam's own `GetZoneOf` answer in the live adapter, so
            // reading it here cannot disagree with anything; the fallback is the same read on a
            // build that answers no activity facets at all.
            EntityId zone = activity.CurrentZone.IsNone ? vanilla.GetZoneOf(actor) : activity.CurrentZone;
            if (zone.IsNone)
            {
                refusal = "nothing answered where they are";
                return false;
            }

            if (!target.IsNone)
            {
                if (!vanilla.IsAlive(target) || world.Absences.IsPhysicallyAbsent(target))
                {
                    refusal = "the other party is not here to be acted on";
                    return false;
                }

                if (vanilla.GetZoneOf(target) != zone)
                {
                    refusal = "they are not in the same place";
                    return false;
                }
            }

            ActionContext built = new ActionContext(world, vanilla, checks, rng, actor, target);
            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zone);
            for (int i = 0; present != null && i < present.Count; i++)
            {
                if (present[i] != actor && present[i] != target)
                {
                    built.Witnesses.Add(present[i]);
                }
            }

            context = built;
            refusal = string.Empty;
            return true;
        }
    }
}
