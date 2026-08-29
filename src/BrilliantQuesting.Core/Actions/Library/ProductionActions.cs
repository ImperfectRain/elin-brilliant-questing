using System.Collections.Generic;
using System.Globalization;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// What goods have to be, for somebody to accept them.
    ///
    /// A shortage is stated as a property constraint rather than as a named object, which is the
    /// whole of what makes it answerable by production: any thing that meets it answers it, and no
    /// thing that does not. That is also where the difficulty of the craft comes from - asking for
    /// bread and asking for bread a physician would give a fevered child are the same work at two
    /// standards, not two different crafts.
    /// </summary>
    public sealed class ProductionSpec
    {
        public ProductionSpec(string categoryTag, int minimumQuality = 0, int minimumValue = 0)
        {
            CategoryTag = string.IsNullOrWhiteSpace(categoryTag) ? "goods" : categoryTag.Trim();
            MinimumQuality = minimumQuality < 0 ? 0 : minimumQuality;
            MinimumValue = minimumValue < 0 ? 0 : minimumValue;
            _kind = new[] { CategoryTag };
        }

        /// <summary>The kind as the item matcher wants it. Held rather than rebuilt per call.</summary>
        private readonly string[] _kind;

        /// <summary>The kind of thing wanted: "food", "medicine", "timber".</summary>
        public string CategoryTag { get; }

        /// <summary>How well made it has to be. Zero means anything of the right kind will do.</summary>
        public int MinimumQuality { get; }

        /// <summary>What it has to be worth. Zero means worth does not enter into it.</summary>
        public int MinimumValue { get; }

        /// <summary>Whether this particular object satisfies the demand outright.</summary>
        public bool Accepts(ItemDescriptor item)
        {
            return ShortfallOf(item) == null;
        }

        /// <summary>Whether this is the right kind of thing, whatever its standard.</summary>
        public bool IsTheRightKind(ItemDescriptor item) => ActionSupport.LooksLike(item, _kind);

        /// <summary>Why this object will not do, or null when it will.</summary>
        public string ShortfallOf(ItemDescriptor item)
        {
            if (item == null)
            {
                return "there is nothing there";
            }

            if (!ActionSupport.LooksLike(item, _kind))
            {
                return item.Name + " is not " + CategoryTag;
            }

            if (item.Quality < MinimumQuality)
            {
                return item.Name + " is quality " + item.Quality + " and they will not take under " + MinimumQuality;
            }

            if (item.Value < MinimumValue)
            {
                return item.Name + " is worth " + item.Value + " and they will not take under " + MinimumValue;
            }

            return null;
        }

        public string Describe()
        {
            string text = CategoryTag;
            if (MinimumQuality > 0)
            {
                text += " of quality " + MinimumQuality + " or better";
            }

            if (MinimumValue > 0)
            {
                text += (MinimumQuality > 0 ? " and" : " of") + " worth " + MinimumValue + " or better";
            }

            return text;
        }

        /// <summary>
        /// The form a <see cref="FactPredicates.Needs"/> fact carries, and the form
        /// <see cref="Parse"/> reads back. Round-trips.
        /// </summary>
        public string ToFactValue()
        {
            string text = CategoryTag;
            if (MinimumQuality > 0)
            {
                text += " quality " + MinimumQuality.ToString(CultureInfo.InvariantCulture);
            }

            if (MinimumValue > 0)
            {
                text += " worth " + MinimumValue.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        /// <summary>
        /// Reads a specification off a fact value, tolerantly.
        ///
        /// A value that names only a kind is a demand with no threshold, which is a real and
        /// common thing to want; anything unparseable after the kind is ignored rather than
        /// throwing, because a malformed demand should cost its thresholds, not the whole route.
        /// Returns null only when there is no kind at all to work with.
        /// </summary>
        public static ProductionSpec Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] words = value.Trim().Split(' ');
            string category = words[0];
            int quality = 0;
            int worth = 0;

            for (int i = 1; i < words.Length - 1; i++)
            {
                if (words[i] == "quality")
                {
                    int.TryParse(words[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out quality);
                }
                else if (words[i] == "worth")
                {
                    int.TryParse(words[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out worth);
                }
            }

            return new ProductionSpec(category, quality, worth);
        }
    }

    /// <summary>Shared demand lookups. A shortage is a fact in the graph, not a quest flag.</summary>
    internal static class ProductionDemand
    {
        /// <summary>
        /// The open demand this person has, or null.
        ///
        /// Scoped exactly as <see cref="Debt.FindPayable"/> is - an explicitly named fact, then the
        /// thread's own facts - and deliberately not a walk of the whole fact store. Every verb in
        /// this family asks this question on the discovery pass, so the cost of asking it is paid
        /// six times over whenever the game decides what can be attempted here.
        /// </summary>
        public static Fact Find(ActionContext context, out ProductionSpec spec)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsOpenDemand(context, named, out spec))
                {
                    return named;
                }
            }

            if (context.Thread != null)
            {
                for (int i = 0; i < context.Thread.FactIds.Count; i++)
                {
                    Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                    if (IsOpenDemand(context, fact, out spec))
                    {
                        return fact;
                    }
                }
            }

            spec = null;
            return null;
        }

        private static bool IsOpenDemand(ActionContext context, Fact fact, out ProductionSpec spec)
        {
            spec = null;
            if (fact == null
                || fact.Predicate != FactPredicates.Needs
                || fact.Truth != TruthState.True
                || fact.Subject != context.Target)
            {
                return false;
            }

            spec = ProductionSpec.Parse(fact.Value);
            return spec != null;
        }

        /// <summary>Whether anything in this thread is still wanted.</summary>
        public static bool AnyOpenIn(NarrativeThread thread, KnowledgeGraph knowledge)
        {
            if (thread == null)
            {
                return false;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Making something somebody is short of, out of what is actually in your pack.
    ///
    /// The order of preference is the point of the step. If the actor is already carrying goods
    /// that meet the demand, they hand those over and *nothing is rolled*: Elin's own cooking,
    /// brewing and building already decided what came out and how good it is, and a procedural
    /// layer that rolled its own dice over the top of that would be inventing a second, worse
    /// crafting mechanic and then disagreeing with the first. The check exists only for the other
    /// case - raw stock and a standard to hit - and it is the same class of work, so it is the
    /// same class here rather than a second verb.
    ///
    /// The demanded quality is load-bearing twice over. It decides whether a finished thing is
    /// accepted at all, and it sets both the difficulty of working from stock and how much stock
    /// the work eats. A shoddy pie is not a bad roll against a physician's demand; it is the wrong
    /// object, and the verb says so rather than offering a route that could never land.
    /// </summary>
    public abstract class ProductionAction : NarrativeAction
    {
        protected ProductionAction(string id, string label, CheckProfile profile, string[] answers, string[] stock)
            : base(id, ActionFamily.Crafting, label)
        {
            Profile = profile;
            Answers = answers;
            Stock = stock;
        }

        protected CheckProfile Profile { get; }

        /// <summary>
        /// The kinds of demand this craft can answer. Empty answers any of them, which is what
        /// makes `craft_to_property` the generalist rather than a seventh named craft.
        /// </summary>
        protected string[] Answers { get; }

        /// <summary>
        /// The kinds of thing this craft can work from. Empty works from anything carried, which
        /// is what makes the generalist a true superset of the named crafts rather than a seventh
        /// one with its own gaps - a distinction the display tiers lean on.
        /// </summary>
        protected string[] Stock { get; }

        /// <summary>What the thread is called once this verb has ended it.</summary>
        protected virtual string Resolution => "need_met";

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody here is short of anything");
            }

            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null)
            {
                return Availability.NotRelevant("nobody here has asked for anything");
            }

            if (!CanAnswer(spec))
            {
                return Availability.NotRelevant("this is not work that produces " + spec.CategoryTag);
            }

            ItemDescriptor finished = FindFinished(context, spec);
            if (finished != null && context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Available("you are carrying " + finished.Name + ", and it will do");
            }

            if (!context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("materials cannot be used up on this build");
            }

            int needed = StockNeeded(spec);
            List<ItemDescriptor> stock = FindStock(context, needed);
            if (stock.Count < needed)
            {
                return Availability.Impossible(Shortfall(context, spec, stock.Count, needed));
            }

            return Availability.Available("works " + needed + " of what you are carrying into " + spec.Describe());
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null || !CanAnswer(spec))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "Nobody here wants that made.");
                nothing.Notes.Add("no open demand this craft answers");
                return nothing;
            }

            ItemDescriptor finished = FindFinished(context, spec);
            if (finished != null && context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return HandOver(context, demand, spec, finished);
            }

            return WorkFromStock(context, demand, spec);
        }

        /// <summary>
        /// The branch with no roll in it. The object exists, the game decided what it is, and all
        /// that is left is whether it meets the standard - which was settled before this was
        /// called.
        /// </summary>
        private ActionOutcome HandOver(ActionContext context, Fact demand, ProductionSpec spec, ItemDescriptor goods)
        {
            if (!context.Vanilla.TryTransferItem(goods.Id, context.Actor, context.Target))
            {
                ActionOutcome refused = new ActionOutcome(Id, null, "The " + goods.Name + " is not where you thought it was.");
                refused.Notes.Add("hand-over refused: " + goods.Id + " left the actor's keeping");
                return refused;
            }

            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You hand over the " + goods.Name + ". " + context.NameOf(context.Target) + " takes it without argument.");
            outcome.Notes.Add("no check: the game already decided what the " + goods.Name + " is (quality " + goods.Quality + ")");
            Settle(context, demand, spec, outcome, 0.8);
            return outcome;
        }

        /// <summary>
        /// The branch that is a craft. Difficulty and appetite both come from the standard being
        /// asked for, so demanding work is harder *and* eats more of what you brought.
        /// </summary>
        private ActionOutcome WorkFromStock(ActionContext context, Fact demand, ProductionSpec spec)
        {
            int needed = StockNeeded(spec);
            List<ItemDescriptor> stock = FindStock(context, needed + 1);
            if (stock.Count < needed || !context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                ActionOutcome empty = new ActionOutcome(Id, null, "You have nothing to make it out of.");
                empty.Notes.Add("stock on hand " + stock.Count + ", needed " + needed);
                return empty;
            }

            CheckRequest request = new CheckRequest(Profile, context.Actor, EntityId.None);
            request.WithModifier("the standard they set", spec.MinimumQuality / 4);
            request.WithModifier("and what it must be worth", spec.MinimumValue / 200);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            int consumed;
            switch (check.Outcome)
            {
                // Nothing is wasted, so the work costs one less than it should have.
                case CheckOutcome.CriticalPass:
                    consumed = needed > 1 ? needed - 1 : 1;
                    break;

                // The ruined batch takes the rest of what was out on the bench with it.
                case CheckOutcome.CriticalFail:
                    consumed = needed + 1 <= stock.Count ? needed + 1 : stock.Count;
                    break;

                default:
                    consumed = needed;
                    break;
            }

            List<string> spent = Consume(context, stock, consumed);
            if (check.Outcome.IsSuccess())
            {
                ActionOutcome made = new ActionOutcome(Id, check,
                    "You work " + Listed(spent) + " into " + spec.Describe() + " and put it in "
                    + context.NameOf(context.Target) + "'s hands.");
                if (check.Outcome == CheckOutcome.CriticalPass)
                {
                    made.Notes.Add("nothing wasted: the work took " + consumed + " where " + needed + " was expected");
                }

                Settle(context, demand, spec, made, check.Outcome == CheckOutcome.CriticalPass ? 0.9 : 0.7);
                return made;
            }

            bool ruinous = check.Outcome == CheckOutcome.CriticalFail;
            ActionOutcome failed = new ActionOutcome(Id, check, ruinous
                ? "The batch turns on you, and takes " + Listed(spent) + " with it."
                : "It will not come right. " + Listed(spent) + " is wasted.");
            failed.Notes.Add("nothing produced; " + consumed + " used up and the demand still stands");
            return failed;
        }

        /// <summary>
        /// The demand is answered. Superseded rather than deleted, because a shortage that was
        /// filled is a thing that happened and the ledger has to be able to say when.
        /// </summary>
        private void Settle(ActionContext context, Fact demand, ProductionSpec spec, ActionOutcome outcome, double magnitude)
        {
            demand.Truth = TruthState.Superseded;
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                context.Target,
                context.Now,
                magnitude,
                context.Zone,
                related: new[] { demand.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome.Notes.Add(context.NameOf(context.Target) + " is no longer short of " + spec.Describe());

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                context.Thread.State = ThreadState.Resolved;
                context.Thread.Resolution = Resolution;
                outcome.Notes.Add("thread resolved: " + Resolution);
            }
        }

        private bool CanAnswer(ProductionSpec spec)
        {
            return Answers.Length == 0 || ActionSupport.LooksLike(spec.CategoryTag, Answers);
        }

        /// <summary>
        /// How much stock the work eats. Rises with the standard, so a demanding commission is
        /// expensive in materials as well as hard to get right.
        /// </summary>
        private static int StockNeeded(ProductionSpec spec)
        {
            return 1 + spec.MinimumQuality / 20;
        }

        /// <summary>Something already made that answers the demand outright, or null.</summary>
        private static ItemDescriptor FindFinished(ActionContext context, ProductionSpec spec)
        {
            return ActionSupport.FindItem(context, context.Actor, spec.Accepts);
        }

        /// <summary>
        /// Up to <paramref name="wanted"/> things this craft can work from, in carry order.
        ///
        /// Anything that would answer the demand outright is left out: eating the pie to make the
        /// pie is not a route, and a player who is carrying the answer should be offered the
        /// branch that hands it over.
        /// </summary>
        private List<ItemDescriptor> FindStock(ActionContext context, int wanted)
        {
            List<ItemDescriptor> found = new List<ItemDescriptor>();
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < carried.Count && found.Count < wanted; i++)
            {
                ItemDescriptor item = carried[i];
                if (item != null && (Stock.Length == 0 || ActionSupport.LooksLike(item, Stock)))
                {
                    found.Add(item);
                }
            }

            return found;
        }

        private List<string> Consume(ActionContext context, List<ItemDescriptor> stock, int count)
        {
            List<string> spent = new List<string>();
            for (int i = 0; i < stock.Count && spent.Count < count; i++)
            {
                if (context.Vanilla.TryDestroyItem(stock[i].Id, context.Actor))
                {
                    spent.Add(stock[i].Name);
                }
            }

            return spent;
        }

        private string Shortfall(ActionContext context, ProductionSpec spec, int have, int needed)
        {
            ItemDescriptor nearest = ActionSupport.FindItem(context, context.Actor, spec.IsTheRightKind);
            if (nearest != null)
            {
                return spec.ShortfallOf(nearest) + ", and you have "
                       + (have == 0 ? "nothing" : have.ToString(CultureInfo.InvariantCulture) + " of the " + needed)
                       + " to make better with";
            }

            return "you are carrying " + (have == 0 ? "nothing" : "only " + have) + " of the " + needed
                   + " this would take";
        }

        private static string Listed(List<string> names)
        {
            if (names.Count == 0)
            {
                return "nothing";
            }

            if (names.Count == 1)
            {
                return "the " + names[0];
            }

            return "the " + string.Join(", the ", names.ToArray());
        }
    }

    /// <summary>Feeding people. Elin's Cooking, applied to somebody who is short.</summary>
    public sealed class CookAction : ProductionAction
    {
        public CookAction() : base(
            "cook",
            "Cook for them",
            ProceduralCheckProfiles.Cookery,
            new[] { "food", "meal", "bread", "ration" },
            new[] { "food", "ingredient", "crop", "grain", "flour", "fish", "meat", "vegetable", "egg" })
        {
        }
    }

    /// <summary>Fermenting and distilling. Cooking and Alchemy meeting in the middle.</summary>
    public sealed class BrewAction : ProductionAction
    {
        public BrewAction() : base(
            "brew",
            "Brew for them",
            ProceduralCheckProfiles.Brewing,
            new[] { "drink", "ale", "wine", "cider", "tea" },
            new[] { "ingredient", "crop", "grain", "fruit", "herb", "water", "honey" })
        {
        }
    }

    /// <summary>
    /// Compounding a remedy. The making counterpart to identifying a substance, and the route a
    /// physician's demand is actually answered through.
    /// </summary>
    public sealed class AlchemyAction : ProductionAction
    {
        public AlchemyAction() : base(
            "alchemy",
            "Compound it yourself",
            ProceduralCheckProfiles.Compounding,
            new[] { "medicine", "potion", "remedy", "salve", "tonic" },
            new[] { "reagent", "herb", "seed", "powder", "vial", "flask", "ingredient" })
        {
        }
    }

    /// <summary>Raising something that has to stand up.</summary>
    public sealed class BuildAction : ProductionAction
    {
        public BuildAction() : base(
            "build",
            "Build it for them",
            ProceduralCheckProfiles.Construction,
            new[] { "timber", "furniture", "shelter", "structure", "tool" },
            new[] { "material", "wood", "log", "plank", "stone", "ore", "ingot", "nail" })
        {
        }

        protected override string Resolution => "built";
    }

    /// <summary>
    /// Making to a specification no named craft covers.
    ///
    /// The generalist exists so that the demand side can be generated freely - a ceremony wanting
    /// clothing, a guild wanting a blade above a standard - without every new kind of shortage
    /// needing a new verb. It answers anything and works from anything - which also makes it the
    /// route that is available wherever a named craft is - and pays for that in reading Handicraft
    /// rather than the specialist's own skill.
    /// </summary>
    public sealed class CraftToPropertyAction : ProductionAction
    {
        public CraftToPropertyAction() : base(
            "craft_to_property",
            "Make it to their specification",
            ProceduralCheckProfiles.Craftsmanship,
            new string[0],
            new string[0])
        {
        }

        protected override string Resolution => "commission_filled";
    }

    /// <summary>
    /// Putting the broken thing back into service.
    ///
    /// The other half of the family, and the one that answers a shortage by removing its cause
    /// rather than by covering it: a town short of flour because the mill is broken can be fed
    /// sack by sack, or the mill can be mended once. Both are this step's verbs, and the second is
    /// only available to somebody who can reach the thing itself - which is what keeps it a route
    /// through the world rather than a menu entry.
    ///
    /// The critical failure is the reason it is a decision. A botched repair does not leave the
    /// object where it was; it finishes it off, and the route that was there is gone.
    /// </summary>
    public sealed class RepairAction : NarrativeAction
    {
        private static readonly string[] Parts =
        {
            "material", "wood", "log", "plank", "stone", "ore", "ingot", "nail", "rope", "tool"
        };

        public RepairAction() : base("repair", ActionFamily.Crafting, "Repair it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            Fact damage = FindDamage(context, out ItemDescriptor broken, out EntityId _);
            if (damage == null)
            {
                return Availability.NotRelevant("there is nothing broken here you can get at");
            }

            if (!context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("materials cannot be used up on this build");
            }

            List<ItemDescriptor> parts = FindParts(context, 1);
            if (parts.Count < 1)
            {
                return Availability.Impossible("you have nothing to mend the " + broken.Name + " with");
            }

            return Availability.Available("uses what you are carrying on the " + broken.Name);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact damage = FindDamage(context, out ItemDescriptor broken, out EntityId holder);
            if (damage == null)
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here to mend.");
                nothing.Notes.Add("no reachable damaged object");
                return nothing;
            }

            List<ItemDescriptor> parts = FindParts(context, 2);
            if (parts.Count < 1 || !context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                ActionOutcome empty = new ActionOutcome(Id, null, "You have nothing to mend it with.");
                empty.Notes.Add("no materials on hand");
                return empty;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Repairs, context.Actor, EntityId.None);
            request.WithModifier("the size of the thing", broken.Value / 200);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            int consumed = check.Outcome == CheckOutcome.CriticalFail && parts.Count > 1 ? 2 : 1;
            for (int i = 0; i < consumed && i < parts.Count; i++)
            {
                context.Vanilla.TryDestroyItem(parts[i].Id, context.Actor);
            }

            EntityId owner = Ownership.OwnerOf(context, broken.Id);
            if (owner.IsNone)
            {
                owner = context.Target;
            }

            if (check.Outcome.IsSuccess())
            {
                return Mended(context, damage, broken, owner, check);
            }

            if (check.Outcome == CheckOutcome.CriticalFail)
            {
                return Finished(context, broken, holder, owner, check);
            }

            ActionOutcome failed = new ActionOutcome(Id, check,
                "You get nowhere with the " + broken.Name + ", and the parts are spent.");
            failed.Notes.Add("still broken; the damage fact stands");
            return failed;
        }

        /// <summary>
        /// Mending the cause closes what the cause was doing.
        ///
        /// A demand that names this object is a shortage *because of* it, so filling the demand
        /// and fixing the thing are the same result reached two ways - and the second one does not
        /// have to be repeated next month.
        /// </summary>
        private ActionOutcome Mended(ActionContext context, Fact damage, ItemDescriptor broken, EntityId owner, CheckResult check)
        {
            damage.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, check, "The " + broken.Name + " turns over again.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                owner,
                context.Now,
                check.Outcome == CheckOutcome.CriticalPass ? 0.9 : 0.7,
                context.Zone,
                related: new[] { damage.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                evidence: new[] { broken.Id },
                threadId: context.Thread?.Id ?? EntityId.None));

            int closed = CloseDemandsOn(context, broken.Id, outcome);
            outcome.Notes.Add(closed == 0
                ? "nobody was waiting on the " + broken.Name
                : closed + " shortage(s) the " + broken.Name + " was causing are over");

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                context.Thread.State = ThreadState.Resolved;
                context.Thread.Resolution = "cause_removed";
                outcome.Notes.Add("thread resolved: cause_removed");
            }

            return outcome;
        }

        /// <summary>The object does not survive the attempt, and neither does the route through it.</summary>
        private ActionOutcome Finished(ActionContext context, ItemDescriptor broken, EntityId holder, EntityId owner, CheckResult check)
        {
            bool gone = context.Vanilla.TryDestroyItem(broken.Id, holder);
            ActionOutcome outcome = new ActionOutcome(Id, check, gone
                ? "Something gives, and the " + broken.Name + " comes apart in your hands for good."
                : "You make the " + broken.Name + " worse than you found it.");

            if (!gone)
            {
                outcome.Notes.Add("still broken; the object survived the botch");
                return outcome;
            }

            outcome.Events.Add(context.World.Record(
                WorldEventType.Harmed,
                context.Actor,
                owner,
                context.Now,
                0.7,
                context.Zone,
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            context.World.Knowledge.RevokeProofOfItem(broken.Id);
            outcome.Notes.Add("the " + broken.Name + " is gone; nothing can be mended and nothing proved by it");
            return outcome;
        }

        private static int CloseDemandsOn(ActionContext context, EntityId cause, ActionOutcome outcome)
        {
            if (context.Thread == null)
            {
                return 0;
            }

            int closed = 0;
            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null
                    && fact.Predicate == FactPredicates.Needs
                    && fact.Truth == TruthState.True
                    && fact.Object == cause)
                {
                    fact.Truth = TruthState.Superseded;
                    outcome.Notes.Add("no longer wanted: " + ActionSupport.Describe(context, fact.Id));
                    closed++;
                }
            }

            return closed;
        }

        /// <summary>
        /// A broken thing the actor can actually put hands on.
        ///
        /// Reach is the same rule the examination verbs work by: your own pack, or the place you
        /// are standing in, and a restricted place only once it admits you. A mill on the far side
        /// of the valley is somebody's problem, not something a menu entry can fix.
        /// </summary>
        private static Fact FindDamage(ActionContext context, out ItemDescriptor broken, out EntityId holder)
        {
            broken = null;
            holder = EntityId.None;
            if (context.Thread == null && context.SubjectFact.IsNone)
            {
                return null;
            }

            List<EntityId> candidates = new List<EntityId>();
            if (!context.SubjectFact.IsNone)
            {
                candidates.Add(context.SubjectFact);
            }

            if (context.Thread != null)
            {
                candidates.AddRange(context.Thread.FactIds);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(candidates[i]);
                if (fact == null || fact.Predicate != FactPredicates.Damaged || fact.Truth != TruthState.True)
                {
                    continue;
                }

                ItemDescriptor found = Reach(context, fact.Subject, out EntityId where);
                if (found != null)
                {
                    broken = found;
                    holder = where;
                    return fact;
                }
            }

            return null;
        }

        private static ItemDescriptor Reach(ActionContext context, EntityId itemId, out EntityId holder)
        {
            holder = EntityId.None;
            ItemDescriptor carried = Find(context.Vanilla.GetInventory(context.Actor), itemId);
            if (carried != null)
            {
                holder = context.Actor;
                return carried;
            }

            NarrativeSite here = ActionSupport.SiteHere(context);
            if (here != null && !here.Admits(context.Actor))
            {
                return null;
            }

            ItemDescriptor inPlace = Find(context.Vanilla.GetInventory(context.Zone), itemId);
            if (inPlace != null)
            {
                holder = context.Zone;
            }

            return inPlace;
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

        private static List<ItemDescriptor> FindParts(ActionContext context, int wanted)
        {
            List<ItemDescriptor> found = new List<ItemDescriptor>();
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < carried.Count && found.Count < wanted; i++)
            {
                if (ActionSupport.LooksLike(carried[i], Parts))
                {
                    found.Add(carried[i]);
                }
            }

            return found;
        }
    }
}
