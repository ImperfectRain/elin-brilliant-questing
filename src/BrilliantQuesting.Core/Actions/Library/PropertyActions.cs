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

        public static EntityId OwnerOf(ActionContext context, EntityId itemId)
        {
            foreach (KeyValuePair<EntityId, Fact> pair in context.World.Knowledge.Facts)
            {
                Fact fact = pair.Value;
                if (fact.Predicate == FactPredicates.Possesses && fact.Object == itemId && fact.Truth == TruthState.True)
                {
                    return fact.Subject;
                }
            }

            return EntityId.None;
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

        public override Availability GetAvailability(ActionContext context)
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

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = Ownership.FindOwnedBy(context, context.Target);
            context.Vanilla.TryTransferItem(item.Id, context.Actor, context.Target);

            ActionOutcome outcome = new ActionOutcome(Id, null, "You hand the " + item.Name + " back to " + context.NameOf(context.Target) + ".");
            outcome.Events.Add(context.World.Record(
                WorldEventType.ItemReturned,
                context.Actor,
                context.Target,
                context.Now,
                0.8,
                context.Zone,
                evidence: new[] { item.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));

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

        public override Availability GetAvailability(ActionContext context)
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

        public override ActionOutcome Perform(ActionContext context)
        {
            FindSomeoneElsesProperty(context, out ItemDescriptor item, out EntityId owner);

            ActionOutcome outcome = new ActionOutcome(Id, null, "You keep the " + item.Name + ".");

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
