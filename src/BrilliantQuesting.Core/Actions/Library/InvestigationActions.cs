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
                EntityId holder = context.SubjectItem.IsNone ? context.Zone : context.SubjectItem;
                if (context.Vanilla.TryTransferItem(evidenceId, holder, context.Actor))
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
                    outcome = new ActionOutcome(Id, check, who + " does not take your word for it.");
                    outcome.Notes.Add("try again with evidence, or find someone who already half suspects");
                    break;

                default:
                {
                    // The accusation rebounds: you have announced your interest to your target.
                    outcome = new ActionOutcome(Id, check, who + " thinks you are inventing it - and word gets back.");
                    outcome.Events.Add(context.World.Record(WorldEventType.FalseAccusation, context.Actor, fact.Subject, context.Now, 0.5, context.Zone, new[] { factId }, seen));
                    if (!fact.Subject.IsNone && context.World.Registry.GetNpc(fact.Subject) != null)
                    {
                        Fact investigating = context.World.Knowledge.FindFact(context.Actor, FactPredicates.Investigating);
                        if (investigating == null)
                        {
                            investigating = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Investigating, fact.Subject);
                            context.World.Knowledge.AddFact(investigating);
                        }

                        context.World.Knowledge.Teach(fact.Subject, investigating.Id, KnowledgeSource.Hearsay, 0.8, context.Now, false, context.Target);
                        outcome.Notes.Add(context.NameOf(fact.Subject) + " learns you are accusing them");
                    }

                    break;
                }
            }

            return outcome;
        }
    }
}
