using System;
using System.Globalization;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Shared debt lookups. Debt is a fact in the graph, not an invisible quest flag.</summary>
    internal static class Debt
    {
        public static Fact FindPayable(ActionContext context, out int amount)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsPayable(context, named, out amount))
                {
                    return named;
                }
            }

            if (context.Thread != null)
            {
                for (int i = 0; i < context.Thread.FactIds.Count; i++)
                {
                    Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                    if (IsPayable(context, fact, out amount))
                    {
                        return fact;
                    }
                }
            }

            amount = 0;
            return null;
        }

        private static bool IsPayable(ActionContext context, Fact fact, out int amount)
        {
            amount = 0;
            if (fact == null
                || fact.Predicate != FactPredicates.Owes
                || fact.Truth != TruthState.True
                || fact.Object != context.Target
                || (!context.ThirdParty.IsNone && fact.Subject != context.ThirdParty)
                || (context.ThirdParty.IsNone && fact.Subject != context.Actor))
            {
                return false;
            }

            return TryParseOrens(fact.Value, out amount) && amount > 0;
        }

        private static bool TryParseOrens(string value, out int amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            int space = trimmed.IndexOf(' ');
            string number = space >= 0 ? trimmed.Substring(0, space) : trimmed;
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }
    }

    /// <summary>
    /// Settle a recorded debt with real money. No roll: if the debt exists and the player can
    /// cover it, the economic route works because Elin's money transfer worked.
    /// </summary>
    public sealed class PayDebtAction : NarrativeAction
    {
        public PayDebtAction() : base("pay_debt", ActionFamily.Economic, "Pay debt")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to pay");
            }

            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            Fact debt = Debt.FindPayable(context, out int amount);
            if (debt == null)
            {
                return Availability.NotRelevant("no payable debt here");
            }

            if (context.Vanilla.GetMoney(context.Actor) < amount)
            {
                return Availability.Impossible("you cannot pay " + amount + " orens you do not have");
            }

            return Availability.Available("costs " + amount + " orens");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact debt = Debt.FindPayable(context, out int amount);
            if (debt == null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is no debt here to settle.");
                refused.Notes.Add("no payable debt fact");
                return refused;
            }

            if (!context.Vanilla.TrySpendMoney(context.Actor, context.Target, amount))
            {
                ActionOutcome broke = new ActionOutcome(Id, null, "You cannot cover the debt.");
                broke.Notes.Add("payment failed: insufficient funds at resolution time");
                return broke;
            }

            debt.Truth = TruthState.Superseded;

            string debtor = debt.Subject == context.Actor ? "your" : context.NameOf(debt.Subject) + "'s";
            ActionOutcome outcome = new ActionOutcome(Id, null, "You pay " + amount + " orens and settle " + debtor + " debt with " + context.NameOf(context.Target) + ".");
            outcome.Events.Add(context.World.Record(
                WorldEventType.DebtPaid,
                context.Actor,
                context.Target,
                context.Now,
                0.8,
                context.Zone,
                related: new[] { debt.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));

            if (context.Thread != null)
            {
                context.Thread.State = ThreadState.Resolved;
                context.Thread.Resolution = "debt_paid";
                outcome.Notes.Add("thread resolved: debt paid");
            }

            outcome.Notes.Add("paid " + amount + " orens");
            return outcome;
        }
    }
}
