using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Shared ownership lookups. Ownership is a fact in the graph, not a flag on an item.</summary>
    internal static class Ownership
    {
        /// <summary>The item in the actor's pack that the given person is recorded as owning.</summary>
        public static ItemDescriptor FindOwnedBy(ActionContext context, EntityId owner)
        {
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < carried.Count; i++)
            {
                ItemDescriptor item = carried[i];
                if (!context.SubjectItem.IsNone && item.Id != context.SubjectItem)
                {
                    continue;
                }

                if (OwnerOf(context, item.Id) == owner)
                {
                    return item;
                }
            }

            return null;
        }

        public static EntityId OwnerOf(ActionContext context, EntityId itemId) => OwnerOf(context.World, itemId);

        /// <summary>
        /// The same lookup without an action in hand, for readers - goal conditions among them -
        /// that ask who holds a thing outside any attempt. One authority, two callers.
        /// </summary>
        public static EntityId OwnerOf(World.NarrativeWorldState world, EntityId itemId)
        {
            Fact claim = ClaimOn(world, itemId);
            return claim == null ? EntityId.None : claim.Subject;
        }

        /// <summary>
        /// The standing claim that somebody holds this thing, rather than just their name.
        ///
        /// The same lookup one step earlier, for a caller that has to do something to the claim
        /// itself - observed possession changing hands supersedes it rather than editing who it
        /// names, because a claim that quietly changed its subject would take every belief, proof
        /// link and piece of evidence that referred to it along with it.
        /// </summary>
        public static Fact ClaimOn(World.NarrativeWorldState world, EntityId itemId)
        {
            if (world == null || itemId.IsNone)
            {
                return null;
            }

            foreach (KeyValuePair<EntityId, Fact> pair in world.Knowledge.Facts)
            {
                Fact fact = pair.Value;
                if (fact.Predicate == FactPredicates.Possesses && fact.Object == itemId && fact.Truth == TruthState.True)
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>
        /// Is there an undertaking between the actor and this person?
        ///
        /// Direction is deliberately ignored. An agreement to look into someone's stolen property
        /// binds both sides of the conversation - whether the record was written as the victim
        /// asking or the player accepting, keeping the thing afterwards is still a betrayal of it.
        /// </summary>
        public static bool HasUndertakingWith(ActionContext context, EntityId person)
        {
            foreach (WorldEvent worldEvent in context.World.Ledger.OfType(WorldEventType.PromiseMade))
            {
                bool between = (worldEvent.Actor == context.Actor && worldEvent.Target == person)
                               || (worldEvent.Actor == person && worldEvent.Target == context.Actor);
                if (between)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Hand back what you recovered. No roll: giving someone their own property is not a skill
    /// test, and Elin already has the mechanic - the item moves.
    /// </summary>
    public sealed class ReturnItemAction : NarrativeAction
    {
        public ReturnItemAction() : base("return_item", ActionFamily.Social, "Give it back")
        {
        }

        /// <summary>
        /// What it is for: the ownership record and the object agree again. The move itself is
        /// vanilla's, so a build that cannot transfer items is not offered this half at all.
        /// </summary>
        public override ActionEffects Effects => ActionEffects
            .Declaring(ActionEffect.Delegated(
                SemanticEffects.PossessionTransferred,
                "IVanillaState.TryTransferItem",
                VanillaCapability.TransferItems))
            .NeedingAnyOf(SemanticSlots.Item);

        /// <summary>A successful use of this ends the matter it was used inside (BQ-094).</summary>
        public override bool SettlesMatters => true;

        /// <summary>
        /// Success is the object actually being in their hands (BQa-013), and there is no second
        /// thing it could be: this verb has no roll, no bystander cost and nothing to reveal, so
        /// the transfer either happened or the attempt did not take place.
        ///
        /// That is the whole of the classification and it is load-bearing here. The body used to
        /// record <c>ItemReturned</c> and close the matter without looking at what vanilla said,
        /// so a build that would not move the item produced a resolved theft, a settled victim
        /// and an object still in the actor's pack - a label standing in for the state change it
        /// named. There is no failure class because there is no failure: nothing is attempted
        /// and nothing is lost.
        /// </summary>
        public override ActionPostconditions Postconditions => ActionPostconditions
            .Succeeding(SemanticEffects.PossessionTransferred);

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to give it to");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (Ownership.FindOwnedBy(context, context.Target) == null)
            {
                return Availability.Impossible("you are not carrying anything of theirs");
            }

            return Availability.Available();
        }

        protected override ActionOutcome PerformCore(ActionContext context)
        {
            ItemDescriptor item = Ownership.FindOwnedBy(context, context.Target);
            if (item == null)
            {
                ActionOutcome gone = new ActionOutcome(Id, null, "You are not carrying anything of theirs.");
                return gone.Refuse("nothing of " + context.NameOf(context.Target) + "'s is in the actor's keeping any more");
            }

            // Nothing below may run on a move that did not happen. Recording the return and
            // resolving the matter regardless is how an unsupported native write came to close a
            // theft with the ring still in the thief's pocket (BQa-013).
            if (!context.Vanilla.TryTransferItem(item.Id, context.Actor, context.Target))
            {
                ActionOutcome stuck = new ActionOutcome(Id, null, "The " + item.Name + " does not leave your hands.");
                return stuck.Refuse("transfer refused; the ownership record and the object still disagree");
            }

            ActionOutcome outcome = new ActionOutcome(Id, null, "You hand the " + item.Name + " back to " + context.NameOf(context.Target) + ".");
            outcome.Change(SemanticEffects.PossessionTransferred);
            // What this answers is the occurrence the matter began with, not the item. Reading it
            // off the ledger instead - the most recent theft naming this object - is the guess
            // BQa-001 rules out, and it gets the wrong answer the moment the same thing is stolen
            // twice. Outside a matter there is nothing recorded to point at, so it stays unknown.
            outcome.Events.Add(context.World.Record(
                WorldEventType.ItemReturned,
                context.Actor,
                context.Target,
                context.Now,
                0.8,
                context.Zone,
                evidence: new[] { item.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None,
                provenance: EventProvenance.Draft().About(context.Thread?.OriginEventId ?? EntityId.None).Build()));

            ActionSupport.Resolve(context, outcome, "property_returned", 0.8);

            return outcome;
        }
    }

    /// <summary>
    /// Keep it.
    ///
    /// Also no roll, and deliberately quiet: if nobody is watching, nobody's affinity moves and
    /// the world simply carries an act it has not noticed yet. It becomes a problem later, when
    /// someone finds out - which is the entire argument for a knowledge model. Quiet is not the
    /// same as unrecorded, though: the act goes into the ledger either way, tagged unnoticed, so
    /// that the discovery has something to be a discovery *of*.
    /// </summary>
    public sealed class KeepItemAction : NarrativeAction
    {
        public KeepItemAction() : base("keep_item", ActionFamily.Crime, "Keep it")
        {
        }

        /// <summary>
        /// Nothing moves and no ownership record changes - which is the point of the verb, and
        /// why it advances no possession effect however much property is involved. What it can
        /// change is how the owner stands with whoever kept it, once somebody finds out.
        /// </summary>
        public override ActionEffects Effects => ActionEffects
            .Declaring(ActionEffect.Recorded(SemanticEffects.StandingAltered));

        /// <summary>
        /// Deciding is the act (BQa-013), so it always succeeds and what it changes is how the
        /// owner stands with whoever kept their property - recorded now, felt whenever somebody
        /// finds out. There is no roll and so no failure: quiet is carried by the tag on the
        /// event, never by the outcome pretending nothing was decided.
        /// </summary>
        public override ActionPostconditions Postconditions => ActionPostconditions
            .Succeeding(SemanticEffects.StandingAltered);

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.ReadInventory))
            {
                return Availability.Impossible("inventory is unavailable on this build");
            }

            if (FindSomeoneElsesProperty(context, out _, out EntityId owner) && owner != context.Actor)
            {
                return Availability.Available();
            }

            return Availability.NotRelevant("nothing of anyone else's to keep");
        }

        protected override ActionOutcome PerformCore(ActionContext context)
        {
            if (!FindSomeoneElsesProperty(context, out ItemDescriptor item, out EntityId owner))
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing of anyone else's here to keep.");
                return nothing.Refuse("no item in the actor's keeping is recorded as somebody else's");
            }

            ActionOutcome outcome = new ActionOutcome(Id, null, "You keep the " + item.Name + ".");
            outcome.Change(SemanticEffects.StandingAltered);

            if (Ownership.HasUndertakingWith(context, owner))
            {
                outcome.Events.Add(context.World.Record(
                    WorldEventType.PromiseBroken,
                    context.Actor,
                    owner,
                    context.Now,
                    0.7,
                    context.Zone,
                    evidence: new[] { item.Id },
                    threadId: context.Thread?.Id ?? EntityId.None));
                outcome.Notes.Add(context.NameOf(owner) + " was promised this back");
            }
            else
            {
                outcome.Notes.Add("nobody was promised anything, so nobody has been let down");
            }

            // Keeping it is the moment the property changes hands for good, and from the owner's
            // side that is a theft whoever physically took it. The act belongs in history: without
            // it the situation could end - thread resolution and all - having recorded nothing at
            // all, leaving no memory for the owner, nothing for a rumour or an escalation to key
            // on, and a hole in the chronicle exactly where the ending should be.
            //
            // Quiet is preserved by the tag, not by the silence. Unnoticed keeps the world from
            // reacting to something nobody saw - the victim's affinity must not move, because
            // affinity moving is itself information - while the ledger still knows it happened,
            // which is what lets it surface later.
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            outcome.Events.Add(context.World.Record(
                WorldEventType.Theft,
                context.Actor,
                owner,
                context.Now,
                0.6,
                context.Zone,
                related: new[] { item.Id },
                witnesses: seen,
                evidence: new[] { item.Id },
                tags: seen.Count == 0 ? new[] { EventTags.Unnoticed } : null,
                threadId: context.Thread?.Id ?? EntityId.None));

            if (context.Thread != null)
            {
                context.Thread.Resolution = "property_kept";
                outcome.Notes.Add("thread resolution recorded: property kept");
            }

            return outcome;
        }

        private static bool FindSomeoneElsesProperty(ActionContext context, out ItemDescriptor item, out EntityId owner)
        {
            IReadOnlyList<ItemDescriptor> carried = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < carried.Count; i++)
            {
                ItemDescriptor candidate = carried[i];
                if (!context.SubjectItem.IsNone && candidate.Id != context.SubjectItem)
                {
                    continue;
                }

                EntityId candidateOwner = Ownership.OwnerOf(context, candidate.Id);
                if (!candidateOwner.IsNone && candidateOwner != context.Actor)
                {
                    item = candidate;
                    owner = candidateOwner;
                    return true;
                }
            }

            item = null;
            owner = EntityId.None;
            return false;
        }
    }
}
