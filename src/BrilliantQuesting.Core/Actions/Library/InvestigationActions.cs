using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Turn over a place looking for the physical thing that proves what happened.
    ///
    /// This is the route that matters for a player with no social game at all: testimony can be
    /// denied, but a ledger in your pack cannot, and evidence found this way is provable.
    /// </summary>
    public sealed class SearchForEvidenceAction : NarrativeAction
    {
        public SearchForEvidenceAction() : base("search", ActionFamily.Information, "Search the scene")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing here worth searching for");
            }

            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            if (fact == null || fact.EvidenceIds.Count == 0)
            {
                return Availability.NotRelevant("this leaves no physical trace");
            }

            if (context.World.Knowledge.CanProve(context.Actor, context.SubjectFact))
            {
                return Availability.NotRelevant("you already have what you need");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Investigation, context.Actor, EntityId.None);
            request.WithModifier("how well it was hidden", fact.Secrecy / 20);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    bool recovered = TryRecoverEvidence(context, fact);
                    context.World.Knowledge.Teach(context.Actor, fact.Id, KnowledgeSource.Document, check.Outcome == CheckOutcome.CriticalPass ? 1.0 : 0.85, context.Now, recovered);
                    outcome = new ActionOutcome(Id, check, recovered
                        ? "You find it - and you can carry it out with you."
                        : "You find the trace, though there is nothing here you could show anyone.");
                    outcome.Notes.Add("learned: " + ActionSupport.Describe(context, fact.Id) + (recovered ? " (provable)" : " (unprovable)"));
                    outcome.Events.Add(context.World.Record(WorldEventType.SecretLearned, context.Actor, fact.Subject, context.Now, 0.4, context.Zone, new[] { fact.Id }));
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "Nothing. If there was anything here, you have missed it.");
                    break;

                default:
                    // Ransacking the place is itself a thing bystanders can report.
                    outcome = new ActionOutcome(Id, check, "You make a mess of the search, and someone notices you where you should not be.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Trespass, context.Actor, fact.Subject, context.Now, 0.4, context.Zone, witnesses: ActionSupport.Bystanders(context, true)));
                    break;
            }

            return outcome;
        }

        /// <summary>
        /// Evidence is an ordinary Elin item sitting somewhere. If it is loose at the scene the
        /// player simply picks it up; if someone is carrying it, finding it is not taking it.
        ///
        /// Only what is *here* can be picked up. Searching a room used to reach through the world
        /// to whoever the fact was about and take the object off them wherever they happened to
        /// be, which made every other way of getting hold of evidence - going where it is,
        /// following the person carrying it, lifting it out of a pocket - redundant, and quietly
        /// contradicted the trace the search was supposed to be reading. A search that finds the
        /// story but not the object still teaches the fact; it just cannot prove it.
        /// </summary>
        private static bool TryRecoverEvidence(ActionContext context, Fact fact)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return false;
            }

            for (int i = 0; i < fact.EvidenceIds.Count; i++)
            {
                EntityId evidenceId = fact.EvidenceIds[i];
                EntityId holder = EvidenceHolder(context, evidenceId, fact);
                if (!holder.IsNone && context.Vanilla.TryTransferItem(evidenceId, holder, context.Actor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whoever in this zone has the object, or nobody.</summary>
        private static EntityId EvidenceHolder(ActionContext context, EntityId evidenceId, Fact fact)
        {
            IReadOnlyList<EntityId> present = context.Vanilla.GetCharactersInZone(context.Zone);
            for (int i = 0; i < present.Count; i++)
            {
                if (Carries(context, present[i], evidenceId))
                {
                    return present[i];
                }
            }

            // A dead owner is not listed among the people in a zone, and a body with the evidence
            // still on it is the ordinary case for anything that killed somebody.
            if (!fact.Subject.IsNone
                && context.Vanilla.GetZoneOf(fact.Subject) == context.Zone
                && Carries(context, fact.Subject, evidenceId))
            {
                return fact.Subject;
            }

            return Carries(context, context.Zone, evidenceId) ? context.Zone : EntityId.None;
        }

        private static bool Carries(ActionContext context, EntityId owner, EntityId itemId)
        {
            if (owner.IsNone)
            {
                return false;
            }

            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(owner);
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Tell someone what you know.
    ///
    /// Whether it lands depends on whether you can prove it, who you are, and what your legal
    /// standing is. An unprovable accusation from a known criminal is not an accusation, it is a
    /// rumour - and it can rebound as one.
    /// </summary>
    public sealed class ExposeSecretAction : NarrativeAction
    {
        public ExposeSecretAction() : base("expose", ActionFamily.Social, "Tell them what you know")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to tell");
            }

            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing to reveal");
            }

            if (!context.World.Knowledge.Knows(context.Actor, context.SubjectFact))
            {
                return Availability.Impossible("you cannot reveal something you do not know");
            }

            if (context.World.Knowledge.BelievesConfidently(context.Target, context.SubjectFact))
            {
                return Availability.NotRelevant("they already know");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId factId = context.SubjectFact;
            Fact fact = context.World.Knowledge.GetFact(factId);
            bool canProve = context.World.Knowledge.CanProve(context.Actor, factId);

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Credibility, context.Actor, context.Target)
                .With(SituationalModifiers.Rapport(context))
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: true))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: false));

            // Proof is worth more than charm. This is the term that makes burglary a social tool.
            request.WithModifier(canProve ? "you can prove it" : "your word alone", canProve ? -8 : 4);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    double confidence = check.Outcome == CheckOutcome.CriticalPass ? 1.0 : 0.8;
                    context.World.Knowledge.Teach(context.Target, factId, canProve ? KnowledgeSource.Document : KnowledgeSource.Hearsay, confidence, context.Now, canProve, context.Actor);
                    outcome = new ActionOutcome(Id, check, who + " believes you.");
                    outcome.Events.Add(context.World.Record(WorldEventType.SecretRevealed, context.Actor, fact.Subject, context.Now, canProve ? 0.9 : 0.6, context.Zone, new[] { factId }, seen));
                    outcome.Notes.Add(who + " now " + (canProve ? "can prove it too" : "believes it but cannot prove it"));
                    break;
                }

                case CheckOutcome.Fail:
                    // Both halves of this branch used to assume the player had nothing to show.
                    // A tester with proof in hand was told, twice, to go and find evidence - the
                    // one piece of advice that could not help, and the same class of lying
                    // message BQ-009 was about. Somebody who produced proof and was still not
                    // believed has a different problem, and a different route out of it.
                    outcome = new ActionOutcome(Id, check, canProve
                        ? who + " looks at what you have and still will not have it."
                        : who + " does not take your word for it.");
                    outcome.Notes.Add(canProve
                        ? "proof was not the problem; try someone who already half suspects, or make them owe you first"
                        : "try again with evidence, or find someone who already half suspects");
                    break;

                default:
                {
                    // The accusation rebounds: you have announced your interest to your target.
                    outcome = new ActionOutcome(Id, check, who + " thinks you are inventing it - and word gets back.");
                    outcome.Events.Add(context.World.Record(WorldEventType.FalseAccusation, context.Actor, fact.Subject, context.Now, 0.5, context.Zone, new[] { factId }, seen));
                    ActionSupport.WarnUnderInvestigation(
                        context, fact.Subject, context.Target, outcome,
                        note: context.NameOf(fact.Subject) + " learns you are accusing them");

                    break;
                }
            }

            return outcome;
        }
    }
}
