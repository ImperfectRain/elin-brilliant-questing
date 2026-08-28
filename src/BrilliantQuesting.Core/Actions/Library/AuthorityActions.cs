using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>Bring a known claim to someone with standing to act on it.</summary>
    public sealed class ReportToAuthorityAction : NarrativeAction
    {
        public ReportToAuthorityAction() : base("report", ActionFamily.Social, "Report it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to report to");
            }

            if (AuthorityPolicy.RoleOf(context.TargetNpc) == AuthorityRole.None)
            {
                return Availability.NotRelevant("they have no authority here");
            }

            if (context.SubjectFact.IsNone)
            {
                return Availability.NotRelevant("nothing to report");
            }

            if (!context.World.Knowledge.Knows(context.Actor, context.SubjectFact))
            {
                return Availability.Impossible("you cannot report something you do not know");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            AuthorityDecision decision = AuthorityPolicy.Evaluate(context);
            Fact fact = context.World.Knowledge.GetFact(context.SubjectFact);
            string who = context.NameOf(context.Target);
            ActionOutcome outcome;

            switch (decision.Response)
            {
                case AuthorityResponse.Acts:
                    TeachAuthority(context, fact.Id, confidence: 0.95, copyProof: true);
                    outcome = new ActionOutcome(Id, null, who + " accepts the report and treats it as actionable.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.CrimeReported,
                        context.Actor,
                        fact.Subject,
                        context.Now,
                        0.9,
                        context.Zone,
                        new[] { fact.Id },
                        ActionSupport.Bystanders(context, true)));
                    outcome.Notes.Add("authority response: " + decision.Role + " acted on " + decision.Evidence);
                    break;

                case AuthorityResponse.OpensInquiry:
                    TeachAuthority(context, fact.Id, confidence: 0.65, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " will look into it, but will not act on your word alone.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.SecretRevealed,
                        context.Actor,
                        fact.Subject,
                        context.Now,
                        0.5,
                        context.Zone,
                        new[] { fact.Id }));
                    outcome.Notes.Add("authority response: inquiry opened without proof");
                    break;

                case AuthorityResponse.RejectsRumor:
                    TeachAuthority(context, fact.Id, confidence: 0.25, copyProof: false);
                    outcome = new ActionOutcome(Id, null, who + " files it as rumor and does nothing.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.RumorSpread,
                        context.Actor,
                        context.Target,
                        context.Now,
                        0.25,
                        context.Zone,
                        new[] { fact.Id }));
                    outcome.Notes.Add("authority response: rumor rejected");
                    break;

                case AuthorityResponse.Rebounds:
                    outcome = new ActionOutcome(Id, null, who + " refuses to act without proof, and the accusation rebounds.");
                    outcome.Events.Add(context.World.Record(
                        WorldEventType.FalseAccusation,
                        context.Actor,
                        fact.Subject,
                        context.Now,
                        0.6,
                        context.Zone,
                        new[] { fact.Id },
                        ActionSupport.Bystanders(context, true)));
                    WarnAccused(context, fact, outcome);
                    outcome.Notes.Add("authority response: accusation rebounded at " + decision.Evidence);
                    break;

                default:
                    outcome = new ActionOutcome(Id, null, who + " cannot act on this.");
                    outcome.Notes.Add("authority response: unavailable");
                    break;
            }

            return outcome;
        }

        private static void TeachAuthority(ActionContext context, EntityId factId, double confidence, bool copyProof)
        {
            context.World.Knowledge.TryGetBelief(context.Actor, factId, out KnowledgeRecord actorBelief);
            bool canProve = copyProof && actorBelief != null && actorBelief.CanProve;
            IReadOnlyList<ProofLink> proofs = canProve ? actorBelief.Proofs : null;
            context.World.Knowledge.Teach(
                context.Target,
                factId,
                canProve ? KnowledgeSource.Document : KnowledgeSource.Hearsay,
                confidence,
                context.Now,
                canProve,
                proofs,
                context.Actor);
        }

        private static void WarnAccused(ActionContext context, Fact fact, ActionOutcome outcome)
        {
            if (fact == null || fact.Subject.IsNone || fact.Subject == context.Actor)
            {
                return;
            }

            Fact accusation = context.World.Knowledge.FindFact(context.Actor, FactPredicates.Investigating);
            if (accusation == null)
            {
                accusation = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Investigating, fact.Subject);
                context.World.Knowledge.AddFact(accusation);
            }

            context.World.Knowledge.Teach(fact.Subject, accusation.Id, KnowledgeSource.Hearsay, 0.8, context.Now, false, context.Target);
            outcome.Notes.Add(context.NameOf(fact.Subject) + " learns you made an accusation");
        }
    }
}
