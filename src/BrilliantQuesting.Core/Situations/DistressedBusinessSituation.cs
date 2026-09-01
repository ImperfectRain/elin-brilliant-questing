using System.Globalization;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// A failing shop whose problem is a real debt and whose later continuity records the route
    /// the player chose: rescue, buyout, coercion, or no rescue at all.
    /// </summary>
    public sealed class DistressedBusinessSituation
    {
        public const string ArchetypeId = "distressed_business";

        private DistressedBusinessSituation()
        {
        }

        public NarrativeThread Thread { get; private set; }

        public EntityId OwnerId { get; private set; }

        public EntityId CreditorId { get; private set; }

        public EntityId BusinessId { get; private set; }

        public EntityId DebtFactId { get; private set; }

        public EntityId LeverageFactId { get; private set; }

        public EntityId MarketId { get; private set; }

        public int Amount { get; private set; }

        public static DistressedBusinessSituation Create(
            NarrativeWorldState world,
            ISituationStager stager,
            EntityId market,
            GameTime now,
            int amount = 1200)
        {
            DistressedBusinessSituation situation = new DistressedBusinessSituation
            {
                Amount = amount,
                MarketId = market,
                BusinessId = world.NewId("business")
            };

            if (world.Registry.GetSite(market) == null)
            {
                world.Registry.Add(new NarrativeSite(market, "Kell's Ford store", "market"));
            }

            NarrativeNpc owner = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Mira")
            {
                Occupation = "shopkeeper",
                Importance = NarrativeImportance.Known
            });
            NarrativeNpc creditor = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), "Haron")
            {
                Occupation = "merchant",
                Importance = NarrativeImportance.Known
            });
            creditor.Roles.Add(GuildNetworks.MerchantsRole);

            situation.OwnerId = owner.Id;
            situation.CreditorId = creditor.Id;

            stager.StageCharacter(owner.Id, new CharacterBlueprint(owner.Name)
            {
                Money = amount / 6
            }, market);
            stager.StageCharacter(creditor.Id, new CharacterBlueprint(creditor.Name)
            {
                Money = amount * 2
            }.With(VanillaSkill.Investing, 12), market);

            BusinessContinuity businesses = new BusinessContinuity(world);
            businesses.TryRegister(situation.BusinessId, market, owner.Id, now);

            WorldEvent origin = world.Record(
                WorldEventType.DebtCreated,
                owner.Id,
                creditor.Id,
                now,
                magnitude: 0.7,
                zone: market);

            Fact debt = new Fact(world.NewId("fact"), owner.Id, FactPredicates.Owes, creditor.Id, amount + " orens", TruthState.True, originEvent: origin.Id);
            world.Knowledge.AddFact(debt);
            world.Knowledge.Teach(owner.Id, debt.Id, KnowledgeSource.Participant, 1.0, now, true);
            world.Knowledge.Teach(creditor.Id, debt.Id, KnowledgeSource.Participant, 1.0, now, true);
            situation.DebtFactId = debt.Id;

            Fact leverage = new Fact(world.NewId("fact"), creditor.Id, FactPredicates.Funds, owner.Id, "off-ledger pressure loans", TruthState.True, secrecy: 65);
            world.Knowledge.AddFact(leverage);
            world.Knowledge.Teach(creditor.Id, leverage.Id, KnowledgeSource.Participant, 1.0, now, true);
            situation.LeverageFactId = leverage.Id;

            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), ArchetypeId, now)
            {
                Tension = 45,
                Importance = 45,
                State = ThreadState.Active,
                OriginEventId = origin.Id
            };
            thread.ParticipantIds.Add(owner.Id);
            thread.ParticipantIds.Add(creditor.Id);
            thread.FactIds.Add(debt.Id);
            thread.FactIds.Add(leverage.Id);
            thread.SiteIds.Add(market);
            thread.OpenQuestions.Add("Will " + owner.Name + "'s shop survive the debt?");
            thread.Escalation.Add(new EscalationStep("creditor_calls_note", 3, "The creditor calls in the debt."));
            thread.Escalation.Add(new EscalationStep("business_fails", 7, "The shop fails under the debt."));
            ArchetypeRecoveryRoutes.AddDistressedBusiness(thread);

            world.Threads.Add(thread);
            situation.Thread = thread;

            businesses.TryChangeState(situation.BusinessId, BusinessContinuityState.Struggling, now, debt.Id, owner.Id);
            return situation;
        }

        public static bool TryMarkSaved(ActionContext context, EntityId debtFactId, ActionOutcome outcome)
        {
            BusinessRecord business = FindBusiness(context, debtFactId);
            if (business == null)
            {
                return false;
            }

            bool changed = new BusinessContinuity(context.World).TryChangeState(
                business.BusinessId,
                BusinessContinuityState.Recovered,
                context.Now,
                debtFactId,
                context.Actor);
            if (changed)
            {
                outcome?.Notes.Add("business recovered after its debt was settled");
            }

            return changed;
        }

        public static bool TryMarkBought(ActionContext context, out int cost, ActionOutcome outcome)
        {
            cost = 0;
            Fact debt = FindDebt(context, out int amount);
            BusinessRecord business = FindBusiness(context, debt?.Id ?? EntityId.None);
            if (debt == null || business == null)
            {
                return false;
            }

            cost = amount * 2;
            if (!context.Vanilla.TrySpendMoney(context.Actor, debt.Object, cost))
            {
                return false;
            }

            debt.Truth = TruthState.Superseded;
            new BusinessContinuity(context.World).TryChangeState(
                business.BusinessId,
                BusinessContinuityState.BoughtOut,
                context.Now,
                debt.Id,
                context.Actor,
                replacementOperatorId: context.Actor);

            outcome?.Events.Add(context.World.Record(
                WorldEventType.DebtPaid,
                context.Actor,
                debt.Object,
                context.Now,
                0.9,
                context.Zone,
                related: new[] { debt.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome?.Notes.Add("business bought out for " + cost + " orens");
            ActionSupport.Resolve(context, outcome, "business_bought", 0.9);
            return true;
        }

        public static bool TryMarkExtorted(ActionContext context, ActionOutcome outcome)
        {
            Fact debt = FindDebt(context, out int _);
            BusinessRecord business = FindBusiness(context, debt?.Id ?? EntityId.None);
            if (debt == null || business == null || debt.Object != context.Target)
            {
                return false;
            }

            debt.Truth = TruthState.Superseded;
            bool changed = new BusinessContinuity(context.World).TryChangeState(
                business.BusinessId,
                BusinessContinuityState.Extorted,
                context.Now,
                debt.Id,
                context.Actor);
            if (changed)
            {
                outcome?.Notes.Add("creditor backs off; the shop remains under coercive pressure");
                ActionSupport.Resolve(context, outcome, "business_extorted", 0.7);
            }

            return changed;
        }

        public static bool TryRecoverFailedBusiness(ActionContext context, ActionOutcome outcome, out int cost)
        {
            cost = 0;
            BusinessRecord business = FindFailedBusiness(context);
            Fact debt = FindDebtById(context, business?.CauseFactId ?? EntityId.None, out int amount);
            if (business == null || debt == null)
            {
                return false;
            }

            cost = RecoveryCost(amount);
            bool changed = new BusinessContinuity(context.World).TryChangeState(
                business.BusinessId,
                BusinessContinuityState.Recovered,
                context.Now,
                debt.Id,
                context.Actor);
            if (changed)
            {
                outcome?.Notes.Add("failed business reopened after a recovery investment");
            }

            return changed;
        }

        public static int RecoveryCost(int amount)
        {
            return amount <= 0 ? 0 : amount * 3;
        }

        internal static Fact FindDebt(ActionContext context, out int amount)
        {
            amount = 0;
            if (context?.Thread == null || context.Thread.ArchetypeId != ArchetypeId)
            {
                return null;
            }

            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsLiveDebtInThisBusiness(context, named, out amount))
                {
                    return named;
                }
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (IsLiveDebtInThisBusiness(context, fact, out amount))
                {
                    return fact;
                }
            }

            return null;
        }

        internal static BusinessRecord FindFailedBusiness(ActionContext context)
        {
            if (context?.Thread == null || context.Thread.ArchetypeId != ArchetypeId)
            {
                return null;
            }

            foreach (BusinessRecord record in context.World.Businesses.Records)
            {
                if (record.State == BusinessContinuityState.Failed && context.Thread.FactIds.Contains(record.CauseFactId))
                {
                    return record;
                }
            }

            return null;
        }

        internal static Fact FindDebtById(ActionContext context, EntityId debtFactId, out int amount)
        {
            amount = 0;
            if (context?.Thread == null || context.Thread.ArchetypeId != ArchetypeId || debtFactId.IsNone)
            {
                return null;
            }

            Fact fact = context.World.Knowledge.GetFact(debtFactId);
            return IsDebtInThisBusiness(context, fact, out amount) ? fact : null;
        }

        private static bool IsLiveDebtInThisBusiness(ActionContext context, Fact fact, out int amount)
        {
            amount = 0;
            if (fact == null
                || fact.Predicate != FactPredicates.Owes
                || fact.Truth != TruthState.True
                || (!context.Target.IsNone && fact.Object != context.Target)
                || (!context.ThirdParty.IsNone && fact.Subject != context.ThirdParty))
            {
                return false;
            }

            return TryParseOrens(fact.Value, out amount) && amount > 0;
        }

        private static bool IsDebtInThisBusiness(ActionContext context, Fact fact, out int amount)
        {
            amount = 0;
            if (fact == null
                || fact.Predicate != FactPredicates.Owes
                || (!context.Target.IsNone && fact.Subject != context.Target)
                || (!context.ThirdParty.IsNone && fact.Object != context.ThirdParty))
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

        private static BusinessRecord FindBusiness(ActionContext context, EntityId debtFactId)
        {
            if (context?.Thread == null || context.Thread.ArchetypeId != ArchetypeId || debtFactId.IsNone)
            {
                return null;
            }

            foreach (BusinessRecord record in context.World.Businesses.Records)
            {
                if (record.CauseFactId == debtFactId)
                {
                    return record;
                }
            }

            return null;
        }
    }

    public sealed class DistressedBusinessEscalation : IThreadEscalationHandler
    {
        public void Apply(NarrativeWorldState world, NarrativeThread thread, EscalationStep step, GameTime now)
        {
            if (world == null || thread == null || step == null || step.Id != "business_fails")
            {
                return;
            }

            EntityId cause = thread.FactIds.Count > 0 ? thread.FactIds[0] : EntityId.None;
            foreach (BusinessRecord business in world.Businesses.Records)
            {
                if (business.CauseFactId == cause)
                {
                    new BusinessContinuity(world).TryChangeState(
                        business.BusinessId,
                        BusinessContinuityState.Failed,
                        now,
                        cause,
                        business.OperatorId);
                    ThreadResolution.Resolve(world, thread, "business_failed", business.OperatorId, now, 0.8, business.PlaceId);
                    return;
                }
            }
        }
    }
}
