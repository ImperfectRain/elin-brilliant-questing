using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Who in the world will do criminal work for you, and why they would.
    ///
    /// This is where `MD 13.1` and `PM 67` are actually implemented, and the shape of the rule is
    /// the whole point of it. Thieves Guild rank and Karma do not gate picking a lock, breaking a
    /// winch or leaning on somebody - those are your own hands and always available, however badly
    /// they would go. What standing gates is whether a receiver, a forger or a carrier will deal
    /// with you at all, because that is not a skill the player has, it is a decision somebody else
    /// makes about a stranger.
    ///
    /// The practical consequence is the one BQ-025 is measured by: a criminal build has routes
    /// through the world that a lawful one cannot walk, and the difference is not that the lawful
    /// character rolls worse. It is that nobody in the trade knows them.
    ///
    /// Three ways in, because a trade this old does not recognise only one credential:
    /// a guild card, a name the law already knows, or being personally vouched for by long dealing.
    /// </summary>
    public static class UnderworldPolicy
    {
        /// <summary>Roles anybody - adapter, situation, organization - can grant, exactly like authority.</summary>
        public const string FenceRole = "fence";
        public const string ForgerRole = "forger";
        public const string SmugglerRole = "smuggler";

        /// <summary>At or below this, the trade has heard of you without an introduction.</summary>
        public const int KnownToTheTrade = -20;

        /// <summary>Affinity at which a contact deals with you because it is you, credentials or not.</summary>
        public const int VouchedForAt = 50;

        public static IReadOnlyList<string> UnderworldRoles { get; } =
            new List<string> { FenceRole, ForgerRole, SmugglerRole };

        /// <summary>
        /// The person here who does this kind of work, or nobody.
        ///
        /// An explicitly named target wins when they hold the role; otherwise the zone is walked in
        /// order, so the same alley yields the same contact twice.
        /// </summary>
        public static EntityId FindContact(ActionContext context, string role)
        {
            if (Holds(context, context.Target, role))
            {
                return context.Target;
            }

            if (!context.Target.IsNone)
            {
                return EntityId.None;
            }

            IReadOnlyList<EntityId> present = context.Vanilla.GetCharactersInZone(context.Zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i] != context.Actor && Holds(context, present[i], role))
                {
                    return present[i];
                }
            }

            return EntityId.None;
        }

        /// <summary>Whether this contact will take work from this actor at all.</summary>
        public static bool WillDealWith(ActionContext context, EntityId contact)
        {
            if (contact.IsNone)
            {
                return false;
            }

            return context.Vanilla.IsGuildMember(GuildId.Thieves)
                   || context.Vanilla.Karma <= KnownToTheTrade
                   || context.Vanilla.GetAffinity(contact) >= VouchedForAt;
        }

        /// <summary>
        /// The one place the contact requirement is turned into an availability answer, so all
        /// three verbs refuse in the same words and for the same reasons.
        ///
        /// Being turned away is <see cref="Availability.Impossible"/> rather than merely
        /// irrelevant, and deliberately so: `PM 62` files "invoke guild authority without
        /// membership" under impossible, and this is the same shape. It is not a hard attempt that
        /// might come off. There is no attempt.
        /// </summary>
        public static Availability Reach(ActionContext context, string role, out EntityId contact)
        {
            contact = FindContact(context, role);
            if (contact.IsNone)
            {
                return Availability.NotRelevant("nobody here does that kind of work");
            }

            if (!WillDealWith(context, contact))
            {
                return Availability.Impossible(
                    context.NameOf(contact) + " does not do that kind of work for people like you");
            }

            return Availability.Available();
        }

        private static bool Holds(ActionContext context, EntityId who, string role)
        {
            if (!ActionSupport.OnDuty(context, who))
            {
                return false;
            }

            NarrativeNpc npc = context.World.Registry.GetNpc(who);
            return npc != null && npc.Roles.Contains(role);
        }
    }

    /// <summary>
    /// Turn something into money through somebody who does not ask where it came from.
    ///
    /// The interesting half is not the orens, it is that the object leaves. Evidence you sold is
    /// evidence you cannot produce, and a player who fences the ledger for a good price has
    /// converted a case into a purse - which is a real choice, and an irreversible one, because
    /// the receiver is not going to hand it back.
    /// </summary>
    public sealed class FenceGoodsAction : NarrativeAction
    {
        public FenceGoodsAction() : base("fence", ActionFamily.Crime, "Move it through the trade")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems)
                || !context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("goods and money cannot both change hands on this build");
            }

            Availability reach = UnderworldPolicy.Reach(context, UnderworldPolicy.FenceRole, out EntityId contact);
            if (!reach.IsAvailable)
            {
                return reach;
            }

            ItemDescriptor goods = Goods(context);
            if (goods == null)
            {
                return Availability.NotRelevant("you are carrying nothing worth moving");
            }

            return context.Vanilla.GetMoney(contact) <= 0
                ? Availability.Impossible(context.NameOf(contact) + " has nothing to pay you with")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId contact = UnderworldPolicy.FindContact(context, UnderworldPolicy.FenceRole);
            ItemDescriptor goods = Goods(context);
            if (contact.IsNone || goods == null)
            {
                ActionOutcome nobody = new ActionOutcome(Id, null, "There is no one here to take it off you.");
                nobody.Notes.Add("no fence in reach, or nothing to sell");
                return nobody;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Fencing, context.Actor, contact)
                .With(SituationalModifiers.Rapport(context));
            request.WithModifier("how recognisable it is", goods.Value / 400);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(contact);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    // A fraction of the real vanilla price. Fences are not charities, and the
                    // discount is the standing cost of not being able to use a shop.
                    int offer = goods.Value * (check.Outcome == CheckOutcome.CriticalPass ? 6 : 4) / 10;
                    if (!context.Vanilla.TryTransferItem(goods.Id, context.Actor, contact))
                    {
                        outcome = new ActionOutcome(Id, check, "The " + goods.Name + " will not leave your hands.");
                        outcome.Notes.Add("transfer refused; no money changed hands");
                        return outcome;
                    }

                    bool paid = offer > 0 && context.Vanilla.TrySpendMoney(contact, context.Actor, offer);
                    outcome = new ActionOutcome(Id, check, paid
                        ? who + " takes the " + goods.Name + " and counts out " + offer + " orens without a word about it."
                        : who + " takes the " + goods.Name + " and owes you for it.");

                    outcome.Events.Add(context.World.Record(
                        WorldEventType.ItemGiven, context.Actor, contact, context.Now, 0.4, context.Zone,
                        evidence: new[] { goods.Id }, tags: new[] { EventTags.Unnoticed },
                        threadId: context.Thread?.Id ?? EntityId.None));

                    LoseProof(context, goods, outcome);
                    outcome.Notes.Add(paid ? "sold for " + offer + " orens" : "handed over; the payment did not come");
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " turns the " + goods.Name + " over and names a price you will not take.");
                    outcome.Notes.Add("no deal; the object is still yours");
                    break;

                default:
                {
                    // The deal itself is what goes wrong, in front of people. Nothing is
                    // confiscated - what you have lost is that it was quiet.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome = new ActionOutcome(Id, check, "The haggling goes on too long and too loudly, and you are still holding the " + goods.Name + ".");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.CrimeWitnessed, context.Actor, contact, context.Now, 0.6, context.Zone,
                        witnesses: seen, evidence: new[] { goods.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add(seen.Count + " witness(es) saw you trying to move it");
                    break;
                }
            }

            return outcome;
        }

        /// <summary>
        /// The object is somewhere else now, so this person cannot walk anybody through it. Their
        /// belief is untouched - they know exactly what they sold and to whom.
        /// </summary>
        private static void LoseProof(ActionContext context, ItemDescriptor goods, ActionOutcome outcome)
        {
            foreach (Fact fact in context.World.Knowledge.FactsEvidencedBy(new[] { goods.Id }))
            {
                if (context.World.Knowledge.CanProve(context.Actor, fact.Id))
                {
                    outcome.Notes.Add("you can no longer show: " + ActionSupport.Describe(context, fact.Id));
                }
            }

            context.World.Knowledge.RevokeProofOfItem(context.Actor, goods.Id);
        }

        private static ItemDescriptor Goods(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, item => item.Value > 0);
        }
    }

    /// <summary>
    /// Have a document made to say what you need it to say.
    ///
    /// Modelled as work done on a paper you brought rather than a paper conjured out of nothing,
    /// which is both truer and much cheaper: forging is copying a hand and a seal, so a specimen
    /// of both is the raw material, and Core never has to ask the game to create an object it
    /// would then have to describe.
    ///
    /// What it produces is proof of something you already believed - the manufactured evidence
    /// that closes a case nothing honest could close. Note what it does *not* do: it does not make
    /// the claim true. `extorting` is true here or it is not, independently, and the doctrine that
    /// truth, belief, proof and institutional judgment are four different things is exactly what
    /// lets a player get a guard to act on a true claim by way of a false paper.
    ///
    /// And it can come apart. The forgery is minted as its own true fact with the paper as its
    /// evidence, so somebody who works hard enough at that document can prove it was made. How hard
    /// is the check's own outcome, and the threshold it is set against is
    /// <see cref="ReadDocumentAction.ObscuredAt"/>: a rushed job sits below it and anybody who
    /// sits down and reads the thing will notice, while a clean one is buried well past it and
    /// only comes apart under `translate`. Reusing that constant rather than inventing a second
    /// one is the point - it is already the line between writing you can read and writing that is
    /// a problem in itself, and a forgery is exactly the second kind.
    /// </summary>
    public sealed class ForgeAction : NarrativeAction
    {
        /// <summary>What a forger charges, per point of how well-kept the claim is.</summary>
        private const int OrensPerSecrecyPoint = 8;

        public ForgeAction() : base("forge", ActionFamily.Crime, "Have papers made")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            Availability reach = UnderworldPolicy.Reach(context, UnderworldPolicy.ForgerRole, out EntityId contact);
            if (!reach.IsAvailable)
            {
                return reach;
            }

            if (Exemplar(context) == null)
            {
                return Availability.Impossible("you have nothing written in the hand you want copied");
            }

            Fact claim = Claim(context);
            if (claim == null)
            {
                return Availability.NotRelevant("there is nothing you believe and cannot show");
            }

            int price = Price(claim);
            return context.Vanilla.GetMoney(context.Actor) < price
                ? Availability.Impossible("you cannot pay the " + price + " orens this would cost")
                : Availability.Available("costs " + price + " orens");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId contact = UnderworldPolicy.FindContact(context, UnderworldPolicy.ForgerRole);
            ItemDescriptor exemplar = Exemplar(context);
            Fact claim = Claim(context);
            if (contact.IsNone || exemplar == null || claim == null)
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is nothing here to have made.");
                nothing.Notes.Add("no forger in reach, no exemplar, or nothing worth forging");
                return nothing;
            }

            int price = Price(claim);
            if (!context.Vanilla.TrySpendMoney(context.Actor, contact, price))
            {
                ActionOutcome broke = new ActionOutcome(Id, null, "He names his price, and you cannot meet it.");
                broke.Notes.Add("payment of " + price + " orens failed");
                return broke;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Fabrication, context.Actor, contact);

            // How closely the claim will be read is how hard it is to make it hold up.
            request.WithModifier("how closely it will be read", claim.Secrecy / 20);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(contact);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    bool clean = check.Outcome == CheckOutcome.CriticalPass;

                    // The paper now substantiates the claim, for anybody who comes to hold it.
                    if (!claim.EvidenceIds.Contains(exemplar.Id))
                    {
                        claim.EvidenceIds.Add(exemplar.Id);
                    }

                    context.World.Knowledge.Teach(
                        context.Actor, claim.Id, KnowledgeSource.Document, clean ? 0.95 : 0.85, context.Now, true,
                        new[] { new ProofLink(ProofKind.PhysicalEvidence, exemplar.Id) });

                    Fact forgery = new Fact(
                        context.World.NewId("fact"), contact, FactPredicates.Forged, exemplar.Id, exemplar.Name,
                        TruthState.True, secrecy: clean ? 95 : 55);
                    forgery.EvidenceIds.Add(exemplar.Id);
                    context.World.Knowledge.AddFact(forgery);
                    context.World.Knowledge.Teach(contact, forgery.Id, KnowledgeSource.Participant, 1.0, context.Now, false);
                    context.World.Knowledge.Teach(context.Actor, forgery.Id, KnowledgeSource.Participant, 1.0, context.Now, false);

                    outcome = new ActionOutcome(Id, check, clean
                        ? who + " hands back the " + exemplar.Name + ", and there is nothing in it to argue with."
                        : who + " hands back the " + exemplar.Name + ", and it will pass unless somebody sits down with it.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.EvidenceCreated, context.Actor, claim.Subject, context.Now, 0.7, context.Zone,
                        new[] { claim.Id, forgery.Id }, evidence: new[] { exemplar.Id },
                        tags: new[] { EventTags.Unnoticed },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    outcome.Notes.Add("now provable on paper: " + ActionSupport.Describe(context, claim.Id));
                    outcome.Notes.Add("the forgery is itself a true fact at secrecy " + forgery.Secrecy
                                      + "; the paper can be worked out for what it is");
                    outcome.Notes.Add("paid " + price + " orens");
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " works at it, gives up, and keeps the fee.");
                    outcome.Notes.Add("paid " + price + " orens for nothing; the " + exemplar.Name + " is unchanged");
                    break;

                default:
                {
                    // The raw material is destroyed. The problem this creates is a real one: the
                    // specimen took getting hold of, and there is not another one lying about.
                    bool ruined = context.Vanilla.Supports(VanillaCapability.DestroyItems)
                                  && context.Vanilla.TryDestroyItem(exemplar.Id, context.Actor);
                    outcome = new ActionOutcome(Id, check, ruined
                        ? who + " ruins the " + exemplar.Name + " outright, and shrugs."
                        : who + " makes a mess of it and hands back something nobody would look at twice.");
                    if (ruined)
                    {
                        context.World.Knowledge.RevokeProofOfItem(exemplar.Id);
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.EvidenceDestroyed, context.Actor, contact, context.Now, 0.5, context.Zone,
                            evidence: new[] { exemplar.Id }, tags: new[] { EventTags.Unnoticed },
                            threadId: context.Thread?.Id ?? EntityId.None));
                        outcome.Notes.Add("the exemplar is gone, and anything it proved went with it");
                    }

                    outcome.Notes.Add("paid " + price + " orens");
                    break;
                }
            }

            return outcome;
        }

        private static int Price(Fact claim) => 100 + (claim.Secrecy * OrensPerSecrecyPoint);

        /// <summary>A specimen of the hand and seal being copied: any document you are carrying.</summary>
        private static ItemDescriptor Exemplar(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, TraceMaterial.IsDocument);
        }

        /// <summary>
        /// Something you believe and cannot demonstrate. That gap is the entire market for this.
        ///
        /// Resolved to the best-kept candidate rather than the first the store yields, so the same
        /// world quotes the same fee twice, and never to a claim already superseded.
        /// </summary>
        private static Fact Claim(ActionContext context)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                return IsForgeable(context, named) ? named : null;
            }

            Fact best = null;
            foreach (KnowledgeRecord belief in context.World.Knowledge.BeliefsOf(context.Actor))
            {
                Fact fact = context.World.Knowledge.GetFact(belief.FactId);
                if (!IsForgeable(context, fact))
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

        private static bool IsForgeable(ActionContext context, Fact fact)
        {
            return fact != null
                   && fact.Truth != TruthState.Superseded
                   && context.World.Knowledge.Knows(context.Actor, fact.Id)
                   && !context.World.Knowledge.CanProve(context.Actor, fact.Id);
        }
    }

    /// <summary>
    /// Put a thing on a road nobody watches.
    ///
    /// The verb for reaching somebody you are not standing next to. Every other way of handing an
    /// object over needs both parties in the same room, which is exactly the constraint that makes
    /// a watched town, a gaol or a quarantined quarter a problem - and this is the answer to it.
    ///
    /// It costs the sender their proof, like fencing does, because the object is no longer theirs
    /// to produce. And on a bad run it costs the object: goods taken on the road do not come back.
    /// </summary>
    public sealed class SmuggleAction : NarrativeAction
    {
        public SmuggleAction() : base("smuggle", ActionFamily.Crime, "Get it to them")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (!ActionSupport.Present(context, context.ThirdParty))
            {
                return Availability.NotRelevant("nobody chosen to receive it");
            }

            if (context.Vanilla.GetZoneOf(context.ThirdParty) == context.Zone)
            {
                return Availability.NotRelevant("they are standing right there; hand it to them");
            }

            Availability reach = UnderworldPolicy.Reach(context, UnderworldPolicy.SmugglerRole, out EntityId _);
            if (!reach.IsAvailable)
            {
                return reach;
            }

            return Cargo(context) == null
                ? Availability.NotRelevant("you are carrying nothing to send")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId carrier = UnderworldPolicy.FindContact(context, UnderworldPolicy.SmugglerRole);
            ItemDescriptor cargo = Cargo(context);
            if (carrier.IsNone || cargo == null || context.ThirdParty.IsNone)
            {
                ActionOutcome nothing = new ActionOutcome(Id, null, "There is no one here to carry anything anywhere.");
                nothing.Notes.Add("no smuggler in reach, no cargo, or no recipient");
                return nothing;
            }

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Smuggling, context.Actor, carrier)
                .With(SituationalModifiers.Rapport(context));
            request.WithModifier("how recognisable it is", cargo.Value / 400);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string recipient = context.NameOf(context.ThirdParty);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    if (!context.Vanilla.TryTransferItem(cargo.Id, context.Actor, context.ThirdParty))
                    {
                        outcome = new ActionOutcome(Id, check, "The " + cargo.Name + " does not go anywhere.");
                        outcome.Notes.Add("transfer refused; the cargo is still yours");
                        return outcome;
                    }

                    outcome = new ActionOutcome(Id, check, "The " + cargo.Name + " reaches " + recipient + ", and no one who would have stopped it ever saw it.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.ItemGiven, context.Actor, context.ThirdParty, context.Now, 0.4, context.Zone,
                        evidence: new[] { cargo.Id }, tags: new[] { EventTags.Unnoticed },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    context.World.Knowledge.RevokeProofOfItem(context.Actor, cargo.Id);
                    outcome.Notes.Add("delivered across zones unobserved; you can no longer produce it yourself");
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, context.NameOf(carrier) + " will not put the " + cargo.Name + " on the road this week.");
                    outcome.Notes.Add("no run made; the cargo is still yours");
                    break;

                default:
                {
                    // Taken on the road. Whether that was bad luck or the carrier is not something
                    // the sender is ever going to find out.
                    bool lost = context.Vanilla.Supports(VanillaCapability.DestroyItems)
                                && context.Vanilla.TryDestroyItem(cargo.Id, context.Actor);
                    outcome = new ActionOutcome(Id, check, lost
                        ? "The " + cargo.Name + " goes out and does not arrive, and nobody can tell you where it stopped."
                        : "The run is called off, badly, and half the alley knows what you were sending.");

                    if (lost)
                    {
                        context.World.Knowledge.RevokeProofOfItem(cargo.Id);
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.EvidenceDestroyed, context.Actor, context.ThirdParty, context.Now, 0.6, context.Zone,
                            evidence: new[] { cargo.Id }, threadId: context.Thread?.Id ?? EntityId.None));
                        outcome.Notes.Add("the cargo is gone, and anything it proved went with it");
                    }
                    else
                    {
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.CrimeWitnessed, context.Actor, carrier, context.Now, 0.6, context.Zone,
                            witnesses: ActionSupport.Bystanders(context, true), evidence: new[] { cargo.Id },
                            threadId: context.Thread?.Id ?? EntityId.None));
                    }

                    break;
                }
            }

            return outcome;
        }

        private static ItemDescriptor Cargo(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor);
        }
    }
}
