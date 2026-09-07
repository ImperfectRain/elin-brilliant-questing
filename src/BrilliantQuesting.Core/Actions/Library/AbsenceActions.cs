using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Withdraw from public reach over a debt or exposure without claiming a new physical
    /// location.
    ///
    /// This is a shared verb, not a scheme-only action. It exists because "flee the debt" is not
    /// a movement order in BQ-095: until BQ-097 proves travel steering, the safe expression is
    /// BQ-032's Grade A absence. The actor is still wherever Elin says they are; BQ records that
    /// their procedural service/social availability has gone quiet for a while.
    /// </summary>
    public sealed class GoToGroundAction : NarrativeAction
    {
        public const string AbsenceReason = "gone_to_ground";

        public GoToGroundAction() : base("go_to_ground", ActionFamily.Crime, "Go to ground")
        {
        }

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (context.ActorIsPlayer)
            {
                return Availability.NotRelevant("the player decides their own movements");
            }

            if (context.World.Absences.IsAbsent(context.Actor))
            {
                return Availability.NotRelevant("they are already out of ordinary reach");
            }

            Fact pressure = Pressure(context);
            if (pressure == null)
            {
                return Availability.NotRelevant("nothing here gives them a reason to go to ground");
            }

            return Availability.Available("withdraws from procedural availability without moving them");
        }

        protected override ActionOutcome PerformCore(ActionContext context)
        {
            Fact pressure = Pressure(context);
            if (pressure == null)
            {
                ActionOutcome empty = new ActionOutcome(Id, null, "There is nothing to duck.");
                empty.Notes.Add("no debt or exposure pressure in the binding");
                return empty;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Deception, context.Actor, context.Target)
                .WithModifier("leaving obligations unanswered", 2);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string pressureText = ActionSupport.Describe(context, pressure.Id);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    AbsenceLifecycle absences = new AbsenceLifecycle(context.World, context.Vanilla);
                    bool withdrawn = absences.TryWithdrawService(
                        context.Actor,
                        AbsenceReason,
                        context.Now.PlusDays(check.Outcome == CheckOutcome.CriticalPass ? 14 : 7));

                    ActionOutcome outcome = new ActionOutcome(
                        Id,
                        check,
                        withdrawn
                            ? context.NameOf(context.Actor) + " goes quiet over " + pressureText + "."
                            : context.NameOf(context.Actor) + " tries to go quiet, but cannot.");

                    if (withdrawn)
                    {
                        WorldEvent recorded = Latest(context.World, WorldEventType.WentAbsent, context.Actor);
                        if (recorded != null)
                        {
                            outcome.Events.Add(recorded);
                        }

                        outcome.Notes.Add("Grade A absence only: Elin keeps the actor's physical location");
                    }
                    else
                    {
                        outcome.Notes.Add("absence lifecycle refused the withdrawal");
                    }

                    return outcome;
                }

                case CheckOutcome.Fail:
                {
                    ActionOutcome outcome = new ActionOutcome(
                        Id,
                        check,
                        context.NameOf(context.Actor) + " cannot get away from " + pressureText + ".");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.PromiseBroken,
                        context.Actor,
                        context.Target,
                        context.Now,
                        0.35,
                        context.Zone,
                        related: new[] { pressure.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add("the obligation stayed live and the failure became history");
                    return outcome;
                }

                default:
                {
                    ActionOutcome outcome = new ActionOutcome(
                        Id,
                        check,
                        context.NameOf(context.Actor) + " is caught trying to duck " + pressureText + ".");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.DeceptionExposed,
                        context.Actor,
                        context.Target,
                        context.Now,
                        0.55,
                        context.Zone,
                        related: new[] { pressure.Id },
                        witnesses: ActionSupport.Bystanders(context, true),
                        threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add("caught trying to disappear; no physical location was asserted");
                    return outcome;
                }
            }
        }

        private static Fact Pressure(ActionContext context)
        {
            if (context == null || context.SubjectFact.IsNone)
            {
                return null;
            }

            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            if (fact == null || fact.Truth != TruthState.True)
            {
                return null;
            }

            if (fact.Predicate == FactPredicates.Owes && fact.Subject == context.Actor)
            {
                return fact;
            }

            if (fact.Subject == context.Actor && fact.Secrecy > 0)
            {
                return fact;
            }

            return null;
        }

        private static WorldEvent Latest(NarrativeWorldState world, WorldEventType type, EntityId actor)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                WorldEvent recorded = events[i];
                if (recorded.Type == type && recorded.Actor == actor)
                {
                    return recorded;
                }
            }

            return null;
        }
    }
}
