using System;
using System.Globalization;
using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;

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

            ActionSupport.Resolve(context, outcome, "debt_paid", 0.8);
            DistressedBusinessSituation.TryMarkSaved(context, debt.Id, outcome);

            outcome.Notes.Add("paid " + amount + " orens");
            return outcome;
        }
    }

    public sealed class BuyDistressedBusinessAction : NarrativeAction
    {
        public BuyDistressedBusinessAction() : base("buy_business", ActionFamily.Economic, "Buy the business")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            Fact debt = DistressedBusinessSituation.FindDebt(context, out int amount);
            if (debt == null)
            {
                return Availability.NotRelevant("no distressed business can be bought here");
            }

            int cost = amount * 2;
            return context.Vanilla.GetMoney(context.Actor) < cost
                ? Availability.Impossible("you cannot buy the business for " + cost + " orens you do not have")
                : Availability.Available("costs " + cost + " orens");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ActionOutcome outcome = new ActionOutcome(Id, null, "You buy out the failing business and its debt stops hanging over the counter.");
            if (!DistressedBusinessSituation.TryMarkBought(context, out int cost, outcome))
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is no buyout to make here.");
                refused.Notes.Add("no live distressed-business debt, business record, or payable funds at resolution time");
                return refused;
            }

            outcome.Notes.Add("spent " + cost + " orens");
            return outcome;
        }
    }

    /// <summary>
    /// Buy a failed shop back into service. The failure stays in history; this records the later,
    /// expensive attempt to make the counter usable again.
    /// </summary>
    public sealed class ReopenFailedBusinessAction : NarrativeAction
    {
        public ReopenFailedBusinessAction() : base("reopen_business", ActionFamily.Economic, "Reopen business")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            BusinessRecord business = DistressedBusinessSituation.FindFailedBusiness(context);
            Fact debt = DistressedBusinessSituation.FindDebtById(context, business?.CauseFactId ?? EntityId.None, out int amount);
            if (business == null || debt == null)
            {
                return Availability.NotRelevant("no failed business can be reopened here");
            }

            int cost = DistressedBusinessSituation.RecoveryCost(amount);
            return context.Vanilla.GetMoney(context.Actor) < cost
                ? Availability.Impossible("you cannot reopen the business for " + cost + " orens you do not have")
                : Availability.Available("costs " + cost + " orens and may still fail");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            BusinessRecord business = DistressedBusinessSituation.FindFailedBusiness(context);
            Fact debt = DistressedBusinessSituation.FindDebtById(context, business?.CauseFactId ?? EntityId.None, out int amount);
            if (business == null || debt == null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is no failed business here to reopen.");
                refused.Notes.Add("no failed distressed-business record tied to this thread");
                return refused;
            }

            int cost = DistressedBusinessSituation.RecoveryCost(amount);
            if (!context.Vanilla.TrySpendMoney(context.Actor, business.OperatorId, cost))
            {
                ActionOutcome broke = new ActionOutcome(Id, null, "You cannot cover the reopening stake.");
                broke.Notes.Add("payment failed for " + cost + " orens");
                return broke;
            }

            CheckRequest request = new CheckRequest(
                    ProceduralCheckProfiles.RecoveryInvestment,
                    context.Actor,
                    business.OperatorId)
                .WithModifier("failed business", 4);
            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome = new ActionOutcome(Id, check, check.Succeeded
                ? "You sink " + cost + " orens into the failed counter, and there is enough left to open the doors again."
                : "You sink " + cost + " orens into the failed counter, but it is not enough to bring the business back.");

            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                business.OperatorId,
                context.Now,
                check.Succeeded ? 0.7 : 0.25,
                business.PlaceId,
                related: new[] { debt.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome.Notes.Add("spent " + cost + " orens on a recovery attempt");

            if (check.Succeeded)
            {
                DistressedBusinessSituation.TryRecoverFailedBusiness(context, outcome, out int _);
            }

            return outcome;
        }
    }

    /// <summary>
    /// Buying goods that answer an open demand.
    ///
    /// This is not a shop simulator. Elin owns shops, prices and item generation; this verb only
    /// records the narrative act of spending real money to procure the kind of goods a standing
    /// shortage names. If the build cannot move money, or the player cannot cover the price, the
    /// route is impossible rather than a long shot.
    /// </summary>
    public sealed class BuySuppliesAction : NarrativeAction
    {
        public BuySuppliesAction() : base("buy_supplies", ActionFamily.Economic, "Buy supplies")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here is short of anything");
            }

            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null)
            {
                return Availability.NotRelevant("no open shortage to buy for");
            }

            int cost = ProcurementCost(spec);
            return context.Vanilla.GetMoney(context.Actor) < cost
                ? Availability.Impossible("you cannot spend " + cost + " orens you do not have")
                : Availability.Available("costs " + cost + " orens to procure " + spec.Describe());
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is no open shortage to buy for.");
                refused.Notes.Add("no needs fact for " + context.NameOf(context.Target));
                return refused;
            }

            int cost = ProcurementCost(spec);
            if (!context.Vanilla.TrySpendMoney(context.Actor, EntityId.None, cost))
            {
                ActionOutcome broke = new ActionOutcome(Id, null, "You cannot cover the purchase.");
                broke.Notes.Add("payment failed for " + cost + " orens");
                return broke;
            }

            demand.Truth = TruthState.Superseded;

            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You spend " + cost + " orens and buy " + spec.Describe() + " for " + context.NameOf(context.Target) + ".");
            ActionSupport.RelieveDemand(context, demand, spec, outcome, 25, 2);
            CloseTreatedTrouble(context, demand, spec, outcome);
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                context.Target,
                context.Now,
                0.65,
                context.Zone,
                related: new[] { demand.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome.Notes.Add(context.NameOf(context.Target) + " is no longer short of " + spec.Describe());

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                ActionSupport.Resolve(context, outcome, "supplies_bought", 0.65);
            }

            return outcome;
        }

        private static int ProcurementCost(ProductionSpec spec)
        {
            return 80 + spec.MinimumQuality * 5 + spec.MinimumValue / 2;
        }

        private static void CloseTreatedTrouble(ActionContext context, Fact demand, ProductionSpec spec, ActionOutcome outcome)
        {
            if (demand == null || spec == null || demand.Object.IsNone || !IsTreatment(spec.CategoryTag))
            {
                return;
            }

            Fact trouble = context.World.Knowledge.GetFact(demand.Object);
            if (trouble == null || trouble.Predicate != FactPredicates.Damaged || trouble.Truth != TruthState.True)
            {
                return;
            }

            trouble.Truth = TruthState.Superseded;
            outcome?.Notes.Add("treated: " + ActionSupport.Describe(context, trouble.Id));
        }

        private static bool IsTreatment(string category)
        {
            return Contains(category, "medicine")
                   || Contains(category, "remedy")
                   || Contains(category, "antibody")
                   || Contains(category, "cure");
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                   && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Put money into the supplier or tool whose failure is causing a shortage.
    ///
    /// Investment answers the cause rather than the symptom, like repair, but with coin instead
    /// of craft skill. It pays the owner of the failing thing and closes only demands that name
    /// that thing as their cause.
    /// </summary>
    public sealed class InvestInSupplierAction : NarrativeAction
    {
        public InvestInSupplierAction() : base("invest_in_supplier", ActionFamily.Economic, "Invest in the supplier")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            Fact damage = FindSupplierFailure(context, out EntityId cause, out EntityId owner);
            if (damage == null)
            {
                return Availability.NotRelevant("no supplier failure is causing this shortage");
            }

            if (!ActionSupport.Present(context, owner))
            {
                return Availability.NotRelevant("nobody here can take the investment");
            }

            int cost = InvestmentCost(context, cause);
            return context.Vanilla.GetMoney(context.Actor) < cost
                ? Availability.Impossible("you cannot invest " + cost + " orens you do not have")
                : Availability.Available("invests " + cost + " orens in " + context.NameOf(owner));
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact damage = FindSupplierFailure(context, out EntityId cause, out EntityId owner);
            if (damage == null)
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "There is no supplier failure to invest in.");
                refused.Notes.Add("no damaged cause with open dependent demand");
                return refused;
            }

            int cost = InvestmentCost(context, cause);
            if (!context.Vanilla.TrySpendMoney(context.Actor, owner, cost))
            {
                ActionOutcome broke = new ActionOutcome(Id, null, "The investment never leaves your purse.");
                broke.Notes.Add("payment failed for " + cost + " orens");
                return broke;
            }

            damage.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You put " + cost + " orens into " + context.NameOf(owner) + "'s failure, and the supply starts again.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                owner,
                context.Now,
                0.75,
                context.Zone,
                related: new[] { damage.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                evidence: cause.IsNone ? null : new[] { cause },
                threadId: context.Thread?.Id ?? EntityId.None));

            int closed = ActionSupport.CloseDemandsOn(context, cause, outcome);
            outcome.Notes.Add(closed + " shortage(s) ended by funding the cause");

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                ActionSupport.Resolve(context, outcome, "supplier_funded", 0.75);
            }

            return outcome;
        }

        private static Fact FindSupplierFailure(ActionContext context, out EntityId cause, out EntityId owner)
        {
            cause = EntityId.None;
            owner = EntityId.None;
            if (context.Thread == null)
            {
                return null;
            }

            HashSet<EntityId> openCauses = new HashSet<EntityId>();
            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact demand = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (demand != null
                    && demand.Predicate == FactPredicates.Needs
                    && demand.Truth == TruthState.True
                    && !demand.Object.IsNone)
                {
                    openCauses.Add(demand.Object);
                }
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact damage = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (damage == null
                    || damage.Predicate != FactPredicates.Damaged
                    || damage.Truth != TruthState.True
                    || !openCauses.Contains(damage.Subject))
                {
                    continue;
                }

                EntityId foundOwner = Ownership.OwnerOf(context, damage.Subject);
                if (foundOwner.IsNone)
                {
                    foundOwner = context.Target;
                }

                cause = damage.Subject;
                owner = foundOwner;
                return damage;
            }

            return null;
        }

        private static int InvestmentCost(ActionContext context, EntityId cause)
        {
            ItemDescriptor item = FindItem(context, cause);
            return 300 + (item == null ? 0 : item.Value / 3);
        }

        private static ItemDescriptor FindItem(ActionContext context, EntityId itemId)
        {
            if (itemId.IsNone)
            {
                return null;
            }

            foreach (EntityId holder in context.Thread.ParticipantIds)
            {
                ItemDescriptor held = Find(context.Vanilla.GetInventory(holder), itemId);
                if (held != null)
                {
                    return held;
                }
            }

            for (int i = 0; i < context.Thread.SiteIds.Count; i++)
            {
                ItemDescriptor placed = Find(context.Vanilla.GetInventory(context.Thread.SiteIds[i]), itemId);
                if (placed != null)
                {
                    return placed;
                }
            }

            return Find(context.Vanilla.GetInventory(context.Zone), itemId);
        }

        private static ItemDescriptor Find(IReadOnlyList<ItemDescriptor> inventory, EntityId itemId)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].Id == itemId)
                {
                    return inventory[i];
                }
            }

            return null;
        }
    }
}
