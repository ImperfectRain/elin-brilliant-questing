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
    /// <summary>
    /// Take something out of a real inventory.
    ///
    /// The important half is not the item, it is the witness list: a clean lift creates no
    /// knowledge at all, and a botched one hands the whole room a provable fact about you.
    /// </summary>
    public sealed class PickpocketAction : NarrativeAction
    {
        public PickpocketAction() : base("pickpocket", ActionFamily.Crime, "Pick their pocket")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to steal from");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (SelectTarget(context) == null)
            {
                return Availability.NotRelevant("they are carrying nothing worth taking");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = SelectTarget(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Pickpocketing, context.Actor, context.Target);

            // Heavier, more valuable things are harder to palm - and a crowd is a complication.
            request.WithModifier("value of the item", item.Value / 500);
            request.WithModifier("onlookers", context.Witnesses.Count > 1 ? context.Witnesses.Count - 1 : 0);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    bool taken = context.Vanilla.TryTransferItem(item.Id, context.Target, context.Actor);
                    // Nobody saw it. The theft is real, it is recorded, and no one knows.
                    outcome = new ActionOutcome(Id, check, taken
                        ? "You lift the " + item.Name + " without anyone noticing."
                        : "Your fingers close on nothing.");
                    if (taken)
                    {
                        Fact theft = RecordTheftFact(context, item);
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.Theft, context.Actor, context.Target, context.Now, 0.5, context.Zone,
                            new[] { theft.Id }, evidence: new[] { item.Id }, tags: new[] { EventTags.Unnoticed }));
                        outcome.Notes.Add("no witnesses: nobody in the world knows this happened");
                    }

                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "You think better of it halfway through. They shift, suspicious.");
                    break;

                default:
                {
                    // Caught in the act, in front of whoever is standing there.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    Fact caught = RecordTheftFact(context, item);
                    outcome = new ActionOutcome(Id, check, "Your hand is caught in their pocket in front of everyone.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Theft, context.Actor, context.Target, context.Now, 0.8, context.Zone, new[] { caught.Id }, seen, new[] { item.Id }));
                    outcome.Events.Add(context.World.Record(WorldEventType.CrimeWitnessed, context.Actor, context.Target, context.Now, 0.8, context.Zone, new[] { caught.Id }, seen));
                    outcome.Notes.Add(seen.Count + " witness(es) can now prove it");
                    break;
                }
            }

            return outcome;
        }

        private static Fact RecordTheftFact(ActionContext context, ItemDescriptor item)
        {
            Fact fact = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Stole, item.Id, item.Name, TruthState.True, secrecy: 0);
            fact.EvidenceIds.Add(item.Id);
            context.World.Knowledge.AddFact(fact);
            return fact;
        }

        private static ItemDescriptor SelectTarget(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(context.Target);
            ItemDescriptor best = null;
            for (int i = 0; i < inventory.Count; i++)
            {
                ItemDescriptor item = inventory[i];
                if (!context.SubjectItem.IsNone)
                {
                    if (item.Id == context.SubjectItem)
                    {
                        return item;
                    }

                    continue;
                }

                if (best == null || item.Value > best.Value)
                {
                    best = item;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Put something of someone else's where a third party will be blamed for it.
    ///
    /// Requires an actual object in your actual inventory. The fact it creates is flagged False,
    /// so the world knows the accusation is a lie even while every character in it believes the
    /// opposite - which is what lets the truth come out later.
    /// </summary>
    public sealed class PlantEvidenceAction : NarrativeAction
    {
        public PlantEvidenceAction() : base("frame", ActionFamily.Crime, "Plant it on someone")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.ThirdParty.IsNone)
            {
                return Availability.NotRelevant("nobody chosen to take the blame");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (FindPlantable(context) == null)
            {
                return Availability.Impossible("you are not carrying anything that would incriminate them");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = FindPlantable(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Fabrication, context.Actor, context.ThirdParty)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: false));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string patsy = context.NameOf(context.ThirdParty);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    context.Vanilla.TryTransferItem(item.Id, context.Actor, context.ThirdParty);

                    Fact lie = FalseClaimForPlantedItem(context, item);
                    lie.EvidenceIds.Add(item.Id);
                    context.World.Knowledge.AddFact(lie);
                    context.World.Knowledge.Teach(context.Actor, lie.Id, KnowledgeSource.Document, 0.95, context.Now, true);

                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    for (int i = 0; i < seen.Count; i++)
                    {
                        context.World.Knowledge.Teach(seen[i], lie.Id, KnowledgeSource.Witnessed, 0.9, context.Now, true);
                    }

                    outcome = new ActionOutcome(Id, check, "The " + item.Name + " is in " + patsy + "'s belongings now.");
                    outcome.Notes.Add("created a fact flagged False: the world knows this is a frame even though nobody in it does");
                    outcome.Events.Add(context.World.Record(WorldEventType.EvidenceCreated, context.Actor, context.ThirdParty, context.Now, 0.7, context.Zone, new[] { lie.Id }, seen, new[] { item.Id }));
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "You cannot get near enough to place it.");
                    break;

                default:
                {
                    // Seen doing it. Now the provable fact is about you.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome = new ActionOutcome(Id, check, "You are seen slipping the " + item.Name + " into " + patsy + "'s things.");
                    outcome.Events.Add(context.World.Record(WorldEventType.FalseAccusation, context.Actor, context.ThirdParty, context.Now, 0.8, context.Zone, witnesses: seen, evidence: new[] { item.Id }));
                    break;
                }
            }

            return outcome;
        }

        private static ItemDescriptor FindPlantable(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < inventory.Count; i++)
            {
                if (context.SubjectItem.IsNone || inventory[i].Id == context.SubjectItem)
                {
                    return inventory[i];
                }
            }

            return null;
        }

        private static Fact FalseClaimForPlantedItem(ActionContext context, ItemDescriptor item)
        {
            foreach (Fact fact in context.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.Stole
                    && fact.Object == item.Id
                    && fact.Truth == TruthState.True
                    && fact.Subject != context.ThirdParty)
                {
                    return new Fact(
                        context.World.NewId("fact"),
                        context.ThirdParty,
                        FactPredicates.Stole,
                        item.Id,
                        fact.Value ?? item.Name,
                        TruthState.False);
                }
            }

            return new Fact(
                context.World.NewId("fact"),
                context.ThirdParty,
                FactPredicates.Possesses,
                item.Id,
                item.Name,
                TruthState.False);
        }
    }

    /// <summary>
    /// Get at what a place keeps that somebody else holds the key to.
    ///
    /// The verb that makes a locked room a problem rather than a wall. It is deliberately open to
    /// everybody - a lawful character is perfectly capable of jemmying a shutter, and hiding the
    /// option from them would be the "low odds are a reason to hide it" mistake in a different
    /// coat. What being lawful costs is not the attempt; it is that the attempt is a crime, and
    /// this is the one verb in the family where the ordinary case leaves no trace at all and the
    /// bad one puts a witness on you.
    ///
    /// It grants reach, not location. Elin decides where the player's body is and would be right
    /// to; what breaking in changes is whether the strongbox in the corner is open, which is what
    /// <see cref="SearchForEvidenceAction"/> then reads.
    /// </summary>
    public sealed class TrespassAction : NarrativeAction
    {
        public TrespassAction() : base("trespass", ActionFamily.Crime, "Let yourself in")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            NarrativeSite site = ActionSupport.SiteHere(context);
            if (site == null || !site.Restricted)
            {
                return Availability.NotRelevant("there is nothing shut to you here");
            }

            if (site.Admits(context.Actor))
            {
                return Availability.NotRelevant("you already have the run of the place");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            NarrativeSite site = ActionSupport.SiteHere(context);
            if (site == null || site.Admits(context.Actor))
            {
                ActionOutcome open = new ActionOutcome(Id, null, "Nothing here is shut to you.");
                open.Notes.Add("no restricted site at " + context.Zone);
                return open;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Burglary, context.Actor, EntityId.None)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: false));
            request.WithModifier("people about", context.Witnesses.Count);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    site.Admit(context.Actor);
                    outcome = new ActionOutcome(Id, check, "It opens for you, and closes behind you as though it never did.");
                    outcome.Notes.Add("no event recorded: nothing about " + site.Name + " will ever say you were here");
                    break;

                case CheckOutcome.Pass:
                    site.Admit(context.Actor);
                    outcome = new ActionOutcome(Id, check, "The lock gives, and you have the run of " + site.Name + ".");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.Trespass, context.Actor, site.ControllingOrganizationId, context.Now, 0.4, context.Zone,
                        tags: new[] { EventTags.Unnoticed }, threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add("in, unseen; what " + site.Name + " keeps is now within reach");
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "It will not give, and you are not going to force it quietly.");
                    outcome.Notes.Add("nothing recorded; the lock is unchanged and can be tried again");
                    break;

                default:
                {
                    // In anyway. A critical failure that simply refused would be the least
                    // interesting thing that can happen to a burglar.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    site.Admit(context.Actor);
                    outcome = new ActionOutcome(Id, check, "You are inside - and you were watched going in.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.Trespass, context.Actor, site.ControllingOrganizationId, context.Now, 0.6, context.Zone,
                        witnesses: seen, threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.CrimeWitnessed, context.Actor, site.ControllingOrganizationId, context.Now, 0.6, context.Zone,
                        witnesses: seen, threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add("in, but " + seen.Count + " witness(es) can place you here");
                    break;
                }
            }

            return outcome;
        }
    }

    /// <summary>
    /// Taking a real object out of the world, and what that does to what can still be shown.
    ///
    /// Two verbs share this. Burning the note that proves you did it and jamming the winch a
    /// carter depends on are the same physical act - an object stops existing - and they differ
    /// only in what you were pointing at and what the world makes of it afterwards. Keeping them
    /// as one implementation is the same argument the examination verbs make: the moment they stop
    /// sharing a body they start disagreeing about what destroying something means.
    ///
    /// Belief is never touched. Somebody who watched you can still stand up and say so after the
    /// ring is melted; what goes is the ability to point at the thing.
    /// </summary>
    public abstract class DestructiveAction : NarrativeAction
    {
        protected DestructiveAction(string id, string label, CheckProfile profile)
            : base(id, ActionFamily.Crime, label)
        {
            Profile = profile;
        }

        protected CheckProfile Profile { get; }

        /// <summary>What to say when this verb has nothing it could be pointed at.</summary>
        protected abstract string NothingToBreak { get; }

        /// <summary>Whose keeping this verb reaches into.</summary>
        protected abstract EntityId HolderOf(ActionContext context);

        /// <summary>
        /// The object this verb would take out of the world, or null.
        ///
        /// Each verb chooses its own, because "what is worth breaking" and "what proves something"
        /// are answered by different queries against different stores, and the second one is the
        /// expensive one. Must be side-effect free: the discovery pass calls it for every
        /// registered action.
        /// </summary>
        protected abstract ItemDescriptor Selected(ActionContext context);

        /// <summary>What history calls this, once the thing is gone.</summary>
        protected abstract WorldEventType RecordedAs { get; }

        protected abstract string Narrate(ActionContext context, ItemDescriptor item);

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("nothing can be destroyed for good on this build");
            }

            return Selected(context) == null
                ? Availability.NotRelevant(NothingToBreak)
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = Selected(context);
            if (item == null)
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here for you to do that to.");
                nothing.Notes.Add(NothingToBreak);
                return nothing;
            }

            EntityId holder = HolderOf(context);
            CheckRequest request = new CheckRequest(Profile, context.Actor, EntityId.None);
            request.WithModifier("people about", context.Witnesses.Count);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            if (check.Outcome == CheckOutcome.Fail)
            {
                ActionOutcome balked = new ActionOutcome(Id, check, "You cannot get at the " + item.Name + " without making more of a scene than it is worth.");
                balked.Notes.Add("nothing destroyed; the object is where it was");
                return balked;
            }

            if (!context.Vanilla.TryDestroyItem(item.Id, holder))
            {
                ActionOutcome refused = new ActionOutcome(Id, check, "The " + item.Name + " is not where you thought it was.");
                refused.Notes.Add("destruction refused: " + context.NameOf(holder) + " is not holding " + item.Id);
                return refused;
            }

            bool seen = check.Outcome == CheckOutcome.CriticalFail;
            IReadOnlyList<EntityId> witnesses = ActionSupport.Bystanders(context, seen);
            ActionOutcome outcome = new ActionOutcome(Id, check, Narrate(context, item)
                + (seen ? " Somebody was watching you do it." : string.Empty));

            outcome.Events.Add(context.World.Record(
                RecordedAs, context.Actor, holder, context.Now, seen ? 0.7 : 0.5, context.Zone,
                witnesses: witnesses, evidence: new[] { item.Id },
                tags: seen ? null : new[] { EventTags.Unnoticed },
                threadId: context.Thread?.Id ?? EntityId.None));

            StripProof(context, item, outcome);
            if (seen)
            {
                outcome.Events.Add(context.World.Record(
                    WorldEventType.CrimeWitnessed, context.Actor, holder, context.Now, 0.7, context.Zone,
                    witnesses: witnesses, threadId: context.Thread?.Id ?? EntityId.None));
                outcome.Notes.Add(witnesses.Count + " witness(es) saw the " + item.Name + " destroyed");
            }

            return outcome;
        }

        /// <summary>
        /// Every claim that rested on this object loses its physical leg, for everybody.
        ///
        /// Unlike selling it, this is not "you no longer have it" - the thing is gone, so nobody
        /// can produce it, and the guard who was going to be shown it cannot be shown it either.
        /// </summary>
        private static void StripProof(ActionContext context, ItemDescriptor item, ActionOutcome outcome)
        {
            int stripped = 0;
            foreach (Fact fact in context.World.Knowledge.FactsEvidencedBy(new[] { item.Id }))
            {
                outcome.Notes.Add("no longer provable by this object: " + ActionSupport.Describe(context, fact.Id));
                stripped++;
            }

            context.World.Knowledge.RevokeProofOfItem(item.Id);
            if (stripped == 0)
            {
                outcome.Notes.Add("the object proved nothing; nobody's case changes");
            }
        }
    }

    /// <summary>
    /// Burn what could be shown.
    ///
    /// Reaches only into your own pack, which is the constraint that makes it a move rather than a
    /// spell: to destroy the thing that proves it, you first have to be holding it, and the ways
    /// of coming to hold it are the rest of the library.
    /// </summary>
    public sealed class DestroyEvidenceAction : DestructiveAction
    {
        public DestroyEvidenceAction()
            : base("destroy_evidence", "Get rid of it", ProceduralCheckProfiles.CoveringTracks)
        {
        }

        protected override string NothingToBreak => "you are carrying nothing that proves anything";

        protected override EntityId HolderOf(ActionContext context) => context.Actor;

        /// <summary>
        /// The first thing in the pack that substantiates something, in carry order.
        ///
        /// The pack is asked about once rather than once per object. Testing each item on its own
        /// would walk the whole fact store for every thing the player is carrying, on the path the
        /// game runs to decide what can be attempted here - which is the cost
        /// <see cref="KnowledgeGraph.FactsEvidencedBy"/> takes a set to avoid.
        /// </summary>
        protected override ItemDescriptor Selected(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            Dictionary<EntityId, int> order = new Dictionary<EntityId, int>();
            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i] != null && !order.ContainsKey(carried[i].Id))
                {
                    order[carried[i].Id] = i;
                }
            }

            int best = int.MaxValue;
            foreach (Fact evidenced in context.World.Knowledge.FactsEvidencedBy(order.Keys))
            {
                for (int i = 0; i < evidenced.EvidenceIds.Count; i++)
                {
                    if (!order.TryGetValue(evidenced.EvidenceIds[i], out int index) || index >= best)
                    {
                        continue;
                    }

                    // A named object is the one meant, and is never quietly swapped for another.
                    if (context.SubjectItem.IsNone || evidenced.EvidenceIds[i] == context.SubjectItem)
                    {
                        best = index;
                    }
                }
            }

            return best == int.MaxValue ? null : carried[best];
        }

        protected override WorldEventType RecordedAs => WorldEventType.EvidenceDestroyed;

        protected override string Narrate(ActionContext context, ItemDescriptor item)
        {
            return "The " + item.Name + " is gone, and there is nothing left to show anybody.";
        }
    }

    /// <summary>
    /// Break the thing somebody is relying on.
    ///
    /// Reaches into somebody else's keeping for the first thing there worth anything, or for the
    /// object the caller named. The mod does not model machinery: a cart, a winch and a still are
    /// all, at the seam, an object somebody owns, and inventing a private vocabulary of breakable
    /// apparatus would be a second description of a world the game already describes.
    /// </summary>
    public sealed class SabotageAction : DestructiveAction
    {
        public SabotageAction()
            : base("sabotage", "Break something of theirs", ProceduralCheckProfiles.Sabotage)
        {
        }

        protected override string NothingToBreak => "they have nothing here worth breaking";

        protected override EntityId HolderOf(ActionContext context) => context.Target;

        protected override ItemDescriptor Selected(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Target, item => item.Value > 0);
        }

        protected override WorldEventType RecordedAs => WorldEventType.Harmed;

        protected override string Narrate(ActionContext context, ItemDescriptor item)
        {
            return "The " + item.Name + " will not be any use to " + context.NameOf(context.Target) + " again.";
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here to do that to");
            }

            return base.GetAvailability(context);
        }
    }

    /// <summary>
    /// Name your price for staying quiet.
    ///
    /// The canonical impossible precondition from `PM 62` lives here: blackmail without leverage
    /// is not a hard attempt, it is not an attempt. Leverage means something true about them,
    /// which they know about themselves, and which they were keeping quiet - all three, because
    /// squeezing somebody over a thing they do not believe they did is an accusation, and
    /// <see cref="ExposeSecretAction"/> is already the verb for that.
    ///
    /// What it buys is money and an enemy. The target learns, in every branch that lands, exactly
    /// who has this over them - which is the cost that stops it being free income.
    /// </summary>
    public sealed class ExtortAction : NarrativeAction
    {
        /// <summary>What a secret is worth per point of how hard it was kept.</summary>
        private const int OrensPerSecrecyPoint = 20;

        public ExtortAction() : base("extort", ActionFamily.Crime, "Name your price")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target) || context.Target == context.Actor)
            {
                return Availability.NotRelevant("nobody here to squeeze");
            }

            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            if (FindLeverage(context) == null)
            {
                return Availability.Impossible("you have nothing on them");
            }

            if (context.Vanilla.GetMoney(context.Target) <= 0)
            {
                return Availability.Impossible(context.NameOf(context.Target) + " has nothing to pay you with");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact leverage = FindLeverage(context);
            if (leverage == null)
            {
                ActionOutcome empty = new ActionOutcome(Id, null, "You have nothing to hold over them.");
                empty.Notes.Add("no leverage: nothing true, secret and theirs that you know of");
                return empty;
            }

            bool canProve = context.World.Knowledge.CanProve(context.Actor, leverage.Id);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Extortion, context.Actor, context.Target)
                .With(SituationalModifiers.Grudge(context))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: true));

            // The same term `expose` uses, and for the same reason: what decides whether a threat
            // works is whether it could be carried out.
            request.WithModifier(canProve ? "you can prove it" : "your word alone", canProve ? -6 : 4);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    int asked = Price(context, leverage, check.Outcome);
                    bool paid = asked > 0 && context.Vanilla.TrySpendMoney(context.Target, context.Actor, asked);
                    outcome = new ActionOutcome(Id, check, paid
                        ? who + " counts out " + asked + " orens and does not look at you while doing it."
                        : who + " agrees to your price and cannot raise it.");

                    if (paid)
                    {
                        Fact squeeze = new Fact(
                            context.World.NewId("fact"), context.Actor, FactPredicates.Extorted, context.Target,
                            asked + " orens", TruthState.True, secrecy: 70);
                        context.World.Knowledge.AddFact(squeeze);
                        context.World.Knowledge.Teach(context.Actor, squeeze.Id, KnowledgeSource.Participant, 1.0, context.Now, false);
                        context.World.Knowledge.Teach(context.Target, squeeze.Id, KnowledgeSource.Participant, 1.0, context.Now, true);

                        outcome.Events.Add(context.World.Record(
                            WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.7, context.Zone,
                            new[] { squeeze.Id, leverage.Id }, threadId: context.Thread?.Id ?? EntityId.None));
                        outcome.Notes.Add("took " + asked + " orens over: " + ActionSupport.Describe(context, leverage.Id));
                    }
                    else
                    {
                        outcome.Notes.Add("agreed, but the money was not there; nothing changed hands");
                    }

                    // Whether they paid or not, they now know precisely who holds this.
                    ActionSupport.WarnUnderInvestigation(
                        context, context.Target, context.Actor, outcome,
                        note: who + " knows exactly what you have on them, and who has it");
                    DistressedBusinessSituation.TryMarkExtorted(context, outcome);
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " hears you out and tells you to do your worst.");
                    outcome.Notes.Add(canProve
                        ? "they would rather be exposed than pay you"
                        : "they do not believe you could make it stick");
                    break;

                default:
                {
                    // The threat is made in front of people, and it is the threat that is now the
                    // provable thing.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome = new ActionOutcome(Id, check, who + " says it loudly enough that the room hears what you just asked for.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.8, context.Zone,
                        new[] { leverage.Id }, seen, threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.CrimeWitnessed, context.Actor, context.Target, context.Now, 0.8, context.Zone,
                        witnesses: seen, threadId: context.Thread?.Id ?? EntityId.None));
                    ActionSupport.WarnUnderInvestigation(
                        context, context.Target, context.Actor, outcome,
                        note: who + " knows what you have on them, and so does everybody who was standing there");
                    break;
                }
            }

            return outcome;
        }

        private static int Price(ActionContext context, Fact leverage, CheckOutcome outcome)
        {
            int asked = leverage.Secrecy * OrensPerSecrecyPoint;
            if (outcome == CheckOutcome.CriticalPass)
            {
                asked *= 2;
            }

            int available = context.Vanilla.GetMoney(context.Target);
            return asked < available ? asked : available;
        }

        /// <summary>
        /// Something true, about them, that they know and were keeping.
        ///
        /// Walked in a stable order and resolved to the most damaging candidate rather than the
        /// first one the fact store happens to yield, so the same world quotes the same price twice.
        /// </summary>
        private static Fact FindLeverage(ActionContext context)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                return IsLeverage(context, named) ? named : null;
            }

            Fact best = null;
            foreach (Fact fact in context.World.Knowledge.Facts.Values)
            {
                if (!IsLeverage(context, fact))
                {
                    continue;
                }

                if (best == null
                    || fact.Secrecy > best.Secrecy
                    || (fact.Secrecy == best.Secrecy && string.CompareOrdinal(fact.Id.Value, best.Id.Value) < 0))
                {
                    best = fact;
                }
            }

            return best;
        }

        private static bool IsLeverage(ActionContext context, Fact fact)
        {
            return fact != null
                   && fact.Subject == context.Target
                   && fact.Truth == TruthState.True
                   && fact.Secrecy > 0
                   && context.World.Knowledge.Knows(context.Actor, fact.Id)
                   && context.World.Knowledge.Knows(context.Target, fact.Id);
        }
    }

    /// <summary>
    /// Be taken for somebody you are not.
    ///
    /// Needs a prop and a stranger, and both are genuine impossibilities rather than long odds: a
    /// claim to be the notary's man with nothing on you to say so is not a hard performance, it is
    /// no performance, and somebody who knows your face is not going to be talked out of knowing it.
    /// That first requirement is where <see cref="ForgeAction"/> pays off - papers are exactly the
    /// prop this verb is short of.
    ///
    /// What it produces is whatever standing would have got you: something the target would only
    /// tell that person, or the run of a place that person could walk into.
    /// </summary>
    public sealed class ImpersonateAction : NarrativeAction
    {
        /// <summary>Past this, they know you too well for a costume to survive the first sentence.</summary>
        public const int KnowsYourFaceAt = 40;

        public ImpersonateAction() : base("impersonate", ActionFamily.Crime, "Pass yourself off")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target) || context.Target == context.Actor)
            {
                return Availability.NotRelevant("nobody here to take you for anyone");
            }

            if (Credentials(context) == null)
            {
                return Availability.Impossible("nothing about you says you are anyone else");
            }

            if (context.Vanilla.GetAffinity(context.Target) >= KnowsYourFaceAt)
            {
                return Availability.Impossible(context.NameOf(context.Target) + " knows your face too well");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor papers = Credentials(context);
            if (papers == null)
            {
                ActionOutcome bare = new ActionOutcome(Id, null, "You have nothing to show for the claim.");
                bare.Notes.Add("no credentials carried");
                return bare;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Deception, context.Actor, context.Target)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: false))
                .With(SituationalModifiers.Grudge(context));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    double confidence = check.Outcome == CheckOutcome.CriticalPass ? 0.9 : 0.7;
                    outcome = new ActionOutcome(Id, check, who + " reads the " + papers.Name + ", and treats you as the man it names.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.Deceived, context.Actor, context.Target, context.Now, 0.5, context.Zone,
                        evidence: new[] { papers.Id }, tags: new[] { EventTags.Unnoticed },
                        threadId: context.Thread?.Id ?? EntityId.None));

                    if (!Told(context, confidence, outcome) && !LetIn(context, outcome))
                    {
                        outcome.Notes.Add("they take you for him and have nothing that station would be given");
                    }

                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " does not take you for anyone in particular.");
                    outcome.Notes.Add("nothing recorded; you can try the story on somebody else");
                    break;

                default:
                {
                    // Seen through, in front of whoever is there - and now they know somebody is
                    // going about wearing other people's names.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome = new ActionOutcome(Id, check, who + " looks at the " + papers.Name + ", then at you, and asks who you really are.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.DeceptionExposed, context.Actor, context.Target, context.Now, 0.7, context.Zone,
                        witnesses: seen, evidence: new[] { papers.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    ActionSupport.WarnUnderInvestigation(
                        context, context.Target, context.Actor, outcome,
                        note: who + " knows you came to them under somebody else's name");
                    break;
                }
            }

            return outcome;
        }

        /// <summary>They tell you what they would tell the man on the paper.</summary>
        private static bool Told(ActionContext context, double confidence, ActionOutcome outcome)
        {
            EntityId learned = ActionSupport.FindTeachableFact(context);
            if (learned.IsNone)
            {
                return false;
            }

            // Hearsay and unprovable, like anything else said to you. A costume changes who they
            // think they are talking to, not what a second party would accept from you afterwards.
            context.World.Knowledge.Teach(
                context.Actor, learned, KnowledgeSource.Hearsay, confidence, context.Now, false, context.Target);
            outcome.Notes.Add("told, believing you had a right to it: " + ActionSupport.Describe(context, learned));
            return true;
        }

        /// <summary>Or they simply open the door the station would have opened.</summary>
        private static bool LetIn(ActionContext context, ActionOutcome outcome)
        {
            NarrativeSite site = ActionSupport.SiteHere(context);
            if (site == null || site.Admits(context.Actor))
            {
                return false;
            }

            site.Admit(context.Actor);
            outcome.Notes.Add("admitted to " + site.Name + " without breaking anything");
            return true;
        }

        /// <summary>
        /// Paper is what a stranger reads you by. Any document will do - the check decides whether
        /// it bears looking at, which is the difference between a precondition and a difficulty.
        /// </summary>
        private static ItemDescriptor Credentials(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, TraceMaterial.IsDocument);
        }
    }

    /// <summary>
    /// Start a fight.
    ///
    /// Deliberately thin: the mod does not resolve combat. Elin does that far better than a
    /// narrative layer could, so this records intent and lets the game decide the rest. Reading
    /// the result back in is the adapter's job.
    /// </summary>
    public sealed class AttackAction : NarrativeAction
    {
        public AttackAction() : base("attack", ActionFamily.Physical, "Attack")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to fight");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            ActionOutcome outcome = new ActionOutcome(Id, null, "You go for " + context.NameOf(context.Target) + ".");
            outcome.Events.Add(context.World.Record(WorldEventType.Attacked, context.Actor, context.Target, context.Now, 0.9, context.Zone, witnesses: seen));
            outcome.Notes.Add("no check: vanilla combat resolves this, and the adapter records how it ended");
            return outcome;
        }
    }
}
